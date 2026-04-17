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

        [Authorize(Roles = "Admin, Staff")] 
        public async Task<IActionResult> Index(string status = "all")
        {
            var orders = _context.Orders.AsQueryable();


            switch (status.ToLower())
            {
                case "pending":
                    orders = orders.Where(o => o.OrderStatus == "Chờ thanh toán" || o.OrderStatus == "Đang xử lý" || o.OrderStatus == "Chờ xác nhận");
                    break;
                case "shipping":
                    orders = orders.Where(o => o.OrderStatus == "Đang giao");
                    break;
                case "completed":
                    orders = orders.Where(o => o.OrderStatus == "Hoàn thành" || o.OrderStatus.Contains("Đã thanh toán") || o.OrderStatus == "Đã giao thành công");
                    break;
                case "cancelled":
                    orders = orders.Where(o => o.OrderStatus.Contains("Đã hủy") || o.OrderStatus == "Hủy");
                    break;
            }

            var finalOrders = await orders.OrderByDescending(o => o.OrderDate).ToListAsync();
            ViewBag.CurrentStatus = status.ToLower();

            return View(finalOrders);
        }

        [HttpGet("Admin/Orders/Details/{id}")] 
        [Authorize(Roles = "Admin, Staff")]
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
        [Authorize(Roles = "Admin, Staff")]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            string oldStatus = order.OrderStatus ?? "Chưa rõ"; 
            var oldData = new { Trạng_Thái = oldStatus };
            

            string finalVietnameseStatus = newStatus == "Completed" ? "Hoàn thành" : 
                                           newStatus == "Pending" ? "Đang xử lý" : 
                                           newStatus == "Shipping" ? "Đang giao" : 
                                           newStatus == "Cancelled" ? "Đã hủy" : newStatus;


            order.OrderStatus = finalVietnameseStatus;
            var newData = new { Trạng_Thái = finalVietnameseStatus };


            string notiTitle = "";
            string notiMessage = "";
            string notiIcon = "fa-box";

            if (finalVietnameseStatus == "Đã xác nhận" || finalVietnameseStatus == "Đang xử lý")
            {
                notiTitle = "Đơn hàng đã được xác nhận";
                notiMessage = $"Đơn hàng #VS-{order.OrderDate.ToString("yyMMdd")}-{order.Id} đã được shop xác nhận và đang đóng gói.";
                notiIcon = "fa-box-open"; 
            }
            else if (finalVietnameseStatus == "Đang giao")
            {
                notiTitle = "Đơn hàng đang trên đường giao!";
                string shipInfo = !string.IsNullOrEmpty(order.ShipperName) ? $" Shipper {order.ShipperName} ({order.ShipperPhone}) sẽ sớm liên hệ với bạn." : " Kiện hàng đã được bàn giao cho đối tác vận chuyển.";
                notiMessage = $"Đơn hàng #VS-{order.OrderDate.ToString("yyMMdd")}-{order.Id} đang hướng về phía bạn.{shipInfo}";
                notiIcon = "fa-truck-fast"; 
            }
            else if (finalVietnameseStatus == "Hoàn thành" || finalVietnameseStatus == "Đã giao" || finalVietnameseStatus == "Đã giao thành công")
            {
                notiTitle = "Giao hàng thành công";
                notiMessage = $"Đơn hàng #VS-{order.OrderDate.ToString("yyMMdd")}-{order.Id} đã giao thành công. Cảm ơn bạn đã tin tưởng Velvy Skin!";
                notiIcon = "fa-circle-check"; 
            }
            else if (finalVietnameseStatus.Contains("Đã hủy"))
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


            var log = new VelvySkinWeb.Models.AuditLog
            {
                Username = User.Identity?.Name ?? "Hệ thống",
                ActionType = "UPDATE",
                TableName = "Orders",
                Description = $"Admin đã đổi trạng thái đơn hàng #VS-{id} từ '{oldStatus}' sang '{finalVietnameseStatus}'",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Không xác định",
                OldValues = System.Text.Json.JsonSerializer.Serialize(oldData),
                NewValues = System.Text.Json.JsonSerializer.Serialize(newData),
                Timestamp = DateTime.Now
            };
            _context.AuditLogs.Add(log);

            await _context.SaveChangesAsync();

            if (finalVietnameseStatus == "Đang giao")
            {
                return RedirectToAction("ShippingDetails", new { id = id });
            }
            
            return RedirectToAction("Details", new { id = id });
        }

        [HttpGet("Admin/Orders/ShippingDetails/{id}")] 
        [Authorize(Roles = "Admin, Staff")]
        public async Task<IActionResult> ShippingDetails(int id)
        {
            var order = await _context.Orders
                                      .Include(o => o.OrderDetails)
                                      .ThenInclude(od => od.Product)
                                      .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();


            if (order.OrderStatus != "Đang giao")
            {
                TempData["ErrorMsg"] = "Đơn hàng này chưa được bàn giao cho ĐVVC!";
                return RedirectToAction("Details", new { id = id });
            }

            return View(order);
        }

        [HttpGet("Admin/Orders/CompletedDetails/{id}")] 
        [Authorize(Roles = "Admin, Staff")]
        public async Task<IActionResult> CompletedDetails(int id)
        {
            var order = await _context.Orders
                                      .Include(o => o.OrderDetails)
                                      .ThenInclude(od => od.Product)
                                      .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();


            if (order.OrderStatus != "Hoàn thành" && order.OrderStatus != "Đã giao thành công")
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
        [Authorize(Roles = "Admin, Staff")]
        public async Task<IActionResult> UpdateShipperInfo(int id, string shipperName, string shipperPhone)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            var oldData = new { Tên_Shipper = order.ShipperName ?? "Trống", SĐT = order.ShipperPhone ?? "Trống" };
            var newData = new { Tên_Shipper = shipperName, SĐT = shipperPhone };

            order.ShipperName = shipperName;
            order.ShipperPhone = shipperPhone;
            
            var log = new AuditLog {
                Username = User.Identity?.Name ?? "Hệ thống",
                ActionType = "UPDATE",
                TableName = "Orders",
                Description = $"Đã cập nhật thông tin Shipper cho đơn hàng #{id}",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Không xác định",
                OldValues = System.Text.Json.JsonSerializer.Serialize(oldData),
                NewValues = System.Text.Json.JsonSerializer.Serialize(newData),
                Timestamp = DateTime.Now
            };
            _context.AuditLogs.Add(log);

            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = "Đã cập nhật thông tin Shipper thành công! Giờ có thể gọi điện luôn rồi đó.";
            return RedirectToAction("ShippingDetails", new { id = id });
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Staff")]
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