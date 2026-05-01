using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class ReportService
    {
        private readonly AppDbContext _context;

        public ReportService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gets project count by status for pie chart
        /// </summary>
        public async Task<Dictionary<string, int>> GetProjectsByStatus()
        {
            try
            {
                var result = new Dictionary<string, int>();

                var statusCounts = await _context.Projects
                    .GroupBy(p => p.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                foreach (var item in statusCounts)
                {
                    result[item.Status.ToString()] = item.Count;
                }

                // Ensure all statuses are represented
                foreach (ProjectStatus status in Enum.GetValues(typeof(ProjectStatus)))
                {
                    if (!result.ContainsKey(status.ToString()))
                        result[status.ToString()] = 0;
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get projects by status error: {ex.Message}");
                return new Dictionary<string, int>();
            }
        }

        /// <summary>
        /// Gets milestone completion data by month for bar chart
        /// </summary>
        public async Task<Dictionary<string, int>> GetMilestoneCompletionByMonth()
        {
            try
            {
                var result = new Dictionary<string, int>();

                var completedMilestones = await _context.Milestones
                    .Where(m => m.Status == MilestoneStatus.Completed)
                    .ToListAsync();

                var groupedByMonth = completedMilestones
                    .GroupBy(m => m.DueDate.ToString("yyyy-MM"))
                    .ToDictionary(g => g.Key, g => g.Count());

                // Get last 12 months
                var today = DateTime.UtcNow;
                for (int i = 11; i >= 0; i--)
                {
                    var date = today.AddMonths(-i);
                    var monthKey = date.ToString("yyyy-MM");

                    if (groupedByMonth.ContainsKey(monthKey))
                        result[monthKey] = groupedByMonth[monthKey];
                    else
                        result[monthKey] = 0;
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get milestone completion by month error: {ex.Message}");
                return new Dictionary<string, int>();
            }
        }

        /// <summary>
        /// Gets proposal submission trend data for line chart
        /// </summary>
        public async Task<Dictionary<string, int>> GetProposalSubmissionTrend()
        {
            try
            {
                var result = new Dictionary<string, int>();

                var proposals = await _context.Proposals
                    .ToListAsync();

                var groupedByMonth = proposals
                    .GroupBy(p => p.SubmittedAt.ToString("yyyy-MM"))
                    .ToDictionary(g => g.Key, g => g.Count());

                // Get last 12 months
                var today = DateTime.UtcNow;
                for (int i = 11; i >= 0; i--)
                {
                    var date = today.AddMonths(-i);
                    var monthKey = date.ToString("yyyy-MM");

                    if (groupedByMonth.ContainsKey(monthKey))
                        result[monthKey] = groupedByMonth[monthKey];
                    else
                        result[monthKey] = 0;
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get proposal submission trend error: {ex.Message}");
                return new Dictionary<string, int>();
            }
        }

        /// <summary>
        /// Exports data to CSV format
        /// </summary>
        public string ExportToCSV<T>(List<T> data)
        {
            try
            {
                if (data == null || data.Count == 0)
                    return string.Empty;

                var csv = new System.Text.StringBuilder();
                var properties = typeof(T).GetProperties();

                // Write header
                var headers = properties.Select(p => p.Name);
                csv.AppendLine(string.Join(",", headers));

                // Write data
                foreach (var item in data)
                {
                    var values = properties.Select(p =>
                    {
                        var value = p.GetValue(item)?.ToString() ?? string.Empty;
                        // Escape quotes and wrap in quotes if contains comma
                        if (value.Contains(",") || value.Contains("\""))
                        {
                            value = $"\"{value.Replace("\"", "\"\"")}\"";
                        }
                        return value;
                    });
                    csv.AppendLine(string.Join(",", values));
                }

                return csv.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Export to CSV error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets overall project statistics
        /// </summary>
        public async Task<Dictionary<string, object>> GetProjectStatistics()
        {
            try
            {
                var stats = new Dictionary<string, object>();

                var totalProjects = await _context.Projects.CountAsync();
                var activeProjects = await _context.Projects.CountAsync(p => p.Status == ProjectStatus.Active);
                var completedProjects = await _context.Projects.CountAsync(p => p.Status == ProjectStatus.Completed);
                var totalMilestones = await _context.Milestones.CountAsync();
                var completedMilestones = await _context.Milestones.CountAsync(m => m.Status == MilestoneStatus.Completed);

                stats["TotalProjects"] = totalProjects;
                stats["ActiveProjects"] = activeProjects;
                stats["CompletedProjects"] = completedProjects;
                stats["PendingProjects"] = totalProjects - activeProjects - completedProjects;
                stats["TotalMilestones"] = totalMilestones;
                stats["CompletedMilestones"] = completedMilestones;
                stats["CompletionRate"] = totalMilestones > 0 ? Math.Round((decimal)completedMilestones / totalMilestones * 100, 2) : 0;

                return stats;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get project statistics error: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Gets proposal statistics
        /// </summary>
        public async Task<Dictionary<string, object>> GetProposalStatistics()
        {
            try
            {
                var stats = new Dictionary<string, object>();

                var totalProposals = await _context.Proposals.CountAsync();
                var approvedProposals = await _context.Proposals.CountAsync(p => p.Status == ProposalStatus.ApprovedByHOD);
                var rejectedProposals = await _context.Proposals.CountAsync(p => p.Status == ProposalStatus.Rejected);

                stats["TotalProposals"] = totalProposals;
                stats["ApprovedProposals"] = approvedProposals;
                stats["RejectedProposals"] = rejectedProposals;
                stats["PendingProposals"] = totalProposals - approvedProposals - rejectedProposals;
                stats["ApprovalRate"] = totalProposals > 0 ? Math.Round((decimal)approvedProposals / totalProposals * 100, 2) : 0;

                return stats;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get proposal statistics error: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Gets evaluation statistics
        /// </summary>
        public async Task<Dictionary<string, object>> GetEvaluationStatistics()
        {
            try
            {
                var stats = new Dictionary<string, object>();

                var evaluations = await _context.Evaluations.ToListAsync();
                var totalEvaluations = evaluations.Count;
                var lockedEvaluations = evaluations.Count(e => e.IsLocked);
                var averageScore = evaluations.Count > 0 ? evaluations.Average(e => e.TotalScore) : 0;

                stats["TotalEvaluations"] = totalEvaluations;
                stats["LockedEvaluations"] = lockedEvaluations;
                stats["AverageScore"] = Math.Round((decimal)averageScore, 2);
                stats["HighestScore"] = evaluations.Count > 0 ? evaluations.Max(e => e.TotalScore) : 0;
                stats["LowestScore"] = evaluations.Count > 0 ? evaluations.Min(e => e.TotalScore) : 0;

                return stats;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get evaluation statistics error: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }
    }
}
