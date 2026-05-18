using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class VivaService
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;
        private static readonly UserRole[] SchedulingFacultyRoles = [UserRole.Supervisor, UserRole.Panel];
        private static readonly TimeSpan DefaultDayStart = new(8, 0, 0);
        private static readonly TimeSpan DefaultDayEnd = new(17, 0, 0);

        public VivaService(AppDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
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
        /// Creates an enhanced viva slot with group, time slot, and panel assignment.
        /// Returns a structured outcome so the UI can show the exact reason on rejection.
        /// </summary>
        public async Task<ScheduleSlotOutcome> CreateScheduledSlot(
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
                if (endTime <= startTime)
                    return Fail(ScheduleResult.InvalidTimeRange, "End time must be after start time.");
                if (string.IsNullOrWhiteSpace(venue))
                    return Fail(ScheduleResult.VenueRequired, "Venue is required.");
                if (startTime < DefaultDayStart || endTime > DefaultDayEnd)
                    return Fail(ScheduleResult.InvalidTimeRange, $"Slots must be within {DefaultDayStart:hh\\:mm}–{DefaultDayEnd:hh\\:mm}.");

                var group = await _context.Groups
                    .Include(g => g.Members)
                    .Include(g => g.Project)
                    .Include(g => g.Supervisor)
                    .FirstOrDefaultAsync(g => g.Id == groupId);
                if (group == null)
                    return Fail(ScheduleResult.GroupNotFound, "Selected group was not found.");
                if (group.SupervisorId <= 0 || group.Supervisor == null || !group.Supervisor.IsActive)
                    return Fail(ScheduleResult.SupervisorMissing, "This group has no active supervisor assigned.");

                var cleanVenue = venue.Trim();
                var dateStart = ToUtcDate(date);
                if (dateStart < ToUtcDate(DateTime.UtcNow))
                    return Fail(ScheduleResult.DateInPast, "Date cannot be in the past.");

                var dateEnd = dateStart.AddDays(1);
                var dayOfWeek = dateStart.DayOfWeek;

                var daySlots = await _context.VivaSlots
                    .Include(v => v.PanelMembers)
                    .Include(v => v.Group)
                    .Where(v => v.ScheduledAt >= dateStart &&
                                v.ScheduledAt < dateEnd &&
                                v.Status == VivaStatus.Scheduled)
                    .ToListAsync();

                // Venue must be unique for the same overlapping time range.
                if (daySlots.Any(v =>
                        string.Equals(v.Venue.Trim(), cleanVenue, StringComparison.OrdinalIgnoreCase) &&
                        Overlaps(v.StartTime, v.EndTime, startTime, endTime)))
                {
                    return Fail(ScheduleResult.VenueDoubleBooked,
                        $"Venue '{cleanVenue}' is already booked for an overlapping time.");
                }

                // The same group cannot have overlapping slots.
                if (daySlots.Any(v =>
                        v.GroupId == groupId &&
                        Overlaps(v.StartTime, v.EndTime, startTime, endTime)))
                {
                    return Fail(ScheduleResult.GroupDoubleBooked,
                        "This group already has another slot in an overlapping time.");
                }

                var timetableRows = await _context.FacultyTimetables
                    .Where(ft => ft.Day == dayOfWeek)
                    .ToListAsync();

                // SOURCE OF TRUTH: supervisor must be free per FacultyTimetables AND VivaSlots.
                if (HasTimetableConflict(group.SupervisorId, startTime, endTime, timetableRows))
                {
                    return Fail(ScheduleResult.SlotNotFreeForSupervisor,
                        $"Supervisor {group.Supervisor.FullName} has a class scheduled in this time window.");
                }
                if (HasOverlappingVivaCommitment(group.SupervisorId, startTime, endTime, daySlots))
                {
                    return Fail(ScheduleResult.SupervisorHasOverlappingViva,
                        $"Supervisor {group.Supervisor.FullName} is already booked for another viva in this time window.");
                }

                var uniquePanelIds = panelMemberIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();
                if (uniquePanelIds.Count == 0)
                    return Fail(ScheduleResult.NoPanelSelected, "Please assign at least one panel member.");

                // Panel options are restricted to supervisors and panel members.
                var panelUsers = await _context.Users
                    .Where(u => u.IsActive &&
                                SchedulingFacultyRoles.Contains(u.Role) &&
                                uniquePanelIds.Contains(u.Id))
                    .ToListAsync();
                if (panelUsers.Count != uniquePanelIds.Count)
                    return Fail(ScheduleResult.PanelInvalid, "One or more selected panel members are inactive or invalid.");

                // Group supervisor is mandatory attendee; avoid adding duplicate as panel member.
                panelUsers = panelUsers
                    .Where(u => u.Id != group.SupervisorId)
                    .ToList();
                if (panelUsers.Count == 0)
                    return Fail(ScheduleResult.NoPanelSelected, "Panel must contain at least one member other than the supervisor.");

                // No panel member can be busy in timetable or overlapping viva commitments.
                var firstBusyPanel = panelUsers.FirstOrDefault(u =>
                    HasTimetableConflict(u.Id, startTime, endTime, timetableRows) ||
                    HasOverlappingVivaCommitment(u.Id, startTime, endTime, daySlots));
                if (firstBusyPanel != null)
                {
                    return Fail(ScheduleResult.PanelMemberBusy,
                        $"Panel member {firstBusyPanel.FullName} is not free in this time window.");
                }

                var project = await EnsureProjectForGroupAsync(group);
                if (project == null)
                    return Fail(ScheduleResult.ProjectCreateFailed, "Could not create or attach a project for this group.");

                var scheduledAt = DateTime.SpecifyKind(dateStart + startTime, DateTimeKind.Utc);

                if (!milestoneId.HasValue || milestoneId.Value <= 0)
                {
                    var slotMilestone = await EnsureMilestoneForScheduledSlotAsync(project.Id, slotType, scheduledAt, venue);
                    milestoneId = slotMilestone?.Id;
                }

                var vivaSlot = new VivaSlot
                {
                    ScheduledAt = scheduledAt,
                    Venue = cleanVenue,
                    ProjectId = project.Id,
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
                if (panelUsers.Count > 0)
                {
                    var slot = await _context.VivaSlots
                        .Include(v => v.PanelMembers)
                        .FirstAsync(v => v.Id == vivaSlot.Id);

                    foreach (var u in panelUsers)
                    {
                        slot.PanelMembers.Add(u);
                    }
                    await _context.SaveChangesAsync();
                }

                if (group.Members.Count > 0)
                {
                    var slotName = slotType switch
                    {
                        SlotType.Viva => "Viva",
                        SlotType.Presentation => "Presentation",
                        SlotType.Evaluation => "Evaluation",
                        SlotType.DocumentSubmission => "Document Submission",
                        _ => "Slot"
                    };

                    await _notificationService.CreateNotificationsForUsers(
                        group.Members.Select(m => m.Id),
                        $"{slotName} Scheduled",
                        $"{slotName} scheduled on {scheduledAt:yyyy-MM-dd} at {startTime:hh\\:mm} in {venue}.",
                        NotificationType.Deadline,
                        "slot_scheduled",
                        vivaSlot.Id.ToString(),
                        "/student/milestones");
                }

                return new ScheduleSlotOutcome(ScheduleResult.Ok, vivaSlot, "Slot scheduled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create scheduled slot error: {ex.Message}");
                return Fail(ScheduleResult.UnknownError, $"Unexpected error: {ex.Message}");
            }
        }

        private static ScheduleSlotOutcome Fail(ScheduleResult result, string message)
            => new(result, null, message);

        /// <summary>
        /// Returns slot-sized windows where the GROUP'S SUPERVISOR is completely free
        /// for the given date, derived directly from FacultyTimetables and existing
        /// VivaSlots. For each such window, also lists panel/supervisor candidates
        /// that are simultaneously free.
        /// </summary>
        public async Task<List<SupervisorSlotAvailability>> GetSupervisorBasedAvailabilityForGroupDay(
            int groupId,
            DateTime date,
            int slotMinutes = 30)
        {
            if (groupId <= 0 || slotMinutes <= 0) return new List<SupervisorSlotAvailability>();

            var group = await _context.Groups
                .Include(g => g.Supervisor)
                .FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null || group.SupervisorId <= 0 || group.Supervisor == null || !group.Supervisor.IsActive)
                return new List<SupervisorSlotAvailability>();

            var dateStart = ToUtcDate(date);
            var dateEnd = dateStart.AddDays(1);
            var day = dateStart.DayOfWeek;

            // Authoritative source: supervisor's class timetable for this weekday.
            var supervisorTimetable = await _context.FacultyTimetables
                .Where(ft => ft.Day == day && ft.FacultyId == group.SupervisorId)
                .ToListAsync();

            // Full timetable for the day (used later when computing panel availability).
            var allDayTimetable = await _context.FacultyTimetables
                .Where(ft => ft.Day == day)
                .ToListAsync();

            // Already-scheduled vivas on the date — count as busy for everyone attending.
            var daySlots = await _context.VivaSlots
                .Include(v => v.PanelMembers)
                .Include(v => v.Group)
                .Where(v => v.ScheduledAt >= dateStart &&
                            v.ScheduledAt < dateEnd &&
                            v.Status == VivaStatus.Scheduled)
                .ToListAsync();

            // Build the supervisor's BUSY intervals (timetable + their viva commitments).
            var supervisorBusy = new List<(TimeSpan Start, TimeSpan End)>();
            foreach (var ft in supervisorTimetable)
            {
                if (ft.EndTime > ft.StartTime)
                    supervisorBusy.Add((ft.StartTime, ft.EndTime));
            }
            foreach (var v in daySlots)
            {
                if (v.EndTime <= v.StartTime) continue;
                var attending = v.Group?.SupervisorId == group.SupervisorId
                                || v.PanelMembers.Any(p => p.Id == group.SupervisorId);
                if (attending)
                    supervisorBusy.Add((v.StartTime, v.EndTime));
            }

            // Subtract busy from the working window to get true FREE intervals.
            var freeWindows = ComputeFreeIntervals(DefaultDayStart, DefaultDayEnd, supervisorBusy);

            // Candidate panel users (everyone except the supervisor; restricted to faculty roles).
            var candidateFaculty = await _context.Users
                .Where(u => u.IsActive && SchedulingFacultyRoles.Contains(u.Role) && u.Id != group.SupervisorId)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            // Emit fixed-duration slots strictly INSIDE each free window (no partial overlap).
            var duration = TimeSpan.FromMinutes(slotMinutes);
            var result = new List<SupervisorSlotAvailability>();

            foreach (var win in freeWindows)
            {
                var cursor = win.Start;
                while (cursor + duration <= win.End)
                {
                    var start = cursor;
                    var end = cursor + duration;

                    // Defensive cross-check (cheap): supervisor must be free per source-of-truth.
                    if (HasTimetableConflict(group.SupervisorId, start, end, allDayTimetable) ||
                        HasOverlappingVivaCommitment(group.SupervisorId, start, end, daySlots))
                    {
                        cursor = end;
                        continue;
                    }

                    var availablePanel = candidateFaculty
                        .Where(f => !HasTimetableConflict(f.Id, start, end, allDayTimetable)
                                 && !HasOverlappingVivaCommitment(f.Id, start, end, daySlots))
                        .ToList();

                    result.Add(new SupervisorSlotAvailability
                    {
                        StartTime = start,
                        EndTime = end,
                        AvailablePanelMembers = availablePanel
                    });

                    cursor = end;
                }
            }

            return result;
        }

        private async Task<Project?> EnsureProjectForGroupAsync(Group group)
        {
            if (group.Project != null)
            {
                return group.Project;
            }

            var project = await _context.Projects.FirstOrDefaultAsync(p => p.GroupId == group.Id);
            if (project != null)
            {
                return project;
            }

            var approvedProposal = await _context.Proposals
                .Where(p => p.GroupId == group.Id && p.Status == ProposalStatus.CoordinatorApproved)
                .OrderByDescending(p => p.CoordinatorApprovedAt ?? p.UpdatedAt)
                .FirstOrDefaultAsync();

            project = new Project
            {
                GroupId = group.Id,
                Title = string.IsNullOrWhiteSpace(approvedProposal?.Title)
                    ? $"{group.GroupName} Project"
                    : approvedProposal!.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(approvedProposal?.Abstract)
                    ? "Project created automatically during scheduling."
                    : approvedProposal!.Abstract.Trim(),
                GitHubUrl = approvedProposal?.GitHubUrl?.Trim(),
                Status = approvedProposal != null ? ProjectStatus.Active : ProjectStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();
            return project;
        }

        private async Task<Milestone?> EnsureMilestoneForScheduledSlotAsync(int projectId, SlotType slotType, DateTime scheduledAt, string venue)
        {
            var title = slotType switch
            {
                SlotType.Viva => $"Scheduled Viva - {scheduledAt:dd MMM yyyy}",
                SlotType.Presentation => $"Scheduled Presentation - {scheduledAt:dd MMM yyyy}",
                SlotType.Evaluation => $"Scheduled Evaluation - {scheduledAt:dd MMM yyyy}",
                SlotType.DocumentSubmission => $"Scheduled Document Submission - {scheduledAt:dd MMM yyyy}",
                _ => $"Scheduled Slot - {scheduledAt:dd MMM yyyy}"
            };

            var existing = await _context.Milestones
                .Where(m => m.ProjectId == projectId && m.Title == title && m.DueDate == scheduledAt)
                .FirstOrDefaultAsync();
            if (existing != null)
            {
                return existing;
            }

            var milestone = new Milestone
            {
                Title = title,
                Description = $"{slotType} scheduled at {venue}.",
                DueDate = scheduledAt,
                ProjectId = projectId,
                Status = scheduledAt <= DateTime.UtcNow ? MilestoneStatus.InProgress : MilestoneStatus.Pending,
                ProgressPercent = 0
            };

            _context.Milestones.Add(milestone);
            await _context.SaveChangesAsync();
            return milestone;
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
                    var dateStart = ToUtcDate(date.Value);
                    var dateEnd = dateStart.AddDays(1);

                    var existingSlots = await _context.VivaSlots
                        .Include(v => v.PanelMembers)
                        .Include(v => v.Group)
                        .Where(v => v.ScheduledAt >= dateStart &&
                                    v.ScheduledAt < dateEnd &&
                                    v.Status == VivaStatus.Scheduled)
                        .ToListAsync();

                    busyInViva = existingSlots
                        .Where(v => v.StartTime < endTime && v.EndTime > startTime)
                        .SelectMany(v =>
                        {
                            var ids = v.PanelMembers.Select(p => p.Id).ToList();
                            if (v.Group?.SupervisorId > 0)
                            {
                                ids.Add(v.Group.SupervisorId);
                            }

                            return ids;
                        })
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
                    .Include(v => v.PanelMembers)
                    .Include(v => v.Group)
                        .ThenInclude(g => g!.Project)
                    .Include(v => v.Group)
                        .ThenInclude(g => g!.Members)
                    .Include(v => v.Milestone)
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

        private static bool Overlaps(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd)
            => aStart < bEnd && aEnd > bStart;

        private static DateTime ToUtcDate(DateTime value)
        {
            var d = value.Date;
            return d.Kind == DateTimeKind.Utc
                ? d
                : DateTime.SpecifyKind(d, DateTimeKind.Utc);
        }

        private static bool HasTimetableConflict(
            int facultyId,
            TimeSpan startTime,
            TimeSpan endTime,
            IEnumerable<FacultyTimetable> timetableRows)
            => timetableRows.Any(ft =>
                ft.FacultyId == facultyId &&
                Overlaps(ft.StartTime, ft.EndTime, startTime, endTime));

        private static bool HasOverlappingVivaCommitment(
            int facultyId,
            TimeSpan startTime,
            TimeSpan endTime,
            IEnumerable<VivaSlot> daySlots)
            => daySlots.Any(v =>
                Overlaps(v.StartTime, v.EndTime, startTime, endTime) &&
                (v.PanelMembers.Any(p => p.Id == facultyId) || v.Group?.SupervisorId == facultyId));

        /// <summary>
        /// Subtracts a set of busy intervals from [windowStart, windowEnd] and returns
        /// the resulting free intervals in chronological order. Handles overlap/merge.
        /// </summary>
        private static List<(TimeSpan Start, TimeSpan End)> ComputeFreeIntervals(
            TimeSpan windowStart,
            TimeSpan windowEnd,
            IEnumerable<(TimeSpan Start, TimeSpan End)> busy)
        {
            // Clamp busy intervals into the window, drop empties/inverted.
            var clamped = busy
                .Where(b => b.End > windowStart && b.Start < windowEnd && b.End > b.Start)
                .Select(b => (
                    Start: b.Start < windowStart ? windowStart : b.Start,
                    End:   b.End   > windowEnd   ? windowEnd   : b.End))
                .OrderBy(b => b.Start)
                .ThenBy(b => b.End)
                .ToList();

            // Merge overlaps/adjacent.
            var merged = new List<(TimeSpan Start, TimeSpan End)>();
            foreach (var iv in clamped)
            {
                if (merged.Count == 0 || iv.Start > merged[^1].End)
                {
                    merged.Add(iv);
                }
                else
                {
                    var last = merged[^1];
                    merged[^1] = (last.Start, iv.End > last.End ? iv.End : last.End);
                }
            }

            // Walk the window, emitting the gaps.
            var free = new List<(TimeSpan Start, TimeSpan End)>();
            var cursor = windowStart;
            foreach (var iv in merged)
            {
                if (iv.Start > cursor)
                    free.Add((cursor, iv.Start));
                if (iv.End > cursor)
                    cursor = iv.End;
            }
            if (cursor < windowEnd)
                free.Add((cursor, windowEnd));

            return free;
        }
    }

    public enum ScheduleResult
    {
        Ok,
        GroupNotFound,
        SupervisorMissing,
        DateInPast,
        InvalidTimeRange,
        VenueRequired,
        SlotNotFreeForSupervisor,
        SupervisorHasOverlappingViva,
        VenueDoubleBooked,
        GroupDoubleBooked,
        NoPanelSelected,
        PanelInvalid,
        PanelMemberBusy,
        ProjectCreateFailed,
        UnknownError
    }

    public sealed record ScheduleSlotOutcome(ScheduleResult Result, VivaSlot? Slot, string Message);

    public class SupervisorSlotAvailability
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public List<User> AvailablePanelMembers { get; set; } = new();
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
