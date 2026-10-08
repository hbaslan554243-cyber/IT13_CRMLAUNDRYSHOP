using Microsoft.AspNetCore.Identity;
using CRM.domain.Enums;

namespace CRM.domain.Entities.Tenant
{
    /// <summary>
    /// Extends Identity's built-in user so login/password/security are handled
    /// by a battle-tested library, while we add the shop-specific fields on top.
    /// This is the account for STAFF (Front Desk, Manager, Owner, Super Admin) -
    /// not for customers. Customers are tracked separately in Customer.cs since
    /// they don't need login credentials in this desktop system.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? LastLoginAt { get; set; }

        // Which shop this account belongs to. Null only for Super Admin,
        // who isn't attached to any one shop. An AdminOwner's CompanyId
        // points at the Company they own; Staff/Manager inherit theirs
        // from whichever Owner created their account.
        public int? CompanyId { get; set; }
        public virtual Company? Company { get; set; }

        // Navigation - orders this staff member processed, complaints they resolved, etc.
        public virtual ICollection<LaundryOrder> OrdersHandled { get; set; } = new List<LaundryOrder>();
        public virtual ICollection<Feedback> FeedbackResolved { get; set; } = new List<Feedback>();
    }
}
