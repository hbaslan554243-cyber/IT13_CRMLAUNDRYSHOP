using System.ComponentModel.DataAnnotations;

namespace CRM.domain.Entities.Tenant
{
    /// <summary>
    /// One shop, owned by one AdminOwner account. This is the tenant
    /// boundary: every Customer, Order, ServiceType, InventoryItem, etc.
    /// carries a CompanyId (via BaseEntity) matching one of these rows, so
    /// multiple Owners can run completely separate shops from the same
    /// database without ever seeing each other's data.
    ///
    /// Deliberately does NOT inherit BaseEntity - a Company isn't itself
    /// "owned by" a company, it IS one.
    /// </summary>
    public class Company
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // The AdminOwner account that owns this shop.
        public string OwnerUserId { get; set; } = string.Empty;
        public virtual ApplicationUser? Owner { get; set; }

        // Tenant C can add branch companies under its original company. Each
        // branch uses CompanyId as its data boundary for users, orders and reports.
        public int? ParentCompanyId { get; set; }
        public virtual Company? ParentCompany { get; set; }

        // Platform-level subscription controls managed by the Super Admin.
        public bool IsActive { get; set; } = true;
        public DateTime? SubscriptionExpiresAt { get; set; }

        [Required, MaxLength(150)]
        public string ShopName { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Address { get; set; }

        [MaxLength(20)]
        public string? ContactNumber { get; set; }

        // What this shop has chosen to avail, set by the Owner in Shop
        // Profile. A one-counter shop where the Owner does everything
        // themselves might not need either; a shop with a supervisor would
        // turn on Manager accounts, etc.
        public bool AllowStaffAccounts { get; set; } = true;
        public bool AllowManagerAccounts { get; set; } = true;

        // Segmentation thresholds - configurable per shop instead of hardcoded.
        public int FrequentOrderThreshold { get; set; } = 5;
        public int InactiveDaysThreshold { get; set; } = 60;
        public decimal HighValueSpendThreshold { get; set; } = 5000m;

        // Loyalty configuration: every this-many pesos spent earns 1 point
        // (Points Earned = Floor(EligibleAmount / PesosPerPoint)). Default 10
        // means P259 -> 25 points. Still adjustable per shop in Shop Profile.
        public decimal PesosPerPoint { get; set; } = 10m;

        [MaxLength(2000)]
        public string? TermsAndConditions { get; set; }
    }
}
