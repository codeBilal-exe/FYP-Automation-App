using FYP_AutomationSystem.Models;
using FYP_AutomationSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(AppDbContext db, AuthService auth)
        {
            await EnsureGroupLeadColumnAsync(db);

            var now = DateTime.UtcNow;

            var demoUsers = new List<(string FullName, string Email, string Password, UserRole Role, string? Expertise)>
            {
                ("System Admin", "admin@fyp.edu", "Admin@123", UserRole.Admin, null),
                ("Coordinator", "coordinator@fyp.edu", "Coord@123", UserRole.Coordinator, null),
                ("Dr. HOD", "hod@fyp.edu", "HOD@123", UserRole.HOD, null),
                ("Dr. HOD - CS", "hod2@fyp.edu", "HOD@123", UserRole.HOD, null),
                ("Dr. Supervisor", "supervisor1@fyp.edu", "Super@123", UserRole.Supervisor, "AI / Machine Learning"),
                ("Dr. Supervisor Two", "supervisor2@fyp.edu", "Super@123", UserRole.Supervisor, "Software Engineering"),
                ("Dr. Supervisor Three", "supervisor3@fyp.edu", "Super@123", UserRole.Supervisor, "Data Science"),
                ("Panel Member", "panel@fyp.edu", "Panel@123", UserRole.Panel, null),
                ("Alice Student", "student1@fyp.edu", "Student@123", UserRole.Student, null),
                ("Bob Student", "student2@fyp.edu", "Student@123", UserRole.Student, null),
                ("Charlie Student", "student3@fyp.edu", "Student@123", UserRole.Student, null),
                ("Diana Student", "student4@fyp.edu", "Student@123", UserRole.Student, null),
                ("Ethan Student", "student5@fyp.edu", "Student@123", UserRole.Student, null),
                ("Fatima Student", "student6@fyp.edu", "Student@123", UserRole.Student, null),
                ("Hassan Student", "student7@fyp.edu", "Student@123", UserRole.Student, null),
                ("Iqra Student", "student8@fyp.edu", "Student@123", UserRole.Student, null),
            };

            foreach (var u in demoUsers)
            {
                await UpsertDemoUserAsync(db, auth, now, u.FullName, u.Email, u.Password, u.Role, u.Expertise);
            }

            var supervisor = await db.Users.FirstAsync(u => u.Email == "supervisor1@fyp.edu");
            var alice = await db.Users.FirstAsync(u => u.Email == "student1@fyp.edu");
            var bob = await db.Users.FirstAsync(u => u.Email == "student2@fyp.edu");
            var charlie = await db.Users.FirstAsync(u => u.Email == "student3@fyp.edu");

            // Group + Project
            if (!await db.Groups.AnyAsync())
            {
                var group = new Group
                {
                    GroupName = "Group Alpha",
                    SupervisorId = supervisor.Id,
                    GroupLeadId = alice.Id,
                    CreatedAt = now,
                    Semester = "Spring-2026",
                    Department = "CS",
                    FinalGrade = 88,
                    LetterGrade = "A",
                    IsFinalGradeConfirmed = true,
                    Members = new List<User> { alice, bob, charlie }
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
                    CreatedAt = now
                };
                db.Projects.Add(project);
                await db.SaveChangesAsync();

                db.Milestones.AddRange(
                    new Milestone { Title = "Proposal Approved", Description = "Initial proposal sign-off", DueDate = now.AddDays(-30), Status = MilestoneStatus.Completed, ProgressPercent = 100, ProjectId = project.Id },
                    new Milestone { Title = "System Design", Description = "ER + UI design done", DueDate = now.AddDays(-10), Status = MilestoneStatus.Completed, ProgressPercent = 100, ProjectId = project.Id },
                    new Milestone { Title = "Mid Implementation", Description = "Core modules implemented", DueDate = now.AddDays(15), Status = MilestoneStatus.InProgress, ProgressPercent = 55, ProjectId = project.Id },
                    new Milestone { Title = "Final Demo", Description = "Full system demo", DueDate = now.AddDays(45), Status = MilestoneStatus.Pending, ProgressPercent = 0, ProjectId = project.Id }
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
                    CoordinatorApprovedAt = now.AddDays(-41),
                    HODApprovedAt = now.AddDays(-40),
                    StudentId = alice.Id,
                    GroupId = group.Id,
                    SubmittedAt = now.AddDays(-40)
                });
                await db.SaveChangesAsync();
            }

            // Rubric items
            if (!await db.RubricItems.AnyAsync())
            {
                db.RubricItems.AddRange(
                    new RubricItem { Criterion = "Code Quality", MaxMarks = 30, Description = "Code readability, structure, best practices" },
                    new RubricItem { Criterion = "Documentation", MaxMarks = 20, Description = "Quality of report, diagrams and inline docs" },
                    new RubricItem { Criterion = "Presentation", MaxMarks = 20, Description = "Clarity, demo and Q&A handling" }
                );
                await db.SaveChangesAsync();
            }

            // Notifications
            if (!await db.Notifications.AnyAsync())
            {
                db.Notifications.AddRange(
                    new Notification { Title = "Welcome", Message = "Welcome to the FYP Automation System.", Type = NotificationType.Info, RecipientId = alice.Id, IsRead = false, CreatedAt = now.AddHours(-5) },
                    new Notification { Title = "Proposal Approved", Message = "Your proposal has been approved by the HOD.", Type = NotificationType.ProposalDecision, RecipientId = alice.Id, IsRead = false, CreatedAt = now.AddHours(-3) },
                    new Notification { Title = "Upcoming Deadline", Message = "Mid Implementation milestone due soon.", Type = NotificationType.Deadline, RecipientId = bob.Id, IsRead = false, CreatedAt = now.AddHours(-2) },
                    new Notification { Title = "New Group Assigned", Message = "Group Alpha has been assigned to you.", Type = NotificationType.Info, RecipientId = supervisor.Id, IsRead = false, CreatedAt = now.AddHours(-1) },
                    new Notification { Title = "System Update", Message = "FYP system v1.0 is now live.", Type = NotificationType.Info, RecipientId = (await db.Users.FirstAsync(u => u.Role == UserRole.Admin)).Id, IsRead = false, CreatedAt = now }
                );
                foreach (var n in db.Notifications.Local)
                {
                    n.SentAt = n.CreatedAt;
                }
                await db.SaveChangesAsync();
            }
        }

        private static async Task UpsertDemoUserAsync(
            AppDbContext db,
            AuthService auth,
            DateTime createdAt,
            string fullName,
            string email,
            string password,
            UserRole role,
            string? expertise)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                db.Users.Add(new User
                {
                    FullName = fullName,
                    Email = email,
                    PasswordHash = auth.HashPassword(password),
                    Role = role,
                    Expertise = expertise,
                    CreatedAt = createdAt,
                    IsActive = true,
                    FailedLoginAttempts = 0,
                    IsLockedOut = false,
                    LockoutUntil = null
                });
            }
            else
            {
                user.FullName = fullName;
                user.Role = role;
                user.IsActive = true;
                user.IsLockedOut = false;
                user.FailedLoginAttempts = 0;
                user.LockoutUntil = null;
                user.PasswordHash = auth.HashPassword(password);
                if (!string.IsNullOrWhiteSpace(expertise))
                {
                    user.Expertise = expertise;
                }
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsureGroupLeadColumnAsync(AppDbContext db)
        {
            try
            {
                var provider = db.Database.ProviderName ?? string.Empty;

                if (db.Database.IsNpgsql())
                {
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Groups\" ADD COLUMN IF NOT EXISTS \"GroupLeadId\" integer NULL;");
                }
                else if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    var hasColumn = false;
                    await using var command = db.Database.GetDbConnection().CreateCommand();
                    command.CommandText = "PRAGMA table_info('Groups');";
                    var connection = command.Connection;
                    if (connection == null)
                    {
                        return;
                    }

                    if (connection.State != System.Data.ConnectionState.Open)
                    {
                        await connection.OpenAsync();
                    }

                    await using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var name = reader[1]?.ToString();
                        if (string.Equals(name, "GroupLeadId", StringComparison.OrdinalIgnoreCase))
                        {
                            hasColumn = true;
                            break;
                        }
                    }

                    if (!hasColumn)
                    {
                        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Groups ADD COLUMN GroupLeadId INTEGER NULL;");
                    }
                }
            }
            catch
            {
                // Best-effort schema patch for demo environments.
            }
        }
    }
}
