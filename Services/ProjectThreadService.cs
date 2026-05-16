using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class ProjectThreadService
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;

        public ProjectThreadService(AppDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<ProjectThread?> EnsureThreadForApprovedProposalAsync(Proposal proposal, int deadlineDays = 120)
        {
            var existing = await _context.ProjectThreads.FirstOrDefaultAsync(t => t.GroupId == proposal.GroupId);
            if (existing != null)
                return existing;

            var project = await _context.Projects.FirstOrDefaultAsync(p => p.GroupId == proposal.GroupId);
            if (project == null)
                return null;

            var now = DateTime.UtcNow;
            var thread = new ProjectThread
            {
                GroupId = proposal.GroupId,
                ProjectId = project.Id,
                CreatedAt = now,
                OverallDeadline = now.AddDays(deadlineDays),
                Status = "Active"
            };

            _context.ProjectThreads.Add(thread);
            await _context.SaveChangesAsync();

            var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == proposal.GroupId);
            if (group != null)
            {
                var recipientIds = group.Members.Select(m => m.Id).ToList();
                if (group.SupervisorId > 0)
                {
                    recipientIds.Add(group.SupervisorId);
                }

                await _notificationService.CreateNotificationsForUsers(
                    recipientIds,
                    "Project Thread Created",
                    $"Project thread is now active for {proposal.Title}.",
                    NotificationType.Info,
                    "project_thread_created",
                    thread.Id.ToString(),
                    $"/project/thread/{group.Id}");
            }

            return thread;
        }

        public async Task<ProjectThread?> EnsureThreadForGroupAsync(int groupId, int deadlineDays = 120)
        {
            var existing = await _context.ProjectThreads.FirstOrDefaultAsync(t => t.GroupId == groupId);
            if (existing != null)
            {
                return existing;
            }

            var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null)
            {
                return null;
            }

            var project = await _context.Projects.FirstOrDefaultAsync(p => p.GroupId == groupId);
            if (project == null)
            {
                return null;
            }

            var thread = new ProjectThread
            {
                GroupId = groupId,
                ProjectId = project.Id,
                CreatedAt = DateTime.UtcNow,
                OverallDeadline = DateTime.UtcNow.AddDays(deadlineDays),
                Status = "Active"
            };

            _context.ProjectThreads.Add(thread);
            await _context.SaveChangesAsync();

            var recipients = group.Members.Select(m => m.Id).ToList();
            if (group.SupervisorId > 0)
            {
                recipients.Add(group.SupervisorId);
            }

            await _notificationService.CreateNotificationsForUsers(
                recipients,
                "Project Thread Created",
                $"Project thread is now active for {group.GroupName}.",
                NotificationType.Info,
                "project_thread_created",
                thread.Id.ToString(),
                $"/project/thread/{groupId}");

            return thread;
        }

        public async Task<ProjectThread?> GetThreadByGroupIdAsync(int groupId)
        {
            return await _context.ProjectThreads.FirstOrDefaultAsync(t => t.GroupId == groupId);
        }

        public async Task<List<ProjectTask>> GetTasksByGroupIdAsync(int groupId)
        {
            return await _context.ProjectTasks
                .Where(t => t.GroupId == groupId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<ProjectTask?> AssignTaskAsync(
            int threadId,
            int groupId,
            int supervisorId,
            string title,
            string description,
            DateTime deadline,
            string priority,
            IEnumerable<int>? assigneeIds,
            bool isProgressDemand,
            string? resourcePath,
            string? resourceName)
        {
            if (string.IsNullOrWhiteSpace(title))
                return null;

            var now = DateTime.UtcNow;
            var task = new ProjectTask
            {
                ThreadId = threadId,
                GroupId = groupId,
                CreatedBySupervisorId = supervisorId,
                Title = title.Trim(),
                Description = description?.Trim() ?? string.Empty,
                Deadline = deadline,
                Priority = string.IsNullOrWhiteSpace(priority) ? "Medium" : priority,
                Status = "Pending",
                CreatedAt = now,
                AssignToMemberIdsCsv = assigneeIds != null ? string.Join(",", assigneeIds.Distinct()) : null,
                IsProgressUpdateDemand = isProgressDemand,
                ResourcePath = resourcePath,
                ResourceName = resourceName
            };

            _context.ProjectTasks.Add(task);
            await _context.SaveChangesAsync();

            var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId);
            if (group != null)
            {
                var assignedTargets = assigneeIds?.Any() == true
                    ? group.Members.Where(m => assigneeIds.Contains(m.Id)).Select(m => m.Id).ToList()
                    : group.Members.Select(m => m.Id).ToList();

                await _notificationService.CreateNotificationsForUsers(
                    assignedTargets,
                    "New Task Assigned",
                    $"{title} has been assigned. Due {deadline:yyyy-MM-dd HH:mm}.",
                    NotificationType.Deadline,
                    "task_assigned",
                    task.Id.ToString(),
                    $"/project/thread/{groupId}");
            }

            return task;
        }

        public async Task<TaskSubmission?> SubmitTaskAsync(int taskId, int studentId, string text, string? filePath, string? fileName)
        {
            var task = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                return null;

            var submission = new TaskSubmission
            {
                TaskId = taskId,
                SubmittedByStudentId = studentId,
                SubmissionText = text?.Trim() ?? string.Empty,
                FilePath = filePath,
                FileName = fileName,
                SubmittedAt = DateTime.UtcNow,
                ReviewStatus = "Submitted",
                Feedback = null
            };

            _context.TaskSubmissions.Add(submission);
            task.Status = "Submitted";
            _context.ProjectTasks.Update(task);
            await _context.SaveChangesAsync();

            await _notificationService.CreateNotification(
                "Task Submitted",
                $"Task '{task.Title}' has a new submission.",
                NotificationType.Info,
                task.CreatedBySupervisorId,
                "task_submitted",
                task.Id.ToString(),
                $"/project/thread/{task.GroupId}");

            return submission;
        }

        public async Task<bool> ReviewSubmissionAsync(int taskId, bool approve, string feedback)
        {
            var task = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId);
            if (task == null)
                return false;

            var latestSubmission = await _context.TaskSubmissions
                .Where(s => s.TaskId == taskId)
                .OrderByDescending(s => s.SubmittedAt)
                .FirstOrDefaultAsync();

            if (latestSubmission == null)
                return false;

            latestSubmission.ReviewStatus = approve ? "Approved" : "Revision Required";
            latestSubmission.Feedback = feedback;
            task.Status = approve ? "Approved" : "Revision Required";

            _context.TaskSubmissions.Update(latestSubmission);
            _context.ProjectTasks.Update(task);
            await _context.SaveChangesAsync();

            await _notificationService.CreateNotification(
                approve ? "Task Approved" : "Task Revision Required",
                approve ? $"Task '{task.Title}' is approved." : $"Task '{task.Title}' needs revision. {feedback}",
                approve ? NotificationType.Success : NotificationType.Warning,
                latestSubmission.SubmittedByStudentId,
                "task_reviewed",
                task.Id.ToString(),
                $"/project/thread/{task.GroupId}");

            return true;
        }
    }
}
