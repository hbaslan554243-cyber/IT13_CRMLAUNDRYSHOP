using System.ComponentModel.DataAnnotations;
using CRM.domain.Enums;

namespace CRM.domain.Entities.Tenant
{
    /// <summary>
    /// One laundry transaction/drop-off. Tracks status through the workflow:
    /// Pending -> Ongoing -> ReadyForPickup -> Completed.
    /// </summary>
    public class LaundryOrder : BaseEntity
    {
        [Required]
        public int CustomerId { get; set; }
        public virtual Customer? Customer { get; set; }

        // Which staff member took/processed this order
        public string? HandledByUserId { get; set; }
        public virtual ApplicationUser? HandledByUser { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadyAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        [MaxLength(500)]
        public string? SpecialInstructions { get; set; }

        public decimal TotalAmount { get; set; } = 0m;

        // Navigation
        public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
        public virtual ICollection<OrderAddOn> AddOns { get; set; } = new List<OrderAddOn>();
        public virtual Payment? Payment { get; set; }

        /// <summary>
        /// Recalculates TotalAmount from line items plus any add-ons
        /// (detergent, fabric conditioner, etc.). Call after adding/removing
        /// either. Keeping this logic on the entity itself (not scattered in
        /// a controller) is basic OOP encapsulation - the order is
        /// responsible for its own total.
        /// </summary>
        public void RecalculateTotal()
        {
            TotalAmount = Items.Sum(i => i.Subtotal) + AddOns.Sum(a => a.Subtotal);
        }
    }

    /// <summary>
    /// A single line item within an order, e.g. "Wash & Fold - 5kg" or "Dry Clean - Suit x2".
    /// </summary>
    public class OrderItem : BaseEntity
    {
        [Required]
        public int LaundryOrderId { get; set; }
        public virtual LaundryOrder? LaundryOrder { get; set; }

        [Required, MaxLength(150)]
        public string ServiceName { get; set; } = string.Empty; // e.g. "Wash & Fold", "Dry Clean"

        public decimal Quantity { get; set; } = 1m; // kg or piece count, depending on service
        public decimal UnitPrice { get; set; } = 0m;

        public decimal Subtotal => Quantity * UnitPrice;
    }

    /// <summary>
    /// An optional add-on the customer chose for this order - detergent,
    /// fabric conditioner, etc. Drawn from the shop's InventoryItem catalog
    /// so selling one also consumes stock (see InventoryController).
    /// </summary>
    public class OrderAddOn : BaseEntity
    {
        [Required]
        public int LaundryOrderId { get; set; }
        public virtual LaundryOrder? LaundryOrder { get; set; }

        [Required]
        public int InventoryItemId { get; set; }
        public virtual InventoryItem? InventoryItem { get; set; }

        public decimal Quantity { get; set; } = 1m;

        // Snapshot of the price at the time of sale, so later price changes
        // to the inventory item don't retroactively change past orders.
        public decimal UnitPrice { get; set; } = 0m;

        public decimal Subtotal => Quantity * UnitPrice;
    }
}
