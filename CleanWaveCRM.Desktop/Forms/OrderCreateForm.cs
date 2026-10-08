using CRM.api.Controllers;
using CRM.domain.Entities.Tenant;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    public class OrderCreateForm : Form
    {
        private readonly OrderController _orderController;
        private readonly CustomerController _customerController;
        private readonly ServiceTypeController _serviceController;
        private readonly InventoryController _inventoryController;
        private readonly LaundryShopDbContext _db;

        private readonly ComboBox _cmbCustomer = new();
        private readonly DataGridView _gridItems = new();
        private readonly CheckedListBox _clbAddOns = new();
        private readonly TextBox _txtInstructions = new();
        private readonly Label _lblTotal = new();
        private readonly Label _lblStatus = new();
        private List<ServiceType> _services = new();
        private List<InventoryItem> _addOnItems = new();

        public OrderCreateForm(LaundryShopDbContext db)
        {
            _db = db;
            _orderController = new OrderController(db);
            _customerController = new CustomerController(db);
            _serviceController = new ServiceTypeController(db);
            _inventoryController = new InventoryController(db);
            BuildUi();
            LoadCustomers();
            LoadServices();
            LoadAddOns();
        }

        private void BuildUi()
        {
            Text = "New Laundry Order";
            Width = 620;
            Height = 660;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblCustomer = new Label { Text = "Customer*", Left = 20, Top = 20, AutoSize = true };
            _cmbCustomer.Left = 130;
            _cmbCustomer.Top = 18;
            _cmbCustomer.Width = 300;
            _cmbCustomer.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbCustomer.DisplayMember = "Display";
            _cmbCustomer.ValueMember = "Id";

            var lblItems = new Label { Text = "Services / Items", Left = 20, Top = 60, AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };

            _gridItems.Left = 20;
            _gridItems.Top = 85;
            _gridItems.Width = 560;
            _gridItems.Height = 170;
            _gridItems.AllowUserToAddRows = true;
            _gridItems.AllowUserToDeleteRows = true;
            _gridItems.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            var serviceColumn = new DataGridViewComboBoxColumn
            {
                Name = "ServiceName",
                HeaderText = "Service",
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                FlatStyle = FlatStyle.Flat
            };
            _gridItems.Columns.Add(serviceColumn);
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "Weight (kg) / Items", DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "UnitPrice", HeaderText = "Unit Price", DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _gridItems.CellValueChanged += GridItems_CellValueChanged;
            _gridItems.CurrentCellDirtyStateChanged += (_, _) =>
            {
                if (_gridItems.IsCurrentCellDirty) _gridItems.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            var lblAddOns = new Label { Text = "Add-Ons", Left = 20, Top = 265, AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            var lblAddOnsHint = new Label { Text = "Optional: detergent, fabric conditioner, etc.", Left = 90, Top = 267, AutoSize = true, ForeColor = Color.Gray };

            _clbAddOns.Left = 20;
            _clbAddOns.Top = 290;
            _clbAddOns.Width = 560;
            _clbAddOns.Height = 90;
            _clbAddOns.CheckOnClick = true;
            _clbAddOns.ItemCheck += (_, _) => BeginInvoke(new MethodInvoker(RecalculateTotalDisplay)); // ItemCheck fires before the checked state updates, so defer

            var lblInstructions = new Label { Text = "Special Instructions", Left = 20, Top = 395, AutoSize = true };
            _txtInstructions.Left = 20;
            _txtInstructions.Top = 415;
            _txtInstructions.Width = 560;
            _txtInstructions.Height = 50;
            _txtInstructions.Multiline = true;

            _lblTotal.Left = 20;
            _lblTotal.Top = 475;
            _lblTotal.Width = 300;
            _lblTotal.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            _lblTotal.Text = "Total: 0.00";

            var btnSave = new Button
            {
                Text = "Create Order",
                Left = 20,
                Top = 510,
                Width = 180,
                Height = 35,
                BackColor = Color.SeaGreen,
                ForeColor = Color.White
            };
            btnSave.Click += BtnSave_Click;

            var btnCancel = new Button { Text = "Cancel", Left = 210, Top = 510, Width = 90, Height = 35 };
            btnCancel.Click += (_, _) => Close();

            _lblStatus.Left = 20;
            _lblStatus.Top = 555;
            _lblStatus.Width = 560;
            _lblStatus.ForeColor = Color.Firebrick;

            Controls.AddRange(new Control[]
            {
                lblCustomer, _cmbCustomer, lblItems, _gridItems,
                lblAddOns, lblAddOnsHint, _clbAddOns,
                lblInstructions, _txtInstructions, _lblTotal, btnSave, btnCancel, _lblStatus
            });
        }

        private void LoadCustomers()
        {
            var customers = _customerController.GetAllCustomers()
                .Select(c => new { c.Id, Display = $"{c.FullName} ({c.ContactNumber})" })
                .ToList();
            _cmbCustomer.DataSource = customers;
        }

        private void LoadServices()
        {
            _services = _serviceController.GetActiveServiceTypes();

            var serviceColumn = (DataGridViewComboBoxColumn)_gridItems.Columns["ServiceName"]!;
            serviceColumn.Items.Clear();
            serviceColumn.Items.AddRange(_services.Select(s => s.Name).ToArray());
        }

        private void LoadAddOns()
        {
            _addOnItems = _inventoryController.GetActive().Where(i => i.SellingPrice > 0).ToList();
            _clbAddOns.Items.Clear();
            foreach (var item in _addOnItems)
            {
                var lowStockNote = item.IsLowStock ? "  (LOW STOCK)" : "";
                _clbAddOns.Items.Add($"{item.Name} - {item.SellingPrice:N2} per {item.Unit}{lowStockNote}");
            }
        }

        private void GridItems_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            var row = _gridItems.Rows[e.RowIndex];
            var columnName = _gridItems.Columns[e.ColumnIndex].Name;

            // When a service is picked, auto-fill its default price
            // (staff can still override it afterward if needed).
            if (columnName == "ServiceName")
            {
                var selectedName = row.Cells["ServiceName"].Value?.ToString();
                var match = _services.FirstOrDefault(s => s.Name == selectedName);
                if (match != null)
                {
                    row.Cells["UnitPrice"].Value = match.UnitPrice;
                    if (row.Cells["Quantity"].Value == null)
                        row.Cells["Quantity"].Value = 1;
                }
            }

            RecalculateTotalDisplay();
        }

        private void RecalculateTotalDisplay()
        {
            decimal total = 0;
            foreach (DataGridViewRow row in _gridItems.Rows)
            {
                if (row.IsNewRow) continue;
                decimal.TryParse(row.Cells["Quantity"].Value?.ToString(), out var qty);
                decimal.TryParse(row.Cells["UnitPrice"].Value?.ToString(), out var price);
                total += qty * price;
            }

            for (int i = 0; i < _addOnItems.Count; i++)
            {
                if (_clbAddOns.GetItemChecked(i))
                    total += _addOnItems[i].SellingPrice;
            }

            _lblTotal.Text = $"Total: {total:N2}";
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (_cmbCustomer.SelectedValue == null)
            {
                _lblStatus.Text = "Please select a customer.";
                return;
            }

            var lineItems = new List<(string, decimal, decimal)>();
            foreach (DataGridViewRow row in _gridItems.Rows)
            {
                if (row.IsNewRow) continue;
                var name = row.Cells["ServiceName"].Value?.ToString();
                decimal.TryParse(row.Cells["Quantity"].Value?.ToString(), out var qty);
                decimal.TryParse(row.Cells["UnitPrice"].Value?.ToString(), out var price);

                if (!string.IsNullOrWhiteSpace(name) && qty > 0)
                    lineItems.Add((name.Trim(), qty, price));
            }

            var addOnIds = new List<int>();
            for (int i = 0; i < _addOnItems.Count; i++)
            {
                if (_clbAddOns.GetItemChecked(i))
                    addOnIds.Add(_addOnItems[i].Id);
            }

            var customerId = (int)_cmbCustomer.SelectedValue;
            var result = _orderController.CreateOrder(customerId, lineItems, addOnIds, _txtInstructions.Text, AuthController.CurrentUser?.Id);

            if (!result.Success)
            {
                _lblStatus.Text = result.Message;
                return;
            }

            MessageBox.Show(result.Message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
