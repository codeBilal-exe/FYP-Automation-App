using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class ProposalService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;

        public ProposalService(AppDbContext context, AuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<List<Proposal>> GetAllProposalsAsync()
        {
            return await _context.Proposals.ToListAsync();
        }

        public async Task<Proposal?> GetProposalByIdAsync(int id)
        {
            return await _context.Proposals.FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<List<Proposal>> GetProposalsByStudentAsync(int studentId)
        {
            return await _context.Proposals
                .Where(p => p.StudentId == studentId)
                .ToListAsync();
        }

        public async Task<Proposal> CreateProposalAsync(Proposal proposal)
        {
            _context.Proposals.Add(proposal);
            await _context.SaveChangesAsync();
            return proposal;
        }

        public async Task<Proposal> UpdateProposalAsync(Proposal proposal)
        {
            _context.Proposals.Update(proposal);
            await _context.SaveChangesAsync();
            return proposal;
        }

        public async Task<List<Proposal>> GetProposalsByStatusAsync(ProposalStatus status)
        {
            return await _context.Proposals
                .Where(p => p.Status == status)
                .ToListAsync();
        }

        public async Task<Proposal> UpdateProposalStatusAsync(int id, ProposalStatus status, string? rejectionReason = null)
        {
            var proposal = await GetProposalByIdAsync(id);
            if (proposal != null)
            {
                proposal.Status = status;
                if (!string.IsNullOrEmpty(rejectionReason))
                {
                    proposal.RejectionReason = rejectionReason;
                }
                await UpdateProposalAsync(proposal);
            }
            return proposal;
        }
    }
}
