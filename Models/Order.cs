using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VelvySkinWeb.Models
{
    public class Order
    {
        public int Id { get; set; }
        

        public string? UserId { get; set; } 

        [Required(ErrorMessage = "Vui lòng nhập họ tên người nhận")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string? PaymentMethod { get; set; }
        public string? ShipperName { get; set; }
        public string? ShipperPhone { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng")]
        public string ShippingAddress { get; set; } = string.Empty;

        public DateTime OrderDate { get; set; } = DateTime.Now;
        public decimal TotalAmount { get; set; }
        

        public string OrderStatus { get; set; } = "Pending"; 


        public ICollection<OrderDetail>? OrderDetails { get; set; }
    }
}