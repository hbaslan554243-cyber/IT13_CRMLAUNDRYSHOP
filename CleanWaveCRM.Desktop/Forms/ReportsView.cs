using CRM.api.Controllers;
using CRM.infrastructure.data;
using System.Windows.Forms.DataVisualization.Charting;
using CRM.domain.Entities.Tenant;

namespace CleanWaveCRM.Desktop.Forms
{
    /// <summary>
    /// Analytics & Reports (Feature 9), rebuilt as actual charts rather
    /// than plain grids: Revenue (today/week/month/year), Orders over
    /// time, Order Status breakdown, Top Services, and Payment Methods -
    /// plus a Customer Segments chart, since it's the same kind of
    /// at-a-glance business insight and the data was already there.
    /// </summary>
    public class ReportsView : UserControl
    {
        private readonly ReportController _controller;
        private readonly LaundryShopDbContext _db;
        private readonly Label _lblSummary = new();
        private readonly Label _lblKpis = new();
        private readonly bool _dateRangeEnabled;
        private readonly DateTimePicker _fromDate = new();
        private readonly DateTimePicker _toDate = new();
        private ReportController.FilteredReport? _currentReport;

        public ReportsView(LaundryShopDbContext db, string? tenantKey = null)
        {
            _db = db;
            _controller = new ReportController(db);
            _dateRangeEnabled = tenantKey is not null
                && (tenantKey.Equals("tenant-b", StringComparison.OrdinalIgnoreCase)
                    || tenantKey.Equals("tenant-c", StringComparison.OrdinalIgnoreCase));
            BuildUi();
            LoadData();
        }

        private void BuildUi()
        {
            Dock = DockStyle.Fill;
            AutoScroll = true;
            BackColor = UiTheme.Background;

            var lblTitle = new Label
            {
                Text = "Analytics & Reports",
                Font = UiTheme.TitleFont,
                ForeColor = UiTheme.TextDark,
                AutoSize = true,
                Left = 20,
                Top = 20
            };
            Controls.Add(lblTitle);

            if (_dateRangeEnabled)
            {
                var fromLabel = new Label { Text = "From:", Left = 20, Top = 57, AutoSize = true, ForeColor = UiTheme.TextDark };
                _fromDate.Left = 65; _fromDate.Top = 52; _fromDate.Width = 145; _fromDate.Format = DateTimePickerFormat.Short;
                _fromDate.Value = DateTime.Today.AddDays(-29);
                var toLabel = new Label { Text = "To:", Left = 225, Top = 57, AutoSize = true, ForeColor = UiTheme.TextDark };
                _toDate.Left = 255; _toDate.Top = 52; _toDate.Width = 145; _toDate.Format = DateTimePickerFormat.Short;
                var apply = new Button { Text = "Apply Filter", Left = 415, Top = 51, Width = 105, Height = 28 };
                apply.Click += (_, _) => LoadData();
                var export = new Button { Text = "Export PDF", Left = 530, Top = 51, Width = 105, Height = 28 };
                export.Click += (_, _) => ExportFilteredPdf();
                Controls.AddRange(new Control[] { fromLabel, _fromDate, toLabel, _toDate, apply, export });
            }

            _lblSummary.Left = 20;
            _lblSummary.Top = _dateRangeEnabled ? 89 : 55;
            _lblSummary.AutoSize = true;
            _lblSummary.MaximumSize = new Size(950, 0);
            _lblSummary.Font = UiTheme.BodyFont;
            _lblSummary.ForeColor = UiTheme.TextMuted;
            Controls.Add(_lblSummary);

            _lblKpis.Left = 20;
            _lblKpis.Top = _dateRangeEnabled ? 112 : 78;
            _lblKpis.AutoSize = true;
            _lblKpis.MaximumSize = new Size(950, 0);
            _lblKpis.Font = new Font(UiTheme.BodyFont, FontStyle.Bold);
            _lblKpis.ForeColor = UiTheme.PrimaryDark;
            Controls.Add(_lblKpis);

            const int chartWidth = 460;
            const int chartHeight = 300;
            const int leftCol = 20;
            const int rightCol = leftCol + chartWidth + 20;
            int row1 = _dateRangeEnabled ? 150 : 115;
            int row2 = row1 + chartHeight + 30;
            int row3 = row2 + chartHeight + 30;

            _revenueChart = CreateChartPanel(_dateRangeEnabled ? "Revenue by Day (Selected Range)" : "Revenue (Today / Week / Month / Year)", leftCol, row1, chartWidth, chartHeight, SeriesChartType.Column);
            _ordersChart = CreateChartPanel(_dateRangeEnabled ? "Orders by Day (Selected Range)" : "Orders Over Time (Last 30 Days)", rightCol, row1, chartWidth, chartHeight, SeriesChartType.Line);
            _statusChart = CreateChartPanel("Order Status", leftCol, row2, chartWidth, chartHeight, SeriesChartType.Pie);
            _servicesChart = CreateChartPanel("Top Services", rightCol, row2, chartWidth, chartHeight, SeriesChartType.Bar);
            _paymentChart = CreateChartPanel("Payment Methods (Revenue Share)", leftCol, row3, chartWidth, chartHeight, SeriesChartType.Pie);
            _segmentChart = CreateChartPanel("Customer Segments", rightCol, row3, chartWidth, chartHeight, SeriesChartType.Doughnut);
        }

