# FYP Automation App

FYP Automation App is a role-based Final Year Project management platform built with **ASP.NET Core (.NET 10)** and **Blazor Interactive Server**. It manages the complete FYP lifecycle: proposal workflow, group/project tracking, milestones, viva scheduling, evaluations, reporting, notifications, and audit logs.

## What This Project Does

- Centralizes FYP operations for **Student, Supervisor, HOD, Coordinator, Admin, and Panel** roles.
- Enforces role-specific workflows with cookie auth + authorization.
- Uses **Supabase PostgreSQL** (via EF Core + Npgsql) as the primary data store.
- Supports operational workflows like:
  - team-lead proposal submission,
  - multi-stage approvals,
  - automatic project/thread activation,
  - timetable-aware viva scheduling,
  - milestone and evaluation tracking,
  - report generation and archival.

## Architecture Snapshot

- **Frontend/UI**: Blazor components under `Components/`
- **App Host**: ASP.NET Core Web app (`Program.cs`)
- **Business Layer**: services under `Services/`
- **Data Layer**: EF Core context + entities under `Data/` and `Models/`
- **DB**: Supabase PostgreSQL
- **Auth**: cookie-based authentication + role authorization

## Role Modules

Based on current routing/navigation:

- **Student**
  - Dashboard, proposal submission, milestones, documents, group, result pages
- **Supervisor**
  - Proposal review, milestone assignment, progress tracking, evaluations, group oversight, rejection history
- **HOD**
  - Dashboard, proposal review stage, rejection history, reports
- **Coordinator**
  - Dashboard, announcements, group management, final proposal approval, viva scheduling, reports
- **Admin**
  - Dashboard, users, audit logs, announcements
- **Panel**
  - Dashboard, schedule, evaluations

## Core Workflows

### 1) Proposal Pipeline

- Proposal submission/edit is **team-lead only** within a group.
- Approval path:
  1. Supervisor
  2. HOD
  3. Coordinator (final)
- On final approval:
  - project is created/activated,
  - project thread is initialized,
  - group notifications are sent.

### 2) Group + Project Threading

- Groups map to supervisors and members.
- Approved proposals can auto-populate project metadata.
- Project thread/tasks/submissions enable supervisor-student execution tracking.

### 3) Milestones

- Supervisor and scheduling flows create/manage milestones.
- Students can submit milestone evidence.
- Deadlines/status updates are tracked with notifications.

### 4) Viva Scheduling (Coordinator/Admin route)

Current scheduling logic includes:

- date/time/venue scheduling with slot type support,
- supervisor-first availability generation,
- timetable-aware conflict checks,
- overlap prevention for:
  - venue,
  - group,
  - supervisor,
  - selected panel members.

### 5) Evaluations & Results

- Supervisor and panel evaluation flows
- Rubric scoring support
- Result aggregation per student/project context

### 6) Notifications & Announcements

- Per-user notifications with unread/read state and deduping
- Group-level proposal status notifications
- Admin/Coordinator announcement broadcasting

### 7) Reporting & Audit

- Audit logs for key actions
- Semester/department report generation and archival
- Aggregated statistics endpoints/service methods for dashboards

## Tech Stack

- **.NET 10**
- **ASP.NET Core Web + Blazor Interactive Server**
- **Entity Framework Core 10**
- **Npgsql PostgreSQL provider**
- **Supabase PostgreSQL**
- **ClosedXML** (Excel import)
- **QuestPDF** (report outputs)

## Project Structure

- `Program.cs` - DI, auth, middleware, startup tasks, auth endpoints
- `Components/` - pages, layouts, shared components, routing
- `Services/` - business logic (proposal, viva, evaluation, reporting, etc.)
- `Data/AppDbContext.cs` - EF model + relational mapping
- `Models/` - entities, DTOs, enums
- `Migrations/` - EF migrations history
- `wwwroot/` - static assets/uploads
- `Tools/DbResetImport/` - utility tool (excluded from main app compile)
- `fyp_mock_users.csv` - login demo credential source

## Local Setup

### Prerequisites

- .NET SDK **10.x**
- Supabase project (PostgreSQL)

### 1) Restore and Build

```bash
dotnet restore
dotnet build
```

### 2) Configure Database Connection

Preferred via environment variable:

```bash
export SUPABASE_CONNECTION='Host=...;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true;Pooling=true'
```

Fallback: `appsettings.json` -> `ConnectionStrings:DefaultConnection`.

### 3) Optional Startup Task

To sync existing app users to Supabase auth at startup:

```bash
export StartupTasks__SyncSupabaseAuth=true
```

### 4) Run

```bash
dotnet run
```

App default in development: `http://localhost:5280` (see `launchSettings.json` / runtime logs).

## Runtime Notes

- Startup uses `db.Database.EnsureCreatedAsync()`.
- App has a safe SQL startup patch for `Groups.RepoLink` (`ALTER TABLE ... ADD COLUMN IF NOT EXISTS`).
- Authentication uses app cookie `FYP_AutomationSystem.Auth` with sliding expiration.
- Auth endpoints are mapped in `Program.cs`:
  - `/auth/login`
  - `/auth/logout`
  - `/auth/forgot-password`
  - `/auth/reset-password`
  - `/auth/reset-password-supabase`

## Demo Credentials

- Login page demo pills are sourced from `fyp_mock_users.csv`.
- Use `Tools/DbResetImport` only when you intentionally want to reset/import mock users.

## Deployment Notes

This project is **server-hosted Blazor**, not standalone static WASM output, so deploy it to a .NET-capable host (e.g., Azure App Service).

For Azure/App Service, ensure these app settings are configured:

- `SUPABASE_CONNECTION`
- `Supabase__Url`
- `Supabase__AnonKey`
- `Supabase__ServiceRoleKey` (if your flows use it)
- SMTP settings if password reset email is enabled

## Security Checklist

- Do not commit real DB passwords/API keys/service-role keys.
- Use environment variables or platform secrets in hosted environments.
- Rotate exposed credentials immediately if they were ever committed.

## Troubleshooting

- **Connection string/host/password issues**: check `SUPABASE_CONNECTION` format and credentials.
- **PostgreSQL UTC errors**: normalize date/timestamp writes/queries to UTC for `timestamptz` columns.
- **Scheduling conflicts**: validate faculty timetable rows and existing overlapping viva slots.
- **Missing/incorrect join-table column names**: verify `VivaPanelMembers` mapping in `Data/AppDbContext.cs` matches actual DB schema.

---

If you are onboarding, start with:

1. `Program.cs` (startup/auth flow)
2. `Data/AppDbContext.cs` (schema/mappings)
3. `Services/ProposalService.cs` and `Services/VivaService.cs` (core workflows)
4. `Components/Layout/NavMenu.razor` + role pages in `Components/Pages/`
