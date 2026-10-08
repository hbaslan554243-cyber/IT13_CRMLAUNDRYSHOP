using CRM.domain.Entities.Tenant;
using CRM.infrastructure.data;

namespace CRM.api.Controllers
{
    /// <summary>
    /// Inventory Management. Tracks supplies (detergent, fabric
    /// conditioner, etc.), monitors stock levels, and flags low stock.
    /// Also the source catalog for order add-ons (see OrderController).
    /// </summary>
    public class InventoryController
    {
        private readonly LaundryShopDbContext _db;
        private readonly int _companyId;

        public InventoryController(LaundryShopDbContext db)
        {
            _db = db;
            _companyId = AuthController.CurrentCompanyId ?? 0;
        }

        public List<InventoryItem> GetAll() =>
            _db.InventoryItems.Where(i => i.CompanyId == _companyId).OrderBy(i => i.Name).ToList();

        public List<InventoryItem> GetActive() =>
            _db.InventoryItems.Where(i => i.IsActive && i.CompanyId == _companyId).OrderBy(i => i.Name).ToList();

        public List<InventoryItem> GetLowStockItems() =>
            _db.InventoryItems.Where(i => i.IsActive && i.CompanyId == _companyId && i.QuantityOnHand <= i.ReorderThreshold)
                .OrderBy(i => i.Name).ToList();

        public OperationResult CreateItem(string name, string unit, decimal quantityOnHand, decimal reorderThreshold, decimal sellingPrice)
        {
            if (string.IsNullOrWhiteSpace(name))
                return new OperationResult { Success = false, Message = "Item name is required." };

            try
            {
                _db.InventoryItems.Add(new InventoryItem
                {
                    CompanyId = _companyId,
                    Name = name.Trim(),
                    Unit = string.IsNullOrWhiteSpace(unit) ? "unit" : unit.Trim(),
                    QuantityOnHand = quantityOnHand,
                    ReorderThreshold = reorderThreshold,
                    SellingPrice = sellingPrice,
                    IsActive = true
                });
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = "Item added." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }

        public OperationResult UpdateItem(int id, string name, string unit, decimal quantityOnHand, decimal reorderThreshold, decimal sellingPrice, bool isActive)
        {
            var item = _db.InventoryItems.FirstOrDefault(i => i.Id == id && i.CompanyId == _companyId);
            if (item == null) return new OperationResult { Success = false, Message = "Item not found." };

            item.Name = name.Trim();
            item.Unit = string.IsNullOrWhiteSpace(unit) ? "unit" : unit.Trim();
            item.QuantityOnHand = quantityOnHand;
            item.ReorderThreshold = reorderThreshold;
            item.SellingPrice = sellingPrice;
            item.IsActive = isActive;
            item.UpdatedAt = DateTime.UtcNow;

            try
            {
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = "Item updated." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }
    }
}
