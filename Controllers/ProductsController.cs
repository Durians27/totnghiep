using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

        public async Task<IActionResult> Index()
        {
            var products = await _context.Products.Include(p => p.Category).ToListAsync();
            return View(products);
        }

        [HttpGet("Admin/Products/Create")]
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            return View();
        }

        // =======================================================
        // 🔥 HÀM TẠO SẢN PHẨM MỚI (ĐÃ FIX LỖI MODEL & DATABASE)
        // =======================================================
        [HttpPost("Admin/Products/Create")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Id,Name,Price,DiscountPrice,PriceLarge,StockQuantity,ShortDescription,FullDescription,UsageInstructions,CategoryId,IsActive")] Product product, 
            IFormFile imageFile, IFormFile imageFile2, IFormFile imageFile3, IFormFile imageFile4, IFormFile imageFile5, 
            List<string> ingredient, string ingredientDetail) 
        {
            // Xóa validation cho những trường không nhập từ form để tránh báo lỗi ẩn
            ModelState.Remove("ImageUrl");
            ModelState.Remove("Category");

            if (ModelState.IsValid)
            {
                try
                {
                    product.ImageUrl = await SaveImageAsync(imageFile) ?? "https://via.placeholder.com/300"; 
                    if (imageFile2 != null) product.ImageUrl2 = await SaveImageAsync(imageFile2); 
                    if (imageFile3 != null) product.ImageUrl3 = await SaveImageAsync(imageFile3);
                    if (imageFile4 != null) product.ImageUrl4 = await SaveImageAsync(imageFile4);
                    if (imageFile5 != null) product.ImageUrl5 = await SaveImageAsync(imageFile5);

                    if (ingredient != null && ingredient.Count > 0)
                    {
                        var validIngredients = ingredient.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
                        string detail = string.IsNullOrWhiteSpace(ingredientDetail) ? "Đang cập nhật..." : ingredientDetail;

                        if (validIngredients.Count > 0)
                        {
                            product.Ingredients = string.Join(", ", validIngredients) + " | " + detail;
                        }
                    }

                    _context.Products.Add(product);

                    // ==========================================================
                    // 🔥 AUDIT LOG 1: CAMERA GHI NHẬN THÊM SẢN PHẨM MỚI
                    // ==========================================================
                    var logCreate = new VelvySkinWeb.Models.AuditLog
                    {
                        Username = User.Identity?.Name ?? "Hệ thống",
                        ActionType = "CREATE",
                        TableName = "Products",
                        Description = $"Admin đã thêm sản phẩm mới: '{product.Name}' với giá {product.Price.ToString("N0")}đ",
                        Timestamp = DateTime.Now
                    };
                    _context.AuditLogs.Add(logCreate);
                    // ==========================================================

                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMsg"] = "Tuyệt vời! Đã thêm sản phẩm mới thành công!";
                    return RedirectToAction("Products", "Admin");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Lỗi Database: " + (ex.InnerException?.Message ?? ex.Message));
                }
            }
            else
            {
                ModelState.AddModelError("", "Vui lòng kiểm tra lại! Bạn nhập thiếu thông tin bắt buộc.");
            }
            
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        [HttpGet("Admin/Products/Edit/{id}")] 
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> SearchSuggest(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) 
                return Json(new List<object>()); 

            var results = await _context.Products
                .Where(p => p.Name.Contains(keyword) && p.IsActive) 
                .Select(p => new {
                    id = p.Id,
                    name = p.Name,
                    price = p.Price,
                    imageUrl = p.ImageUrl
                })
                .Take(5)
                .ToListAsync();

            return Json(results);
        }

       [HttpPost("Admin/Products/Edit/{id}")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Price,DiscountPrice,PriceLarge,StockQuantity,ShortDescription,FullDescription,UsageInstructions,CategoryId,IsActive")] Product product, 
            IFormFile imageFile, IFormFile imageFile2, IFormFile imageFile3, IFormFile imageFile4, IFormFile imageFile5, 
            List<string> ingredient, string ingredientDetail) 
        {
            if (id != product.Id) return NotFound();

            var existingProduct = await _context.Products.FindAsync(id);
            if (existingProduct == null) return NotFound();

            // 🔥 BẮT LẠI GIÁ CŨ TRƯỚC KHI BỊ GHI ĐÈ ĐỂ SO SÁNH GHI LOG
            decimal oldPrice = existingProduct.Price;

            existingProduct.Name = product.Name;
            existingProduct.Price = product.Price;
            existingProduct.DiscountPrice = product.DiscountPrice; 
            existingProduct.PriceLarge = product.PriceLarge;
            existingProduct.StockQuantity = product.StockQuantity;
            existingProduct.CategoryId = product.CategoryId;
            existingProduct.IsActive = product.IsActive;
            existingProduct.ShortDescription = product.ShortDescription;
            existingProduct.FullDescription = product.FullDescription;
            existingProduct.UsageInstructions = product.UsageInstructions;

            if (imageFile != null) existingProduct.ImageUrl = await SaveImageAsync(imageFile);
            if (imageFile2 != null) existingProduct.ImageUrl2 = await SaveImageAsync(imageFile2);
            if (imageFile3 != null) existingProduct.ImageUrl3 = await SaveImageAsync(imageFile3);
            if (imageFile4 != null) existingProduct.ImageUrl4 = await SaveImageAsync(imageFile4);
            if (imageFile5 != null) existingProduct.ImageUrl5 = await SaveImageAsync(imageFile5);

            if (ingredient != null && ingredient.Count > 0)
            {
                var validIngredients = ingredient.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
                string detail = string.IsNullOrWhiteSpace(ingredientDetail) ? "Đang cập nhật..." : ingredientDetail;

                if (validIngredients.Count > 0)
                {
                    existingProduct.Ingredients = string.Join(", ", validIngredients) + " | " + detail;
                }
            }

            try
            {
                _context.Update(existingProduct);

                // ==========================================================
                // 🔥 AUDIT LOG 2: CAMERA GHI NHẬN CẬP NHẬT SP & SOI LỖI ĐỔI GIÁ
                // ==========================================================
                string priceChangeLog = "";
                if (oldPrice != product.Price)
                {
                    priceChangeLog = $" (ĐỔI GIÁ: Từ {oldPrice.ToString("N0")}đ thành {product.Price.ToString("N0")}đ)";
                }

                var logUpdate = new VelvySkinWeb.Models.AuditLog
                {
                    Username = User.Identity?.Name ?? "Hệ thống",
                    ActionType = "UPDATE",
                    TableName = "Products",
                    Description = $"Admin đã cập nhật sản phẩm '{existingProduct.Name}'{priceChangeLog}",
                    Timestamp = DateTime.Now
                };
                _context.AuditLogs.Add(logUpdate);
                // ==========================================================

                await _context.SaveChangesAsync();
                
                TempData["SuccessMsg"] = "Tuyệt vời! Đã chỉnh sửa sản phẩm thành công!";
                return RedirectToAction("Products", "Admin");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi không lưu được vào Database: " + ex.Message);
            }
            
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var product = await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(m => m.Id == id);
            if (product == null) return NotFound();
            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                DeleteImageFile(product.ImageUrl);
                DeleteImageFile(product.ImageUrl2);
                DeleteImageFile(product.ImageUrl3);
                DeleteImageFile(product.ImageUrl4);
                DeleteImageFile(product.ImageUrl5);

                _context.Products.Remove(product);

                // ==========================================================
                // 🔥 AUDIT LOG 3: CAMERA GHI NHẬN XÓA SẢN PHẨM
                // ==========================================================
                var logDelete = new VelvySkinWeb.Models.AuditLog
                {
                    Username = User.Identity?.Name ?? "Hệ thống",
                    ActionType = "DELETE",
                    TableName = "Products",
                    Description = $"Admin đã XÓA sản phẩm ID #{product.Id} - Tên: '{product.Name}'",
                    Timestamp = DateTime.Now
                };
                _context.AuditLogs.Add(logDelete);
                // ==========================================================

                await _context.SaveChangesAsync();
                
                TempData["SuccessMsg"] = $"Đã xóa vĩnh viễn sản phẩm '{product.Name}' thành công!";
            }
            
            return RedirectToAction("Products", "Admin");
        }

        private void DeleteImageFile(string imageUrl)
        {
            if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.Contains("via.placeholder.com"))
            {
                string imagePath = Path.Combine(_webHostEnvironment.WebRootPath, imageUrl.TrimStart('/'));
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                }
            }
        }

        [AllowAnonymous]
        public async Task<IActionResult> Haircare(string sortOrder)
        {
            var allProducts = await _context.Products.Include(p => p.Category).Where(p => p.IsActive).ToListAsync();
            var hairKeywords = new[] { "tóc", "gội", "xả", "dưỡng tóc", "mặt nạ tóc", "xịt", "hair" };
            
            var hairProducts = allProducts.Where(p => 
                p.Category != null && 
                hairKeywords.Any(keyword => p.Category.Name.ToLower().Contains(keyword))
            ).ToList();

            switch (sortOrder)
            {
                case "price_asc": hairProducts = hairProducts.OrderBy(p => p.Price).ToList(); break;
                case "price_desc": hairProducts = hairProducts.OrderByDescending(p => p.Price).ToList(); break;
                case "popular": hairProducts = hairProducts.OrderBy(p => p.Name).ToList(); break;
                default: hairProducts = hairProducts.OrderByDescending(p => p.Id).ToList(); break;
            }

            ViewBag.PageTitle = "Chăm Sóc Tóc";
            ViewBag.PageDescription = "Lộ trình hoàn hảo cho mái tóc khỏe mạnh và óng ả";
            ViewBag.CurrentSort = sortOrder;

            return View(hairProducts); 
        }

        [AllowAnonymous]
        public async Task<IActionResult> Shampoo(string sortOrder)
        {
            var allProducts = await _context.Products.Include(p => p.Category).Where(p => p.IsActive).ToListAsync();
            var shampooKeywords = new[] { "gội", "xả", "shampoo", "conditioner" };
            
            var shampooProducts = allProducts.Where(p => 
                p.Category != null && 
                shampooKeywords.Any(keyword => p.Category.Name.ToLower().Contains(keyword))
            ).ToList();

            switch (sortOrder)
            {
                case "price_asc": shampooProducts = shampooProducts.OrderBy(p => p.Price).ToList(); break;
                case "price_desc": shampooProducts = shampooProducts.OrderByDescending(p => p.Price).ToList(); break;
                case "popular": shampooProducts = shampooProducts.OrderBy(p => p.Name).ToList(); break;
                default: shampooProducts = shampooProducts.OrderByDescending(p => p.Id).ToList(); break;
            }

            ViewBag.PageTitle = "Dầu Gội & Dầu Xả";
            ViewBag.PageDescription = "Làm sạch sâu, nuôi dưỡng chân tóc chắc khỏe";
            ViewBag.CurrentSort = sortOrder;
            ViewBag.IsShampooPage = true; 

            return View("Haircare", shampooProducts);
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Skincare(string sortOrder)
        {
            var allProducts = await _context.Products.Include(p => p.Category).Where(p => p.IsActive).ToListAsync();
            var skincareKeywords = new[] { "rửa mặt", "tẩy", "serum", "đặc trị", "kem", "dưỡng", "nắng" };
            
            var skincareProducts = allProducts.Where(p => 
                p.Category != null && 
                skincareKeywords.Any(keyword => p.Category.Name.ToLower().Contains(keyword))
            ).ToList();

            switch (sortOrder)
            {
                case "price_asc": skincareProducts = skincareProducts.OrderBy(p => p.Price).ToList(); break;
                case "price_desc": skincareProducts = skincareProducts.OrderByDescending(p => p.Price).ToList(); break;
                case "popular": skincareProducts = skincareProducts.OrderBy(p => p.Name).ToList(); break;
                default: skincareProducts = skincareProducts.OrderByDescending(p => p.Id).ToList(); break;
            }

            ViewBag.PageTitle = "Chăm Sóc Da";
            ViewBag.PageDescription = "Lộ trình hoàn hảo cho làn da rạng rỡ mỗi ngày";
            ViewBag.CurrentSort = sortOrder; 

            return View(skincareProducts); 
        }

        [AllowAnonymous]
        public async Task<IActionResult> CategoryDetail(int id, string sortOrder)
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id);
            if (category == null) return NotFound();

            IQueryable<Product> productsQuery = _context.Products
                .Where(p => p.CategoryId == id && p.IsActive) 
                .Include(p => p.Category);

            switch (sortOrder)
            {
                case "price_asc": productsQuery = productsQuery.OrderBy(p => p.Price); break;
                case "price_desc": productsQuery = productsQuery.OrderByDescending(p => p.Price); break;
                case "popular": productsQuery = productsQuery.OrderBy(p => p.Name); break;
                default: productsQuery = productsQuery.OrderByDescending(p => p.Id); break;
            }

            var products = await productsQuery.ToListAsync();

            ViewBag.PageTitle = category.Name; 
            ViewBag.PageDescription = category.Description;
            ViewBag.CurrentSort = sortOrder;

            string catName = category.Name.ToLower();

            if (catName.Contains("gội") || catName.Contains("xả") || catName.Contains("tóc") || catName.Contains("tinh dầu") || catName.Contains("xịt"))
            {
                if (catName.Contains("gội") || catName.Contains("xả")) ViewBag.IsShampooPage = true; 
                else if (catName.Contains("tinh dầu")) ViewBag.IsHairOilPage = true;
                else if (catName.Contains("xịt")) ViewBag.IsHairSprayPage = true;
                
                return View("Haircare", products); 
            }
            else if (catName.Contains("trang điểm") || catName.Contains("son") || catName.Contains("phấn") || catName.Contains("má hồng"))
            {
                return View("Makeup", products); 
            }

            return View("Skincare", products);
        }
   
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products
                .Include(p => p.Category) 
                .FirstOrDefaultAsync(m => m.Id == id);

            if (product == null || !product.IsActive) return NotFound();

            return View(product);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Makeup(string sortOrder)
        {
            var allProducts = await _context.Products.Include(p => p.Category).Where(p => p.IsActive).ToListAsync();
            var makeupKeywords = new[] { "trang điểm", "son", "phấn", "má hồng", "kem nền", "makeup", "mắt", "môi" };
            
            var makeupProducts = allProducts.Where(p => 
                p.Category != null && 
                makeupKeywords.Any(keyword => p.Category.Name.ToLower().Contains(keyword))
            ).ToList();

            switch (sortOrder)
            {
                case "price_asc": makeupProducts = makeupProducts.OrderBy(p => p.Price).ToList(); break;
                case "price_desc": makeupProducts = makeupProducts.OrderByDescending(p => p.Price).ToList(); break;
                case "popular": makeupProducts = makeupProducts.OrderBy(p => p.Name).ToList(); break;
                default: makeupProducts = makeupProducts.OrderByDescending(p => p.Id).ToList(); break;
            }

            ViewBag.PageTitle = "Trang Điểm";
            ViewBag.PageDescription = "Lộ trình hoàn hảo cho vẻ đẹp rạng rỡ và tự tin";
            ViewBag.CurrentSort = sortOrder;

            return View(makeupProducts); 
        }

        // =======================================================
        // 🔥 HÀM XỬ LÝ ẢNH (BẤT TỬ, KHÔNG BAO GIỜ LỖI ĐƯỜNG DẪN)
        // =======================================================
        private async Task<string> SaveImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            // Dùng _webHostEnvironment chuẩn xác thay vì GetCurrentDirectory
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
            
            // TỰ ĐỘNG TẠO THƯ MỤC NẾU CHƯA CÓ (TRÁNH LỖI CRASH WEB)
            if (!Directory.Exists(uploadsFolder)) 
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = Path.GetFileNameWithoutExtension(file.FileName) + "_" + DateTime.Now.ToString("yymmssfff") + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);
            
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            
            return "/images/products/" + fileName;
        }
    }
}