        private Chart _revenueChart = null!, _ordersChart = null!, _statusChart = null!,
                      _servicesChart = null!, _paymentChart = null!, _segmentChart = null!;

        /// <summary>
        /// Builds a titled card containing one Chart control, styled to
        /// match the app's soft blue/white theme rather than the chart
        /// library's harsh defaults.
        /// </summary>
        private Chart CreateChartPanel(string title, int left, int top, int width, int height, SeriesChartType type)
        {
            var card = new Panel
            {
                Left = left,
                Top = top,
                Width = width,
                Height = height,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblTitle = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = UiTheme.TextDark,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };

            var chart = new Chart { Dock = DockStyle.Fill, BackColor = Color.White };
            var area = new ChartArea("Main")
            {
                BackColor = Color.White
            };
            area.AxisX.LineColor = Color.LightGray;
            area.AxisY.LineColor = Color.LightGray;
            area.AxisX.MajorGrid.LineColor = Color.WhiteSmoke;
            area.AxisY.MajorGrid.LineColor = Color.WhiteSmoke;
            chart.ChartAreas.Add(area);

            var legend = new Legend { Docking = Docking.Bottom, Alignment = StringAlignment.Center };
            chart.Legends.Add(legend);

            var series = new Series
            {
                ChartType = type,
                ChartArea = "Main",
                Palette = ChartColorPalette.Pastel,
                IsValueShownAsLabel = type is SeriesChartType.Pie or SeriesChartType.Doughnut
            };
            if (type == SeriesChartType.Column || type == SeriesChartType.Bar)
                series.Color = UiTheme.Primary;
            if (type == SeriesChartType.Line)
            {
                series.Color = UiTheme.PrimaryDark;
                series.BorderWidth = 3;
                series.MarkerStyle = MarkerStyle.Circle;
                series.MarkerSize = 5;
            }
            chart.Series.Add(series);

            card.Controls.Add(chart);
            card.Controls.Add(lblTitle);
            Controls.Add(card);
            return chart;
        }

        private void LoadData()
        {
            if (_dateRangeEnabled)
            {
                LoadFilteredData();
                return;
            }

            LoadAllTimeData();
        }

