using CRM.api.Controllers;
using CRM.domain.Entities.Tenant;
using CRM.domain.Enums;
using CRM.infrastructure.data;
using CleanWaveCRM.Desktop;

namespace CleanWaveCRM.Desktop.Forms
{
    /// <summary>
    /// Role/module matrix implemented here:
    ///
    ///   Super Admin  -> Create Admin/Owner accounts. Nothing else - it's
    ///                   a platform/bootstrap role, not a shop-floor one.
    ///   Admin/Owner  -> Dashboard, Reports, Inventory, Promotions, Manage
    ///                   Services, Manage Users, and Shop Profile.
    ///   Manager      -> Customer Profiles, Orders, Notifications,
    ///                   Segmentation, Feedback (can resolve/escalate), Loyalty.
    ///   Front Desk   -> Customer Profiles, Orders, Feedback (log only),
    ///                   Loyalty (view/redeem at checkout).
    ///
    /// Segmentation is additionally hidden when this shop hasn't availed
    /// Manager accounts (Shop Profile), since that's the role Segmentation
    /// duties are meant for.
    /// </summary>
    public class MainForm : Form
    {
        private readonly LaundryShopDbContext _db;
        private readonly LaundryShopDbContext _centralDb;
        private readonly IReadOnlyList<TenantDatabaseConfig> _tenantConfigs;
        private readonly ApplicationUser _currentUser;
        private readonly TenantRegistry? _tenantRegistration;
        private readonly Panel _contentPanel = new() { Dock = DockStyle.Fill, BackColor = UiTheme.Background };
        private readonly bool _managerAccountsAvailed;
        private Label? _branchIndicator;

