using CRM.api.Controllers;
using CRM.domain.Enums;
using CRM.infrastructure.data;

namespace CleanWaveCRM.Desktop.Forms
{
    /// <summary>
    /// The Admin/Owner's homepage. Every card is clickable and opens a
    /// dialog showing the value, the exact date/time period used, the
    /// formula/calculation, and the actual database records behind it -
    /// nothing here is hardcoded, everything is computed live per click.
    /// </summary>
    public class DashboardView : UserControl
    {
        private readonly LaundryShopDbContext _db;
        private readonly ReportController _reportController;
        private readonly OrderController _orderController;
        private readonly SettingsController _settingsController;
        private readonly DataGridView _gridRecent = new();

        public DashboardView(LaundryShopDbContext db)
        {
            _db = db;
            _reportController = new ReportController(db);
            _orderController = new OrderController(db);
            _settingsController = new SettingsController(db);
            BuildUi();
            LoadData();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            AutoScroll = true;
            BackColor = UiTheme.Background;

            var lblTitle = new Label { Text = "Dashboard", Font = UiTheme.TitleFont, ForeColor = UiTheme.TextDark, AutoSize = true, Left = 25, Top = 20 };
            Controls.Add(lblTitle);

            var settings = _settingsController.GetMyCompany();
            var lblPlan = new Label { Text = settings.ShopName, Font = UiTheme.BodyFont, ForeColor = UiTheme.TextMuted, AutoSize = true, Left = 25, Top = 50 };
            Controls.Add(lblPlan);

            _cardOrders = CreateStatCard("Today's Orders", "0", UiTheme.Primary, 25, 80, ShowTodaysOrders);
            _cardPending = CreateStatCard("Pending Tasks", "0", Color.FromArgb(255, 183, 77), 235, 80, ShowPendingTasks);
            _cardEarnings = CreateStatCard("Today's Earnings", "0.00", UiTheme.Success, 445, 80, ShowTodaysEarnings);
            _cardLowStock = CreateStatCard("Low Stock Alerts", "0", UiTheme.Danger, 655, 80, ShowLowStock);

            var lblKpis = new Label { Text = "Key Performance Indicators", Font = UiTheme.HeaderFont, ForeColor = UiTheme.TextDark, AutoSize = true, Left = 25, Top = 220 };
            Controls.Add(lblKpis);

            _cardAov = CreateStatCard("Avg Order Value", "0.00", UiTheme.Primary, 25, 250, ShowAverageOrderValue);
            _cardRepeat = CreateStatCard("Repeat Customers", "0%", Color.MediumPurple, 235, 250, ShowRetentionDetails);
            _cardCompletion = CreateStatCard("Order Completion", "0%", UiTheme.Success, 445, 250, ShowCompletionBreakdown);
            _cardRating = CreateStatCard("Avg Rating", "N/A", Color.Goldenrod, 655, 250, ShowRatedFeedback);
            _cardNewCustomers = CreateStatCard("New Customers (Month)", "0", UiTheme.PrimaryDark, 865, 250, ShowNewCustomers);

            var lblRecent = new Label { Text = "Recent Orders", Font = UiTheme.HeaderFont, ForeColor = UiTheme.TextDark, AutoSize = true, Left = 25, Top = 390 };
            Controls.Add(lblRecent);

            _gridRecent.Left = 25;
            _gridRecent.Top = 420;
            _gridRecent.Width = 1030;
            _gridRecent.Height = 220;
            _gridRecent.ReadOnly = true;
            _gridRecent.AllowUserToAddRows = false;
            _gridRecent.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridRecent.BackgroundColor = Color.White;
            _gridRecent.BorderStyle = BorderStyle.FixedSingle;
            _gridRecent.CellDoubleClick += (_, e) =>
            {
                if (e.RowIndex >= 0 && int.TryParse(_gridRecent.Rows[e.RowIndex].Cells["Id"].Value?.ToString(), out var orderId))
                    OpenOrderDetail(orderId);
            };
            Controls.Add(_gridRecent);
        }

        private Panel _cardOrders = null!, _cardPending = null!, _cardEarnings = null!, _cardLowStock = null!;
        private Panel _cardAov = null!, _cardRepeat = null!, _cardCompletion = null!, _cardRating = null!, _cardNewCustomers = null!;

