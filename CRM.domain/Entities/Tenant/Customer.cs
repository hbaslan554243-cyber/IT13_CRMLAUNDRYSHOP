using System.ComponentModel.DataAnnotations;
using CRM.domain.Enums;

namespace CRM.domain.Entities.Tenant
{
    /// <summary>
    /// One record per customer, built up from every visit. This is the center
    /// of the whole CRM - almost every other entity (orders, feedback, loyalty,
    /// notifications) links back to a Customer.
    /// </summary>
    public class Customer : BaseEntity
    {
        [Required, MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string ContactNumber { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? Email { get; set; }

        [MaxLength(250)]
        public string? Address { get; set; }

        // Free-text preferences captured by Front Desk, e.g. "no fabric softener",
        // "always fold, never hang". Kept simple as text rather than a rigid
        // structure since preferences vary a lot per customer.
        [MaxLength(500)]
        public string? Preferences { get; set; }

        public CustomerSegment Segment { get; set; } = CustomerSegment.New;
        public DateTime? LastOrderDate { get; set; }
        public int TotalOrders { get; set; } = 0;
        public decimal TotalSpend { get; set; } = 0m;

        // Navigation properties - the "order history" the profile is built from
        public virtual ICollection<LaundryOrder> Orders { get; set; } = new List<LaundryOrder>();
        public virtual ICollection<Feedback> FeedbackEntries { get; set; } = new List<Feedback>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public virtual LoyaltyAccount? LoyaltyAccount { get; set; }
    }
}
