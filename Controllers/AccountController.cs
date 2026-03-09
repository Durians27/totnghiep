using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using VelvySkinWeb.Models.ViewModels;
using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Http;
using System;

namespace VelvySkinWeb.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
        }

        // ==========================================
        // 1. ĐĂNG KÝ (REGISTER)
        // ==========================================
        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new IdentityUser { UserName = model.Email, Email = model.Email };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    if (await _roleManager.RoleExistsAsync("Customer"))
                    {
                        await _userManager.AddToRoleAsync(user, "Customer");
                    }

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

        // ==========================================
        // 2. ĐĂNG NHẬP (LOGIN)
        // ==========================================
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
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return LocalRedirect(returnUrl);
                    else
                        return RedirectToAction("Index", "Home");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
                    return View(model);
                }
            }
            return View(model);
        }

        // ==========================================
        // 3. TẠO ADMIN (CÔNG CỤ KHỞI TẠO)
        // ==========================================
        public async Task<IActionResult> CreateAdmin()
        {
            string[] roleNames = { "Admin", "Customer" };
            foreach (var roleName in roleNames)
            {
                var roleExist = await _roleManager.RoleExistsAsync(roleName);
                if (!roleExist) await _roleManager.CreateAsync(new IdentityRole(roleName));
            }

            var adminUser = await _userManager.FindByEmailAsync("admin@velvyskin.com");
            if (adminUser == null)
            {
                var user = new IdentityUser { UserName = "admin@velvyskin.com", Email = "admin@velvyskin.com" };
                var createPowerUser = await _userManager.CreateAsync(user, "Admin@123"); 
                if (createPowerUser.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Admin");
                }
            }
            return Content("Đã khởi tạo xong Hệ thống Phân Quyền và Tài khoản Admin!");
        }

        // ==========================================
        // 4. ĐĂNG XUẤT (LOGOUT)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.Remove("Cart"); 
            return RedirectToAction("Index", "Home");
        }

        // ==========================================
        // TRÁI TIM HỆ THỐNG: HÀM GỬI EMAIL TỰ ĐỘNG
        // ==========================================
        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var senderEmail = "nguyenluc86755@gmail.com"; 
            var appPassword = "vehk egzm gbyl fkwx";     

            var client = new SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(senderEmail, appPassword)
            };

            var mailMessage = new MailMessage(from: senderEmail, to: toEmail, subject, body)
            {
                IsBodyHtml = true
            };

            await client.SendMailAsync(mailMessage);
        }

        // ==========================================
        // 5. TRANG QUÊN MẬT KHẨU (NHẬP EMAIL)
        // ==========================================
        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                TempData["Message"] = "Không tìm thấy tài khoản nào với Email này!";
                return View();
            }

            // 1. Sinh mã OTP 6 số
            string otp = new Random().Next(100000, 999999).ToString();
            
            // 2. Lưu OTP vào Session
            HttpContext.Session.SetString("OTP_" + email, otp);

            // 3. Gửi Email
            string subject = "Mã xác nhận khôi phục mật khẩu - Velvy Skin";
            string body = $@"
                <div style='font-family: Arial; padding: 20px; background: #fdfdfd; text-align: center; border-radius: 10px; border: 1px solid #eee;'>
                    <h2 style='color: #2ec4b6;'>Velvy Skin</h2>
                    <p>Chào bạn,</p>
                    <p>Bạn vừa yêu cầu khôi phục mật khẩu. Mã OTP của bạn là:</p>
                    <h1 style='color: #e993b0; letter-spacing: 5px; font-size: 36px; margin: 20px 0;'>{otp}</h1>
                    <p style='color: #888; font-size: 12px;'>Tuyệt đối không chia sẻ mã này cho bất kỳ ai.</p>
                </div>";

            await SendEmailAsync(email, subject, body);

            return RedirectToAction("VerifyOTP", new { email = email });
        }

        // ==========================================
        // 6. TRANG XÁC THỰC MÃ OTP
        // ==========================================
        [HttpGet]
        public IActionResult VerifyOTP(string email)
        {
            ViewBag.Email = email;
            return View();
        }

        [HttpPost]
        public IActionResult VerifyOTP(string email, string otp)
        {
            string savedOtp = HttpContext.Session.GetString("OTP_" + email);

            if (savedOtp != null && savedOtp == otp)
            {
                return RedirectToAction("ResetPassword", new { email = email });
            }
            
            ModelState.AddModelError(string.Empty, "Mã OTP không hợp lệ. Vui lòng kiểm tra lại.");
            ViewBag.Email = email;
            return View();
        }

        // ==========================================
        // 7. TRANG THIẾT LẬP MẬT KHẨU MỚI
        // ==========================================
        [HttpGet]
        public IActionResult ResetPassword(string email)
        {
            ViewBag.Email = email;
            return View();
        }

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
                
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
            }
            
            ViewBag.Email = email;
            return View();
        }
    }
}