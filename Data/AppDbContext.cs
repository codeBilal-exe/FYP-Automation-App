using Microsoft.EntityFrameworkCore;
using FYP_AutomationSystem.Models;

namespace FYP_AutomationSystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<Proposal> Proposals { get; set; }
        public DbSet<Milestone> Milestones { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Evaluation> Evaluations { get; set; }
        public DbSet<RubricItem> RubricItems { get; set; }
        public DbSet<RubricScore> RubricScores { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<VivaSlot> VivaSlots { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<PlagiarismReport> PlagiarismReports { get; set; }
        public DbSet<ReportArchive> ReportArchives { get; set; }

        public DbSet<ProjectThread> ProjectThreads { get; set; }
        public DbSet<ProjectTask> ProjectTasks { get; set; }
        public DbSet<TaskSubmission> TaskSubmissions { get; set; }
        public DbSet<RejectionHistory> RejectionHistories { get; set; }
        public DbSet<GroupMessageThread> GroupMessageThreads { get; set; }
        public DbSet<GroupMessage> GroupMessages { get; set; }
        public DbSet<PersonalMessage> PersonalMessages { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<FacultyTimetable> FacultyTimetables { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasKey(u => u.Id);
            modelBuilder.Entity<User>()
                .Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(255);
            modelBuilder.Entity<User>()
                .Property(u => u.FullName)
                .IsRequired()
                .HasMaxLength(255);
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Group>()
                .HasKey(g => g.Id);
            modelBuilder.Entity<Group>()
                .Property(g => g.GroupName)
                .IsRequired()
                .HasMaxLength(255);
            modelBuilder.Entity<Group>()
                .HasOne(g => g.Supervisor)
                .WithMany()
                .HasForeignKey(g => g.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Group>()
                .HasOne(g => g.GroupLead)
                .WithMany()
                .HasForeignKey(g => g.GroupLeadId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Group>()
                .HasMany(g => g.Members)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "GroupMembers",
                    l => l.HasOne<User>().WithMany().HasForeignKey("MembersId"),
                    r => r.HasOne<Group>().WithMany().HasForeignKey("GroupsId"));

            modelBuilder.Entity<Project>()
                .HasKey(p => p.Id);
            modelBuilder.Entity<Project>()
                .Property(p => p.Title)
                .IsRequired()
                .HasMaxLength(255);
            modelBuilder.Entity<Project>()
                .HasOne(p => p.Group)
                .WithOne(g => g.Project)
                .HasForeignKey<Project>(p => p.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Project>()
                .HasMany(p => p.Milestones)
                .WithOne(m => m.Project)
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Project>()
                .HasMany(p => p.Documents)
                .WithOne()
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Proposal>()
                .HasKey(p => p.Id);
            modelBuilder.Entity<Proposal>()
                .Property(p => p.Title)
                .IsRequired()
                .HasMaxLength(255);
            modelBuilder.Entity<Proposal>()
                .Property(p => p.Abstract)
                .IsRequired();

            modelBuilder.Entity<Milestone>()
                .HasKey(m => m.Id);
            modelBuilder.Entity<Milestone>()
                .Property(m => m.Title)
                .IsRequired()
                .HasMaxLength(255);

            modelBuilder.Entity<Document>()
                .HasKey(d => d.Id);
            modelBuilder.Entity<Document>()
                .Property(d => d.FileName)
                .IsRequired()
                .HasMaxLength(255);
            modelBuilder.Entity<Document>()
                .Property(d => d.FilePath)
                .IsRequired();

            modelBuilder.Entity<Evaluation>()
                .HasKey(e => e.Id);
            modelBuilder.Entity<Evaluation>()
                .HasOne(e => e.Evaluator)
                .WithMany()
                .HasForeignKey(e => e.EvaluatorId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Evaluation>()
                .HasMany(e => e.RubricScores)
                .WithOne()
                .HasForeignKey(rs => rs.EvaluationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RubricItem>()
                .HasKey(ri => ri.Id);
            modelBuilder.Entity<RubricItem>()
                .Property(ri => ri.Criterion)
                .IsRequired()
                .HasMaxLength(255);

            modelBuilder.Entity<RubricScore>()
                .HasKey(rs => rs.Id);
            modelBuilder.Entity<RubricScore>()
                .HasOne(rs => rs.RubricItem)
                .WithMany()
                .HasForeignKey(rs => rs.RubricItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Notification>()
                .HasKey(n => n.Id);
            modelBuilder.Entity<Notification>()
                .Property(n => n.Title)
                .IsRequired()
                .HasMaxLength(255);
            modelBuilder.Entity<Notification>()
                .Property(n => n.Description)
                .HasMaxLength(2000);
            modelBuilder.Entity<Notification>()
                .Property(n => n.EventType)
                .HasMaxLength(128);
            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.RecipientId, n.EventType, n.ReferenceId });

            modelBuilder.Entity<Message>()
                .HasKey(m => m.Id);
            modelBuilder.Entity<Message>()
                .Property(m => m.Content)
                .IsRequired();
            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<VivaSlot>()
                .HasKey(vs => vs.Id);
            modelBuilder.Entity<VivaSlot>()
                .Property(vs => vs.Venue)
                .IsRequired()
                .HasMaxLength(255);
            modelBuilder.Entity<VivaSlot>()
                .HasMany(vs => vs.PanelMembers)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "VivaPanelMembers",
                    l => l.HasOne<User>().WithMany().HasForeignKey("PanelMembersId"),
                    r => r.HasOne<VivaSlot>().WithMany().HasForeignKey("VivaSlotsId"));
            modelBuilder.Entity<VivaSlot>()
                .HasOne(vs => vs.Group)
                .WithMany()
                .HasForeignKey(vs => vs.GroupId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<VivaSlot>()
                .HasOne(vs => vs.Milestone)
                .WithMany()
                .HasForeignKey(vs => vs.MilestoneId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<FacultyTimetable>()
                .HasKey(ft => ft.Id);
            modelBuilder.Entity<FacultyTimetable>()
                .HasOne(ft => ft.Faculty)
                .WithMany()
                .HasForeignKey(ft => ft.FacultyId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<FacultyTimetable>()
                .HasIndex(ft => new { ft.FacultyId, ft.Day });

            modelBuilder.Entity<AuditLog>()
                .HasKey(al => al.Id);
            modelBuilder.Entity<AuditLog>()
                .Property(al => al.Action)
                .IsRequired()
                .HasMaxLength(255);

            modelBuilder.Entity<PlagiarismReport>()
                .HasKey(pr => pr.Id);

            modelBuilder.Entity<ReportArchive>()
                .HasKey(ra => ra.Id);
            modelBuilder.Entity<ReportArchive>()
                .Property(ra => ra.ReportType)
                .IsRequired()
                .HasMaxLength(64);
            modelBuilder.Entity<ReportArchive>()
                .Property(ra => ra.FilePath)
                .IsRequired()
                .HasMaxLength(500);

            modelBuilder.Entity<ProjectThread>()
                .HasKey(t => t.Id);
            modelBuilder.Entity<ProjectTask>()
                .HasKey(t => t.Id);
            modelBuilder.Entity<TaskSubmission>()
                .HasKey(s => s.Id);
            modelBuilder.Entity<RejectionHistory>()
                .HasKey(r => r.Id);
            modelBuilder.Entity<GroupMessageThread>()
                .HasKey(t => t.Id);
            modelBuilder.Entity<GroupMessage>()
                .HasKey(m => m.Id);
            modelBuilder.Entity<PersonalMessage>()
                .HasKey(m => m.Id);
            modelBuilder.Entity<PasswordResetToken>()
                .HasKey(t => t.Id);
            modelBuilder.Entity<PasswordResetToken>()
                .Property(t => t.TokenHash)
                .IsRequired()
                .HasMaxLength(128);
            modelBuilder.Entity<PasswordResetToken>()
                .HasIndex(t => t.TokenHash)
                .IsUnique();
            modelBuilder.Entity<PasswordResetToken>()
                .HasIndex(t => new { t.UserId, t.ExpiresAt });
            modelBuilder.Entity<PasswordResetToken>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
