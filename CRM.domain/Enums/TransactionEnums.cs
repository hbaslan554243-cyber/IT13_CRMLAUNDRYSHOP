namespace CRM.domain.Enums
{
    public enum LoyaltyTransactionType
    {
        Earned = 0,   // Points gained from an order
        Redeemed = 1, // Points spent on a reward
        Expired = 2,  // Points removed due to expiry
        Adjusted = 3  // Manual correction by Manager/Owner
    }

    public enum PromotionDiscountType
    {
        PercentageOff = 0,
        FixedAmountOff = 1,
        FreeService = 2
    }

    public enum PaymentMethod
    {
        Cash = 0,
        GCash = 1,
        Card = 2,
        BankTransfer = 3
    }

    public enum PaymentStatus
    {
        Pending = 0,
        Paid = 1,
        PartiallyPaid = 2,
        Refunded = 3
    }
}
