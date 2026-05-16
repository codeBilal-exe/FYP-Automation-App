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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User Configuration
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

            // Group Configuration
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
                .HasMany(g => g.Members)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "GroupMembers",
                    l => l.HasOne<User>().WithMany().HasForeignKey("MembersId"),
                    r => r.HasOne<Group>().WithMany().HasForeignKey("GroupsId"));

            // Project Configuration
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

            // Proposal Configuration
            modelBuilder.Entity<Proposal>()
                .HasKey(p => p.Id);
            modelBuilder.Entity<Proposal>()
                .Property(p => p.Title)
                .IsRequired()
                .HasMaxLength(255);
            modelBuilder.Entity<Proposal>()
                .Property(p => p.Abstract)
                .IsRequired();

            // Milestone Configuration
            modelBuilder.Entity<Milestone>()
                .HasKey(m => m.Id);
            modelBuilder.Entity<Milestone>()
                .Property(m => m.Title)
                .IsRequired()
                .HasMaxLength(255);

            // Document Configuration
            modelBuilder.Entity<Document>()
                .HasKey(d => d.Id);
            modelBuilder.Entity<Document>()
                .Property(d => d.FileName)
                .IsRequired()
                .HasMaxLength(255);
            modelBuilder.Entity<Document>()
                .Property(d => d.FilePath)
                .IsRequired();

            // Evaluation Configuration
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

            // RubricItem Configuration
            modelBuilder.Entity<RubricItem>()
                .HasKey(ri => ri.Id);
            modelBuilder.Entity<RubricItem>()
                .Property(ri => ri.Criterion)
                .IsRequired()
                .HasMaxLength(255);

            // RubricScore Configuration
            modelBuilder.Entity<RubricScore>()
                .HasKey(rs => rs.Id);
            modelBuilder.Entity<RubricScore>()
                .HasOne(rs => rs.RubricItem)
                .WithMany()
                .HasForeignKey(rs => rs.RubricItemId)
                .OnDelete(DeleteBehavior.Restrict);

            // Notification Configuration
            modelBuilder.Entity<Notification>()
                .HasKey(n => n.Id);
            modelBuilder.Entity<Notification>()
                .Property(n => n.Title)
                .IsRequired()
                .HasMaxLength(255);

            // Message Configuration
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

            // VivaSlot Configuration
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

            // AuditLog Configuration
            modelBuilder.Entity<AuditLog>()
                .HasKey(al => al.Id);
            modelBuilder.Entity<AuditLog>()
                .Property(al => al.Action)
                .IsRequired()
                .HasMaxLength(255);

            // PlagiarismReport Configuration
            modelBuilder.Entity<PlagiarismReport>()
                .HasKey(pr => pr.Id);

            // ReportArchive Configuration
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
        }
    }
}
