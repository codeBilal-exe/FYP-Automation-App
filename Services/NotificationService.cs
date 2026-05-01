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

        /// <summary>
        /// Creates a new notification for a user
        /// </summary>
        public async Task<Notification?> CreateNotification(string title, string message, NotificationType type, int recipientId)
        {
            try
            {
                // Verify recipient exists
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
                    CreatedAt = DateTime.UtcNow
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

        /// <summary>
        /// Retrieves all notifications for a user
        /// </summary>
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

        /// <summary>
        /// Marks a notification as read
        /// </summary>
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

        /// <summary>
        /// Counts unread notifications for a user
        /// </summary>
        public async Task<int> CountUnread(int userId)
        {
            try
            {
                return await _context.Notifications
                    .CountAsync(n => n.RecipientId == userId && !n.IsRead);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Count unread error: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Deletes a notification
        /// </summary>
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

        /// <summary>
        /// Marks all notifications as read for a user
        /// </summary>
        public async Task<bool> MarkAllAsRead(int userId)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.RecipientId == userId && !n.IsRead)
                    .ToListAsync();

                if (notifications.Count == 0)
                    return true;

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

        /// <summary>
        /// Gets unread notifications for a user
        /// </summary>
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

        /// <summary>
        /// Gets notification by ID
        /// </summary>
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

        /// <summary>
        /// Deletes all notifications older than specified days
        /// </summary>
        public async Task<bool> DeleteOldNotifications(int daysOld)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
                var oldNotifications = await _context.Notifications
                    .Where(n => n.CreatedAt < cutoffDate)
                    .ToListAsync();

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