        public MainForm(LaundryShopDbContext db, ApplicationUser currentUser, LaundryShopDbContext centralDb, IReadOnlyList<TenantDatabaseConfig> tenantConfigs, TenantRegistry? tenantRegistration = null)
        {
            _db = db;
            _centralDb = centralDb;
            _tenantConfigs = tenantConfigs;
            _currentUser = currentUser;
            _tenantRegistration = tenantRegistration;
            // Super Admin has no CompanyId/company at all - default harmlessly.
            _managerAccountsAvailed = currentUser.CompanyId.HasValue
                && (tenantRegistration?.AllowManagerAccounts ?? new SettingsController(db).GetMyCompany().AllowManagerAccounts);
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "CleanWave CRM";
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = UiTheme.Background;

            var sidebar = new Panel { Dock = DockStyle.Left, Width = 230, BackColor = UiTheme.PrimaryDarker };

            var lblWelcome = new Label
            {
                Text = $"{_currentUser.FullName}\n{_currentUser.Role}",
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                Height = 65,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(15, 0, 0, 0),
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            sidebar.Controls.Add(lblWelcome);

            if (_tenantRegistration?.TenantKey.Equals("tenant-c", StringComparison.OrdinalIgnoreCase) == true
                && _currentUser.CompanyId.HasValue)
            {
                _branchIndicator = new Label
                {
                    Text = GetCurrentBranchLabel(),
                    ForeColor = Color.White,
                    Dock = DockStyle.Top,
                    Height = 42,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(15, 0, 8, 0),
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    BackColor = UiTheme.PrimaryDark
                };
                sidebar.Controls.Add(_branchIndicator);
            }

            var menu = BuildMenuForRole(_currentUser.Role);
            foreach (var (label, handler) in menu)
            {
                var btn = new Button
                {
                    Text = label,
                    Dock = DockStyle.Top,
                    Height = 45,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = UiTheme.PrimaryDarker,
                    ForeColor = Color.White,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(18, 0, 0, 0),
                    Font = new Font("Segoe UI", 9.5f)
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.MouseOverBackColor = UiTheme.PrimaryDark;
                btn.Click += (_, _) => handler.Invoke();
                sidebar.Controls.Add(btn);
                sidebar.Controls.SetChildIndex(btn, 0);
            }

            var btnLogout = new Button
            {
                Text = "Log Out",
                Dock = DockStyle.Bottom,
                Height = 45,
                FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.Danger,
                ForeColor = Color.White
            };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.Click += (_, _) => Logout();
            sidebar.Controls.Add(btnLogout);

            Controls.Add(_contentPanel);
            Controls.Add(sidebar);

            // Admin/Owner lands on the Dashboard; everyone else lands on
            // the first module in their own menu (Customer Profiles).
            if (_currentUser.Role == UserRole.AdminOwner)
                OpenDashboardModule();
            else if (_currentUser.Role == UserRole.SuperAdmin)
                OpenSuperAdminModule();
            else
                OpenCustomerModule();
        }

        private List<(string Label, Action Handler)> BuildMenuForRole(UserRole role)
        {
            var items = new List<(string, Action)>();

            if (role == UserRole.SuperAdmin)
            {
                items.Add(("Tenant Management", OpenSuperAdminModule));
                items.Add(("Tenant Users", OpenSuperAdminUsersModule));
                items.Add(("Business Intelligence & Agreements", OpenSuperAdminAnalyticsModule));
                return items; // Super Admin's only job - nothing else applies.
            }

            if (role == UserRole.AdminOwner)
            {
                items.Add(("Dashboard", OpenDashboardModule));
                items.Add(("Promotions", OpenPromotionModule));
                items.Add(("Manage Services", OpenServicesModule));
                items.Add(("Inventory", OpenInventoryModule));
                items.Add(("Analytics & Reports", OpenReportsModule));
                items.Add(("Manage Users", OpenUserManagementModule));
                items.Add(("Shop Profile", OpenSystemSettingsModule));
                if (_tenantRegistration?.TenantKey.Equals("tenant-c", StringComparison.OrdinalIgnoreCase) == true)
                {
                    items.Add(("Switch Branch", SwitchBranch));
                    items.Add(("Manage Branches", OpenBranchManagementModule));
                }
                return items;
            }

            // Operational modules belong to Front Desk and Manager roles.
            items.Add(("Customer Profiles", OpenCustomerModule));
            items.Add(("Laundry Orders", OpenOrderModule));
            items.Add(("Feedback / Complaints", OpenFeedbackModule));
            items.Add(("Loyalty & Rewards", OpenLoyaltyModule));

            if (role == UserRole.Manager)
            {
                items.Add(("Notifications", OpenNotificationModule));

                // Hide segmentation if this shop hasn't availed Manager accounts.
                if (_managerAccountsAvailed)
                    items.Add(("Segmentation", OpenSegmentationModule));
            }

            return items;
        }

        private void OpenDashboardModule()
        {
            _contentPanel.Controls.Clear();
            var view = new DashboardView(_db) { Dock = DockStyle.Fill };
            _contentPanel.Controls.Add(view);
        }

        private void OpenCustomerModule()
        {
            _contentPanel.Controls.Clear();
            var view = new CustomerListView(_db) { Dock = DockStyle.Fill };
            _contentPanel.Controls.Add(view);
        }

        private void OpenOrderModule()
        {
            _contentPanel.Controls.Clear();
            var view = new OrderListView(_db) { Dock = DockStyle.Fill };
            _contentPanel.Controls.Add(view);
        }

        private void OpenNotificationModule()
        {
            _contentPanel.Controls.Clear();
            var view = new NotificationListView(_db) { Dock = DockStyle.Fill };
            _contentPanel.Controls.Add(view);
        }

        private void OpenFeedbackModule()
        {
            _contentPanel.Controls.Clear();
            var view = new FeedbackListView(_db) { Dock = DockStyle.Fill };
            _contentPanel.Controls.Add(view);
        }

        private void OpenLoyaltyModule()
        {
            _contentPanel.Controls.Clear();
            var view = new LoyaltyView(_db) { Dock = DockStyle.Fill };
            _contentPanel.Controls.Add(view);
        }

        private void OpenSegmentationModule()
        {
            _contentPanel.Controls.Clear();
            var view = new SegmentationView(_db) { Dock = DockStyle.Fill };
            _contentPanel.Controls.Add(view);
        }

        private void OpenPromotionModule()
        {
            _contentPanel.Controls.Clear();
            var view = new PromotionListView(_db) { Dock = DockStyle.Fill };
            _contentPanel.Controls.Add(view);
        }

        private void OpenServicesModule()
        {
            _contentPanel.Controls.Clear();
            var view = new ServiceTypeManagerView(_db) { Dock = DockStyle.Fill };
            _contentPanel.Controls.Add(view);
        }

        private void OpenInventoryModule()
        {
            _contentPanel.Controls.Clear();
            var view = new InventoryView(_db) { Dock = DockStyle.Fill };
            _contentPanel.Controls.Add(view);
        }

        private void OpenReportsModule()
        {
            _contentPanel.Controls.Clear();
            var view = new ReportsView(_db, _tenantRegistration?.TenantKey) { Dock = DockStyle.Fill };
            _contentPanel.Controls.Add(view);
        }

        private void OpenUserManagementModule()
        {
            _contentPanel.Controls.Clear();
            var view = new UserManagementView(_db) { Dock = DockStyle.Fill };
            _contentPanel.Controls.Add(view);
        }

        private void OpenSystemSettingsModule()
        {
            _contentPanel.Controls.Clear();
            var view = new SystemSettingsView(_db, platformManaged: _tenantRegistration != null) { Dock = DockStyle.Fill };
            _contentPanel.Controls.Add(view);
        }

        private void OpenBranchManagementModule()
        {
            if (_tenantRegistration?.TenantKey.Equals("tenant-c", StringComparison.OrdinalIgnoreCase) != true
                || !_currentUser.CompanyId.HasValue)
                return;
            _contentPanel.Controls.Clear();
            var view = new BranchManagementView(_db, _currentUser.CompanyId.Value, _tenantRegistration.MaxBranches) { Dock = DockStyle.Fill };
            _contentPanel.Controls.Add(view);
        }

        private void SwitchBranch()
        {
            if (_tenantRegistration?.TenantKey.Equals("tenant-c", StringComparison.OrdinalIgnoreCase) != true
                || !_currentUser.CompanyId.HasValue)
                return;

            var branches = _db.Companies
                .Where(c => c.IsActive && (c.Id == _currentUser.CompanyId.Value || c.ParentCompanyId == _currentUser.CompanyId.Value))
                .OrderBy(c => c.ParentCompanyId.HasValue)
                .ThenBy(c => c.ShopName)
                .ToList();
            if (branches.Count <= 1)
            {
                MessageBox.Show("Add a branch first, or continue using the main branch.", "Branch Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new Form { Text = "Switch Branch", Width = 380, Height = 155, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
            var picker = new ComboBox { Left = 15, Top = 20, Width = 330, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = nameof(Company.ShopName) };
            picker.Items.AddRange(branches.Cast<object>().ToArray());
            var activeId = AuthController.CurrentCompanyId;
            picker.SelectedIndex = Math.Max(0, branches.FindIndex(c => c.Id == activeId));
            var select = new Button { Text = "Switch", Left = 260, Top = 65, Width = 85, DialogResult = DialogResult.OK };
            dialog.Controls.AddRange(new Control[] { picker, select });
            dialog.AcceptButton = select;
            if (dialog.ShowDialog(this) != DialogResult.OK || picker.SelectedItem is not Company selected) return;

            if (!new AuthController(_db).SelectCompany(selected.Id))
            {
                MessageBox.Show("That branch could not be selected.", "Branch Selection", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (_branchIndicator != null)
                _branchIndicator.Text = GetCurrentBranchLabel();
            OpenDashboardModule();
        }

        private string GetCurrentBranchLabel()
        {
            var selectedCompanyId = AuthController.CurrentCompanyId ?? _currentUser.CompanyId;
            var company = selectedCompanyId.HasValue
                ? _db.Companies.FirstOrDefault(c => c.Id == selectedCompanyId.Value)
                : null;
            return $"Current branch: {company?.ShopName ?? "Unknown"}";
        }

        private void OpenSuperAdminModule()
        {
            _contentPanel.Controls.Clear();
            var view = new SuperAdminHomeView(_db) { Dock = DockStyle.Fill };
            _contentPanel.Controls.Add(view);
        }

        private void OpenSuperAdminUsersModule()
        {
            _contentPanel.Controls.Clear();
            var view = new SuperAdminUserManagementView(_centralDb, _tenantConfigs) { Dock = DockStyle.Fill };
            _contentPanel.Controls.Add(view);
        }

        private void OpenSuperAdminAnalyticsModule()
        {
            _contentPanel.Controls.Clear();
            var view = new SuperAdminAnalyticsView(_centralDb, _tenantConfigs) { Dock = DockStyle.Fill };
            _contentPanel.Controls.Add(view);
        }

        private void Logout()
        {
            new AuthController(_db).Logout();
            if (!ReferenceEquals(_db, _centralDb)) _db.Dispose();
            var login = new LoginForm(_centralDb, _tenantConfigs);
            login.FormClosed += (_, _) => Application.Exit();
            Hide();
            login.Show();
        }
    }
}
