using FYP_AutomationSystem.Models;
using FYP_AutomationSystem.Services;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(AppDbContext db, AuthService auth)
        {
            await EnsureRuntimeSchemaAsync(db);

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
                ("Iqra Student", "student8@fyp.edu", "Student@123", UserRole.Student, null)
            };

            foreach (var u in demoUsers)
            {
                await UpsertDemoUserAsync(db, auth, now, u.FullName, u.Email, u.Password, u.Role, u.Expertise);
            }

            var supervisor1 = await db.Users.FirstAsync(u => u.Email == "supervisor1@fyp.edu");
            var supervisor2 = await db.Users.FirstAsync(u => u.Email == "supervisor2@fyp.edu");
            var alice = await db.Users.FirstAsync(u => u.Email == "student1@fyp.edu");
            var bob = await db.Users.FirstAsync(u => u.Email == "student2@fyp.edu");
            var charlie = await db.Users.FirstAsync(u => u.Email == "student3@fyp.edu");
            var diana = await db.Users.FirstAsync(u => u.Email == "student4@fyp.edu");
            var ethan = await db.Users.FirstAsync(u => u.Email == "student5@fyp.edu");
            var fatima = await db.Users.FirstAsync(u => u.Email == "student6@fyp.edu");

            if (!await db.Groups.AnyAsync())
            {
                var g1 = new Group
                {
                    GroupName = "Group Alpha",
                    SupervisorId = supervisor1.Id,
                    GroupLeadId = alice.Id,
                    CreatedAt = now,
                    Semester = "Spring-2026",
                    Department = "CS",
                    Members = new List<User> { alice, bob, charlie }
                };
                var g2 = new Group
                {
                    GroupName = "Group Beta",
                    SupervisorId = supervisor2.Id,
                    GroupLeadId = diana.Id,
                    CreatedAt = now,
                    Semester = "Spring-2026",
                    Department = "CS",
                    Members = new List<User> { diana, ethan, fatima }
                };

                db.Groups.AddRange(g1, g2);
                await db.SaveChangesAsync();

                var p1 = new Project
                {
                    Title = "Smart FYP Automation",
                    Description = "Workflow automation for FYP lifecycle.",
                    GitHubUrl = "https://github.com/dotnet/aspnetcore",
                    Status = ProjectStatus.Active,
                    GroupId = g1.Id,
                    CreatedAt = now.AddDays(-20)
                };

                var p2 = new Project
                {
                    Title = "Campus Data Insights",
                    Description = "Analytics platform for university operations.",
                    GitHubUrl = "https://github.com/dotnet/runtime",
                    Status = ProjectStatus.Pending,
                    GroupId = g2.Id,
                    CreatedAt = now.AddDays(-5)
                };

                db.Projects.AddRange(p1, p2);
                await db.SaveChangesAsync();

                db.Proposals.AddRange(
                    new Proposal
                    {
                        Title = "Smart FYP Automation",
                        Abstract = "Automate proposal, milestones, evaluations and reporting.",
                        Objectives = "1. End-to-end workflow\n2. Role-based dashboards",
                        Domain = "Education Technology",
                        Technologies = "ASP.NET Core, Blazor, PostgreSQL",
                        GitHubUrl = "https://github.com/dotnet/aspnetcore",
                        Status = ProposalStatus.CoordinatorApproved,
                        CurrentApprovalLevel = null,
                        ApprovedByHOD = true,
                        ApprovedByCoordinator = true,
                        HODApprovedAt = now.AddDays(-18),
                        CoordinatorApprovedAt = now.AddDays(-17),
                        StudentId = alice.Id,
                        GroupId = g1.Id,
                        SubmittedAt = now.AddDays(-22),
                        UpdatedAt = now.AddDays(-17)
                    },
                    new Proposal
                    {
                        Title = "Campus Data Insights",
                        Abstract = "Build dashboards for academic and operational intelligence.",
                        Objectives = "1. ETL pipelines\n2. Visualization\n3. Decision support",
                        Domain = "Data Science",
                        Technologies = "C#, PostgreSQL, Power BI",
                        GitHubUrl = "https://github.com/dotnet/runtime",
                        Status = ProposalStatus.Submitted,
                        CurrentApprovalLevel = "supervisor",
                        StudentId = diana.Id,
                        GroupId = g2.Id,
                        SubmittedAt = now.AddDays(-1),
                        UpdatedAt = now.AddDays(-1)
                    });
                await db.SaveChangesAsync();

                db.Milestones.AddRange(
                    new Milestone { Title = "Proposal Approved", Description = "Initial sign-off", DueDate = now.AddDays(-15), Status = MilestoneStatus.Completed, ProgressPercent = 100, ProjectId = p1.Id },
                    new Milestone { Title = "Mid Implementation", Description = "Core modules", DueDate = now.AddDays(15), Status = MilestoneStatus.InProgress, ProgressPercent = 55, ProjectId = p1.Id },
                    new Milestone { Title = "Final Demo", Description = "Full system demo", DueDate = now.AddDays(45), Status = MilestoneStatus.Pending, ProgressPercent = 0, ProjectId = p1.Id }
                );
                await db.SaveChangesAsync();

                db.ProjectThreads.Add(new ProjectThread
                {
                    GroupId = g1.Id,
                    ProjectId = p1.Id,
                    CreatedAt = now.AddDays(-16),
                    OverallDeadline = now.AddDays(90),
                    Status = "Active"
                });
                await db.SaveChangesAsync();
            }

            if (!await db.RubricItems.AnyAsync())
            {
                db.RubricItems.AddRange(
                    new RubricItem { Criterion = "Code Quality", MaxMarks = 30, Description = "Code readability and structure" },
                    new RubricItem { Criterion = "Documentation", MaxMarks = 20, Description = "Report and diagrams" },
                    new RubricItem { Criterion = "Presentation", MaxMarks = 20, Description = "Demo and Q&A" }
                );
                await db.SaveChangesAsync();
            }

            if (!await db.Notifications.AnyAsync())
            {
                var admin = await db.Users.FirstAsync(u => u.Role == UserRole.Admin);
                db.Notifications.AddRange(
                    new Notification { Title = "Welcome", Description = "Welcome to the FYP Automation System.", Type = NotificationType.Info, RecipientId = alice.Id, RecipientRole = UserRole.Student.ToString(), EventType = "welcome", ReferenceId = null, LinkUrl = "/student/dashboard", IsRead = false, CreatedAt = now.AddHours(-5), SentAt = now.AddHours(-5), ExpiresAt = now.AddDays(30) },
                    new Notification { Title = "Proposal Approved", Description = "Your proposal reached final approval.", Type = NotificationType.ProposalDecision, RecipientId = alice.Id, RecipientRole = UserRole.Student.ToString(), EventType = "proposal_fully_approved", ReferenceId = "1", LinkUrl = "/student/proposal", IsRead = false, CreatedAt = now.AddHours(-3), SentAt = now.AddHours(-3), ExpiresAt = now.AddDays(30) },
                    new Notification { Title = "System Update", Description = "FYP system is live.", Type = NotificationType.Info, RecipientId = admin.Id, RecipientRole = UserRole.Admin.ToString(), EventType = "system_update", ReferenceId = null, LinkUrl = "/admin/dashboard", IsRead = false, CreatedAt = now, SentAt = now, ExpiresAt = now.AddDays(30) }
                );
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

        private static async Task EnsureRuntimeSchemaAsync(AppDbContext db)
        {
            try
            {
                var provider = db.Database.ProviderName ?? string.Empty;
                var isNpgsql = db.Database.IsNpgsql();
                var isSqlite = provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);

                if (isNpgsql)
                {
                    var sql = new[]
                    {
                        "ALTER TABLE \"Groups\" ADD COLUMN IF NOT EXISTS \"GroupLeadId\" integer NULL;",
                        "ALTER TABLE \"Proposals\" ADD COLUMN IF NOT EXISTS \"Domain\" text NOT NULL DEFAULT '';",
                        "ALTER TABLE \"Proposals\" ADD COLUMN IF NOT EXISTS \"Technologies\" text NOT NULL DEFAULT '';",
                        "ALTER TABLE \"Proposals\" ADD COLUMN IF NOT EXISTS \"GitHubUrl\" text NULL;",
                        "ALTER TABLE \"Proposals\" ADD COLUMN IF NOT EXISTS \"CurrentApprovalLevel\" text NULL;",
                        "ALTER TABLE \"Proposals\" ADD COLUMN IF NOT EXISTS \"DocumentPath\" text NULL;",
                        "ALTER TABLE \"Proposals\" ADD COLUMN IF NOT EXISTS \"DocumentName\" text NULL;",
                        "ALTER TABLE \"Proposals\" ADD COLUMN IF NOT EXISTS \"DocumentUploadedAt\" timestamp with time zone NULL;",
                        "ALTER TABLE \"Proposals\" ADD COLUMN IF NOT EXISTS \"ReviewerComments\" text NULL;",
                        "ALTER TABLE \"Proposals\" ADD COLUMN IF NOT EXISTS \"UpdatedAt\" timestamp with time zone NOT NULL DEFAULT NOW();",
                        "ALTER TABLE \"Milestones\" ADD COLUMN IF NOT EXISTS \"CreatedBySupervisorId\" integer NULL;",
                        "ALTER TABLE \"Milestones\" ADD COLUMN IF NOT EXISTS \"SubmissionFilePath\" text NULL;",
                        "ALTER TABLE \"Milestones\" ADD COLUMN IF NOT EXISTS \"SubmissionFileName\" text NULL;",
                        "ALTER TABLE \"Milestones\" ADD COLUMN IF NOT EXISTS \"SubmissionNotes\" text NULL;",
                        "ALTER TABLE \"Milestones\" ADD COLUMN IF NOT EXISTS \"SubmittedAt\" timestamp with time zone NULL;",
                        "ALTER TABLE \"Milestones\" ADD COLUMN IF NOT EXISTS \"SubmittedByStudentId\" integer NULL;",
                        "ALTER TABLE \"Messages\" ADD COLUMN IF NOT EXISTS \"DeliveredAt\" timestamp with time zone NULL;",
                        "ALTER TABLE \"Messages\" ADD COLUMN IF NOT EXISTS \"ReadAt\" timestamp with time zone NULL;",
                        "ALTER TABLE \"Notifications\" ADD COLUMN IF NOT EXISTS \"RecipientRole\" text NOT NULL DEFAULT '';",
                        "ALTER TABLE \"Notifications\" ADD COLUMN IF NOT EXISTS \"Description\" text NOT NULL DEFAULT '';",
                        "ALTER TABLE \"Notifications\" ADD COLUMN IF NOT EXISTS \"EventType\" text NOT NULL DEFAULT 'general';",
                        "ALTER TABLE \"Notifications\" ADD COLUMN IF NOT EXISTS \"ReferenceId\" text NULL;",
                        "ALTER TABLE \"Notifications\" ADD COLUMN IF NOT EXISTS \"LinkUrl\" text NULL;",
                        "ALTER TABLE \"Notifications\" ADD COLUMN IF NOT EXISTS \"ExpiresAt\" timestamp with time zone NOT NULL DEFAULT NOW() + INTERVAL '30 days';",
                        "UPDATE \"Notifications\" SET \"Description\" = COALESCE(NULLIF(\"Description\", ''), COALESCE(\"Message\", ''));",
                        "CREATE TABLE IF NOT EXISTS \"ProjectThreads\" (\"Id\" SERIAL PRIMARY KEY, \"GroupId\" integer NOT NULL, \"ProjectId\" integer NOT NULL, \"CreatedAt\" timestamp with time zone NOT NULL, \"OverallDeadline\" timestamp with time zone NOT NULL, \"Status\" text NOT NULL);",
                        "CREATE TABLE IF NOT EXISTS \"ProjectTasks\" (\"Id\" SERIAL PRIMARY KEY, \"ThreadId\" integer NOT NULL, \"GroupId\" integer NOT NULL, \"CreatedBySupervisorId\" integer NOT NULL, \"Title\" text NOT NULL, \"Description\" text NOT NULL, \"Deadline\" timestamp with time zone NOT NULL, \"Priority\" text NOT NULL, \"Status\" text NOT NULL, \"CreatedAt\" timestamp with time zone NOT NULL, \"AssignToMemberIdsCsv\" text NULL, \"IsProgressUpdateDemand\" boolean NOT NULL DEFAULT FALSE, \"ResourcePath\" text NULL, \"ResourceName\" text NULL);",
                        "CREATE TABLE IF NOT EXISTS \"TaskSubmissions\" (\"Id\" SERIAL PRIMARY KEY, \"TaskId\" integer NOT NULL, \"SubmittedByStudentId\" integer NOT NULL, \"SubmissionText\" text NOT NULL, \"FilePath\" text NULL, \"FileName\" text NULL, \"SubmittedAt\" timestamp with time zone NOT NULL, \"ReviewStatus\" text NOT NULL, \"Feedback\" text NULL);",
                        "CREATE TABLE IF NOT EXISTS \"RejectionHistories\" (\"Id\" SERIAL PRIMARY KEY, \"ProposalId\" integer NOT NULL, \"GroupId\" integer NOT NULL, \"RejectedByUserId\" integer NOT NULL, \"RejectedByRole\" text NOT NULL, \"RejectionReason\" text NOT NULL, \"RejectedAt\" timestamp with time zone NOT NULL, \"ResubmissionId\" integer NULL, \"ResubmissionImproved\" boolean NULL);",
                        "CREATE TABLE IF NOT EXISTS \"GroupMessageThreads\" (\"Id\" SERIAL PRIMARY KEY, \"GroupId\" integer NOT NULL, \"SupervisorId\" integer NOT NULL, \"CreatedAt\" timestamp with time zone NOT NULL);",
                        "CREATE TABLE IF NOT EXISTS \"GroupMessages\" (\"Id\" SERIAL PRIMARY KEY, \"ThreadId\" integer NOT NULL, \"SenderId\" integer NOT NULL, \"Content\" text NOT NULL, \"SentAt\" timestamp with time zone NOT NULL, \"IsRead\" boolean NOT NULL DEFAULT FALSE);",
                        "CREATE TABLE IF NOT EXISTS \"PersonalMessages\" (\"Id\" SERIAL PRIMARY KEY, \"SenderId\" integer NOT NULL, \"RecipientId\" integer NOT NULL, \"Content\" text NOT NULL, \"SentAt\" timestamp with time zone NOT NULL, \"IsRead\" boolean NOT NULL DEFAULT FALSE);",
                        "CREATE INDEX IF NOT EXISTS \"IX_Notifications_RecipientId_EventType_ReferenceId\" ON \"Notifications\" (\"RecipientId\", \"EventType\", \"ReferenceId\");"
                    };

                    foreach (var statement in sql)
                    {
                        await db.Database.ExecuteSqlRawAsync(statement);
                    }
                }
                else if (isSqlite)
                {
                    var sql = new[]
                    {
                        "ALTER TABLE Groups ADD COLUMN GroupLeadId INTEGER NULL;",
                        "ALTER TABLE Proposals ADD COLUMN Domain TEXT NOT NULL DEFAULT '';",
                        "ALTER TABLE Proposals ADD COLUMN Technologies TEXT NOT NULL DEFAULT '';",
                        "ALTER TABLE Proposals ADD COLUMN GitHubUrl TEXT NULL;",
                        "ALTER TABLE Proposals ADD COLUMN CurrentApprovalLevel TEXT NULL;",
                        "ALTER TABLE Proposals ADD COLUMN DocumentPath TEXT NULL;",
                        "ALTER TABLE Proposals ADD COLUMN DocumentName TEXT NULL;",
                        "ALTER TABLE Proposals ADD COLUMN DocumentUploadedAt TEXT NULL;",
                        "ALTER TABLE Proposals ADD COLUMN ReviewerComments TEXT NULL;",
                        "ALTER TABLE Proposals ADD COLUMN UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP;",
                        "ALTER TABLE Milestones ADD COLUMN CreatedBySupervisorId INTEGER NULL;",
                        "ALTER TABLE Milestones ADD COLUMN SubmissionFilePath TEXT NULL;",
                        "ALTER TABLE Milestones ADD COLUMN SubmissionFileName TEXT NULL;",
                        "ALTER TABLE Milestones ADD COLUMN SubmissionNotes TEXT NULL;",
                        "ALTER TABLE Milestones ADD COLUMN SubmittedAt TEXT NULL;",
                        "ALTER TABLE Milestones ADD COLUMN SubmittedByStudentId INTEGER NULL;",
                        "ALTER TABLE Messages ADD COLUMN DeliveredAt TEXT NULL;",
                        "ALTER TABLE Messages ADD COLUMN ReadAt TEXT NULL;",
                        "ALTER TABLE Notifications ADD COLUMN RecipientRole TEXT NOT NULL DEFAULT '';",
                        "ALTER TABLE Notifications ADD COLUMN Description TEXT NOT NULL DEFAULT '';",
                        "ALTER TABLE Notifications ADD COLUMN EventType TEXT NOT NULL DEFAULT 'general';",
                        "ALTER TABLE Notifications ADD COLUMN ReferenceId TEXT NULL;",
                        "ALTER TABLE Notifications ADD COLUMN LinkUrl TEXT NULL;",
                        "ALTER TABLE Notifications ADD COLUMN ExpiresAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP;",
                        "CREATE TABLE IF NOT EXISTS ProjectThreads (Id INTEGER PRIMARY KEY AUTOINCREMENT, GroupId INTEGER NOT NULL, ProjectId INTEGER NOT NULL, CreatedAt TEXT NOT NULL, OverallDeadline TEXT NOT NULL, Status TEXT NOT NULL);",
                        "CREATE TABLE IF NOT EXISTS ProjectTasks (Id INTEGER PRIMARY KEY AUTOINCREMENT, ThreadId INTEGER NOT NULL, GroupId INTEGER NOT NULL, CreatedBySupervisorId INTEGER NOT NULL, Title TEXT NOT NULL, Description TEXT NOT NULL, Deadline TEXT NOT NULL, Priority TEXT NOT NULL, Status TEXT NOT NULL, CreatedAt TEXT NOT NULL, AssignToMemberIdsCsv TEXT NULL, IsProgressUpdateDemand INTEGER NOT NULL DEFAULT 0, ResourcePath TEXT NULL, ResourceName TEXT NULL);",
                        "CREATE TABLE IF NOT EXISTS TaskSubmissions (Id INTEGER PRIMARY KEY AUTOINCREMENT, TaskId INTEGER NOT NULL, SubmittedByStudentId INTEGER NOT NULL, SubmissionText TEXT NOT NULL, FilePath TEXT NULL, FileName TEXT NULL, SubmittedAt TEXT NOT NULL, ReviewStatus TEXT NOT NULL, Feedback TEXT NULL);",
                        "CREATE TABLE IF NOT EXISTS RejectionHistories (Id INTEGER PRIMARY KEY AUTOINCREMENT, ProposalId INTEGER NOT NULL, GroupId INTEGER NOT NULL, RejectedByUserId INTEGER NOT NULL, RejectedByRole TEXT NOT NULL, RejectionReason TEXT NOT NULL, RejectedAt TEXT NOT NULL, ResubmissionId INTEGER NULL, ResubmissionImproved INTEGER NULL);",
                        "CREATE TABLE IF NOT EXISTS GroupMessageThreads (Id INTEGER PRIMARY KEY AUTOINCREMENT, GroupId INTEGER NOT NULL, SupervisorId INTEGER NOT NULL, CreatedAt TEXT NOT NULL);",
                        "CREATE TABLE IF NOT EXISTS GroupMessages (Id INTEGER PRIMARY KEY AUTOINCREMENT, ThreadId INTEGER NOT NULL, SenderId INTEGER NOT NULL, Content TEXT NOT NULL, SentAt TEXT NOT NULL, IsRead INTEGER NOT NULL DEFAULT 0);",
                        "CREATE TABLE IF NOT EXISTS PersonalMessages (Id INTEGER PRIMARY KEY AUTOINCREMENT, SenderId INTEGER NOT NULL, RecipientId INTEGER NOT NULL, Content TEXT NOT NULL, SentAt TEXT NOT NULL, IsRead INTEGER NOT NULL DEFAULT 0);",
                        "CREATE INDEX IF NOT EXISTS IX_Notifications_RecipientId_EventType_ReferenceId ON Notifications (RecipientId, EventType, ReferenceId);"
                    };

                    foreach (var statement in sql)
                    {
                        try { await db.Database.ExecuteSqlRawAsync(statement); } catch { }
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
