using CRM.domain.Entities.Tenant;
using CRM.domain.Enums;
using CRM.infrastructure.data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers
{
    public class AuthResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
    }

    /// <summary>
    /// Business-logic controller for authentication. This is NOT an ASP.NET
    /// [ApiController] HTTP endpoint - it's a plain C# class that the
    /// CleanWaveCRM.Desktop Forms call directly (in-process, no HTTP hop),
    /// keeping the MVC "Controller" role: sits between Views (Forms) and
    /// the database (LaundryShopDbContext).
    ///
    /// Uses ASP.NET Core Identity's PasswordHasher directly (PBKDF2-based,
    /// the same algorithm real ASP.NET Identity uses) without needing the
    /// full UserManager/SignInManager pipeline, which requires a web host.
    /// </summary>
    public class AuthController
    {
        private readonly LaundryShopDbContext _db;
        private readonly PasswordHasher<ApplicationUser> _hasher = new();

        public AuthController(LaundryShopDbContext db)
        {
            _db = db;
        }

        /// <summary>Holds the currently logged-in user for the rest of the app session.</summary>
        public static ApplicationUser? CurrentUser { get; private set; }
        private static int? _selectedCompanyId;

        /// <summary>The selected branch/company boundary for the current session.</summary>
        public static int? CurrentCompanyId => CurrentUser == null ? null : _selectedCompanyId ?? CurrentUser.CompanyId;

        public AuthResult Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return new AuthResult { Success = false, Message = "Please enter both email and password." };

            ApplicationUser? user;
            try
            {
                var normalized = email.Trim().ToUpperInvariant();
                user = _db.Users.SingleOrDefault(u => u.NormalizedEmail == normalized);
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, Message = $"Could not reach the database: {ex.Message}" };
            }

            if (user == null)
                return new AuthResult { Success = false, Message = "Invalid email or password." };

            if (!user.IsActive)
                return new AuthResult { Success = false, Message = "This account has been deactivated. Contact the Super Admin." };

            if (user.CompanyId.HasValue && !_db.Companies.Any(c => c.Id == user.CompanyId.Value && c.IsActive))
                return new AuthResult { Success = false, Message = "This branch is archived. Contact the tenant Admin." };

            var verifyResult = _hasher.VerifyHashedPassword(user, user.PasswordHash ?? string.Empty, password);
            if (verifyResult == PasswordVerificationResult.Failed)
                return new AuthResult { Success = false, Message = "Invalid email or password." };

            user.LastLoginAt = DateTime.UtcNow;
            _db.SaveChanges();
            CurrentUser = user;
            _selectedCompanyId = user.CompanyId;

            return new AuthResult { Success = true, Message = "Login successful.", User = user };
        }

        public bool SelectCompany(int companyId)
        {
            if (CurrentUser?.Role != UserRole.AdminOwner || !CurrentUser.CompanyId.HasValue)
                return false;

            var rootCompany = _db.Companies.FirstOrDefault(c => c.Id == CurrentUser.CompanyId.Value);
            if (rootCompany == null) return false;
            var selectedCompany = _db.Companies.FirstOrDefault(c => c.Id == companyId
                && (c.Id == rootCompany.Id || c.ParentCompanyId == rootCompany.Id)
                && c.OwnerUserId == rootCompany.OwnerUserId);
            if (selectedCompany == null) return false;

            _selectedCompanyId = selectedCompany.Id;
            return true;
        }

        public void Logout()
        {
            CurrentUser = null;
            _selectedCompanyId = null;
        }

        /// <summary>
        /// Creates a staff account. Behavior depends on role:
        /// - AdminOwner: also creates a brand-new Company (this is how a
        ///   new independent shop is provisioned - see SuperAdminHomeView).
        /// - FrontDeskStaff / Manager: inherits CompanyId from whichever
        ///   Admin/Owner is currently logged in and creating them.
        /// - SuperAdmin: no company (platform-level account).
        /// </summary>
        public AuthResult CreateUser(string email, string password, string fullName, UserRole role, string? tenantName = null)
        {
            var normalized = email.Trim().ToUpperInvariant();
            if (_db.Users.Any(u => u.NormalizedEmail == normalized))
                return new AuthResult { Success = false, Message = "An account with this email already exists." };

            var user = new ApplicationUser
            {
                UserName = email.Trim(),
                NormalizedUserName = normalized,
                Email = email.Trim(),
                NormalizedEmail = normalized,
                EmailConfirmed = true,
                FullName = fullName,
                Role = role,
                IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString()
            };
            user.PasswordHash = _hasher.HashPassword(user, password);

            if (role is UserRole.FrontDeskStaff or UserRole.Manager)
            {
                // Inherit the shop from whichever Admin/Owner is creating this account.
                var companyId = CurrentCompanyId;
                var company = companyId.HasValue ? _db.Companies.FirstOrDefault(c => c.Id == companyId.Value) : null;
                if (company == null)
                    return new AuthResult { Success = false, Message = "Select a valid branch before creating staff." };
                if (role == UserRole.FrontDeskStaff && !company.AllowStaffAccounts
                    || role == UserRole.Manager && !company.AllowManagerAccounts)
                    return new AuthResult { Success = false, Message = "That role is not included in this tenant's plan." };
                user.CompanyId = company.Id;
            }

            _db.Users.Add(user);
            _db.SaveChanges();

            if (role == UserRole.AdminOwner)
            {
                // Provisioning a brand-new independent shop for this Owner.
                var company = new Company
                {
                    OwnerUserId = user.Id,
                    ShopName = string.IsNullOrWhiteSpace(tenantName) ? $"{fullName}'s Laundry Shop" : tenantName.Trim()
                };
                _db.Companies.Add(company);
                _db.SaveChanges();

                user.CompanyId = company.Id;
                _db.SaveChanges();

                TenantDefaults.SeedFor(_db, company.Id);
            }

            return new AuthResult { Success = true, Message = "User created.", User = user };
        }

        public List<ApplicationUser> GetAllUsers() => _db.Users.OrderBy(u => u.FullName).ToList();
    }
}
