using CRM.api.Controllers;
using CRM.domain.Entities.Tenant;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    /// <summary>
    /// Drill-down behind the "Repeat Customers" KPI card. Every number here
    /// is computed live from actual orders/customers (see
    /// ReportController.GetRetentionDetails) - nothing is hardcoded.
    /// Lets the Owner select any customer in the grid and create a
    /// promotion for them, with the reason auto-suggested from their
    /// actual behavior.
    /// </summary>
    public class CustomerRetentionDetailsForm : Form
    {
        private readonly LaundryShopDbContext _db;
        private readonly ReportController _controller;
        private readonly Label _lblRate = new();
        private readonly Label _lblBreakdown = new();
        private readonly Label _lblAverages = new();
        private readonly ComboBox _cmbCategory = new();
        private readonly DataGridView _grid = new();
        private readonly Button _btnCreatePromotion = new();
        private ReportController.RetentionDetails _details = null!;

        public CustomerRetentionDetailsForm(LaundryShopDbContext db)
        {
            _db = db;
            _controller = new ReportController(db);
            BuildUi();
            LoadData();
        }

        private void BuildUi()
        {
            Text = "Customer Retention Details";
            Width = 700;
            Height = 620;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = UiTheme.Background;

            var lblTitle = new Label { Text = "Customer Retention", Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = UiTheme.TextDark, Left = 20, Top = 20, AutoSize = true };

            _lblRate.Left = 20; _lblRate.Top = 55; _lblRate.AutoSize = true;
            _lblRate.Font = new Font("Segoe UI", 22, FontStyle.Bold); _lblRate.ForeColor = Color.MediumPurple;

            _lblBreakdown.Left = 20; _lblBreakdown.Top = 100; _lblBreakdown.AutoSize = true;
            _lblBreakdown.MaximumSize = new Size(650, 0); _lblBreakdown.ForeColor = UiTheme.TextMuted; _lblBreakdown.Font = UiTheme.BodyFont;

            _lblAverages.Left = 20; _lblAverages.Top = 145; _lblAverages.AutoSize = true;
            _lblAverages.ForeColor = UiTheme.TextMuted; _lblAverages.Font = UiTheme.BodyFont;

            var lblView = new Label { Text = "View:", Left = 20, Top = 180, AutoSize = true };
            _cmbCategory.Left = 65; _cmbCategory.Top = 177; _cmbCategory.Width = 220;
            _cmbCategory.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbCategory.Items.AddRange(new[] { "Repeat Customers", "New Customers", "Inactive Customers", "Reactivated Customers", "Churned Customers" });
            _cmbCategory.SelectedIndex = 0;
            _cmbCategory.SelectedIndexChanged += (_, _) => ShowCategory();

            _btnCreatePromotion.Text = "Create Promotion for Selected";
            _btnCreatePromotion.Left = 300; _btnCreatePromotion.Top = 176; _btnCreatePromotion.Width = 200; _btnCreatePromotion.Height = 28;
            _btnCreatePromotion.BackColor = Color.SeaGreen; _btnCreatePromotion.ForeColor = Color.White; _btnCreatePromotion.FlatStyle = FlatStyle.Flat;
            _btnCreatePromotion.Click += (_, _) => CreatePromotionForSelected();

            _grid.Left = 20; _grid.Top = 215; _grid.Width = 640; _grid.Height = 320;
            _grid.ReadOnly = true; _grid.AllowUserToAddRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.BackgroundColor = Color.White;

            var btnClose = new Button { Text = "Close", Left = 20, Top = 545, Width = 100, Height = 35 };
            btnClose.Click += (_, _) => Close();

            Controls.AddRange(new Control[]
            {
                lblTitle, _lblRate, _lblBreakdown, _lblAverages,
                lblView, _cmbCategory, _btnCreatePromotion, _grid, btnClose
            });
        }

        private void LoadData()
        {
            _details = _controller.GetRetentionDetails();

            _lblRate.Text = $"{_details.RepeatCustomerRate:N0}% Repeat Rate";
            _lblBreakdown.Text = $"Total: {_details.TotalCustomers}   \u2022   New: {_details.NewCustomerCount}   \u2022   " +
                                  $"Repeat: {_details.RepeatCustomerCount}   \u2022   Inactive: {_details.InactiveCount}   \u2022   " +
                                  $"Reactivated: {_details.ReactivatedCount}   \u2022   Churned: {_details.ChurnedCount}";
            _lblAverages.Text = $"Avg Orders/Customer: {_details.AverageOrdersPerCustomer:N1}   \u2022   " +
                                 $"Avg Days Between Orders: {(_details.AverageDaysBetweenOrders.HasValue ? $"{_details.AverageDaysBetweenOrders:N0} days" : "N/A")}";

            ShowCategory();
        }

        private void ShowCategory()
        {
            List<Customer> customers = _cmbCategory.SelectedIndex switch
            {
                0 => _details.RepeatCustomers,
                1 => _details.NewCustomers,
                2 => _details.InactiveCustomers,
                3 => _details.ReactivatedCustomers,
                4 => _details.ChurnedCustomers,
                _ => new List<Customer>()
            };

            _grid.DataSource = customers.Select(c => new
            {
                c.Id,
                c.FullName,
                c.ContactNumber,
                Segment = c.Segment.ToString(),
                c.TotalOrders,
                c.TotalSpend,
                LastOrder = c.LastOrderDate.HasValue ? c.LastOrderDate.Value.ToLocalTime().ToString("d") : "-"
            }).ToList();
        }

        private void CreatePromotionForSelected()
        {
            if (_grid.CurrentRow == null)
            {
                MessageBox.Show("Select a customer first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!int.TryParse(_grid.CurrentRow.Cells["Id"].Value?.ToString(), out var customerId)) return;
            var customerName = _grid.CurrentRow.Cells["FullName"].Value?.ToString() ?? "Customer";

            using var form = new PromotionCreateForm(_db, customerId, customerName);
            form.ShowDialog();
        }
    }
}
