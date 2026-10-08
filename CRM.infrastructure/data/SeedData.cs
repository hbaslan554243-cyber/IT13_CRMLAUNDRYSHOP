using CRM.domain.Entities.Tenant;
using CRM.domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CRM.infrastructure.data
{
    /// <summary>
    /// Ensures the database exists and has an initial Super Admin login.
    /// Called once from Desktop's Program.cs on startup. Shop-specific data
    /// (Company, default services/inventory) is created per-Owner when
    /// their account is provisioned - see AuthController.CreateUser and
    /// TenantDefaults.SeedFor.
    /// </summary>
    public static class SeedData
    {
        public static void EnsureTenantSchemaColumns(LaundryShopDbContext db)
        {
            db.Database.EnsureCreated();
            db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID('dbo.Companies', 'U') IS NOT NULL AND COL_LENGTH('dbo.Companies', 'IsActive') IS NULL
    ALTER TABLE dbo.Companies ADD IsActive bit NOT NULL CONSTRAINT DF_Companies_IsActive DEFAULT (1);
IF OBJECT_ID('dbo.Companies', 'U') IS NOT NULL AND COL_LENGTH('dbo.Companies', 'SubscriptionExpiresAt') IS NULL
    ALTER TABLE dbo.Companies ADD SubscriptionExpiresAt datetime2 NULL;
IF OBJECT_ID('dbo.Companies', 'U') IS NOT NULL AND COL_LENGTH('dbo.Companies', 'ParentCompanyId') IS NULL
    ALTER TABLE dbo.Companies ADD ParentCompanyId int NULL;");
        }

        public static void EnsureTenantAccountSeeded(LaundryShopDbContext db, string email, string password, string fullName)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();
            var strategy = db.Database.CreateExecutionStrategy();
            strategy.Execute(() =>
            {
                using var transaction = db.Database.BeginTransaction();
                if (db.Users.Any(user => user.NormalizedEmail == normalizedEmail))
                {
                    transaction.Commit();
                    return;
                }

                var user = new ApplicationUser
                {
                    UserName = email.Trim(),
                    NormalizedUserName = normalizedEmail,
                    Email = email.Trim(),
                    NormalizedEmail = normalizedEmail,
                    EmailConfirmed = true,
                    FullName = fullName,
                    Role = UserRole.AdminOwner,
                    IsActive = true,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    ConcurrencyStamp = Guid.NewGuid().ToString()
                };
                user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, password);

                db.Users.Add(user);
                db.SaveChanges();

                var company = new Company
                {
                    OwnerUserId = user.Id,
                    ShopName = fullName.EndsWith("Owner", StringComparison.OrdinalIgnoreCase)
                        ? $"{fullName[..^5].Trim()}'s Laundry Shop"
                        : $"{fullName}'s Laundry Shop"
                };
                db.Companies.Add(company);
                db.SaveChanges();

                user.CompanyId = company.Id;
                db.SaveChanges();
                TenantDefaults.SeedFor(db, company.Id);
                transaction.Commit();
            });
        }

        public static void EnsureSeeded(LaundryShopDbContext db, string superAdminEmail, string superAdminPassword, string superAdminFullName)
        {
            db.Database.EnsureCreated();

            // EnsureCreated does not update existing databases. Add the
            // platform subscription columns safely for existing MonsterASP SQL databases.
            db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID('dbo.Companies', 'U') IS NOT NULL AND COL_LENGTH('dbo.Companies', 'IsActive') IS NULL
    ALTER TABLE dbo.Companies ADD IsActive bit NOT NULL CONSTRAINT DF_Companies_IsActive DEFAULT (1);
IF OBJECT_ID('dbo.Companies', 'U') IS NOT NULL AND COL_LENGTH('dbo.Companies', 'SubscriptionExpiresAt') IS NULL
    ALTER TABLE dbo.Companies ADD SubscriptionExpiresAt datetime2 NULL;
IF OBJECT_ID('dbo.Companies', 'U') IS NOT NULL AND COL_LENGTH('dbo.Companies', 'ParentCompanyId') IS NULL
    ALTER TABLE dbo.Companies ADD ParentCompanyId int NULL;");

            // Existing central databases may predate the tenant registry.
            db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID('dbo.TenantRegistries', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantRegistries (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TenantRegistries PRIMARY KEY,
        TenantKey nvarchar(50) NOT NULL,
        DisplayName nvarchar(100) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_TenantRegistries_IsActive DEFAULT (0),
        SubscriptionExpiresAt datetime2 NULL,
        AllowStaffAccounts bit NOT NULL CONSTRAINT DF_TenantRegistries_AllowStaff DEFAULT (1),
        AllowManagerAccounts bit NOT NULL CONSTRAINT DF_TenantRegistries_AllowManager DEFAULT (1),
        TermsAndConditions nvarchar(max) NULL,
        TermsVersion int NOT NULL CONSTRAINT DF_TenantRegistries_TermsVersion DEFAULT (1),
        MaxBranches int NOT NULL CONSTRAINT DF_TenantRegistries_MaxBranches DEFAULT (1)
    );
    CREATE UNIQUE INDEX IX_TenantRegistries_TenantKey ON dbo.TenantRegistries(TenantKey);
END");

            db.Database.ExecuteSqlRaw(@"
IF COL_LENGTH('dbo.TenantRegistries', 'AllowStaffAccounts') IS NULL
    ALTER TABLE dbo.TenantRegistries ADD AllowStaffAccounts bit NOT NULL CONSTRAINT DF_TenantRegistries_AllowStaff DEFAULT (1);
IF COL_LENGTH('dbo.TenantRegistries', 'AllowManagerAccounts') IS NULL
    ALTER TABLE dbo.TenantRegistries ADD AllowManagerAccounts bit NOT NULL CONSTRAINT DF_TenantRegistries_AllowManager DEFAULT (1);
IF COL_LENGTH('dbo.TenantRegistries', 'TermsAndConditions') IS NULL
    ALTER TABLE dbo.TenantRegistries ADD TermsAndConditions nvarchar(max) NULL;
IF COL_LENGTH('dbo.TenantRegistries', 'TermsVersion') IS NULL
    ALTER TABLE dbo.TenantRegistries ADD TermsVersion int NOT NULL CONSTRAINT DF_TenantRegistries_TermsVersion DEFAULT (1);
IF COL_LENGTH('dbo.TenantRegistries', 'MaxBranches') IS NULL
    ALTER TABLE dbo.TenantRegistries ADD MaxBranches int NOT NULL CONSTRAINT DF_TenantRegistries_MaxBranches DEFAULT (1);");

            var knownTenants = db.TenantRegistries.Select(t => t.TenantKey).ToHashSet();
            foreach (var (key, name) in new[] { ("tenant-a", "Tenant A"), ("tenant-b", "Tenant B"), ("tenant-c", "Tenant C") })
            {
                if (!knownTenants.Contains(key))
                    db.TenantRegistries.Add(new CRM.domain.Entities.Tenant.TenantRegistry { TenantKey = key, DisplayName = name, IsActive = false });
            }
            db.SaveChanges();

            if (!db.Users.Any())
            {
                var hasher = new PasswordHasher<ApplicationUser>();
                var normalized = superAdminEmail.Trim().ToUpperInvariant();

                var admin = new ApplicationUser
                {
                    UserName = superAdminEmail.Trim(),
                    NormalizedUserName = normalized,
                    Email = superAdminEmail.Trim(),
                    NormalizedEmail = normalized,
                    EmailConfirmed = true,
                    FullName = superAdminFullName,
                    Role = UserRole.SuperAdmin,
                    IsActive = true,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    ConcurrencyStamp = Guid.NewGuid().ToString()
                };
                admin.PasswordHash = hasher.HashPassword(admin, superAdminPassword);

                db.Users.Add(admin);
                db.SaveChanges();
            }
        }
    }
}
