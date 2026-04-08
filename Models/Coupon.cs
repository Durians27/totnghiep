using System;
using System.ComponentModel.DataAnnotations;

namespace VelvySkinWeb.Models
{
    public class Coupon
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên chương trình không được để trống")]
        public string Name { get; set; }

        public string? Description { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; }


        public string DiscountType { get; set; } = "Amount";
        
        public double DiscountValue { get; set; }

        public DateTime StartDate { get; set; } = DateTime.Now;
        public DateTime EndDate { get; set; } = DateTime.Now.AddDays(30);

        public int UsedCount { get; set; } = 0;
        public int MaxUses { get; set; }

        public bool IsActive { get; set; } = true; 
        public double MinOrderValue { get; set; } = 0; 
        public int MaxUsesPerUser { get; set; } = 1;   
        public double MaxDiscountAmount { get; set; } = 0;
        
    }
}