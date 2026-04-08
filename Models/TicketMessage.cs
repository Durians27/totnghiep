using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VelvySkinWeb.Models
{
    public class TicketMessage
    {
        [Key]
        public int Id { get; set; }


        [Required]
        public int TicketId { get; set; }
        [ForeignKey("TicketId")]
        public SupportTicket? Ticket { get; set; }

        [Required]
        public string Sender { get; set; }

        [Required]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}