using CRM.domain.Entities.Tenant;
using CRM.infrastructure.data;

namespace CRM.api.Controllers
{
    /// <summary>
    /// Business-logic controller for managing the shop's service/pricing
    /// catalog (e.g. "Wash & Fold" @ 50/kg). Used to populate the service
    /// dropdown in Order creation, and editable by staff so pricing
    /// changes don't require a code change.
    /// </summary>
    public class ServiceTypeController
    {
        private readonly LaundryShopDbContext _db;
        private readonly int _companyId;

        public ServiceTypeController(LaundryShopDbContext db)
        {
            _db = db;
            _companyId = AuthController.CurrentCompanyId ?? 0;
        }

        public List<ServiceType> GetAllServiceTypes() =>
            _db.ServiceTypes.Where(s => s.CompanyId == _companyId).OrderBy(s => s.Name).ToList();

        public List<ServiceType> GetActiveServiceTypes() =>
            _db.ServiceTypes.Where(s => s.IsActive && s.CompanyId == _companyId).OrderBy(s => s.Name).ToList();

        public OperationResult CreateServiceType(string name, decimal unitPrice, string? unitLabel, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name))
                return new OperationResult { Success = false, Message = "Service name is required." };

            try
            {
                _db.ServiceTypes.Add(new ServiceType
                {
                    CompanyId = _companyId,
                    Name = name.Trim(),
                    UnitPrice = unitPrice,
                    UnitLabel = string.IsNullOrWhiteSpace(unitLabel) ? null : unitLabel.Trim(),
                    IsActive = isActive
                });
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = "Service added." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }

        public OperationResult UpdateServiceType(int id, string name, decimal unitPrice, string? unitLabel, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(name))
                return new OperationResult { Success = false, Message = "Service name is required." };

            var service = _db.ServiceTypes.FirstOrDefault(s => s.Id == id && s.CompanyId == _companyId);
            if (service == null)
                return new OperationResult { Success = false, Message = "Service not found." };

            service.Name = name.Trim();
            service.UnitPrice = unitPrice;
            service.UnitLabel = string.IsNullOrWhiteSpace(unitLabel) ? null : unitLabel.Trim();
            service.IsActive = isActive;
            service.UpdatedAt = DateTime.UtcNow;

            try
            {
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = "Service updated." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }
    }
}
