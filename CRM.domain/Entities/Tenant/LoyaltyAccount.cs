using CRM.domain.Enums;

namespace CRM.domain.Entities.Tenant
{
    /// <summary>
    /// One loyalty account per customer (1-to-1). Kept as a separate entity
    /// rather than fields on Customer so the running point balance and its
    /// full transaction history are cleanly isolated from profile data.
    /// </summary>
    public class LoyaltyAccount : BaseEntity
    {
        public int CustomerId { get; set; }
        public virtual Customer? Customer { get; set; }

        public int PointsBalance { get; set; } = 0;

        public virtual ICollection<LoyaltyTransaction> Transactions { get; set; } = new List<LoyaltyTransaction>();

        public void ApplyTransaction(LoyaltyTransaction transaction)
        {
            switch (transaction.Type)
            {
                case LoyaltyTransactionType.Earned:
                case LoyaltyTransactionType.Adjusted when transaction.Points > 0:
                    PointsBalance += Math.Abs(transaction.Points);
                    break;
                case LoyaltyTransactionType.Redeemed:
                case LoyaltyTransactionType.Expired:
                case LoyaltyTransactionType.Adjusted when transaction.Points < 0:
                    PointsBalance -= Math.Abs(transaction.Points);
                    break;
            }
            Transactions.Add(transaction);
        }
    }

    /// <summary>
    /// A single earn/redeem/expire/adjust event. Keeping every transaction
    /// (not just the running balance) means the Owner can audit exactly how
    /// a customer's points ended up where they are.
    /// </summary>
    public class LoyaltyTransaction : BaseEntity
    {
        public int LoyaltyAccountId { get; set; }
        public virtual LoyaltyAccount? LoyaltyAccount { get; set; }

        // Optional link to the order that generated these points
        public int? LaundryOrderId { get; set; }
        public virtual LaundryOrder? LaundryOrder { get; set; }

        public LoyaltyTransactionType Type { get; set; }
        public int Points { get; set; }
        public string? Notes { get; set; }
    }
}
