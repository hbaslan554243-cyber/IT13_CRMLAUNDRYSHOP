using CRM.domain.Entities.Tenant;
using CRM.infrastructure.data;

namespace CRM.api.Controllers
{
    /// <summary>
    /// System Administration (Feature 11). GetMyCompany/UpdateSettings are
    /// used by the Admin/Owner's "Shop Profile" screen for their own
    /// operational details (name, address, thresholds, terms, and which
    /// roles they've chosen to avail - Staff and/or Manager accounts).
    /// GetAllCompanies is Super Admin only, for the shop directory screen.
    /// </summary>
    public class SettingsController
    {
        private readonly LaundryShopDbContext _db;

        public SettingsController(LaundryShopDbContext db)
        {
            _db = db;
        }

        /// <summary>Admin/Owner/Manager/Staff: their own shop's settings.</summary>
        public Company GetMyCompany()
        {
            var companyId = AuthController.CurrentCompanyId;
            return _db.Companies.First(c => c.Id == companyId);
        }

        /// <summary>Super Admin only: every shop on the platform, for the shop directory screen.</summary>
        public List<Company> GetAllCompanies() => _db.Companies.OrderBy(c => c.ShopName).ToList();

        /// <summary>Super Admin platform-level tenant controls.</summary>
        public OperationResult SetTenantSubscription(int companyId, bool isActive, DateTime? expiresAt)
        {
            var company = _db.Companies.FirstOrDefault(c => c.Id == companyId);
            if (company == null) return new OperationResult { Success = false, Message = "Tenant not found." };

            company.IsActive = isActive;
            company.SubscriptionExpiresAt = expiresAt?.Date;
            company.UpdatedAt = DateTime.UtcNow;

            try
            {
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = "Tenant subscription updated." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }

        /// <summary>Admin/Owner's own operational settings, including which roles they've chosen to avail.</summary>
        public OperationResult UpdateSettings(Company company)
        {
            var existing = _db.Companies.FirstOrDefault(c => c.Id == company.Id);
            if (existing == null) return new OperationResult { Success = false, Message = "Shop not found." };

            existing.ShopName = company.ShopName;
            existing.Address = company.Address;
            existing.ContactNumber = company.ContactNumber;
            existing.AllowStaffAccounts = company.AllowStaffAccounts;
            existing.AllowManagerAccounts = company.AllowManagerAccounts;
            existing.FrequentOrderThreshold = company.FrequentOrderThreshold;
            existing.InactiveDaysThreshold = company.InactiveDaysThreshold;
            existing.HighValueSpendThreshold = company.HighValueSpendThreshold;
            existing.PesosPerPoint = company.PesosPerPoint;
            existing.TermsAndConditions = company.TermsAndConditions;
            existing.UpdatedAt = DateTime.UtcNow;

            try
            {
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = "Settings updated." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }
    }
}
