using System.ComponentModel.DataAnnotations;

namespace CRM.domain.Entities.Tenant
{
    /// <summary>
    /// A supply the shop tracks stock of - detergent, fabric conditioner,
    /// etc. Doubles as the catalog for order add-ons: selling one as an
    /// add-on decrements QuantityOnHand (see InventoryController).
    /// </summary>
    public class InventoryItem : BaseEntity
    {
        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        // e.g. "sachet", "liter", "bottle"
        [MaxLength(20)]
        public string Unit { get; set; } = "unit";

        public decimal QuantityOnHand { get; set; } = 0m;

        // Alert threshold - at or below this, the item shows as low stock.
        public decimal ReorderThreshold { get; set; } = 5m;

        // What's charged to the customer if offered as an order add-on.
        // 0 for internal-only supplies not sold directly.
        public decimal SellingPrice { get; set; } = 0m;

        public bool IsActive { get; set; } = true;

        public bool IsLowStock => QuantityOnHand <= ReorderThreshold;
    }
}
