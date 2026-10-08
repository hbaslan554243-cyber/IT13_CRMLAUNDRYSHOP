using System.ComponentModel.DataAnnotations;
using CRM.domain.Enums;

namespace CRM.domain.Entities.Tenant
{
    /// <summary>
    /// An automated notification triggered at a transaction point
    /// (order ready, order completed, promo blast, loyalty reward earned).
    /// </summary>
    public class Notification : BaseEntity
    {
        [Required]
        public int CustomerId { get; set; }
        public virtual Customer? Customer { get; set; }

        // Optional links to what triggered this notification
        public int? LaundryOrderId { get; set; }
        public virtual LaundryOrder? LaundryOrder { get; set; }
        public int? PromotionId { get; set; }
        public virtual Promotion? Promotion { get; set; }

        public NotificationType Type { get; set; }
        public NotificationChannel Channel { get; set; } = NotificationChannel.SMS;

        [Required, MaxLength(500)]
        public string Message { get; set; } = string.Empty;

        public bool IsSent { get; set; } = false;
        public DateTime? SentAt { get; set; }
    }
}
