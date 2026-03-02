using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting; // Để tìm đường dẫn thư mục wwwroot
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; // Để làm Dropdown List chọn Danh mục
using Microsoft.EntityFrameworkCore;
using VelvySkinWeb.Data;
using VelvySkinWeb.Models;

namespace VelvySkinWeb.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment; // Công cụ chỉ đường tới wwwroot

        // Tiêm Database và Công cụ chỉ đường vào Controller
        public ProductsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // ==========================================
        // GET: Hiển thị Form Thêm Sản phẩm
        // ==========================================
        public IActionResult Create()
        {
            // BÍ QUYẾT: Lấy danh sách Categories từ SQL nhét vào một cái hộp (ViewData) 
            // để mang ra giao diện tạo thành thanh Dropdown (Thẻ Select) cho Admin bấm chọn.
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            return View();
        }

        // ==========================================
        // POST: Hứng dữ liệu từ Form và Lưu xuống SQL
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        // Đã thêm StockQuantity và BrandId vào danh sách cho phép nhận dữ liệu
public async Task<IActionResult> Create([Bind("Id,Name,Description,Price,StockQuantity,BrandId,CategoryId,ImageFile")] Product product)
        {
            if (ModelState.IsValid)
            {
                // THUẬT TOÁN UPLOAD ẢNH CỦA BẠN NẰM Ở ĐÂY:
                if (product.ImageFile != null)
                {
                    // 1. Tìm đường dẫn vật lý tới thư mục "wwwroot/images"
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                    
                    // 2. Đổi tên file ảnh để không bao giờ bị trùng (Thêm chuỗi mã hóa Guid vào trước tên gốc)
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + product.ImageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    // 3. Copy file ảnh từ RAM lưu thẳng vào ổ cứng máy tính
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await product.ImageFile.CopyToAsync(fileStream);
                    }

                    // 4. Cất cái đường dẫn text này vào Database để sau này lấy ra in lên web
                    product.ImageUrl = "/images/" + uniqueFileName;
                }

                // Lưu thông tin chữ (Tên, Giá, Danh mục...) vào SQL Server
                _context.Add(product);
                await _context.SaveChangesAsync();
                
                return RedirectToAction("Index", "Home"); // Tạm thời lưu xong cho quay về Trang chủ
            }

            // Nếu nhập lỗi, load lại cái Dropdown danh mục
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }
    }
}