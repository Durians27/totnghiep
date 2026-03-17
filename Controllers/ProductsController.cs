using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VelvySkinWeb.Data;
using VelvySkinWeb.Models;
using Microsoft.AspNetCore.Authorization;
namespace VelvySkinWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // 1. READ: Xem danh sách Sản phẩm (Hàm này đã bị Git xóa mất nên mới báo 404)
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products.Include(p => p.Category).ToListAsync();
            return View(products);
        }

        // 2. CREATE: Thêm Sản phẩm 
        public IActionResult Create()
        {
            // Lấy danh sách Danh mục từ DB đẩy ra hộp Dropdown
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Slug,Price,StockQuantity,Description,ImageUrl,IsActive,CategoryId,BrandId,ImageFile")] Product product)
        {
            if (ModelState.IsValid)
            {
                if (product.ImageFile != null)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + product.ImageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await product.ImageFile.CopyToAsync(fileStream);
                    }
                    product.ImageUrl = "/images/" + uniqueFileName;
                }
                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index)); 
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // 3. UPDATE: Sửa Sản phẩm
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }
// API Gợi ý tìm kiếm (Live Search)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> SearchSuggest(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) 
                return Json(new List<object>()); // Khách chưa gõ gì thì trả về rỗng

            // Tìm tên sản phẩm có chứa chữ khách gõ, lấy tối đa 5 sản phẩm đưa lên gợi ý
            var results = await _context.Products
                .Where(p => p.Name.Contains(keyword))
                .Select(p => new {
                    id = p.Id,
                    name = p.Name,
                    price = p.Price,
                    imageUrl = p.ImageUrl
                })
                .Take(5)
                .ToListAsync();

            return Json(results); // Trả về dạng JSON
        }
       [HttpPost]
        [ValidateAntiForgeryToken]
        // ĐÃ CẤP QUYỀN CHO 4 CỘT MỚI ĐƯỢC PHÉP LƯU XUỐNG DB
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Price,StockQuantity,CategoryId,ImageUrl,ShortDescription,FullDescription,Ingredients,UsageInstructions")] Product product)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(product);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // 4. DELETE: Xóa Sản phẩm
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var product = await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(m => m.Id == id);
            if (product == null) return NotFound();
            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    string imagePath = Path.Combine(_webHostEnvironment.WebRootPath, product.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath)) System.IO.File.Delete(imagePath);
                }
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
[AllowAnonymous]
        public async Task<IActionResult> Haircare(string sortOrder)
        {
            var allProducts = await _context.Products.Include(p => p.Category).ToListAsync();

            // Lọc các từ khóa liên quan đến Tóc
            var hairKeywords = new[] { "tóc", "gội", "xả", "dưỡng tóc", "mặt nạ tóc", "xịt", "hair" };
            
            var hairProducts = allProducts.Where(p => 
                p.Category != null && 
                hairKeywords.Any(keyword => p.Category.Name.ToLower().Contains(keyword))
            ).ToList();

            // Logic Sắp xếp
            switch (sortOrder)
            {
                case "price_asc":
                    hairProducts = hairProducts.OrderBy(p => p.Price).ToList();
                    break;
                case "price_desc":
                    hairProducts = hairProducts.OrderByDescending(p => p.Price).ToList();
                    break;
                case "popular":
                    hairProducts = hairProducts.OrderBy(p => p.Name).ToList();
                    break;
                default:
                    hairProducts = hairProducts.OrderByDescending(p => p.Id).ToList();
                    break;
            }

            ViewBag.PageTitle = "Chăm Sóc Tóc";
            ViewBag.PageDescription = "Lộ trình hoàn hảo cho mái tóc khỏe mạnh và óng ả";
            ViewBag.CurrentSort = sortOrder;

            return View(hairProducts); 
        }
        [AllowAnonymous]
        public async Task<IActionResult> Shampoo(string sortOrder)
        {
            var allProducts = await _context.Products.Include(p => p.Category).ToListAsync();

            // Màng lọc chỉ lôi cổ "gội" và "xả" ra
            var shampooKeywords = new[] { "gội", "xả", "shampoo", "conditioner" };
            
            var shampooProducts = allProducts.Where(p => 
                p.Category != null && 
                shampooKeywords.Any(keyword => p.Category.Name.ToLower().Contains(keyword))
            ).ToList();

            // Logic Sắp xếp
            switch (sortOrder)
            {
                case "price_asc":
                    shampooProducts = shampooProducts.OrderBy(p => p.Price).ToList();
                    break;
                case "price_desc":
                    shampooProducts = shampooProducts.OrderByDescending(p => p.Price).ToList();
                    break;
                case "popular":
                    shampooProducts = shampooProducts.OrderBy(p => p.Name).ToList();
                    break;
                default:
                    shampooProducts = shampooProducts.OrderByDescending(p => p.Id).ToList();
                    break;
            }

            ViewBag.PageTitle = "Dầu Gội & Dầu Xả";
            ViewBag.PageDescription = "Làm sạch sâu, nuôi dưỡng chân tóc chắc khỏe";
            ViewBag.CurrentSort = sortOrder;
            
            // 2 DÒNG MỚI THÊM VÀO ĐÂY:
            ViewBag.IsShampooPage = true; 

            // Thay vì dùng file Shampoo.cshtml, ta xài chung file Haircare luôn cho đỡ phải code lại
            return View("Haircare", shampooProducts);
        }
        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Skincare(string sortOrder)
        {
            // Lấy toàn bộ hàng hóa kèm Danh mục
            var allProducts = await _context.Products.Include(p => p.Category).ToListAsync();

            // MÀNG LỌC TỪ KHÓA THÔNG MINH: Tránh lỗi sai chính tả/viết hoa từ Database
            var skincareKeywords = new[] { "rửa mặt", "tẩy", "serum", "đặc trị", "kem", "dưỡng", "nắng" };
            
            var skincareProducts = allProducts.Where(p => 
                p.Category != null && 
                skincareKeywords.Any(keyword => p.Category.Name.ToLower().Contains(keyword))
            ).ToList();

            // Bắt đầu logic Sắp xếp cho trang Tổng
            switch (sortOrder)
            {
                case "price_asc":
                    skincareProducts = skincareProducts.OrderBy(p => p.Price).ToList();
                    break;
                case "price_desc":
                    skincareProducts = skincareProducts.OrderByDescending(p => p.Price).ToList();
                    break;
                case "popular":
                    skincareProducts = skincareProducts.OrderBy(p => p.Name).ToList();
                    break;
                default:
                    skincareProducts = skincareProducts.OrderByDescending(p => p.Id).ToList();
                    break;
            }

            // Gửi dữ liệu ra View
            ViewBag.PageTitle = "Chăm Sóc Da";
            ViewBag.PageDescription = "Lộ trình hoàn hảo cho làn da rạng rỡ mỗi ngày";
            ViewBag.CurrentSort = sortOrder; // Giữ trạng thái của nút Sắp xếp

            return View(skincareProducts); 
        }
        [AllowAnonymous]
        public async Task<IActionResult> CategoryDetail(int id, string sortOrder)
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id);
            if (category == null) return NotFound();

            IQueryable<Product> productsQuery = _context.Products
                .Where(p => p.CategoryId == id)
                .Include(p => p.Category);

            // Bắt đầu logic Sắp xếp
            switch (sortOrder)
            {
                case "price_asc":
                    productsQuery = productsQuery.OrderBy(p => p.Price);
                    break;
                case "price_desc":
                    productsQuery = productsQuery.OrderByDescending(p => p.Price);
                    break;
                case "popular":
                    productsQuery = productsQuery.OrderBy(p => p.Name); 
                    break;
                default:
                    productsQuery = productsQuery.OrderByDescending(p => p.Id); 
                    break;
            }

            var products = await productsQuery.ToListAsync();

            ViewBag.PageTitle = category.Name; 
            ViewBag.PageDescription = category.Description;
            ViewBag.CurrentSort = sortOrder;

            // =========================================================
            // PHÂN LUỒNG GIAO DIỆN BẰNG TỪ KHÓA (Cảnh sát giao thông)
            // =========================================================
         string catName = category.Name.ToLower();

            // 1. Nhánh TÓC
            if (catName.Contains("gội") || catName.Contains("xả") || catName.Contains("tóc") || catName.Contains("tinh dầu") || catName.Contains("xịt"))
            {
                if (catName.Contains("gội") || catName.Contains("xả")) ViewBag.IsShampooPage = true; 
                else if (catName.Contains("tinh dầu")) ViewBag.IsHairOilPage = true;
                else if (catName.Contains("xịt")) ViewBag.IsHairSprayPage = true;
                
                return View("Haircare", products); 
            }
            // 2. Nhánh TRANG ĐIỂM (Mới thêm)
            else if (catName.Contains("trang điểm") || catName.Contains("son") || catName.Contains("phấn") || catName.Contains("má hồng"))
            {
                return View("Makeup", products); 
            }

            return View("Skincare", products);
            
            return View("Skincare", products);
        }
   
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category) // Kéo theo tên danh mục
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null) return NotFound();

            return View(product);
        }
        [AllowAnonymous]
        public async Task<IActionResult> Makeup(string sortOrder)
        {
            var allProducts = await _context.Products.Include(p => p.Category).ToListAsync();

            // Màng lọc các từ khóa thuộc nhánh Trang điểm
            var makeupKeywords = new[] { "trang điểm", "son", "phấn", "má hồng", "kem nền", "makeup", "mắt", "môi" };
            
            var makeupProducts = allProducts.Where(p => 
                p.Category != null && 
                makeupKeywords.Any(keyword => p.Category.Name.ToLower().Contains(keyword))
            ).ToList();

            // Logic Sắp xếp
            switch (sortOrder)
            {
                case "price_asc":
                    makeupProducts = makeupProducts.OrderBy(p => p.Price).ToList();
                    break;
                case "price_desc":
                    makeupProducts = makeupProducts.OrderByDescending(p => p.Price).ToList();
                    break;
                case "popular":
                    makeupProducts = makeupProducts.OrderBy(p => p.Name).ToList();
                    break;
                default:
                    makeupProducts = makeupProducts.OrderByDescending(p => p.Id).ToList();
                    break;
            }

            ViewBag.PageTitle = "Trang Điểm";
            ViewBag.PageDescription = "Lộ trình hoàn hảo cho vẻ đẹp rạng rỡ và tự tin";
            ViewBag.CurrentSort = sortOrder;

            return View(makeupProducts); 
        }
    }
}