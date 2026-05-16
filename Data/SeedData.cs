using FYP_AutomationSystem.Models;
using FYP_AutomationSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(AppDbContext db, AuthService auth)
        {
            // Users
            if (!await db.Users.AnyAsync())
            {
                var users = new List<User>
                {
                    new User { FullName = "System Admin",   Email = "admin@fyp.edu",        PasswordHash = auth.HashPassword("Admin@123"),   Role = UserRole.Admin,       CreatedAt = DateTime.UtcNow, IsActive = true },
                    new User { FullName = "Dr. Supervisor", Email = "supervisor1@fyp.edu",  PasswordHash = auth.HashPassword("Super@123"),   Role = UserRole.Supervisor,  Expertise = "AI / Machine Learning", CreatedAt = DateTime.UtcNow, IsActive = true },
                    new User { FullName = "Alice Student",  Email = "student1@fyp.edu",     PasswordHash = auth.HashPassword("Student@123"), Role = UserRole.Student,     CreatedAt = DateTime.UtcNow, IsActive = true },
                    new User { FullName = "Bob Student",    Email = "student2@fyp.edu",     PasswordHash = auth.HashPassword("Student@123"), Role = UserRole.Student,     CreatedAt = DateTime.UtcNow, IsActive = true },
                    new User { FullName = "Dr. HOD",        Email = "hod@fyp.edu",          PasswordHash = auth.HashPassword("HOD@123"),     Role = UserRole.HOD,         CreatedAt = DateTime.UtcNow, IsActive = true },
                    new User { FullName = "Coordinator",    Email = "coordinator@fyp.edu",  PasswordHash = auth.HashPassword("Coord@123"),   Role = UserRole.Coordinator, CreatedAt = DateTime.UtcNow, IsActive = true },
                    new User { FullName = "Panel Member",   Email = "panel@fyp.edu",        PasswordHash = auth.HashPassword("Panel@123"),   Role = UserRole.Panel,       CreatedAt = DateTime.UtcNow, IsActive = true },
                };
                db.Users.AddRange(users);
                await db.SaveChangesAsync();
            }

            var supervisor = await db.Users.FirstAsync(u => u.Email == "supervisor1@fyp.edu");
            var alice      = await db.Users.FirstAsync(u => u.Email == "student1@fyp.edu");
            var bob        = await db.Users.FirstAsync(u => u.Email == "student2@fyp.edu");

            // Group + Project
            if (!await db.Groups.AnyAsync())
            {
                var group = new Group
                {
                    GroupName = "Group Alpha",
                    SupervisorId = supervisor.Id,
                    CreatedAt = DateTime.UtcNow,
                    Semester = "Spring-2026",
                    Department = "CS",
                    FinalGrade = 88,
                    LetterGrade = "A",
                    IsFinalGradeConfirmed = true,
                    Members = new List<User> { alice, bob }
                };
                db.Groups.Add(group);
                await db.SaveChangesAsync();

                var project = new Project
                {
                    Title = "Smart FYP Automation",
                    Description = "An automated platform to manage the entire FYP lifecycle.",
                    GitHubUrl = "https://github.com/dotnet/aspnetcore",
                    Status = ProjectStatus.Active,
                    GroupId = group.Id,
                    CreatedAt = DateTime.UtcNow
                };
                db.Projects.Add(project);
                await db.SaveChangesAsync();

                db.Milestones.AddRange(
                    new Milestone { Title = "Proposal Approved",   Description = "Initial proposal sign-off",  DueDate = DateTime.UtcNow.AddDays(-30), Status = MilestoneStatus.Completed,  ProgressPercent = 100, ProjectId = project.Id },
                    new Milestone { Title = "System Design",       Description = "ER + UI design done",        DueDate = DateTime.UtcNow.AddDays(-10), Status = MilestoneStatus.Completed,  ProgressPercent = 100, ProjectId = project.Id },
                    new Milestone { Title = "Mid Implementation",  Description = "Core modules implemented",   DueDate = DateTime.UtcNow.AddDays(15),  Status = MilestoneStatus.InProgress, ProgressPercent = 55,  ProjectId = project.Id },
                    new Milestone { Title = "Final Demo",          Description = "Full system demo",           DueDate = DateTime.UtcNow.AddDays(45),  Status = MilestoneStatus.Pending,    ProgressPercent = 0,   ProjectId = project.Id }
                );
                await db.SaveChangesAsync();

                db.Proposals.Add(new Proposal
                {
                    Title = "Smart FYP Automation",
                    Abstract = "Automate proposal, milestone, evaluation, viva and reporting workflows.",
                    Objectives = "1. End-to-end FYP workflow\n2. Role-based dashboards\n3. Analytics",
                    Status = ProposalStatus.ApprovedActive,
                    ApprovedByCoordinator = true,
                    ApprovedByHOD = true,
                    CoordinatorApprovedAt = DateTime.UtcNow.AddDays(-41),
                    HODApprovedAt = DateTime.UtcNow.AddDays(-40),
                    StudentId = alice.Id,
                    GroupId = group.Id,
                    SubmittedAt = DateTime.UtcNow.AddDays(-40)
                });
                await db.SaveChangesAsync();
            }

            // Rubric items
            if (!await db.RubricItems.AnyAsync())
            {
                db.RubricItems.AddRange(
                    new RubricItem { Criterion = "Code Quality",   MaxMarks = 30, Description = "Code readability, structure, best practices" },
                    new RubricItem { Criterion = "Documentation",  MaxMarks = 20, Description = "Quality of report, diagrams and inline docs" },
                    new RubricItem { Criterion = "Presentation",   MaxMarks = 20, Description = "Clarity, demo and Q&A handling" }
                );
                await db.SaveChangesAsync();
            }

            // Notifications
            if (!await db.Notifications.AnyAsync())
            {
                db.Notifications.AddRange(
                    new Notification { Title = "Welcome",            Message = "Welcome to the FYP Automation System.",       Type = NotificationType.Info,     RecipientId = alice.Id,      IsRead = false, CreatedAt = DateTime.UtcNow.AddHours(-5) },
                    new Notification { Title = "Proposal Approved",  Message = "Your proposal has been approved by the HOD.", Type = NotificationType.ProposalDecision,  RecipientId = alice.Id,      IsRead = false, CreatedAt = DateTime.UtcNow.AddHours(-3) },
                    new Notification { Title = "Upcoming Deadline",  Message = "Mid Implementation milestone due soon.",      Type = NotificationType.Deadline, RecipientId = bob.Id,        IsRead = false, CreatedAt = DateTime.UtcNow.AddHours(-2) },
                    new Notification { Title = "New Group Assigned", Message = "Group Alpha has been assigned to you.",       Type = NotificationType.Info,     RecipientId = supervisor.Id, IsRead = false, CreatedAt = DateTime.UtcNow.AddHours(-1) },
                    new Notification { Title = "System Update",      Message = "FYP system v1.0 is now live.",                Type = NotificationType.Info,     RecipientId = (await db.Users.FirstAsync(u => u.Role == UserRole.Admin)).Id, IsRead = false, CreatedAt = DateTime.UtcNow }
                );
                foreach (var n in db.Notifications.Local)
                {
                    n.SentAt = n.CreatedAt;
                }
                await db.SaveChangesAsync();
            }
        }
    }
}
