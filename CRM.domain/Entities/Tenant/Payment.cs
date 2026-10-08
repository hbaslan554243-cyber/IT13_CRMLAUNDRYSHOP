using System.ComponentModel.DataAnnotations;
using CRM.domain.Enums;

namespace CRM.domain.Entities.Tenant
{
    /// <summary>
    /// One payment record per order. Kept separate from LaundryOrder so an
    /// order can exist (and be worked on) before payment is settled, and so
    /// receipts have a clean, dedicated record to print/reference.
    /// </summary>
    public class Payment : BaseEntity
    {
        [Required]
        public int LaundryOrderId { get; set; }
        public virtual LaundryOrder? LaundryOrder { get; set; }

        public decimal AmountDue { get; set; } = 0m;
        public decimal AmountPaid { get; set; } = 0m;
        public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public DateTime? PaidAt { get; set; }

        [MaxLength(50)]
        public string? ReceiptNumber { get; set; }

        // Loyalty redemption applied at checkout (1 point = 1 peso off).
        public int PointsRedeemed { get; set; } = 0;
        public decimal LoyaltyDiscount { get; set; } = 0m;

        // Guards against awarding loyalty points more than once for the
        // same payment (e.g. if it's edited/re-saved after already being Paid).
        public bool PointsAwarded { get; set; } = false;

        public decimal Balance => AmountDue - AmountPaid;
    }
}
