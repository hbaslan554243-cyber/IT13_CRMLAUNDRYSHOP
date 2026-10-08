using System.Text.Json;
using CRM.infrastructure.data;
using CleanWaveCRM.Desktop.Forms;
using Microsoft.EntityFrameworkCore;

namespace CleanWaveCRM.Desktop
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Without these, an unhandled exception anywhere in the UI
            // (e.g. a chart rendering bug) silently kills the whole app
            // instead of showing what broke. Wire them up first, before
            // anything else can throw.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, e) =>
                MessageBox.Show("An unexpected error occurred:\n\n" + e.Exception,
                    "Unhandled Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                MessageBox.Show("A fatal error occurred:\n\n" + e.ExceptionObject,
                    "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            LaundryShopDbContext db;
            var tenantSeedWarnings = new List<string>();
            try
            {
                var config = LoadConfig();
                var connectionString = config.RootElement
                    .GetProperty("ConnectionStrings")
                    .GetProperty("DefaultConnection")
                    .GetString()!;

                var connectionStrings = config.RootElement.GetProperty("ConnectionStrings");
                var tenantConfigs = config.RootElement.GetProperty("Tenants").EnumerateArray()
                    .Select(item => new TenantDatabaseConfig(
                        item.GetProperty("Key").GetString()!,
                        item.GetProperty("Name").GetString()!,
                        connectionStrings.GetProperty(item.GetProperty("ConnectionName").GetString()!).GetString()!,
                        item.GetProperty("SeedAccount").GetProperty("Email").GetString()!,
                        item.GetProperty("SeedAccount").GetProperty("Password").GetString()!,
                        item.GetProperty("SeedAccount").GetProperty("FullName").GetString()!))
                    .ToList();

                var seedSection = config.RootElement.GetProperty("SeedSuperAdmin");
                var seedEmail = seedSection.GetProperty("Email").GetString()!;
                var seedPassword = seedSection.GetProperty("Password").GetString()!;
                var seedFullName = seedSection.GetProperty("FullName").GetString()!;

                db = CreateDbContext(connectionString);
                SeedData.EnsureSeeded(db, seedEmail, seedPassword, seedFullName);
                foreach (var tenant in tenantConfigs)
                {
                    using var tenantDb = CreateDbContext(tenant.ConnectionString);
                    try
                    {
                        SeedData.EnsureTenantSchemaColumns(tenantDb);
                        SeedData.EnsureTenantAccountSeeded(tenantDb, tenant.SeedEmail, tenant.SeedPassword, tenant.SeedFullName);
                        var tenantOwner = tenantDb.Users.SingleOrDefault(u => u.NormalizedEmail == tenant.SeedEmail.Trim().ToUpperInvariant());
                        if (tenantOwner?.CompanyId is not int companyId)
                            throw new InvalidOperationException($"The configured owner account for {tenant.Name} has no shop assigned.");

                        DemoDataSeeder.SeedIfNoOrders(tenantDb, companyId, orderCount: 120);

                        // Tenant C branches have their own CompanyId data scope.
                        // Seed each active branch once so its reports and BI
                        // dashboards have their own customers, orders, and payments.
                        if (tenant.Key.Equals("tenant-c", StringComparison.OrdinalIgnoreCase))
                        {
                            var activeBranches = tenantDb.Companies
                                .Where(c => c.ParentCompanyId == companyId && c.IsActive)
                                .OrderBy(c => c.Id)
                                .ToList();

                            foreach (var branch in activeBranches)
                            {
                                try
                                {
                                    DemoDataSeeder.SeedIfNoOrders(tenantDb, branch.Id, orderCount: 120);
                                }
                                catch (Exception ex)
                                {
                                    tenantSeedWarnings.Add($"{tenant.Name} branch '{branch.ShopName}': {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        tenantSeedWarnings.Add($"{tenant.Name}: {ex.Message}");
                    }
                }
                _tenantConfigs = tenantConfigs;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Startup failed. This could be a missing/invalid appsettings.json, or a database connection problem.\n\n" + ex,
                    "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (tenantSeedWarnings.Count > 0)
            {
                MessageBox.Show(
                    "Some tenant databases could not be initialized or seeded. Check the tenant database connections and try again:\n\n" + string.Join("\n\n", tenantSeedWarnings),
                    "Tenant Setup Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Run with --seed-demo --company=1 (optionally --count=500) to
            // generate realistic demo customers/orders/payments for ONE
            // shop, for testing reports, segmentation, and the dashboard.
            // Find the company id from the "Shops & Owner Accounts" screen
            // (Super Admin) or the Company table in SSMS. Exits without
            // opening the UI.
            if (args.Contains("--seed-demo"))
            {
                var companyArg = args.FirstOrDefault(a => a.StartsWith("--company="));
                if (companyArg == null || !int.TryParse(companyArg.Split('=')[1], out var companyId))
                {
                    MessageBox.Show(
                        "Missing --company=<id>. Example: CleanWaveCRM.Desktop.exe --seed-demo --company=1\n\n" +
                        "Find the company id by logging in as that shop's Owner once first, or checking the Companies table in SSMS.",
                        "Demo Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var count = 200;
                var countArg = args.FirstOrDefault(a => a.StartsWith("--count="));
                if (countArg != null && int.TryParse(countArg.Split('=')[1], out var parsed))
                    count = parsed;

                try
                {
                    DemoDataSeeder.Seed(db, companyId, count);
                    MessageBox.Show($"Seeded {count} demo orders (plus customers/payments to match) for company {companyId}.",
                        "Demo Data Ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Demo data seeding failed:\n\n" + ex, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            Application.Run(new LoginForm(db, _tenantConfigs));
        }

        private static IReadOnlyList<TenantDatabaseConfig> _tenantConfigs = Array.Empty<TenantDatabaseConfig>();

        internal static LaundryShopDbContext CreateDbContext(string connectionString)
        {
            var options = new DbContextOptionsBuilder<LaundryShopDbContext>()
                .UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                    sqlOptions.CommandTimeout(60);
                })
                .Options;
            return new LaundryShopDbContext(options);
        }

        private static JsonDocument LoadConfig()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var json = File.ReadAllText(path);
            return JsonDocument.Parse(json);
        }
    }
}
