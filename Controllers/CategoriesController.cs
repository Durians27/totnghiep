using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
// Lưu ý: Đổi chữ 'VelvySkinWeb' thành tên Project gốc của bạn nếu nó khác nhé
using VelvySkinWeb.Data; 
using VelvySkinWeb.Models;

namespace VelvySkinWeb.Controllers
{
    // [Authorize(Roles = "Admin")] // Tạm thời Comment lại. Sau khi bạn làm xong tính năng Đăng nhập Admin thì mới mở dòng này ra để khóa API nhé.
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        // BÍ QUYẾT 1: Dependency Injection (Tiêm Database vào Controller)
        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. READ: Lấy danh sách Danh mục (Gửi cho Như hiển thị)
        // ==========================================
        public async Task<IActionResult> Index()
        {
            // Lệnh C# này tương đương: SELECT * FROM Categories
            var categories = await _context.Categories.ToListAsync();
            return View(categories);
        }

        // ==========================================
        // 2. CREATE: Thêm mới (Gồm 2 hàm: Hiển thị Form & Xử lý lưu)
        // ==========================================
        
        // Hàm này chạy khi bấm vào link thêm mới (Chỉ hiện cái Form trống)
        public IActionResult Create()
        {
            return View();
        }

        // Hàm này chạy khi Admin bấm nút "Lưu" trên Form
        [HttpPost]
        [ValidateAntiForgeryToken] // BÍ QUYẾT 2: Mã hóa chống hacker giả mạo Form
        public async Task<IActionResult> Create([Bind("Id,Name,Description")] Category category)
        {
            // BÍ QUYẾT 3: Kiểm thực đầu vào (Backend validation)
            if (ModelState.IsValid)
            {
                _context.Add(category);
                await _context.SaveChangesAsync(); // Lệnh này chính thức ghi dữ liệu xuống SQL Server
                return RedirectToAction(nameof(Index)); // Lưu xong thì quay về trang danh sách
            }
            return View(category); 
        }

        // ==========================================
        // 3. UPDATE: Sửa danh mục (Gồm 2 hàm: Hiển thị Form có sẵn data & Xử lý lưu)
        // ==========================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            // Tìm danh mục có Id tương ứng trong SQL
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description")] Category category)
        {
            if (id != category.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(category);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CategoryExists(category.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // ==========================================
        // 4. DELETE: Xóa danh mục 
        // ==========================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var category = await _context.Categories.FirstOrDefaultAsync(m => m.Id == id);
            if (category == null) return NotFound();

            return View(category);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                _context.Categories.Remove(category);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // Hàm phụ trợ cho Backend
        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.Id == id);
        }
    }
}