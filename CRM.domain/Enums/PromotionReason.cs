namespace CRM.domain.Enums
{
    /// <summary>
    /// Why a promotion is being created. Auto-suggested based on customer
    /// behavior (see PromotionController.SuggestReason), but always
    /// changeable by the Owner before saving.
    /// </summary>
    public enum PromotionReason
    {
        CustomerRetention,
        ReactivateInactiveCustomer,
        RewardLoyalCustomer,
        IncreaseOrderFrequency,
        IncreaseCustomerSpending,
        NewCustomerAcquisition,
        OffPeakPromotion,
        BulkOrderIncentive,
        BirthdayReward,
        CustomerRecovery,
        ServicePromotion
    }
}
