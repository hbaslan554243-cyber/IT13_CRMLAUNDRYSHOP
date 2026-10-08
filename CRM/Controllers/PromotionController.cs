using CRM.domain.Entities.Tenant;
using CRM.domain.Enums;
using CRM.infrastructure.data;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers
{
    /// <summary>
    /// Targeted Promotions (Feature 7). Uses customer segments (or one
    /// specific customer, e.g. from Customer Retention Details) so a
    /// promotion can be a win-back offer for Inactive customers or a
    /// loyalty perk for Frequent ones, without emailing/texting everyone.
    /// </summary>
    public class PromotionController
    {
        private readonly LaundryShopDbContext _db;
        private readonly int _companyId;

        public PromotionController(LaundryShopDbContext db)
        {
            _db = db;
            _companyId = AuthController.CurrentCompanyId ?? 0;
        }

        public OperationResult CreatePromotion(Promotion promo)
        {
            if (string.IsNullOrWhiteSpace(promo.Title))
                return new OperationResult { Success = false, Message = "Title is required." };
            if (promo.EndDate < promo.StartDate)
                return new OperationResult { Success = false, Message = "End date must be after start date." };

            promo.CompanyId = _companyId;

            try
            {
                _db.Promotions.Add(promo);
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = "Promotion created.", Id = promo.Id };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }

        public List<Promotion> GetAll() =>
            _db.Promotions.Include(p => p.TargetCustomer).Where(p => p.CompanyId == _companyId).OrderByDescending(p => p.CreatedAt).ToList();

        /// <summary>
        /// Sends a notification to: one specific customer (if TargetCustomerId
        /// is set), everyone in the target segment, or everyone in the shop
        /// if neither is set.
        /// </summary>
        public OperationResult SendPromotion(int promotionId)
        {
            var promo = _db.Promotions.FirstOrDefault(p => p.Id == promotionId && p.CompanyId == _companyId);
            if (promo == null) return new OperationResult { Success = false, Message = "Promotion not found." };

            List<Customer> customers;
            if (promo.TargetCustomerId.HasValue)
            {
                var single = _db.Customers.FirstOrDefault(c => c.Id == promo.TargetCustomerId.Value && !c.IsDeleted && c.CompanyId == _companyId);
                customers = single != null ? new List<Customer> { single } : new List<Customer>();
            }
            else if (promo.TargetSegment.HasValue)
            {
                customers = _db.Customers.Where(c => !c.IsDeleted && c.CompanyId == _companyId && c.Segment == promo.TargetSegment.Value).ToList();
            }
            else
            {
                customers = _db.Customers.Where(c => !c.IsDeleted && c.CompanyId == _companyId).ToList();
            }

            var notifications = new NotificationController(_db);
            foreach (var c in customers)
                notifications.CreatePromotionNotification(c.Id, promo.Id, $"{promo.Title}: {promo.Description}");

            return new OperationResult { Success = true, Message = $"Sent to {customers.Count} customer(s)." };
        }

        /// <summary>
        /// Suggests a reason based on this specific customer's actual
        /// behavior - not the segment they're bucketed into. Matches the
        /// spec's examples: lapsed a while -> win them back; lots of orders
        /// and still active -> reward loyalty; used to order often but has
        /// recently slowed down -> retention play.
        /// </summary>
        public PromotionReason SuggestReason(int customerId)
        {
            var customer = _db.Customers.FirstOrDefault(c => c.Id == customerId && c.CompanyId == _companyId);
            var company = _db.Companies.First(c => c.Id == _companyId);

            if (customer == null || customer.TotalOrders == 0)
                return PromotionReason.NewCustomerAcquisition;

            var daysSince = customer.LastOrderDate.HasValue
                ? (DateTime.UtcNow - customer.LastOrderDate.Value).TotalDays
                : double.MaxValue;

            // Long-lapsed: harder win-back framing than a simple "come back" offer.
            if (daysSince > company.InactiveDaysThreshold * 2)
                return PromotionReason.CustomerRecovery;

            // Lapsed within the shop's own inactive window.
            if (daysSince > company.InactiveDaysThreshold)
                return PromotionReason.ReactivateInactiveCustomer;

            // Frequent orderer who's still ordering recently - reward the loyalty.
            if (customer.TotalOrders >= company.FrequentOrderThreshold && daysSince <= company.InactiveDaysThreshold / 2.0)
                return PromotionReason.RewardLoyalCustomer;

            // High spender, not yet flagged above - encourage continued spend.
            if (customer.TotalSpend >= company.HighValueSpendThreshold)
                return PromotionReason.IncreaseCustomerSpending;

            // Has ordered more than once but is trending toward the inactive window - catch them before they lapse.
            if (customer.TotalOrders > 1 && daysSince > company.InactiveDaysThreshold / 2.0)
                return PromotionReason.CustomerRetention;

            // Default for an otherwise-normal, still-active customer.
            return PromotionReason.IncreaseOrderFrequency;
        }
    }
}
