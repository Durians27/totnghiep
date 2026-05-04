using System.Collections.Generic;

namespace VelvySkinWeb.Models
{
    public class CustomerDetailViewModel
    {
        public string? Id { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }

        // CÁC THUỘC TÍNH MỚI BỔ SUNG
        public int OrderCount { get; set; }
        public decimal TotalSpent { get; set; }
        public string? SkinType { get; set; } = "Chưa khảo sát";
        public string? SkinConcern { get; set; } = "Chưa khảo sát";
        public string? Allergy { get; set; } = "Không ghi nhận";

        public List<SupportTicket> SupportTickets { get; set; } = new List<SupportTicket>();
    }
}