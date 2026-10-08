namespace CRM.domain.Enums
{
    /// <summary>
    /// Behavior-based grouping so the Manager/Owner can act on customer data,
    /// not just store it. Recalculated periodically (e.g. nightly job or on-demand)
    /// based on order frequency, recency, and total spend.
    /// </summary>
    public enum CustomerSegment
    {
        New = 0,          // Fewer than N orders, not enough history yet
        Regular = 1,       // Consistent but average activity
        Frequent = 2,      // High order frequency
        HighValue = 3,     // High total spend, regardless of frequency
        Inactive = 4       // No orders within the configured lapse window
    }
}
