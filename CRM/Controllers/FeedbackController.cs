using CRM.domain.Entities.Tenant;
using CRM.domain.Enums;
using CRM.infrastructure.data;
using Microsoft.EntityFrameworkCore;

namespace CRM.api.Controllers
{
    /// <summary>
    /// Feedback & Complaint Management (Feature 5). Front Desk logs it;
    /// if they can't resolve it, it gets escalated to a Manager.
    /// </summary>
    public class FeedbackController
    {
        private readonly LaundryShopDbContext _db;
        private readonly int _companyId;

        public FeedbackController(LaundryShopDbContext db)
        {
            _db = db;
            _companyId = AuthController.CurrentCompanyId ?? 0;
        }

        public OperationResult SubmitFeedback(int customerId, int? orderId, FeedbackType type, string message, int? rating = null)
        {
            if (string.IsNullOrWhiteSpace(message))
                return new OperationResult { Success = false, Message = "Message is required." };

            if (rating.HasValue && (rating < 1 || rating > 5))
                return new OperationResult { Success = false, Message = "Rating must be between 1 and 5." };

            var feedback = new Feedback
            {
                CompanyId = _companyId,
                CustomerId = customerId,
                LaundryOrderId = orderId,
                Type = type,
                Status = FeedbackStatus.Open,
                Message = message.Trim(),
                Rating = rating
            };

            try
            {
                _db.Feedbacks.Add(feedback);
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = "Feedback submitted.", Id = feedback.Id };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }

        public OperationResult Escalate(int feedbackId)
        {
            var fb = _db.Feedbacks.FirstOrDefault(f => f.Id == feedbackId && f.CompanyId == _companyId);
            if (fb == null) return new OperationResult { Success = false, Message = "Feedback not found." };

            fb.Status = FeedbackStatus.Escalated;
            fb.UpdatedAt = DateTime.UtcNow;
            _db.SaveChanges();
            return new OperationResult { Success = true, Message = "Escalated to Manager." };
        }

        public OperationResult Resolve(int feedbackId, string resolvedByUserId, string resolutionNotes)
        {
            var fb = _db.Feedbacks.FirstOrDefault(f => f.Id == feedbackId && f.CompanyId == _companyId);
            if (fb == null) return new OperationResult { Success = false, Message = "Feedback not found." };

            fb.Status = FeedbackStatus.Resolved;
            fb.ResolutionNotes = string.IsNullOrWhiteSpace(resolutionNotes) ? null : resolutionNotes.Trim();
            fb.ResolvedByUserId = string.IsNullOrWhiteSpace(resolvedByUserId) ? null : resolvedByUserId;
            fb.ResolvedAt = DateTime.UtcNow;
            fb.UpdatedAt = DateTime.UtcNow;

            try
            {
                _db.SaveChanges();
                return new OperationResult { Success = true, Message = "Feedback resolved." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Database error: {ex.Message}" };
            }
        }

        public List<Feedback> GetAll() =>
            _db.Feedbacks.Where(f => f.CompanyId == _companyId)
                .Include(f => f.Customer).Include(f => f.ResolvedByUser)
                .OrderByDescending(f => f.CreatedAt)
                .ToList();

        public List<Feedback> GetByStatus(FeedbackStatus status) =>
            _db.Feedbacks.Where(f => f.CompanyId == _companyId)
                .Include(f => f.Customer)
                .Where(f => f.Status == status)
                .OrderByDescending(f => f.CreatedAt)
                .ToList();
    }
}
