using CRM.domain.Entities.Tenant;
using CRM.domain.Enums;
using CRM.infrastructure.data;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers
{
    /// <summary>
    /// Automated Notifications (Feature 4). Not a real SMS/email gateway -
    /// records what would be sent and marks it as sent immediately, so the
    /// business logic (when to notify) is fully in place and only the
    /// actual delivery integration needs to be swapped in later.
    /// </summary>
    public class NotificationController
    {
        private readonly LaundryShopDbContext _db;
        private readonly int _companyId;

        public NotificationController(LaundryShopDbContext db)
        {
            _db = db;
            _companyId = AuthController.CurrentCompanyId ?? 0;
        }

        public void CreateOrderNotification(int customerId, int orderId, NotificationType type, string message)
        {
            _db.Notifications.Add(new Notification
            {
                CompanyId = _companyId,
                CustomerId = customerId,
                LaundryOrderId = orderId,
                Type = type,
                Channel = NotificationChannel.SMS,
                Message = message,
                IsSent = true,
                SentAt = DateTime.UtcNow
            });
            _db.SaveChanges();
        }

        public void CreatePromotionNotification(int customerId, int promotionId, string message)
        {
            _db.Notifications.Add(new Notification
            {
                CompanyId = _companyId,
                CustomerId = customerId,
                PromotionId = promotionId,
                Type = NotificationType.Promotion,
                Channel = NotificationChannel.SMS,
                Message = message,
                IsSent = true,
                SentAt = DateTime.UtcNow
            });
            _db.SaveChanges();
        }

        public List<Notification> GetAll() =>
            _db.Notifications.Where(n => n.CompanyId == _companyId)
                .Include(n => n.Customer)
                .OrderByDescending(n => n.CreatedAt)
                .Take(500)
                .ToList();

        public List<Notification> GetForCustomer(int customerId) =>
            _db.Notifications.Where(n => n.CustomerId == customerId && n.CompanyId == _companyId)
                .OrderByDescending(n => n.CreatedAt)
                .ToList();
    }
}
