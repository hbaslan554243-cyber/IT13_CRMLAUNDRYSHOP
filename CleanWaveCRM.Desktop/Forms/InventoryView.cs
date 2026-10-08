using CRM.api.Controllers;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    /// <summary>
    /// Inventory Management. Edit items and stock levels directly in the
    /// grid (same pattern as Manage Services), with low-stock rows
    /// highlighted so staff notice at a glance.
    /// </summary>
    public class InventoryView : UserControl
    {
        private readonly InventoryController _controller;
        private readonly DataGridView _grid = new();
        private readonly Label _lblMessage = new();
        private readonly Label _lblAlert = new();

        public InventoryView(LaundryShopDbContext db)
        {
            _controller = new InventoryController(db);
            BuildUi();
            LoadData();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;

            var top = new Panel { Dock = DockStyle.Top, Height = 80, Padding = new Padding(10) };

            var lblTitle = new Label { Text = "Inventory Management", Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = true, Location = new Point(10, 8) };
            var lblHint = new Label { Text = "Edit stock directly in the grid. Rows in red are at or below their reorder threshold.", ForeColor = Color.Gray, AutoSize = true, Location = new Point(10, 35) };

            _lblAlert.Location = new Point(10, 55);
            _lblAlert.AutoSize = true;
            _lblAlert.ForeColor = Color.Firebrick;
            _lblAlert.Font = new Font("Segoe UI", 9, FontStyle.Bold);

            top.Controls.AddRange(new Control[] { lblTitle, lblHint, _lblAlert });

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = true;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.Columns.Add("Name", "Item Name");
            _grid.Columns.Add("Unit", "Unit (sachet/liter/etc.)");
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "QuantityOnHand", HeaderText = "Stock On Hand", DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ReorderThreshold", HeaderText = "Reorder At", DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SellingPrice", HeaderText = "Add-On Price", DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsActive", HeaderText = "Active" });
            _grid.CellFormatting += Grid_CellFormatting;

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

        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
            var row = _grid.Rows[e.RowIndex];
            if (row.IsNewRow) return;

            decimal.TryParse(row.Cells["QuantityOnHand"].Value?.ToString(), out var qty);
            decimal.TryParse(row.Cells["ReorderThreshold"].Value?.ToString(), out var threshold);

            row.DefaultCellStyle.BackColor = qty <= threshold ? Color.MistyRose : Color.White;
        }

        private void LoadData()
        {
            var items = _controller.GetAll();
            _grid.Rows.Clear();

            foreach (var item in items)
            {
                var idx = _grid.Rows.Add(new object[] { item.Name ?? string.Empty, item.Unit ?? "unit", item.QuantityOnHand, item.ReorderThreshold, item.SellingPrice, item.IsActive });
                _grid.Rows[idx].Tag = item.Id;
            }

            var lowStock = items.Where(i => i.IsLowStock && i.IsActive).ToList();
            _lblAlert.Text = lowStock.Count > 0
                ? $"⚠ Low stock: {string.Join(", ", lowStock.Select(i => i.Name))}"
                : "";
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

                var unit = row.Cells["Unit"].Value?.ToString() ?? "unit";
                decimal.TryParse(row.Cells["QuantityOnHand"].Value?.ToString(), out var qty);
                decimal.TryParse(row.Cells["ReorderThreshold"].Value?.ToString(), out var threshold);
                decimal.TryParse(row.Cells["SellingPrice"].Value?.ToString(), out var price);
                var isActive = row.Cells["IsActive"].Value is bool b && b;
                var id = row.Tag is int i ? i : 0;

                var result = id == 0
                    ? _controller.CreateItem(name, unit, qty, threshold, price)
                    : _controller.UpdateItem(id, name, unit, qty, threshold, price, isActive);

                if (result.Success) savedCount++;
            }

            _lblMessage.Text = $"Saved {savedCount} item(s).";
            LoadData();
        }
    }
}
