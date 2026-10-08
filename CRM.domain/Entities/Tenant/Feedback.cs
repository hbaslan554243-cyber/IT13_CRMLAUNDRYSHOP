using System.ComponentModel.DataAnnotations;
using CRM.domain.Enums;

namespace CRM.domain.Entities.Tenant
{
    /// <summary>
    /// A feedback or complaint entry. Front Desk logs it; if they can't resolve
    /// it, it gets escalated to a Manager. Optionally linked to a specific order.
    /// </summary>
    public class Feedback : BaseEntity
    {
        [Required]
        public int CustomerId { get; set; }
        public virtual Customer? Customer { get; set; }

        // Optional - complaint may relate to a specific order, or be general
        public int? LaundryOrderId { get; set; }
        public virtual LaundryOrder? LaundryOrder { get; set; }

        public FeedbackType Type { get; set; } = FeedbackType.Feedback;
        public FeedbackStatus Status { get; set; } = FeedbackStatus.Open;

        [Required, MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        // Optional 1-5 star customer satisfaction rating. Feeds the
        // Average Rating KPI on the Dashboard/Reports.
        public int? Rating { get; set; }

        [MaxLength(1000)]
        public string? ResolutionNotes { get; set; }

        public string? ResolvedByUserId { get; set; }
        public virtual ApplicationUser? ResolvedByUser { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}
