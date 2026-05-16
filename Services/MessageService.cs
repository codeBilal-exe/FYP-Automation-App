using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class MessageService
    {
        private readonly AppDbContext _context;

        public MessageService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<User>> GetContactsForUser(int userId, UserRole role, string roleFilter = "All")
        {
            var query = _context.Users.Where(u => u.IsActive && u.Id != userId);

            if (role == UserRole.Student)
            {
                var group = await _context.Groups
                    .Include(g => g.Members)
                    .FirstOrDefaultAsync(g => g.Members.Any(m => m.Id == userId));

                if (group == null)
                    return new List<User>();

                var ids = group.Members.Select(m => m.Id).Where(id => id != userId).ToList();
                if (group.SupervisorId > 0)
                {
                    ids.Add(group.SupervisorId);
                }

                return await query.Where(u => ids.Contains(u.Id)).OrderBy(u => u.FullName).ToListAsync();
            }

            if (role == UserRole.Supervisor)
            {
                var groupIds = await _context.Groups
                    .Where(g => g.SupervisorId == userId)
                    .Select(g => g.Id)
                    .ToListAsync();

                return await query
                    .Where(u => u.Role == UserRole.Student && _context.Groups.Any(g => groupIds.Contains(g.Id) && g.Members.Any(m => m.Id == u.Id)))
                    .OrderBy(u => u.FullName)
                    .ToListAsync();
            }

            if (role == UserRole.HOD)
            {
                return await query
                    .Where(u => u.Role == UserRole.Coordinator || u.Role == UserRole.Supervisor)
                    .OrderBy(u => u.Role).ThenBy(u => u.FullName)
                    .ToListAsync();
            }

            if (role == UserRole.Admin || role == UserRole.Coordinator)
            {
                if (!string.Equals(roleFilter, "All", StringComparison.OrdinalIgnoreCase) && Enum.TryParse<UserRole>(roleFilter, true, out var filterRole))
                {
                    query = query.Where(u => u.Role == filterRole);
                }

                return await query.OrderBy(u => u.Role).ThenBy(u => u.FullName).ToListAsync();
            }

            return new List<User>();
        }

        public async Task<List<Group>> GetSupervisorMessagingGroups(int supervisorId)
        {
            return await _context.Groups
                .Include(g => g.Members)
                .Where(g => g.SupervisorId == supervisorId)
                .OrderBy(g => g.GroupName)
                .ToListAsync();
        }

        public async Task<bool> CanSendDirectMessage(int senderId, int recipientId)
        {
            var sender = await _context.Users.FirstOrDefaultAsync(u => u.Id == senderId && u.IsActive);
            var recipient = await _context.Users.FirstOrDefaultAsync(u => u.Id == recipientId && u.IsActive);
            if (sender == null || recipient == null || senderId == recipientId)
                return false;

            if (sender.Role == UserRole.Admin || sender.Role == UserRole.Coordinator)
                return true;

            if (sender.Role == UserRole.HOD)
            {
                return recipient.Role == UserRole.Coordinator || recipient.Role == UserRole.Supervisor;
            }

            if (sender.Role == UserRole.Student)
            {
                var group = await _context.Groups
                    .Include(g => g.Members)
                    .FirstOrDefaultAsync(g => g.Members.Any(m => m.Id == senderId));

                if (group == null)
                    return false;

                var memberIds = group.Members.Select(m => m.Id).ToHashSet();
                return memberIds.Contains(recipientId) || group.SupervisorId == recipientId;
            }

            if (sender.Role == UserRole.Supervisor)
            {
                var groups = await _context.Groups
                    .Include(g => g.Members)
                    .Where(g => g.SupervisorId == senderId)
                    .ToListAsync();

                var allowedStudents = groups.SelectMany(g => g.Members).Select(m => m.Id).ToHashSet();
                return allowedStudents.Contains(recipientId);
            }

            return false;
        }

        public async Task<bool> CanAccessGroupThread(int userId, int groupId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (user == null)
                return false;

            var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null)
                return false;

            if (user.Role == UserRole.Supervisor)
                return group.SupervisorId == userId;

            if (user.Role == UserRole.Student)
                return group.Members.Any(m => m.Id == userId);

            if (user.Role == UserRole.Admin || user.Role == UserRole.Coordinator)
                return true;

            return false;
        }

        public async Task<Message?> SendMessage(int senderId, int recipientId, string content)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(content))
                    return null;

                var canSend = await CanSendDirectMessage(senderId, recipientId);
                if (!canSend)
                    return null;

                var message = new Message
                {
                    SenderId = senderId,
                    RecipientId = recipientId,
                    Content = content.Trim(),
                    SentAt = DateTime.UtcNow,
                    DeliveredAt = null,
                    ReadAt = null,
                    IsRead = false,
                    GroupId = null
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();
                return message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send message error: {ex.Message}");
                return null;
            }
        }

        public async Task<Message?> SendGroupMessage(int senderId, int groupId, string content)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(content))
                    return null;

                var canAccess = await CanAccessGroupThread(senderId, groupId);
                if (!canAccess)
                    return null;

                var message = new Message
                {
                    SenderId = senderId,
                    RecipientId = 0,
                    GroupId = groupId,
                    Content = content.Trim(),
                    SentAt = DateTime.UtcNow,
                    DeliveredAt = DateTime.UtcNow,
                    ReadAt = null,
                    IsRead = false
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();
                return message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send group message error: {ex.Message}");
                return null;
            }
        }

        public async Task<List<Message>> GetMessages(int userId)
        {
            try
            {
                var messages = await _context.Messages
                    .Where(m => (m.RecipientId == userId && m.GroupId == null) || (m.GroupId == null && m.SenderId == userId))
                    .Include(m => m.Sender)
                    .OrderByDescending(m => m.SentAt)
                    .ToListAsync();

                var changed = false;
                foreach (var msg in messages.Where(m => m.RecipientId == userId && m.GroupId == null && m.DeliveredAt == null))
                {
                    msg.DeliveredAt = DateTime.UtcNow;
                    changed = true;
                }

                if (changed)
                {
                    _context.Messages.UpdateRange(messages.Where(m => m.RecipientId == userId && m.GroupId == null));
                    await _context.SaveChangesAsync();
                }

                return messages;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get messages error: {ex.Message}");
                return new List<Message>();
            }
        }

        public async Task<List<Message>> GetConversation(int userId1, int userId2)
        {
            try
            {
                var messages = await _context.Messages
                    .Where(m => m.GroupId == null && (
                        (m.SenderId == userId1 && m.RecipientId == userId2) ||
                        (m.SenderId == userId2 && m.RecipientId == userId1)
                    ))
                    .Include(m => m.Sender)
                    .OrderBy(m => m.SentAt)
                    .ToListAsync();

                var deliveredNow = DateTime.UtcNow;
                var changed = false;
                foreach (var msg in messages.Where(m => m.RecipientId == userId1 && m.DeliveredAt == null))
                {
                    msg.DeliveredAt = deliveredNow;
                    changed = true;
                }

                if (changed)
                {
                    _context.Messages.UpdateRange(messages.Where(m => m.RecipientId == userId1));
                    await _context.SaveChangesAsync();
                }

                return messages;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get conversation error: {ex.Message}");
                return new List<Message>();
            }
        }

        public async Task<List<Message>> GetGroupMessages(int groupId)
        {
            try
            {
                return await _context.Messages
                    .Where(m => m.GroupId == groupId)
                    .Include(m => m.Sender)
                    .OrderBy(m => m.SentAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get group messages error: {ex.Message}");
                return new List<Message>();
            }
        }

        public async Task<bool> MarkAsRead(int messageId)
        {
            try
            {
                var message = await _context.Messages.FirstOrDefaultAsync(m => m.Id == messageId);
                if (message == null)
                    return false;

                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
                if (!message.DeliveredAt.HasValue)
                {
                    message.DeliveredAt = message.ReadAt;
                }
                _context.Messages.Update(message);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mark message as read error: {ex.Message}");
                return false;
            }
        }

        public async Task<int> GetUnreadCount(int userId)
        {
            try
            {
                return await _context.Messages
                    .CountAsync(m => m.RecipientId == userId && !m.IsRead && m.GroupId == null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get unread count error: {ex.Message}");
                return 0;
            }
        }

        public async Task<int> GetUnreadGroupCount(int userId)
        {
            try
            {
                return await _context.Messages
                    .CountAsync(m => m.GroupId != null && !m.IsRead && m.SenderId != userId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get unread group count error: {ex.Message}");
                return 0;
            }
        }

        public async Task<bool> DeleteMessage(int messageId)
        {
            try
            {
                var message = await _context.Messages.FirstOrDefaultAsync(m => m.Id == messageId);
                if (message == null)
                    return false;

                _context.Messages.Remove(message);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete message error: {ex.Message}");
                return false;
            }
        }

        public async Task<Message?> GetMessageById(int id)
        {
            try
            {
                return await _context.Messages
                    .Include(m => m.Sender)
                    .FirstOrDefaultAsync(m => m.Id == id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get message by id error: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> MarkConversationAsRead(int userId, int otherUserId)
        {
            try
            {
                var messages = await _context.Messages
                    .Where(m => m.GroupId == null && m.RecipientId == userId && m.SenderId == otherUserId && !m.IsRead)
                    .ToListAsync();

                foreach (var message in messages)
                {
                    if (!message.DeliveredAt.HasValue)
                    {
                        message.DeliveredAt = DateTime.UtcNow;
                    }
                    message.IsRead = true;
                    message.ReadAt = DateTime.UtcNow;
                }

                _context.Messages.UpdateRange(messages);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mark conversation as read error: {ex.Message}");
                return false;
            }
        }

        public async Task<int> SendBroadcastMessage(int senderId, UserRole targetRole, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return 0;

            var sender = await _context.Users.FirstOrDefaultAsync(u => u.Id == senderId && u.IsActive);
            if (sender == null || (sender.Role != UserRole.Admin && sender.Role != UserRole.Coordinator))
                return 0;

            var recipients = await _context.Users
                .Where(u => u.IsActive && u.Id != senderId && u.Role == targetRole)
                .Select(u => u.Id)
                .ToListAsync();

            var created = 0;
            foreach (var recipientId in recipients)
            {
                var sent = await SendMessage(senderId, recipientId, content);
                if (sent != null)
                {
                    created++;
                }
            }

            return created;
        }

        public async Task<int> GetSidebarUnreadCount(int userId)
        {
            var direct = await GetUnreadCount(userId);
            var group = await GetUnreadGroupCount(userId);
            return direct + group;
        }

        public async Task<Dictionary<int, int>> GetUnreadCountsBySender(int userId)
        {
            try
            {
                return await _context.Messages
                    .Where(m => m.GroupId == null && m.RecipientId == userId && !m.IsRead)
                    .GroupBy(m => m.SenderId)
                    .Select(g => new { SenderId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.SenderId, x => x.Count);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get unread counts by sender error: {ex.Message}");
                return new Dictionary<int, int>();
            }
        }

        public async Task<Dictionary<int, int>> GetUnreadCountsByGroup(int userId)
        {
            try
            {
                return await _context.Messages
                    .Where(m => m.GroupId != null && m.SenderId != userId && !m.IsRead)
                    .GroupBy(m => m.GroupId!.Value)
                    .Select(g => new { GroupId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.GroupId, x => x.Count);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get unread counts by group error: {ex.Message}");
                return new Dictionary<int, int>();
            }
        }
    }
}