        private void LoadAllTimeData()
        {
            var dashboard = _controller.GetDashboard();
            _lblSummary.Text = $"Total Customers: {dashboard.TotalCustomers}     Total Orders: {dashboard.TotalOrders}     " +
                                $"Total Sales: {dashboard.TotalSales:N2}     Open Feedback: {dashboard.OpenFeedback}     Pending Payments: {dashboard.PendingPayments}";

            var kpis = _controller.GetKpis();
            var ratingText = kpis.AverageRating.HasValue ? $"{kpis.AverageRating:N1} \u2605 ({kpis.RatingCount} ratings)" : "No ratings yet";
            _lblKpis.Text = $"KPIs \u2014 Avg Order Value: {kpis.AverageOrderValue:N2}     Repeat Customers: {kpis.RepeatCustomerRate:N0}%     " +
                             $"Order Completion: {kpis.OrderCompletionRate:N0}%     Avg Rating: {ratingText}     New Customers This Month: {kpis.NewCustomersThisMonth}";

            // --- Revenue by period ---
            var revenue = _controller.GetRevenueByPeriod();
            _revenueChart.Series[0].Points.Clear();
            _revenueChart.Series[0].Points.AddXY("Today", revenue.Today);
            _revenueChart.Series[0].Points.AddXY("This Week", revenue.ThisWeek);
            _revenueChart.Series[0].Points.AddXY("This Month", revenue.ThisMonth);
            _revenueChart.Series[0].Points.AddXY("This Year", revenue.ThisYear);
            _revenueChart.Series[0]["PointWidth"] = "0.6";

            // --- Orders over time ---
            var dailyOrders = _controller.GetDailyOrderCounts(30);
            _ordersChart.Series[0].Points.Clear();
            foreach (var (date, count) in dailyOrders)
                _ordersChart.Series[0].Points.AddXY(date.ToString("MM/dd"), count);
            _ordersChart.ChartAreas[0].AxisX.Interval = 5;
            _ordersChart.ChartAreas[0].AxisX.LabelStyle.Angle = -45;

            // --- Order status ---
            var statusData = _controller.GetOrderStatusBreakdown();
            _statusChart.Series[0].Points.Clear();
            foreach (var kv in statusData)
                _statusChart.Series[0].Points.AddXY(kv.Key, kv.Value);

            // --- Top services ---
            var services = _controller.GetMostPopularServices(6);
            _servicesChart.Series[0].Points.Clear();
            foreach (var (service, count) in services)
                _servicesChart.Series[0].Points.AddXY(service, count);
            _servicesChart.Series[0]["PointWidth"] = "0.6";

            // --- Payment methods ---
            var paymentData = _controller.GetRevenueByPaymentMethod();
            _paymentChart.Series[0].Points.Clear();
            foreach (var kv in paymentData)
                _paymentChart.Series[0].Points.AddXY(kv.Key, kv.Value);

            // --- Customer segments (bonus - same rollup data, same kind of insight) ---
            _segmentChart.Series[0].Points.Clear();
            foreach (var kv in dashboard.CustomersBySegment)
                _segmentChart.Series[0].Points.AddXY(kv.Key, kv.Value);
        }

