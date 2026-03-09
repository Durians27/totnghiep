namespace VelvySkinWeb.Models
{
    public class OrderDetail
    {
        public int Id { get; set; }
        
        // Nối với bảng Order (Đơn hàng nào?)
        public int OrderId { get; set; }
        public Order? Order { get; set; }
        
        // Nối với bảng Product (Mua chai mỹ phẩm nào?)
        public int ProductId { get; set; }
        public Product? Product { get; set; }
        
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
      
    }
}