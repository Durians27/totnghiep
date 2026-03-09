using Microsoft.AspNetCore.Mvc;
using VelvySkinWeb.Data;
using VelvySkinWeb.Models.ViewModels;
using VelvySkinWeb.Extensions;
using VelvySkinWeb.Models;
namespace VelvySkinWeb.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var cart = HttpContext.Session.GetJson<List<CartItem>>("Cart") ?? new List<CartItem>();

            ViewBag.GrandTotal = cart.Sum(item => item.Total);

            return View(cart);
        }
        // ==========================================
        // 2. THÊM VÀO GIỎ HÀNG (ĐÃ NÂNG CẤP CHẶN TỒN KHO)
        // ==========================================
        public IActionResult AddToCart(int id)
        {
            var product = _context.Products.Find(id);
            if (product == null) return NotFound();

            var cart = HttpContext.Session.GetJson<List<CartItem>>("Cart") ?? new List<CartItem>();
            var cartItem = cart.FirstOrDefault(c => c.ProductId == id);

            // --------------------------------------------------------
            // CHỐT CHẶN 1: SẢN PHẨM ĐÃ HẾT SẠCH (Tồn kho = 0)
            // --------------------------------------------------------
            if (product.StockQuantity <= 0)
            {
                TempData["ErrorMsg"] = $"Rất tiếc, sản phẩm '{product.Name}' hiện đã hết hàng!";
                return RedirectToAction("Index"); // Đẩy về trang Giỏ hàng để xem lỗi
            }

            // --------------------------------------------------------
            // CHỐT CHẶN 2: KHÁCH MUA QUÁ SỐ LƯỢNG TRONG KHO
            // --------------------------------------------------------
            int currentQtyInCart = cartItem != null ? cartItem.Quantity : 0;
            if (currentQtyInCart + 1 > product.StockQuantity)
            {
                TempData["ErrorMsg"] = $"Không thể thêm! Kho chỉ còn {product.StockQuantity} sản phẩm '{product.Name}'. Bạn đang có {currentQtyInCart} sản phẩm này trong giỏ.";
                return RedirectToAction("Index"); // Đẩy về trang Giỏ hàng để xem lỗi
            }

            // NẾU VƯỢT QUA 2 CHỐT CHẶN TRÊN THÌ CHO PHÉP THÊM BÌNH THƯỜNG
            if (cartItem == null)
            {
                cart.Add(new CartItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Price = product.Price,
                    Quantity = 1,
                    ImageUrl = product.ImageUrl
                });
            }
            else
            {
                cartItem.Quantity += 1;
            }

            HttpContext.Session.SetJson("Cart", cart);
            TempData["SuccessMsg"] = $"Đã thêm {product.Name} vào giỏ!";
            
            return RedirectToAction("Index"); 
        }

        public IActionResult RemoveFromCart(int id)
        {
            var cart = HttpContext.Session.GetJson<List<CartItem>>("Cart");
            if (cart != null)
            {
                cart.RemoveAll(c => c.ProductId == id);
                HttpContext.Session.SetJson("Cart", cart);
            }
            return RedirectToAction("Index");
        }
       
        public IActionResult ClearCart()
        {
            HttpContext.Session.Remove("Cart");
            return RedirectToAction("Index");
        }

        // ==========================================
        // 5. HIỂN THỊ FORM THANH TOÁN (GET)
        // ==========================================
        // Bắt buộc khách phải Đăng nhập mới được vào trang Thanh toán
        [Microsoft.AspNetCore.Authorization.Authorize] 
        [HttpGet]
        public IActionResult Checkout()
        {
            var cart = HttpContext.Session.GetJson<List<CartItem>>("Cart");
            if (cart == null || !cart.Any())
            {
                return RedirectToAction("Index"); // Giỏ trống thì đá về trang Giỏ hàng
            }

            ViewBag.GrandTotal = cart.Sum(item => item.Total);
            return View(new Order()); // Trả về form trống cho khách điền
        }

        // ==========================================
        // 6. XỬ LÝ CHỐT ĐƠN VÀ TRỪ TỒN KHO (POST)
        // ==========================================
        [Microsoft.AspNetCore.Authorization.Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(Order order)
        {
            var cart = HttpContext.Session.GetJson<List<CartItem>>("Cart");
            if (cart == null || !cart.Any()) return RedirectToAction("Index");

            if (ModelState.IsValid)
            {
                // 1. Ghi thông tin Đơn Hàng (Order)
                order.UserId = User.Identity?.Name; // Lấy Email tài khoản đang đăng nhập
                order.OrderDate = DateTime.Now;
                order.OrderStatus = "Pending";
                order.TotalAmount = cart.Sum(item => item.Total);

                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // Lưu để sinh ra mã OrderId

                // 2. Ghi chi tiết từng chai mỹ phẩm (OrderDetail) & Trừ Tồn Kho
                foreach (var item in cart)
                {
                    var orderDetail = new OrderDetail
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price
                    };
                    _context.OrderDetails.Add(orderDetail);

                    // THUẬT TOÁN TRỪ TỒN KHO
                    var productInDb = await _context.Products.FindAsync(item.ProductId);
                    if (productInDb != null)
                    {
                        productInDb.StockQuantity -= item.Quantity; // Lấy tồn kho trừ đi số lượng khách mua
                        _context.Products.Update(productInDb);
                    }
                }
                
                await _context.SaveChangesAsync(); // Lưu toàn bộ xuống SQL

                // 3. Xóa sạch giỏ hàng trên RAM
                HttpContext.Session.Remove("Cart");

                // 4. Chuyển hướng sang trang Thành công
                return RedirectToAction("OrderSuccess", new { id = order.Id });
            }

            ViewBag.GrandTotal = cart.Sum(item => item.Total);
            return View(order);
        }

        // ==========================================
        // 7. TRANG THÔNG BÁO THÀNH CÔNG
        // ==========================================
        public IActionResult OrderSuccess(int id)
        {
            ViewBag.OrderId = id;
            return View();
        }
    }
}