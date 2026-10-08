using CRM.api.Controllers;
using CRM.domain.Enums;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    public class FeedbackCreateForm : Form
    {
        private readonly FeedbackController _controller;
        private readonly ComboBox _cmbCustomer = new();
        private readonly ComboBox _cmbType = new();
        private readonly TextBox _txtMessage = new();
        private readonly Label[] _stars = new Label[5];
        private readonly Label _lblRatingValue = new();
        private readonly Label _lblStatus = new();
        private int? _selectedRating;

        public FeedbackCreateForm(LaundryShopDbContext db)
        {
            _controller = new FeedbackController(db);
            var customerController = new CustomerController(db);
            BuildUi();

            var customers = customerController.GetAllCustomers()
                .Select(c => new { c.Id, Display = $"{c.FullName} ({c.ContactNumber})" }).ToList();
            _cmbCustomer.DataSource = customers;
        }

        private void BuildUi()
        {
            Text = "Log Feedback / Complaint";
            Width = 420;
            Height = 400;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var lblCustomer = new Label { Text = "Customer*", Left = 20, Top = 20, AutoSize = true };
            _cmbCustomer.Left = 130; _cmbCustomer.Top = 18; _cmbCustomer.Width = 260;
            _cmbCustomer.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbCustomer.DisplayMember = "Display";
            _cmbCustomer.ValueMember = "Id";

            var lblType = new Label { Text = "Type", Left = 20, Top = 55, AutoSize = true };
            _cmbType.Left = 130; _cmbType.Top = 53; _cmbType.Width = 260;
            _cmbType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbType.Items.AddRange(Enum.GetNames(typeof(FeedbackType)));
            _cmbType.SelectedIndex = 0;

            var lblRating = new Label { Text = "Satisfaction Rating", Left = 20, Top = 90, AutoSize = true };
            for (int i = 0; i < 5; i++)
            {
                var star = new Label
                {
                    Text = "\u2606", // ☆ empty star; filled to ★ when selected
                    Font = new Font("Segoe UI", 18),
                    ForeColor = Color.Gray,
                    Left = 130 + i * 30,
                    Top = 105,
                    Width = 28,
                    Height = 30,
                    Cursor = Cursors.Hand,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Tag = i + 1
                };
                var starValue = i + 1;
                star.Click += (_, _) => SetRating(starValue);
                _stars[i] = star;
            }

            _lblRatingValue.Left = 130 + 5 * 30 + 10;
            _lblRatingValue.Top = 110;
            _lblRatingValue.AutoSize = true;
            _lblRatingValue.ForeColor = Color.Gray;
            _lblRatingValue.Text = "(optional)";

            var lblMessage = new Label { Text = "Message*", Left = 20, Top = 150, AutoSize = true };
            _txtMessage.Left = 20; _txtMessage.Top = 170; _txtMessage.Width = 370; _txtMessage.Height = 100;
            _txtMessage.Multiline = true;

            var btnSave = new Button { Text = "Submit", Left = 20, Top = 285, Width = 150, Height = 35, BackColor = Color.SeaGreen, ForeColor = Color.White };
            btnSave.Click += BtnSave_Click;
            var btnCancel = new Button { Text = "Cancel", Left = 180, Top = 285, Width = 90, Height = 35 };
            btnCancel.Click += (_, _) => Close();

            _lblStatus.Left = 20; _lblStatus.Top = 330; _lblStatus.Width = 370; _lblStatus.ForeColor = Color.Firebrick;

            Controls.AddRange(new Control[] { lblCustomer, _cmbCustomer, lblType, _cmbType, lblRating });
            Controls.AddRange(_stars);
            Controls.AddRange(new Control[] { _lblRatingValue, lblMessage, _txtMessage, btnSave, btnCancel, _lblStatus });
        }

        private void SetRating(int value)
        {
            _selectedRating = value;
            for (int i = 0; i < 5; i++)
            {
                _stars[i].Text = i < value ? "\u2605" : "\u2606"; // ★ filled up to the clicked star, ☆ after
                _stars[i].ForeColor = i < value ? Color.Goldenrod : Color.Gray;
            }
            _lblRatingValue.Text = $"{value}/5";
            _lblRatingValue.ForeColor = Color.Goldenrod;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (_cmbCustomer.SelectedValue == null) { _lblStatus.Text = "Please select a customer."; return; }

            var customerId = (int)_cmbCustomer.SelectedValue;
            var type = Enum.Parse<FeedbackType>(_cmbType.SelectedItem!.ToString()!);
            var result = _controller.SubmitFeedback(customerId, null, type, _txtMessage.Text, _selectedRating);

            if (!result.Success) { _lblStatus.Text = result.Message; return; }

            MessageBox.Show(result.Message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
