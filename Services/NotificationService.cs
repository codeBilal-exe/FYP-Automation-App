using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class NotificationService
    {
        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Notification?> CreateNotification(string title, string message, NotificationType type, int recipientId)
        {
            try
            {
                var recipient = await _context.Users.FirstOrDefaultAsync(u => u.Id == recipientId);
                if (recipient == null)
                    return null;

                var notification = new Notification
                {
                    Title = title,
                    Message = message,
                    Type = type,
                    RecipientId = recipientId,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    SentAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                return notification;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create notification error: {ex.Message}");
                return null;
            }
        }

        public async Task NotifyHODDecision(int proposalId, bool approved, string? feedback)
        {
            var proposal = await _context.Proposals.FirstOrDefaultAsync(p => p.Id == proposalId)
                ?? throw new InvalidOperationException("Proposal not found.");

            var group = await _context.Groups
                .Include(g => g.Members)
                .Include(g => g.Supervisor)
                .FirstOrDefaultAsync(g => g.Id == proposal.GroupId)
                ?? throw new InvalidOperationException("Group not found for proposal.");

            var coordinator = await _context.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Coordinator && u.IsActive);
            var hod = await _context.Users.FirstOrDefaultAsync(u => u.Role == UserRole.HOD && u.IsActive);

            var message = approved
                ? "Your proposal has been approved by the HOD. Your project is now Active."
                : $"Your proposal has been rejected by the HOD. Reason: {feedback}";

            var recipients = new List<int>();
            recipients.AddRange(group.Members.Select(m => m.Id));
            if (group.SupervisorId > 0) recipients.Add(group.SupervisorId);
            if (coordinator != null) recipients.Add(coordinator.Id);
            recipients = recipients.Distinct().ToList();

            foreach (var recipientId in recipients)
            {
                _context.Notifications.Add(new Notification
                {
                    Title = approved ? "Proposal Approved by HOD" : "Proposal Rejected by HOD",
                    Message = message,
                    Type = NotificationType.ProposalDecision,
                    RecipientId = recipientId,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    SentAt = DateTime.UtcNow
                });
            }

            if (hod != null)
            {
                _context.Notifications.Add(new Notification
                {
                    Title = "HOD Decision Recorded",
                    Message = approved
                        ? $"You approved proposal #{proposalId}."
                        : $"You rejected proposal #{proposalId}.",
                    Type = NotificationType.ProposalDecision,
                    RecipientId = hod.Id,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    SentAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<Notification>> GetNotificationsByUser(int userId)
        {
            try
            {
                return await _context.Notifications
                    .Where(n => n.RecipientId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get notifications by user error: {ex.Message}");
                return new List<Notification>();
            }
        }

        public async Task<bool> MarkAsRead(int notificationId)
        {
            try
            {
                var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId);
                if (notification == null)
                    return false;

                notification.IsRead = true;
                _context.Notifications.Update(notification);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mark as read error: {ex.Message}");
                return false;
            }
        }

        public async Task<int> CountUnread(int userId)
        {
            try
            {
                return await _context.Notifications.CountAsync(n => n.RecipientId == userId && !n.IsRead);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Count unread error: {ex.Message}");
                return 0;
            }
        }

        public async Task<bool> DeleteNotification(int notificationId)
        {
            try
            {
                var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId);
                if (notification == null)
                    return false;

                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete notification error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> MarkAllAsRead(int userId)
        {
            try
            {
                var notifications = await _context.Notifications.Where(n => n.RecipientId == userId && !n.IsRead).ToListAsync();
                foreach (var notification in notifications)
                    notification.IsRead = true;

                _context.Notifications.UpdateRange(notifications);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mark all as read error: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Notification>> GetUnreadNotifications(int userId)
        {
            try
            {
                return await _context.Notifications
                    .Where(n => n.RecipientId == userId && !n.IsRead)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get unread notifications error: {ex.Message}");
                return new List<Notification>();
            }
        }

        public async Task<Notification?> GetNotificationById(int id)
        {
            try
            {
                return await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get notification by id error: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> DeleteOldNotifications(int daysOld)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
                var oldNotifications = await _context.Notifications.Where(n => n.CreatedAt < cutoffDate).ToListAsync();
                if (oldNotifications.Count > 0)
                {
                    _context.Notifications.RemoveRange(oldNotifications);
                    await _context.SaveChangesAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete old notifications error: {ex.Message}");
                return false;
            }
        }
    }
}
