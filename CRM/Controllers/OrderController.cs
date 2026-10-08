using CRM.domain.Entities.Tenant;
using CRM.domain.Enums;
using CRM.infrastructure.data;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers
{
    /// <summary>
    /// Business-logic controller for Laundry Order Management (Feature 2).
    /// Tracks an order through Pending -> Ongoing -> ReadyForPickup ->
    /// Completed (Cancelled as an exit state), and keeps the owning
    /// Customer's summary stats (TotalOrders, TotalSpend, LastOrderDate)
    /// in sync. All queries are scoped to the logged-in user's Company.
    /// </summary>
    public class OrderController
    {
        private readonly LaundryShopDbContext _db;
        private readonly int _companyId;

        public OrderController(LaundryShopDbContext db)
        {
            _db = db;
            _companyId = AuthController.CurrentCompanyId ?? 0;
        }

        // The linear happy-path pipeline. Cancelled is reachable from any
        // state before Completed, but not part of the "next step" sequence.
        private static readonly OrderStatus[] Pipeline =
        {
            OrderStatus.Pending, OrderStatus.Ongoing,
            OrderStatus.ReadyForPickup, OrderStatus.Completed
        };

        public OperationResult CreateOrder(
            int customerId,
            List<(string ServiceName, decimal Quantity, decimal UnitPrice)> lineItems,
            List<int> addOnInventoryItemIds,
            string? specialInstructions,
            string? handledByUserId)
        {
            if (lineItems == null || lineItems.Count == 0)
                return new OperationResult { Success = false, Message = "Add at least one service/item to the order." };

            var customer = _db.Customers.FirstOrDefault(c => c.Id == customerId && !c.IsDeleted && c.CompanyId == _companyId);
            if (customer == null)
                return new OperationResult { Success = false, Message = "Customer not found." };

            var order = new LaundryOrder
            {
                CompanyId = _companyId,
                CustomerId = customerId,
                HandledByUserId = handledByUserId,
                Status = OrderStatus.Pending,
                ReceivedAt = DateTime.UtcNow,
                SpecialInstructions = string.IsNullOrWhiteSpace(specialInstructions) ? null : specialInstructions.Trim()
            };

            foreach (var (serviceName, qty, unitPrice) in lineItems)
            {
                order.Items.Add(new OrderItem
                {
                    CompanyId = _companyId,
                    ServiceName = serviceName,
                    Quantity = qty,
                    UnitPrice = unitPrice
                });
            }

            // Add-ons (detergent, fabric conditioner, etc.) - drawn from
            // this company's Inventory, one unit each, at the current price.
            var consumedItems = new List<InventoryItem>();
            if (addOnInventoryItemIds != null)
            {
                foreach (var itemId in addOnInventoryItemIds.Distinct())
                {
                    var invItem = _db.InventoryItems.FirstOrDefault(i => i.Id == itemId && i.IsActive && i.CompanyId == _companyId);
                    if (invItem == null) continue;

                    order.AddOns.Add(new OrderAddOn
                    {
                        CompanyId = _companyId,
                        InventoryItemId = invItem.Id,
                        Quantity = 1m,
                        UnitPrice = invItem.SellingPrice
                    });
                    consumedItems.Add(invItem);
                }
            }

            order.RecalculateTotal();

            try
            {
                _db.LaundryOrders.Add(order);

                // Consume stock for each add-on used.
                foreach (var invItem in consumedItems)
                {
                    invItem.QuantityOnHand = Math.Max(0, invItem.QuantityOnHand - 1);
                    invItem.UpdatedAt = DateTime.UtcNow;
                }

                // Keep the customer's rollup stats in sync - this is what
                // Segmentation/Loyalty/Analytics will read later.
                customer.TotalOrders += 1;
                customer.LastOrderDate = order.ReceivedAt;
                customer.TotalSpend += order.TotalAmount;

                _db.SaveChanges();
                return new OperationResult { Success = true, Message = "Order created.", Id = order.Id };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }

        public OperationResult AdvanceStatus(int orderId)
        {
            var order = _db.LaundryOrders.Include(o => o.Items).FirstOrDefault(o => o.Id == orderId && o.CompanyId == _companyId);
            if (order == null)
                return new OperationResult { Success = false, Message = "Order not found." };

            if (order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Completed)
                return new OperationResult { Success = false, Message = $"Order is already {order.Status} and cannot be advanced further." };

            var currentIndex = Array.IndexOf(Pipeline, order.Status);
            if (currentIndex < 0 || currentIndex == Pipeline.Length - 1)
                return new OperationResult { Success = false, Message = "Order is already at the final step." };

            var nextStatus = Pipeline[currentIndex + 1];
            return SetStatus(order, nextStatus);
        }

        public OperationResult CancelOrder(int orderId)
        {
            var order = _db.LaundryOrders.FirstOrDefault(o => o.Id == orderId && o.CompanyId == _companyId);
            if (order == null)
                return new OperationResult { Success = false, Message = "Order not found." };

            if (order.Status == OrderStatus.Completed)
                return new OperationResult { Success = false, Message = "A completed order cannot be cancelled." };

            return SetStatus(order, OrderStatus.Cancelled);
        }

        private OperationResult SetStatus(LaundryOrder order, OrderStatus newStatus)
        {
            order.Status = newStatus;
            order.UpdatedAt = DateTime.UtcNow;

            if (newStatus == OrderStatus.ReadyForPickup) order.ReadyAt = DateTime.UtcNow;
            if (newStatus == OrderStatus.Completed) order.CompletedAt = DateTime.UtcNow;

            try
            {
                _db.SaveChanges();

                // Automated Notifications (Feature 4): fire at the exact
                // transaction points the spec calls out.
                if (newStatus == OrderStatus.ReadyForPickup || newStatus == OrderStatus.Completed)
                {
                    var message = newStatus == OrderStatus.ReadyForPickup
                        ? "Your laundry order is ready for pickup!"
                        : "Your laundry order has been completed. Thank you!";
                    var type = newStatus == OrderStatus.ReadyForPickup ? NotificationType.OrderReady : NotificationType.OrderCompleted;

                    new NotificationController(_db).CreateOrderNotification(order.CustomerId, order.Id, type, message);
                }

                return new OperationResult { Success = true, Message = $"Order moved to {newStatus}." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }

        public List<LaundryOrder> GetAllOrders() =>
            _db.LaundryOrders
                .Where(o => o.CompanyId == _companyId)
                .Include(o => o.Customer)
                .Include(o => o.Items)
                .Include(o => o.AddOns)
                .OrderBy(o => o.Id)
                .ToList();

        public List<LaundryOrder> SearchOrders(string keyword, OrderStatus? statusFilter)
        {
            var query = _db.LaundryOrders.Where(o => o.CompanyId == _companyId)
                .Include(o => o.Customer).Include(o => o.Items).AsQueryable();

            if (statusFilter.HasValue)
                query = query.Where(o => o.Status == statusFilter.Value);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                query = query.Where(o => o.Customer != null &&
                    (o.Customer.FullName.Contains(kw) || o.Customer.ContactNumber.Contains(kw)));
            }

            return query.OrderBy(o => o.Id).ToList();
        }

        public LaundryOrder? GetOrder(int orderId) =>
            _db.LaundryOrders
                .Where(o => o.CompanyId == _companyId)
                .Include(o => o.Customer)
                .Include(o => o.Items)
                .Include(o => o.AddOns).ThenInclude(a => a.InventoryItem)
                .Include(o => o.HandledByUser)
                .FirstOrDefault(o => o.Id == orderId);
    }
}
