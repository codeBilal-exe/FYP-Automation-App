using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class VivaService
    {
        private readonly AppDbContext _context;

        public VivaService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Creates a new viva slot for a project
        /// </summary>
        public async Task<VivaSlot?> CreateVivaSlot(DateTime scheduledAt, string venue, int projectId)
        {
            try
            {
                // Verify project exists
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
                if (project == null)
                    return null;

                var vivaSlot = new VivaSlot
                {
                    ScheduledAt = scheduledAt,
                    Venue = venue,
                    ProjectId = projectId,
                    Status = VivaStatus.Scheduled,
                    PanelMembers = new List<User>()
                };

                _context.VivaSlots.Add(vivaSlot);
                await _context.SaveChangesAsync();
                return vivaSlot;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create viva slot error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Assigns a panel member to a viva slot
        /// </summary>
        public async Task<bool> AssignPanelMember(int vivaSlotId, int userId)
        {
            try
            {
                var vivaSlot = await _context.VivaSlots.Include(v => v.PanelMembers).FirstOrDefaultAsync(v => v.Id == vivaSlotId);
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

                if (vivaSlot == null || user == null)
                    return false;

                // Check if already assigned
                if (vivaSlot.PanelMembers.Any(p => p.Id == userId))
                    return false;

                vivaSlot.PanelMembers.Add(user);
                _context.VivaSlots.Update(vivaSlot);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Assign panel member error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes a panel member from a viva slot
        /// </summary>
        public async Task<bool> RemovePanelMember(int vivaSlotId, int userId)
        {
            try
            {
                var vivaSlot = await _context.VivaSlots.Include(v => v.PanelMembers).FirstOrDefaultAsync(v => v.Id == vivaSlotId);
                if (vivaSlot == null)
                    return false;

                var user = vivaSlot.PanelMembers.FirstOrDefault(p => p.Id == userId);
                if (user == null)
                    return false;

                vivaSlot.PanelMembers.Remove(user);
                _context.VivaSlots.Update(vivaSlot);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Remove panel member error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Retrieves all viva slots for a project
        /// </summary>
        public async Task<List<VivaSlot>> GetVivaSlotsByProject(int projectId)
        {
            try
            {
                return await _context.VivaSlots
                    .Where(v => v.ProjectId == projectId)
                    .Include(v => v.PanelMembers)
                    .OrderBy(v => v.ScheduledAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get viva slots by project error: {ex.Message}");
                return new List<VivaSlot>();
            }
        }

        /// <summary>
        /// Updates viva slot status
        /// </summary>
        public async Task<bool> UpdateVivaStatus(int id, VivaStatus status)
        {
            try
            {
                var vivaSlot = await _context.VivaSlots.FirstOrDefaultAsync(v => v.Id == id);
                if (vivaSlot == null)
                    return false;

                vivaSlot.Status = status;
                _context.VivaSlots.Update(vivaSlot);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update viva status error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Retrieves upcoming vivas for notifications
        /// </summary>
        public async Task<List<VivaSlot>> GetUpcomingVivas()
        {
            try
            {
                var now = DateTime.UtcNow;
                return await _context.VivaSlots
                    .Where(v => v.ScheduledAt > now && v.Status == VivaStatus.Scheduled)
                    .Include(v => v.PanelMembers)
                    .OrderBy(v => v.ScheduledAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get upcoming vivas error: {ex.Message}");
                return new List<VivaSlot>();
            }
        }

        /// <summary>
        /// Retrieves upcoming vivas within next N days
        /// </summary>
        public async Task<List<VivaSlot>> GetUpcomingVivasWithinDays(int daysAhead)
        {
            try
            {
                var now = DateTime.UtcNow;
                var futureDate = now.AddDays(daysAhead);

                return await _context.VivaSlots
                    .Where(v => v.ScheduledAt >= now && v.ScheduledAt <= futureDate && v.Status == VivaStatus.Scheduled)
                    .Include(v => v.PanelMembers)
                    .OrderBy(v => v.ScheduledAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get upcoming vivas within days error: {ex.Message}");
                return new List<VivaSlot>();
            }
        }

        /// <summary>
        /// Gets viva slot by ID
        /// </summary>
        public async Task<VivaSlot?> GetVivaSlotById(int id)
        {
            try
            {
                return await _context.VivaSlots
                    .Include(v => v.PanelMembers)
                    .FirstOrDefaultAsync(v => v.Id == id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get viva slot by id error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets all viva slots for a panel member
        /// </summary>
        public async Task<List<VivaSlot>> GetVivaSlotsForPanelMember(int panelMemberId)
        {
            try
            {
                return await _context.VivaSlots
                    .Where(v => v.PanelMembers.Any(p => p.Id == panelMemberId))
                    .OrderBy(v => v.ScheduledAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get viva slots for panel member error: {ex.Message}");
                return new List<VivaSlot>();
            }
        }

        /// <summary>
        /// Deletes a viva slot
        /// </summary>
        public async Task<bool> DeleteVivaSlot(int id)
        {
            try
            {
                var vivaSlot = await _context.VivaSlots.FirstOrDefaultAsync(v => v.Id == id);
                if (vivaSlot == null)
                    return false;

                _context.VivaSlots.Remove(vivaSlot);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete viva slot error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates viva slot details
        /// </summary>
        public async Task<bool> UpdateVivaSlot(int id, DateTime scheduledAt, string venue)
        {
            try
            {
                var vivaSlot = await _context.VivaSlots.FirstOrDefaultAsync(v => v.Id == id);
                if (vivaSlot == null)
                    return false;

                vivaSlot.ScheduledAt = scheduledAt;
                vivaSlot.Venue = venue;
                _context.VivaSlots.Update(vivaSlot);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update viva slot error: {ex.Message}");
                return false;
            }
        }
    }
}
