using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using VelvySkinWeb.Models.ViewModels;
using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Http;
using System;
using Microsoft.AspNetCore.Authorization; 
using System.Security.Claims; 
using Microsoft.EntityFrameworkCore;
using VelvySkinWeb.Models;
using VelvySkinWeb.Data;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace VelvySkinWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context; 
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {

                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    if (await _roleManager.RoleExistsAsync("Customer"))
                    {
                        await _userManager.AddToRoleAsync(user, "Customer");
                    }


                    _context.AuditLogs.Add(new AuditLog {
                        Username = model.Email,
                        ActionType = "REGISTER",
                        TableName = "AspNetUsers",
                        Description = $"Thành viên mới [{model.Email}] đã đăng ký tài khoản thành công.",
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Localhost",
                        OldValues = "{}", 
                        NewValues = "{}", 
                        Timestamp = DateTime.Now
                    });
                    await _context.SaveChangesAsync();

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index", "Home");
                }
                
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                
                if (user != null)
                {
                    if (await _userManager.IsLockedOutAsync(user))
                    {
                        _context.AuditLogs.Add(new AuditLog {
                            Username = model.Email,
                            ActionType = "SECURITY_ALERT",
                            TableName = "AspNetUsers",
                            Description = $"CẢNH BÁO: Tài khoản đang bị khóa [{model.Email}] cố gắng đăng nhập.",
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Localhost",
                            OldValues = "{}", 
                            NewValues = "{}", 
                            Timestamp = DateTime.Now
                        });
                        await _context.SaveChangesAsync();

                        return RedirectToAction("Lockout", "Account", new { email = model.Email });
                    }

                    if (await _userManager.CheckPasswordAsync(user, model.Password))
                    {
                        _context.AuditLogs.Add(new AuditLog {
                            Username = model.Email,
                            ActionType = "LOGIN_SUCCESS",
                            TableName = "AspNetUsers",
                            Description = $"Người dùng [{model.Email}] đã đăng nhập thành công.",
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Localhost",
                            OldValues = "{}", 
                            NewValues = "{}", 
                            Timestamp = DateTime.Now
                        });
                        await _context.SaveChangesAsync();

                        if (user.TwoFactorEnabled)
                        {
                            string otp = new Random().Next(100000, 999999).ToString();
                            HttpContext.Session.SetString("2FA_" + user.Id, otp);

                            string subject = "Mã xác minh Đăng nhập (2FA) - Velvy Skin";
                            string body = $"<div style='font-family: Arial; padding: 20px; text-align: center;'><h2>Mã đăng nhập của bạn là:</h2><h1 style='color: #2ec4b6; letter-spacing: 5px;'>{otp}</h1></div>";
                            await SendEmailAsync(user.Email, subject, body);

                            return RedirectToAction("VerifyLogin2FA", new { userId = user.Id, returnUrl = returnUrl, rememberMe = model.RememberMe });
                        }

                        await _signInManager.SignInAsync(user, model.RememberMe);
                        
                        bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                        bool isStaff = await _userManager.IsInRoleAsync(user, "Staff");

                        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) 
                        {
                            string urlLower = returnUrl.ToLower();
                            if (isStaff && !isAdmin && (urlLower == "/admin" || urlLower == "/admin/index"))
                            {
                                return RedirectToAction("Products", "Admin");
                            }
                            return LocalRedirect(returnUrl);
                        }
                        
                       if (isAdmin) return RedirectToAction("Index", "Admin");


if (isStaff) 
{
    var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
    string duty = profile?.Duty ?? "";
    string dutyLower = duty.ToLower(); 


    if (dutyLower.Contains("cskh") || dutyLower.Contains("chăm sóc") || dutyLower.Contains("cham soc") || dutyLower.Contains("hỗ trợ")) 
    {
        return RedirectToAction("Support", "Admin"); 
    }

    else if (dutyLower.Contains("kho") || dutyLower.Contains("sản phẩm") || dutyLower.Contains("san pham")) 
    {
        return RedirectToAction("Products", "Admin"); 
    }

    else if (dutyLower.Contains("vận hành") || dutyLower.Contains("van hanh") || dutyLower.Contains("điều phối") || dutyLower.Contains("đơn hàng")) 
    {
        return RedirectToAction("Orders", "Admin"); 
    }
    

    return RedirectToAction("AdminProfile", "Admin"); 
}


return RedirectToAction("Index", "Home");
                    }
                    else
                    {
                        _context.AuditLogs.Add(new AuditLog {
                            Username = model.Email,
                            ActionType = "LOGIN_FAILED",
                            TableName = "AspNetUsers",
                            Description = $"Phát hiện đăng nhập sai mật khẩu tại tài khoản [{model.Email}].",
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Localhost",
                            OldValues = "{}", 
                            NewValues = "{}", 
                            Timestamp = DateTime.Now
                        });
                        await _context.SaveChangesAsync();
                    }
                }
                
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            }
            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Security(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

            if (result.Succeeded)
            {
                _context.AuditLogs.Add(new AuditLog {
                    Username = user.Email,
                    ActionType = "PASSWORD_CHANGED",
                    TableName = "AspNetUsers",
                    Description = $"Người dùng [{user.Email}] đã cập nhật mật khẩu mới thành công.",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Localhost",
                    OldValues = "{}", 
                    NewValues = "{}", 
                    Timestamp = DateTime.Now
                });
                await _context.SaveChangesAsync();

                await _signInManager.RefreshSignInAsync(user);
                TempData["SuccessMsg"] = "Đổi mật khẩu thành công! Tài khoản của bạn đã an toàn hơn.";
                return RedirectToAction("Security");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            
            return View(model);
        }

        [HttpGet] 
        public async Task<IActionResult> Lockout(string email) 
        { 
            var user = await _userManager.FindByEmailAsync(email); 
            if (user == null) return View(); 
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id); 
            ViewBag.LockEnd = user.LockoutEnd; 
            ViewBag.Reason = profile?.LockReason ?? "Vi phạm quy định của hệ thống."; 
            ViewBag.UserEmail = email; 
            if (await _userManager.IsInRoleAsync(user, "Staff")) return View("StaffLockout"); 
            return View(); 
        }

        [HttpPost] 
        [AllowAnonymous] 
        public async Task<IActionResult> SubmitAppeal(string email, string appealReason) 
        { 
            var user = await _userManager.FindByEmailAsync(email); 
            if (user != null) 
            { 
                var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id); 
                var ticket = new SupportTicket { UserId = user.Id, UserEmail = user.Email, UserFullName = profile?.FullName ?? "Khách hàng", Subject = "Kháng cáo khóa tài khoản", Content = appealReason, IssueType = "Kháng cáo", Status = "Pending", CreatedAt = DateTime.Now }; 
                _context.SupportTickets.Add(ticket); 
                await _context.SaveChangesAsync(); 
                var firstMsg = new TicketMessage { TicketId = ticket.Id, Sender = "User", Content = appealReason, CreatedAt = DateTime.Now }; 
                _context.TicketMessages.Add(firstMsg); 
                await _context.SaveChangesAsync(); 
            } 
            TempData["AppealSuccess"] = "Đã gửi yêu cầu kháng cáo thành công! Quản trị viên sẽ xem xét và phản hồi qua Email của bạn."; 
            return RedirectToAction("Lockout", new { email = email }); 
        }

        public async Task<IActionResult> CreateAdmin() 
        { 
            string[] roleNames = { "Admin", "Staff", "Customer" }; 
            foreach (var roleName in roleNames) 
            { 
                var roleExist = await _roleManager.RoleExistsAsync(roleName); 
                if (!roleExist) await _roleManager.CreateAsync(new IdentityRole(roleName)); 
            } 
            var adminUser = await _userManager.FindByEmailAsync("admin@velvyskin.com"); 
            if (adminUser == null) 
            { 

                var user = new ApplicationUser { UserName = "admin@velvyskin.com", Email = "admin@velvyskin.com" }; 
                var createPowerUser = await _userManager.CreateAsync(user, "Admin@123"); 
                if (createPowerUser.Succeeded) 
                { 
                    await _userManager.AddToRoleAsync(user, "Admin"); 
                } 
            } 
            return Content("Đã khởi tạo xong Hệ thống Phân Quyền và Tài khoản Admin!"); 
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync(); 
            HttpContext.Session.Clear(); 
            return RedirectToAction("Index", "Home");
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body) 
        { 
            await VelvySkinWeb.Helpers.EmailHelper.SendEmailAsync(toEmail, subject, body); 
        }

        [HttpGet] public IActionResult ForgotPassword() => View();

        [HttpPost] 
        public async Task<IActionResult> ForgotPassword(string email) 
        { 
            var user = await _userManager.FindByEmailAsync(email); 
            if (user == null) 
            { 
                TempData["Message"] = "Không tìm thấy tài khoản nào với Email này!"; 
                return View(); 
            } 
            string otp = new Random().Next(100000, 999999).ToString(); 
            HttpContext.Session.SetString("OTP_" + email, otp); 
            string subject = "Mã xác nhận khôi phục mật khẩu - Velvy Skin"; 
            string body = $"<div style='font-family: Arial; padding: 20px; background: #fdfdfd; text-align: center; border-radius: 10px; border: 1px solid #eee;'><h2 style='color: #2ec4b6;'>Velvy Skin</h2><p>Chào bạn,</p><p>Bạn vừa yêu cầu khôi phục mật khẩu. Mã OTP của bạn là:</p><h1 style='color: #e993b0; letter-spacing: 5px; font-size: 36px; margin: 20px 0;'>{otp}</h1><p style='color: #888; font-size: 12px;'>Tuyệt đối không chia sẻ mã này cho bất kỳ ai.</p></div>"; 
            await SendEmailAsync(email, subject, body); 
            return RedirectToAction("VerifyOTP", new { email = email }); 
        }

        [HttpGet] public IActionResult VerifyOTP(string email) { ViewBag.Email = email; return View(); }

        [HttpPost] 
        public IActionResult VerifyOTP(string email, string otp) 
        { 
            string savedOtp = HttpContext.Session.GetString("OTP_" + email); 
            if (savedOtp != null && savedOtp == otp) return RedirectToAction("ResetPassword", new { email = email }); 
            ModelState.AddModelError(string.Empty, "Mã OTP không hợp lệ. Vui lòng kiểm tra lại."); 
            ViewBag.Email = email; 
            return View(); 
        }

        [HttpGet] 
        [Authorize] 
        public async Task<IActionResult> Profile() 
        { 
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); 
            var userEmail = User.Identity.Name; 
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId); 
            if (profile == null) 
            { 
                profile = new UserProfile { UserId = userId, FullName = "Khách hàng Velvy", PhoneNumber = "", DefaultAddress = "", SkinType = "", SkinConcern = "", Allergies = "", Gender = "", LockReason = "" }; 
                _context.UserProfiles.Add(profile); 
                await _context.SaveChangesAsync(); 
            } 
            profile.Email = userEmail; 
            ViewBag.SupportTickets = await _context.SupportTickets.Where(t => t.UserId == userId).OrderByDescending(t => t.CreatedAt).ToListAsync(); 
            ViewBag.RecentOrders = await _context.Orders.Where(o => o.UserId == userId).OrderByDescending(o => o.OrderDate).Take(10).ToListAsync(); 
            return View(profile); 
        }

        [HttpPost] 
        [Authorize] 
        [ValidateAntiForgeryToken] 
        public async Task<IActionResult> Profile(UserProfile model, IFormFile avatarFile, [FromServices] IWebHostEnvironment env) 
        { 
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); 
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
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId); 
            if (profile != null) 
            { 
                profile.FullName = model.FullName ?? ""; 
                profile.PhoneNumber = model.PhoneNumber ?? ""; 
                profile.DateOfBirth = model.DateOfBirth; 
                profile.Gender = model.Gender ?? ""; 
                profile.DefaultAddress = model.DefaultAddress ?? ""; 
                profile.SkinType = model.SkinType ?? ""; 
                profile.SkinConcern = model.SkinConcern ?? ""; 
                profile.Allergies = model.Allergies ?? ""; 
                await _context.SaveChangesAsync(); 
                TempData["SuccessMsg"] = "Tuyệt vời! Hồ sơ và ảnh đại diện đã được cập nhật."; 
            } 
            return RedirectToAction("Profile"); 
        }

        [HttpGet] public IActionResult ResetPassword(string email) { ViewBag.Email = email; return View(); }

        [HttpPost] 
        public async Task<IActionResult> ResetPassword(string email, string newPassword, string confirmPassword) 
        { 
            if (newPassword != confirmPassword) 
            { 
                ModelState.AddModelError(string.Empty, "Mật khẩu xác nhận không khớp!"); 
                ViewBag.Email = email; 
                return View(); 
            } 
            var user = await _userManager.FindByEmailAsync(email); 
            if (user != null) 
            { 
                var token = await _userManager.GeneratePasswordResetTokenAsync(user); 
                var result = await _userManager.ResetPasswordAsync(user, token, newPassword); 
                if (result.Succeeded) 
                { 
                    HttpContext.Session.Remove("OTP_" + email); 
                    TempData["Message"] = "Tuyệt vời! Thiết lập mật khẩu mới thành công. Vui lòng đăng nhập lại."; 
                    return RedirectToAction("Login"); 
                } 
                foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description); 
            } 
            ViewBag.Email = email; 
            return View(); 
        }

        [HttpGet] 
        [Authorize] 
        public async Task<IActionResult> Security() 
        { 
            var user = await _userManager.GetUserAsync(User); 
            ViewBag.Is2FAEnabled = user?.TwoFactorEnabled ?? false; 
            return View(); 
        }

        [HttpGet] 
        [Authorize] 
        public async Task<IActionResult> DeleteAccount() 
        { 
            var user = await _userManager.GetUserAsync(User); 
            if (user != null) 
            { 
                var profile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == user.Id); 
                if (profile != null) 
                { 
                    _context.UserProfiles.Remove(profile); 
                    await _context.SaveChangesAsync(); 
                } 
                await _userManager.DeleteAsync(user); 
                await _signInManager.SignOutAsync(); 
                HttpContext.Session.Remove("Cart"); 
            } 
            return RedirectToAction("Index", "Home"); 
        }

        [HttpPost] 
        [Authorize] 
        public async Task<IActionResult> Toggle2FA(bool enable) 
        { 
            var user = await _userManager.GetUserAsync(User); 
            if (user != null) 
            { 
                await _userManager.SetTwoFactorEnabledAsync(user, enable); 
                return Json(new { success = true, message = enable ? "Đã BẬT bảo vệ 2 lớp!" : "Đã TẮT bảo vệ 2 lớp!" }); 
            } 
            return Json(new { success = false, message = "Lỗi xác thực." }); 
        }

        [HttpGet] 
        public IActionResult VerifyLogin2FA(string userId, string returnUrl, bool rememberMe) 
        { 
            ViewBag.UserId = userId; 
            ViewBag.ReturnUrl = returnUrl; 
            ViewBag.RememberMe = rememberMe; 
            return View(); 
        }

        [HttpPost] 
        public async Task<IActionResult> VerifyLogin2FA(string userId, string otp, string returnUrl, bool rememberMe) 
        { 
            string savedOtp = HttpContext.Session.GetString("2FA_" + userId); 
            if (savedOtp != null && savedOtp == otp) 
            { 
                var user = await _userManager.FindByIdAsync(userId); 
                await _signInManager.SignInAsync(user, rememberMe); 
                HttpContext.Session.Remove("2FA_" + userId); 
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl); 
                if (await _userManager.IsInRoleAsync(user, "Admin")) return RedirectToAction("Index", "Admin"); 
                if (await _userManager.IsInRoleAsync(user, "Staff")) return RedirectToAction("Products", "Admin"); 
                return RedirectToAction("Index", "Home"); 
            } 
            ModelState.AddModelError(string.Empty, "Mã xác thực không hợp lệ!"); 
            ViewBag.UserId = userId; 
            ViewBag.ReturnUrl = returnUrl; 
            ViewBag.RememberMe = rememberMe; 
            return View(); 
        }

        [HttpPost] 
        [Authorize] 
        [ValidateAntiForgeryToken] 
        public async Task<IActionResult> SubmitSupportTicket(SupportTicket ticket, IFormFile attachment) 
        { 
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); 
            var userEmail = User.FindFirstValue(ClaimTypes.Email); 
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId); 
            string attachmentHtml = ""; 
            if (attachment != null && attachment.Length > 0) 
            { 
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "tickets"); 
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder); 
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + attachment.FileName; 
                string filePath = Path.Combine(uploadsFolder, uniqueFileName); 
                using (var fileStream = new FileStream(filePath, FileMode.Create)) 
                { 
                    await attachment.CopyToAsync(fileStream); 
                } 
                attachmentHtml = $"<br/><br/><div style='font-size:12px; color:#888; margin-bottom:5px;'><i class='fa-solid fa-paperclip'></i> Ảnh đính kèm:</div><a href='/images/tickets/{uniqueFileName}' target='_blank'><img src='/images/tickets/{uniqueFileName}' style='max-width: 200px; border-radius: 10px; border: 1px solid #ddd; box-shadow: 0 2px 5px rgba(0,0,0,0.1);' /></a>"; 
            } 
            ticket.UserId = userId; 
            ticket.UserEmail = userEmail; 
            ticket.UserFullName = userProfile?.FullName ?? "Khách hàng"; 
            ticket.CreatedAt = DateTime.Now; 
            ticket.Status = "Pending"; 
            ticket.Content = ticket.Content + attachmentHtml; 
            _context.SupportTickets.Add(ticket); 
            await _context.SaveChangesAsync(); 
            var firstMsg = new TicketMessage { TicketId = ticket.Id, Sender = "User", Content = ticket.Content, CreatedAt = DateTime.Now }; 
            _context.TicketMessages.Add(firstMsg); 
            await _context.SaveChangesAsync(); 
            if (VelvySkinWeb.Models.GlobalSettings.AutoReplyComplaint && !string.IsNullOrEmpty(userEmail)) 
            { 
                string subject = $"[Velvy Skin] Xác nhận yêu cầu hỗ trợ: {ticket.IssueType ?? "Khiếu nại"}"; 
                string body = $"<div style='font-family: Arial, sans-serif; padding: 25px; border: 1px solid #e0e0e0; border-radius: 15px; max-width: 600px; margin: 0 auto;'><h2 style='color: #2ec4b6; text-align: center;'>Xin chào {ticket.UserFullName},</h2><p>Hệ thống CSKH của Velvy Skin đã tiếp nhận yêu cầu hỗ trợ của bạn với các thông tin sau:</p><div style='background: #f9f9f9; padding: 15px; border-left: 4px solid #c5a059; margin: 15px 0;'><p><b>Mã đơn hàng:</b> {ticket.OrderCode ?? "Không có"}</p><p><b>Loại vấn đề:</b> {ticket.IssueType ?? "Khác"}</p><p><b>Nội dung chi tiết:</b></p><p><i>{ticket.Content}</i></p></div><p>Đội ngũ chuyên viên của chúng tôi đang tiến hành kiểm tra và sẽ liên hệ/phản hồi lại cho bạn trong thời gian sớm nhất (Thông thường từ 2 - 4 tiếng trong giờ hành chính).</p><p>Cảm ơn bạn đã kiên nhẫn và đồng hành cùng Velvy Skin!</p><hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'><p style='color: #888; font-size: 12px; text-align: center;'>Đây là email tự động từ hệ thống Velvy Skin. Vui lòng không trả lời email này.</p></div>"; 
                await SendEmailAsync(userEmail, subject, body); 
            } 
            TempData["ActiveTab"] = "tab-support"; 
            TempData["SuccessMsg"] = "Đã gửi yêu cầu kèm ảnh thành công! Vui lòng kiểm tra hộp thư Email của bạn."; 
            return RedirectToAction("Profile"); 
        }

        [HttpGet] 
        [Authorize] 
        public async Task<IActionResult> TicketDetail(int id) 
        { 
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); 
            var ticket = await _context.SupportTickets.Include(t => t.TicketMessages).FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId); 
            if (ticket == null) return NotFound("Không tìm thấy khiếu nại hoặc bạn không có quyền xem."); 
            if (ticket.TicketMessages == null || !ticket.TicketMessages.Any()) 
            { 
                var firstMessage = new TicketMessage { TicketId = ticket.Id, Sender = "User", Content = ticket.Content ?? ticket.Message, CreatedAt = ticket.CreatedAt }; 
                _context.TicketMessages.Add(firstMessage); 
                await _context.SaveChangesAsync(); 
                ticket = await _context.SupportTickets.Include(t => t.TicketMessages).FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId); 
            } 
            return View(ticket); 
        }

        [HttpPost] 
        [Authorize] 
        [ValidateAntiForgeryToken] 
        public async Task<IActionResult> ReplyTicketUser(int id, string replyContent) 
        { 
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); 
            var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId); 
            if (ticket == null || string.IsNullOrWhiteSpace(replyContent)) return NotFound(); 
            var newMsg = new TicketMessage { TicketId = id, Sender = "User", Content = replyContent, CreatedAt = DateTime.Now }; 
            _context.TicketMessages.Add(newMsg); 
            ticket.Status = "Chờ xử lý"; 
            await _context.SaveChangesAsync(); 
            TempData["SuccessMsg"] = "Đã gửi tin nhắn cho đội ngũ hỗ trợ!"; 
            return RedirectToAction("TicketDetail", new { id = id }); 
        }

        [HttpPost] 
        [Authorize] 
        public async Task<IActionResult> AutoCreateOrderTicket(string orderCode) 
        { 
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); 
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId); 
            var existingTicket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.UserId == userId && t.OrderCode == orderCode && t.Status != "Đã hoàn thành"); 
            if (existingTicket != null) return RedirectToAction("TicketDetail", "Account", new { id = existingTicket.Id }); 
            var newTicket = new SupportTicket { UserId = userId, UserEmail = User.Identity.Name, UserFullName = userProfile?.FullName ?? "Khách hàng", OrderCode = orderCode, IssueType = "Hỗ trợ đơn hàng", Subject = $"Cần hỗ trợ vận chuyển đơn {orderCode}", Content = $"Chào Admin, tôi cần hỗ trợ thông tin giao nhận của đơn hàng {orderCode}.", CreatedAt = DateTime.Now, Status = "Pending" }; 
            _context.SupportTickets.Add(newTicket); 
            await _context.SaveChangesAsync(); 
            var firstMsg = new TicketMessage { TicketId = newTicket.Id, Sender = "User", Content = newTicket.Content, CreatedAt = DateTime.Now }; 
            _context.TicketMessages.Add(firstMsg); 
            await _context.SaveChangesAsync(); 
            return RedirectToAction("TicketDetail", "Account", new { id = newTicket.Id }); 
        }

        [HttpPost] 
        [Authorize] 
        public async Task<IActionResult> CreateProductConsultationTicket(int productId) 
        { 
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); 
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId); 
            var product = await _context.Products.FindAsync(productId); 
            if (product == null) return NotFound(); 
            string ticketSubject = $"Tư vấn sản phẩm: {product.Name}"; 
            var existingTicket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.UserId == userId && t.Subject == ticketSubject && t.Status != "Đã hoàn thành"); 
            if (existingTicket != null) return RedirectToAction("TicketDetail", "Account", new { id = existingTicket.Id }); 
            var newTicket = new SupportTicket { UserId = userId, UserEmail = User.Identity.Name, UserFullName = userProfile?.FullName ?? "Khách hàng Velvy", IssueType = "Tư vấn làm đẹp", Subject = ticketSubject, Content = $"Chào chuyên gia Velvy Skin, em cần tư vấn chi tiết hơn về cách dùng và thành phần của sản phẩm '{product.Name}' (Mã SP: {product.Id}) ạ.", CreatedAt = DateTime.Now, Status = "Pending" }; 
            _context.SupportTickets.Add(newTicket); 
            await _context.SaveChangesAsync(); 
            var firstMsg = new TicketMessage { TicketId = newTicket.Id, Sender = "User", Content = newTicket.Content, CreatedAt = DateTime.Now }; 
            _context.TicketMessages.Add(firstMsg); 
            await _context.SaveChangesAsync(); 
            return RedirectToAction("TicketDetail", "Account", new { id = newTicket.Id }); 
        }

        [HttpGet] 
        [Authorize] 
        public async Task<IActionResult> Notifications() 
        { 
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); 
            var notifications = await _context.Notifications.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).ToListAsync(); 
            return View(notifications); 
        }

        [HttpPost] 
        [Authorize] 
        public async Task<IActionResult> MarkAllAsRead() 
        { 
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); 
            var unreadNotifs = await _context.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync(); 
            if (unreadNotifs.Any()) 
            { 
                foreach(var n in unreadNotifs) n.IsRead = true; 
                await _context.SaveChangesAsync(); 
            } 
            return Json(new { success = true }); 
        }

        [HttpGet] 
        [Authorize] 
        public async Task<IActionResult> ChatDetail(int id) 
        { 
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); 
            var ticket = await _context.SupportTickets.Include(t => t.TicketMessages).FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId); 
            if (ticket == null) return RedirectToAction("Index", "Home"); 
            var unreadNoti = await _context.Notifications.FirstOrDefaultAsync(n => n.UserId == userId && n.TargetUrl.Contains($"/Account/ChatDetail/{id}") && !n.IsRead); 
            if (unreadNoti != null) 
            { 
                unreadNoti.IsRead = true; 
                await _context.SaveChangesAsync(); 
            } 
            return View(ticket); 
        }

        [HttpPost] 
        [Authorize] 
        public async Task<IActionResult> SendMessage(int ticketId, string content) 
        { 
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); 
            var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId && t.UserId == userId); 
            if (ticket == null || string.IsNullOrWhiteSpace(content)) return NotFound(); 
            var newMsg = new TicketMessage { TicketId = ticketId, Sender = "User", Content = content, CreatedAt = DateTime.Now }; 
            _context.TicketMessages.Add(newMsg); 
            ticket.Status = "Chờ xử lý"; 
            await _context.SaveChangesAsync(); 
            return RedirectToAction("ChatDetail", new { id = ticketId }); 
        }

        [HttpPost] 
        [AllowAnonymous] 
        [ValidateAntiForgeryToken] 
        public IActionResult ExternalLogin(string provider, string returnUrl = null) 
        { 
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl }); 
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl); 
            return Challenge(properties, provider); 
        }

        [HttpGet] 
        [AllowAnonymous] 
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null) 
        { 
            returnUrl = returnUrl ?? Url.Content("~/"); 
            if (remoteError != null) 
            { 
                TempData["ErrorMsg"] = $"Lỗi từ Google: {remoteError}"; 
                return RedirectToAction("Login", new { ReturnUrl = returnUrl }); 
            } 
            var info = await _signInManager.GetExternalLoginInfoAsync(); 
            if (info == null) 
            { 
                TempData["ErrorMsg"] = "Lỗi lấy thông tin từ Google."; 
                return RedirectToAction("Login", new { ReturnUrl = returnUrl }); 
            } 
            var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true); 
            if (signInResult.Succeeded) 
            { 
                TempData["SuccessMsg"] = "Đăng nhập thành công!"; 
                return LocalRedirect(returnUrl); 
            } 
            var email = info.Principal.FindFirstValue(ClaimTypes.Email); 
            var name = info.Principal.FindFirstValue(ClaimTypes.Name); 
            if (email != null) 
            { 
                var user = await _userManager.FindByEmailAsync(email); 
                if (user == null) 
                { 

                    user = new ApplicationUser { UserName = email, Email = email }; 
                    var createResult = await _userManager.CreateAsync(user); 
                    if (createResult.Succeeded) 
                    { 
                        if (await _roleManager.RoleExistsAsync("Customer")) 
                        { 
                            await _userManager.AddToRoleAsync(user, "Customer"); 
                        } 
                        var profile = new UserProfile { UserId = user.Id, FullName = name ?? "Khách hàng Google", LockReason = "" }; 
                        _context.UserProfiles.Add(profile); 
                        await _context.SaveChangesAsync(); 
                    } 
                } 
                var addLoginResult = await _userManager.AddLoginAsync(user, info); 
                if (addLoginResult.Succeeded) 
                { 
                    await _signInManager.SignInAsync(user, isPersistent: false); 
                    TempData["SuccessMsg"] = "Chào mừng bạn gia nhập Velvy Skin qua Google!"; 
                    return LocalRedirect(returnUrl); 
                } 
            } 
            TempData["ErrorMsg"] = "Hệ thống không thể lấy được Email từ Google của bạn."; 
            return RedirectToAction("Login", new { ReturnUrl = returnUrl }); 
        }

        [HttpGet] 
        [AllowAnonymous] 
        public IActionResult AccessDenied() => View();
    }
}