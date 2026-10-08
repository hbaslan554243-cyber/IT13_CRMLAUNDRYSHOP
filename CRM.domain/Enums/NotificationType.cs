namespace CRM.domain.Enums
{
    public enum NotificationType
    {
        OrderReady = 0,
        OrderCompleted = 1,
        Promotion = 2,
        LoyaltyReward = 3,
        General = 4
    }

    public enum NotificationChannel
    {
        SMS = 0,
        Email = 1,
        InApp = 2
    }
}
