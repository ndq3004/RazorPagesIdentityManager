# RazorPageIdentityManager

## Project Overview

RazorPageIdentityManager is an ASP.NET Core 8 Razor Pages application that integrates:

- ASP.NET Core Identity for user authentication and authorization
- Entity Framework Core for data access
- SQL Server LocalDB for development storage

The project is designed as a learning-friendly template for adding Identity into a Razor Pages app, with built-in role support (`Admin`, `Manager`, `Staff`) and the default Identity UI under `Areas/Identity`.

## How It Works

At startup, the app configures core services in `Program.cs`:

1. Registers `AppDbContext` with SQL Server using `DefaultConnection` from `appsettings.json`.
2. Registers Identity with `ApplicationUser` and role support.
3. Enables Razor Pages and maps Razor endpoints.
4. On application start, runs role seeding (`SeedData.SeedRolesAsync`) to ensure base roles exist.

### Authentication and Authorization Flow

- Identity handles registration, login, logout, and account management using scaffolded pages in `Areas/Identity/Pages/Account`.
- User data is stored in Identity tables managed by EF Core migrations.
- Role-based authorization is enabled by `.AddRoles<IdentityRole>()`, so you can later protect pages by role.

### Data Layer

- `AppDbContext` inherits from `IdentityDbContext<ApplicationUser>`.
- This means Identity tables and your custom entities can live in one database context.
- Entity configurations are applied in `OnModelCreating` (for example, `ProductConfiguration`).

## Prerequisites

- .NET 8 SDK
- SQL Server LocalDB (default with Visual Studio) or an available SQL Server instance

## Commands To Start The Project

Run these commands from the solution root:

```powershell
cd RazorPageIdentityManager
dotnet restore
dotnet ef database update
dotnet run
```

If `dotnet ef` is not available, install the EF CLI tool first:

```powershell
dotnet tool install --global dotnet-ef
```

After running, open one of these URLs (based on launch profile):

- https://localhost:7203
- http://localhost:5275

## Notes

- Default connection string points to LocalDB:
	- `Server=(localdb)\\mssqllocaldb;Database=RazorPageIdentityManager;Trusted_Connection=True;MultipleActiveResultSets=true`
- In Production, use a secure SQL Server connection string and configure Identity options (password policy, confirmation flow, etc.) accordingly.
