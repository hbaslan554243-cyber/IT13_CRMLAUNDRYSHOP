using CRM.api.Controllers;
using CRM.domain.Enums;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    public class OrderListView : UserControl
    {
        private readonly OrderController _controller;
        private readonly LaundryShopDbContext _db;
        private readonly TextBox _txtSearch = new();
        private readonly ComboBox _cmbStatus = new();
        private readonly DataGridView _grid = new();

        public OrderListView(LaundryShopDbContext db)
        {
            _db = db;
            _controller = new OrderController(_db);
            BuildUi();
            LoadData();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;

            var topPanel = new Panel { Dock = DockStyle.Top, Height = 60, Padding = new Padding(10) };

            var lblTitle = new Label
            {
                Text = "Laundry Orders",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(10, 8)
            };

            _txtSearch.Location = new Point(160, 12);
            _txtSearch.Width = 200;
            _txtSearch.PlaceholderText = "Search customer...";
            _txtSearch.TextChanged += (_, _) => LoadData();

            _cmbStatus.Location = new Point(370, 12);
            _cmbStatus.Width = 150;
            _cmbStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbStatus.Items.Add("All Statuses");
            _cmbStatus.Items.AddRange(Enum.GetNames(typeof(OrderStatus)));
            _cmbStatus.SelectedIndex = 0;
            _cmbStatus.SelectedIndexChanged += (_, _) => LoadData();

            var btnNew = new Button
            {
                Text = "+ New Order",
                Location = new Point(540, 10),
                Width = 130,
                Height = 30,
                BackColor = Color.SeaGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnNew.Click += (_, _) => OpenCreateForm();

            var btnOpen = new Button
            {
                Text = "Open Selected",
                Location = new Point(680, 10),
                Width = 140,
                Height = 30,
                FlatStyle = FlatStyle.Flat
            };
            btnOpen.Click += (_, _) => OpenDetail();

            topPanel.Controls.AddRange(new Control[] { lblTitle, _txtSearch, _cmbStatus, btnNew, btnOpen });

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.CellDoubleClick += (_, _) => OpenDetail();

            Controls.Add(_grid);
            Controls.Add(topPanel);
        }

        private void LoadData()
        {
            OrderStatus? statusFilter = _cmbStatus.SelectedIndex > 0
                ? Enum.Parse<OrderStatus>(_cmbStatus.SelectedItem!.ToString()!)
                : null;

            var orders = _controller.SearchOrders(_txtSearch.Text, statusFilter);
            _grid.DataSource = orders.Select(o => new
            {
                o.Id,
                Customer = o.Customer?.FullName ?? "(unknown)",
                Status = o.Status.ToString(),
                Items = o.Items.Count,
                Total = o.TotalAmount,
                Received = o.ReceivedAt.ToLocalTime().ToString("g"),
                Ready = o.ReadyAt.HasValue ? o.ReadyAt.Value.ToLocalTime().ToString("g") : "-",
                Completed = o.CompletedAt.HasValue ? o.CompletedAt.Value.ToLocalTime().ToString("g") : "-"
            }).ToList();
        }

        private void OpenCreateForm()
        {
            using var form = new OrderCreateForm(_db);
            if (form.ShowDialog() == DialogResult.OK)
                LoadData();
        }

        private void OpenDetail()
        {
            if (_grid.CurrentRow == null) return;
            if (!int.TryParse(_grid.CurrentRow.Cells["Id"].Value?.ToString(), out var id)) return;

            using var form = new OrderDetailForm(_db, id);
            form.ShowDialog();
            LoadData();
        }
    }
}
