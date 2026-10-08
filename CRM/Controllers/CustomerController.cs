using System.Text.RegularExpressions;
using CRM.domain.Entities.Tenant;
using CRM.infrastructure.data;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers
{
    public class OperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? Id { get; set; }
    }

    /// <summary>
    /// Business-logic controller for Customer Profile Management (Feature 1).
    /// Called directly by CleanWaveCRM.Desktop's Forms (View layer);
    /// talks to LaundryShopDbContext (Model/Data layer) in CRM.infrastructure.
    /// All queries are scoped to the logged-in user's Company (multi-tenancy).
    /// </summary>
    public class CustomerController
    {
        private readonly LaundryShopDbContext _db;
        private readonly int _companyId;

        public CustomerController(LaundryShopDbContext db)
        {
            _db = db;
            _companyId = AuthController.CurrentCompanyId ?? 0;
        }

        public OperationResult RegisterCustomer(Customer customer)
        {
            var validation = Validate(customer);
            if (!validation.Success) return validation;

            customer.CompanyId = _companyId;

            try
            {
                _db.Customers.Add(customer);
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = "Customer registered successfully.", Id = customer.Id };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }

        public OperationResult UpdateCustomer(Customer customer)
        {
            var validation = Validate(customer);
            if (!validation.Success) return validation;

            try
            {
                customer.UpdatedAt = DateTime.UtcNow;
                _db.Customers.Update(customer);
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = "Customer updated successfully." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }

        public List<Customer> SearchCustomers(string keyword)
        {
            var query = _db.Customers.Where(c => !c.IsDeleted && c.CompanyId == _companyId);
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim();
                query = query.Where(c =>
                    c.FullName.Contains(kw) ||
                    c.ContactNumber.Contains(kw) ||
                    (c.Email != null && c.Email.Contains(kw)));
            }
            return query.OrderBy(c => c.Id).ToList();
        }

        public List<Customer> GetAllCustomers() =>
            _db.Customers.Where(c => !c.IsDeleted && c.CompanyId == _companyId).OrderBy(c => c.Id).ToList();

        public Customer? GetCustomer(int id) =>
            _db.Customers.FirstOrDefault(c => c.Id == id && !c.IsDeleted && c.CompanyId == _companyId);

        /// <summary>Soft-delete: keeps order/feedback/loyalty history intact, just hides the profile from active lists.</summary>
        public OperationResult DeleteCustomer(int id)
        {
            var customer = _db.Customers.FirstOrDefault(c => c.Id == id && !c.IsDeleted && c.CompanyId == _companyId);
            if (customer == null)
                return new OperationResult { Success = false, Message = "Customer not found." };

            customer.IsDeleted = true;
            customer.UpdatedAt = DateTime.UtcNow;

            try
            {
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = "Customer removed." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }

        private static OperationResult Validate(Customer c)
        {
            if (string.IsNullOrWhiteSpace(c.FullName))
                return new OperationResult { Success = false, Message = "Full name is required." };

            if (string.IsNullOrWhiteSpace(c.ContactNumber))
                return new OperationResult { Success = false, Message = "Contact number is required." };

            if (!Regex.IsMatch(c.ContactNumber, @"^[0-9+\-\s()]{7,20}$"))
                return new OperationResult { Success = false, Message = "Contact number format looks invalid." };

            if (!string.IsNullOrWhiteSpace(c.Email) &&
                !Regex.IsMatch(c.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return new OperationResult { Success = false, Message = "Email format looks invalid." };

            return new OperationResult { Success = true };
        }
    }
}
