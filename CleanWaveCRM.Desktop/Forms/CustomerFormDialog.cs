using CRM.api.Controllers;
using CRM.domain.Entities.Tenant;
using CRM.domain.Enums;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    public class CustomerFormDialog : Form
    {
        private readonly CustomerController _controller;
        private readonly Customer? _existing;

        private readonly TextBox _txtFullName = new();
        private readonly TextBox _txtContact = new();
        private readonly TextBox _txtEmail = new();
        private readonly TextBox _txtAddress = new();
        private readonly TextBox _txtPreferences = new();
        private readonly ComboBox _cmbSegment = new();
        private readonly Label _lblStats = new();
        private readonly Label _lblStatus = new();

        public CustomerFormDialog(LaundryShopDbContext db, Customer? existing)
        {
            _controller = new CustomerController(db);
            _existing = existing;
            BuildUi();
            if (_existing != null) PopulateFields(_existing);
        }

        private void BuildUi()
        {
            Text = _existing == null ? "Register New Customer" : "Edit Customer";
            Width = 480;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            int y = 20;
            const int labelX = 20, fieldX = 160, fieldWidth = 280, rowHeight = 40;

            AddField("Full Name*", _txtFullName, ref y, labelX, fieldX, fieldWidth, rowHeight);
            AddField("Contact Number*", _txtContact, ref y, labelX, fieldX, fieldWidth, rowHeight);
            AddField("Email", _txtEmail, ref y, labelX, fieldX, fieldWidth, rowHeight);
            AddField("Address", _txtAddress, ref y, labelX, fieldX, fieldWidth, rowHeight);

            Controls.Add(new Label { Text = "Preferences", Location = new Point(labelX, y + 5), AutoSize = true });
            _txtPreferences.Location = new Point(fieldX, y);
            _txtPreferences.Width = fieldWidth;
            _txtPreferences.Height = 60;
            _txtPreferences.Multiline = true;
            _txtPreferences.PlaceholderText = "e.g. no fabric softener, hang-dry delicates...";
            Controls.Add(_txtPreferences);
            y += 70;

            Controls.Add(new Label { Text = "Segment", Location = new Point(labelX, y + 3), AutoSize = true });
            _cmbSegment.Location = new Point(fieldX, y);
            _cmbSegment.Width = fieldWidth;
            _cmbSegment.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbSegment.Items.AddRange(Enum.GetNames(typeof(CustomerSegment)));
            _cmbSegment.SelectedIndex = 0;
            Controls.Add(_cmbSegment);
            y += rowHeight;

            // Orders/spend are system-computed from order history, not hand-edited here
            _lblStats.Location = new Point(labelX, y);
            _lblStats.Width = 420;
            _lblStats.ForeColor = Color.Gray;
            _lblStats.Text = _existing == null
                ? "Orders and total spend will populate automatically once orders are recorded."
                : string.Empty;
            Controls.Add(_lblStats);
            y += rowHeight;

            var btnSave = new Button
            {
                Text = _existing == null ? "Register Customer" : "Save Changes",
                Location = new Point(fieldX, y),
                Width = 180,
                Height = 35,
                BackColor = Color.SeaGreen,
                ForeColor = Color.White
            };
            btnSave.Click += BtnSave_Click;
            Controls.Add(btnSave);

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(fieldX + 190, y),
                Width = 90,
                Height = 35
            };
            btnCancel.Click += (_, _) => Close();
            Controls.Add(btnCancel);
            y += 45;

            _lblStatus.Location = new Point(labelX, y);
            _lblStatus.Width = 420;
            _lblStatus.ForeColor = Color.Firebrick;
            Controls.Add(_lblStatus);
        }

        private void AddField(string label, TextBox box, ref int y, int labelX, int fieldX, int fieldWidth, int rowHeight)
        {
            Controls.Add(new Label { Text = label, Location = new Point(labelX, y + 3), AutoSize = true });
            box.Location = new Point(fieldX, y);
            box.Width = fieldWidth;
            Controls.Add(box);
            y += rowHeight;
        }

        private void PopulateFields(Customer c)
        {
            _txtFullName.Text = c.FullName;
            _txtContact.Text = c.ContactNumber;
            _txtEmail.Text = c.Email;
            _txtAddress.Text = c.Address;
            _txtPreferences.Text = c.Preferences;
            _cmbSegment.SelectedItem = c.Segment.ToString();
            _lblStats.Text = $"Orders: {c.TotalOrders}   Total Spend: {c.TotalSpend:C}   Last Order: " +
                              (c.LastOrderDate.HasValue ? c.LastOrderDate.Value.ToLocalTime().ToString("d") : "-");
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            var customer = _existing ?? new Customer();
            customer.FullName = _txtFullName.Text.Trim();
            customer.ContactNumber = _txtContact.Text.Trim();
            customer.Email = string.IsNullOrWhiteSpace(_txtEmail.Text) ? null : _txtEmail.Text.Trim();
            customer.Address = string.IsNullOrWhiteSpace(_txtAddress.Text) ? null : _txtAddress.Text.Trim();
            customer.Preferences = string.IsNullOrWhiteSpace(_txtPreferences.Text) ? null : _txtPreferences.Text.Trim();
            customer.Segment = Enum.Parse<CustomerSegment>((string)_cmbSegment.SelectedItem!);

            var result = _existing == null
                ? _controller.RegisterCustomer(customer)
                : _controller.UpdateCustomer(customer);

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
