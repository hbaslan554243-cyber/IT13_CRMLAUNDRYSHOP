using CRM.api.Controllers;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    /// <summary>
    /// Services & Pricing catalog, as its own menu item rather than a
    /// button buried inside the New Order dialog - this is shop
    /// configuration, not something that belongs mid-transaction.
    /// </summary>
    public class ServiceTypeManagerView : UserControl
    {
        private readonly ServiceTypeController _controller;
        private readonly DataGridView _grid = new();
        private readonly Label _lblMessage = new();

        public ServiceTypeManagerView(LaundryShopDbContext db)
        {
            _controller = new ServiceTypeController(db);
            BuildUi();
            LoadData();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;

            var top = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };
            var lblTitle = new Label { Text = "Services & Pricing", Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = true, Location = new Point(10, 8) };
            var lblHint = new Label { Text = "Edit prices directly in the grid. Use the last row to add a new service.", ForeColor = Color.Gray, AutoSize = true, Location = new Point(10, 35) };
            top.Controls.AddRange(new Control[] { lblTitle, lblHint });

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = true;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.Columns.Add("Name", "Service Name");
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UnitPrice", HeaderText = "Price", DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _grid.Columns.Add("UnitLabel", "Unit (kg/piece)");
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsActive", HeaderText = "Active" });

            var btnSave = new Button { Text = "Save Changes", Dock = DockStyle.Bottom, Height = 40, BackColor = Color.SeaGreen, ForeColor = Color.White };
            btnSave.Click += (_, _) => Save();

            _lblMessage.Dock = DockStyle.Bottom;
            _lblMessage.Height = 25;
            _lblMessage.TextAlign = ContentAlignment.MiddleLeft;
            _lblMessage.Padding = new Padding(10, 0, 0, 0);
            _lblMessage.ForeColor = Color.SeaGreen;

            Controls.Add(_grid);
            Controls.Add(btnSave);
            Controls.Add(_lblMessage);
            Controls.Add(top);
        }

        private void LoadData()
        {
            _grid.Rows.Clear();
            foreach (var s in _controller.GetAllServiceTypes())
            {
                var idx = _grid.Rows.Add(s.Name, s.UnitPrice, s.UnitLabel, s.IsActive);
                _grid.Rows[idx].Tag = s.Id;
            }
        }

        private void Save()
        {
            if (_grid.IsCurrentCellDirty)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);

            int savedCount = 0;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;

                var name = row.Cells["Name"].Value?.ToString();
                if (string.IsNullOrWhiteSpace(name)) continue;

                decimal.TryParse(row.Cells["UnitPrice"].Value?.ToString(), out var price);
                var unitLabel = row.Cells["UnitLabel"].Value?.ToString();
                var isActive = row.Cells["IsActive"].Value is bool b && b;
                var id = row.Tag is int i ? i : 0;

                var result = id == 0
                    ? _controller.CreateServiceType(name, price, unitLabel, isActive)
                    : _controller.UpdateServiceType(id, name, price, unitLabel, isActive);

                if (result.Success) savedCount++;
            }

            _lblMessage.Text = $"Saved {savedCount} service(s).";
            LoadData();
        }
    }
}
