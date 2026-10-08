using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CRM.domain.Entities.Tenant;

namespace CRM.infrastructure.data
{
    /// <summary>
    /// The single database backing every shop. Multi-tenancy is done at the
    /// row level: every business entity carries a CompanyId (via BaseEntity),
    /// and every Controller filters by the current user's CompanyId - see
    /// AuthController.CurrentCompanyId. Multiple Owners can run completely
    /// separate shops from this one database without ever seeing each
    /// other's data.
    /// </summary>
    public class LaundryShopDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<Company> Companies => Set<Company>();
        public DbSet<TenantRegistry> TenantRegistries => Set<TenantRegistry>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<LaundryOrder> LaundryOrders => Set<LaundryOrder>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Feedback> Feedbacks => Set<Feedback>();
        public DbSet<LoyaltyAccount> LoyaltyAccounts => Set<LoyaltyAccount>();
        public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<Promotion> Promotions => Set<Promotion>();
        public DbSet<ServiceType> ServiceTypes => Set<ServiceType>();
        public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
        public DbSet<OrderAddOn> OrderAddOns => Set<OrderAddOn>();

        public LaundryShopDbContext(DbContextOptions<LaundryShopDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ---- Customer: the hub everything else links back to ----
            builder.Entity<Customer>(entity =>
            {
                entity.HasOne(c => c.LoyaltyAccount)
                    .WithOne(l => l.Customer)
                    .HasForeignKey<LoyaltyAccount>(l => l.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(c => c.Orders)
                    .WithOne(o => o.Customer)
                    .HasForeignKey(o => o.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict); // keep order history even if a customer row is removed

                entity.HasMany(c => c.FeedbackEntries)
                    .WithOne(f => f.Customer)
                    .HasForeignKey(f => f.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(c => c.Notifications)
                    .WithOne(n => n.Customer)
                    .HasForeignKey(n => n.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(c => c.TotalSpend).HasPrecision(12, 2);
            });

            // ---- LaundryOrder ----
            builder.Entity<LaundryOrder>(entity =>
            {
                entity.HasMany(o => o.Items)
                    .WithOne(i => i.LaundryOrder)
                    .HasForeignKey(i => i.LaundryOrderId)
                    .OnDelete(DeleteBehavior.Cascade); // line items belong to exactly one order

                entity.HasMany(o => o.AddOns)
                    .WithOne(a => a.LaundryOrder)
                    .HasForeignKey(a => a.LaundryOrderId)
                    .OnDelete(DeleteBehavior.Cascade); // add-ons belong to exactly one order

                entity.HasOne(o => o.Payment)
                    .WithOne(p => p.LaundryOrder)
                    .HasForeignKey<Payment>(p => p.LaundryOrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(o => o.HandledByUser)
                    .WithMany(u => u.OrdersHandled)
                    .HasForeignKey(o => o.HandledByUserId)
                    .OnDelete(DeleteBehavior.Restrict); // don't delete order history if a staff account is removed

                entity.Property(o => o.TotalAmount).HasPrecision(12, 2);
            });

            builder.Entity<OrderItem>(entity =>
            {
                entity.Property(i => i.Quantity).HasPrecision(10, 2);
                entity.Property(i => i.UnitPrice).HasPrecision(10, 2);
            });

            // ---- Payment ----
            builder.Entity<Payment>(entity =>
            {
                entity.Property(p => p.AmountDue).HasPrecision(12, 2);
                entity.Property(p => p.AmountPaid).HasPrecision(12, 2);
            });

            // ---- Feedback ----
            builder.Entity<Feedback>(entity =>
            {
                entity.HasOne(f => f.LaundryOrder)
                    .WithMany()
                    .HasForeignKey(f => f.LaundryOrderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(f => f.ResolvedByUser)
                    .WithMany(u => u.FeedbackResolved)
                    .HasForeignKey(f => f.ResolvedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---- Loyalty ----
            builder.Entity<LoyaltyAccount>(entity =>
            {
                entity.HasMany(l => l.Transactions)
                    .WithOne(t => t.LoyaltyAccount)
                    .HasForeignKey(t => t.LoyaltyAccountId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<LoyaltyTransaction>(entity =>
            {
                entity.HasOne(t => t.LaundryOrder)
                    .WithMany()
                    .HasForeignKey(t => t.LaundryOrderId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---- Notification ----
            builder.Entity<Notification>(entity =>
            {
                entity.HasOne(n => n.LaundryOrder)
                    .WithMany()
                    .HasForeignKey(n => n.LaundryOrderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(n => n.Promotion)
                    .WithMany()
                    .HasForeignKey(n => n.PromotionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---- Promotion ----
            builder.Entity<Promotion>(entity =>
            {
                entity.Property(p => p.DiscountValue).HasPrecision(10, 2);

                entity.HasOne(p => p.CreatedByUser)
                    .WithMany()
                    .HasForeignKey(p => p.CreatedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.TargetCustomer)
                    .WithMany()
                    .HasForeignKey(p => p.TargetCustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---- Company: the tenant boundary ----
            builder.Entity<Company>(entity =>
            {
                entity.Property(c => c.HighValueSpendThreshold).HasPrecision(12, 2);
                entity.Property(c => c.PesosPerPoint).HasPrecision(10, 2);

                entity.HasOne(c => c.Owner)
                    .WithMany()
                    .HasForeignKey(c => c.OwnerUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(c => c.ParentCompany)
                    .WithMany()
                    .HasForeignKey(c => c.ParentCompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---- ApplicationUser: which shop this staff account belongs to ----
            builder.Entity<ApplicationUser>(entity =>
            {
                entity.HasOne(u => u.Company)
                    .WithMany()
                    .HasForeignKey(u => u.CompanyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---- ServiceType: the pricing catalog ----
            builder.Entity<ServiceType>(entity =>
            {
                entity.Property(s => s.UnitPrice).HasPrecision(10, 2);
            });

            // ---- InventoryItem: supplies, also the order add-on catalog ----
            builder.Entity<InventoryItem>(entity =>
            {
                entity.Property(i => i.QuantityOnHand).HasPrecision(10, 2);
                entity.Property(i => i.ReorderThreshold).HasPrecision(10, 2);
                entity.Property(i => i.SellingPrice).HasPrecision(10, 2);
            });

            // ---- OrderAddOn ----
            builder.Entity<OrderAddOn>(entity =>
            {
                entity.Property(a => a.Quantity).HasPrecision(10, 2);
                entity.Property(a => a.UnitPrice).HasPrecision(10, 2);

                entity.HasOne(a => a.InventoryItem)
                    .WithMany()
                    .HasForeignKey(a => a.InventoryItemId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
