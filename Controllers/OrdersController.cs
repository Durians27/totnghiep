using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims; 
using VelvySkinWeb.Data;
using VelvySkinWeb.Models;

namespace VelvySkinWeb.Controllers
{
    [Authorize] 
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize(Roles = "Admin")] 
        public async Task<IActionResult> Index(string status = "all")
        {
            var orders = _context.Orders.AsQueryable();

            switch (status.ToLower())
            {
                case "pending":
                    orders = orders.Where(o => o.OrderStatus == "Pending");
                    break;
                case "shipping":
                    orders = orders.Where(o => o.OrderStatus == "Shipping");
                    break;
                case "completed":
                    orders = orders.Where(o => o.OrderStatus == "Completed");
                    break;
                case "cancelled":
                    orders = orders.Where(o => o.OrderStatus == "Cancelled");
                    break;
            }

            var finalOrders = await orders.OrderByDescending(o => o.OrderDate).ToListAsync();
            ViewBag.CurrentStatus = status.ToLower();

            return View(finalOrders);
        }

        [HttpGet("Admin/Orders/Details/{id}")] 
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                                      .Include(o => o.OrderDetails)
                                      .ThenInclude(od => od.Product)
                                      .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            return View(order);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            // 🔥 1. Bật camera: Lưu lại trạng thái CŨ trước khi đổi
            string oldStatus = order.OrderStatus ?? "Chưa rõ"; 
            
            order.OrderStatus = newStatus;

            // ==========================================================
            // PHẦN 1: GIỮ NGUYÊN CODE GỬI THÔNG BÁO CHO KHÁCH CỦA SẾP
            // ==========================================================
            string notiTitle = "";
            string notiMessage = "";
            string notiIcon = "fa-box";

            if (newStatus == "Đã xác nhận" || newStatus == "Confirmed")
            {
                notiTitle = "Đơn hàng đã được xác nhận";
                notiMessage = $"Đơn hàng #VS-{order.OrderDate.ToString("yyMMdd")}-{order.Id} đã được shop xác nhận và đang đóng gói.";
                notiIcon = "fa-box-open"; 
            }
            else if (newStatus == "Shipping" || newStatus == "Đang giao")
            {
                notiTitle = "Đơn hàng đang trên đường giao!";
                string shipInfo = !string.IsNullOrEmpty(order.ShipperName) ? $" Shipper {order.ShipperName} ({order.ShipperPhone}) sẽ sớm liên hệ với bạn." : " Kiện hàng đã được bàn giao cho đối tác vận chuyển.";
                notiMessage = $"Đơn hàng #VS-{order.OrderDate.ToString("yyMMdd")}-{order.Id} đang hướng về phía bạn.{shipInfo}";
                notiIcon = "fa-truck-fast"; 
            }
            else if (newStatus == "Completed" || newStatus == "Hoàn thành" || newStatus == "Đã giao" || newStatus == "Thành công")
            {
                notiTitle = "Giao hàng thành công";
                notiMessage = $"Đơn hàng #VS-{order.OrderDate.ToString("yyMMdd")}-{order.Id} đã giao thành công. Cảm ơn bạn đã tin tưởng Velvy Skin!";
                notiIcon = "fa-circle-check"; 
            }
            else if (newStatus == "Cancelled" || newStatus == "Đã hủy")
            {
                notiTitle = "Đơn hàng đã bị hủy";
                notiMessage = $"Đơn hàng #VS-{order.OrderDate.ToString("yyMMdd")}-{order.Id} đã bị hủy. Vui lòng liên hệ CSKH nếu bạn cần hỗ trợ thêm.";
                notiIcon = "fa-ban"; 
            }

            if (!string.IsNullOrEmpty(notiTitle))
            {
                var notiStatus = new VelvySkinWeb.Models.Notification
                {
                    UserId = order.UserId,
                    Title = notiTitle,
                    Message = notiMessage,
                    Type = "order",
                    Icon = notiIcon,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                };
                _context.Notifications.Add(notiStatus);
            }

            // ==========================================================
            // 🔥 PHẦN 2: CHÈN THÊM CODE AUDIT LOG CHO ADMIN (CÓ PHIÊN DỊCH)
            // ==========================================================
            
            // 1. Bộ dịch thuật ngầm (Chỉ dịch cho Log, không đổi DB)
            string logOldStatus = oldStatus == "Completed" ? "Hoàn thành" : 
                                  oldStatus == "Pending" ? "Chờ xác nhận" : 
                                  oldStatus == "Shipping" ? "Đang giao" : 
                                  oldStatus == "Cancelled" ? "Đã hủy" : oldStatus;

            string logNewStatus = newStatus == "Completed" ? "Hoàn thành" : 
                                  newStatus == "Pending" ? "Chờ xác nhận" : 
                                  newStatus == "Shipping" ? "Đang giao" : 
                                  newStatus == "Cancelled" ? "Đã hủy" : newStatus;

            // 2. Ghi vào sổ
            var log = new VelvySkinWeb.Models.AuditLog
            {
                Username = User.Identity?.Name ?? "Hệ thống",
                ActionType = "UPDATE",
                TableName = "Orders",
                Description = $"Admin đã đổi trạng thái đơn hàng #VS-{id} từ '{logOldStatus}' sang '{logNewStatus}'"
            };
            _context.AuditLogs.Add(log);

            // ==========================================================
            // LƯU TẤT CẢ CÙNG LÚC (Đơn hàng + Thông báo + Nhật ký)
            // ==========================================================
            await _context.SaveChangesAsync();

            if (newStatus == "Shipping" || newStatus == "Đang giao")
            {
                return RedirectToAction("ShippingDetails", new { id = id });
            }
            
            return RedirectToAction("Details", new { id = id });
        }

        [HttpGet("Admin/Orders/ShippingDetails/{id}")] 
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ShippingDetails(int id)
        {
            var order = await _context.Orders
                                      .Include(o => o.OrderDetails)
                                      .ThenInclude(od => od.Product)
                                      .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            if (order.OrderStatus != "Shipping" && order.OrderStatus != "Đang giao")
            {
                TempData["ErrorMsg"] = "Đơn hàng này chưa được bàn giao cho ĐVVC!";
                return RedirectToAction("Details", new { id = id });
            }

            return View(order);
        }

        [HttpGet("Admin/Orders/CompletedDetails/{id}")] 
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CompletedDetails(int id)
        {
            var order = await _context.Orders
                                      .Include(o => o.OrderDetails)
                                      .ThenInclude(od => od.Product)
                                      .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            if (order.OrderStatus != "Completed" && order.OrderStatus != "Thành công" && order.OrderStatus != "Đã giao")
            {
                TempData["ErrorMsg"] = "Đơn hàng này chưa hoàn tất!";
                return RedirectToAction("Orders", "Admin");
            }

            return View(order);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _context.Orders
                                      .Include(o => o.OrderDetails) 
                                      .FirstOrDefaultAsync(o => o.Id == id);

            if (order != null)
            {
                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = $"Đã xóa thành công đơn hàng #{id}!";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> History()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var orders = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product) 
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate) 
                .ToListAsync();

            return View(orders);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> OrderInfo(int id)
        {
            var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            
            var order = await _context.Orders
                                      .Include(o => o.OrderDetails)
                                      .ThenInclude(od => od.Product) 
                                      .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return RedirectToAction("History"); 
            }

            return View(order);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateShipperInfo(int id, string shipperName, string shipperPhone)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.ShipperName = shipperName;
            order.ShipperPhone = shipperPhone;
            
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = "Đã cập nhật thông tin Shipper thành công! Giờ có thể gọi điện luôn rồi đó.";
            return RedirectToAction("ShippingDetails", new { id = id });
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PrintInvoice(int id)
        {
            var order = await _context.Orders
                                      .Include(o => o.OrderDetails)
                                      .ThenInclude(od => od.Product)
                                      .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                TempData["ErrorMsg"] = "Không tìm thấy hóa đơn này!";
                return RedirectToAction("Index"); 
            }

            return View(order);
        }
    }
}