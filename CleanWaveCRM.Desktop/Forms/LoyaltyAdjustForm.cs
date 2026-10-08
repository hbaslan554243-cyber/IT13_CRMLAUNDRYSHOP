using CRM.api.Controllers;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    public class LoyaltyAdjustForm : Form
    {
        private readonly LoyaltyController _controller;
        private readonly int _customerId;

        private readonly DataGridView _gridHistory = new();
        private readonly NumericUpDown _numPoints = new();
        private readonly TextBox _txtNotes = new();
        private readonly Label _lblStatus = new();

        public LoyaltyAdjustForm(LaundryShopDbContext db, int customerId, string customerName)
        {
            _controller = new LoyaltyController(db);
            _customerId = customerId;
            BuildUi(customerName);
            LoadHistory();
        }

        private void BuildUi(string customerName)
        {
            Text = $"Loyalty - {customerName}";
            Width = 480;
            Height = 500;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblTitle = new Label { Text = customerName, Left = 20, Top = 15, AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold) };

            var lblHistory = new Label { Text = "Transaction history", Left = 20, Top = 50, AutoSize = true };
            _gridHistory.Left = 20; _gridHistory.Top = 70; _gridHistory.Width = 420; _gridHistory.Height = 220;
            _gridHistory.ReadOnly = true;
            _gridHistory.AllowUserToAddRows = false;
            _gridHistory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            var lblPoints = new Label { Text = "Points (negative = redeem)", Left = 20, Top = 305, AutoSize = true };
            _numPoints.Left = 20; _numPoints.Top = 325; _numPoints.Width = 100; _numPoints.Minimum = -100000; _numPoints.Maximum = 100000;

            var lblNotes = new Label { Text = "Notes", Left = 140, Top = 305, AutoSize = true };
            _txtNotes.Left = 140; _txtNotes.Top = 325; _txtNotes.Width = 300;

            var btnApply = new Button { Text = "Apply Adjustment", Left = 20, Top = 365, Width = 160, Height = 35, BackColor = Color.SteelBlue, ForeColor = Color.White };
            btnApply.Click += (_, _) => Apply();

            var btnClose = new Button { Text = "Close", Left = 190, Top = 365, Width = 90, Height = 35 };
            btnClose.Click += (_, _) => Close();

            _lblStatus.Left = 20; _lblStatus.Top = 410; _lblStatus.Width = 420; _lblStatus.ForeColor = Color.SeaGreen;

            Controls.AddRange(new Control[] { lblTitle, lblHistory, _gridHistory, lblPoints, _numPoints, lblNotes, _txtNotes, btnApply, btnClose, _lblStatus });
        }

        private void LoadHistory()
        {
            var transactions = _controller.GetTransactions(_customerId);
            _gridHistory.DataSource = transactions.Select(t => new
            {
                t.Type,
                t.Points,
                t.Notes,
                Date = t.CreatedAt.ToLocalTime().ToString("g")
            }).ToList();
        }

        private void Apply()
        {
            var delta = (int)_numPoints.Value;
            var result = _controller.AdjustPoints(_customerId, delta, _txtNotes.Text);
            _lblStatus.ForeColor = result.Success ? Color.SeaGreen : Color.Firebrick;
            _lblStatus.Text = result.Message;
            LoadHistory();
        }
    }
}
