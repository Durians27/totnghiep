using System;
using System.ComponentModel.DataAnnotations;

namespace VelvySkinWeb.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }
        public string Username { get; set; } 
        public string ActionType { get; set; } 
        public string TableName { get; set; } 
        public string Description { get; set; } 
        public DateTime Timestamp { get; set; } = DateTime.Now; 


        public string IpAddress { get; set; } // Lưu IP người dùng
        public string OldValues { get; set; } // Dữ liệu trước khi sửa (JSON string)
        public string NewValues { get; set; } // Dữ liệu sau khi sửa/thêm (JSON string)
    }
}