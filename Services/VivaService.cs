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
        /// Creates an enhanced viva slot with group, time slot, and panel assignment
        /// </summary>
        public async Task<VivaSlot?> CreateScheduledSlot(
            DateTime date,
            TimeSpan startTime,
            TimeSpan endTime,
            string venue,
            int groupId,
            int? milestoneId,
            SlotType slotType,
            List<int> panelMemberIds)
        {
            try
            {
                var group = await _context.Groups
                    .Include(g => g.Project)
                    .FirstOrDefaultAsync(g => g.Id == groupId);
                if (group == null) return null;

                var projectId = group.Project?.Id ?? 0;
                if (projectId == 0) return null;

                var scheduledAt = date.Date + startTime;

                var vivaSlot = new VivaSlot
                {
                    ScheduledAt = scheduledAt,
                    Venue = venue,
                    ProjectId = projectId,
                    GroupId = groupId,
                    MilestoneId = milestoneId,
                    SlotType = slotType,
                    StartTime = startTime,
                    EndTime = endTime,
                    Status = VivaStatus.Scheduled,
                    PanelMembers = new List<User>()
                };

                _context.VivaSlots.Add(vivaSlot);
                await _context.SaveChangesAsync();

                // Assign panel members
                if (panelMemberIds.Count > 0)
                {
                    var slot = await _context.VivaSlots
                        .Include(v => v.PanelMembers)
                        .FirstAsync(v => v.Id == vivaSlot.Id);

                    var users = await _context.Users
                        .Where(u => panelMemberIds.Contains(u.Id))
                        .ToListAsync();

                    foreach (var u in users)
                    {
                        slot.PanelMembers.Add(u);
                    }
                    await _context.SaveChangesAsync();
                }

                return vivaSlot;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create scheduled slot error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets faculty who are FREE during a given day + time range.
        /// Pulls all data to memory to avoid EF Core translation issues with Npgsql.
        /// </summary>
        public async Task<List<User>> GetAvailableFaculty(DayOfWeek day, TimeSpan startTime, TimeSpan endTime, DateTime? date = null)
        {
            // Step 1: Always get all faculty — this must never fail
            var facultyRoles = new[]
            {
                UserRole.Supervisor,
                UserRole.Panel,
                UserRole.HOD,
                UserRole.Coordinator
            };

            List<User> allFaculty;
            try
            {
                allFaculty = await _context.Users
                    .Where(u => u.IsActive && facultyRoles.Contains(u.Role))
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get faculty users error: {ex.Message}");
                return new List<User>();
            }

            if (allFaculty.Count == 0)
                return allFaculty;

            // Step 2: Try to filter by timetable (if table exists)
            var busyInTimetable = new HashSet<int>();
            try
            {
                var dayInt = (int)day;
                var allTimetable = await _context.FacultyTimetables.ToListAsync();
                busyInTimetable = allTimetable
                    .Where(ft => (int)ft.Day == dayInt &&
                                 ft.StartTime < endTime &&
                                 ft.EndTime > startTime)
                    .Select(ft => ft.FacultyId)
                    .Distinct()
                    .ToHashSet();
            }
            catch (Exception ex)
            {
                // FacultyTimetables table may not exist yet — skip filtering
                Console.WriteLine($"Timetable check skipped (table may not exist): {ex.Message}");
            }

            // Step 3: Try to filter by existing viva slot overlaps
            var busyInViva = new HashSet<int>();
            if (date.HasValue)
            {
                try
                {
                    var dateStart = date.Value.Date;
                    var dateEnd = dateStart.AddDays(1);

                    var existingSlots = await _context.VivaSlots
                        .Include(v => v.PanelMembers)
                        .Where(v => v.ScheduledAt >= dateStart &&
                                    v.ScheduledAt < dateEnd &&
                                    v.Status == VivaStatus.Scheduled)
                        .ToListAsync();

                    busyInViva = existingSlots
                        .Where(v => v.StartTime < endTime && v.EndTime > startTime)
                        .SelectMany(v => v.PanelMembers.Select(p => p.Id))
                        .Distinct()
                        .ToHashSet();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Viva overlap check skipped: {ex.Message}");
                }
            }

            var busyIds = busyInTimetable.Union(busyInViva).ToHashSet();

            return allFaculty
                .Where(f => !busyIds.Contains(f.Id))
                .OrderBy(f => f.FullName)
                .ToList();
        }

        /// <summary>
        /// Imports timetable data from parsed rows (used by CSV/Excel upload).
        /// Clears existing entries for matched faculty before inserting.
        /// </summary>
        public async Task<(int Imported, int Skipped, List<string> Errors)> ImportTimetableAsync(
            List<TimetableImportRow> rows)
        {
            int imported = 0, skipped = 0;
            var errors = new List<string>();

            try
            {
                // Resolve faculty by email
                var emails = rows.Select(r => r.FacultyEmail.ToLowerInvariant()).Distinct().ToList();
                var facultyMap = await _context.Users
                    .Where(u => emails.Contains(u.Email.ToLower()) && u.IsActive)
                    .ToDictionaryAsync(u => u.Email.ToLowerInvariant(), u => u.Id);

                // Clear existing timetable for matched faculty
                var matchedIds = facultyMap.Values.ToList();
                var existingEntries = await _context.FacultyTimetables
                    .Where(ft => matchedIds.Contains(ft.FacultyId))
                    .ToListAsync();
                _context.FacultyTimetables.RemoveRange(existingEntries);

                foreach (var row in rows)
                {
                    var email = row.FacultyEmail.Trim().ToLowerInvariant();
                    if (!facultyMap.TryGetValue(email, out var facultyId))
                    {
                        errors.Add($"Faculty not found: {row.FacultyEmail}");
                        skipped++;
                        continue;
                    }

                    if (!TryParseDay(row.Day, out var dayOfWeek))
                    {
                        errors.Add($"Invalid day '{row.Day}' for {row.FacultyEmail}");
                        skipped++;
                        continue;
                    }

                    if (!TimeSpan.TryParse(row.StartTime, out var start) ||
                        !TimeSpan.TryParse(row.EndTime, out var end))
                    {
                        errors.Add($"Invalid time '{row.StartTime}-{row.EndTime}' for {row.FacultyEmail}");
                        skipped++;
                        continue;
                    }

                    _context.FacultyTimetables.Add(new FacultyTimetable
                    {
                        FacultyId = facultyId,
                        Day = dayOfWeek,
                        StartTime = start,
                        EndTime = end,
                        Subject = row.Subject?.Trim() ?? "",
                        RoomNumber = row.RoomNumber?.Trim() ?? ""
                    });
                    imported++;
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                errors.Add($"Import error: {ex.Message}");
            }

            return (imported, skipped, errors);
        }

        private static bool TryParseDay(string dayStr, out DayOfWeek day)
        {
            day = DayOfWeek.Monday;
            if (string.IsNullOrWhiteSpace(dayStr)) return false;
            var d = dayStr.Trim().ToLowerInvariant();
            return d switch
            {
                "monday" or "mon" => SetDay(out day, DayOfWeek.Monday),
                "tuesday" or "tue" => SetDay(out day, DayOfWeek.Tuesday),
                "wednesday" or "wed" => SetDay(out day, DayOfWeek.Wednesday),
                "thursday" or "thu" => SetDay(out day, DayOfWeek.Thursday),
                "friday" or "fri" => SetDay(out day, DayOfWeek.Friday),
                "saturday" or "sat" => SetDay(out day, DayOfWeek.Saturday),
                "sunday" or "sun" => SetDay(out day, DayOfWeek.Sunday),
                _ => int.TryParse(d, out var num) && num >= 0 && num <= 6
                    ? SetDay(out day, (DayOfWeek)num)
                    : false
            };
        }

        private static bool SetDay(out DayOfWeek day, DayOfWeek value)
        {
            day = value;
            return true;
        }

        /// <summary>
        /// Gets all scheduled viva slots with full navigation data
        /// </summary>
        public async Task<List<VivaSlot>> GetAllScheduledSlots()
        {
            try
            {
                return await _context.VivaSlots
                    .Include(v => v.PanelMembers)
                    .Include(v => v.Group)
                    .Include(v => v.Milestone)
                    .OrderByDescending(v => v.ScheduledAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get all scheduled slots error: {ex.Message}");
                return new List<VivaSlot>();
            }
        }

        /// <summary>
        /// Gets timetable entry count (for display)
        /// </summary>
        public async Task<int> GetTimetableCount()
        {
            try { return await _context.FacultyTimetables.CountAsync(); }
            catch { return 0; }
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
                var vivaSlot = await _context.VivaSlots
                    .Include(v => v.PanelMembers)
                    .FirstOrDefaultAsync(v => v.Id == id);
                if (vivaSlot == null)
                    return false;

                vivaSlot.PanelMembers.Clear();
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

    /// <summary>
    /// DTO for timetable CSV/Excel import rows
    /// </summary>
    public class TimetableImportRow
    {
        public string FacultyEmail { get; set; } = "";
        public string Day { get; set; } = "";
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public string? Subject { get; set; }
        public string? RoomNumber { get; set; }
    }
}