        private Panel CreateStatCard(string title, string value, Color accent, int left, int top, Action onClick)
        {
            var card = new Panel { Left = left, Top = top, Width = 190, Height = 120, BackColor = Color.White, Cursor = Cursors.Hand };
            var accentBar = new Panel { Dock = DockStyle.Top, Height = 6, BackColor = accent, Cursor = Cursors.Hand };
            var lblTitle = new Label { Text = title, Font = new Font("Segoe UI", 9), ForeColor = Color.Gray, Dock = DockStyle.Top, Height = 30, Padding = new Padding(12, 10, 0, 0), Cursor = Cursors.Hand };
            var lblValue = new Label
            {
                Name = "Value", Text = value, Font = new Font("Segoe UI", 22, FontStyle.Bold), ForeColor = UiTheme.TextDark,
                Dock = DockStyle.Fill, Padding = new Padding(12, 0, 0, 10), TextAlign = ContentAlignment.MiddleLeft, Cursor = Cursors.Hand
            };

            void Handler(object? s, EventArgs e) => onClick();
            card.Click += Handler; accentBar.Click += Handler; lblTitle.Click += Handler; lblValue.Click += Handler;

            card.Controls.Add(lblValue);
            card.Controls.Add(lblTitle);
            card.Controls.Add(accentBar);
            Controls.Add(card);
            return card;
        }

        private static void SetCardValue(Panel card, string value)
        {
            foreach (Control c in card.Controls)
                if (c.Name == "Value") { c.Text = value; return; }
        }

        private void LoadData()
        {
            var summary = _reportController.GetTodaySummary();
            SetCardValue(_cardOrders, summary.TodaysOrders.ToString());
            SetCardValue(_cardPending, summary.PendingTasks.ToString());
            SetCardValue(_cardEarnings, summary.TodaysEarnings.ToString("N2"));
            SetCardValue(_cardLowStock, summary.LowStockCount.ToString());

            var kpis = _reportController.GetKpis();
            SetCardValue(_cardAov, kpis.AverageOrderValue.ToString("N2"));
            SetCardValue(_cardRepeat, $"{kpis.RepeatCustomerRate:N0}%");
            SetCardValue(_cardCompletion, $"{kpis.OrderCompletionRate:N0}%");
            SetCardValue(_cardRating, kpis.AverageRating.HasValue ? $"{kpis.AverageRating:N1} \u2605" : "N/A");
            SetCardValue(_cardNewCustomers, kpis.NewCustomersThisMonth.ToString());

            var recent = _orderController.GetAllOrders().Take(10).ToList();
            _gridRecent.DataSource = recent.Select(o => new
            {
                o.Id,
                Customer = o.Customer?.FullName ?? "(unknown)",
                o.Status,
                Total = o.TotalAmount,
                Received = o.ReceivedAt.ToLocalTime().ToString("g")
            }).ToList();
        }

        private void OpenOrderDetail(int orderId)
        {
            using var form = new OrderDetailForm(_db, orderId);
            form.ShowDialog();
            LoadData();
        }

        // ---- Card drill-downs: value, period, formula, and real records ----

        private void ShowTodaysOrders()
        {
            var today = DateTime.UtcNow.Date;
            var orders = _orderController.GetAllOrders().Where(o => o.ReceivedAt.Date == today).ToList();
            var rows = orders.Select(o => new
            {
                Order = $"#{o.Id}",
                Customer = o.Customer?.FullName ?? "(unknown)",
                o.Status,
                Total = o.TotalAmount,
                Received = o.ReceivedAt.ToLocalTime().ToString("t")
            }).ToList();
            new SimpleListDialog("Today's Orders", orders.Count.ToString(),
                DateTime.Now.ToString("MMMM d, yyyy"),
                "Count of orders where Received Date = today (all statuses, including Cancelled)",
                rows, "No orders received today yet.").ShowDialog();
        }

        private void ShowPendingTasks()
        {
            var orders = _orderController.GetAllOrders().Where(o => o.Status is OrderStatus.Pending or OrderStatus.Ongoing).ToList();
            var rows = orders.Select(o => new
            {
                Order = $"#{o.Id}",
                Customer = o.Customer?.FullName ?? "(unknown)",
                o.Status,
                Total = o.TotalAmount,
                Received = o.ReceivedAt.ToLocalTime().ToString("g")
            }).ToList();
            new SimpleListDialog("Pending Tasks", orders.Count.ToString(),
                "All time (not date-limited)",
                "Count of orders with Status = Pending or Ongoing",
                rows, "Nothing pending right now.").ShowDialog();
        }

        private void ShowTodaysEarnings()
        {
            var today = DateTime.UtcNow.Date;
            var payments = new PaymentController(_db).GetRecentPayments(500)
                .Where(p => p.PaidAt.HasValue && p.PaidAt.Value.Date == today).ToList();
            var total = payments.Sum(p => p.AmountPaid);
            var rows = BuildTransactionRows(payments);
            new SimpleListDialog("Today's Earnings", total.ToString("N2"),
                DateTime.Now.ToString("MMMM d, yyyy"),
                "Sum of AmountPaid for Paid transactions today, on orders that are NOT Cancelled",
                rows, "No payments recorded today yet.").ShowDialog();
        }

