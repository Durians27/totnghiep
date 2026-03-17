using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims; // Thư viện để lấy ID của User
using VelvySkinWeb.Data;
using VelvySkinWeb.Models;

namespace VelvySkinWeb.Controllers
{
    // ĐÃ FIX: Chỉ dùng [Authorize] chung để Khách hàng cũng vào được controller này
    [Authorize] 
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================================
        // PHẦN 1: GÓC NHÌN CỦA ADMIN (Phải có quyền Admin mới được đụng vào)
        // =========================================================================

        [Authorize(Roles = "Admin")] // Gắn khóa Admin riêng cho hàm này
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

        [Authorize(Roles = "Admin")] // Gắn khóa Admin riêng cho hàm này
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
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")] // Gắn khóa Admin riêng cho hàm này
        public async Task<IActionResult> UpdateStatus(int id, string newStatus)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.OrderStatus = newStatus;
            _context.Update(order);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = $"Đã cập nhật trạng thái đơn hàng #{id} thành: {newStatus}";
            
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

[HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _context.Orders
                                      .Include(o => o.OrderDetails) // Kéo theo cả chi tiết để xóa cho sạch
                                      .FirstOrDefaultAsync(o => o.Id == id);

            if (order != null)
            {
                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = $"Đã xóa thành công đơn hàng #{id}!";
            }

            return RedirectToAction(nameof(Index));
        }
        // =========================================================================
        // PHẦN 2: GÓC NHÌN CỦA KHÁCH HÀNG (Lịch sử mua hàng)
        // =========================================================================
        
        // Không gắn [Authorize(Roles="Admin")] nên Khách hàng bình thường vào xem thoải mái
        public async Task<IActionResult> History()
        {
            // 1. Lấy mã thẻ căn cước (UserId) của tài khoản đang đăng nhập
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 2. Chui vào DB, CHỈ lôi ra những đơn hàng của đúng ông khách này
            var orders = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product) // Móc nối lấy Hình ảnh, Tên SP
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate) // Sắp xếp đơn mới nhất lên đầu
                .ToListAsync();

            return View(orders);
        }
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> OrderInfo(int id)
        {
            var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            
            // Tìm đơn hàng theo ID, bắt buộc phải đúng UserId đang đăng nhập để bảo mật
            var order = await _context.Orders
                                      .Include(o => o.OrderDetails)
                                      .ThenInclude(od => od.Product) // Móc luôn thông tin chai mỹ phẩm
                                      .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
            {
                return RedirectToAction("History"); // Không tìm thấy thì đá về lịch sử
            }

            return View(order);
        }
    }
}