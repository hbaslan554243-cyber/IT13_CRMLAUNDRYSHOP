namespace CRM.domain.Enums
{
    public enum FeedbackStatus
    {
        Open = 0,          // Just submitted, nobody has looked at it yet
        UnderReview = 1,   // Staff/Manager is looking into it
        Escalated = 2,     // Front Desk couldn't resolve it, sent up to Manager
        Resolved = 3,
        Closed = 4         // Resolved and confirmed, no further action
    }

    public enum FeedbackType
    {
        Feedback = 0,   // General comment / suggestion
        Complaint = 1   // Something went wrong
    }
}
