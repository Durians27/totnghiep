#nullable enable
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http; // Đã cấy: Bắt buộc có để dùng IFormFile hứng ảnh

namespace VelvySkinWeb.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên sản phẩm không được để trống")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Slug { get; set; } // Thêm ?: Cho phép trống lúc mới tạo

        [Required(ErrorMessage = "Giá không được để trống")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public int StockQuantity { get; set; }
        
        public string? Description { get; set; } // Thêm ?: Cho phép trống
        
        public string? ImageUrl { get; set; } // Thêm ?: Cho phép trống vì ban đầu chưa có ảnh
        
        public bool IsActive { get; set; }
        [Display(Name = "Danh mục")]
        public int CategoryId { get; set; }
        
        public Category? Category { get; set; }

        // ĐÃ CẤY: Biến này KHÔNG LƯU VÀO SQL, chỉ dùng làm "thùng chứa" file ảnh từ giao diện
        [NotMapped]
        public IFormFile? ImageFile { get; set; }

        // Khóa ngoại nối sang bảng Category
        [Required(ErrorMessage = "Vui lòng chọn danh mục")]
        [Display(Name = "Mô tả ngắn")]
        public string? ShortDescription { get; set; }

        [Display(Name = "Mô tả chi tiết")]
        public string? FullDescription { get; set; }

        [Display(Name = "Thành phần")]
        public string? Ingredients { get; set; }

        [Display(Name = "Hướng dẫn sử dụng")]
        public string? UsageInstructions { get; set; }
        public int BrandId { get; set; } 
    }
}