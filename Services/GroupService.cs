using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class GroupService
    {
        private readonly AppDbContext _context;

        public GroupService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Returns all groups with members, project and supervisor loaded.
        /// </summary>
        public async Task<List<Group>> GetAllGroupsAsync()
        {
            try
            {
                return await _context.Groups
                    .Include(g => g.Members)
                    .Include(g => g.Supervisor)
                    .Include(g => g.Project)
                    .OrderBy(g => g.GroupName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetAllGroupsAsync error: {ex.Message}");
                return new List<Group>();
            }
        }

        /// <summary>
        /// Returns the group (with members) that a specific student belongs to.
        /// </summary>
        public async Task<Group?> GetGroupForStudentAsync(int studentId)
        {
            try
            {
                return await _context.Groups
                    .Include(g => g.Members)
                    .Include(g => g.Supervisor)
                    .Include(g => g.Project)
                    .FirstOrDefaultAsync(g => g.Members.Any(m => m.Id == studentId));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetGroupForStudentAsync error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns all groups supervised by the given supervisor.
        /// </summary>
        public async Task<List<Group>> GetGroupsForSupervisorAsync(int supervisorId)
        {
            try
            {
                return await _context.Groups
                    .Include(g => g.Members)
                    .Include(g => g.Project)
                    .Where(g => g.SupervisorId == supervisorId)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetGroupsForSupervisorAsync error: {ex.Message}");
                return new List<Group>();
            }
        }

        /// <summary>
        /// Updates the repo link for a group.
        /// SERVER-SIDE ENFORCED: only the team lead (GroupLeadId) may call this.
        /// </summary>
        /// <param name="groupId">Target group.</param>
        /// <param name="repoUrl">The repository URL to store (may be null/empty to clear).</param>
        /// <param name="requestingUserId">The user attempting the update.</param>
        /// <returns>A result tuple: (Success, ErrorMessage)</returns>
        public async Task<(bool Success, string Error)> UpdateRepoLinkAsync(
            int groupId,
            string? repoUrl,
            int requestingUserId)
        {
            try
            {
                if (groupId <= 0)
                    return (false, "Invalid group.");

                if (requestingUserId <= 0)
                    return (false, "Invalid requesting user.");

                var group = await _context.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
                if (group == null)
                    return (false, "Group not found.");

                // Server-side team lead enforcement
                if (group.GroupLeadId != requestingUserId)
                    return (false, "Only the team lead can update the repository link.");

                // Normalise/validate URL
                var normalized = NormalizeRepoUrl(repoUrl);

                group.RepoLink = normalized;
                _context.Groups.Update(group);
                await _context.SaveChangesAsync();

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateRepoLinkAsync error: {ex.Message}");
                return (false, $"Unexpected error: {ex.Message}");
            }
        }

        // ── Private helpers ────────────────────────────────────────────────

        /// <summary>
        /// Ensures the URL has an https:// prefix. Returns null for blank inputs.
        /// </summary>
        private static string? NormalizeRepoUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            var value = url.Trim();

            if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return value;

            if (value.Contains("github.com", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("gitlab.com", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("bitbucket.org", StringComparison.OrdinalIgnoreCase))
                return $"https://{value.TrimStart('/')}";

            // Treat bare paths as GitHub paths
            return $"https://github.com/{value.TrimStart('/')}";
        }
    }
}
