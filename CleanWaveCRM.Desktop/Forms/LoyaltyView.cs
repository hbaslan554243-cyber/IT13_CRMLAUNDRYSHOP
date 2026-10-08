using CRM.api.Controllers;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    public class LoyaltyView : UserControl
    {
        private readonly LoyaltyController _controller;
        private readonly LaundryShopDbContext _db;
        private readonly DataGridView _grid = new();

        public LoyaltyView(LaundryShopDbContext db)
        {
            _db = db;
            _controller = new LoyaltyController(db);
            BuildUi();
            LoadData();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            var top = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };

            var lblTitle = new Label { Text = "Loyalty & Rewards", Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = true, Location = new Point(10, 8) };
            var lblHint = new Label
            {
                Text = "Points are earned automatically on full payment. Double-click a customer to adjust or redeem points.",
                ForeColor = Color.Gray,
                AutoSize = true,
                Location = new Point(10, 35)
            };
            top.Controls.AddRange(new Control[] { lblTitle, lblHint });

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.CellDoubleClick += (_, _) => OpenAdjust();

            Controls.Add(_grid);
            Controls.Add(top);
        }

        private void LoadData()
        {
            var balances = _controller.GetAllBalances();
            _grid.DataSource = balances.Select(b => new
            {
                b.Customer.Id,
                b.Customer.FullName,
                b.Customer.ContactNumber,
                Points = b.Points
            }).ToList();
        }

        private void OpenAdjust()
        {
            if (_grid.CurrentRow == null) return;
            var customerId = Convert.ToInt32(_grid.CurrentRow.Cells["Id"].Value);
            var customerName = _grid.CurrentRow.Cells["FullName"].Value?.ToString() ?? "";

            using var form = new LoyaltyAdjustForm(_db, customerId, customerName);
            form.ShowDialog();
            LoadData();
        }
    }
}
