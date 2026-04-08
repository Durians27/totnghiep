using System;
using System.ComponentModel.DataAnnotations;

namespace VelvySkinWeb.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }
        public string Username { get; set; } // Ai làm?
        public string ActionType { get; set; } // Làm gì? (CREATE, UPDATE, DELETE)
        public string TableName { get; set; } // Ở đâu? (Orders, Products...)
        public string Description { get; set; } // Chi tiết hành động
        public DateTime Timestamp { get; set; } = DateTime.Now; // Khi nào?
    }
}