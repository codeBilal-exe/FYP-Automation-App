using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class ProposalService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;
        private readonly NotificationService _notificationService;

        public ProposalService(AppDbContext context, AuditService auditService, NotificationService notificationService)
        {
            _context = context;
            _auditService = auditService;
            _notificationService = notificationService;
        }

        public async Task<List<Proposal>> GetAllProposalsAsync() => await _context.Proposals.ToListAsync();

        public async Task<Proposal?> GetProposalByIdAsync(int id) => await _context.Proposals.FirstOrDefaultAsync(p => p.Id == id);

        public async Task<List<Proposal>> GetProposalsByStudentAsync(int studentId)
            => await _context.Proposals.Where(p => p.StudentId == studentId).ToListAsync();

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
            => await _context.Proposals.Where(p => p.Status == status).ToListAsync();

        public async Task<List<Proposal>> GetPendingHODProposalsAsync()
            => await _context.Proposals
                .Where(p => p.Status == ProposalStatus.PendingHOD && p.ApprovedByCoordinator && !p.ApprovedByHOD)
                .ToListAsync();

        public async Task ApproveBySupervisorOrCoordinator(int proposalId, int actorUserId, UserRole actorRole)
        {
            var proposal = await _context.Proposals.FirstOrDefaultAsync(p => p.Id == proposalId)
                ?? throw new InvalidOperationException("Proposal not found.");

            if (actorRole == UserRole.Supervisor)
            {
                if (proposal.Status != ProposalStatus.SubmittedToSupervisor)
                    throw new InvalidOperationException("Only submitted proposals can be approved by supervisor.");

                proposal.Status = ProposalStatus.ApprovedBySupervisor;
                await _auditService.LogActionAsync(actorUserId, "SUPERVISOR_APPROVED", $"Proposal {proposalId} approved by supervisor");
            }
            else if (actorRole == UserRole.Coordinator)
            {
                if (proposal.Status != ProposalStatus.ApprovedBySupervisor)
                    throw new InvalidOperationException("Proposal must be approved by supervisor first.");

                proposal.ApprovedByCoordinator = true;
                proposal.CoordinatorApprovedAt = DateTime.UtcNow;
                proposal.Status = ProposalStatus.PendingHOD;
                await _auditService.LogActionAsync(actorUserId, "COORDINATOR_APPROVED", $"Proposal {proposalId} forwarded to HOD");

                var group = await _context.Groups.FirstOrDefaultAsync(g => g.Id == proposal.GroupId);
                var hodUsers = await _context.Users.Where(u => u.Role == UserRole.HOD && u.IsActive).ToListAsync();
                var message = $"A proposal is awaiting your review: {proposal.Title} by {group?.GroupName ?? "Unknown Group"}";
                foreach (var hod in hodUsers)
                {
                    await _notificationService.CreateNotification(
                        "Proposal Awaiting Review",
                        message,
                        NotificationType.Info,
                        hod.Id);
                }
            }
            else
            {
                throw new InvalidOperationException("Unsupported actor role for this operation.");
            }

            await _context.SaveChangesAsync();
        }

        public async Task ApproveByHOD(int proposalId, int hodUserId)
        {
            var proposal = await _context.Proposals.FirstOrDefaultAsync(p => p.Id == proposalId)
                ?? throw new InvalidOperationException("Proposal not found.");

            if (!proposal.ApprovedByCoordinator)
                throw new InvalidOperationException("Proposal has not been approved by FYP Coordinator yet.");

            proposal.ApprovedByHOD = true;
            proposal.Status = ProposalStatus.ApprovedActive;
            proposal.HODApprovedAt = DateTime.UtcNow;

            var project = await _context.Projects.FirstOrDefaultAsync(p => p.GroupId == proposal.GroupId);
            if (project != null)
                project.Status = ProjectStatus.Active;

            await _auditService.LogActionAsync(hodUserId, "HOD_APPROVED", $"Proposal {proposalId} approved by HOD");
            await _notificationService.NotifyHODDecision(proposalId, approved: true, feedback: null);
            await _context.SaveChangesAsync();
        }

        public async Task RejectByHOD(int proposalId, int hodUserId, string rejectionReason)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
                throw new ArgumentException("Rejection reason is required.");

            var proposal = await _context.Proposals.FirstOrDefaultAsync(p => p.Id == proposalId)
                ?? throw new InvalidOperationException("Proposal not found.");

            if (!proposal.ApprovedByCoordinator)
                throw new InvalidOperationException("Proposal has not been approved by FYP Coordinator yet.");

            proposal.Status = ProposalStatus.Rejected;
            proposal.HODFeedback = rejectionReason;
            proposal.RejectionReason = rejectionReason;
            proposal.HODRejectedAt = DateTime.UtcNow;

            await _auditService.LogActionAsync(hodUserId, "HOD_REJECTED", $"Proposal {proposalId} rejected by HOD. Reason: {rejectionReason}");
            await _notificationService.NotifyHODDecision(proposalId, approved: false, feedback: rejectionReason);
            await _context.SaveChangesAsync();
        }

        public async Task<Proposal?> UpdateProposalStatusAsync(int id, ProposalStatus status, string? rejectionReason = null)
        {
            var proposal = await GetProposalByIdAsync(id);
            if (proposal != null)
            {
                proposal.Status = status;
                if (!string.IsNullOrEmpty(rejectionReason))
                    proposal.RejectionReason = rejectionReason;

                await UpdateProposalAsync(proposal);
            }
            return proposal;
        }
    }
}
