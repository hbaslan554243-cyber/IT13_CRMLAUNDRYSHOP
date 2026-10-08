using CRM.api.Controllers;
using CRM.domain.Enums;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    public class OrderDetailForm : Form
    {
        private readonly OrderController _controller;
        private readonly LaundryShopDbContext _db;
        private readonly int _orderId;

        private readonly Label _lblCustomer = new();
        private readonly Label _lblStatus = new();
        private readonly Label _lblReceived = new();
        private readonly DataGridView _gridItems = new();
        private readonly Label _lblTotal = new();
        private readonly Label _lblPayment = new();
        private readonly Label _lblInstructions = new();
        private readonly Button _btnAdvance = new();
        private readonly Button _btnCancelOrder = new();
        private readonly Button _btnPayment = new();
        private readonly Button _btnPrintReceipt = new();
        private readonly Label _lblMessage = new();

        public OrderDetailForm(LaundryShopDbContext db, int orderId)
        {
            _db = db;
            _controller = new OrderController(db);
            _orderId = orderId;
            BuildUi();
            LoadOrder();
        }

        private void BuildUi()
        {
            Text = "Order Details";
            Width = 520;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            _lblCustomer.Left = 20; _lblCustomer.Top = 20; _lblCustomer.AutoSize = true;
            _lblCustomer.Font = new Font("Segoe UI", 12, FontStyle.Bold);

            _lblStatus.Left = 20; _lblStatus.Top = 50; _lblStatus.AutoSize = true;
            _lblStatus.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            _lblStatus.ForeColor = Color.SteelBlue;

            _lblReceived.Left = 20; _lblReceived.Top = 75; _lblReceived.AutoSize = true;
            _lblReceived.ForeColor = Color.Gray;

            _gridItems.Left = 20; _gridItems.Top = 105; _gridItems.Width = 460; _gridItems.Height = 180;
            _gridItems.ReadOnly = true;
            _gridItems.AllowUserToAddRows = false;
            _gridItems.AllowUserToDeleteRows = false;
            _gridItems.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            _lblTotal.Left = 20; _lblTotal.Top = 295; _lblTotal.AutoSize = true;
            _lblTotal.Font = new Font("Segoe UI", 11, FontStyle.Bold);

            _lblPayment.Left = 20; _lblPayment.Top = 320; _lblPayment.AutoSize = true;
            _lblPayment.ForeColor = Color.DarkSlateGray;

            _lblInstructions.Left = 20; _lblInstructions.Top = 350; _lblInstructions.Width = 460; _lblInstructions.Height = 40;

            _btnAdvance.Left = 20; _btnAdvance.Top = 400; _btnAdvance.Width = 200; _btnAdvance.Height = 35;
            _btnAdvance.BackColor = Color.SeaGreen; _btnAdvance.ForeColor = Color.White;
            _btnAdvance.Click += (_, _) => Advance();

            _btnPayment.Left = 230; _btnPayment.Top = 400; _btnPayment.Width = 140; _btnPayment.Height = 35;
            _btnPayment.Text = "Record Payment";
            _btnPayment.BackColor = Color.SteelBlue; _btnPayment.ForeColor = Color.White;
            _btnPayment.Click += (_, _) => OpenPayment();

            _btnPrintReceipt.Left = 380; _btnPrintReceipt.Top = 400; _btnPrintReceipt.Width = 100; _btnPrintReceipt.Height = 35;
            _btnPrintReceipt.Text = "Receipt PDF";
            _btnPrintReceipt.Click += (_, _) => SaveReceiptPdf();

            _btnCancelOrder.Left = 20; _btnCancelOrder.Top = 445; _btnCancelOrder.Width = 150; _btnCancelOrder.Height = 35;
            _btnCancelOrder.BackColor = Color.Firebrick; _btnCancelOrder.ForeColor = Color.White;
            _btnCancelOrder.Text = "Cancel Order";
            _btnCancelOrder.Click += (_, _) => CancelOrder();

            var btnClose = new Button { Text = "Close", Left = 380, Top = 445, Width = 100, Height = 35 };
            btnClose.Click += (_, _) => Close();

            _lblMessage.Left = 20; _lblMessage.Top = 490; _lblMessage.Width = 460; _lblMessage.ForeColor = Color.Firebrick;

            Controls.AddRange(new Control[]
            {
                _lblCustomer, _lblStatus, _lblReceived, _gridItems, _lblTotal, _lblPayment,
                _lblInstructions, _btnAdvance, _btnPayment, _btnPrintReceipt, _btnCancelOrder, btnClose, _lblMessage
            });
        }

        private void LoadOrder()
        {
            var order = _controller.GetOrder(_orderId);
            if (order == null)
            {
                _lblMessage.Text = "Order not found.";
                _btnAdvance.Enabled = false;
                _btnCancelOrder.Enabled = false;
                return;
            }

            _lblCustomer.Text = order.Customer?.FullName ?? "(unknown customer)";
            _lblStatus.Text = $"Status: {order.Status}";
            _lblReceived.Text = $"Received: {order.ReceivedAt.ToLocalTime():g}" +
                                 (order.ReadyAt.HasValue ? $"   |   Ready: {order.ReadyAt.Value.ToLocalTime():g}" : "") +
                                 (order.CompletedAt.HasValue ? $"   |   Completed: {order.CompletedAt.Value.ToLocalTime():g}" : "");

            _gridItems.DataSource = order.Items.Select(i => new
            {
                Service = i.ServiceName,
                Qty = i.Quantity,
                Unit = i.UnitPrice,
                Subtotal = i.Subtotal
            }).Concat(order.AddOns.Select(a => new
            {
                Service = $"{a.InventoryItem?.Name ?? "Add-on"} (add-on)",
                Qty = a.Quantity,
                Unit = a.UnitPrice,
                Subtotal = a.Subtotal
            })).ToList();

            _lblTotal.Text = $"Total: {order.TotalAmount:N2}";

            var paymentController = new PaymentController(_db);
            var payment = paymentController.GetPaymentForOrder(order.Id);
            _btnPayment.Enabled = payment?.Status != CRM.domain.Enums.PaymentStatus.Paid;
            _btnPrintReceipt.Enabled = payment?.Status == CRM.domain.Enums.PaymentStatus.Paid;
            _lblPayment.Text = payment == null
                ? "Payment: not yet recorded"
                : $"Payment: {payment.Status}   Paid {payment.AmountPaid:N2} of {payment.AmountDue:N2}" +
                  (payment.Balance > 0 ? $"   (Balance: {payment.Balance:N2})" : "");

            _lblInstructions.Text = string.IsNullOrWhiteSpace(order.SpecialInstructions)
                ? "No special instructions."
                : $"Instructions: {order.SpecialInstructions}";

            var isFinal = order.Status is OrderStatus.Completed or OrderStatus.Cancelled;
            _btnAdvance.Enabled = !isFinal;
            _btnCancelOrder.Enabled = !isFinal;
            _btnAdvance.Text = order.Status == OrderStatus.ReadyForPickup ? "Mark as Completed" : "Advance to Next Step";
        }

        private void OpenPayment()
        {
            using var form = new PaymentForm(_db, _orderId);
            form.ShowDialog();
            LoadOrder();
        }

        private void SaveReceiptPdf()
        {
            var order = _controller.GetOrder(_orderId);
            var payment = new PaymentController(_db).GetPaymentForOrder(_orderId);
            var company = order == null ? null : _db.Companies.FirstOrDefault(c => c.Id == order.CompanyId);
            if (order == null || payment?.Status != CRM.domain.Enums.PaymentStatus.Paid || company == null)
            {
                _lblMessage.Text = "A completed payment is required before printing its receipt.";
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Title = "Save Payment Receipt PDF",
                Filter = "PDF file (*.pdf)|*.pdf",
                FileName = $"{payment.ReceiptNumber ?? $"Receipt-{order.Id}"}.pdf",
                AddExtension = true,
                DefaultExt = "pdf"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                ReceiptPdfWriter.Write(dialog.FileName, company, order, payment);
                _lblMessage.ForeColor = Color.SeaGreen;
                _lblMessage.Text = $"Receipt saved: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                _lblMessage.ForeColor = Color.Firebrick;
                _lblMessage.Text = $"Could not save receipt: {ex.Message}";
            }
        }

        private void Advance()
        {
            var result = _controller.AdvanceStatus(_orderId);
            _lblMessage.ForeColor = result.Success ? Color.SeaGreen : Color.Firebrick;
            _lblMessage.Text = result.Message;
            LoadOrder();
        }

        private void CancelOrder()
        {
            var confirm = MessageBox.Show("Cancel this order?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            var result = _controller.CancelOrder(_orderId);
            _lblMessage.ForeColor = result.Success ? Color.SeaGreen : Color.Firebrick;
            _lblMessage.Text = result.Message;
            LoadOrder();
        }
    }
}
