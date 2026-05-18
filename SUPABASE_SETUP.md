# Supabase Setup (FYP Automation)

This app uses Supabase PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL`.

## 1) Get Connection String

From Supabase Dashboard:

- `Project Settings -> Database -> Connection string`
- Use **Session pooler** (`5432`) for Blazor Server workloads.

Example ADO.NET/Npgsql format:

```text
Host=aws-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<db-password>;SSL Mode=Require;Trust Server Certificate=true;Pooling=true
```

## 2) Configure App

Preferred: environment variable

```powershell
$env:SUPABASE_CONNECTION="Host=...;Port=5432;Database=postgres;Username=postgres.<ref>;Password=...;SSL Mode=Require;Trust Server Certificate=true;Pooling=true"
```

Fallback: `appsettings.json -> ConnectionStrings:DefaultConnection`.

## 3) Run

```bash
dotnet restore
dotnet run
```

Startup behavior:

- Connects to Supabase PostgreSQL.
- Runs `db.Database.EnsureCreatedAsync()`.
- If enabled, runs `StartupTasks:SyncSupabaseAuth`.

## 4) Current Data/Seeding Behavior

- `Data/SeedData.cs` is removed.
- No automatic demo seeding runs at startup.
- Login demo credential buttons are sourced from `fyp_mock_users.csv`.

## 5) Proposal/Project/Milestone Sync Behavior

- Proposal submission/edit is team-lead-only.
- Approvals are group-synced for all members.
- Coordinator approval auto-creates/activates project from proposal title/details.
- Scheduling (viva/presentation/evaluation/document submission) auto-creates group milestone entries.

## 6) Troubleshooting

- **Auth sync disabled:** set `StartupTasks:SyncSupabaseAuth=true`.
- **Connection failures:** verify host/user/password/SSL settings.
- **Schema mismatch:** confirm app starts with correct Supabase DB and role permissions.
