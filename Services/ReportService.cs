using System.Text;
using ClosedXML.Excel;
using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FYP_AutomationSystem.Services
{
    public class ReportService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ReportService(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<bool> AllFinalGradesConfirmed(string semester, string department)
        {
            var groups = await _context.Groups
                .Where(g => g.Semester == semester && g.Department == department)
                .ToListAsync();

            if (!groups.Any())
                return false;

            return groups.All(g => g.IsFinalGradeConfirmed);
        }

        public async Task<string> GenerateReport(
            string reportType,
            string semester,
            string department,
            DateTime fromDate,
            DateTime toDate,
            string format,
            int requestedByUserId)
        {
            if (!await AllFinalGradesConfirmed(semester, department))
                throw new InvalidOperationException("Cannot generate report: not all groups have confirmed final grades.");

            var records = await BuildReportRows(reportType, semester, department, fromDate, toDate);
            var reportsFolder = Path.Combine(_env.WebRootPath, "reports");
            Directory.CreateDirectory(reportsFolder);

            var extension = format.Equals("PDF", StringComparison.OrdinalIgnoreCase) ? "pdf" : "xlsx";
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var fileName = $"{reportType}_{department}_{semester}_{timestamp}.{extension}";
            var physicalPath = Path.Combine(reportsFolder, fileName);
            var publicPath = $"/reports/{fileName}";

            if (format.Equals("PDF", StringComparison.OrdinalIgnoreCase))
                GeneratePdf(physicalPath, reportType, semester, department, fromDate, toDate, records);
            else
                GenerateXlsx(physicalPath, reportType, records);

            _context.ReportArchives.Add(new ReportArchive
            {
                ReportType = reportType,
                Semester = semester,
                Department = department,
                GeneratedAt = DateTime.UtcNow,
                GeneratedByUserId = requestedByUserId,
                FilePath = publicPath,
                FileFormat = format.ToUpperInvariant()
            });

            await _context.SaveChangesAsync();
            return publicPath;
        }

        public async Task<List<ReportArchive>> GetReportArchivesAsync()
            => await _context.ReportArchives.OrderByDescending(r => r.GeneratedAt).ToListAsync();

        private async Task<List<Dictionary<string, string>>> BuildReportRows(string reportType, string semester, string department, DateTime fromDate, DateTime toDate)
        {
            return reportType switch
            {
                "FinalGrade" => await BuildFinalGradeRows(semester, department),
                "Workload" => await BuildWorkloadRows(department),
                "Milestone" => await BuildMilestoneRows(semester, department, fromDate, toDate),
                "Departmental" => await BuildDepartmentalRows(semester, department),
                "HEC_PEC" => await BuildHecPecRows(semester, department),
                _ => throw new InvalidOperationException("Unknown report type.")
            };
        }

        private async Task<List<Dictionary<string, string>>> BuildFinalGradeRows(string semester, string department)
        {
            var groups = await _context.Groups
                .Where(g => g.Semester == semester && g.Department == department)
                .ToListAsync();

            return groups.Select(g => new Dictionary<string, string>
            {
                ["Group"] = g.GroupName,
                ["FinalGrade"] = g.FinalGrade?.ToString("0.##") ?? "N/A",
                ["LetterGrade"] = g.LetterGrade ?? "N/A",
                ["Confirmed"] = g.IsFinalGradeConfirmed ? "Yes" : "No"
            }).ToList();
        }

        private async Task<List<Dictionary<string, string>>> BuildWorkloadRows(string department)
        {
            var groups = await _context.Groups.Where(g => g.Department == department).ToListAsync();
            var supervisors = await _context.Users.Where(u => u.Role == UserRole.Supervisor && u.IsActive).ToListAsync();

            return supervisors.Select(s => new Dictionary<string, string>
            {
                ["Supervisor"] = s.FullName,
                ["AssignedGroups"] = groups.Count(g => g.SupervisorId == s.Id).ToString()
            }).ToList();
        }

        private async Task<List<Dictionary<string, string>>> BuildMilestoneRows(string semester, string department, DateTime fromDate, DateTime toDate)
        {
            var groupIds = await _context.Groups
                .Where(g => g.Semester == semester && g.Department == department)
                .Select(g => g.Id)
                .ToListAsync();

            var projects = await _context.Projects.Where(p => groupIds.Contains(p.GroupId)).ToListAsync();
            var projectIds = projects.Select(p => p.Id).ToList();
            var milestones = await _context.Milestones
                .Where(m => projectIds.Contains(m.ProjectId) && m.DueDate >= fromDate && m.DueDate <= toDate)
                .ToListAsync();

            return milestones.Select(m => new Dictionary<string, string>
            {
                ["ProjectId"] = m.ProjectId.ToString(),
                ["Milestone"] = m.Title,
                ["Status"] = m.Status.ToString(),
                ["DueDate"] = m.DueDate.ToString("yyyy-MM-dd")
            }).ToList();
        }

        private async Task<List<Dictionary<string, string>>> BuildDepartmentalRows(string semester, string department)
        {
            var groups = await _context.Groups.Where(g => g.Semester == semester && g.Department == department).ToListAsync();
            var groupIds = groups.Select(g => g.Id).ToList();
            var proposals = await _context.Proposals.Where(p => groupIds.Contains(p.GroupId)).ToListAsync();

            return new List<Dictionary<string, string>>
            {
                new()
                {
                    ["Department"] = department,
                    ["Semester"] = semester,
                    ["TotalGroups"] = groups.Count.ToString(),
                    ["ConfirmedFinalGrades"] = groups.Count(g => g.IsFinalGradeConfirmed).ToString(),
                    ["PendingHODProposals"] = proposals.Count(p => p.Status == ProposalStatus.SupervisorApproved).ToString(),
                    ["ActiveApprovedProjects"] = proposals.Count(p => p.Status == ProposalStatus.CoordinatorApproved).ToString()
                }
            };
        }

        private async Task<List<Dictionary<string, string>>> BuildHecPecRows(string semester, string department)
        {
            var groups = await _context.Groups.Where(g => g.Semester == semester && g.Department == department).ToListAsync();
            var avg = groups.Where(g => g.FinalGrade.HasValue).Select(g => g.FinalGrade!.Value).DefaultIfEmpty().Average();

            return new List<Dictionary<string, string>>
            {
                new()
                {
                    ["Department"] = department,
                    ["Semester"] = semester,
                    ["TotalGroups"] = groups.Count.ToString(),
                    ["AverageFinalGrade"] = avg.ToString("0.##"),
                    ["AccreditationNote"] = "HEC/PEC template summary"
                }
            };
        }

        private static void GenerateXlsx(string path, string reportType, List<Dictionary<string, string>> rows)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add(reportType);

            if (!rows.Any())
            {
                ws.Cell(1, 1).Value = "No data.";
                workbook.SaveAs(path);
                return;
            }

            var headers = rows.First().Keys.ToList();
            for (int i = 0; i < headers.Count; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            for (int r = 0; r < rows.Count; r++)
            {
                for (int c = 0; c < headers.Count; c++)
                    ws.Cell(r + 2, c + 1).Value = rows[r][headers[c]];
            }

            ws.Columns().AdjustToContents();
            workbook.SaveAs(path);
        }

        private static void GeneratePdf(string path, string reportType, string semester, string department, DateTime fromDate, DateTime toDate, List<Dictionary<string, string>> rows)
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var headers = rows.Any() ? rows.First().Keys.ToList() : new List<string>();

            QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Size(PageSizes.A4.Landscape());
                    page.Content().Column(col =>
                    {
                        col.Item().Text($"{reportType} Report").Bold().FontSize(18);
                        col.Item().Text($"Semester: {semester} | Department: {department}");
                        col.Item().Text($"Range: {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}");
                        col.Item().PaddingVertical(10);

                        if (!rows.Any())
                        {
                            col.Item().Text("No data available.");
                            return;
                        }

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                foreach (var _ in headers)
                                    c.RelativeColumn();
                            });

                            table.Header(h =>
                            {
                                foreach (var header in headers)
                                    h.Cell().Text(header).Bold();
                            });

                            foreach (var row in rows)
                            {
                                foreach (var header in headers)
                                    table.Cell().Text(row[header]);
                            }
                        });
                    });
                });
            }).GeneratePdf(path);
        }

        public string ExportToCSV<T>(List<T> data)
        {
            if (data == null || data.Count == 0)
                return string.Empty;

            var csv = new StringBuilder();
            var properties = typeof(T).GetProperties();
            csv.AppendLine(string.Join(",", properties.Select(p => p.Name)));

            foreach (var item in data)
            {
                var values = properties.Select(p =>
                {
                    var value = p.GetValue(item)?.ToString() ?? string.Empty;
                    return value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
                });
                csv.AppendLine(string.Join(",", values));
            }

            return csv.ToString();
        }

        public async Task<Dictionary<string, int>> GetProjectsByStatus()
        {
            var result = new Dictionary<string, int>();
            var statusCounts = await _context.Projects.GroupBy(p => p.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();
            foreach (var item in statusCounts) result[item.Status.ToString()] = item.Count;
            foreach (ProjectStatus status in Enum.GetValues(typeof(ProjectStatus)))
                if (!result.ContainsKey(status.ToString())) result[status.ToString()] = 0;
            return result;
        }

        public async Task<Dictionary<string, int>> GetMilestoneCompletionByMonth()
        {
            var result = new Dictionary<string, int>();
            var completedMilestones = await _context.Milestones.Where(m => m.Status == MilestoneStatus.Completed).ToListAsync();
            var groupedByMonth = completedMilestones.GroupBy(m => m.DueDate.ToString("yyyy-MM")).ToDictionary(g => g.Key, g => g.Count());
            var today = DateTime.UtcNow;
            for (int i = 11; i >= 0; i--)
            {
                var monthKey = today.AddMonths(-i).ToString("yyyy-MM");
                result[monthKey] = groupedByMonth.ContainsKey(monthKey) ? groupedByMonth[monthKey] : 0;
            }
            return result;
        }

        public async Task<Dictionary<string, int>> GetProposalSubmissionTrend()
        {
            var result = new Dictionary<string, int>();
            var proposals = await _context.Proposals.ToListAsync();
            var groupedByMonth = proposals.GroupBy(p => p.SubmittedAt.ToString("yyyy-MM")).ToDictionary(g => g.Key, g => g.Count());
            var today = DateTime.UtcNow;
            for (int i = 11; i >= 0; i--)
            {
                var monthKey = today.AddMonths(-i).ToString("yyyy-MM");
                result[monthKey] = groupedByMonth.ContainsKey(monthKey) ? groupedByMonth[monthKey] : 0;
            }
            return result;
        }

        public async Task<Dictionary<string, object>> GetProjectStatistics()
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

        public async Task<Dictionary<string, object>> GetProposalStatistics()
        {
            var stats = new Dictionary<string, object>();
            var totalProposals = await _context.Proposals.CountAsync();
            var approvedProposals = await _context.Proposals.CountAsync(p => p.Status == ProposalStatus.CoordinatorApproved);
            var rejectedProposals = await _context.Proposals.CountAsync(p => p.Status == ProposalStatus.Rejected);

            stats["TotalProposals"] = totalProposals;
            stats["ApprovedProposals"] = approvedProposals;
            stats["RejectedProposals"] = rejectedProposals;
            stats["PendingProposals"] = totalProposals - approvedProposals - rejectedProposals;
            stats["ApprovalRate"] = totalProposals > 0 ? Math.Round((decimal)approvedProposals / totalProposals * 100, 2) : 0;
            return stats;
        }

        public async Task<Dictionary<string, object>> GetEvaluationStatistics()
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
    }
}
