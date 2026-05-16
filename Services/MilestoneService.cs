using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class MilestoneService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;
        private readonly IWebHostEnvironment _environment;
        private readonly NotificationService _notificationService;

        private static readonly string[] AllowedSubmissionExtensions = [".pdf", ".doc", ".docx", ".zip"];
        private const long MaxSubmissionBytes = 15 * 1024 * 1024;

        public MilestoneService(
            AppDbContext context,
            AuditService auditService,
            IWebHostEnvironment environment,
            NotificationService notificationService)
        {
            _context = context;
            _auditService = auditService;
            _environment = environment;
            _notificationService = notificationService;
        }

        public async Task<List<Milestone>> GetAllMilestonesAsync()
        {
            return await _context.Milestones.Include(m => m.Project).ToListAsync();
        }

        public async Task<Milestone?> GetMilestoneByIdAsync(int id)
        {
            return await _context.Milestones.Include(m => m.Project).FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<List<Milestone>> GetMilestonesByProjectAsync(int projectId)
        {
            return await _context.Milestones
                .Where(m => m.ProjectId == projectId)
                .ToListAsync();
        }

        public async Task<Milestone> CreateMilestoneAsync(Milestone milestone)
        {
            _context.Milestones.Add(milestone);
            await _context.SaveChangesAsync();
            return milestone;
        }

        public async Task<Milestone> UpdateMilestoneAsync(Milestone milestone)
        {
            _context.Milestones.Update(milestone);
            await _context.SaveChangesAsync();
            return milestone;
        }

        public async Task<bool> DeleteMilestoneAsync(int id)
        {
            var milestone = await GetMilestoneByIdAsync(id);
            if (milestone != null)
            {
                _context.Milestones.Remove(milestone);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<List<Milestone>> GetOverdueMilestonesAsync()
        {
            return await _context.Milestones
                .Where(m => m.DueDate < DateTime.UtcNow && m.Status != MilestoneStatus.Completed)
                .ToListAsync();
        }

        public async Task<Project?> EnsureProjectForGroupAsync(int groupId)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.GroupId == groupId);
            if (project != null)
            {
                return project;
            }

            var group = await _context.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null)
            {
                return null;
            }

            project = new Project
            {
                GroupId = groupId,
                Title = $"{group.GroupName} Project",
                Description = "Project auto-created from milestone assignment.",
                Status = ProjectStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.Projects.Add(project);
            await _context.SaveChangesAsync();
            return project;
        }

        public async Task<(bool Success, string Message, Milestone? Milestone)> CreateMilestoneForGroupAsync(
            int supervisorId,
            int groupId,
            string title,
            string description,
            DateTime dueDate)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return (false, "Milestone title is required.", null);
            }

            var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null)
            {
                return (false, "Group not found.", null);
            }

            if (group.SupervisorId != supervisorId)
            {
                return (false, "You can only assign milestones to your own groups.", null);
            }

            var project = await EnsureProjectForGroupAsync(groupId);
            if (project == null)
            {
                return (false, "Unable to locate or create project for this group.", null);
            }

            var milestone = new Milestone
            {
                Title = title.Trim(),
                Description = description?.Trim() ?? string.Empty,
                DueDate = dueDate,
                CreatedBySupervisorId = supervisorId,
                ProjectId = project.Id,
                Status = MilestoneStatus.Pending,
                ProgressPercent = 0
            };

            _context.Milestones.Add(milestone);
            await _context.SaveChangesAsync();

            await _auditService.LogActionAsync(supervisorId, "MILESTONE_ASSIGNED", $"Milestone {milestone.Id} for group {groupId}");
            await _notificationService.CreateNotificationsForUsers(
                group.Members.Select(m => m.Id),
                "New Milestone Assigned",
                $"{milestone.Title} has been assigned. Due {milestone.DueDate:yyyy-MM-dd HH:mm}.",
                NotificationType.Deadline,
                "milestone_assigned",
                milestone.Id.ToString(),
                "/student/milestones");

            return (true, "Milestone assigned to group.", milestone);
        }

        public async Task<(bool Success, string Message)> UpdateMilestoneDeadlineAsync(int supervisorId, int milestoneId, DateTime newDueDate)
        {
            var milestone = await _context.Milestones.Include(m => m.Project).FirstOrDefaultAsync(m => m.Id == milestoneId);
            if (milestone == null || milestone.Project == null)
            {
                return (false, "Milestone not found.");
            }

            var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == milestone.Project.GroupId);
            if (group == null)
            {
                return (false, "Owning group not found.");
            }

            if (group.SupervisorId != supervisorId)
            {
                return (false, "You can only edit milestones for your own groups.");
            }

            if (milestone.CreatedBySupervisorId.HasValue && milestone.CreatedBySupervisorId.Value != supervisorId)
            {
                return (false, "You can only edit milestones that you created.");
            }

            milestone.DueDate = newDueDate;
            _context.Milestones.Update(milestone);
            await _context.SaveChangesAsync();

            await _auditService.LogActionAsync(supervisorId, "MILESTONE_DEADLINE_UPDATED", $"Milestone {milestone.Id} new due date {newDueDate:O}");
            await _notificationService.CreateNotificationsForUsers(
                group.Members.Select(m => m.Id),
                "Milestone Deadline Updated",
                $"Deadline for {milestone.Title} is now {newDueDate:yyyy-MM-dd HH:mm}.",
                NotificationType.Warning,
                "milestone_deadline_updated",
                milestone.Id.ToString(),
                "/student/milestones");

            return (true, "Deadline updated.");
        }

        public async Task<(bool Success, string Message)> SubmitMilestoneByStudentAsync(
            int studentId,
            int milestoneId,
            string notes,
            IBrowserFile file)
        {
            var milestone = await _context.Milestones.Include(m => m.Project).FirstOrDefaultAsync(m => m.Id == milestoneId);
            if (milestone == null || milestone.Project == null)
            {
                return (false, "Milestone not found.");
            }

            var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == milestone.Project.GroupId);
            if (group == null || !group.Members.Any(m => m.Id == studentId))
            {
                return (false, "You are not allowed to submit this milestone.");
            }

            if (DateTime.UtcNow > milestone.DueDate)
            {
                return (false, "Deadline has passed. Submission is closed.");
            }

            if (file == null)
            {
                return (false, "Please attach a submission file.");
            }

            var ext = Path.GetExtension(file.Name).ToLowerInvariant();
            if (!AllowedSubmissionExtensions.Contains(ext))
            {
                return (false, "Invalid file type. Allowed: PDF, DOC, DOCX, ZIP.");
            }

            if (file.Size <= 0 || file.Size > MaxSubmissionBytes)
            {
                return (false, "Submission file size exceeds 15 MB limit.");
            }

            var dir = Path.Combine(_environment.WebRootPath, "uploads", "milestones", milestoneId.ToString());
            Directory.CreateDirectory(dir);
            var serverFile = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(dir, serverFile);
            await using (var source = file.OpenReadStream(MaxSubmissionBytes))
            await using (var target = File.Create(fullPath))
            {
                await source.CopyToAsync(target);
            }

            milestone.SubmissionFilePath = $"/uploads/milestones/{milestoneId}/{serverFile}";
            milestone.SubmissionFileName = file.Name;
            milestone.SubmissionNotes = notes?.Trim();
            milestone.SubmittedAt = DateTime.UtcNow;
            milestone.SubmittedByStudentId = studentId;
            milestone.Status = MilestoneStatus.Completed;
            milestone.ProgressPercent = 100;

            _context.Milestones.Update(milestone);
            await _context.SaveChangesAsync();

            await _auditService.LogActionAsync(studentId, "MILESTONE_SUBMITTED", $"Milestone {milestoneId} submitted by student {studentId}");
            if (group.SupervisorId > 0)
            {
                await _notificationService.CreateNotification(
                    "Milestone Submitted",
                    $"{group.GroupName} submitted milestone '{milestone.Title}'.",
                    NotificationType.Info,
                    group.SupervisorId,
                    "milestone_submitted",
                    milestone.Id.ToString(),
                    "/supervisor/milestones");
            }

            return (true, "Milestone submitted successfully.");
        }
    }
}
