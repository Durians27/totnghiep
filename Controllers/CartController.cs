using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization; 
using System.Security.Claims; 
using VelvySkinWeb.Data;
using VelvySkinWeb.Models.ViewModels;
using VelvySkinWeb.Extensions;
using VelvySkinWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace VelvySkinWeb.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. HIỂN THỊ GIỎ HÀNG
        // ==========================================
        public IActionResult Index()
        {
            var cart = HttpContext.Session.GetJson<List<CartItem>>("Cart") ?? new List<CartItem>();
            ViewBag.GrandTotal = cart.Sum(item => item.Total);
            return View(cart);
        }

        // ==========================================
        // 2. THÊM VÀO GIỎ HÀNG (CÓ CHẶN TỒN KHO)
        // ==========================================
        public IActionResult AddToCart(int id, int quantity = 1, decimal selectedPrice = 0)
        {
            // ----------------------------------------------------
            // 🛑 CHỐT CHẶN BẢO VỆ: KIỂM TRA ĐĂNG NHẬP
            // ----------------------------------------------------
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                TempData["ErrorMsg"] = "Vui lòng đăng nhập tài khoản để thêm sản phẩm vào giỏ hàng!";
                string returnUrl = $"/Products/Details/{id}"; 
                return Redirect($"/Account/Login?ReturnUrl={returnUrl}");
            }

            var product = _context.Products.Find(id);
            if (product == null) return NotFound();

            var cart = HttpContext.Session.GetJson<List<CartItem>>("Cart") ?? new List<CartItem>();
            
            decimal finalPrice = selectedPrice > 0 ? selectedPrice : product.Price;
            var cartItem = cart.FirstOrDefault(c => c.ProductId == id && c.Price == finalPrice);

            if (product.StockQuantity <= 0)
            {
                TempData["ErrorMsg"] = $"Rất tiếc, sản phẩm '{product.Name}' hiện đã hết hàng!";
                return RedirectToAction("Index"); 
            }

            int currentQtyInCart = cartItem != null ? cartItem.Quantity : 0;
            if (currentQtyInCart + quantity > product.StockQuantity)
            {
                TempData["ErrorMsg"] = $"Không thể thêm! Kho chỉ còn {product.StockQuantity} sản phẩm. Bạn đang có {currentQtyInCart} sản phẩm loại này trong giỏ.";
                return RedirectToAction("Index"); 
            }

            if (cartItem == null)
            {
                string variantName = finalPrice > product.Price ? " (Bản Lớn)" : " (Tiêu chuẩn)";
                cart.Add(new CartItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name + variantName,
                    Price = finalPrice,                      
                    Quantity = quantity,
                    ImageUrl = product.ImageUrl
                });
            }
            else
            {
                cartItem.Quantity += quantity;
            }

            HttpContext.Session.SetJson("Cart", cart);
            TempData["SuccessMsg"] = $"Đã thêm {product.Name} vào giỏ!";
            
            string referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer))
            {
                return Redirect(referer); 
            }
            
            return RedirectToAction("Index", "Home"); 
        }

        // ==========================================
        // 3. CẬP NHẬT SỐ LƯỢNG TRONG GIỎ (+ / -)
        // ==========================================
        public IActionResult UpdateQuantity(int id, int quantity)
        {
            var cart = HttpContext.Session.GetJson<List<CartItem>>("Cart");
            if (cart != null)
            {
                var cartItem = cart.FirstOrDefault(c => c.ProductId == id);
                if (cartItem != null)
                {
                    var product = _context.Products.Find(id);
                    if (product != null && quantity <= product.StockQuantity)
                    {
                        cartItem.Quantity = quantity;
                        if (cartItem.Quantity <= 0) 
                        {
                            cart.Remove(cartItem);
                        }
                        HttpContext.Session.SetJson("Cart", cart);
                    }
                    else
                    {
                        TempData["ErrorMsg"] = "Số lượng vượt quá tồn kho!";
                    }
                }
            }
            return RedirectToAction("Index");
        }

        // ==========================================
        // 4. XÓA SẢN PHẨM KHỎI GIỎ
        // ==========================================
        [HttpGet]
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
       
        // ==========================================
        // 5. LÀM SẠCH GIỎ HÀNG
        // ==========================================
        public IActionResult ClearCart()
        {
            HttpContext.Session.Remove("Cart");
            return RedirectToAction("Index");
        }

        // ==========================================
        // 6. NHẬN NHỮNG MÓN ĐƯỢC CHECKBOX ĐỂ CHUẨN BỊ THANH TOÁN
        // ==========================================
        [HttpPost] 
        [Authorize]
        public IActionResult Checkout(List<int> selectedItems)
        {
            var cart = HttpContext.Session.GetJson<List<CartItem>>("Cart") ?? new List<CartItem>();
            
            if (selectedItems == null || !selectedItems.Any())
            {
                TempData["ErrorMsg"] = "Bạn chưa chọn sản phẩm nào để thanh toán!";
                return RedirectToAction("Index");
            }

            var itemsToCheckout = cart.Where(c => selectedItems.Contains(c.ProductId)).ToList();

            if (itemsToCheckout.Count == 0)
            {
                TempData["ErrorMsg"] = "Sản phẩm đã chọn không hợp lệ hoặc không có trong giỏ!";
                return RedirectToAction("Index");
            }

            HttpContext.Session.SetJson("ItemsToCheckout", itemsToCheckout);
            return RedirectToAction("Checkout"); 
        }

        // ==========================================
        // 7. TRANG THANH TOÁN (TỰ ĐỘNG ĐIỀN THÔNG TIN KHÁCH CŨ)
        // ==========================================
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            var itemsToCheckout = HttpContext.Session.GetJson<List<CartItem>>("ItemsToCheckout");
            
            if (itemsToCheckout == null || !itemsToCheckout.Any())
            {
                TempData["ErrorMsg"] = "Không có sản phẩm nào để thanh toán!";
                return RedirectToAction("Index");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var lastOrder = await _context.Orders
                                          .Where(o => o.UserId == userId)
                                          .OrderByDescending(o => o.OrderDate)
                                          .FirstOrDefaultAsync();

            if (lastOrder != null)
            {
                ViewBag.OldName = lastOrder.CustomerName;
                ViewBag.OldPhone = lastOrder.PhoneNumber;
                ViewBag.OldAddress = lastOrder.ShippingAddress;
            }
            else
            {
                ViewBag.OldName = ""; ViewBag.OldPhone = ""; ViewBag.OldAddress = "";
            }

            return View(itemsToCheckout);
        }

        // ==========================================
        // 8. XỬ LÝ CHỐT ĐƠN VÀ TRỪ TỒN KHO (TẠO ĐƠN HÀNG THẬT)
        // ==========================================
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(Order order, string PaymentMethod)
        {
            var itemsToCheckout = HttpContext.Session.GetJson<List<CartItem>>("ItemsToCheckout");
            if (itemsToCheckout == null || !itemsToCheckout.Any()) return RedirectToAction("Index");

            if (ModelState.IsValid)
            {
                order.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                order.OrderDate = DateTime.Now;
                order.OrderStatus = "Pending";
                order.PaymentMethod = PaymentMethod;

                decimal subTotal = itemsToCheckout.Sum(item => item.Total);
                decimal shippingFee = (subTotal > 0 && subTotal < 500000) ? 30000 : 0;
                order.TotalAmount = subTotal + shippingFee; 

                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); 

                foreach (var item in itemsToCheckout)
                {
                    var orderDetail = new OrderDetail
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price
                    };
                    _context.OrderDetails.Add(orderDetail);

                    var productInDb = await _context.Products.FindAsync(item.ProductId);
                    if (productInDb != null)
                    {
                        productInDb.StockQuantity -= item.Quantity;
                        _context.Products.Update(productInDb);
                    }
                }
                
                await _context.SaveChangesAsync(); 

                var mainCart = HttpContext.Session.GetJson<List<CartItem>>("Cart");
                if (mainCart != null)
                {
                    mainCart.RemoveAll(c => itemsToCheckout.Any(checkoutItem => checkoutItem.ProductId == c.ProductId));
                    HttpContext.Session.SetJson("Cart", mainCart); 
                }

                HttpContext.Session.Remove("ItemsToCheckout");

                if (PaymentMethod == "Chuyển khoản")
                {
                    return RedirectToAction("BankTransfer", new { id = order.Id });
                }
                
                // ĐÃ FIX: Chuyền thêm biến pm cho COD
                return RedirectToAction("OrderSuccess", new { id = order.Id, pm = PaymentMethod });
            }

            return View("Checkout", itemsToCheckout); 
        }

        // ==========================================
        // TRANG THANH TOÁN CHUYỂN KHOẢN (BƯỚC ĐỆM)
        // ==========================================
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> BankTransfer(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            return View(order);
        }
        
        // ==========================================
        // 9. TRANG THÔNG BÁO THÀNH CÔNG (ĐÃ UPDATE ĐỂ MÓC DATA)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> OrderSuccess(int id, string pm = "Thanh toán khi nhận hàng (COD)")
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return RedirectToAction("Index", "Home");

            ViewBag.PaymentMethod = pm;
            return View(order);
        }
    }
}