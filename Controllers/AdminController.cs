using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VelvySkinWeb.Data;
using VelvySkinWeb.Models;
using System.Dynamic;

namespace VelvySkinWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public AdminController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Now;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            ViewBag.TotalOrders = await _context.Orders.CountAsync(o => o.OrderDate >= startOfMonth);

            ViewBag.TotalRevenue = await _context.Orders
                .Where(o => o.OrderDate >= startOfMonth && o.OrderStatus != "Cancelled" && o.OrderStatus != "Đã hủy")
                .SumAsync(o => o.TotalAmount);

            ViewBag.TotalProducts = await _context.Products.CountAsync();

            var customers = await _userManager.GetUsersInRoleAsync("Customer");
            ViewBag.TotalCustomers = customers.Count;

            var labels = new List<string>();
            var revenueData = new List<decimal>();

            for (int i = 6; i >= 0; i--)
            {
                var targetDate = today.AddDays(-i).Date;
                labels.Add(targetDate.ToString("dd/MM"));

                var dailyRev = await _context.Orders
                    .Where(o => o.OrderDate.Date == targetDate && o.OrderStatus != "Cancelled" && o.OrderStatus != "Đã hủy")
                    .SumAsync(o => o.TotalAmount);
                
                revenueData.Add(dailyRev / 1000000m); 
            }
            ViewBag.ChartLabels = labels;
            ViewBag.ChartData = revenueData;

            ViewBag.RecentOrders = await _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToListAsync();

            return View();
        }

        public async Task<IActionResult> Orders(string search, string status, DateTime? date)
        {
            var query = _context.Orders.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(o => o.Id.ToString() == search || 
                                         o.CustomerName.Contains(search) || 
                                         o.PhoneNumber.Contains(search));
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.OrderStatus == status);
            }

            if (date.HasValue)
            {
                query = query.Where(o => o.OrderDate.Date == date.Value.Date);
            }

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentDate = date?.ToString("yyyy-MM-dd");

            var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();
            return View(orders);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Products(string search, int? categoryId)
        {
            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentCategory = categoryId;

            var query = _context.Products.Include(p => p.Category).AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.Name.Contains(search) || p.Id.ToString() == search);
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            var products = await query.OrderByDescending(p => p.Id).ToListAsync();
            return View(products);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Categories()
        {
            var categories = await _context.Categories.ToListAsync();
            return View(categories);
        }

        [HttpGet("Admin/Categories/Create")]
        [Authorize(Roles = "Admin")]
        public IActionResult CreateCategory()
        {
            return View();
        }

        [HttpPost("Admin/Categories/Create")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(Category category)
        {
            if (ModelState.IsValid)
            {
                bool isExist = await _context.Categories.AnyAsync(c => c.Name == category.Name);
                if (isExist)
                {
                    ModelState.AddModelError("Name", "Tên danh mục này đã tồn tại trong hệ thống!");
                    return View(category);
                }

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                TempData["SuccessMsg"] = $"Tuyệt vời! Đã thêm danh mục '{category.Name}' thành công.";
                return RedirectToAction("Categories");
            }
            return View(category);
        }

        [HttpGet("Admin/Categories/Edit/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditCategory(int? id)
        {
            if (id == null) return NotFound();
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();
            return View(category);
        }

        [HttpPost("Admin/Categories/Edit/{id}")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(int id, Category category)
        {
            if (id != category.Id) return NotFound();

            if (ModelState.IsValid)
            {
                bool isExist = await _context.Categories.AnyAsync(c => c.Name == category.Name && c.Id != category.Id);
                if (isExist)
                {
                    ModelState.AddModelError("Name", "Tên danh mục này đã tồn tại trong hệ thống!");
                    return View(category);
                }

                _context.Update(category);
                await _context.SaveChangesAsync();

                TempData["SuccessMsg"] = $"Đã cập nhật danh mục '{category.Name}' thành công!";
                return RedirectToAction(nameof(Categories));
            }
            return View(category);
        }

        [HttpPost("Admin/Categories/Delete/{id}")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            bool hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);
            if (hasProducts)
            {
                TempData["ErrorMsg"] = $"KHÔNG THỂ XÓA: Danh mục '{category.Name}' đang chứa sản phẩm. Vui lòng chuyển các sản phẩm đó sang danh mục khác trước khi xóa!";
                return RedirectToAction(nameof(Categories));
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = $"Đã xóa vĩnh viễn danh mục '{category.Name}' khỏi hệ thống SQL!";
            return RedirectToAction(nameof(Categories));
        }

        [HttpGet("Admin/Settings")]
        [Authorize(Roles = "Admin")]
        public IActionResult Settings()
        {
            return View();
        }

        [HttpPost("Admin/Settings/Update")]
        [Authorize(Roles = "Admin")]
        public IActionResult UpdateSettings(bool isMaintenance, bool isAutoReply)
        {
            GlobalSettings.IsMaintenanceMode = isMaintenance;
            GlobalSettings.AutoReplyComplaint = isAutoReply;
            
            TempData["SuccessMsg"] = "Đã lưu cài đặt hệ thống thành công!";
            return RedirectToAction("Settings");
        }

        [HttpGet("Admin/Support")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Support(string type = "all")
        {
            var tickets = _context.SupportTickets.AsQueryable();

            if (type == "tuvan") tickets = tickets.Where(t => t.IssueType.Contains("Tư vấn"));
            else if (type == "giaohang") tickets = tickets.Where(t => t.IssueType.Contains("đơn hàng") || t.IssueType.Contains("Giao"));
            else if (type == "khieunai") tickets = tickets.Where(t => t.IssueType.Contains("Khiếu nại"));
            else if (type == "khangcao") tickets = tickets.Where(t => t.IssueType.Contains("Kháng cáo"));

            ViewBag.CurrentType = type;

            var list = await tickets.OrderByDescending(t => t.CreatedAt).ToListAsync();
            
            ViewBag.PendingCount = await _context.SupportTickets.CountAsync(t => t.Status == "Pending" || t.Status == "Chờ xử lý");
            ViewBag.ProcessingCount = await _context.SupportTickets.CountAsync(t => t.Status == "Replied" || t.Status == "Đang giải quyết");
            ViewBag.ResolvedCount = await _context.SupportTickets.CountAsync(t => t.Status == "Đã hoàn thành");
            
            return View(list);
        }

        [HttpGet("Admin/HelpDesk")]
        [Authorize(Roles = "Admin")]
        public IActionResult HelpDesk()
        {
            return View();
        }

        [HttpGet("Admin/TicketDetail/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> TicketDetail(int id)
        {
            var ticket = await _context.SupportTickets
                .Include(t => t.TicketMessages) 
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();

            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == ticket.UserId);
            ViewBag.UserProfile = userProfile;

            var suggestedProducts = await _context.Products.Take(3).ToListAsync();
            ViewBag.SuggestedProducts = suggestedProducts;

            if (ticket.TicketMessages == null || !ticket.TicketMessages.Any())
            {
                var firstMessage = new TicketMessage { TicketId = ticket.Id, Sender = "User", Content = ticket.Content ?? ticket.Message, CreatedAt = ticket.CreatedAt };
                _context.TicketMessages.Add(firstMessage);
                await _context.SaveChangesAsync();
                ticket = await _context.SupportTickets.Include(t => t.TicketMessages).FirstOrDefaultAsync(t => t.Id == id);
            }

            string loaiVanDe = (ticket.IssueType ?? "").ToLower();

            if (loaiVanDe.Contains("khiếu nại") || loaiVanDe.Contains("thiếu") || loaiVanDe.Contains("lỗi") || loaiVanDe.Contains("hỏng") || loaiVanDe.Contains("vỡ"))
            {
                return View("TicketDetail", ticket);
            }
            else if (loaiVanDe.Contains("tư vấn") || loaiVanDe.Contains("skincare") || loaiVanDe.Contains("chăm sóc da"))
            {
                return View("ConsultationChat", ticket); 
            }
            else if (loaiVanDe.Contains("giao") || loaiVanDe.Contains("vận chuyển") || loaiVanDe.Contains("đơn hàng"))
            {
                return View("OrderChat", ticket);
            }
            
            return View("TicketDetail", ticket);
        }

        [HttpPost("Admin/ReplyTicket")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReplyTicket(int id, string replyContent)
        {
            var ticket = await _context.SupportTickets.FindAsync(id);
            if (ticket == null || string.IsNullOrWhiteSpace(replyContent)) return NotFound();

            var newMsg = new TicketMessage
            {
                TicketId = id,
                Sender = "Admin",
                Content = replyContent,
                CreatedAt = DateTime.Now
            };
            _context.TicketMessages.Add(newMsg);

            ticket.Status = "Đang giải quyết";
            
            string loaiVanDe = (ticket.IssueType ?? "").ToLower();
            string notiTitle = "Admin đã phản hồi tin nhắn";
            string notiIcon = "fa-headset";

            if (loaiVanDe.Contains("khiếu nại") || loaiVanDe.Contains("lỗi") || loaiVanDe.Contains("hỏng"))
            {
                notiTitle = "Phản hồi Khiếu nại";
                notiIcon = "fa-triangle-exclamation";
            }
            else if (loaiVanDe.Contains("tư vấn") || loaiVanDe.Contains("skincare") || loaiVanDe.Contains("chăm sóc da"))
            {
                notiTitle = "Chuyên gia Velvy đã trả lời";
                notiIcon = "fa-sparkles";
            }
            else if (loaiVanDe.Contains("giao") || loaiVanDe.Contains("vận chuyển") || loaiVanDe.Contains("đơn hàng"))
            {
                notiTitle = "Hỗ trợ Đơn hàng / Giao hàng";
                notiIcon = "fa-box-open";
            }

            var chatNoti = new VelvySkinWeb.Models.Notification
            {
                UserId = ticket.UserId,
                Title = notiTitle,
                Message = replyContent.Length > 50 ? replyContent.Substring(0, 50) + "..." : replyContent,
                Type = "support",
                Icon = notiIcon,
                IsRead = false,
                CreatedAt = DateTime.Now,
                TargetUrl = $"/Account/ChatDetail/{ticket.Id}" 
            };
            _context.Notifications.Add(chatNoti);

            await _context.SaveChangesAsync();

            string subject = $"[Velvy Skin] Có tin nhắn mới từ Admin - Yêu cầu: {ticket.IssueType}";
            string body = $@"
                <div style='font-family: Arial; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                    <h2 style='color: #2ec4b6;'>Chào {ticket.UserFullName},</h2>
                    <p>Đội ngũ hỗ trợ vừa phản hồi yêu cầu của bạn.</p>
                    <p><strong>Nội dung:</strong> <i>{replyContent}</i></p>
                    <p><a href='https://localhost:7079/Account/ChatDetail/{ticket.Id}'>Nhấn vào đây để xem chi tiết</a></p>
                </div>";

            try { await VelvySkinWeb.Helpers.EmailHelper.SendEmailAsync(ticket.UserEmail, subject, body); } catch {}

            return RedirectToAction("TicketDetail", new { id = id });
        }

        [HttpPost("Admin/QuickAction")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> QuickAction(int id, string actionType)
        {
            var ticket = await _context.SupportTickets.FindAsync(id);
            if (ticket == null) return NotFound();

            string systemMsgContent = "";

            if (actionType == "Coupon")
            {
                string newCode = "SORRY15_" + new Random().Next(1000, 9999);
                systemMsgContent = $"🎁 Hệ thống gửi tặng bạn mã giảm giá 15% để đền bù trải nghiệm không tốt: <strong>{newCode}</strong>. Áp dụng cho đơn hàng tiếp theo.";
            }
            else if (actionType == "ReturnOrder")
            {
                systemMsgContent = $"📦 Admin đã kích hoạt lệnh Đổi Trả cho đơn hàng của bạn. Shipper sẽ đến lấy hàng trong vòng 24h tới.";
            }

            var sysMsg = new TicketMessage { TicketId = id, Sender = "System", Content = systemMsgContent, CreatedAt = DateTime.Now };
            _context.TicketMessages.Add(sysMsg);
            await _context.SaveChangesAsync();

            return RedirectToAction("TicketDetail", new { id = id });
        }

        [HttpPost("Admin/ResolveTicket")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ResolveTicket(int id)
        {
            var ticket = await _context.SupportTickets.FindAsync(id);
            if (ticket != null)
            {
                ticket.Status = "Đã hoàn thành";
                await _context.SaveChangesAsync();
                TempData["SuccessMsg"] = "Đã đóng vé và kết thúc hỗ trợ thành công!";
            }
            return RedirectToAction("TicketDetail", new { id = id });
        }

        [HttpGet("Admin/Customers")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Customers(string search)
        {
            var customerUsers = await _userManager.GetUsersInRoleAsync("Customer");
            var customerIds = customerUsers.Select(u => u.Id).ToList();

            var profiles = await _context.UserProfiles.Where(p => customerIds.Contains(p.UserId)).ToListAsync();
            var allOrders = await _context.Orders
                .Where(o => customerIds.Contains(o.UserId) && o.OrderStatus != "Cancelled" && o.OrderStatus != "Đã hủy")
                .ToListAsync();

            var customerList = new List<dynamic>();
            int totalVIPs = 0;
            int newThisMonth = 0;

            foreach (var user in customerUsers)
            {
                var profile = profiles.FirstOrDefault(p => p.UserId == user.Id);
                var userOrders = allOrders.Where(o => o.UserId == user.Id).ToList();

                decimal totalSpent = userOrders.Sum(o => o.TotalAmount);
                int orderCount = userOrders.Count;
                
                var lastOrder = userOrders.OrderByDescending(o => o.OrderDate).FirstOrDefault();
                string lastActive = lastOrder != null ? lastOrder.OrderDate.ToString("dd/MM/yyyy") : "Chưa mua hàng";

                string rank = "Mới";
                string badgeClass = "rank-new";
                
                if (totalSpent >= 50000000) { rank = "Kim Cương"; badgeClass = "rank-vip"; totalVIPs++; }
                else if (totalSpent >= 20000000) { rank = "Bạch Kim"; badgeClass = "rank-vip"; totalVIPs++; }
                else if (totalSpent >= 10000000) { rank = "Vàng"; badgeClass = "rank-vip"; totalVIPs++; }
                else if (totalSpent >= 5000000) { rank = "Bạc"; badgeClass = "rank-member"; }
                else if (totalSpent >= 1000000) { rank = "Member"; badgeClass = "rank-member"; }

                if (lastOrder != null && lastOrder.OrderDate.Month == DateTime.Now.Month && lastOrder.OrderDate.Year == DateTime.Now.Year && orderCount == 1)
                {
                    newThisMonth++;
                }

                bool isMatch = true;
                if (!string.IsNullOrEmpty(search))
                {
                    string keyword = search.ToLower();
                    isMatch = (profile?.FullName?.ToLower().Contains(keyword) ?? false) || 
                              (user.Email?.ToLower().Contains(keyword) ?? false) ||
                              (profile?.PhoneNumber?.Contains(keyword) ?? false);
                }

                if (isMatch)
                {
                    customerList.Add(new {
                        UserId = user.Id,
                        FullName = profile?.FullName ?? "Khách hàng",
                        Email = user.Email,
                        Phone = profile?.PhoneNumber ?? "Chưa có",
                        TotalSpent = totalSpent,
                        OrderCount = orderCount,
                        LastActive = lastActive,
                        Rank = rank,
                        BadgeClass = badgeClass
                    });
                }
            }

            ViewBag.TotalCustomers = customerUsers.Count;
            ViewBag.TotalVIPs = totalVIPs;
            ViewBag.NewThisMonth = newThisMonth;
            ViewBag.SearchTerm = search;

            var sortedList = customerList.OrderByDescending(c => c.TotalSpent).ToList();
            return View(sortedList);
        }

        [HttpGet("Admin/CustomerDetail/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CustomerDetail(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var userAccount = await _userManager.FindByIdAsync(id);
            if (userAccount == null) return NotFound();

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == id);
            if (profile == null) profile = new UserProfile { UserId = id, FullName = "Khách hàng Velvy" };

            var userOrders = await _context.Orders
                .Where(o => o.UserId == id && o.OrderStatus != "Cancelled" && o.OrderStatus != "Đã hủy")
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            decimal totalSpent = userOrders.Sum(o => o.TotalAmount);
            
            string rank = "Mới";
            if (totalSpent >= 50000000) rank = "Kim Cương";
            else if (totalSpent >= 20000000) rank = "Bạch Kim";
            else if (totalSpent >= 10000000) rank = "Vàng";
            else if (totalSpent >= 5000000) rank = "Bạc";
            else if (totalSpent >= 1000000) rank = "Member";

            var activeTickets = await _context.SupportTickets
                .Where(t => t.UserId == id && t.Status != "Đã hoàn thành" && t.Status != "Closed")
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            ViewBag.UserAccount = userAccount;
            ViewBag.Rank = rank;
            ViewBag.TotalSpent = totalSpent;
            ViewBag.OrderCount = userOrders.Count;
            ViewBag.RecentOrders = userOrders.Take(5).ToList();
            ViewBag.ActiveTickets = activeTickets;

            return View(profile);
        }

        [HttpGet("Admin/EditCustomer/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditCustomer(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var userAccount = await _userManager.FindByIdAsync(id);
            if (userAccount == null) return NotFound();

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == id);
            if (profile == null) profile = new UserProfile { UserId = id };

            ViewBag.UserEmail = userAccount.Email;
            return View(profile);
        }

        [HttpPost("Admin/EditCustomer/{id}")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCustomer(string id, UserProfile model)
        {
            if (id != model.UserId) return NotFound();

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == id);
            if (profile != null)
            {
                profile.FullName = model.FullName;
                profile.PhoneNumber = model.PhoneNumber;
                profile.SkinType = model.SkinType;
                profile.DefaultAddress = model.DefaultAddress;

                _context.Update(profile);
                await _context.SaveChangesAsync();

                TempData["SuccessMsg"] = $"Đã cập nhật hồ sơ của khách hàng {profile.FullName} thành công!";
                return RedirectToAction("CustomerDetail", new { id = id });
            }

            return View(model);
        }

        [HttpPost("Admin/DeleteCustomer/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCustomer(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == id);
                if (profile != null) _context.UserProfiles.Remove(profile);
                
                await _userManager.DeleteAsync(user);
                
                TempData["SuccessMsg"] = "Đã xóa khách hàng khỏi hệ thống thành công!";
            }
            return RedirectToAction("Customers");
        }

        [HttpGet("Admin/CreateCustomer")]
        [Authorize(Roles = "Admin")]
        public IActionResult CreateCustomer()
        {
            return View(new UserProfile());
        }

        [HttpPost("Admin/CreateCustomer")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCustomer(UserProfile model, string Email)
        {
            if (string.IsNullOrWhiteSpace(model.FullName) || string.IsNullOrWhiteSpace(model.PhoneNumber))
            {
                TempData["ErrorMsg"] = "Vui lòng nhập đầy đủ Họ Tên và Số điện thoại!";
                return View(model);
            }

            string userEmail = string.IsNullOrWhiteSpace(Email) ? $"{model.PhoneNumber}@velvy.local" : Email;
            string defaultPassword = "Velvy@123";

            var existingUser = await _userManager.FindByEmailAsync(userEmail);
            if (existingUser != null)
            {
                TempData["ErrorMsg"] = "Khách hàng với Số điện thoại hoặc Email này đã tồn tại trong hệ thống!";
                return View(model);
            }

            var newUser = new IdentityUser { UserName = userEmail, Email = userEmail, PhoneNumber = model.PhoneNumber };
            var result = await _userManager.CreateAsync(newUser, defaultPassword);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(newUser, "Customer");

                model.UserId = newUser.Id;
                _context.UserProfiles.Add(model);
                await _context.SaveChangesAsync();

                TempData["SuccessMsg"] = $"Thêm khách hàng {model.FullName} thành công! Mật khẩu mặc định là: {defaultPassword}";
                return RedirectToAction("Customers");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            TempData["ErrorMsg"] = "Có lỗi xảy ra khi tạo tài khoản, vui lòng thử lại!";
            return View(model);
        }

        [HttpGet("Admin/Profile")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminProfile()
        {
            var userId = _userManager.GetUserId(User);
            var userAcc = await _userManager.FindByIdAsync(userId);
            
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            
            if (profile == null) 
            {
                profile = new UserProfile { UserId = userId, FullName = "Admin Velvy" };
                _context.UserProfiles.Add(profile);
                await _context.SaveChangesAsync();
            }

            ViewBag.UserEmail = userAcc.Email;
            
            var roles = await _userManager.GetRolesAsync(userAcc);
            ViewBag.Role = roles.FirstOrDefault() ?? "Quản trị viên";

            return View(profile);
        }

        [HttpPost("Admin/Profile")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminProfile(UserProfile model, IFormFile avatarFile, [FromServices] IWebHostEnvironment env)
        {
            var userId = _userManager.GetUserId(User);
            
            if (avatarFile != null && avatarFile.Length > 0)
            {
                string uploadsFolder = Path.Combine(env.WebRootPath, "images", "avatars");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                
                string filePath = Path.Combine(uploadsFolder, userId + ".jpg");
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await avatarFile.CopyToAsync(fileStream);
                }
            }

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile != null)
            {
                profile.FullName = model.FullName ?? "Admin Velvy";
                profile.PhoneNumber = model.PhoneNumber ?? "";
                
                _context.Update(profile);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMsg"] = "Cập nhật Hồ sơ Quản trị viên thành công!";
            }
            
            return RedirectToAction("AdminProfile");
        }

        [HttpGet]
        [Authorize]
        public IActionResult Notifications()
        {
            var notifications = new List<dynamic>
            {
                new { 
                    Id = 1, Title = "Đơn hàng đang trên đường giao!", 
                    Message = "Đơn hàng #VS-260330-1 của bạn đã được bàn giao cho đơn vị vận chuyển và dự kiến sẽ đến trong 2-3 ngày tới.", 
                    Type = "order", Icon = "fa-box-open", Time = "2 giờ trước", IsRead = false 
                },
                new { 
                    Id = 2, Title = "Admin đã phản hồi khiếu nại", 
                    Message = "Đội ngũ CSKH đã trả lời yêu cầu hỗ trợ đổi trả của bạn. Bấm vào để xem chi tiết.", 
                    Type = "support", Icon = "fa-headset", Time = "5 giờ trước", IsRead = false 
                },
                new { 
                    Id = 3, Title = "Ưu đãi độc quyền cho bạn", 
                    Message = "Tặng mã giảm giá 15% cho dòng sản phẩm chăm sóc tóc mới nhất. Kiểm tra ngay trong ví voucher của bạn!", 
                    Type = "promo", Icon = "fa-ticket", Time = "Hôm qua", IsRead = true 
                },
                new { 
                    Id = 4, Title = "Bảo trì hệ thống", 
                    Message = "Tính năng tư vấn sẽ được bảo trì từ 00:00 đến 02:00 ngày mai để cập nhật thuật toán mới.", 
                    Type = "system", Icon = "fa-shield-heart", Time = "2 ngày trước", IsRead = true 
                }
            };

            return View(notifications);
        }

        [HttpGet("Admin/SendNotification")]
        [Authorize(Roles = "Admin")]
        public IActionResult SendNotification()
        {
            return View();
        }

        [HttpPost("Admin/SendNotification")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SendNotification(string notiTitle, string notiMessage, string notiType)
        {
            if (string.IsNullOrWhiteSpace(notiTitle) || string.IsNullOrWhiteSpace(notiMessage))
            {
                TempData["ErrorMsg"] = "Vui lòng nhập đầy đủ Tiêu đề và Nội dung thông báo!";
                return View();
            }

            string icon = "fa-bell";
            string targetLink = "";

            if (notiType == "promo") 
            {
                icon = "fa-ticket";
                targetLink = "/Home/Vouchers";
            }
            else if (notiType == "system") 
            {
                icon = "fa-screwdriver-wrench";
                targetLink = "";
            }
            else if (notiType == "news") 
            {
                icon = "fa-bullhorn";
                targetLink = "/Home/Index";
            }

            var customers = await _userManager.GetUsersInRoleAsync("Customer");
            if (customers.Count == 0)
            {
                TempData["ErrorMsg"] = "Hệ thống chưa có khách hàng nào để gửi!";
                return View();
            }

            var notifications = new List<VelvySkinWeb.Models.Notification>();

            foreach (var user in customers)
            {
                notifications.Add(new VelvySkinWeb.Models.Notification
                {
                    UserId = user.Id,
                    Title = notiTitle,
                    Message = notiMessage,
                    Type = notiType,
                    Icon = icon,
                    TargetUrl = targetLink,
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });
            }

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = $"Đã bắn thông báo thành công tới {customers.Count} khách hàng!";
            return RedirectToAction("SendNotification");
        }

        // ==========================================
        // 🔥 ĐÃ XÓA SỔ HOÀN TOÀN LỖI LAG WEB (MEMORY LEAK) 🔥
        // ==========================================
        [HttpGet("Admin/Reports")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reports()
        {
            var today = DateTime.Now;
            var startOfThisMonth = new DateTime(today.Year, today.Month, 1);
            var startOfLastMonth = startOfThisMonth.AddMonths(-1);
            var sixMonthsAgo = startOfThisMonth.AddMonths(-5);

            // 🔥 Danh sách cứng (Dùng để ép SQL chạy lệnh IN và NOT IN, tuyệt đối cấm dùng LIKE %)
            var validStatuses = new List<string> { "Hoàn thành", "Đã thanh toán", "Đã giao" };
            var cancelStatuses = new List<string> { "Đã hủy", "Hủy", "Cancelled", "Canceled" };

            // ==========================================
            // 1. TÍNH DOANH THU & SỐ ĐƠN (Chỉ kéo đúng số liệu của 2 tháng)
            // ==========================================
            var recentOrders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.OrderDate >= startOfLastMonth && validStatuses.Contains(o.OrderStatus))
                .Select(o => new { o.OrderDate, o.TotalAmount })
                .ToListAsync();

            var thisMonthOrders = recentOrders.Where(o => o.OrderDate >= startOfThisMonth).ToList();
            var lastMonthOrders = recentOrders.Where(o => o.OrderDate < startOfThisMonth).ToList();

            decimal thisMonthRev = thisMonthOrders.Sum(o => o.TotalAmount);
            decimal lastMonthRev = lastMonthOrders.Sum(o => o.TotalAmount);
            int thisMonthOrdersCount = thisMonthOrders.Count;
            int lastMonthOrdersCount = lastMonthOrders.Count;

            double revTrend = lastMonthRev == 0m ? 100 : (double)((thisMonthRev - lastMonthRev) / lastMonthRev) * 100;
            double orderTrend = lastMonthOrdersCount == 0 ? 100 : (double)((thisMonthOrdersCount - lastMonthOrdersCount) / (double)lastMonthOrdersCount) * 100;

            ViewBag.ThisMonthRev = thisMonthRev;
            ViewBag.RevTrend = Math.Round(revTrend, 1);
            ViewBag.ThisMonthOrders = thisMonthOrdersCount;
            ViewBag.OrderTrend = Math.Round(orderTrend, 1);

            // ==========================================
            // 2. ĐẾM TỔNG KHÁCH HÀNG
            // ==========================================
            var customerRoleId = await _context.Roles.AsNoTracking().Where(r => r.Name == "Customer").Select(r => r.Id).FirstOrDefaultAsync();
            ViewBag.TotalCustomers = customerRoleId == null ? 0 : await _context.UserRoles.CountAsync(ur => ur.RoleId == customerRoleId);

            // ==========================================
            // 3. BIỂU ĐỒ 6 THÁNG (Ép SQL tự Group By)
            // ==========================================
            var monthlyStats = await _context.Orders
                .AsNoTracking()
                .Where(o => o.OrderDate >= sixMonthsAgo && validStatuses.Contains(o.OrderStatus))
                .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, TotalAmount = g.Sum(o => o.TotalAmount) })
                .ToListAsync();

            var labels = new List<string>();
            var revenueData = new List<decimal>();

            for (int i = 5; i >= 0; i--)
            {
                var targetMonth = startOfThisMonth.AddMonths(-i);
                labels.Add($"Tháng {targetMonth.Month}");
                var monthData = monthlyStats.FirstOrDefault(m => m.Year == targetMonth.Year && m.Month == targetMonth.Month);
                revenueData.Add((monthData?.TotalAmount ?? 0m) / 1000000m);
            }
            ViewBag.ChartLabels = labels;
            ViewBag.ChartData = revenueData;

            // ==========================================
            // 4. TOP SẢN PHẨM BÁN CHẠY (Join trực tiếp dưới SQL Server)
            // ==========================================
            var topProductStats = await _context.OrderDetails
                .AsNoTracking()
                .Join(_context.Orders, od => od.OrderId, o => o.Id, (od, o) => new { od, o })
                .Where(x => validStatuses.Contains(x.o.OrderStatus))
                .GroupBy(x => x.od.ProductId)
                .Select(g => new {
                    ProductId = g.Key,
                    TotalSold = g.Sum(x => x.od.Quantity),
                    TotalRevenue = g.Sum(x => x.od.Quantity * x.od.UnitPrice)
                })
                .OrderByDescending(x => x.TotalSold)
                .Take(5)
                .ToListAsync();

            var finalTopProducts = new List<dynamic>();
            if (topProductStats.Any())
            {
                var topIds = topProductStats.Select(t => t.ProductId).ToList();
                var productsList = await _context.Products.AsNoTracking().Where(p => topIds.Contains(p.Id)).ToListAsync();

                foreach (var stat in topProductStats)
                {
                    var product = productsList.FirstOrDefault(p => p.Id == stat.ProductId);
                    if (product != null)
                    {
                        dynamic dynItem = new System.Dynamic.ExpandoObject();
                        dynItem.Product = product;
                        dynItem.TotalSold = stat.TotalSold;
                        dynItem.TotalRevenue = stat.TotalRevenue;
                        finalTopProducts.Add(dynItem);
                    }
                }
            }
            ViewBag.TopProducts = finalTopProducts;

            // ==========================================
            // 5. TỶ LỆ THÀNH CÔNG VÀ ĐƠN PENDING (Dùng CountAsync siêu nhẹ)
            // ==========================================
            int totalEver = await _context.Orders.CountAsync();
            int completedOrders = await _context.Orders.CountAsync(o => o.OrderStatus != null && validStatuses.Contains(o.OrderStatus));
            
            // Ép SQL dùng NOT IN, không được kéo String về RAM
            int pendingOrders = await _context.Orders.CountAsync(o =>
                o.OrderStatus != null &&
                !validStatuses.Contains(o.OrderStatus) &&
                !cancelStatuses.Contains(o.OrderStatus));

            double successRate = totalEver == 0 ? 0 : (double)completedOrders / totalEver * 100;
            ViewBag.SuccessRate = Math.Round(successRate, 1);
            ViewBag.PendingOrders = pendingOrders;

            return View();
        }
        [HttpPost("Admin/ChangeAdminPassword")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ChangeAdminPassword(string currentPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            {
                TempData["ErrorMsg"] = "Vui lòng nhập đầy đủ mật khẩu hiện tại và mật khẩu mới!";
                return RedirectToAction("Settings"); 
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["ErrorMsg"] = "Lỗi hệ thống: Không tìm thấy tài khoản!";
                return RedirectToAction("Settings");
            }

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

            if (result.Succeeded)
            {
                TempData["SuccessMsg"] = "Đổi mật khẩu thành công! Hãy ghi nhớ mật khẩu mới nhé.";
            }
            else
            {
                TempData["ErrorMsg"] = "Mật khẩu hiện tại không đúng hoặc mật khẩu mới quá ngắn!";
            }

            return RedirectToAction("Settings");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> LockAccount(string userId, int lockDays, string lockReason)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            DateTimeOffset lockoutEnd = lockDays == 9999 ? DateTimeOffset.MaxValue : DateTimeOffset.UtcNow.AddDays(lockDays);
            
            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, lockoutEnd);

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile != null) {
                profile.LockReason = lockReason;
                _context.Update(profile);
            }

            string durationText = lockDays == 9999 ? "Vĩnh viễn" : $"{lockDays} ngày";
            var log = new AuditLog {
                Username = User.Identity.Name,
                ActionType = "UPDATE",
                TableName = "AspNetUsers",
                Description = $"Admin đã khóa tài khoản {user.Email} ({durationText}). Lý do: {lockReason}"
            };
            _context.AuditLogs.Add(log);
            
            await _context.SaveChangesAsync();

            TempData["SuccessMsg"] = $"Đã ban tài khoản {user.Email} thành công!";
            return RedirectToAction("CustomerDetail", new { id = userId });
        }
        
        [HttpGet]
        public async Task<IActionResult> SystemLogs()
        {
            var logs = await _context.AuditLogs
                .OrderByDescending(x => x.Timestamp)
                .Take(200) 
                .ToListAsync();
            return View(logs);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAdminNotifications()
        {
            var pendingTickets = await _context.SupportTickets
                .Where(t => t.Status == "Pending" || t.Status == "Chờ xử lý")
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            int count = pendingTickets.Count;
            
            var latestList = pendingTickets.Take(5).Select(t => new {
                id = t.Id,
                type = t.IssueType ?? "Yêu cầu hỗ trợ",
                customer = string.IsNullOrEmpty(t.UserFullName) ? "Khách hàng" : t.UserFullName,
                time = t.CreatedAt.ToString("HH:mm - dd/MM"),
                icon = t.IssueType == "Kháng cáo" ? "fa-user-lock text-danger" :
                       t.IssueType == "Tư vấn làm đẹp" ? "fa-spa text-info" :
                       t.IssueType == "Hỗ trợ đơn hàng" ? "fa-truck-fast text-warning" : "fa-circle-exclamation text-primary"
            }).ToList();

            return Json(new { success = true, count = count, data = latestList });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UnlockAccountFromTicket(int ticketId, string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("Lỗi: Không tìm thấy tội phạm!");

            await _userManager.SetLockoutEndDateAsync(user, null);

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile != null) {
                profile.LockReason = "";
                _context.Update(profile);
            }

            var ticket = await _context.SupportTickets.FindAsync(ticketId);
            string userFullName = profile?.FullName ?? "Khách hàng Velvy";

            if (ticket != null) {
                ticket.Status = "Đã hoàn thành";
                _context.Update(ticket);

                var sysMsg = new TicketMessage {
                    TicketId = ticketId,
                    Sender = "System", 
                    Content = "<i class='fa-solid fa-unlock text-success'></i> <b>CHÚC MỪNG:</b> Quản trị viên đã chấp nhận đơn kháng cáo và MỞ KHÓA tài khoản. Vui lòng tuân thủ chính sách của Velvy Skin!",
                    CreatedAt = DateTime.Now
                };
                _context.TicketMessages.Add(sysMsg);
            }

            var log = new AuditLog {
                Username = User.Identity?.Name ?? "Admin",
                ActionType = "UPDATE",
                TableName = "AspNetUsers",
                Description = $"Admin đã ÂN XÁ (MỞ KHÓA) tài khoản {user.Email} thông qua đơn kháng cáo #{ticketId}"
            };
            _context.AuditLogs.Add(log);

            await _context.SaveChangesAsync();

            try
            {
                string loginLink = Url.Action("Login", "Account", null, Request.Scheme);
                string subject = "🎉 Chúc mừng! Tài khoản Velvy Skin của bạn đã được mở khóa";
                string body = $@"
                    <div style='font-family: Arial, sans-serif; padding: 30px; border: 1px solid #e0e0e0; border-radius: 15px; max-width: 600px; margin: 0 auto; text-align: center; box-shadow: 0 4px 15px rgba(0,0,0,0.05);'>
                        <h2 style='color: #2ec4b6; font-size: 28px; margin-bottom: 5px;'>Velvy Skin</h2>
                        <h3 style='color: #333; margin-top: 0;'>Xin chào {userFullName},</h3>
                        
                        <div style='background: #f0fdfa; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                            <p style='font-size: 16px; color: #555; line-height: 1.6; margin: 0;'>
                                Tuyệt vời! Yêu cầu kháng cáo của bạn đã được Quản trị viên xem xét và chấp nhận. <br><br>
                                <b style='color: #208b81; font-size: 18px;'>Tài khoản của bạn đã chính thức được MỞ KHÓA! 🔓</b>
                            </p>
                        </div>
                        <p style='font-size: 14px; color: #666; margin: 20px 0; line-height: 1.5;'>
                            Lần sau có bận đi đẻ nhớ nhờ người nhà nhận hàng giúp shop nhé! Cảm ơn bạn đã tiếp tục đồng hành và tin tưởng Velvy Skin.
                        </p>
                        <a href='{loginLink}' style='display: inline-block; background: #2ec4b6; color: white; padding: 14px 30px; text-decoration: none; border-radius: 10px; font-weight: bold; font-size: 16px; margin-top: 10px; transition: background 0.3s;'>ĐĂNG NHẬP NGAY</a>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0 20px;'>
                        <p style='color: #aaa; font-size: 12px;'>Đây là email thông báo tự động từ hệ thống Velvy Skin. Vui lòng không trả lời email này.</p>
                    </div>";

                await VelvySkinWeb.Helpers.EmailHelper.SendEmailAsync(user.Email, subject, body);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi gửi mail mở khóa: " + ex.Message);
            }

            TempData["SuccessMsg"] = $"Bao Công xuất chiêu! Đã ân xá thành công và gửi Email cho {user.Email}!";
            return RedirectToAction("TicketDetail", new { id = ticketId }); 
        }
    }
}