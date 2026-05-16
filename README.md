# FYP Automation App

A Blazor Server-based Final Year Project automation system built on .NET 10 with PostgreSQL (Supabase) and EF Core.

## Overview

This application manages the full FYP lifecycle for students, supervisors, administrators, and panel members. It provides authentication, project proposals, milestone tracking, document submission, evaluation, notifications, messaging, viva scheduling, and audit logging.

## Technologies

- .NET 10 / ASP.NET Core
- Blazor Server (interactive server components)
- Entity Framework Core
- PostgreSQL via Npgsql
- Supabase-hosted Postgres
- Cookie authentication and role-based access

## Key Features

- Multi-role authentication: Admin, Supervisor, Student, HOD, Coordinator, Panel
- Project proposals and approval workflow
- Milestone creation and progress tracking
- Document upload and submission review
- Evaluation rubrics and scoring
- Viva scheduling and panel management
- Notifications, messaging, and audit logs
- Automatic database creation and seed data on first run

## Prerequisites

- .NET SDK 10.0
- Access to a Supabase project with a PostgreSQL database
- Recommended: Visual Studio 2022/2023 or Visual Studio Code

## Setup

1. Clone the repository.
2. Install dependencies and build the project:

   ```bash
   dotnet restore
   dotnet build
   ```

3. Configure the Supabase connection:
   - Option A: Update `appsettings.json` `ConnectionStrings:DefaultConnection` with your Supabase connection string.
   - Option B (recommended): Set environment variable `SUPABASE_CONNECTION`.

   Example:

   ```powershell
   $env:SUPABASE_CONNECTION = "Host=...;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<your-password>;SSL Mode=Require;Trust Server Certificate=true;Pooling=true"
   ```

4. Run the application:

   ```bash
   dotnet run
   ```

5. Open the app in your browser at the URL shown by `dotnet run`.

## Configuration

`Program.cs` reads the Supabase connection from the environment variable `SUPABASE_CONNECTION` first, then falls back to `appsettings.json`.

### Important

- Do not commit real credentials, API keys, or database passwords to source control.
- The repository currently includes sample Supabase settings in `appsettings.json`; replace them with your own values or override them using environment variables.

## Seed Data

On first launch, the app automatically creates the database schema and seeds sample data, including default users and sample records.

## Project Structure

- `Program.cs` - application startup, Supabase configuration, authentication, routing
- `FYP_AutomationSystem.csproj` - project dependencies and SDK settings
- `Data/` - EF Core DbContext and seed data
- `Models/` - domain entities
- `Services/` - business logic services for users, auth, proposals, notifications, etc.
- `Components/` and `Pages/` - Blazor UI components and pages
- `wwwroot/` - static assets and client-side files

## Running in Development

Use the same command:

```bash
dotnet run
```

If you want to debug or modify the application, open the solution file `FYP_AutomationSystem.sln` in Visual Studio or VS Code.

## Notes

- The app is configured to ignore missing prune package data for .NET 10 via `AllowMissingPrunePackageData` in the project file.
- If you need schema migrations in the future, use EF Core migrations with `dotnet ef migrations add` and `dotnet ef database update`.
