using CRM.api.Controllers;
using CRM.domain.Entities.Tenant;
using CRM.domain.Enums;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    public class PromotionCreateForm : Form
    {
        private readonly PromotionController _controller;
        private readonly int? _targetCustomerId;
        private readonly string? _targetCustomerName;

        private readonly TextBox _txtTitle = new();
        private readonly TextBox _txtDescription = new();
        private readonly ComboBox _cmbSegment = new();
        private readonly ComboBox _cmbReason = new();
        private readonly TextBox _txtReasonNotes = new();
        private readonly ComboBox _cmbDiscountType = new();
        private readonly NumericUpDown _numValue = new();
        private readonly DateTimePicker _dtStart = new();
        private readonly DateTimePicker _dtEnd = new();
        private readonly Label _lblStatus = new();

        /// <param name="targetCustomerId">If set, this promotion is for one specific customer (e.g. launched from Customer Retention Details) - segment targeting is hidden.</param>
        public PromotionCreateForm(LaundryShopDbContext db, int? targetCustomerId = null, string? targetCustomerName = null)
        {
            _controller = new PromotionController(db);
            _targetCustomerId = targetCustomerId;
            _targetCustomerName = targetCustomerName;
            BuildUi();

            if (_targetCustomerId.HasValue)
            {
                var suggested = _controller.SuggestReason(_targetCustomerId.Value);
                SelectReason(suggested);
            }
        }

        private void BuildUi()
        {
            Text = _targetCustomerId.HasValue ? $"New Promotion for {_targetCustomerName}" : "New Promotion";
            Width = 460;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            int y = 20;
            var lblTitle = new Label { Text = "Title*", Left = 20, Top = y, AutoSize = true };
            _txtTitle.Left = 150; _txtTitle.Top = y - 3; _txtTitle.Width = 260;
            y += 35;

            var lblDesc = new Label { Text = "Description", Left = 20, Top = y, AutoSize = true };
            _txtDescription.Left = 150; _txtDescription.Top = y - 3; _txtDescription.Width = 260; _txtDescription.Height = 45; _txtDescription.Multiline = true;
            y += 55;

            Label lblSegment;
            if (_targetCustomerId.HasValue)
            {
                lblSegment = new Label { Text = "Target", Left = 20, Top = y, AutoSize = true };
                var lblFixedTarget = new Label { Text = _targetCustomerName ?? "(customer)", Left = 150, Top = y, AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
                Controls.Add(lblFixedTarget);
            }
            else
            {
                lblSegment = new Label { Text = "Target Segment", Left = 20, Top = y, AutoSize = true };
                _cmbSegment.Left = 150; _cmbSegment.Top = y - 3; _cmbSegment.Width = 260;
                _cmbSegment.DropDownStyle = ComboBoxStyle.DropDownList;
                _cmbSegment.Items.Add("Everyone");
                _cmbSegment.Items.AddRange(Enum.GetNames(typeof(CustomerSegment)));
                _cmbSegment.SelectedIndex = 0;
                Controls.Add(_cmbSegment);
            }
            y += 35;

            var lblReason = new Label { Text = "Reason*", Left = 20, Top = y, AutoSize = true };
            var lblReasonHint = _targetCustomerId.HasValue
                ? new Label { Text = "Auto-suggested from this customer's order history.", Left = 150, Top = y, AutoSize = true, ForeColor = Color.Gray, Font = new Font("Segoe UI", 7.5f) }
                : new Label { Text = "", Left = 150, Top = y, AutoSize = true };
            _cmbReason.Left = 150; _cmbReason.Top = y + 15; _cmbReason.Width = 260;
            _cmbReason.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (var reason in Enum.GetValues<PromotionReason>())
                _cmbReason.Items.Add(SplitCamelCase(reason.ToString()));
            _cmbReason.SelectedIndex = 0;
            y += 50;

            var lblReasonNotes = new Label { Text = "Reason Notes", Left = 20, Top = y, AutoSize = true };
            _txtReasonNotes.Left = 150; _txtReasonNotes.Top = y - 3; _txtReasonNotes.Width = 260; _txtReasonNotes.Height = 40; _txtReasonNotes.Multiline = true;
            y += 50;

            var lblDiscType = new Label { Text = "Discount Type", Left = 20, Top = y, AutoSize = true };
            _cmbDiscountType.Left = 150; _cmbDiscountType.Top = y - 3; _cmbDiscountType.Width = 260;
            _cmbDiscountType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbDiscountType.Items.AddRange(Enum.GetNames(typeof(PromotionDiscountType)));
            _cmbDiscountType.SelectedIndex = 0;
            y += 35;

            var lblValue = new Label { Text = "Discount Value", Left = 20, Top = y, AutoSize = true };
            _numValue.Left = 150; _numValue.Top = y - 3; _numValue.Width = 100; _numValue.Maximum = 100000; _numValue.DecimalPlaces = 2;
            y += 35;

            var lblStart = new Label { Text = "Start Date", Left = 20, Top = y, AutoSize = true };
            _dtStart.Left = 150; _dtStart.Top = y - 3; _dtStart.Width = 200;
            y += 35;

            var lblEnd = new Label { Text = "End Date", Left = 20, Top = y, AutoSize = true };
            _dtEnd.Left = 150; _dtEnd.Top = y - 3; _dtEnd.Width = 200;
            _dtEnd.Value = DateTime.Now.AddDays(30);
            y += 45;

            var btnSave = new Button { Text = "Create Promotion", Left = 20, Top = y, Width = 180, Height = 35, BackColor = Color.SeaGreen, ForeColor = Color.White };
            btnSave.Click += BtnSave_Click;
            var btnCancel = new Button { Text = "Cancel", Left = 210, Top = y, Width = 90, Height = 35 };
            btnCancel.Click += (_, _) => Close();
            y += 45;

            _lblStatus.Left = 20; _lblStatus.Top = y; _lblStatus.Width = 400; _lblStatus.ForeColor = Color.Firebrick;

            Controls.AddRange(new Control[]
            {
                lblTitle, _txtTitle, lblDesc, _txtDescription, lblSegment,
                lblReason, lblReasonHint, _cmbReason, lblReasonNotes, _txtReasonNotes,
                lblDiscType, _cmbDiscountType, lblValue, _numValue, lblStart, _dtStart,
                lblEnd, _dtEnd, btnSave, btnCancel, _lblStatus
            });
        }

        private void SelectReason(PromotionReason reason)
        {
            var text = SplitCamelCase(reason.ToString());
            var index = _cmbReason.Items.IndexOf(text);
            if (index >= 0) _cmbReason.SelectedIndex = index;
        }

        private static string SplitCamelCase(string value) =>
            System.Text.RegularExpressions.Regex.Replace(value, "(?<!^)([A-Z])", " $1");

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_txtTitle.Text)) { _lblStatus.Text = "Title is required."; return; }

            var reasonValues = Enum.GetValues<PromotionReason>();
            var reason = reasonValues[_cmbReason.SelectedIndex];

            var promo = new Promotion
            {
                Title = _txtTitle.Text.Trim(),
                Description = string.IsNullOrWhiteSpace(_txtDescription.Text) ? null : _txtDescription.Text.Trim(),
                TargetCustomerId = _targetCustomerId,
                TargetSegment = _targetCustomerId.HasValue
                    ? null
                    : (_cmbSegment.SelectedIndex == 0 ? null : Enum.Parse<CustomerSegment>(_cmbSegment.SelectedItem!.ToString()!)),
                Reason = reason,
                ReasonNotes = string.IsNullOrWhiteSpace(_txtReasonNotes.Text) ? null : _txtReasonNotes.Text.Trim(),
                DiscountType = Enum.Parse<PromotionDiscountType>(_cmbDiscountType.SelectedItem!.ToString()!),
                DiscountValue = _numValue.Value,
                StartDate = _dtStart.Value.ToUniversalTime(),
                EndDate = _dtEnd.Value.ToUniversalTime(),
                IsActive = true,
                CreatedByUserId = AuthController.CurrentUser?.Id ?? string.Empty
            };

            var result = _controller.CreatePromotion(promo);
            if (!result.Success) { _lblStatus.Text = result.Message; return; }

            MessageBox.Show(result.Message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
