using Microsoft.EntityFrameworkCore;
using VelvySkinWeb.Data;
using Microsoft.AspNetCore.Identity;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google; 
using Microsoft.AspNetCore.Authentication.Facebook;
using VelvySkinWeb.Models;
var builder = WebApplication.CreateBuilder(args);

// 1. KẾT NỐI DATABASE
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. CẤU HÌNH TÀI KHOẢN (IDENTITY)
builder.Services.AddDefaultIdentity<ApplicationUser>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
})
.AddRoles<IdentityRole>() 
.AddEntityFrameworkStores<ApplicationDbContext>();

// ==========================================
// 🔥 THÊM MỚI: CẤU HÌNH ĐĂNG NHẬP GOOGLE & FACEBOOK
// ==========================================
builder.Services.AddAuthentication()
    .AddGoogle(googleOptions =>
    {
        googleOptions.ClientId = "531812427365-tk75len07rov7ibia0n7hlo3en04qfme.apps.googleusercontent.com";
        googleOptions.ClientSecret = "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";
    })
    .AddFacebook(facebookOptions => 
    {
        // Tôi lấy sẵn App ID trên URL ảnh của Lực dán vào luôn rồi nè!
        facebookOptions.AppId = "1566482217793743";
        
        // 🚨 LỰC NHỚ DÁN APP SECRET (Mã bí mật) CỦA FACEBOOK VÀO ĐÂY NHÉ:
        facebookOptions.AppSecret = "6f3c57baaf0cd4658bb621b83fad1780";
    });

// 3. CẤU HÌNH COOKIE & ĐƯỜNG DẪN LOGIN (Bắt buộc phải đặt SAU Identity)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login"; 
    options.AccessDeniedPath = "/Account/AccessDenied"; 
});

// 4. CÁC DỊCH VỤ KHÁC
builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromMinutes(30); 
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// 5. CẤU HÌNH NGÔN NGỮ (TIẾNG VIỆT)
var supportedCultures = new[] { new CultureInfo("vi-VN") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("vi-VN"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

// 6. PIPELINE XỬ LÝ REQUEST
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.MapStaticAssets(); 
app.UseRouting();

app.UseSession();
app.UseAuthentication(); 
app.UseAuthorization();  

// ==========================================
// THUẬT TOÁN GÁC CỬA BẢO TRÌ THÔNG MINH (PHIÊN BẢN 2.0 - FORCE LOGOUT)
// ==========================================
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower() ?? "";

    // TÌNH HUỐNG 1: Công tắc Bảo trì ĐANG BẬT
    if (VelvySkinWeb.Models.GlobalSettings.IsMaintenanceMode)
    {
        bool isStaticFile = path.StartsWith("/css") || path.StartsWith("/js") || path.StartsWith("/images") || path.StartsWith("/lib");
        bool isAllowedRoute = path.Contains("/home/maintenance") || 
                              path.Contains("/account/login") || 
                              path.Contains("/account/logout") ||
                              path.Contains("/signin-google") || 
                              path.Contains("/signin-facebook"); // 🔥 ĐÃ THÊM: Cho phép luồng Facebook đi qua luôn

        bool isAdmin = context.User != null && 
                       context.User.Identity!.IsAuthenticated && 
                       context.User.IsInRole("Admin");

        // Nếu là User bình thường đang bị đá văng
        if (!isAdmin && !isStaticFile && !isAllowedRoute)
        {
            // HỦY DIỆT TÀI KHOẢN ĐANG ĐĂNG NHẬP (FORCE LOGOUT)
            if (context.User != null && context.User.Identity!.IsAuthenticated)
            {
                // Xóa Cookie Đăng nhập của Identity
                await context.SignOutAsync(IdentityConstants.ApplicationScheme);
                // Xóa luôn giỏ hàng / phiên làm việc tạm thời
                context.Session.Clear(); 
            }

            context.Response.Redirect("/Home/Maintenance");
            return; 
        }
    }
    // TÌNH HUỐNG 2: Công tắc Bảo trì ĐÃ TẮT
    else 
    {
        if (path.Contains("/home/maintenance"))
        {
            context.Response.Redirect("/Account/Login");
            return;
        }
    }

    await next(); 
});
// ==========================================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();