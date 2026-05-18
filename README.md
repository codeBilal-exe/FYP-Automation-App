# FYP Automation App

A Blazor Server Final Year Project automation system built on .NET 10, EF Core, and Supabase PostgreSQL.

## Overview

This app manages the FYP lifecycle for students, supervisors, HOD, coordinator, admin, and panel roles.

## Core Workflows

- Team-lead-only proposal submission/editing (group members can view workflow/status).
- Group-synced proposal approvals (supervisor -> HOD -> coordinator) for all members.
- Automatic project creation/activation when coordinator approves a proposal.
- Group-synced milestones for project work, viva, presentation, evaluation, and document submission schedules.
- Viva scheduling with faculty availability checks and panel assignment.

## Technologies

- .NET 10 / ASP.NET Core
- Blazor Server (interactive server)
- Entity Framework Core + Npgsql
- Supabase PostgreSQL
- Cookie authentication + role-based authorization

## Prerequisites

- .NET SDK 10+
- Supabase project with PostgreSQL

## Setup

1. Clone the repository.
2. Restore and build:

```bash
dotnet restore
dotnet build
```

3. Configure database connection (recommended via env var):

```powershell
$env:SUPABASE_CONNECTION = "Host=...;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true;Pooling=true"
```

`Program.cs` reads `SUPABASE_CONNECTION` first, then `ConnectionStrings:DefaultConnection` from `appsettings.json`.

4. Run:

```bash
dotnet run
```

## Configuration Notes

- On startup, schema is created/validated using `EnsureCreatedAsync()`.
- Optional startup task still available:
  - `StartupTasks:SyncSupabaseAuth` -> sync existing app users to Supabase Auth.
- `Data/SeedData.cs` is removed; demo seed is not run automatically.

## Demo Credentials

- Login page demo pills read from `fyp_mock_users.csv` (one credential per role).
- Use `Tools/DbResetImport` only if you intentionally want to reset/import users to database.

## Project Structure

- `Program.cs` - startup, DB/auth wiring, endpoint mapping
- `Data/` - EF Core context
- `Models/` - entities and enums
- `Services/` - business logic
- `Components/` - Blazor pages/layout/shared UI
- `wwwroot/` - static assets

## Security Reminder

- Do not commit real passwords, API keys, or Supabase service-role keys.
- Prefer environment variables/secrets for production.
