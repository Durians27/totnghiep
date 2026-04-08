using System;
using System.ComponentModel.DataAnnotations;

namespace VelvySkinWeb.Models
{
    public class SupportTicket
    {
        [Key]
        public int Id { get; set; }
        

        public string? UserId { get; set; } 
        public string? UserFullName { get; set; } 
        public string? UserEmail { get; set; } 


        public string? IssueType { get; set; }
        public string? OrderCode { get; set; }
        public string? Content { get; set; }
        public string? AttachedFiles { get; set; }

        public string? Subject { get; set; } 
        public string? Message { get; set; } 

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Pending"; 
        public string? AdminResponse { get; set; } 
        public DateTime? RespondedAt { get; set; } 
        public ICollection<TicketMessage>? TicketMessages { get; set; }
    }
}