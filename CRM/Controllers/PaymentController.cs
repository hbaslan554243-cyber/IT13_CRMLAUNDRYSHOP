using CRM.domain.Entities.Tenant;
using CRM.domain.Enums;
using CRM.infrastructure.data;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers
{
    /// <summary>
    /// Payment & Billing (Feature 8). Records payments against an order,
    /// requires one exact settlement, generates a receipt number, and
    /// triggers loyalty points once an order is fully paid.
    /// </summary>
    public class PaymentController
    {
        private readonly LaundryShopDbContext _db;
        private readonly int _companyId;

        public PaymentController(LaundryShopDbContext db)
        {
            _db = db;
            _companyId = AuthController.CurrentCompanyId ?? 0;
        }

        /// <summary>
        /// Records one exact payment against an order, optionally redeeming
        /// loyalty points at checkout (1 point = 1 peso off). Partial and
        /// overpayments are rejected.
        /// </summary>
        public OperationResult RecordPayment(int orderId, decimal amountPaid, PaymentMethod method, int pointsToRedeem = 0)
        {
            if (amountPaid < 0 || pointsToRedeem < 0)
                return new OperationResult { Success = false, Message = "Payment and points cannot be negative." };

            var order = _db.LaundryOrders.Include(o => o.Payment).FirstOrDefault(o => o.Id == orderId && o.CompanyId == _companyId);
            if (order == null) return new OperationResult { Success = false, Message = "Order not found." };
            if (order.Status == OrderStatus.Cancelled)
                return new OperationResult { Success = false, Message = "A cancelled order cannot be paid." };

            var payment = order.Payment;
            if (payment?.Status == PaymentStatus.Paid)
                return new OperationResult { Success = false, Message = "This order is already fully paid." };
            var isNew = payment == null;
            var baseDue = payment?.AmountDue ?? order.TotalAmount;
            var alreadyPaid = payment?.AmountPaid ?? 0m;
            var balanceBeforeDiscount = Math.Max(0m, baseDue - alreadyPaid);

            // --- Loyalty redemption (once per payment only) ---
            if (pointsToRedeem > 0)
            {
                if (payment?.PointsRedeemed > 0)
                    return new OperationResult { Success = false, Message = "Points have already been redeemed for this payment." };

                var loyalty = new LoyaltyController(_db);
                var balance = loyalty.GetBalance(order.CustomerId);
                if (pointsToRedeem > balance)
                    return new OperationResult { Success = false, Message = $"Customer only has {balance} points available." };

                var maxRedeemable = (int)Math.Floor(balanceBeforeDiscount); // don't let discounts exceed the outstanding balance
                if (pointsToRedeem > maxRedeemable)
                    return new OperationResult { Success = false, Message = $"Cannot redeem more than {maxRedeemable} points on this order balance." };
            }

            var netDue = Math.Max(0m, baseDue - pointsToRedeem);
            var exactAmountRequired = netDue - alreadyPaid;
            if (amountPaid != exactAmountRequired)
                return new OperationResult { Success = false, Message = $"Enter the exact amount due: {exactAmountRequired:N2}. Partial and extra payments are not accepted." };

            if (amountPaid <= 0 && pointsToRedeem == 0)
                return new OperationResult { Success = false, Message = "Enter an amount greater than zero, or redeem points." };

            payment ??= new Payment { CompanyId = _companyId, LaundryOrderId = orderId };
            if (pointsToRedeem > 0)
            {
                var redeemResult = new LoyaltyController(_db).RedeemPoints(order.CustomerId, pointsToRedeem, $"Redeemed at checkout for Order #{orderId}");
                if (!redeemResult.Success) return redeemResult;
                payment.PointsRedeemed = pointsToRedeem;
                payment.LoyaltyDiscount = pointsToRedeem; // 1 point = 1 peso
            }

            payment.AmountDue = netDue;
            payment.AmountPaid += amountPaid;
            payment.Method = method;
            payment.PaidAt = DateTime.UtcNow;
            payment.ReceiptNumber ??= $"RCPT-{orderId:D6}-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
            payment.Status = PaymentStatus.Paid;

            if (isNew) _db.Payments.Add(payment);

            try
            {
                _db.SaveChanges();

                // Award loyalty points exactly once, only when the payment
                // is genuinely fully settled and hasn't already been awarded.
                if (payment.Status == PaymentStatus.Paid && !payment.PointsAwarded)
                {
                    new LoyaltyController(_db).AwardPointsForPayment(order.CustomerId, order.Id, payment.AmountPaid);
                    payment.PointsAwarded = true;
                    _db.SaveChanges();
                }

                return new OperationResult { Success = true, Message = $"Full payment recorded. Receipt {payment.ReceiptNumber} is ready." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }

        public Payment? GetPaymentForOrder(int orderId) =>
            _db.Payments.FirstOrDefault(p => p.LaundryOrderId == orderId && p.CompanyId == _companyId);

        /// <summary>Drill-down behind the Avg Order Value / earnings KPI cards. Excludes payments on Cancelled orders.</summary>
        public List<Payment> GetRecentPayments(int top = 50) =>
            _db.Payments.Include(p => p.LaundryOrder).ThenInclude(o => o!.Customer)
                .Where(p => p.CompanyId == _companyId && p.Status == PaymentStatus.Paid
                    && p.LaundryOrder != null && p.LaundryOrder.Status != OrderStatus.Cancelled)
                .OrderByDescending(p => p.PaidAt)
                .Take(top)
                .ToList();
    }
}
