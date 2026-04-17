using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace VelvySkinWeb.Models.ViewModels
{
    public class CreateStaffViewModel
    {
        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Họ Tên")]
        public string FullName { get; set; }

        public string? PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? Address { get; set; }
        public string? PrimaryDuty { get; set; } // CSKH, Kho, Đơn hàng
        public IFormFile? AvatarFile { get; set; }
    }
}