        private void LoadFilteredData()
        {
            if (_fromDate.Value.Date > _toDate.Value.Date)
            {
                _currentReport = null;
                MessageBox.Show("The From date must be on or before the To date.", "Invalid Date Range", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _currentReport = _controller.GetFilteredReport(_fromDate.Value.Date, _toDate.Value.Date);
            var report = _currentReport;
            _lblSummary.Text = $"Range: {_fromDate.Value:yyyy-MM-dd} to {_toDate.Value:yyyy-MM-dd}     New Customers: {report.NewCustomers}     " +
                $"Orders: {report.TotalOrders}     Paid Revenue: {report.TotalSales:N2}     Open Feedback: {report.OpenFeedback}     Unpaid Orders: {report.PendingPayments}";
            var rating = report.AverageRating.HasValue ? $"{report.AverageRating:N1} ★ ({report.RatingCount} ratings)" : "No ratings";
            _lblKpis.Text = $"Filtered KPIs — Average Paid Order: {report.AverageOrderValue:N2}     Repeat Customers: {report.RepeatCustomerRate:N0}%     " +
                $"Completion Rate: {report.OrderCompletionRate:N0}%     Average Rating: {rating}";

            _revenueChart.Series[0].Points.Clear();
            foreach (var (date, sales) in report.DailySales)
                _revenueChart.Series[0].Points.AddXY(date.ToString("MM/dd"), sales);
            _revenueChart.Series[0]["PointWidth"] = "0.6";

            _ordersChart.Series[0].Points.Clear();
            foreach (var (date, count) in report.DailyOrders)
                _ordersChart.Series[0].Points.AddXY(date.ToString("MM/dd"), count);
            _ordersChart.ChartAreas[0].AxisX.Interval = Math.Max(1, report.DailyOrders.Count / 12);
            _ordersChart.ChartAreas[0].AxisX.LabelStyle.Angle = -45;

            _statusChart.Series[0].Points.Clear();
            foreach (var item in report.OrdersByStatus)
                _statusChart.Series[0].Points.AddXY(item.Key, item.Value);

            _servicesChart.Series[0].Points.Clear();
            foreach (var (service, count) in report.TopServices)
                _servicesChart.Series[0].Points.AddXY(service, count);
            _servicesChart.Series[0]["PointWidth"] = "0.6";

            _paymentChart.Series[0].Points.Clear();
            foreach (var item in report.RevenueByPaymentMethod)
                _paymentChart.Series[0].Points.AddXY(item.Key, item.Value);

            _segmentChart.Series[0].Points.Clear();
            foreach (var item in report.CustomersBySegment)
                _segmentChart.Series[0].Points.AddXY(item.Key, item.Value);
        }

        private void ExportFilteredPdf()
        {
            LoadFilteredData();
            if (_currentReport == null) return;
            var companyId = AuthController.CurrentCompanyId;
            var company = companyId.HasValue ? _db.Companies.FirstOrDefault(c => c.Id == companyId.Value) : null;
            if (company == null)
            {
                MessageBox.Show("Could not load the selected shop or branch name.", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var report = _currentReport;
            var lines = new List<string>
            {
                company.ShopName,
                "BUSINESS INTELLIGENCE REPORT",
                $"Date range: {_fromDate.Value:yyyy-MM-dd} through {_toDate.Value:yyyy-MM-dd}",
                $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}",
                "",
                "SUMMARY",
                $"New customers: {report.NewCustomers}",
                $"Orders received: {report.TotalOrders}",
                $"Paid revenue: PHP {report.TotalSales:N2}",
                $"Open feedback: {report.OpenFeedback}",
                $"Unpaid orders: {report.PendingPayments}",
                "",
                "KEY PERFORMANCE INDICATORS",
                $"Average paid order: PHP {report.AverageOrderValue:N2}",
                $"Repeat customer rate: {report.RepeatCustomerRate:N1}%",
                $"Order completion rate: {report.OrderCompletionRate:N1}%",
                $"Average rating: {(report.AverageRating.HasValue ? report.AverageRating.Value.ToString("N1") : "No ratings")} ({report.RatingCount} ratings)",
                "",
                "DAILY REVENUE"
            };
            lines.AddRange(report.DailySales.Select(x => $"{x.Date:yyyy-MM-dd}: PHP {x.Sales:N2}"));
            lines.Add("");
            lines.Add("DAILY ORDERS");
            lines.AddRange(report.DailyOrders.Select(x => $"{x.Date:yyyy-MM-dd}: {x.Count}"));
            lines.Add("");
            lines.Add("ORDER STATUS");
            lines.AddRange(report.OrdersByStatus.Select(x => $"{x.Key}: {x.Value}"));
            lines.Add("");
            lines.Add("TOP SERVICES");
            lines.AddRange(report.TopServices.Select(x => $"{x.Service}: {x.Count} item(s)"));
            lines.Add("");
            lines.Add("REVENUE BY PAYMENT METHOD");
            lines.AddRange(report.RevenueByPaymentMethod.Select(x => $"{x.Key}: PHP {x.Value:N2}"));
            lines.Add("");
            lines.Add("NEW CUSTOMERS BY SEGMENT");
            lines.AddRange(report.CustomersBySegment.Select(x => $"{x.Key}: {x.Value}"));

            using var dialog = new SaveFileDialog
            {
                Title = "Export Filtered Report PDF",
                Filter = "PDF file (*.pdf)|*.pdf",
                FileName = $"Business-Report-{_fromDate.Value:yyyyMMdd}-{_toDate.Value:yyyyMMdd}.pdf",
                AddExtension = true,
                DefaultExt = "pdf"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                ReceiptPdfWriter.WriteLines(dialog.FileName, lines);
                MessageBox.Show($"Filtered report saved to:\n{dialog.FileName}", "PDF Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save report PDF:\n{ex.Message}", "PDF Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }
}
