using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VelvySkinWeb.Data;
using VelvySkinWeb.Models;

namespace VelvySkinWeb.Controllers
{
    // KHÓA BẢO MẬT: Chỉ có tài khoản mang Role "Admin" mới được vào đây
    [Authorize(Roles = "Admin")]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. DANH SÁCH TẤT CẢ ĐƠN HÀNG
        // ==========================================
        // ==========================================
        // 1. DANH SÁCH ĐƠN HÀNG (CÓ BỘ LỌC THÔNG MINH)
        // ==========================================
        public async Task<IActionResult> Index(string status = "all")
        {
            // 1. Lấy toàn bộ giỏ hàng ra làm vốn
            var orders = _context.Orders.AsQueryable();

            // 2. Bộ lọc Tab thần thánh: Khách bấm Tab nào, lọc data Tab đó
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

            // 3. Luôn xếp đơn mới nhất lên trên cùng
            var finalOrders = await orders.OrderByDescending(o => o.OrderDate).ToListAsync();

            // 4. Gửi cờ hiệu ra ngoài View để biết Tab nào đang được sáng lên
            ViewBag.CurrentStatus = status.ToLower();

            return View(finalOrders);
        }

        // ==========================================
        // 2. XEM CHI TIẾT 1 ĐƠN HÀNG KÈM SẢN PHẨM
        // ==========================================
        public async Task<IActionResult> Details(int id)
        {
            // Kỹ thuật Include: Lôi Đơn hàng -> Lôi Chi tiết đơn -> Lôi luôn thông tin Mỹ phẩm
            var order = await _context.Orders
                                      .Include(o => o.OrderDetails)
                                      .ThenInclude(od => od.Product)
                                      .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            return View(order);
        }

        // ==========================================
        // 3. THUẬT TOÁN CẬP NHẬT TRẠNG THÁI
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            // Cập nhật trạng thái mới và lưu xuống SQL Server
            order.OrderStatus = newStatus;
            _context.Update(order);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = $"Đã cập nhật trạng thái đơn hàng #{id} thành: {newStatus}";
            
            // Cập nhật xong thì quay lại đúng trang Chi tiết của đơn đó
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }
    }
}