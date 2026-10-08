using CRM.api.Controllers;
using CRM.domain.Entities.Tenant;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    public class CustomerListView : UserControl
    {
        private readonly CustomerController _controller;
        private readonly LaundryShopDbContext _db;
        private readonly TextBox _txtSearch = new();
        private readonly DataGridView _grid = new();

        public CustomerListView(LaundryShopDbContext db)
        {
            _db = db;
            _controller = new CustomerController(_db);
            BuildUi();
            LoadData(string.Empty);
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };

            var lblTitle = new Label
            {
                Text = "Customer Profiles",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(10, 8)
            };

            _txtSearch.Location = new Point(230, 12);
            _txtSearch.Width = 250;
            _txtSearch.PlaceholderText = "Search name, phone, or email...";
            _txtSearch.TextChanged += (_, _) => LoadData(_txtSearch.Text);

            var btnAdd = new Button
            {
                Text = "+ Register New Customer",
                Location = new Point(500, 10),
                Width = 190,
                Height = 30,
                BackColor = Color.SeaGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnAdd.Click += (_, _) => OpenForm(null);

            var btnEdit = new Button
            {
                Text = "Edit Selected",
                Location = new Point(700, 10),
                Width = 130,
                Height = 30,
                FlatStyle = FlatStyle.Flat
            };
            btnEdit.Click += (_, _) => EditSelected();

            var btnDeactivate = new Button
            {
                Text = "Deactivate",
                Location = new Point(840, 10),
                Width = 110,
                Height = 30,
                BackColor = Color.Firebrick,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnDeactivate.Click += (_, _) => DeactivateSelected();

            topPanel.Controls.AddRange(new Control[] { lblTitle, _txtSearch, btnAdd, btnEdit, btnDeactivate });

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.CellDoubleClick += (_, _) => EditSelected();

            Controls.Add(_grid);
            Controls.Add(topPanel);
        }

        private void LoadData(string keyword)
        {
            var customers = _controller.SearchCustomers(keyword);
            _grid.DataSource = customers.Select(c => new
            {
                c.Id,
                c.FullName,
                c.ContactNumber,
                c.Email,
                Segment = c.Segment.ToString(),
                c.TotalOrders,
                c.TotalSpend,
                LastOrder = c.LastOrderDate.HasValue ? c.LastOrderDate.Value.ToLocalTime().ToString("d") : "-",
                Registered = c.CreatedAt.ToLocalTime().ToString("g")
            }).OrderBy(c => c.Id).ToList();
        }

        private void EditSelected()
        {
            if (_grid.CurrentRow == null) return;
            if (!int.TryParse(_grid.CurrentRow.Cells["Id"].Value?.ToString(), out var id)) return;
            var customer = _controller.GetCustomer(id);
            if (customer != null) OpenForm(customer);
        }

        private void DeactivateSelected()
        {
            if (_grid.CurrentRow == null) return;
            if (!int.TryParse(_grid.CurrentRow.Cells["Id"].Value?.ToString(), out var id)) return;
            var name = _grid.CurrentRow.Cells["FullName"].Value?.ToString() ?? "this customer";

            var confirm = MessageBox.Show(
                $"Remove {name}? Their order/feedback/loyalty history is kept, just hidden from active lists.",
                "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            var result = _controller.DeleteCustomer(id);
            MessageBox.Show(result.Message, result.Success ? "Success" : "Error",
                MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            LoadData(_txtSearch.Text);
        }

        private void OpenForm(Customer? existing)
        {
            using var form = new CustomerFormDialog(_db, existing);
            if (form.ShowDialog() == DialogResult.OK)
                LoadData(_txtSearch.Text);
        }
    }
}
