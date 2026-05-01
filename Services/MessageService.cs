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

        /// <summary>
        /// Sends a one-to-one message between users
        /// </summary>
        public async Task<Message?> SendMessage(int senderId, int recipientId, string content)
        {
            try
            {
                // Verify both users exist
                var sender = await _context.Users.FirstOrDefaultAsync(u => u.Id == senderId);
                var recipient = await _context.Users.FirstOrDefaultAsync(u => u.Id == recipientId);

                if (sender == null || recipient == null || senderId == recipientId)
                    return null;

                if (string.IsNullOrWhiteSpace(content))
                    return null;

                var message = new Message
                {
                    SenderId = senderId,
                    RecipientId = recipientId,
                    Content = content,
                    SentAt = DateTime.UtcNow,
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

        /// <summary>
        /// Sends a message to a group
        /// </summary>
        public async Task<Message?> SendGroupMessage(int senderId, int groupId, string content)
        {
            try
            {
                // Verify sender and group exist
                var sender = await _context.Users.FirstOrDefaultAsync(u => u.Id == senderId);
                var group = await _context.Groups.FirstOrDefaultAsync(g => g.Id == groupId);

                if (sender == null || group == null)
                    return null;

                if (string.IsNullOrWhiteSpace(content))
                    return null;

                var message = new Message
                {
                    SenderId = senderId,
                    RecipientId = 0,
                    GroupId = groupId,
                    Content = content,
                    SentAt = DateTime.UtcNow,
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

        /// <summary>
        /// Retrieves inbox messages for a user (as recipient)
        /// </summary>
        public async Task<List<Message>> GetMessages(int userId)
        {
            try
            {
                return await _context.Messages
                    .Where(m => (m.RecipientId == userId && m.GroupId == null) || (m.GroupId == null && m.SenderId == userId))
                    .Include(m => m.Sender)
                    .OrderByDescending(m => m.SentAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get messages error: {ex.Message}");
                return new List<Message>();
            }
        }

        /// <summary>
        /// Retrieves conversation between two users
        /// </summary>
        public async Task<List<Message>> GetConversation(int userId1, int userId2)
        {
            try
            {
                return await _context.Messages
                    .Where(m => m.GroupId == null && (
                        (m.SenderId == userId1 && m.RecipientId == userId2) ||
                        (m.SenderId == userId2 && m.RecipientId == userId1)
                    ))
                    .Include(m => m.Sender)
                    .OrderBy(m => m.SentAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get conversation error: {ex.Message}");
                return new List<Message>();
            }
        }

        /// <summary>
        /// Retrieves all messages in a group
        /// </summary>
        public async Task<List<Message>> GetGroupMessages(int groupId)
        {
            try
            {
                return await _context.Messages
                    .Where(m => m.GroupId == groupId)
                    .Include(m => m.Sender)
                    .OrderByDescending(m => m.SentAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get group messages error: {ex.Message}");
                return new List<Message>();
            }
        }

        /// <summary>
        /// Marks a message as read
        /// </summary>
        public async Task<bool> MarkAsRead(int messageId)
        {
            try
            {
                var message = await _context.Messages.FirstOrDefaultAsync(m => m.Id == messageId);
                if (message == null)
                    return false;

                message.IsRead = true;
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

        /// <summary>
        /// Gets unread message count for a user
        /// </summary>
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

        /// <summary>
        /// Gets unread count in groups for a user
        /// </summary>
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

        /// <summary>
        /// Deletes a message
        /// </summary>
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

        /// <summary>
        /// Gets message by ID
        /// </summary>
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

        /// <summary>
        /// Marks all messages in a conversation as read
        /// </summary>
        public async Task<bool> MarkConversationAsRead(int userId, int otherUserId)
        {
            try
            {
                var messages = await _context.Messages
                    .Where(m => m.GroupId == null && m.RecipientId == userId && m.SenderId == otherUserId && !m.IsRead)
                    .ToListAsync();

                if (messages.Count == 0)
                    return true;

                foreach (var message in messages)
                {
                    message.IsRead = true;
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
    }
}
