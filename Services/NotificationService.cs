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

        public async Task<Notification?> CreateNotification(
            string title,
            string description,
            NotificationType type,
            int recipientId,
            string eventType = "general",
            string? referenceId = null,
            string? linkUrl = null,
            TimeSpan? dedupeWindow = null)
        {
            try
            {
                var recipient = await _context.Users.FirstOrDefaultAsync(u => u.Id == recipientId && u.IsActive);
                if (recipient == null)
                    return null;

                var window = dedupeWindow ?? TimeSpan.FromSeconds(60);
                var since = DateTime.UtcNow.Subtract(window);
                var duplicateExists = await _context.Notifications.AnyAsync(n =>
                    n.RecipientId == recipientId &&
                    n.EventType == eventType &&
                    n.ReferenceId == referenceId &&
                    n.CreatedAt >= since);

                if (duplicateExists)
                    return null;

                var now = DateTime.UtcNow;
                var notification = new Notification
                {
                    Title = title,
                    Description = description,
                    Type = type,
                    RecipientId = recipientId,
                    RecipientRole = recipient.Role.ToString(),
                    EventType = eventType,
                    ReferenceId = referenceId,
                    LinkUrl = linkUrl,
                    IsRead = false,
                    CreatedAt = now,
                    SentAt = now,
                    ExpiresAt = now.AddDays(30)
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

        public async Task<int> CreateNotificationsForUsers(
            IEnumerable<int> recipientIds,
            string title,
            string description,
            NotificationType type,
            string eventType,
            string? referenceId,
            string? linkUrl)
        {
            var uniqueIds = recipientIds.Distinct().ToList();
            var created = 0;

            foreach (var recipientId in uniqueIds)
            {
                var notification = await CreateNotification(title, description, type, recipientId, eventType, referenceId, linkUrl);
                if (notification != null)
                {
                    created++;
                }
            }

            return created;
        }

        public async Task NotifyProposalStatusForGroup(
            int groupId,
            string title,
            string message,
            NotificationType type,
            string eventType,
            string referenceId,
            string linkUrl)
        {
            var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null)
                return;

            var recipientIds = group.Members.Select(m => m.Id).Distinct();
            await CreateNotificationsForUsers(recipientIds, title, message, type, eventType, referenceId, linkUrl);
        }

        public async Task<List<Notification>> GetNotificationsByUser(int userId)
        {
            try
            {
                var now = DateTime.UtcNow;
                return await _context.Notifications
                    .Where(n => n.RecipientId == userId && n.ExpiresAt >= now)
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
                var now = DateTime.UtcNow;
                return await _context.Notifications.CountAsync(n => n.RecipientId == userId && !n.IsRead && n.ExpiresAt >= now);
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
                {
                    notification.IsRead = true;
                }

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

        public async Task<bool> ClearAll(int userId)
        {
            try
            {
                var notifications = await _context.Notifications.Where(n => n.RecipientId == userId).ToListAsync();
                if (notifications.Count == 0)
                    return true;

                _context.Notifications.RemoveRange(notifications);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Clear all notifications error: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Notification>> GetUnreadNotifications(int userId)
        {
            try
            {
                var now = DateTime.UtcNow;
                return await _context.Notifications
                    .Where(n => n.RecipientId == userId && !n.IsRead && n.ExpiresAt >= now)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get unread notifications error: {ex.Message}");
                return new List<Notification>();
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
    }
}
