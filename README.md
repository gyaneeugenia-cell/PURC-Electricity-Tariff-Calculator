# PURC Historic Tariff Reckoner C# Migration

This folder contains the first C# migration foundation for your Python-based historic tariff reckoner.

## What this version already sets up

- ASP.NET Core backend project structure
- Direct PostgreSQL access for Supabase using `Npgsql`
- Typed tariff models for components, tax, levies, period catalog, results, and historic trend rows
- A reusable C# tariff calculation service for:
  - Residential forward calculation
  - Non-residential forward calculation
  - SLT forward calculation
  - Reverse bill-to-kWh search
  - Historic trend generation
- API endpoints for:
  - period catalog
  - category options
  - calculation
  - historic trend
- SQL starter for:
  - admin users
  - privileges
  - audit trail on tariff changes

## Why this migration is cleaner than the Python layout

Your current Python app uses many year-specific files such as `tariff_2024_db.py`, `tariff_2025_db.py`, and so on. The C# version starts from a more centralized engine:

- The database still remains Supabase PostgreSQL
- The calculator still uses the same tariff tables
- The engine now applies recurring rule patterns instead of copying the whole calculator per year

That means new gazetted tariffs can be added with less code duplication.

## Current architecture

- `Controllers/`
  - HTTP endpoints for catalog, calculation, and trends
- `Models/`
  - request/response and database row models
- `Services/PostgresTariffRepository.cs`
  - fetches tariff rows, tax rows, levies, periods, and component types
- `Services/TariffCalculationService.cs`
  - contains the translated calculation logic from Python into C#
- `Sql/001_admin_security_audit.sql`
  - creates the user management and audit structures requested in the meeting

## Features from the meeting transcript mapped into this migration

- Maintain Supabase PostgreSQL:
  - kept; the new code still targets PostgreSQL directly
- Change programming language to C#:
  - started here with an ASP.NET Core backend
- Preserve existing tariff calculation logic:
  - the C# service mirrors the current Python forward and reverse calculation flow
- Historic trend:
  - included as a service plus API endpoint
- Admin mode:
  - database tables and audit triggers are provided as the first step
- User creation / deletion / privileges:
  - schema included in the SQL starter
- Track edits, deletions, timestamps, and actors:
  - implemented through `tariff_change_audit`
- Print / export / view:
  - should be implemented next in the C# UI layer or reporting endpoints

## Important notes

- `appsettings.json` is kept safe for source control and does not store the live database password.
- Create a local `appsettings.Development.json` from `appsettings.Development.example.json` and paste your real Supabase PostgreSQL connection string there before running locally.
- The Python app appears to be stable for Residential, Non-Residential, and SLT paths.
- The `EV-CHARGING` path in the Python app looks only partially wired; the C# service currently marks that path as pending.

## How to continue once the .NET SDK is available

1. Copy `appsettings.Development.example.json` to `appsettings.Development.json`.
2. Paste the real PostgreSQL connection string into `appsettings.Development.json`.
3. Open this folder in Visual Studio or VS Code.
4. Run:

```powershell
dotnet restore
dotnet build
dotnet run
```

5. Test the endpoints:
  - `GET /api/catalog/periods`
  - `GET /api/catalog/categories?yearId=57&period=Q2%202026%20(April)`
  - `POST /api/calculator/calculate`
  - `POST /api/trends/historic`

## Recommended next implementation steps

1. Add a Razor Pages or Blazor UI so the C# version can replace the Streamlit interface.
2. Add login, password hashing, and admin authorization.
3. Add tariff maintenance screens for create/update/delete.
4. Add CSV/PNG/JPEG/PDF export endpoints.
5. Add AI analytics as a separate service once the core migration is stable.
