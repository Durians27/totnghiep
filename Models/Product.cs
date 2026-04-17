#nullable enable
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

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
        public string? Slug { get; set; }

        [Required(ErrorMessage = "Giá không được để trống")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public int StockQuantity { get; set; }
        
        public string? Description { get; set; }
        
        public string? ImageUrl { get; set; } 
        public string? ImageUrl2 { get; set; }
        public string? ImageUrl3 { get; set; }
        public string? ImageUrl4 { get; set; }
        public string? ImageUrl5 { get; set; }
        
        public bool IsActive { get; set; }
        [Display(Name = "Danh mục")]
        public int CategoryId { get; set; }
        
        public Category? Category { get; set; }


        [NotMapped]
        public IFormFile? ImageFile { get; set; }


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
        public decimal? PriceLarge { get; set; }
        public decimal DiscountPrice { get; set; }
        public string? Brand { get; set; } 
        public string? Tags { get; set; }
    }
}