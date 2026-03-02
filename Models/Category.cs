#nullable enable
using System.ComponentModel.DataAnnotations;

namespace VelvySkinWeb.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        // Mối quan hệ 1-Nhiều: 1 Danh mục có nhiều Sản phẩm
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}