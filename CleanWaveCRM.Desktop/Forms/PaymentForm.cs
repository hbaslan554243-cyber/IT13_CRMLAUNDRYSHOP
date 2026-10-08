using CRM.api.Controllers;
using CRM.domain.Enums;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    public class PaymentForm : Form
    {
        private readonly LaundryShopDbContext _db;
        private readonly PaymentController _controller;
        private readonly LoyaltyController _loyaltyController;
        private readonly OrderController _orderController;
        private readonly int _orderId;
        private readonly int _customerId;
        private decimal _orderTotal;

        private readonly Label _lblInfo = new();
        private readonly Label _lblBalance = new();
        private readonly NumericUpDown _numRedeem = new();
        private readonly Label _lblDiscountPreview = new();
        private readonly NumericUpDown _numAmount = new();
        private readonly ComboBox _cmbMethod = new();
        private readonly Label _lblStatus = new();
        private readonly Button _btnSave = new();
        private decimal _currentDue;
        private decimal _alreadyPaid;

        public PaymentForm(LaundryShopDbContext db, int orderId)
        {
            _db = db;
            _controller = new PaymentController(db);
            _loyaltyController = new LoyaltyController(db);
            _orderController = new OrderController(db);
            _orderId = orderId;
            _customerId = _orderController.GetOrder(orderId)?.CustomerId ?? 0;
            BuildUi();
            LoadInfo();
        }

        private void BuildUi()
        {
            Text = "Record Payment";
            Width = 400;
            Height = 430;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            _lblInfo.Left = 20; _lblInfo.Top = 20; _lblInfo.AutoSize = true; _lblInfo.MaximumSize = new Size(340, 0);
            _lblInfo.Font = new Font("Segoe UI", 9, FontStyle.Bold);

            _lblBalance.Left = 20; _lblBalance.Top = 55; _lblBalance.AutoSize = true; _lblBalance.ForeColor = Color.Goldenrod;

            var lblRedeem = new Label { Text = "Points to Redeem (1 pt = \u20b11)", Left = 20, Top = 85, AutoSize = true };
            _numRedeem.Left = 20; _numRedeem.Top = 105; _numRedeem.Width = 120; _numRedeem.Maximum = 100000;
            _numRedeem.ValueChanged += (_, _) => UpdatePreview();

            _lblDiscountPreview.Left = 150; _lblDiscountPreview.Top = 108; _lblDiscountPreview.AutoSize = true; _lblDiscountPreview.ForeColor = Color.SeaGreen;

            var lblAmount = new Label { Text = "Exact full amount to pay (no partials)", Left = 20, Top = 145, AutoSize = true };
            _numAmount.Left = 20; _numAmount.Top = 165; _numAmount.Width = 150; _numAmount.Maximum = 1000000; _numAmount.DecimalPlaces = 2;

            var lblMethod = new Label { Text = "Method", Left = 20, Top = 200, AutoSize = true };
            _cmbMethod.Left = 20; _cmbMethod.Top = 220; _cmbMethod.Width = 150;
            _cmbMethod.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbMethod.Items.AddRange(Enum.GetNames(typeof(PaymentMethod)));
            _cmbMethod.SelectedIndex = 0;

            _btnSave.Text = "Record Full Payment"; _btnSave.Left = 20; _btnSave.Top = 265; _btnSave.Width = 170; _btnSave.Height = 35; _btnSave.BackColor = Color.SeaGreen; _btnSave.ForeColor = Color.White;
            _btnSave.Click += BtnSave_Click;
            var btnCancel = new Button { Text = "Cancel", Left = 200, Top = 265, Width = 90, Height = 35 };
            btnCancel.Click += (_, _) => Close();

            _lblStatus.Left = 20; _lblStatus.Top = 315; _lblStatus.Width = 340; _lblStatus.Height = 60; _lblStatus.ForeColor = Color.Firebrick;

            Controls.AddRange(new Control[]
            {
                _lblInfo, _lblBalance, lblRedeem, _numRedeem, _lblDiscountPreview,
                lblAmount, _numAmount, lblMethod, _cmbMethod, _btnSave, btnCancel, _lblStatus
            });
        }

        private void LoadInfo()
        {
            var order = _orderController.GetOrder(_orderId);
            _orderTotal = order?.TotalAmount ?? 0m;

            var payment = _controller.GetPaymentForOrder(_orderId);
            _currentDue = payment?.AmountDue ?? _orderTotal;
            _alreadyPaid = payment?.AmountPaid ?? 0m;
            _lblInfo.Text = payment != null
                ? $"Order Total: {_orderTotal:N2}   Due: {payment.AmountDue:N2}   Paid so far: {payment.AmountPaid:N2}   Balance: {payment.Balance:N2}"
                : $"Order Total: {_orderTotal:N2}   No payment recorded yet.";

            if (payment != null && payment.PointsRedeemed > 0)
            {
                _numRedeem.Enabled = false;
                _lblDiscountPreview.Text = $"({payment.PointsRedeemed} pts already redeemed: -{payment.LoyaltyDiscount:N2})";
            }
            if (payment?.Status == CRM.domain.Enums.PaymentStatus.Paid)
            {
                _btnSave.Enabled = false;
                _lblStatus.ForeColor = Color.SeaGreen;
                _lblStatus.Text = $"Already paid. Receipt {payment.ReceiptNumber} can be reopened from Order Details.";
            }

            var balance = _loyaltyController.GetBalance(_customerId);
            _lblBalance.Text = $"Customer's loyalty balance: {balance} points. Enter the exact remaining balance.";
            _numRedeem.Maximum = Math.Min(balance, (int)Math.Floor(Math.Max(0m, _currentDue - _alreadyPaid)));

            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var discount = _numRedeem.Enabled ? _numRedeem.Value : 0m;
            var netDue = Math.Max(0, _currentDue - discount);
            var exactAmount = Math.Max(0, netDue - _alreadyPaid);
            _numAmount.Value = Math.Min(_numAmount.Maximum, exactAmount);
            _lblDiscountPreview.Text = discount > 0 ? $"-{discount:N2}   (New total: {netDue:N2})" : "";
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            var method = Enum.Parse<PaymentMethod>(_cmbMethod.SelectedItem!.ToString()!);
            var pointsToRedeem = (int)_numRedeem.Value;
            var result = _controller.RecordPayment(_orderId, _numAmount.Value, method, pointsToRedeem);

            if (!result.Success) { _lblStatus.Text = result.Message; return; }

            var payment = _controller.GetPaymentForOrder(_orderId);
            var order = _orderController.GetOrder(_orderId);
            var company = order == null ? null : _db.Companies.FirstOrDefault(c => c.Id == order.CompanyId);
            if (payment != null && order != null && company != null)
            {
                using var saveDialog = new SaveFileDialog
                {
                    Title = "Save Payment Receipt PDF",
                    Filter = "PDF file (*.pdf)|*.pdf",
                    FileName = $"{payment.ReceiptNumber}.pdf",
                    AddExtension = true,
                    DefaultExt = "pdf"
                };
                if (saveDialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        ReceiptPdfWriter.Write(saveDialog.FileName, company, order, payment);
                        MessageBox.Show($"Payment recorded. Receipt saved to:\n{saveDialog.FileName}", "Receipt Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Payment was recorded, but the receipt PDF could not be saved:\n{ex.Message}", "Receipt Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
