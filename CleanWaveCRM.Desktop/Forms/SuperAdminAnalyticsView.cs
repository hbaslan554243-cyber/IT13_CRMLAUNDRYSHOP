using System.Windows.Forms.DataVisualization.Charting;
using CRM.domain.Enums;
using CRM.infrastructure.data;
using CleanWaveCRM.Desktop;

namespace CleanWaveCRM.Desktop.Forms;

public sealed class SuperAdminAnalyticsView : UserControl
{
    private sealed record TenantSnapshot(string Tenant, decimal Revenue, int Orders, int Customers, string ConnectionStatus);

    private readonly LaundryShopDbContext _centralDb;
    private readonly IReadOnlyList<TenantDatabaseConfig> _tenants;
    private readonly ComboBox _tenantPicker = new();
    private readonly CheckBox _allowStaff = new();
    private readonly CheckBox _allowManager = new();
    private readonly NumericUpDown _maxBranches = new();
    private readonly TextBox _terms = new();
    private readonly Label _planMessage = new();
    private readonly Label _revenueValue = new();
    private readonly Label _ordersValue = new();
    private readonly Label _customersValue = new();
    private readonly Label _activeTenantsValue = new();
    private readonly DataGridView _comparisonGrid = new();
    private readonly Chart _revenueChart = new();
    private readonly Chart _ordersChart = new();

    public SuperAdminAnalyticsView(LaundryShopDbContext centralDb, IReadOnlyList<TenantDatabaseConfig> tenants)
    {
        _centralDb = centralDb;
        _tenants = tenants;
        BuildUi();
        LoadPlanChoices();
        RefreshAnalytics();
    }

