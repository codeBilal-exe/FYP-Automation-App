using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class EvaluationService
    {
        private readonly AppDbContext _context;

        public EvaluationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<EvaluationItemDto>> GetSupervisorEvaluationItemsAsync(int supervisorId)
        {
            try
            {
                var groups = await _context.Groups
                    .AsNoTracking()
                    .Include(g => g.Project)
                    .Where(g => g.SupervisorId == supervisorId)
                    .OrderBy(g => g.Id)
                    .ToListAsync();

                if (groups.Count == 0)
                {
                    return new List<EvaluationItemDto>();
                }

                var groupById = groups.ToDictionary(g => g.Id);
                var projectToGroup = groups
                    .Where(g => g.Project != null)
                    .ToDictionary(g => g.Project!.Id, g => g);
                var projectIds = projectToGroup.Keys.ToList();
                var groupIds = groups.Select(g => g.Id).ToList();

                var milestones = await _context.Milestones
                    .AsNoTracking()
                    .Where(m => projectIds.Contains(m.ProjectId))
                    .ToListAsync();

                var vivas = await _context.VivaSlots
                    .AsNoTracking()
                    .Include(v => v.Milestone)
                    .Where(v =>
                        (v.GroupId.HasValue && groupIds.Contains(v.GroupId.Value)) ||
                        projectIds.Contains(v.ProjectId))
                    .ToListAsync();

                var milestoneIds = milestones.Select(m => m.Id).ToList();
                var vivaIds = vivas.Select(v => v.Id).ToList();

                var existingEvaluations = await _context.Evaluations
                    .AsNoTracking()
                    .Where(e => e.EvaluatorId == supervisorId &&
                                ((e.ItemType == "Milestone" && milestoneIds.Contains(e.ItemId)) ||
                                 (e.ItemType == "Viva" && vivaIds.Contains(e.ItemId))))
                    .ToListAsync();

                var existingByKey = existingEvaluations.ToDictionary(
                    e => $"{e.ItemType}:{e.ItemId}",
                    e => e);

                var items = new List<EvaluationItemDto>();

                foreach (var milestone in milestones)
                {
                    if (!projectToGroup.TryGetValue(milestone.ProjectId, out var group))
                    {
                        continue;
                    }

                    existingByKey.TryGetValue($"Milestone:{milestone.Id}", out var existing);
                    items.Add(new EvaluationItemDto
                    {
                        Id = milestone.Id,
                        Type = "Milestone",
                        GroupId = group.Id,
                        GroupName = group.GroupName,
                        Title = milestone.Title,
                        ScheduledDate = milestone.DueDate,
                        Status = milestone.Status.ToString(),
                        CanEvaluate = true,
                        ExistingMarks = existing?.Marks,
                        ExistingComment = existing?.Comment,
                        SubmissionFilePath = milestone.SubmissionFilePath,
                        SubmissionFileName = milestone.SubmissionFileName,
                        SubmissionNotes = milestone.SubmissionNotes,
                        SubmittedAt = milestone.SubmittedAt
                    });
                }

                foreach (var viva in vivas)
                {
                    var resolvedGroup = ResolveGroupForViva(viva, groupById, projectToGroup);
                    if (resolvedGroup == null)
                    {
                        continue;
                    }

                    existingByKey.TryGetValue($"Viva:{viva.Id}", out var existing);
                    items.Add(new EvaluationItemDto
                    {
                        Id = viva.Id,
                        Type = "Viva",
                        GroupId = resolvedGroup.Id,
                        GroupName = resolvedGroup.GroupName,
                        Title = BuildVivaTitle(viva),
                        ScheduledDate = viva.ScheduledAt,
                        Status = viva.Status.ToString(),
                        CanEvaluate = true,
                        ExistingMarks = existing?.Marks,
                        ExistingComment = existing?.Comment
                    });
                }

                return items
                    .OrderBy(i => i.GroupId)
                    .ThenBy(i => i.ScheduledDate ?? DateTime.MaxValue)
                    .ThenBy(i => i.Type)
                    .ThenBy(i => i.Title)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetSupervisorEvaluationItemsAsync error: {ex.Message}");
                return new List<EvaluationItemDto>();
            }
        }

        public async Task<List<EvaluationItemDto>> GetPanelEvaluationItemsAsync(int panelMemberId)
        {
            try
            {
                var vivas = await _context.VivaSlots
                    .AsNoTracking()
                    .Include(v => v.Group)
                    .Include(v => v.Milestone)
                    .Include(v => v.PanelMembers)
                    .Where(v => v.Status == VivaStatus.Completed &&
                                v.PanelMembers.Any(p => p.Id == panelMemberId))
                    .OrderBy(v => v.ScheduledAt)
                    .ToListAsync();

                if (vivas.Count == 0)
                {
                    return new List<EvaluationItemDto>();
                }

                var projectIds = vivas.Select(v => v.ProjectId).Distinct().ToList();
                var projectToGroup = await _context.Projects
                    .AsNoTracking()
                    .Include(p => p.Group)
                    .Where(p => projectIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.Group);

                var vivaIds = vivas.Select(v => v.Id).ToList();
                var existingEvaluations = await _context.Evaluations
                    .AsNoTracking()
                    .Where(e => e.EvaluatorId == panelMemberId &&
                                e.ItemType == "Viva" &&
                                vivaIds.Contains(e.ItemId))
                    .ToListAsync();

                var existingById = existingEvaluations.ToDictionary(e => e.ItemId, e => e);
                var items = new List<EvaluationItemDto>();

                foreach (var viva in vivas)
                {
                    var group = viva.Group;
                    if (group == null &&
                        projectToGroup.TryGetValue(viva.ProjectId, out var mappedGroup))
                    {
                        group = mappedGroup;
                    }

                    if (group == null)
                    {
                        continue;
                    }

                    existingById.TryGetValue(viva.Id, out var existing);
                    items.Add(new EvaluationItemDto
                    {
                        Id = viva.Id,
                        Type = "Viva",
                        GroupId = group.Id,
                        GroupName = group.GroupName,
                        Title = BuildVivaTitle(viva),
                        ScheduledDate = viva.ScheduledAt,
                        Status = viva.Status.ToString(),
                        CanEvaluate = true,
                        ExistingMarks = existing?.Marks,
                        ExistingComment = existing?.Comment
                    });
                }

                return items
                    .OrderBy(i => i.ScheduledDate ?? DateTime.MaxValue)
                    .ThenBy(i => i.GroupName)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetPanelEvaluationItemsAsync error: {ex.Message}");
                return new List<EvaluationItemDto>();
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> SaveEvaluationAsync(
            int evaluatorId,
            string evaluatorRole,
            int itemId,
            string itemType,
            decimal marks,
            string? comment,
            int groupId)
        {
            try
            {
                if (marks < 0 || marks > 100)
                {
                    return (false, "Marks must be between 0 and 100.");
                }

                var normalizedRole = NormalizeEvaluatorRole(evaluatorRole);
                if (normalizedRole == null)
                {
                    return (false, "Only supervisors and panel members can evaluate.");
                }

                var normalizedType = NormalizeItemType(itemType);
                if (normalizedType == null)
                {
                    return (false, "Invalid evaluation item type.");
                }

                int resolvedGroupId;
                if (normalizedType == "Milestone")
                {
                    if (normalizedRole != "Supervisor")
                    {
                        return (false, "Only supervisors can evaluate milestones.");
                    }

                    var milestone = await _context.Milestones
                        .AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Id == itemId);

                    if (milestone == null)
                    {
                        return (false, "Milestone not found.");
                    }

                    var milestoneGroup = await _context.Projects
                        .AsNoTracking()
                        .Where(p => p.Id == milestone.ProjectId)
                        .Join(_context.Groups.AsNoTracking(),
                            p => p.GroupId,
                            g => g.Id,
                            (p, g) => g)
                        .FirstOrDefaultAsync();

                    if (milestoneGroup == null)
                    {
                        return (false, "Milestone group not found.");
                    }

                    if (milestoneGroup.SupervisorId != evaluatorId)
                    {
                        return (false, "You can only evaluate milestones for your own groups.");
                    }

                    resolvedGroupId = milestoneGroup.Id;
                }
                else
                {
                    var viva = await _context.VivaSlots
                        .Include(v => v.PanelMembers)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(v => v.Id == itemId);

                    if (viva == null)
                    {
                        return (false, "Viva not found.");
                    }

                    var vivaGroup = await ResolveVivaGroupAsync(viva);
                    if (vivaGroup == null)
                    {
                        return (false, "Viva group not found.");
                    }

                    if (normalizedRole == "Supervisor")
                    {
                        if (vivaGroup.SupervisorId != evaluatorId)
                        {
                            return (false, "You can only evaluate vivas for your own groups.");
                        }
                    }
                    else if (normalizedRole == "Panel Member")
                    {
                        if (viva.Status != VivaStatus.Completed)
                        {
                            return (false, "Panel members can only evaluate completed vivas.");
                        }

                        var isAssigned = viva.PanelMembers.Any(p => p.Id == evaluatorId);
                        if (!isAssigned)
                        {
                            return (false, "You are not assigned as a panel member for this viva.");
                        }
                    }

                    resolvedGroupId = vivaGroup.Id;
                }

                if (groupId > 0 && groupId != resolvedGroupId)
                {
                    return (false, "Invalid group for this evaluation item.");
                }

                var trimmedComment = string.IsNullOrWhiteSpace(comment)
                    ? null
                    : comment.Trim();

                if (trimmedComment?.Length > 500)
                {
                    return (false, "Comment must be 500 characters or fewer.");
                }

                var existing = await _context.Evaluations
                    .FirstOrDefaultAsync(e =>
                        e.ItemId == itemId &&
                        e.ItemType == normalizedType &&
                        e.EvaluatorId == evaluatorId);

                if (existing != null)
                {
                    existing.Marks = marks;
                    existing.Comment = trimmedComment;
                    existing.EvaluatorRole = normalizedRole;
                    existing.GroupId = resolvedGroupId;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.Evaluations.Add(new Evaluation
                    {
                        ItemId = itemId,
                        ItemType = normalizedType,
                        EvaluatorId = evaluatorId,
                        EvaluatorRole = normalizedRole,
                        GroupId = resolvedGroupId,
                        Marks = marks,
                        Comment = trimmedComment,
                        EvaluatedAt = DateTime.UtcNow,
                        UpdatedAt = null
                    });
                }

                await _context.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveEvaluationAsync error: {ex.Message}");
                return (false, "Unable to save evaluation at the moment.");
            }
        }

        public async Task<StudentResultDto?> GetStudentResultAsync(int studentId)
        {
            try
            {
                var group = await _context.Groups
                    .AsNoTracking()
                    .Include(g => g.Members)
                    .Include(g => g.Project)
                    .FirstOrDefaultAsync(g => g.Members.Any(m => m.Id == studentId));

                if (group == null)
                {
                    return null;
                }

                var milestones = new List<Milestone>();
                if (group.Project != null)
                {
                    milestones = await _context.Milestones
                        .AsNoTracking()
                        .Where(m => m.ProjectId == group.Project.Id)
                        .ToListAsync();
                }

                var vivas = await _context.VivaSlots
                    .AsNoTracking()
                    .Include(v => v.Milestone)
                    .Where(v =>
                        (v.GroupId.HasValue && v.GroupId.Value == group.Id) ||
                        (group.Project != null && v.ProjectId == group.Project.Id))
                    .ToListAsync();

                var evaluations = await _context.Evaluations
                    .AsNoTracking()
                    .Where(e => e.GroupId == group.Id &&
                                (e.ItemType == "Milestone" || e.ItemType == "Viva"))
                    .OrderBy(e => e.EvaluatedAt)
                    .ToListAsync();

                var taskResults = new List<TaskResultDto>();

                foreach (var milestone in milestones)
                {
                    var marks = evaluations
                        .Where(e => e.ItemType == "Milestone" && e.ItemId == milestone.Id)
                        .Select(e => new EvaluatorMarkDto
                        {
                            EvaluatorRole = e.EvaluatorRole,
                            Marks = e.Marks,
                            Comment = e.Comment,
                            EvaluatedAt = e.EvaluatedAt
                        })
                        .ToList();

                    taskResults.Add(new TaskResultDto
                    {
                        Type = "Milestone",
                        Title = milestone.Title,
                        Date = milestone.DueDate,
                        Marks = marks
                    });
                }

                foreach (var viva in vivas)
                {
                    var marks = evaluations
                        .Where(e => e.ItemType == "Viva" && e.ItemId == viva.Id)
                        .Select(e => new EvaluatorMarkDto
                        {
                            EvaluatorRole = e.EvaluatorRole,
                            Marks = e.Marks,
                            Comment = e.Comment,
                            EvaluatedAt = e.EvaluatedAt
                        })
                        .ToList();

                    taskResults.Add(new TaskResultDto
                    {
                        Type = "Viva",
                        Title = BuildVivaTitle(viva),
                        Date = viva.ScheduledAt,
                        Marks = marks
                    });
                }

                taskResults = taskResults
                    .OrderBy(t => t.Date ?? DateTime.MaxValue)
                    .ThenBy(t => t.Type)
                    .ThenBy(t => t.Title)
                    .ToList();

                var totalMarks = taskResults.Sum(t => t.AverageMark);
                var maxMarks = taskResults.Count * 100m;

                return new StudentResultDto
                {
                    GroupName = group.GroupName,
                    Tasks = taskResults,
                    TotalMarks = Math.Round(totalMarks, 1),
                    MaxMarks = maxMarks
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetStudentResultAsync error: {ex.Message}");
                return null;
            }
        }

        // Legacy rubric-based APIs retained for compatibility with existing pages.
        public async Task<Evaluation?> CreateEvaluation(int projectId, int evaluatorId)
        {
            try
            {
                var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
                var evaluator = await _context.Users.FirstOrDefaultAsync(u => u.Id == evaluatorId);

                if (project == null || evaluator == null)
                    return null;

                var evaluation = new Evaluation
                {
                    ProjectId = projectId,
                    ItemId = projectId,
                    ItemType = "LegacyProject",
                    EvaluatorId = evaluatorId,
                    EvaluatorRole = evaluator.Role == UserRole.Panel ? "Panel Member" : evaluator.Role.ToString(),
                    GroupId = project.GroupId,
                    Marks = 0,
                    Comment = null,
                    TotalScore = 0,
                    Feedback = string.Empty,
                    IsLocked = false,
                    EvaluatedAt = DateTime.UtcNow
                };

                _context.Evaluations.Add(evaluation);
                await _context.SaveChangesAsync();
                return evaluation;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create evaluation error: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> AddRubricScore(int evaluationId, int rubricItemId, decimal obtainedMarks)
        {
            try
            {
                var evaluation = await _context.Evaluations
                    .FirstOrDefaultAsync(e => e.Id == evaluationId);

                if (evaluation == null || evaluation.IsLocked)
                    return false;

                var rubricItem = await _context.RubricItems
                    .FirstOrDefaultAsync(ri => ri.Id == rubricItemId);

                if (rubricItem == null || obtainedMarks > rubricItem.MaxMarks || obtainedMarks < 0)
                    return false;

                var existingScore = await _context.RubricScores
                    .FirstOrDefaultAsync(rs => rs.EvaluationId == evaluationId && rs.RubricItemId == rubricItemId);

                if (existingScore != null)
                {
                    existingScore.ObtainedMarks = obtainedMarks;
                    _context.RubricScores.Update(existingScore);
                }
                else
                {
                    _context.RubricScores.Add(new RubricScore
                    {
                        EvaluationId = evaluationId,
                        RubricItemId = rubricItemId,
                        ObtainedMarks = obtainedMarks
                    });
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Add rubric score error: {ex.Message}");
                return false;
            }
        }

        public async Task<decimal> CalculateTotalScore(int evaluationId)
        {
            try
            {
                var evaluation = await _context.Evaluations
                    .Include(e => e.RubricScores)
                    .FirstOrDefaultAsync(e => e.Id == evaluationId);

                if (evaluation == null)
                    return 0;

                var totalScore = evaluation.RubricScores.Sum(rs => rs.ObtainedMarks);
                evaluation.TotalScore = totalScore;
                await _context.SaveChangesAsync();

                return totalScore;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Calculate total score error: {ex.Message}");
                return 0;
            }
        }

        public async Task<bool> LockEvaluation(int evaluationId)
        {
            try
            {
                var evaluation = await _context.Evaluations.FirstOrDefaultAsync(e => e.Id == evaluationId);
                if (evaluation == null)
                    return false;

                evaluation.IsLocked = true;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lock evaluation error: {ex.Message}");
                return false;
            }
        }

        public async Task<Evaluation?> GetEvaluationByProject(int projectId)
        {
            try
            {
                return await _context.Evaluations
                    .Include(e => e.RubricScores)
                    .FirstOrDefaultAsync(e => e.ProjectId == projectId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get evaluation by project error: {ex.Message}");
                return null;
            }
        }

        public async Task<List<Evaluation>> GetUserEvaluations(int userId)
        {
            try
            {
                return await _context.Evaluations
                    .Where(e => e.EvaluatorId == userId)
                    .Include(e => e.RubricScores)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get user evaluations error: {ex.Message}");
                return new List<Evaluation>();
            }
        }

        public async Task<List<Evaluation>> GetEvaluationsByProject(int projectId)
        {
            try
            {
                return await _context.Evaluations
                    .Where(e => e.ProjectId == projectId)
                    .Include(e => e.RubricScores)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get evaluations by project error: {ex.Message}");
                return new List<Evaluation>();
            }
        }

        public async Task<Evaluation?> GetEvaluationById(int id)
        {
            try
            {
                return await _context.Evaluations
                    .Include(e => e.RubricScores)
                    .FirstOrDefaultAsync(e => e.Id == id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get evaluation by id error: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> UpdateFeedback(int evaluationId, string feedback)
        {
            try
            {
                var evaluation = await _context.Evaluations.FirstOrDefaultAsync(e => e.Id == evaluationId);
                if (evaluation == null || evaluation.IsLocked)
                    return false;

                evaluation.Feedback = feedback;
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update feedback error: {ex.Message}");
                return false;
            }
        }

        private static string BuildVivaTitle(VivaSlot viva)
        {
            if (viva.Milestone != null && !string.IsNullOrWhiteSpace(viva.Milestone.Title))
            {
                return $"{viva.Milestone.Title} Viva";
            }

            return $"Viva #{viva.Id}";
        }

        private static Group? ResolveGroupForViva(
            VivaSlot viva,
            IReadOnlyDictionary<int, Group> groupsById,
            IReadOnlyDictionary<int, Group> projectToGroup)
        {
            if (viva.GroupId.HasValue &&
                groupsById.TryGetValue(viva.GroupId.Value, out var groupByDirect))
            {
                return groupByDirect;
            }

            if (projectToGroup.TryGetValue(viva.ProjectId, out var groupByProject))
            {
                return groupByProject;
            }

            return null;
        }

        private async Task<Group?> ResolveVivaGroupAsync(VivaSlot viva)
        {
            if (viva.GroupId.HasValue)
            {
                var directGroup = await _context.Groups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Id == viva.GroupId.Value);
                if (directGroup != null)
                {
                    return directGroup;
                }
            }

            var projectGroupId = await _context.Projects
                .AsNoTracking()
                .Where(p => p.Id == viva.ProjectId)
                .Select(p => (int?)p.GroupId)
                .FirstOrDefaultAsync();

            if (!projectGroupId.HasValue)
            {
                return null;
            }

            return await _context.Groups
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == projectGroupId.Value);
        }

        private static string? NormalizeItemType(string itemType)
        {
            if (string.Equals(itemType, "Milestone", StringComparison.OrdinalIgnoreCase))
            {
                return "Milestone";
            }

            if (string.Equals(itemType, "Viva", StringComparison.OrdinalIgnoreCase))
            {
                return "Viva";
            }

            return null;
        }

        private static string? NormalizeEvaluatorRole(string evaluatorRole)
        {
            if (string.Equals(evaluatorRole, "Supervisor", StringComparison.OrdinalIgnoreCase))
            {
                return "Supervisor";
            }

            if (string.Equals(evaluatorRole, "Panel Member", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(evaluatorRole, "Panel", StringComparison.OrdinalIgnoreCase))
            {
                return "Panel Member";
            }

            return null;
        }
    }
}
