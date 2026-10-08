using CRM.api.Controllers;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    public class NotificationListView : UserControl
    {
        private readonly NotificationController _controller;
        private readonly DataGridView _grid = new();

        public NotificationListView(LaundryShopDbContext db)
        {
            _controller = new NotificationController(db);
            BuildUi();
            LoadData();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            var top = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10) };

            var lblTitle = new Label { Text = "Notifications", Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = true, Location = new Point(10, 8) };
            var lblHint = new Label
            {
                Text = "Created automatically when orders become Ready/Completed, or when promotions are sent.",
                ForeColor = Color.Gray,
                AutoSize = true,
                Location = new Point(190, 14)
            };
            top.Controls.AddRange(new Control[] { lblTitle, lblHint });

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            Controls.Add(_grid);
            Controls.Add(top);
        }

        private void LoadData()
        {
            var notifications = _controller.GetAll();
            _grid.DataSource = notifications.Select(n => new
            {
                Customer = n.Customer?.FullName ?? "(unknown)",
                n.Type,
                n.Channel,
                n.Message,
                Sent = n.SentAt.HasValue ? n.SentAt.Value.ToLocalTime().ToString("g") : "-"
            }).ToList();
        }
    }
}
