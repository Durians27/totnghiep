using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization; 
using System.Security.Claims; 
using VelvySkinWeb.Data;
using VelvySkinWeb.Models.ViewModels;
using VelvySkinWeb.Extensions;
using VelvySkinWeb.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http;
using Net.payOS;
using Net.payOS.Types;

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

        public IActionResult AddToCart(int id, int quantity = 1, decimal selectedPrice = 0)
        {
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
            if (!string.IsNullOrEmpty(referer)) return Redirect(referer); 
            
            return RedirectToAction("Index", "Home"); 
        }

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
                        if (cartItem.Quantity <= 0) cart.Remove(cartItem);
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
        
        public IActionResult ClearCart()
        {
            HttpContext.Session.Remove("Cart");
            return RedirectToAction("Index");
        }

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

            string appliedCoupon = HttpContext.Session.GetString("AppliedCoupon");
            string discountAmountStr = HttpContext.Session.GetString("DiscountAmount");
            
            ViewBag.AppliedCoupon = appliedCoupon;
            ViewBag.DiscountAmount = !string.IsNullOrEmpty(discountAmountStr) ? Convert.ToDecimal(discountAmountStr) : 0m;

            return View(itemsToCheckout);
        }

        // =======================================================
        // 🔥 GIẢI PHẪU THUẬT TOÁN: BƯỚC TẠO ĐƠN NHÁP & CHUYỂN HƯỚNG
        // =======================================================
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
                
                // 🛑 CHỈ LƯU NHÁP: Đổi thành Chờ thanh toán, CHƯA TRỪ KHO, CHƯA XÓA GIỎ, CHƯA TÍNH VOUCHER
                order.OrderStatus = "Chờ thanh toán"; 
                order.PaymentMethod = PaymentMethod;

                decimal subTotal = itemsToCheckout.Sum(item => item.Total);
                decimal shippingFee = (subTotal > 0 && subTotal < 500000) ? 30000 : 0;
                
                string appliedCoupon = HttpContext.Session.GetString("AppliedCoupon");
                string discountAmountStr = HttpContext.Session.GetString("DiscountAmount");
                decimal discountAmount = !string.IsNullOrEmpty(discountAmountStr) ? Convert.ToDecimal(discountAmountStr) : 0m;

                order.TotalAmount = subTotal + shippingFee - discountAmount; 

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
                }
                await _context.SaveChangesAsync(); 
                // TUYỆT ĐỐI DỪNG LẠI Ở ĐÂY, CHƯA CHẠM VÀO SỐ LƯỢNG TỒN KHO!

                // PHÂN LUỒNG XỬ LÝ
                // NẾU LÀ THANH TOÁN TIỀN MẶT (COD) -> Xử lý thành công luôn
                if (PaymentMethod != "Ví ZaloPay" && PaymentMethod != "Chuyển khoản")
                {
                    order.OrderStatus = "Đang xử lý";
                    await _context.SaveChangesAsync();
                    await ProcessOrderSuccessLogic(order.Id); // Kích hoạt trừ kho, dọn giỏ
                    return RedirectToAction("OrderSuccess", new { id = order.Id, pm = PaymentMethod });
                }

                // NẾU LÀ PAYOS (CHUYỂN KHOẢN)
                if (PaymentMethod == "Chuyển khoản")
                {
            
                    string clientId = "0b6a8fda-43f6-41bf-82a2-681b814d7b99";
                    string apiKey = "5f7c0d51-f094-4733-a4d4-1dd022d60b88";
                    string checksumKey = "ba9efd887d736248ac00e2c3a0c8d396dde9046173ce5d084fa8747d0fc6f57f";

                    Net.payOS.PayOS payOS = new Net.payOS.PayOS(clientId, apiKey, checksumKey);
                    long orderCode = long.Parse(DateTime.Now.ToString("yyMMddHHmmss") + order.Id.ToString());

                    ItemData item = new ItemData($"Thanh toan don hang {order.Id}", 1, (int)order.TotalAmount);
                    List<ItemData> items = new List<ItemData> { item };

                    PaymentData paymentData = new PaymentData(
                        orderCode: orderCode,
                        amount: (int)order.TotalAmount,
                        description: $"Thanh toan don {order.Id}",
                        items: items,
                        cancelUrl: $"https://localhost:7079/Cart/PaymentFail?orderId={order.Id}", // 🔥 BẤM HỦY SẼ CHẠY VÀO HÀM BÁO LỖI
                        returnUrl: $"https://localhost:7079/Cart/PayOSCallback?orderId={order.Id}" 
                    );

                    try
                    {
                        CreatePaymentResult createPayment = await payOS.createPaymentLink(paymentData);
                        return Redirect(createPayment.checkoutUrl);
                    }
                    catch (Exception ex)
                    {
                        return RedirectToAction("PaymentFail", new { orderId = order.Id, error = ex.Message });
                    }
                }
                
                // ĐÃ THÊM LUỒNG TẠO ĐƠN ZALOPAY TẠI ĐÂY
                if (PaymentMethod == "Ví ZaloPay")
                {
                    string host = $"{Request.Scheme}://{Request.Host}";
                    var zaloPayService = new VelvySkinWeb.Services.ZaloPayService();
                    
                    string zaloPayUrl = await zaloPayService.CreatePaymentUrl(order.Id, 2000m, host);
                    
                    if (!string.IsNullOrEmpty(zaloPayUrl))
                    {
                        return Redirect(zaloPayUrl); // Đá văng khách sang trang quét mã QR của ZaloPay
                    }
                    else
                    {
                        return RedirectToAction("PaymentFail", new { orderId = order.Id, error = "Lỗi khởi tạo cổng ZaloPay Sandbox" });
                    }
                }
                
                return RedirectToAction("OrderSuccess", new { id = order.Id, pm = PaymentMethod });
            }

            return View("Checkout", itemsToCheckout); 
        }

        // =======================================================
        // 🔥 HÀM ĐÓN KHÁCH TỪ CỔNG THANH TOÁN (XỬ LÝ THÀNH CÔNG/HỦY)
        // =======================================================

        [HttpGet]
        public async Task<IActionResult> PayOSCallback(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null && order.OrderStatus == "Chờ thanh toán")
            {
                order.OrderStatus = "Đã thanh toán (Chuyển khoản)";
                await _context.SaveChangesAsync();
                await ProcessOrderSuccessLogic(orderId); // Tiền ting ting rồi mới trừ kho
            }
            
            TempData["SuccessMsg"] = "Tuyệt vời! Thanh toán VietQR tự động thành công.";
            return RedirectToAction("OrderSuccess", new { id = orderId, pm = "Chuyển khoản ngân hàng" });
        }

        // ĐÃ THÊM HÀM CALLBACK CHUYÊN DỤNG CHO ZALOPAY (THAY THẾ MOMO)
        [HttpGet]
        public async Task<IActionResult> ZaloPayCallback(int status, string apptransid)
        {
            // ZaloPay trả về apptransid dạng "240704_123_4567" -> Mình split ra để lấy cái order.Id (nằm ở giữa)
            if (string.IsNullOrEmpty(apptransid)) return RedirectToAction("Index");
            
            string[] parts = apptransid.Split('_');
            if (parts.Length < 2) return RedirectToAction("Index");
            
            int orderId = int.Parse(parts[1]);
            var order = await _context.Orders.FindAsync(orderId);

            if (status == 1) 
            {
                if (order != null && order.OrderStatus == "Chờ thanh toán")
                {
                    order.OrderStatus = "Đã thanh toán (ZaloPay)";
                    await _context.SaveChangesAsync();
                    await ProcessOrderSuccessLogic(orderId); // Trừ kho, dọn giỏ
                }
                TempData["SuccessMsg"] = "Tuyệt vời! Giao dịch ZaloPay của bạn đã thành công.";
                return RedirectToAction("OrderSuccess", new { id = orderId, pm = "Ví ZaloPay" });
            }
            else
            {
                // Khách bấm Hủy ở cổng ZaloPay
                return RedirectToAction("PaymentFail", new { orderId = orderId, error = "Giao dịch ZaloPay bị hủy bỏ" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> PaymentFail(int orderId, string error = "")
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null && order.OrderStatus == "Chờ thanh toán")
            {
                order.OrderStatus = "Đã hủy (Lỗi thanh toán)";
                await _context.SaveChangesAsync();
            }
            // ĐÁ VỀ GIỎ HÀNG, ĐỒ TRONG GIỎ VẪN CÒN Y NGUYÊN ĐỂ KHÁCH MUA LẠI
            TempData["ErrorMsg"] = "Giao dịch thanh toán đã bị hủy hoặc thất bại! " + error;
            return RedirectToAction("Index", "Cart"); 
        }

        // =======================================================
        // 🧠 HÀM LÕI: TRỪ KHO, CẬP NHẬT VOUCHER & XÓA GIỎ KHI CHẮC CHẮN CÓ TIỀN
        // =======================================================
        private async Task ProcessOrderSuccessLogic(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return;

            // 1. Trừ tồn kho (Bốc từ DB cho chắc chắn, không dùng Session)
            var orderDetails = await _context.OrderDetails.Where(od => od.OrderId == orderId).ToListAsync();
            foreach (var item in orderDetails)
            {
                var productInDb = await _context.Products.FindAsync(item.ProductId);
                if (productInDb != null)
                {
                    productInDb.StockQuantity -= item.Quantity;
                    _context.Products.Update(productInDb);
                }
            }

            // 2. Tăng lượt sử dụng Voucher
            string appliedCoupon = HttpContext.Session.GetString("AppliedCoupon");
            if (!string.IsNullOrEmpty(appliedCoupon))
            {
                var couponInDb = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == appliedCoupon);
                if (couponInDb != null)
                {
                    couponInDb.UsedCount += 1;
                    _context.Coupons.Update(couponInDb);
                }
            }

            // 3. Gửi chuông thông báo
            var notiOrder = new VelvySkinWeb.Models.Notification
            {
                UserId = order.UserId, 
                Title = "Đặt hàng thành công!",
                Message = $"Đơn hàng #VS-{order.OrderDate.ToString("yyMMdd")}-{order.Id} của bạn đã được ghi nhận và đang chờ shop xác nhận. Cảm ơn bạn đã tin tưởng Velvy Skin!",
                Type = "order",
                Icon = "fa-clipboard-check",
                IsRead = false,
                CreatedAt = DateTime.Now
            };
            _context.Notifications.Add(notiOrder);

            await _context.SaveChangesAsync(); 

            // 4. Dọn dẹp Hàng hóa khỏi Giỏ
            var itemsToCheckout = HttpContext.Session.GetJson<List<CartItem>>("ItemsToCheckout");
            var mainCart = HttpContext.Session.GetJson<List<CartItem>>("Cart");
            if (mainCart != null && itemsToCheckout != null)
            {
                mainCart.RemoveAll(c => itemsToCheckout.Any(checkoutItem => checkoutItem.ProductId == c.ProductId));
                HttpContext.Session.SetJson("Cart", mainCart); 
            }

            // 5. Xóa Session Voucher để tránh áp mã bậy bạ cho đơn sau
            HttpContext.Session.Remove("ItemsToCheckout");
            HttpContext.Session.Remove("AppliedCoupon");
            HttpContext.Session.Remove("DiscountAmount");
        }

        // =======================================================
        // 🎫 HÀM VOUCHER (ĐÃ TRẢ LẠI NGUYÊN BẢN 100% CỦA SẾP)
        // =======================================================
        [HttpPost]
        public async Task<IActionResult> ApplyCoupon(string code)
        {
            var sessionCart = HttpContext.Session.GetString("Cart");
            if (string.IsNullOrEmpty(sessionCart)) 
                return Json(new { success = false, message = "Giỏ hàng của bạn đang trống!" });

            var cartItems = System.Text.Json.JsonSerializer.Deserialize<List<CartItem>>(sessionCart);
            
            double subTotal = (double)cartItems.Sum(c => c.Price * c.Quantity);
            double currentShippingFee = (subTotal > 0 && subTotal < 500000) ? 30000 : 0;

            code = code?.Trim().ToUpper();
            var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == code);

            if (coupon == null || !coupon.IsActive)
                return Json(new { success = false, message = "Mã khuyến mãi không tồn tại hoặc đã bị khóa!" });

            if (coupon.EndDate < DateTime.Now)
                return Json(new { success = false, message = "Rất tiếc, mã khuyến mãi này đã hết hạn!" });

            if (coupon.MaxUses > 0 && coupon.UsedCount >= coupon.MaxUses)
                return Json(new { success = false, message = "Mã này đã hết lượt sử dụng!" });

            if (subTotal < coupon.MinOrderValue)
                return Json(new { success = false, message = $"Đơn hàng cần đạt tối thiểu {coupon.MinOrderValue:N0}đ để dùng mã này!" });

            double discountAmount = 0;
            if (coupon.DiscountType == "Amount")
            {
                discountAmount = coupon.DiscountValue;
            }
            else if (coupon.DiscountType == "Percent")
            {
                discountAmount = subTotal * (coupon.DiscountValue / 100.0);
                if (coupon.MaxDiscountAmount > 0 && discountAmount > coupon.MaxDiscountAmount)
                {
                    discountAmount = coupon.MaxDiscountAmount;
                }
            }
            else if (coupon.DiscountType == "FreeShip" || coupon.DiscountType == "Freeship")
            {
                discountAmount = currentShippingFee;
            }

            if (discountAmount > subTotal + currentShippingFee) discountAmount = subTotal + currentShippingFee;

            HttpContext.Session.SetString("AppliedCoupon", coupon.Code);
            HttpContext.Session.SetString("DiscountAmount", discountAmount.ToString());

            return Json(new { 
                success = true, 
                message = "Áp dụng mã thành công!", 
                discount = discountAmount, 
                discountType = coupon.DiscountType, 
                newTotal = subTotal + currentShippingFee - discountAmount 
            });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> BankTransfer(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            return View(order);
        }
        
        [HttpGet]
        public async Task<IActionResult> OrderSuccess(int id, string pm = "Thanh toán khi nhận hàng (COD)")
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return RedirectToAction("Index", "Home");

            ViewBag.PaymentMethod = pm;
            return View(order);
        }

        private string ComputeHmacSha256(string message, string secretKey)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var messageBytes = Encoding.UTF8.GetBytes(message);
            using (var hmac = new HMACSHA256(keyBytes))
            {
                var hashBytes = hmac.ComputeHash(messageBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }
    }
}