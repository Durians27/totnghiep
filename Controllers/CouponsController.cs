using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using VelvySkinWeb.Data;
using VelvySkinWeb.Models;

namespace VelvySkinWeb.Controllers
{
    [Authorize(Roles = "Admin")] 
    public class CouponsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CouponsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("Admin/Coupons")] 
        [Authorize(Roles = "Admin")]        
        public async Task<IActionResult> Index()
        {
            var coupons = await _context.Coupons.OrderByDescending(c => c.Id).ToListAsync();
            return View(coupons);
        }

        [HttpGet("Admin/Coupons/Create")]
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost("Admin/Coupons/Create")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Coupon coupon)
        {
            if (!string.IsNullOrEmpty(coupon.Code))
            {
                coupon.Code = coupon.Code.Trim().ToUpper();
            }

            bool isCodeExist = await _context.Coupons.AnyAsync(c => c.Code == coupon.Code);
            if (isCodeExist)
            {
                ModelState.AddModelError("Code", "Mã code này đã tồn tại trong hệ thống! Vui lòng tạo mã khác.");
            }

            if (coupon.EndDate < coupon.StartDate)
            {
                ModelState.AddModelError("EndDate", "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu!");
            }

            if (ModelState.IsValid)
            {
                _context.Coupons.Add(coupon);
                await _context.SaveChangesAsync();

                TempData["SuccessMsg"] = $"Tuyệt vời! Đã kích hoạt mã {coupon.Code} thành công!";
                return RedirectToAction(nameof(Index)); 
            }

            return View(coupon);
        }
      
        [HttpGet("Admin/Coupons/Details/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int id)
        {
           
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null) return NotFound();
            return View(coupon);
        }

        [HttpPost("Admin/Coupons/Pause/{id}")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pause(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon != null)
            {
                coupon.IsActive = false; 
                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = $"Đã tạm dừng mã {coupon.Code} thành công!";
            }
            return RedirectToAction(nameof(Details), new { id = id });
        }

        
        [HttpGet("Admin/Coupons/Edit/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null) return NotFound();
            return View(coupon);
        }

        [HttpPost("Admin/Coupons/Edit/{id}")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Coupon coupon)
        {
            var existing = await _context.Coupons.FindAsync(id);
            if (existing == null) return NotFound();

            if (coupon.EndDate < coupon.StartDate)
            {
                ModelState.AddModelError("EndDate", "Ngày kết thúc phải lớn hơn ngày bắt đầu!");
                return View(coupon);
            }

            if (existing.UsedCount > 0)
            {
                existing.Name = coupon.Name;
                existing.MinOrderValue = coupon.MinOrderValue;
                existing.StartDate = coupon.StartDate;
                existing.EndDate = coupon.EndDate;
                existing.MaxUses = coupon.MaxUses;
                existing.MaxUsesPerUser = coupon.MaxUsesPerUser;
                existing.Description = coupon.Description;
            }
            else
            {
                if (existing.Code != coupon.Code && await _context.Coupons.AnyAsync(c => c.Code == coupon.Code))
                {
                    ModelState.AddModelError("Code", "Mã code đã tồn tại!");
                    return View(coupon);
                }
                existing.Code = coupon.Code?.Trim().ToUpper();
                existing.Name = coupon.Name;
                existing.DiscountType = coupon.DiscountType;
                existing.DiscountValue = coupon.DiscountValue;
                existing.MaxDiscountAmount = coupon.MaxDiscountAmount; 
                existing.MinOrderValue = coupon.MinOrderValue;
                existing.StartDate = coupon.StartDate;
                existing.EndDate = coupon.EndDate;
                existing.MaxUses = coupon.MaxUses;
                existing.MaxUsesPerUser = coupon.MaxUsesPerUser;
                existing.Description = coupon.Description;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMsg"] = "Đã cập nhật mã khuyến mãi thành công!";
            return RedirectToAction(nameof(Details), new { id = existing.Id });
        }

      
        [HttpPost("Admin/Coupons/Delete/{id}")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null) return NotFound();

            if (coupon.UsedCount > 0)
            {
                TempData["ErrorMsg"] = $"MÃ ĐANG BỊ KHÓA BẢO VỆ: Mã {coupon.Code} đã có khách hàng sử dụng ({coupon.UsedCount} lượt). Để bảo vệ dữ liệu lịch sử đơn hàng, bạn KHÔNG THỂ xóa vĩnh viễn. Vui lòng bấm vào Xem chi tiết và chọn [Tạm dừng]!";
                return RedirectToAction(nameof(Index));
            }

            _context.Coupons.Remove(coupon);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = $"Đã xóa vĩnh viễn mã {coupon.Code} khỏi hệ thống SQL!";
            return RedirectToAction(nameof(Index));
        }
    }
}