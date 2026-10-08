using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    /// <summary>
    /// Super Admin controls whether each separately hosted tenant database
    /// is available and manages its subscription expiry.
    /// </summary>
    public class SuperAdminHomeView : UserControl
    {
        private readonly LaundryShopDbContext _db;
        private readonly DataGridView _grid = new();
        private readonly Label _lblMessage = new();

        public SuperAdminHomeView(LaundryShopDbContext db)
        {
            _db = db;
            BuildUi();
            LoadData();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;

            var top = new Panel { Dock = DockStyle.Top, Height = 90, Padding = new Padding(10) };

            var lblTitle = new Label { Text = "Tenant Administration", Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = true, Location = new Point(10, 8) };
            var lblHint = new Label
            {
                Text = "Activate or deactivate each tenant database and manage its subscription expiry.",
                ForeColor = Color.Gray,
                AutoSize = true,
                MaximumSize = new Size(750, 0),
                Location = new Point(10, 36)
            };

            top.Controls.AddRange(new Control[] { lblTitle, lblHint });

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 55, Padding = new Padding(10) };
            var btnSubscription = new Button { Text = "Manage Selected Tenant", Left = 10, Top = 10, Width = 190, Height = 28, FlatStyle = FlatStyle.Flat };
            btnSubscription.Click += (_, _) => SetSubscription();

            _lblMessage.Left = 215; _lblMessage.Top = 15; _lblMessage.AutoSize = true; _lblMessage.ForeColor = Color.SeaGreen;

            bottom.Controls.AddRange(new Control[] { btnSubscription, _lblMessage });

            Controls.Add(_grid);
            Controls.Add(bottom);
            Controls.Add(top);
        }

        private void LoadData()
        {
            _grid.DataSource = _db.TenantRegistries.OrderBy(t => t.TenantKey).Select(t => new
            {
                t.Id,
                t.TenantKey,
                Tenant = t.DisplayName,
                Status = !t.IsActive ? "Inactive"
                    : t.SubscriptionExpiresAt.HasValue && t.SubscriptionExpiresAt.Value.Date < DateTime.UtcNow.Date ? "Expired"
                    : "Active",
                SubscriptionExpires = t.SubscriptionExpiresAt.HasValue ? t.SubscriptionExpiresAt.Value.ToString("yyyy-MM-dd") : "Never"
            }).ToList();
        }

        private void SetSubscription()
        {
            if (_grid.CurrentRow == null) return;
            if (!int.TryParse(_grid.CurrentRow.Cells["Id"].Value?.ToString(), out var registryId))
            {
                _lblMessage.Text = "Select a tenant first.";
                return;
            }

            var registration = _db.TenantRegistries.FirstOrDefault(t => t.Id == registryId);
            if (registration == null) return;

            using var dialog = new Form { Text = $"Manage {registration.DisplayName}", Width = 340, Height = 175, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
            var active = new CheckBox { Text = "Tenant active", Checked = registration.IsActive, Left = 15, Top = 15, AutoSize = true };
            var noExpiry = new CheckBox { Text = "No expiration", Checked = !registration.SubscriptionExpiresAt.HasValue, Left = 15, Top = 45, AutoSize = true };
            var expiry = new DateTimePicker { Left = 135, Top = 42, Width = 170, Format = DateTimePickerFormat.Short, Value = registration.SubscriptionExpiresAt ?? DateTime.Today };
            noExpiry.CheckedChanged += (_, _) => expiry.Enabled = !noExpiry.Checked;
            expiry.Enabled = !noExpiry.Checked;
            var save = new Button { Text = "Save", Left = 220, Top = 85, Width = 85, DialogResult = DialogResult.OK };
            dialog.Controls.AddRange(new Control[] { active, noExpiry, expiry, save });
            dialog.AcceptButton = save;

            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            registration.IsActive = active.Checked;
            registration.SubscriptionExpiresAt = noExpiry.Checked ? null : expiry.Value.Date;
            _db.SaveChanges();
            _lblMessage.Text = $"{registration.DisplayName} updated.";
            LoadData();
        }
    }
}
