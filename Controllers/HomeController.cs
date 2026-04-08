using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VelvySkinWeb.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using VelvySkinWeb.Models;
using System.Linq;

namespace VelvySkinWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Logout()
        {

            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            

            HttpContext.Session.Clear(); 
            

            return RedirectToAction("Index", "Home");
        }

public async Task<IActionResult> Index()
        {
            // =======================================================
            // 1. BEST SELLERS (TOP BÁN CHẠY NHẤT)
            // =======================================================
            var topSellingData = await _context.OrderDetails
                .Include(od => od.Order)
                .Where(od => od.Order.OrderStatus == "Đang xử lý" || od.Order.OrderStatus.Contains("Đã thanh toán") || od.Order.OrderStatus == "Thành công" || od.Order.OrderStatus == "Đã giao")
                .GroupBy(od => od.ProductId)
                .Select(g => new {
                    ProductId = g.Key,
                    TotalSold = g.Sum(x => x.Quantity)
                })
                .OrderByDescending(x => x.TotalSold)
                .Take(8)
                .ToListAsync();

            var topProductIds = topSellingData.Select(x => x.ProductId).ToList();
            var bestSellers = new List<Product>();
            
            if (topProductIds.Any())
            {
                bestSellers = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => topProductIds.Contains(p.Id) && p.IsActive)
                    .ToListAsync();

                bestSellers = bestSellers.OrderBy(p => topProductIds.IndexOf(p.Id)).ToList();
            }
            else
            {
                // CHEAT CODE: Random nếu chưa có ai mua
                bestSellers = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.IsActive)
                    .OrderBy(p => Guid.NewGuid()) 
                    .Take(8)
                    .ToListAsync();
            }

            // =======================================================
            // 2. SALE PRODUCTS (SIÊU SALE HÔM NAY)
            // =======================================================
            var saleProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.DiscountPrice > 0 && p.DiscountPrice < p.Price && p.IsActive)
                .OrderByDescending(p => p.Id)
                .Take(8)
                .ToListAsync();

            // =======================================================
            // 3. LOVED PRODUCTS (ĐƯỢC YÊU THÍCH NHẤT) - ĐÃ FIX LỖI
            // =======================================================
            // Sửa logic: Yêu thích nhất = Sản phẩm có nhiều khách hàng mua lặp lại nhất
            // Hoặc có thể hiểu là sản phẩm Hot Trending (Bán được nhiều số lượng)
            // Nếu không có dữ liệu, sẽ lấy Random thay vì lấy thằng mới nhất (tránh lỗi sếp nhắc)
            
            var lovedProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive)
                .Select(p => new 
                {
                    Product = p,
                    TotalSold = _context.OrderDetails.Where(od => od.ProductId == p.Id).Sum(od => (int?)od.Quantity) ?? 0
                })
                .OrderByDescending(x => x.TotalSold)
                // Tránh trùng lặp với BestSellers (Tùy chọn, sếp có thể bỏ dòng Skip nếu muốn)
                // .Skip(8) 
                .Take(8)
                .Select(x => x.Product)
                .ToListAsync();
                
            // Nếu hệ thống mới tinh chưa có lượt bán nào, tránh việc danh sách bị rỗng
            if (lovedProducts.Count == 0 || lovedProducts.All(p => topProductIds.Contains(p.Id)))
            {
                lovedProducts = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.IsActive && !topProductIds.Contains(p.Id)) // Lấy những thằng chưa lọt top bán chạy
                    .OrderBy(p => Guid.NewGuid()) // Random để có sự đa dạng
                    .Take(8)
                    .ToListAsync();
            }

            ViewBag.BestSellers = bestSellers;
            ViewBag.SaleProducts = saleProducts;
            ViewBag.LovedProducts = lovedProducts;

            var allProducts = await _context.Products.Include(p => p.Category).Where(p => p.IsActive).ToListAsync();
            return View(allProducts);
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




        [HttpPost]
        public async Task<IActionResult> AiResult(string skinType, string skinConcern)
        {

            int moisture = 50, oil = 50, acne = 10, smooth = 70;
            string diagnosis = "";


            if (skinType == "Da khô") 
            { 
                moisture = new Random().Next(15, 30);
                oil = new Random().Next(10, 25);
                smooth = new Random().Next(40, 60);
            }
            else if (skinType == "Da dầu") 
            { 
                moisture = new Random().Next(40, 60);
                oil = new Random().Next(75, 95);
                acne = new Random().Next(30, 50); 
            }
            else if (skinType == "Da hỗn hợp") 
            { 
                moisture = new Random().Next(45, 65);
                oil = new Random().Next(60, 80); 
            }
            else
            {
                moisture = new Random().Next(70, 90);
                oil = new Random().Next(40, 60);
                smooth = new Random().Next(80, 95);
            }


            if (skinConcern == "Mụn & Thâm") acne += new Random().Next(30, 50);


            diagnosis = $"Dựa trên phân tích, da bạn thuộc loại <strong>{skinType}</strong>. " +
                        $"Vấn đề chính đang hiện hữu là <strong>{skinConcern}</strong>. " +
                        $"Lượng dầu và nước đang mất cân bằng. Hệ thống đề xuất lộ trình phục hồi chuyên sâu dưới đây.";


            var suggestedProducts = await _context.Products
                                                  .OrderBy(x => Guid.NewGuid())
                                                  .Take(3)
                                                  .ToListAsync();


            ViewBag.Moisture = moisture;
            ViewBag.Oil = oil;
            ViewBag.Acne = acne;
            ViewBag.Smooth = smooth;
            ViewBag.Diagnosis = diagnosis;

            return View(suggestedProducts);
        }

        public IActionResult About()
        {
            return View();
        }

        public async Task<IActionResult> Vouchers()
        {

            var activeCoupons = await _context.Coupons
                .Where(c => c.IsActive && c.EndDate >= DateTime.Now)
                .OrderBy(c => c.EndDate)
                .ToListAsync();

            return View(activeCoupons);
        }

        [HttpGet]
        public async Task<IActionResult> GetVoucherListHtml()
        {

            var activeCoupons = await _context.Coupons
                .Where(c => c.IsActive && c.EndDate >= DateTime.Now)
                .OrderBy(c => c.EndDate) 
                .ToListAsync();


            return PartialView("_VoucherListPartial", activeCoupons);
        }

        [HttpGet]
        public IActionResult Maintenance()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Support()
        {

            TempData["ActiveTab"] = "tab-support";
            

            return RedirectToAction("Profile", "Account");
        }
    }
}