    private void BuildUi()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 82, Padding = new Padding(12) };
        top.Controls.Add(CreateMetric("Combined Revenue", _revenueValue, 10));
        top.Controls.Add(CreateMetric("Orders", _ordersValue, 220));
        top.Controls.Add(CreateMetric("Customers", _customersValue, 430));
        top.Controls.Add(CreateMetric("Active Tenants", _activeTenantsValue, 640));
        var refresh = new Button { Text = "Refresh", Left = 860, Top = 18, Width = 100, Height = 30 };
        refresh.Click += (_, _) => RefreshAnalytics();
        top.Controls.Add(refresh);

        var charts = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(8) };
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        ConfigureChart(_revenueChart, "Revenue by Tenant", SeriesChartType.Column, "Revenue");
        ConfigureChart(_ordersChart, "Orders by Tenant", SeriesChartType.Column, "Orders");
        charts.Controls.Add(_revenueChart, 0, 0);
        charts.Controls.Add(_ordersChart, 1, 0);

        _comparisonGrid.Dock = DockStyle.Top;
        _comparisonGrid.Height = 105;
        _comparisonGrid.ReadOnly = true;
        _comparisonGrid.AllowUserToAddRows = false;
        _comparisonGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        var agreement = new GroupBox { Text = "Tenant Plan & Agreement", Dock = DockStyle.Bottom, Height = 188, Padding = new Padding(10) };
        var note = new Label { Text = "These settings are reflected in the tenant Admin's Shop Profile at their next login.", Left = 12, Top = 20, Width = 880, Height = 22, ForeColor = Color.DimGray };
        _tenantPicker.Left = 12; _tenantPicker.Top = 47; _tenantPicker.Width = 180;
        _tenantPicker.DropDownStyle = ComboBoxStyle.DropDownList;
        _tenantPicker.DisplayMember = nameof(TenantDatabaseConfig.Name);
        _tenantPicker.Items.AddRange(_tenants.Cast<object>().ToArray());
        _tenantPicker.SelectedIndexChanged += (_, _) => LoadSelectedPlan();

        _allowStaff.Text = "Front Desk Staff accounts";
        _allowStaff.Left = 210; _allowStaff.Top = 49; _allowStaff.AutoSize = true;
        _allowManager.Text = "Manager accounts";
        _allowManager.Left = 420; _allowManager.Top = 49; _allowManager.AutoSize = true;
        var branchLimitLabel = new Label { Text = "Tenant C branch limit (including main branch)", Left = 620, Top = 50, Width = 250, Height = 20 };
        _maxBranches.Left = 875; _maxBranches.Top = 46; _maxBranches.Width = 65; _maxBranches.Minimum = 1; _maxBranches.Maximum = 50;

        _terms.Left = 12; _terms.Top = 78; _terms.Width = 760; _terms.Height = 72; _terms.Multiline = true; _terms.ScrollBars = ScrollBars.Vertical;
        var savePlan = new Button { Text = "Save Plan & Agreement", Left = 785, Top = 78, Width = 160, Height = 34 };
        savePlan.Click += (_, _) => SaveSelectedPlan();
        _planMessage.Left = 785; _planMessage.Top = 120; _planMessage.Width = 180; _planMessage.Height = 42;
        agreement.Controls.AddRange(new Control[] { note, _tenantPicker, _allowStaff, _allowManager, branchLimitLabel, _maxBranches, _terms, savePlan, _planMessage });

        var center = new Panel { Dock = DockStyle.Fill };
        center.Controls.Add(charts);
        center.Controls.Add(_comparisonGrid);

        Controls.Add(center);
        Controls.Add(agreement);
        Controls.Add(top);
    }

    private static Control CreateMetric(string title, Label value, int left)
    {
        var panel = new Panel { Left = left, Top = 5, Width = 195, Height = 64, BackColor = Color.White };
        var heading = new Label { Text = title, Left = 8, Top = 5, AutoSize = true, ForeColor = Color.DimGray };
        value.Left = 8; value.Top = 27; value.AutoSize = true; value.Font = new Font("Segoe UI", 16, FontStyle.Bold);
        panel.Controls.AddRange(new Control[] { heading, value });
        return panel;
    }

    private static void ConfigureChart(Chart chart, string title, SeriesChartType type, string seriesName)
    {
        chart.Dock = DockStyle.Fill;
        chart.ChartAreas.Add(new ChartArea("Main"));
        chart.Titles.Add(title);
        chart.Series.Add(new Series(seriesName) { ChartType = type, IsValueShownAsLabel = true });
        chart.ChartAreas[0].AxisX.Interval = 1;
        chart.ChartAreas[0].AxisX.LabelStyle.Angle = -30;
    }

    private void LoadPlanChoices()
    {
        if (_tenants.Count > 0) _tenantPicker.SelectedIndex = 0;
    }

    private void RefreshAnalytics()
    {
        var registry = _centralDb.TenantRegistries.ToDictionary(t => t.TenantKey);
        var snapshots = new List<TenantSnapshot>();
        foreach (var tenant in _tenants)
        {
            try
            {
                using var db = Program.CreateDbContext(tenant.ConnectionString);
                var orderCount = db.LaundryOrders.Count();
                var customerCount = db.Customers.Count(c => !c.IsDeleted);
                var revenue = db.Payments
                    .Where(p => p.Status == PaymentStatus.Paid && p.LaundryOrder != null && p.LaundryOrder.Status != OrderStatus.Cancelled)
                    .Sum(p => (decimal?)p.AmountPaid) ?? 0m;
                snapshots.Add(new TenantSnapshot(tenant.Name, revenue, orderCount, customerCount, "Connected"));
            }
            catch (Exception ex)
            {
                snapshots.Add(new TenantSnapshot(tenant.Name, 0m, 0, 0, $"Unavailable: {ex.Message}"));
            }
        }

        _revenueValue.Text = snapshots.Sum(s => s.Revenue).ToString("N2");
        _ordersValue.Text = snapshots.Sum(s => s.Orders).ToString("N0");
        _customersValue.Text = snapshots.Sum(s => s.Customers).ToString("N0");
        _activeTenantsValue.Text = registry.Values.Count(t => t.IsActive && (!t.SubscriptionExpiresAt.HasValue || t.SubscriptionExpiresAt.Value.Date >= DateTime.UtcNow.Date)).ToString();
        _comparisonGrid.DataSource = snapshots.Select(s => new
        {
            s.Tenant,
            Revenue = s.Revenue.ToString("N2"),
            s.Orders,
            s.Customers,
            s.ConnectionStatus
        }).ToList();

        _revenueChart.Series[0].Points.Clear();
        _ordersChart.Series[0].Points.Clear();
        foreach (var snapshot in snapshots.Where(s => s.ConnectionStatus == "Connected"))
        {
            _revenueChart.Series[0].Points.AddXY(snapshot.Tenant, snapshot.Revenue);
            _ordersChart.Series[0].Points.AddXY(snapshot.Tenant, snapshot.Orders);
        }
    }

    private void LoadSelectedPlan()
    {
        if (_tenantPicker.SelectedItem is not TenantDatabaseConfig tenant) return;
        var registration = _centralDb.TenantRegistries.FirstOrDefault(t => t.TenantKey == tenant.Key);
        if (registration == null) return;
        _allowStaff.Checked = registration.AllowStaffAccounts;
        _allowManager.Checked = registration.AllowManagerAccounts;
        _maxBranches.Value = Math.Clamp(registration.MaxBranches, 1, (int)_maxBranches.Maximum);
        _maxBranches.Enabled = registration.TenantKey.Equals("tenant-c", StringComparison.OrdinalIgnoreCase);
        _terms.Text = registration.TermsAndConditions ?? string.Empty;
        _planMessage.Text = $"Agreement version {registration.TermsVersion}";
    }

    private void SaveSelectedPlan()
    {
        if (_tenantPicker.SelectedItem is not TenantDatabaseConfig tenant) return;
        var registration = _centralDb.TenantRegistries.FirstOrDefault(t => t.TenantKey == tenant.Key);
        if (registration == null) return;

        var terms = string.IsNullOrWhiteSpace(_terms.Text) ? null : _terms.Text.Trim();
        if (!string.Equals(registration.TermsAndConditions, terms, StringComparison.Ordinal))
            registration.TermsVersion++;
        registration.AllowStaffAccounts = _allowStaff.Checked;
        registration.AllowManagerAccounts = _allowManager.Checked;
        registration.TermsAndConditions = terms;
        registration.MaxBranches = registration.TenantKey.Equals("tenant-c", StringComparison.OrdinalIgnoreCase)
            ? (int)_maxBranches.Value
            : 1;
        _centralDb.SaveChanges();
        _planMessage.Text = $"Saved agreement version {registration.TermsVersion}.";
    }
}
