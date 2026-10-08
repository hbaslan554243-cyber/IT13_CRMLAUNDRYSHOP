using CRM.domain.Entities.Tenant;
using CRM.domain.Enums;
using CRM.infrastructure.data;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers
{
    /// <summary>
    /// Analytics & Reports (Feature 9). Rolled up for the Owner: customers,
    /// orders, sales, feedback, and loyalty in one dashboard summary.
    /// Scoped entirely to this shop's own data. Every revenue figure here
    /// explicitly excludes payments on Cancelled orders, even if a payment
    /// happened to be recorded before the order was cancelled - cancelled
    /// orders never count toward revenue/earnings.
    /// </summary>
    public class ReportController
    {
        private readonly LaundryShopDbContext _db;
        private readonly int _companyId;

        public ReportController(LaundryShopDbContext db)
        {
            _db = db;
            _companyId = AuthController.CurrentCompanyId ?? 0;
        }

        /// <summary>Base query for "money that actually counts" - Paid payments on non-cancelled orders.</summary>
        private IQueryable<Payment> ValidPaidPayments() =>
            _db.Payments
                .Include(p => p.LaundryOrder)
                .Where(p => p.CompanyId == _companyId
                    && p.Status == PaymentStatus.Paid
                    && p.LaundryOrder != null
                    && p.LaundryOrder.Status != OrderStatus.Cancelled);

        public class DashboardSummary
        {
            public int TotalCustomers { get; set; }
            public int TotalOrders { get; set; }
            public decimal TotalSales { get; set; }
            public int OpenFeedback { get; set; }
            public int PendingPayments { get; set; }
            public Dictionary<string, int> OrdersByStatus { get; set; } = new();
            public Dictionary<string, int> CustomersBySegment { get; set; } = new();
            public List<(string CustomerName, int Orders, decimal Spend)> TopCustomers { get; set; } = new();
        }

        public DashboardSummary GetDashboard()
        {
            var summary = new DashboardSummary
            {
                TotalCustomers = _db.Customers.Count(c => !c.IsDeleted && c.CompanyId == _companyId),
                TotalOrders = _db.LaundryOrders.Count(o => o.CompanyId == _companyId),
                TotalSales = ValidPaidPayments().Sum(p => (decimal?)p.AmountPaid) ?? 0m,
                OpenFeedback = _db.Feedbacks.Count(f => f.CompanyId == _companyId && (f.Status == FeedbackStatus.Open || f.Status == FeedbackStatus.Escalated)),
                PendingPayments = _db.Payments.Count(p => p.CompanyId == _companyId && p.Status != PaymentStatus.Paid)
            };

            summary.OrdersByStatus = _db.LaundryOrders
                .Where(o => o.CompanyId == _companyId)
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList()
                .ToDictionary(x => x.Status.ToString(), x => x.Count);

            summary.CustomersBySegment = _db.Customers.Where(c => !c.IsDeleted && c.CompanyId == _companyId)
                .GroupBy(c => c.Segment)
                .Select(g => new { Segment = g.Key, Count = g.Count() })
                .ToList()
                .ToDictionary(x => x.Segment.ToString(), x => x.Count);

            summary.TopCustomers = _db.Customers.Where(c => !c.IsDeleted && c.CompanyId == _companyId)
                .OrderByDescending(c => c.TotalSpend)
                .Take(5)
                .Select(c => new { c.FullName, c.TotalOrders, c.TotalSpend })
                .ToList()
                .Select(x => (x.FullName, x.TotalOrders, x.TotalSpend))
                .ToList();

            return summary;
        }

        public sealed class FilteredReport
        {
            public int NewCustomers { get; init; }
            public int TotalOrders { get; init; }
            public decimal TotalSales { get; init; }
            public int OpenFeedback { get; init; }
            public int PendingPayments { get; init; }
            public decimal AverageOrderValue { get; init; }
            public double RepeatCustomerRate { get; init; }
            public double OrderCompletionRate { get; init; }
            public double? AverageRating { get; init; }
            public int RatingCount { get; init; }
            public List<(DateTime Date, decimal Sales)> DailySales { get; init; } = new();
            public List<(DateTime Date, int Count)> DailyOrders { get; init; } = new();
            public Dictionary<string, int> OrdersByStatus { get; init; } = new();
            public List<(string Service, int Count)> TopServices { get; init; } = new();
            public Dictionary<string, decimal> RevenueByPaymentMethod { get; init; } = new();
            public Dictionary<string, int> CustomersBySegment { get; init; } = new();
        }

        /// <summary>Builds one report snapshot with every metric constrained to the chosen inclusive date range.</summary>
        public FilteredReport GetFilteredReport(DateTime fromDate, DateTime toDate)
        {
            var from = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
            var until = DateTime.SpecifyKind(toDate.Date.AddDays(1), DateTimeKind.Utc);
            if (toDate.Date < fromDate.Date)
                throw new ArgumentException("The From date must be on or before the To date.");

            var orders = _db.LaundryOrders
                .Where(o => o.CompanyId == _companyId && o.ReceivedAt >= from && o.ReceivedAt < until)
                .ToList();
            var paid = ValidPaidPayments()
                .Where(p => p.PaidAt.HasValue && p.PaidAt.Value >= from && p.PaidAt.Value < until)
                .ToList();
            var customers = _db.Customers
                .Where(c => !c.IsDeleted && c.CompanyId == _companyId && c.CreatedAt >= from && c.CreatedAt < until)
                .ToList();
            var feedback = _db.Feedbacks
                .Where(f => f.CompanyId == _companyId && f.CreatedAt >= from && f.CreatedAt < until)
                .ToList();

            var orderCountsByCustomer = orders.GroupBy(o => o.CustomerId).ToList();
            var repeatRate = orderCountsByCustomer.Count > 0
                ? orderCountsByCustomer.Count(g => g.Count() > 1) / (double)orderCountsByCustomer.Count * 100d
                : 0d;
            var completed = orders.Count(o => o.Status == OrderStatus.Completed);
            var cancelled = orders.Count(o => o.Status == OrderStatus.Cancelled);
            var finalized = completed + cancelled;

            var firstDay = from.Date;
            var finalDay = toDate.Date;
            var salesByDay = paid.Where(p => p.PaidAt.HasValue)
                .GroupBy(p => p.PaidAt!.Value.Date)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.AmountPaid));
            var ordersByDay = orders.GroupBy(o => o.ReceivedAt.Date)
                .ToDictionary(g => g.Key, g => g.Count());
            var days = Enumerable.Range(0, (finalDay - firstDay).Days + 1).Select(dayOffset => firstDay.AddDays(dayOffset)).ToList();

            var itemRows = _db.OrderItems
                .Include(i => i.LaundryOrder)
                .Where(i => i.CompanyId == _companyId && i.LaundryOrder != null
                    && i.LaundryOrder.ReceivedAt >= from && i.LaundryOrder.ReceivedAt < until)
                .ToList();
            var ratings = feedback.Where(f => f.Rating.HasValue).Select(f => (double)f.Rating!.Value).ToList();
            var pendingPayments = _db.Payments
                .Count(p => p.CompanyId == _companyId && p.Status != PaymentStatus.Paid
                    && p.LaundryOrder != null && p.LaundryOrder.ReceivedAt >= from && p.LaundryOrder.ReceivedAt < until);

            return new FilteredReport
            {
                NewCustomers = customers.Count,
                TotalOrders = orders.Count,
                TotalSales = paid.Sum(p => p.AmountPaid),
                OpenFeedback = feedback.Count(f => f.Status == FeedbackStatus.Open || f.Status == FeedbackStatus.Escalated),
                PendingPayments = pendingPayments,
                AverageOrderValue = paid.Count > 0 ? paid.Average(p => p.AmountPaid) : 0m,
                RepeatCustomerRate = repeatRate,
                OrderCompletionRate = finalized > 0 ? completed / (double)finalized * 100d : 0d,
                AverageRating = ratings.Count > 0 ? ratings.Average() : null,
                RatingCount = ratings.Count,
                DailySales = days.Select(day => (day, salesByDay.TryGetValue(day, out var sales) ? sales : 0m)).ToList(),
                DailyOrders = days.Select(day => (day, ordersByDay.TryGetValue(day, out var count) ? count : 0)).ToList(),
                OrdersByStatus = orders.GroupBy(o => o.Status).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                TopServices = itemRows.GroupBy(i => i.ServiceName)
                    .Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).Take(6).ToList(),
                RevenueByPaymentMethod = paid.GroupBy(p => p.Method).ToDictionary(g => g.Key.ToString(), g => g.Sum(p => p.AmountPaid)),
                CustomersBySegment = customers.GroupBy(c => c.Segment).ToDictionary(g => g.Key.ToString(), g => g.Count())
            };
        }

        public class TodaySummary
        {
            public int TodaysOrders { get; set; }
            public int PendingTasks { get; set; }
            public decimal TodaysEarnings { get; set; }
            public int LowStockCount { get; set; }
        }

        /// <summary>For the Admin/Owner Dashboard landing page.</summary>
        public TodaySummary GetTodaySummary()
        {
            var todayStart = DateTime.UtcNow.Date;
            var todayEnd = todayStart.AddDays(1);

            return new TodaySummary
            {
                TodaysOrders = _db.LaundryOrders.Count(o => o.CompanyId == _companyId && o.ReceivedAt >= todayStart && o.ReceivedAt < todayEnd),
                PendingTasks = _db.LaundryOrders.Count(o => o.CompanyId == _companyId && (o.Status == OrderStatus.Pending || o.Status == OrderStatus.Ongoing)),
                TodaysEarnings = ValidPaidPayments().Where(p => p.PaidAt >= todayStart && p.PaidAt < todayEnd).Sum(p => (decimal?)p.AmountPaid) ?? 0m,
                LowStockCount = _db.InventoryItems.Count(i => i.CompanyId == _companyId && i.IsActive && i.QuantityOnHand <= i.ReorderThreshold)
            };
        }

        /// <summary>Sales total per day for the last N days - for a daily/weekly/monthly sales report.</summary>
        public List<(DateTime Date, decimal Sales)> GetDailySales(int days = 30)
        {
            var since = DateTime.UtcNow.Date.AddDays(-days + 1);

            return ValidPaidPayments()
                .Where(p => p.PaidAt >= since)
                .ToList()
                .GroupBy(p => p.PaidAt!.Value.Date)
                .Select(g => (g.Key, g.Sum(x => x.AmountPaid)))
                .OrderBy(x => x.Key)
                .ToList();
        }

        /// <summary>Which services get ordered the most, by line-item count.</summary>
        public List<(string Service, int Count)> GetMostPopularServices(int top = 5) =>
            _db.OrderItems
                .Where(i => i.CompanyId == _companyId)
                .GroupBy(i => i.ServiceName)
                .Select(g => new { Service = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(top)
                .ToList()
                .Select(x => (x.Service, x.Count))
                .ToList();

        public class RevenueByPeriod
        {
            public decimal Today { get; set; }
            public decimal ThisWeek { get; set; }
            public decimal ThisMonth { get; set; }
            public decimal ThisYear { get; set; }
        }

        /// <summary>Revenue chart: today / this week / this month / this year, all from valid Paid payments.</summary>
        public RevenueByPeriod GetRevenueByPeriod()
        {
            var now = DateTime.UtcNow;
            var todayStart = now.Date;
            var weekStart = todayStart.AddDays(-(int)todayStart.DayOfWeek);
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var paid = ValidPaidPayments().Where(p => p.PaidAt != null).ToList();

            return new RevenueByPeriod
            {
                Today = paid.Where(p => p.PaidAt!.Value >= todayStart).Sum(p => p.AmountPaid),
                ThisWeek = paid.Where(p => p.PaidAt!.Value >= weekStart).Sum(p => p.AmountPaid),
                ThisMonth = paid.Where(p => p.PaidAt!.Value >= monthStart).Sum(p => p.AmountPaid),
                ThisYear = paid.Where(p => p.PaidAt!.Value >= yearStart).Sum(p => p.AmountPaid)
            };
        }

        /// <summary>Orders chart: how many orders were RECEIVED per day over the last N days.</summary>
        public List<(DateTime Date, int Count)> GetDailyOrderCounts(int days = 30)
        {
            var since = DateTime.UtcNow.Date.AddDays(-days + 1);

            var grouped = _db.LaundryOrders
                .Where(o => o.CompanyId == _companyId && o.ReceivedAt >= since)
                .ToList()
                .GroupBy(o => o.ReceivedAt.Date)
                .ToDictionary(g => g.Key, g => g.Count());

            var result = new List<(DateTime, int)>();
            for (var d = since; d <= DateTime.UtcNow.Date; d = d.AddDays(1))
                result.Add((d, grouped.TryGetValue(d, out var count) ? count : 0));

            return result;
        }

        /// <summary>Order Status chart data - counts per status, for a pie/bar chart.</summary>
        public Dictionary<string, int> GetOrderStatusBreakdown() =>
            _db.LaundryOrders
                .Where(o => o.CompanyId == _companyId)
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList()
                .ToDictionary(x => x.Status.ToString(), x => x.Count);

        /// <summary>Payment Methods chart - how much valid revenue came through each method.</summary>
        public Dictionary<string, decimal> GetRevenueByPaymentMethod() =>
            ValidPaidPayments()
                .GroupBy(p => p.Method)
                .Select(g => new { Method = g.Key, Total = g.Sum(p => p.AmountPaid) })
                .ToList()
                .ToDictionary(x => x.Method.ToString(), x => x.Total);

        public class Kpis
        {
            public decimal AverageOrderValue { get; set; }
            public decimal TotalValidRevenue { get; set; }
            public int ValidOrderCount { get; set; }
            public double RepeatCustomerRate { get; set; }
            public double OrderCompletionRate { get; set; }
            public int CompletedCount { get; set; }
            public int CancelledCount { get; set; }
            public double? AverageRating { get; set; }
            public int RatingCount { get; set; }
            public int NewCustomersThisMonth { get; set; }
        }

        /// <summary>
        /// Key Performance Indicators for the Dashboard/Reports: business
        /// health metrics beyond raw counts.
        /// </summary>
        public Kpis GetKpis()
        {
            var paidPayments = ValidPaidPayments().ToList();
            var totalValidRevenue = paidPayments.Sum(p => p.AmountPaid);
            var averageOrderValue = paidPayments.Count > 0 ? paidPayments.Average(p => p.AmountPaid) : 0m;

            var customers = _db.Customers.Where(c => !c.IsDeleted && c.CompanyId == _companyId).ToList();
            var repeatCustomerRate = customers.Count > 0
                ? customers.Count(c => c.TotalOrders > 1) / (double)customers.Count * 100.0
                : 0.0;

            var completedCount = _db.LaundryOrders.Count(o => o.CompanyId == _companyId && o.Status == OrderStatus.Completed);
            var cancelledCount = _db.LaundryOrders.Count(o => o.CompanyId == _companyId && o.Status == OrderStatus.Cancelled);
            var finishedTotal = completedCount + cancelledCount;
            var orderCompletionRate = finishedTotal > 0 ? completedCount / (double)finishedTotal * 100.0 : 0.0;

            var ratings = _db.Feedbacks
                .Where(f => f.CompanyId == _companyId && f.Rating != null)
                .Select(f => f.Rating!.Value)
                .ToList();

            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var newCustomersThisMonth = _db.Customers.Count(c => !c.IsDeleted && c.CompanyId == _companyId && c.CreatedAt >= monthStart);

            return new Kpis
            {
                AverageOrderValue = averageOrderValue,
                TotalValidRevenue = totalValidRevenue,
                ValidOrderCount = paidPayments.Count,
                RepeatCustomerRate = repeatCustomerRate,
                OrderCompletionRate = orderCompletionRate,
                CompletedCount = completedCount,
                CancelledCount = cancelledCount,
                AverageRating = ratings.Count > 0 ? ratings.Average() : null,
                RatingCount = ratings.Count,
                NewCustomersThisMonth = newCustomersThisMonth
            };
        }

        public class RetentionDetails
        {
            public int TotalCustomers { get; set; }
            public int NewCustomerCount { get; set; }       // 0 or 1 order so far
            public int RepeatCustomerCount { get; set; }    // 2+ orders
            public double RepeatCustomerRate { get; set; }
            public int InactiveCount { get; set; }          // lapsed, within 2x threshold
            public int ChurnedCount { get; set; }           // lapsed beyond 2x threshold - likely gone for good
            public int ReactivatedCount { get; set; }       // lapsed once before, but ordered again since
            public double AverageOrdersPerCustomer { get; set; }
            public double? AverageDaysBetweenOrders { get; set; }

            public List<Customer> NewCustomers { get; set; } = new();
            public List<Customer> RepeatCustomers { get; set; } = new();
            public List<Customer> InactiveCustomers { get; set; } = new();
            public List<Customer> ChurnedCustomers { get; set; } = new();
            public List<Customer> ReactivatedCustomers { get; set; } = new();
        }

        /// <summary>
        /// Drill-down behind the "Repeat Customers" KPI card. Definitions:
        /// - New: 0 or 1 order so far.
        /// - Repeat: 2+ orders.
        /// - Inactive: currently past the shop's InactiveDaysThreshold since their last order, but within 2x that.
        /// - Churned: past 2x the InactiveDaysThreshold - a longer, more decisive lapse.
        /// - Reactivated: had a gap between two consecutive orders longer than the InactiveDaysThreshold
        ///   at some point, but isn't currently Inactive/Churned - i.e. they came back after lapsing.
        /// All thresholds come from this shop's own Company settings, not hardcoded.
        /// </summary>
        public RetentionDetails GetRetentionDetails()
        {
            var company = _db.Companies.First(c => c.Id == _companyId);
            var customers = _db.Customers.Where(c => !c.IsDeleted && c.CompanyId == _companyId).ToList();
            var now = DateTime.UtcNow;

            var newCustomers = customers.Where(c => c.TotalOrders <= 1).ToList();
            var repeatCustomers = customers.Where(c => c.TotalOrders > 1).OrderByDescending(c => c.TotalOrders).ToList();

            var inactiveCustomers = new List<Customer>();
            var churnedCustomers = new List<Customer>();
            foreach (var c in customers.Where(c => c.TotalOrders > 0 && c.LastOrderDate.HasValue))
            {
                var daysSince = (now - c.LastOrderDate!.Value).TotalDays;
                if (daysSince > company.InactiveDaysThreshold * 2) churnedCustomers.Add(c);
                else if (daysSince > company.InactiveDaysThreshold) inactiveCustomers.Add(c);
            }

            // Reactivation & average-gap analysis: look at each customer's order history.
            var orderDatesByCustomer = _db.LaundryOrders
                .Where(o => o.CompanyId == _companyId)
                .Select(o => new { o.CustomerId, o.ReceivedAt })
                .ToList()
                .GroupBy(o => o.CustomerId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ReceivedAt).OrderBy(d => d).ToList());

            var allGaps = new List<double>();
            var reactivatedIds = new HashSet<int>();
            foreach (var (customerId, dates) in orderDatesByCustomer)
            {
                for (int i = 1; i < dates.Count; i++)
                {
                    var gapDays = (dates[i] - dates[i - 1]).TotalDays;
                    allGaps.Add(gapDays);
                    if (gapDays > company.InactiveDaysThreshold)
                        reactivatedIds.Add(customerId);
                }
            }

            var lapsedIds = inactiveCustomers.Select(c => c.Id).Concat(churnedCustomers.Select(c => c.Id)).ToHashSet();
            var reactivatedCustomers = customers.Where(c => reactivatedIds.Contains(c.Id) && !lapsedIds.Contains(c.Id)).ToList();

            return new RetentionDetails
            {
                TotalCustomers = customers.Count,
                NewCustomerCount = newCustomers.Count,
                RepeatCustomerCount = repeatCustomers.Count,
                RepeatCustomerRate = customers.Count > 0 ? repeatCustomers.Count / (double)customers.Count * 100.0 : 0.0,
                InactiveCount = inactiveCustomers.Count,
                ChurnedCount = churnedCustomers.Count,
                ReactivatedCount = reactivatedCustomers.Count,
                AverageOrdersPerCustomer = customers.Count > 0 ? customers.Average(c => c.TotalOrders) : 0.0,
                AverageDaysBetweenOrders = allGaps.Count > 0 ? allGaps.Average() : null,
                NewCustomers = newCustomers,
                RepeatCustomers = repeatCustomers,
                InactiveCustomers = inactiveCustomers,
                ChurnedCustomers = churnedCustomers,
                ReactivatedCustomers = reactivatedCustomers
            };
        }
    }
}
