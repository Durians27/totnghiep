using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VelvySkinWeb.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using VelvySkinWeb.Models;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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

                bestSellers = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.IsActive)
                    .OrderBy(p => Guid.NewGuid()) 
                    .Take(8)
                    .ToListAsync();
            }




            var saleProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.DiscountPrice > 0 && p.DiscountPrice < p.Price && p.IsActive)
                .OrderByDescending(p => p.Id)
                .Take(8)
                .ToListAsync();







            
            var lovedProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive)
                .Select(p => new 
                {
                    Product = p,
                    TotalSold = _context.OrderDetails.Where(od => od.ProductId == p.Id).Sum(od => (int?)od.Quantity) ?? 0
                })
                .OrderByDescending(x => x.TotalSold)


                .Take(8)
                .Select(x => x.Product)
                .ToListAsync();
                

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



[HttpPost]
public async Task<IActionResult> AiResult(string skinType, string skinConcern, IFormFile faceImage)
{

    var suggestedProducts = _context.Products.Take(4).ToList();


    if (faceImage == null || faceImage.Length == 0)
    {
        ViewBag.AiAdvice = "⚠️ LỖI: C# không nhận được bức ảnh nào! Hãy kiểm tra lại Form HTML xem có đúng name='faceImage' và enctype='multipart/form-data' chưa nhé!";
        ViewBag.Moisture = 0; ViewBag.Oil = 0; ViewBag.Acne = 0; ViewBag.Smoothness = 0;
        return View(suggestedProducts);
    }

    try
    {

        using var ms = new MemoryStream();
        await faceImage.CopyToAsync(ms);
        string base64Image = Convert.ToBase64String(ms.ToArray());
        string mimeType = faceImage.ContentType;


        string apiKey = "AQ.Ab8RN6L7Do9LqKJIkVW5DzM6x-vbtnF5qj2hMYGICg-Cso7Yvw"; 
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

        string prompt = $"Hãy đóng vai bác sĩ da liễu. Hãy nhìn bức ảnh khuôn mặt này. Người dùng cho biết da họ là '{skinType}' và đang lo ngại về '{skinConcern}'. Hãy phân tích bức ảnh và trả về kết quả BẮT BUỘC ĐÚNG ĐỊNH DẠNG JSON SAU (không có bất kỳ chữ nào khác, giá trị phải là số): {{\"Moisture\": 65, \"Oil\": 40, \"Acne\": 70, \"Smoothness\": 50, \"Advice\": \"3 câu tư vấn cách chăm sóc dựa trên ảnh\"}}";


        var requestData = new
        {
            contents = new[] {
                new {
                    parts = new object[] {
                        new { text = prompt },
                        new { inlineData = new { mimeType = mimeType, data = base64Image } }
                    }
                }
            }
        };

        using (var client = new HttpClient())
        {
            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);
            
            var jsonString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using (JsonDocument doc = JsonDocument.Parse(jsonString))
                {
                    var aiTextResponse = doc.RootElement.GetProperty("candidates")[0]
                                   .GetProperty("content").GetProperty("parts")[0]
                                   .GetProperty("text").GetString();


                    aiTextResponse = aiTextResponse.Replace("```json", "").Replace("```", "").Trim();


                    using (JsonDocument resultDoc = JsonDocument.Parse(aiTextResponse))
                    {
                        ViewBag.Moisture = Convert.ToInt32(resultDoc.RootElement.GetProperty("Moisture").ToString());
                        ViewBag.Oil = Convert.ToInt32(resultDoc.RootElement.GetProperty("Oil").ToString());
                        ViewBag.Acne = Convert.ToInt32(resultDoc.RootElement.GetProperty("Acne").ToString());
                        ViewBag.Smoothness = Convert.ToInt32(resultDoc.RootElement.GetProperty("Smoothness").ToString());
                        ViewBag.AiAdvice = resultDoc.RootElement.GetProperty("Advice").GetString();
                    }
                }
            }
            else 
            {

                ViewBag.AiAdvice = $"⚠️ LỖI TỪ GOOGLE API: {jsonString}";
                RandomizeNumbers();
            }
        }
    }
    catch (Exception ex) 
    { 

        ViewBag.AiAdvice = $"⚠️ LỖI CODE C#: {ex.Message}";
        RandomizeNumbers();
    }

    ViewBag.SkinType = skinType; 
    ViewBag.SkinConcern = skinConcern;
    return View(suggestedProducts); 


    void RandomizeNumbers() {
        Random rnd = new Random();
        ViewBag.Moisture = rnd.Next(40, 80); ViewBag.Oil = rnd.Next(30, 85); 
        ViewBag.Acne = rnd.Next(20, 70); ViewBag.Smoothness = rnd.Next(50, 90);
    }
}

private async Task<string> CallGeminiAI(string skinType, string skinProblem)
{
    try
    {

        string apiKey = "AQ.Ab8RN6L7Do9LqKJIkVW5DzM6x-vbtnF5qj2hMYGICg-Cso7Yvw"; 
       string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";


        var requestData = new
        {
            contents = new[] {
                new { parts = new[] { new { text = $"Đóng vai chuyên gia da liễu Velvy Skin. Viết đúng 3 câu ngắn gọn tư vấn cách chăm sóc cho người có '{skinType}' và đang bị '{skinProblem}'. Trả lời trực tiếp, không dùng ký tự đặc biệt." } } }
            }
        };

        using (var client = new HttpClient())
        {
            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);
            
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(jsonString))
                {
                    var root = doc.RootElement;
                    var text = root.GetProperty("candidates")[0]
                                   .GetProperty("content")
                                   .GetProperty("parts")[0]
                                   .GetProperty("text").GetString();
                    return text; // Trả về câu văn do AI tự sáng tác
                }
            }
        }
    }
    catch { } // Nếu lỗi mạng hoặc API Key sai, nó sẽ chạy xuống dòng dưới
    

    return $"Dựa trên phân tích, da bạn thuộc loại {skinType}. Vấn đề chính đang hiện hữu là {skinProblem}. Lượng dầu và nước đang mất cân bằng. Hệ thống đề xuất lộ trình phục hồi chuyên sâu dưới đây."; 
}
    }
}