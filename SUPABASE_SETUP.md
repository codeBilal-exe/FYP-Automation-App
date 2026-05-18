# Supabase Integration — FYP Automation System

The project has been migrated from SQLite to **PostgreSQL (Supabase)** using
the `Npgsql.EntityFrameworkCore.PostgreSQL` provider. All EF Core code,
models, services, seed data, and `OnModelCreating` mappings are unchanged —
only the database provider was swapped. Tables are created automatically by
`db.Database.EnsureCreated()` in `Program.cs` on first run.

## 1. Get your Supabase connection string

1. Open your Supabase project (`FYP-Automation`).
2. Go to **Project Settings → Database → Connection string**.
3. Copy the **Session pooler** string (port `5432`) — it works best with
   long-lived connections from a Blazor Server app. Transaction pooler
   (`6543`) also works for short queries.
4. Note the **DB password** you set when creating the project (Supabase shows
   `[YOUR-PASSWORD]` as a placeholder — you must substitute it).

A typical Supabase connection string converted to **Npgsql / ADO.NET format**
looks like this:

```
Host=aws-0-eu-west-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.abcdefghijklmnop;Password=YOUR_DB_PASSWORD;SSL Mode=Require;Trust Server Certificate=true;Pooling=true
```

- `Host` — from the Supabase pooler URL.
- `Username` — `postgres.<your-project-ref>` (project ref is the random
  string in your Supabase URL, e.g. `abcdefghijklmnop`).
- `Password` — your Supabase DB password.
- `SSL Mode=Require` is mandatory — Supabase enforces TLS.

## 2. Configure the app

Pick **one** of the following:

### Option A — appsettings.json (simplest)

Edit `appsettings.json` and replace the `DefaultConnection` value with your
real connection string from step 1.

### Option B — environment variable (recommended for production)

Leave `appsettings.json` untouched and set:

```bash
# Linux / macOS
export SUPABASE_CONNECTION="Host=...;Port=5432;Database=postgres;Username=postgres.xxx;Password=...;SSL Mode=Require;Trust Server Certificate=true"

# Windows (PowerShell)
$env:SUPABASE_CONNECTION="Host=...;Port=5432;Database=postgres;Username=postgres.xxx;Password=...;SSL Mode=Require;Trust Server Certificate=true"
```

`Program.cs` reads `SUPABASE_CONNECTION` first, then falls back to
`appsettings.json`.

## 3. Run

```bash
dotnet restore
dotnet run
```

On first launch the app will:

1. Connect to your Supabase Postgres database.
2. Create every table (`Users`, `Groups`, `Projects`, `Proposals`,
   `Milestones`, `Documents`, `Evaluations`, `RubricItems`, `RubricScores`,
   `Notifications`, `VivaSlots`, `AuditLogs`,
   `PlagiarismReports`, plus the `GroupMembers` and `VivaPanelMembers`
   join tables) via `EnsureCreated()`.
3. Seed default users, a sample group, project, milestones, proposal, and
   notifications.

## 4. Default seeded users

| Role        | Email                  | Password     |
|-------------|------------------------|--------------|
| Admin       | admin@fyp.edu          | Admin@123    |
| Supervisor  | supervisor1@fyp.edu    | Super@123    |
| Student     | student1@fyp.edu       | Student@123  |
| Student     | student2@fyp.edu       | Student@123  |
| HOD         | hod@fyp.edu            | HOD@123      |
| Coordinator | coordinator@fyp.edu    | Coord@123    |
| Panel       | panel@fyp.edu          | Panel@123    |

## Notes

- All `DateTime` fields are stored as `timestamp with time zone` and the code
  already uses `DateTime.UtcNow` everywhere — no Npgsql UTC compatibility
  issues.
- Enums (`UserRole`, `MilestoneStatus`, etc.) are stored as `integer`
  columns by default — no extra mapping needed.
- If you later want versioned schema migrations instead of `EnsureCreated()`,
  run `dotnet ef migrations add Initial` and `dotnet ef database update`.
