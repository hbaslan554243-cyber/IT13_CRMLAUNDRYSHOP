using System.ComponentModel.DataAnnotations;
using CRM.domain.Enums;

namespace CRM.domain.Entities.Tenant
{
    /// <summary>
    /// A promotion created by Admin/Owner, optionally targeted at a specific
    /// customer segment (e.g. a win-back discount for Inactive customers,
    /// or a loyalty perk for Frequent ones).
    /// </summary>
    public class Promotion : BaseEntity
    {
        [Required, MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        // Structured reason, from the standard business list. Auto-suggested
        // based on customer behavior (see PromotionController.SuggestReason),
        // always changeable by the Owner before saving.
        public PromotionReason? Reason { get; set; }

        // Optional elaboration on top of the structured reason.
        [MaxLength(500)]
        public string? ReasonNotes { get; set; }

        // Set when this promotion was created for one specific customer
        // (e.g. from Customer Retention Details) rather than a whole segment.
        public int? TargetCustomerId { get; set; }
        public virtual Customer? TargetCustomer { get; set; }

        // Null = applies to everyone; set = targeted at that segment only
        public CustomerSegment? TargetSegment { get; set; }

        public PromotionDiscountType DiscountType { get; set; }
        public decimal DiscountValue { get; set; } // percentage or fixed amount, depending on DiscountType

        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; } = true;

        public string CreatedByUserId { get; set; } = string.Empty;
        public virtual ApplicationUser? CreatedByUser { get; set; }

        public bool IsCurrentlyValid()
        {
            var now = DateTime.UtcNow;
            return IsActive && now >= StartDate && now <= EndDate;
        }
    }
}
