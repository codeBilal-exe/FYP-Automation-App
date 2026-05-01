using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class MilestoneService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;

        public MilestoneService(AppDbContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
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
    }
}
