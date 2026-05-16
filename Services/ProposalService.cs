using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    public class ProposalService
    {
        private readonly AppDbContext _context;
        private readonly AuditService _auditService;
        private readonly NotificationService _notificationService;
        private readonly ProjectThreadService _threadService;
        private readonly IWebHostEnvironment _environment;

        private static readonly string[] AllowedProposalExtensions = [".pdf", ".doc", ".docx"];
        private const long MaxProposalDocumentBytes = 10 * 1024 * 1024;

        public ProposalService(
            AppDbContext context,
            AuditService auditService,
            NotificationService notificationService,
            ProjectThreadService threadService,
            IWebHostEnvironment environment)
        {
            _context = context;
            _auditService = auditService;
            _notificationService = notificationService;
            _threadService = threadService;
            _environment = environment;
        }

        public async Task<List<Proposal>> GetAllProposalsAsync() => await _context.Proposals.ToListAsync();

        public async Task<Proposal?> GetProposalByIdAsync(int id) => await _context.Proposals.FirstOrDefaultAsync(p => p.Id == id);

        public async Task<Proposal?> GetLatestProposalByGroupAsync(int groupId, bool includeDraft = true)
        {
            return await _context.Proposals
                .Where(p => p.GroupId == groupId && (includeDraft || p.Status != ProposalStatus.Draft))
                .OrderByDescending(p => p.SubmittedAt)
                .ThenByDescending(p => p.UpdatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Proposal>> GetProposalsByStudentAsync(int studentId)
        {
            var groupId = await _context.Groups
                .Where(g => g.Members.Any(m => m.Id == studentId))
                .Select(g => g.Id)
                .FirstOrDefaultAsync();

            if (groupId <= 0)
            {
                return new List<Proposal>();
            }

            return await _context.Proposals
                .Where(p => p.GroupId == groupId)
                .OrderByDescending(p => p.SubmittedAt)
                .ToListAsync();
        }

        public async Task<List<Proposal>> GetProposalsByStatusAsync(ProposalStatus status)
            => await _context.Proposals.Where(p => p.Status == status).ToListAsync();

        public async Task<List<Proposal>> GetPendingHODProposalsAsync()
            => await _context.Proposals
                .Where(p => p.Status == ProposalStatus.SupervisorApproved)
                .OrderByDescending(p => p.SubmittedAt)
                .ToListAsync();

        public async Task<List<Proposal>> GetPendingProposalsForRoleAsync(UserRole role, int actorUserId)
        {
            if (role == UserRole.Supervisor)
            {
                var myGroupIds = await _context.Groups
                    .Where(g => g.SupervisorId == actorUserId)
                    .Select(g => g.Id)
                    .ToListAsync();

                return await _context.Proposals
                    .Where(p => p.Status == ProposalStatus.Submitted && myGroupIds.Contains(p.GroupId))
                    .OrderByDescending(p => p.SubmittedAt)
                    .ToListAsync();
            }

            if (role == UserRole.HOD)
            {
                return await _context.Proposals
                    .Where(p => p.Status == ProposalStatus.SupervisorApproved)
                    .OrderByDescending(p => p.SubmittedAt)
                    .ToListAsync();
            }

            if (role == UserRole.Coordinator)
            {
                return await _context.Proposals
                    .Where(p => p.Status == ProposalStatus.HODApproved)
                    .OrderByDescending(p => p.SubmittedAt)
                    .ToListAsync();
            }

            return new List<Proposal>();
        }

        public async Task<Proposal> CreateProposalAsync(Proposal proposal)
        {
            proposal.UpdatedAt = DateTime.UtcNow;
            _context.Proposals.Add(proposal);
            await _context.SaveChangesAsync();
            return proposal;
        }

        public async Task<Proposal> UpdateProposalAsync(Proposal proposal)
        {
            proposal.UpdatedAt = DateTime.UtcNow;
            _context.Proposals.Update(proposal);
            await _context.SaveChangesAsync();
            return proposal;
        }

        public async Task<(bool Success, string Message, string? SavedPath, string? SavedName)> SaveProposalDocumentAsync(IBrowserFile file)
        {
            if (file == null)
                return (false, "No file selected.", null, null);

            var ext = Path.GetExtension(file.Name)?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(ext) || !AllowedProposalExtensions.Contains(ext))
                return (false, "Invalid file type. Only PDF, DOC, DOCX are allowed.", null, null);

            if (file.Size <= 0 || file.Size > MaxProposalDocumentBytes)
                return (false, "File exceeds max size of 10 MB.", null, null);

            var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "proposals");
            Directory.CreateDirectory(uploadsRoot);

            var safeName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadsRoot, safeName);

            await using (var source = file.OpenReadStream(MaxProposalDocumentBytes))
            await using (var target = File.Create(fullPath))
            {
                await source.CopyToAsync(target);
            }

            var relativePath = $"/uploads/proposals/{safeName}";
            return (true, "File uploaded.", relativePath, file.Name);
        }

        public async Task<(bool Success, string Message, Proposal? Proposal)> SaveOrSubmitGroupProposalAsync(
            int studentId,
            Proposal payload,
            ProposalStatus targetStatus)
        {
            var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == payload.GroupId);
            if (group == null)
                return (false, "You must be in a group to submit a proposal.", null);

            if (!group.Members.Any(m => m.Id == studentId))
                return (false, "You are not part of this group.", null);

            var now = DateTime.UtcNow;
            var existingDraft = await _context.Proposals
                .Where(p => p.GroupId == payload.GroupId && p.Status == ProposalStatus.Draft)
                .OrderByDescending(p => p.UpdatedAt)
                .FirstOrDefaultAsync();
            var latestUnlinkedRejection = await _context.RejectionHistories
                .Where(r => r.GroupId == payload.GroupId && r.ResubmissionId == null)
                .OrderByDescending(r => r.RejectedAt)
                .FirstOrDefaultAsync();

            var isSubmit = targetStatus == ProposalStatus.Submitted;
            Proposal proposal;

            if (existingDraft != null)
            {
                proposal = existingDraft;
            }
            else
            {
                proposal = new Proposal
                {
                    GroupId = payload.GroupId,
                    StudentId = studentId,
                    SubmittedAt = now
                };
                _context.Proposals.Add(proposal);
            }

            proposal.Title = payload.Title.Trim();
            proposal.Abstract = payload.Abstract.Trim();
            proposal.Objectives = payload.Objectives.Trim();
            proposal.Domain = payload.Domain?.Trim() ?? string.Empty;
            proposal.Technologies = payload.Technologies?.Trim() ?? string.Empty;
            proposal.GitHubUrl = payload.GitHubUrl?.Trim();
            proposal.DocumentPath = payload.DocumentPath;
            proposal.DocumentName = payload.DocumentName;
            proposal.DocumentUploadedAt = payload.DocumentUploadedAt;
            proposal.RejectionReason = null;
            proposal.ReviewerComments = null;
            proposal.Status = targetStatus;
            proposal.CurrentApprovalLevel = isSubmit ? "supervisor" : "draft";
            proposal.UpdatedAt = now;
            if (isSubmit)
            {
                proposal.SubmittedAt = now;
            }

            await _context.SaveChangesAsync();

            if (!isSubmit)
            {
                return (true, "Draft saved.", proposal);
            }

            if (latestUnlinkedRejection != null)
            {
                latestUnlinkedRejection.ResubmissionId = proposal.Id;
                _context.RejectionHistories.Update(latestUnlinkedRejection);
                await _context.SaveChangesAsync();
            }

            await _auditService.LogActionAsync(studentId, "PROPOSAL_SUBMITTED", $"Proposal {proposal.Id} submitted by group {proposal.GroupId}");

            if (group.SupervisorId > 0)
            {
                await _notificationService.CreateNotification(
                    "New Proposal Submitted",
                    $"Group {group.GroupName} submitted proposal '{proposal.Title}'.",
                    NotificationType.Info,
                    group.SupervisorId,
                    "proposal_submitted",
                    proposal.Id.ToString(),
                    "/supervisor/proposals");
            }

            return (true, "Proposal submitted to supervisor.", proposal);
        }

        public async Task<bool> DiscardDraftAsync(int groupId)
        {
            var draft = await _context.Proposals
                .Where(p => p.GroupId == groupId && p.Status == ProposalStatus.Draft)
                .OrderByDescending(p => p.UpdatedAt)
                .FirstOrDefaultAsync();

            if (draft == null)
                return true;

            _context.Proposals.Remove(draft);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task ApproveBySupervisorOrCoordinator(int proposalId, int actorUserId, UserRole actorRole)
        {
            await ApproveProposalAsync(proposalId, actorUserId, actorRole, null);
        }

        public async Task ApproveByHOD(int proposalId, int hodUserId)
        {
            await ApproveProposalAsync(proposalId, hodUserId, UserRole.HOD, null);
        }

        public async Task RejectByHOD(int proposalId, int hodUserId, string rejectionReason)
        {
            await RejectProposalAsync(proposalId, hodUserId, UserRole.HOD, rejectionReason);
        }

        public async Task<(bool Success, string Message)> ApproveProposalAsync(int proposalId, int actorUserId, UserRole actorRole, string? comments)
        {
            var proposal = await _context.Proposals.FirstOrDefaultAsync(p => p.Id == proposalId)
                ?? throw new InvalidOperationException("Proposal not found.");

            var group = await _context.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == proposal.GroupId)
                ?? throw new InvalidOperationException("Group not found.");

            if (actorRole == UserRole.Supervisor)
            {
                if (group.SupervisorId != actorUserId)
                    throw new InvalidOperationException("You can only review your assigned groups.");
                if (proposal.Status != ProposalStatus.Submitted)
                    throw new InvalidOperationException("Only submitted proposals can be approved by supervisor.");

                proposal.Status = ProposalStatus.SupervisorApproved;
                proposal.CurrentApprovalLevel = "hod";
                proposal.ReviewerComments = comments;

                var hods = await _context.Users.Where(u => u.Role == UserRole.HOD && u.IsActive).ToListAsync();
                foreach (var hod in hods)
                {
                    await _notificationService.CreateNotification(
                        "Proposal Awaiting HOD Approval",
                        $"Group {group.GroupName} proposal '{proposal.Title}' is ready for HOD review.",
                        NotificationType.Info,
                        hod.Id,
                        "proposal_waiting_hod",
                        proposal.Id.ToString(),
                        "/hod/proposals");
                }

                await _notificationService.NotifyProposalStatusForGroup(
                    group.Id,
                    "Supervisor Approved Proposal",
                    "Your proposal was approved by your supervisor and moved to HOD review.",
                    NotificationType.ProposalDecision,
                    "proposal_supervisor_approved",
                    proposal.Id.ToString(),
                    "/student/proposal");

                await _auditService.LogActionAsync(actorUserId, "SUPERVISOR_APPROVED", $"Proposal {proposalId} approved by supervisor.");
            }
            else if (actorRole == UserRole.HOD)
            {
                if (proposal.Status != ProposalStatus.SupervisorApproved)
                    throw new InvalidOperationException("Proposal must be supervisor-approved first.");

                proposal.Status = ProposalStatus.HODApproved;
                proposal.CurrentApprovalLevel = "coordinator";
                proposal.ApprovedByHOD = true;
                proposal.HODApprovedAt = DateTime.UtcNow;
                proposal.HODFeedback = comments;
                proposal.ReviewerComments = comments;

                var coordinators = await _context.Users.Where(u => u.Role == UserRole.Coordinator && u.IsActive).ToListAsync();
                foreach (var coordinator in coordinators)
                {
                    await _notificationService.CreateNotification(
                        "Proposal Awaiting Coordinator Approval",
                        $"Group {group.GroupName} proposal '{proposal.Title}' is ready for coordinator review.",
                        NotificationType.Info,
                        coordinator.Id,
                        "proposal_waiting_coordinator",
                        proposal.Id.ToString(),
                        "/coordinator/proposals");
                }

                await _notificationService.NotifyProposalStatusForGroup(
                    group.Id,
                    "HOD Approved Proposal",
                    "Your proposal was approved by HOD and moved to coordinator review.",
                    NotificationType.ProposalDecision,
                    "proposal_hod_approved",
                    proposal.Id.ToString(),
                    "/student/proposal");

                await _auditService.LogActionAsync(actorUserId, "HOD_APPROVED", $"Proposal {proposalId} approved by HOD.");
            }
            else if (actorRole == UserRole.Coordinator)
            {
                if (proposal.Status != ProposalStatus.HODApproved)
                    throw new InvalidOperationException("Proposal must be HOD-approved first.");

                proposal.Status = ProposalStatus.CoordinatorApproved;
                proposal.CurrentApprovalLevel = null;
                proposal.ApprovedByCoordinator = true;
                proposal.CoordinatorApprovedAt = DateTime.UtcNow;
                proposal.ReviewerComments = comments;

                var project = await _context.Projects.FirstOrDefaultAsync(p => p.GroupId == proposal.GroupId);
                if (project != null)
                {
                    project.Status = ProjectStatus.Active;
                    _context.Projects.Update(project);
                }

                await _threadService.EnsureThreadForApprovedProposalAsync(proposal);

                await _notificationService.NotifyProposalStatusForGroup(
                    group.Id,
                    "Proposal Fully Approved",
                    "Your proposal is fully approved and the project is now active.",
                    NotificationType.Success,
                    "proposal_fully_approved",
                    proposal.Id.ToString(),
                    "/student/dashboard");

                await _auditService.LogActionAsync(actorUserId, "COORDINATOR_APPROVED", $"Proposal {proposalId} fully approved by coordinator.");
            }
            else
            {
                throw new InvalidOperationException("This role cannot approve proposals.");
            }

            proposal.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return (true, "Proposal approved.");
        }

        public async Task<(bool Success, string Message)> RejectProposalAsync(int proposalId, int actorUserId, UserRole actorRole, string rejectionReason)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
                return (false, "Rejection reason is required.");

            var proposal = await _context.Proposals.FirstOrDefaultAsync(p => p.Id == proposalId)
                ?? throw new InvalidOperationException("Proposal not found.");

            if (actorRole != UserRole.Supervisor && actorRole != UserRole.HOD && actorRole != UserRole.Coordinator)
                throw new InvalidOperationException("This role cannot reject proposals.");

            proposal.Status = ProposalStatus.Rejected;
            proposal.RejectionReason = rejectionReason.Trim();
            proposal.ReviewerComments = rejectionReason.Trim();
            proposal.CurrentApprovalLevel = actorRole.ToString().ToLowerInvariant();
            proposal.UpdatedAt = DateTime.UtcNow;

            if (actorRole == UserRole.HOD)
            {
                proposal.HODFeedback = rejectionReason.Trim();
                proposal.HODRejectedAt = DateTime.UtcNow;
            }

            _context.Proposals.Update(proposal);

            if (actorRole == UserRole.Supervisor || actorRole == UserRole.HOD)
            {
                _context.RejectionHistories.Add(new RejectionHistory
                {
                    ProposalId = proposal.Id,
                    GroupId = proposal.GroupId,
                    RejectedByUserId = actorUserId,
                    RejectedByRole = actorRole.ToString(),
                    RejectionReason = rejectionReason.Trim(),
                    RejectedAt = DateTime.UtcNow,
                    ResubmissionId = null,
                    ResubmissionImproved = null
                });
            }

            await _context.SaveChangesAsync();

            await _notificationService.NotifyProposalStatusForGroup(
                proposal.GroupId,
                "Proposal Rejected",
                $"Your proposal was rejected by {actorRole}. Reason: {rejectionReason.Trim()}",
                NotificationType.ProposalDecision,
                "proposal_rejected",
                proposal.Id.ToString(),
                "/student/proposal");

            await _auditService.LogActionAsync(actorUserId, "PROPOSAL_REJECTED", $"Proposal {proposalId} rejected by {actorRole}. Reason: {rejectionReason}");
            return (true, "Proposal rejected.");
        }

        public async Task<Proposal?> UpdateProposalStatusAsync(int id, ProposalStatus status, string? rejectionReason = null)
        {
            var proposal = await GetProposalByIdAsync(id);
            if (proposal != null)
            {
                proposal.Status = status;
                proposal.UpdatedAt = DateTime.UtcNow;

                if (!string.IsNullOrWhiteSpace(rejectionReason))
                {
                    proposal.RejectionReason = rejectionReason.Trim();
                    proposal.ReviewerComments = rejectionReason.Trim();
                }

                await UpdateProposalAsync(proposal);
            }

            return proposal;
        }
    }
}
