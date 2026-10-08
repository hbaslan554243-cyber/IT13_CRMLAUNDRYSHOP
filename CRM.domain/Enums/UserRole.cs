namespace CRM.domain.Enums
{
    /// <summary>
    /// The four-role authority hierarchy for this system.
    /// FrontDeskStaff = day-to-day data capture (customers, orders, payments)
    /// Manager        = quality control (segmentation review, complaint resolution)
    /// AdminOwner     = business intelligence (analytics, reports, promotions)
    /// SuperAdmin     = platform/system level only (accounts, settings) - not shop-floor
    /// </summary>
    public enum UserRole
    {
        FrontDeskStaff = 0,
        Manager = 1,
        AdminOwner = 2,
        SuperAdmin = 3
    }
}
