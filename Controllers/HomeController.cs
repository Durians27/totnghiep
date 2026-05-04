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
                    .Where(p => p.IsActive && !topProductIds.Contains(p.Id))
                    .OrderBy(p => Guid.NewGuid())
                    .Take(8)
                    .ToListAsync();
            }

            ViewBag.BestSellers = bestSellers;
            ViewBag.SaleProducts = saleProducts;
            ViewBag.LovedProducts = lovedProducts;

            var allProducts = await _context.Products.Include(p => p.Category).Where(p => p.IsActive).ToListAsync();
            return View(allProducts);
        }

        public IActionResult AiConsultation() => View();
        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

        public IActionResult About() => View();

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
        public IActionResult Maintenance() => View();

        [HttpGet]
        public IActionResult Support()
        {
            TempData["ActiveTab"] = "tab-support";
            return RedirectToAction("Profile", "Account");
        }

        // ================================================================
        //  AI RESULT — phân tích ảnh + chỉ số % + gợi ý sản phẩm
        // ================================================================
        [HttpPost]
        public async Task<IActionResult> AiResult(string skinType, string skinConcern, IFormFile faceImage)
        {
            // --- Kiểm tra ảnh đầu vào ---
            if (faceImage == null || faceImage.Length == 0)
            {
                ViewBag.AiAdvice   = "⚠️ Không nhận được ảnh! Hãy kiểm tra lại Form HTML.";
                ViewBag.Moisture   = 0; ViewBag.Oil  = 0;
                ViewBag.Acne       = 0; ViewBag.Smoothness = 0;
                ViewBag.SkinType   = skinType;
                ViewBag.SkinConcern = skinConcern;
                var fb = await _context.Products.Include(p => p.Category)
                                       .Where(p => p.IsActive).Take(4).ToListAsync();
                return View(fb);
            }

            // --- Lấy danh sách sản phẩm để nhúng vào prompt (tối đa 40) ---
            var productListForAI = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive)
                .Take(40)
                .Select(p => new
                {
                    id   = p.Id,
                    name = p.Name,
                    desc = p.ShortDescription ?? "",
                    ingr = p.Ingredients ?? "",
                    tags = p.Tags ?? "",
                    cate = p.Category != null ? p.Category.Name : ""
                })
                .ToListAsync();

            string productJson = JsonSerializer.Serialize(productListForAI, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            List<Product> suggestedProducts = new();

            try
            {
                // --- Encode ảnh sang base64 ---
                using var ms = new MemoryStream();
                await faceImage.CopyToAsync(ms);
                string base64Image = Convert.ToBase64String(ms.ToArray());
                string mimeType    = faceImage.ContentType;

                string apiKey = "AIzaSyCj6PQ7WAAzQOATA1dnTKwVGzGCg5_CFtc";
                string url    = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";

                // ============================================================
                // PROMPT — Gemini tự NHẬN XÉT da trước, rồi tự TÍNH % từ
                // chính nhận xét đó để 4 chỉ số luôn nhất quán với lời khuyên.
                //
                // Quy ước chỉ số (giải thích rõ để AI hiểu ý nghĩa):
                //   Moisture   : 0 = rất khô, 100 = đủ ẩm tốt
                //   Oil        : 0 = không dầu, 100 = rất nhiều dầu
                //   Acne       : 0 = không mụn/thâm, 100 = rất nhiều
                //   Smoothness : 0 = da sần sùi, 100 = rất mịn
                // ============================================================
                string prompt = $@"Bạn là bác sĩ da liễu AI của Velvy Skin. Hãy thực hiện đúng 3 bước sau:

BƯỚC 1 — QUAN SÁT ẢNH:
Nhìn kỹ bức ảnh khuôn mặt. Người dùng tự nhận da là ""{skinType}"" và lo ngại về ""{skinConcern}"".
Viết nhận xét thực tế 3-4 câu về tình trạng da nhìn thấy trong ảnh (Advice).

BƯỚC 2 — TÍNH CHỈ SỐ DỰA THEO NHẬN XÉT:
Từ nhận xét vừa viết ở Bước 1, quy đổi sang 4 chỉ số số nguyên 0-100:
- Moisture   (Độ ẩm)    : 0 = rất khô/thiếu ẩm, 100 = căng mọng đủ ẩm
- Oil        (Độ dầu)   : 0 = không tiết dầu, 100 = rất nhiều dầu bóng nhờn
- Acne       (Mụn/Thâm) : 0 = không có mụn/thâm, 100 = rất nhiều mụn và vết thâm
- Smoothness (Độ mịn)   : 0 = da sần/lỗ chân lông to, 100 = da cực mịn đều màu

Ví dụ nhất quán: nếu Advice nói ""da khô, thiếu ẩm"" thì Moisture thấp (~25-40). 
Nếu Advice nói ""da dầu bóng"" thì Oil cao (~70-85). 
Nếu Advice nói ""da mịn, ít mụn"" thì Smoothness cao (~75-90) và Acne thấp (~10-20).
Các chỉ số PHẢI nhất quán với nội dung Advice.

BƯỚC 3 — CHỌN SẢN PHẨM PHÙ HỢP:
Từ danh sách sản phẩm dưới đây, chọn đúng 4 sản phẩm phù hợp nhất với tình trạng da vừa phân tích:
{productJson}

OUTPUT: Chỉ trả về JSON THUẦN (không markdown, không giải thích):
{{
  ""Advice"": ""[Nhận xét 3-4 câu từ Bước 1]"",
  ""Moisture"": 65,
  ""Oil"": 40,
  ""Acne"": 30,
  ""Smoothness"": 75,
  ""SuggestedProductIds"": [1, 5, 12, 20],
  ""SuggestedReasons"": [""lý do sp1"", ""lý do sp2"", ""lý do sp3"", ""lý do sp4""]
}}";

                var requestData = new
                {
                    contents = new[] {
                        new {
                            parts = new object[] {
                                new { text = prompt },
                                new { inline_data = new { mime_type = mimeType, data = base64Image } }
                            }
                        }
                    },
                    generationConfig = new { response_mime_type = "application/json" }
                };

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(45);

                var httpContent = new StringContent(
                    JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
                var response   = await client.PostAsync(url, httpContent);
                var jsonString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using JsonDocument doc = JsonDocument.Parse(jsonString);

                    var aiRaw = doc.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString() ?? "";

                    // Làm sạch phòng Gemini vẫn wrap markdown
                    aiRaw = aiRaw.Replace("```json", "").Replace("```", "").Trim();

                    using JsonDocument result = JsonDocument.Parse(aiRaw);
                    var root = result.RootElement;

                    // --- Parse chỉ số (dùng GetDouble để xử lý cả 65 lẫn 65.0) ---
                    ViewBag.Moisture   = root.TryGetProperty("Moisture",   out var m) ? Clamp((int)m.GetDouble()) : 0;
                    ViewBag.Oil        = root.TryGetProperty("Oil",        out var o) ? Clamp((int)o.GetDouble()) : 0;
                    ViewBag.Acne       = root.TryGetProperty("Acne",       out var a) ? Clamp((int)a.GetDouble()) : 0;
                    ViewBag.Smoothness = root.TryGetProperty("Smoothness", out var s) ? Clamp((int)s.GetDouble()) : 0;
                    ViewBag.AiAdvice   = root.TryGetProperty("Advice",     out var adv) ? adv.GetString() : "";

                    // --- Lý do gợi ý sản phẩm ---
                    var reasons = new List<string>();
                    if (root.TryGetProperty("SuggestedReasons", out var rEl) && rEl.ValueKind == JsonValueKind.Array)
                        reasons = rEl.EnumerateArray().Select(x => x.GetString() ?? "").ToList();
                    ViewBag.SuggestedReasons = reasons;

                    // --- Lấy sản phẩm theo Id Gemini chọn ---
                    if (root.TryGetProperty("SuggestedProductIds", out var idsEl) && idsEl.ValueKind == JsonValueKind.Array)
                    {
                        var ids = idsEl.EnumerateArray().Select(x => x.GetInt32()).ToList();
                        var dbP = await _context.Products.Include(p => p.Category)
                                      .Where(p => ids.Contains(p.Id) && p.IsActive).ToListAsync();
                        suggestedProducts = dbP.OrderBy(p => ids.IndexOf(p.Id)).ToList();
                    }

                    // Fallback nếu Gemini không chọn đúng
                    if (suggestedProducts.Count == 0)
                        suggestedProducts = await FallbackProducts();
                }
                else
                {
                    ViewBag.AiAdvice = $"⚠️ LỖI GOOGLE API: {jsonString}";
                    SetRandomScores();
                    suggestedProducts = await FallbackProducts();
                }
            }
            catch (Exception ex)
            {
                ViewBag.AiAdvice = $"⚠️ LỖI C#: {ex.Message}";
                SetRandomScores();
                suggestedProducts = await FallbackProducts();
            }

            ViewBag.SkinType    = skinType;
            ViewBag.SkinConcern = skinConcern;
            return View(suggestedProducts);

            // ---- helpers ----
            int Clamp(int v) => Math.Max(0, Math.Min(100, v));

            void SetRandomScores()
            {
                var rnd = new Random();
                ViewBag.Moisture   = rnd.Next(30, 75);
                ViewBag.Oil        = rnd.Next(25, 80);
                ViewBag.Acne       = rnd.Next(15, 65);
                ViewBag.Smoothness = rnd.Next(40, 85);
            }

            async Task<List<Product>> FallbackProducts() =>
                await _context.Products.Include(p => p.Category)
                    .Where(p => p.IsActive).OrderBy(p => Guid.NewGuid()).Take(4).ToListAsync();
        }

        private async Task<string> CallGeminiAI(string skinType, string skinProblem)
        {
            try
            {
                string apiKey = "AIzaSyCj6PQ7WAAzQOATA1dnTKwVGzGCg5_CFtc";
                string url    = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";
                var requestData = new
                {
                    contents = new[] {
                        new { parts = new[] { new { text = $"Đóng vai chuyên gia da liễu Velvy Skin. Viết đúng 3 câu ngắn gọn tư vấn cách chăm sóc cho người có '{skinType}' và đang bị '{skinProblem}'. Trả lời trực tiếp, không dùng ký tự đặc biệt." } } }
                    }
                };
                using var client  = new HttpClient();
                var content       = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
                var response      = await client.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(json);
                    return doc.RootElement.GetProperty("candidates")[0]
                        .GetProperty("content").GetProperty("parts")[0]
                        .GetProperty("text").GetString() ?? "";
                }
            }
            catch { }
            return $"Da bạn thuộc loại {skinType}. Vấn đề chính là {skinProblem}. Hệ thống đề xuất lộ trình phục hồi chuyên sâu dưới đây.";
        }
    }
}