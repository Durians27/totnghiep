using Microsoft.AspNetCore.Mvc;
using VelvySkinWeb.Models;
using VelvySkinWeb.Data; 
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace VelvySkinWeb.Controllers
{
    public class WishlistController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WishlistController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. TRANG HIỂN THỊ DANH SÁCH YÊU THÍCH
        public IActionResult Index()
        {
            var sessionWish = HttpContext.Session.GetString("Wishlist");
            var wishlistIds = sessionWish != null ? JsonSerializer.Deserialize<List<int>>(sessionWish) : new List<int>();

            var products = _context.Products.Where(p => wishlistIds.Contains(p.Id)).ToList();
            
            return View(products);
        }

        // 2. NÚT THẢ TIM
        [HttpPost]
        public IActionResult Toggle(int id)
        {
            var sessionWish = HttpContext.Session.GetString("Wishlist");
            var wishlistIds = sessionWish != null ? JsonSerializer.Deserialize<List<int>>(sessionWish) : new List<int>();

            // Chống lỗi vàng (null) cho Deserialize
            if (wishlistIds == null) wishlistIds = new List<int>();

            if (wishlistIds.Contains(id))
            {
                wishlistIds.Remove(id);
            }
            else
            {
                wishlistIds.Add(id);
            }

            HttpContext.Session.SetString("Wishlist", JsonSerializer.Serialize(wishlistIds));
            
         
            string referer = Request.Headers["Referer"].ToString();
            return Redirect(string.IsNullOrEmpty(referer) ? "/" : referer);
        }

        // 3. XÓA TẤT CẢ
        public IActionResult Clear()
        {
            HttpContext.Session.Remove("Wishlist");
            return RedirectToAction("Index");
        }
    }
}