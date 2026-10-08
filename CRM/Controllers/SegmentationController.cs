using CRM.domain.Entities.Tenant;
using CRM.domain.Enums;
using CRM.infrastructure.data;

namespace CRM.api.Controllers
{
    /// <summary>
    /// Customer Segmentation (Feature 3). Groups customers by behavior using
    /// this shop's configurable thresholds (Company), so segments can be
    /// tuned per shop rather than hardcoded.
    /// </summary>
    public class SegmentationController
    {
        private readonly LaundryShopDbContext _db;
        private readonly int _companyId;

        public SegmentationController(LaundryShopDbContext db)
        {
            _db = db;
            _companyId = AuthController.CurrentCompanyId ?? 0;
        }

        /// <summary>Recomputes every active customer's segment. Call on-demand (a button) rather than automatically on every save.</summary>
        public int RecalculateAllSegments()
        {
            var company = _db.Companies.First(c => c.Id == _companyId);
            var customers = _db.Customers.Where(c => !c.IsDeleted && c.CompanyId == _companyId).ToList();
            int changed = 0;

            foreach (var c in customers)
            {
                var newSegment = ComputeSegment(c, company);
                if (c.Segment != newSegment)
                {
                    c.Segment = newSegment;
                    c.UpdatedAt = DateTime.UtcNow;
                    changed++;
                }
            }

            if (changed > 0) _db.SaveChanges();
            return changed;
        }

        private static CustomerSegment ComputeSegment(Customer c, Company company)
        {
            if (c.TotalOrders == 0) return CustomerSegment.New;

            var daysSinceLastOrder = c.LastOrderDate.HasValue
                ? (DateTime.UtcNow - c.LastOrderDate.Value).TotalDays
                : double.MaxValue;

            if (daysSinceLastOrder > company.InactiveDaysThreshold) return CustomerSegment.Inactive;
            if (c.TotalSpend >= company.HighValueSpendThreshold) return CustomerSegment.HighValue;
            if (c.TotalOrders >= company.FrequentOrderThreshold) return CustomerSegment.Frequent;
            return CustomerSegment.Regular;
        }

        public Dictionary<CustomerSegment, List<Customer>> GetCustomersBySegment() =>
            _db.Customers.Where(c => !c.IsDeleted && c.CompanyId == _companyId).ToList()
                .GroupBy(c => c.Segment)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.FullName).ToList());
    }
}
