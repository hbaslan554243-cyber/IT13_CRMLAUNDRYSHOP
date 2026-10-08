using CRM.domain.Entities.Tenant;
using CRM.domain.Enums;
using CRM.infrastructure.data;

namespace CRM.api.Controllers
{
    /// <summary>
    /// Loyalty & Rewards (Feature 6). Points are earned automatically when
    /// a payment is completed (see PaymentController), based on this shop's
    /// configurable PesosPerPoint rate. Manual redeem/adjust is also supported
    /// for staff to hand out perks or correct mistakes.
    /// </summary>
    public class LoyaltyController
    {
        private readonly LaundryShopDbContext _db;
        private readonly int _companyId;

        public LoyaltyController(LaundryShopDbContext db)
        {
            _db = db;
            _companyId = AuthController.CurrentCompanyId ?? 0;
        }

        private LoyaltyAccount GetOrCreateAccount(int customerId)
        {
            var account = _db.LoyaltyAccounts.FirstOrDefault(a => a.CustomerId == customerId && a.CompanyId == _companyId);
            if (account == null)
            {
                account = new LoyaltyAccount { CompanyId = _companyId, CustomerId = customerId };
                _db.LoyaltyAccounts.Add(account);
                _db.SaveChanges();
            }
            return account;
        }

        public void AwardPointsForPayment(int customerId, int? orderId, decimal amountPaid)
        {
            var company = _db.Companies.FirstOrDefault(c => c.Id == _companyId);
            if (company == null || company.PesosPerPoint <= 0) return;

            var points = (int)Math.Floor(amountPaid / company.PesosPerPoint);
            if (points <= 0) return;

            var account = GetOrCreateAccount(customerId);
            account.ApplyTransaction(new LoyaltyTransaction
            {
                CompanyId = _companyId,
                LoyaltyAccountId = account.Id,
                LaundryOrderId = orderId,
                Type = LoyaltyTransactionType.Earned,
                Points = points,
                Notes = "Earned from payment"
            });
            _db.SaveChanges();
        }

        public int GetBalance(int customerId) => GetOrCreateAccount(customerId).PointsBalance;

        public OperationResult RedeemPoints(int customerId, int points, string? notes)
        {
            if (points <= 0)
                return new OperationResult { Success = false, Message = "Enter a positive number of points." };

            var account = GetOrCreateAccount(customerId);
            if (account.PointsBalance < points)
                return new OperationResult { Success = false, Message = $"Customer only has {account.PointsBalance} points." };

            account.ApplyTransaction(new LoyaltyTransaction
            {
                CompanyId = _companyId,
                LoyaltyAccountId = account.Id,
                Type = LoyaltyTransactionType.Redeemed,
                Points = points,
                Notes = notes
            });

            try
            {
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = $"Redeemed {points} points." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }

        public OperationResult AdjustPoints(int customerId, int delta, string? notes)
        {
            var account = GetOrCreateAccount(customerId);
            account.ApplyTransaction(new LoyaltyTransaction
            {
                CompanyId = _companyId,
                LoyaltyAccountId = account.Id,
                Type = LoyaltyTransactionType.Adjusted,
                Points = delta,
                Notes = notes
            });

            try
            {
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = "Adjustment applied." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }

        public List<(Customer Customer, int Points)> GetAllBalances()
        {
            var query = from c in _db.Customers
                        where !c.IsDeleted && c.CompanyId == _companyId
                        join a in _db.LoyaltyAccounts on c.Id equals a.CustomerId into accounts
                        from acc in accounts.DefaultIfEmpty()
                        select new { Customer = c, Points = acc != null ? acc.PointsBalance : 0 };

            return query.ToList()
                .OrderByDescending(x => x.Points)
                .Select(x => (x.Customer, x.Points))
                .ToList();
        }

        public List<LoyaltyTransaction> GetTransactions(int customerId)
        {
            var account = _db.LoyaltyAccounts.FirstOrDefault(a => a.CustomerId == customerId && a.CompanyId == _companyId);
            if (account == null) return new List<LoyaltyTransaction>();

            return _db.LoyaltyTransactions
                .Where(t => t.LoyaltyAccountId == account.Id)
                .OrderByDescending(t => t.CreatedAt)
                .ToList();
        }
    }
}
