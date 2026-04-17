using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace VelvySkinWeb.Models.ViewModels
{
    public class EditStaffViewModel
    {
        public string? Id { get; set; }
        public string? Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Họ Tên")]
        public string FullName { get; set; }

        public string? PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? Address { get; set; }
        public string? PrimaryDuty { get; set; }
        public string? InternalNotes { get; set; }
        public IFormFile? AvatarFile { get; set; }
    }
}