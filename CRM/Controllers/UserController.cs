using CRM.domain.Entities.Tenant;
using CRM.domain.Enums;
using CRM.infrastructure.data;
using Microsoft.AspNetCore.Identity;

namespace CRM.api.Controllers
{
    /// <summary>
    /// User & Role Management (Feature 10) - Super Admin only. Account
    /// creation lives in AuthController.CreateUser; this handles ongoing
    /// maintenance: listing, activating/deactivating, and role changes.
    /// </summary>
    public class UserController
    {
        private readonly LaundryShopDbContext _db;
        private readonly PasswordHasher<ApplicationUser> _hasher = new();

        public UserController(LaundryShopDbContext db)
        {
            _db = db;
        }

        /// <summary>Super Admin only: every account across every shop.</summary>
        public List<ApplicationUser> GetAll() => _db.Users.OrderBy(u => u.FullName).ToList();

        /// <summary>Admin/Owner's own staff list - only accounts belonging to their shop.</summary>
        public List<ApplicationUser> GetForMyCompany()
        {
            var companyId = AuthController.CurrentCompanyId;
            return _db.Users.Where(u => u.CompanyId == companyId).OrderBy(u => u.FullName).ToList();
        }

        public OperationResult SetActive(string userId, bool isActive)
        {
            var user = _db.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return new OperationResult { Success = false, Message = "User not found." };

            user.IsActive = isActive;
            try
            {
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = isActive ? "User activated." : "User deactivated." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }

        public OperationResult ResetPassword(string userId, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
                return new OperationResult { Success = false, Message = "Password must be at least 8 characters." };

            var user = _db.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return new OperationResult { Success = false, Message = "User not found." };

            user.PasswordHash = _hasher.HashPassword(user, newPassword);
            user.SecurityStamp = Guid.NewGuid().ToString();
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
            try
            {
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = "Password reset." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }

        public OperationResult ChangeRole(string userId, UserRole newRole)
        {
            var user = _db.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return new OperationResult { Success = false, Message = "User not found." };

            user.Role = newRole;
            try
            {
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = "Role updated." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }

        public OperationResult UpdateStaffProfile(string userId, string fullName, string email, UserRole role)
        {
            var companyId = AuthController.CurrentCompanyId;
            if (companyId == null || role is not (UserRole.FrontDeskStaff or UserRole.Manager))
                return new OperationResult { Success = false, Message = "Choose a valid staff or manager role." };

            var user = _db.Users.FirstOrDefault(u => u.Id == userId && u.CompanyId == companyId
                && (u.Role == UserRole.FrontDeskStaff || u.Role == UserRole.Manager));
            if (user == null) return new OperationResult { Success = false, Message = "Branch user not found." };
            var company = _db.Companies.FirstOrDefault(c => c.Id == companyId.Value);
            if (company == null || role == UserRole.FrontDeskStaff && !company.AllowStaffAccounts
                || role == UserRole.Manager && !company.AllowManagerAccounts)
                return new OperationResult { Success = false, Message = "That role is not included in this tenant's plan." };
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
                return new OperationResult { Success = false, Message = "Name and email are required." };

            var normalizedEmail = email.Trim().ToUpperInvariant();
            if (_db.Users.Any(u => u.Id != userId && u.NormalizedEmail == normalizedEmail))
                return new OperationResult { Success = false, Message = "That email is already used by another account." };

            user.FullName = fullName.Trim();
            user.Email = email.Trim();
            user.NormalizedEmail = normalizedEmail;
            user.UserName = email.Trim();
            user.NormalizedUserName = normalizedEmail;
            user.Role = role;
            try
            {
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = "User details updated." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }
    }
}