        private void ShowLowStock()
        {
            var items = new InventoryController(_db).GetLowStockItems();
            var rows = items.Select(i => new
            {
                i.Name,
                Current = i.QuantityOnHand,
                Minimum = i.ReorderThreshold,
                i.Unit,
                Status = "Low Stock"
            }).ToList();
            new SimpleListDialog("Low Stock Alerts", items.Count.ToString(),
                "Live snapshot (not date-limited)",
                "Items where Current Stock <= Reorder Threshold",
                rows, "No low-stock items found.").ShowDialog();
        }

        private void ShowAverageOrderValue()
        {
            var kpis = _reportController.GetKpis();
            var payments = new PaymentController(_db).GetRecentPayments(200);
            var rows = BuildTransactionRows(payments);
            new SimpleListDialog("Average Order Value", kpis.AverageOrderValue.ToString("N2"),
                "All time (not date-limited)",
                $"Total Valid Revenue ({kpis.TotalValidRevenue:N2}) \u00f7 Number of Valid Transactions ({kpis.ValidOrderCount}) = {kpis.AverageOrderValue:N2}",
                rows, "No paid transactions yet.").ShowDialog();
        }

        private void ShowRetentionDetails() => new CustomerRetentionDetailsForm(_db).ShowDialog();

        private void ShowCompletionBreakdown()
        {
            var kpis = _reportController.GetKpis();
            var orders = _orderController.GetAllOrders().Where(o => o.Status is OrderStatus.Completed or OrderStatus.Cancelled).ToList();
            var rows = orders.Select(o => new
            {
                Order = $"#{o.Id}",
                Customer = o.Customer?.FullName ?? "(unknown)",
                o.Status,
                Total = o.TotalAmount,
                Received = o.ReceivedAt.ToLocalTime().ToString("g")
            }).ToList();
            new SimpleListDialog("Order Completion", $"{kpis.OrderCompletionRate:N0}%",
                "All time (not date-limited)",
                $"Completed ({kpis.CompletedCount}) \u00f7 [Completed + Cancelled] ({kpis.CompletedCount + kpis.CancelledCount}) = {kpis.OrderCompletionRate:N0}%. Pending/Ongoing orders aren't counted either way until they finish.",
                rows, "No finished orders yet.").ShowDialog();
        }

        private void ShowRatedFeedback()
        {
            var feedback = new FeedbackController(_db).GetAll().Where(f => f.Rating.HasValue).ToList();
            var kpis = _reportController.GetKpis();
            var breakdown = string.Join("  ", Enumerable.Range(1, 5).Reverse()
                .Select(star => $"{star}\u2605={feedback.Count(f => f.Rating == star)}"));
            var rows = feedback.OrderByDescending(f => f.CreatedAt).Select(f => new
            {
                Customer = f.Customer?.FullName ?? "(unknown)",
                Rating = new string('\u2605', f.Rating!.Value) + new string('\u2606', 5 - f.Rating.Value),
                f.Message,
                Logged = f.CreatedAt.ToLocalTime().ToString("g")
            }).ToList();
            new SimpleListDialog("Average Rating", kpis.AverageRating.HasValue ? $"{kpis.AverageRating:N2} \u2605" : "N/A",
                "All time (not date-limited)",
                $"Average of all star ratings ({kpis.RatingCount} total). Breakdown: {breakdown}",
                rows, "No ratings submitted yet.").ShowDialog();
        }

        private void ShowNewCustomers()
        {
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var customers = new CustomerController(_db).GetAllCustomers().Where(c => c.CreatedAt >= monthStart).ToList();
            var rows = customers.Select(c => new
            {
                c.FullName,
                c.ContactNumber,
                Registered = c.CreatedAt.ToLocalTime().ToString("g"),
                c.TotalOrders,
                c.TotalSpend
            }).ToList();
            new SimpleListDialog("New Customers This Month", customers.Count.ToString(),
                $"{monthStart:MMMM yyyy} to present",
                "Count of customers where Registered Date falls within the current calendar month",
                rows, "No new customers registered this month yet.").ShowDialog();
        }

        private static List<object> BuildTransactionRows(IEnumerable<CRM.domain.Entities.Tenant.Payment> payments) =>
            payments.Select(p => (object)new
            {
                Transaction = p.ReceiptNumber ?? $"PMT-{p.Id}",
                Order = $"#{p.LaundryOrderId}",
                Customer = p.LaundryOrder?.Customer?.FullName ?? "(unknown)",
                Subtotal = p.AmountDue + p.LoyaltyDiscount,
                LoyaltyDiscount = p.LoyaltyDiscount,
                PointsRedeemed = p.PointsRedeemed,
                FinalAmount = p.AmountPaid,
                p.Method,
                Date = p.PaidAt.HasValue ? p.PaidAt.Value.ToLocalTime().ToString("g") : "-"
            }).ToList();
    }
}
