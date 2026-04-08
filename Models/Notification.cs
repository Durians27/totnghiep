using System;
using System.ComponentModel.DataAnnotations;

namespace VelvySkinWeb.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }
        
        public string UserId { get; set; }
        
        [Required]
        public string Title { get; set; }
        
        [Required]
        public string Message { get; set; }
        
        public string Type { get; set; }
        
        public string Icon { get; set; }
        
        public bool IsRead { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? TargetUrl { get; set; }
    }
}