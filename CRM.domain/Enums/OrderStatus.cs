namespace CRM.domain.Enums
{
    /// <summary>
    /// Tracks a laundry order's workflow: Pending -> Ongoing -> ReadyForPickup
    /// -> Completed. Cancelled is a possible exit state before Completed.
    /// </summary>
    public enum OrderStatus
    {
        Pending = 0,
        Ongoing = 1,
        ReadyForPickup = 2,
        Completed = 3,
        Cancelled = 4
    }
}
