using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Thêm dòng này
using VelvySkinWeb.Data; // Thêm dòng này
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using VelvySkinWeb.Models;

namespace VelvySkinWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context; // Bơm Database vào đây

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }
public async Task<IActionResult> Logout()
        {
            // 1. Ép hệ thống xóa sạch Cookie đăng nhập của người dùng
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            
            // 2. Tùy chọn: Xóa luôn các "Trí nhớ" session (Giỏ hàng, Yêu thích) của phiên cũ
            HttpContext.Session.Clear(); 
            
            // 3. Đá văng về trang chủ
            return RedirectToAction("Index", "Home");
        }
        public async Task<IActionResult> Index()
        {
            // Lôi 8 sản phẩm mới nhất từ SQL Server ra (kèm theo Tên danh mục của nó)
            var products = await _context.Products
                .Include(p => p.Category)
                .Take(8)
                .ToListAsync();
                
            return View(products); // Nhét cục dữ liệu này vào View để Như hiển thị
        }
        public IActionResult AiConsultation()
        {
            return View();
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // ==========================================
        // THUẬT TOÁN XỬ LÝ KẾT QUẢ AI (MỨC ĐỘ 1)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> AiResult(string skinType, string skinConcern)
        {
            // 1. CHUẨN BỊ BIẾN CHỨA ĐIỂM SỐ
            int moisture = 50, oil = 50, acne = 10, smooth = 70;
            string diagnosis = "";

            // 2. THUẬT TOÁN RẼ NHÁNH: Dựa vào lựa chọn của khách để điều chỉnh điểm
            if (skinType == "Da khô") 
            { 
                moisture = new Random().Next(15, 30); // Độ ẩm thấp
                oil = new Random().Next(10, 25);
                smooth = new Random().Next(40, 60);
            }
            else if (skinType == "Da dầu") 
            { 
                moisture = new Random().Next(40, 60);
                oil = new Random().Next(75, 95);      // Đổ nhiều dầu
                acne = new Random().Next(30, 50); 
            }
            else if (skinType == "Da hỗn hợp") 
            { 
                moisture = new Random().Next(45, 65);
                oil = new Random().Next(60, 80); 
            }
            else // Da thường
            {
                moisture = new Random().Next(70, 90);
                oil = new Random().Next(40, 60);
                smooth = new Random().Next(80, 95);
            }

            // Tăng điểm mụn nếu khách chọn quan tâm Mụn
            if (skinConcern == "Mụn & Thâm") acne += new Random().Next(30, 50);

            // 3. TẠO CÂU CHUẨN ĐOÁN
            diagnosis = $"Dựa trên phân tích, da bạn thuộc loại <strong>{skinType}</strong>. " +
                        $"Vấn đề chính đang hiện hữu là <strong>{skinConcern}</strong>. " +
                        $"Lượng dầu và nước đang mất cân bằng. Hệ thống đề xuất lộ trình phục hồi chuyên sâu dưới đây.";

            // 4. LẤY MỸ PHẨM GỢI Ý TỪ DATABASE (Lấy ngẫu nhiên 3 sản phẩm cho có data thật)
            var suggestedProducts = await _context.Products
                                                  .OrderBy(x => Guid.NewGuid()) // Lấy random
                                                  .Take(3)
                                                  .ToListAsync();

            // 5. NÉM DỮ LIỆU RA GIAO DIỆN
            ViewBag.Moisture = moisture;
            ViewBag.Oil = oil;
            ViewBag.Acne = acne;
            ViewBag.Smooth = smooth;
            ViewBag.Diagnosis = diagnosis;

            return View(suggestedProducts); // Truyền model Mỹ phẩm qua
        }
        public IActionResult About()
        {
            return View();
        }
    }
}