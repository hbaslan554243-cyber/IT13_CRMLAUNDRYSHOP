using CRM.domain.Entities.Tenant;

namespace CRM.infrastructure.data
{
    /// <summary>
    /// Gives a brand-new shop (Company) a sensible starting catalog, so a
    /// new Owner isn't staring at completely empty Services/Inventory
    /// screens before they can even create their first order.
    /// </summary>
    public static class TenantDefaults
    {
        public static void SeedFor(LaundryShopDbContext db, int companyId)
        {
            db.ServiceTypes.AddRange(
                new ServiceType { CompanyId = companyId, Name = "Wash & Fold", UnitPrice = 45m, UnitLabel = "kg" },
                new ServiceType { CompanyId = companyId, Name = "Dry Clean", UnitPrice = 150m, UnitLabel = "piece" },
                new ServiceType { CompanyId = companyId, Name = "Press Only", UnitPrice = 25m, UnitLabel = "piece" }
            );

            db.InventoryItems.AddRange(
                new InventoryItem { CompanyId = companyId, Name = "Detergent", Unit = "sachet", QuantityOnHand = 50, ReorderThreshold = 10, SellingPrice = 20m },
                new InventoryItem { CompanyId = companyId, Name = "Fabric Conditioner", Unit = "sachet", QuantityOnHand = 50, ReorderThreshold = 10, SellingPrice = 20m }
            );

            db.SaveChanges();
        }
    }
}
