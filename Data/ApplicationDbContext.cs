using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VelvySkinWeb.Models;
using VelvySkinWeb.Models;
namespace VelvySkinWeb.Data
{
   public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder); 

    modelBuilder.Entity<Order>().HasIndex(o => o.OrderDate);
    modelBuilder.Entity<Order>().HasIndex(o => o.OrderStatus);
    modelBuilder.Entity<OrderDetail>().HasIndex(od => od.ProductId);
}
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<Coupon> Coupons { get; set; }
      public DbSet<UserProfile> UserProfiles { get; set; }
      public DbSet<SupportTicket> SupportTickets { get; set; }
      public DbSet<TicketMessage> TicketMessages { get; set; }
      public DbSet<Notification> Notifications { get; set; }
      public DbSet<AuditLog> AuditLogs { get; set; }
    }

}

