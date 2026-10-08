using CRM.api.Controllers;
using CRM.domain.Entities.Tenant;
using CRM.infrastructure.data;
using CleanWaveCRM.Desktop;

namespace CleanWaveCRM.Desktop.Forms
{
    public class LoginForm : Form
    {
        private readonly LaundryShopDbContext _db;
        private readonly IReadOnlyList<TenantDatabaseConfig> _tenantConfigs;
        private readonly AuthController _authController;

        private TextBox _txtEmail = new();
        private TextBox _txtPassword = new();
        private Label _lblError = new();

        public LoginForm(LaundryShopDbContext db, IReadOnlyList<TenantDatabaseConfig> tenantConfigs)
        {
            _db = db;
            _tenantConfigs = tenantConfigs;
            _authController = new AuthController(_db);
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "CleanWave CRM - Login";
            Width = 420;
            Height = 500;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = UiTheme.Background;

            var picLogo = new PictureBox
            {
                Left = 135,
                Top = 25,
                Width = 130,
                Height = 130,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
            if (File.Exists(logoPath))
                picLogo.Image = Image.FromFile(logoPath);

            var lblTitle = new Label
            {
                Text = "CleanWave Laundry Shop",
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                ForeColor = UiTheme.PrimaryDarker,
                TextAlign = ContentAlignment.MiddleCenter,
                Left = 20,
                Top = 165,
                Width = 380
            };

            var lblSubtitle = new Label
            {
                Text = "Sign in to continue",
                ForeColor = UiTheme.TextMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                Left = 20,
                Top = 195,
                Width = 380
            };

            var lblEmail = new Label { Text = "Email", Left = 30, Top = 235, AutoSize = true, ForeColor = UiTheme.TextDark };
            _txtEmail = new TextBox { Left = 30, Top = 255, Width = 340 };

            var lblPassword = new Label { Text = "Password", Left = 30, Top = 290, AutoSize = true, ForeColor = UiTheme.TextDark };
            _txtPassword = new TextBox { Left = 30, Top = 310, Width = 340, PasswordChar = '*' };

            _lblError = new Label { Left = 30, Top = 340, Width = 340, Height = 45, ForeColor = UiTheme.Danger, AutoEllipsis = false };

            var btnLogin = new Button
            {
                Text = "Log In",
                Left = 30,
                Top = 390,
                Width = 340,
                Height = 40,
                BackColor = UiTheme.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.Click += BtnLogin_Click;

            AcceptButton = btnLogin;

            Controls.AddRange(new Control[]
            {
                picLogo, lblTitle, lblSubtitle, lblEmail, _txtEmail,
                lblPassword, _txtPassword, _lblError, btnLogin
            });
        }

        private void BtnLogin_Click(object? sender, EventArgs e)
        {
            var result = _authController.Login(_txtEmail.Text, _txtPassword.Text);
            LaundryShopDbContext? authenticatedDb = null;
            TenantRegistry? authenticatedRegistration = null;
            if (result.Success && result.User?.Role == CRM.domain.Enums.UserRole.SuperAdmin)
            {
                authenticatedDb = _db;
            }
            else
            {
                _authController.Logout();
                var registrations = _db.TenantRegistries.ToDictionary(t => t.TenantKey);
                var activeTenantFound = false;
                var tenantConnectionErrors = new List<string>();
                var tenantAuthMessage = "Invalid email or password.";
                foreach (var tenant in _tenantConfigs)
                {
                    if (!registrations.TryGetValue(tenant.Key, out var registration)
                        || !registration.IsActive
                        || registration.SubscriptionExpiresAt.HasValue && registration.SubscriptionExpiresAt.Value.Date < DateTime.UtcNow.Date)
                        continue;

                    activeTenantFound = true;
                    var tenantDb = CleanWaveCRM.Desktop.Program.CreateDbContext(tenant.ConnectionString);
                    try
                    {
                        SeedData.EnsureTenantSchemaColumns(tenantDb);
                        var tenantAuth = new AuthController(tenantDb);
                        var tenantResult = tenantAuth.Login(_txtEmail.Text, _txtPassword.Text);
                        if (tenantResult.Success && tenantResult.User?.Role != CRM.domain.Enums.UserRole.SuperAdmin)
                        {
                            var roleUnavailable = tenantResult.User?.Role == CRM.domain.Enums.UserRole.FrontDeskStaff && !registration.AllowStaffAccounts
                                || tenantResult.User?.Role == CRM.domain.Enums.UserRole.Manager && !registration.AllowManagerAccounts;
                            if (roleUnavailable)
                            {
                                tenantAuth.Logout();
                                tenantAuthMessage = "This role is not included in the tenant's current plan. Contact the Super Admin.";
                                tenantDb.Dispose();
                                continue;
                            }

                            SyncPlatformTerms(tenantDb, registration, tenantResult.User?.CompanyId);
                            authenticatedDb = tenantDb;
                            authenticatedRegistration = registration;
                            result = tenantResult;
                            break;
                        }
                        if (!tenantResult.Success)
                        {
                            if (tenantResult.Message.StartsWith("Could not reach the database:", StringComparison.OrdinalIgnoreCase))
                                tenantConnectionErrors.Add($"{tenant.Name}: {tenantResult.Message}");
                            else
                                tenantAuthMessage = tenantResult.Message;
                        }
                        tenantAuth.Logout();
                    }
                    catch (Exception ex)
                    {
                        tenantConnectionErrors.Add($"{tenant.Name}: {ex.Message}");
                    }

                    tenantDb.Dispose();
                }

                if (authenticatedDb == null)
                {
                    if (tenantConnectionErrors.Count > 0)
                    {
                        _lblError.Text = "Could not connect to one or more active tenant databases. See details.";
                        MessageBox.Show(
                            "Tenant database connection errors:\n\n" + string.Join("\n\n", tenantConnectionErrors),
                            "Tenant Sign-in Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        _lblError.Text = activeTenantFound
                            ? tenantAuthMessage
                            : "No tenant is currently active. Contact the Super Admin.";
                    }
                    return;
                }
            }

            var main = new MainForm(authenticatedDb!, result.User!, _db, _tenantConfigs, authenticatedRegistration);
            main.FormClosed += (_, _) =>
            {
                if (!ReferenceEquals(authenticatedDb, _db)) authenticatedDb!.Dispose();
                _db.Dispose();
                Close();
            };
            Hide();
            main.Show();
        }

        private static void SyncPlatformTerms(LaundryShopDbContext tenantDb, TenantRegistry registration, int? ownerCompanyId)
        {
            if (!ownerCompanyId.HasValue) return;
            var companies = tenantDb.Companies
                .Where(c => c.Id == ownerCompanyId.Value || c.ParentCompanyId == ownerCompanyId.Value)
                .ToList();
            var changed = false;
            foreach (var company in companies)
            {
                if (company.AllowStaffAccounts == registration.AllowStaffAccounts
                    && company.AllowManagerAccounts == registration.AllowManagerAccounts
                    && company.TermsAndConditions == registration.TermsAndConditions)
                    continue;
                company.AllowStaffAccounts = registration.AllowStaffAccounts;
                company.AllowManagerAccounts = registration.AllowManagerAccounts;
                company.TermsAndConditions = registration.TermsAndConditions;
                changed = true;
            }
            if (!changed) return;
            tenantDb.SaveChanges();
        }
    }
}
