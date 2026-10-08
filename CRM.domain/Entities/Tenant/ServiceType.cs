using System.ComponentModel.DataAnnotations;

namespace CRM.domain.Entities.Tenant
{
    /// <summary>
    /// A laundry service offering the shop can select when building an
    /// order (e.g. "Wash & Fold", "Dry Clean - Suit", "Press Only"),
    /// with a default price. Managed by staff so pricing can change
    /// without touching code.
    /// </summary>
    public class ServiceType : BaseEntity
    {
        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        public decimal UnitPrice { get; set; } = 0m;

        // What the quantity is measured in, e.g. "kg", "piece", "load"
        [MaxLength(20)]
        public string? UnitLabel { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
