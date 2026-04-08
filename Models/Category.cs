using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VelvySkinWeb.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên danh mục")]
        [Display(Name = "Tên danh mục")]
        public string Name { get; set; }

        [Display(Name = "Mô tả")]
        public string Description { get; set; }


        [Display(Name = "Nhóm danh mục")]
        public string? CategoryGroup { get; set; } 


        public ICollection<Product>? Products { get; set; }
    }
}