# PURC Electricity Tariff Calculator

PURC Electricity Tariff Calculator is an ASP.NET Core web application for electricity tariff analysis, bill reckoning, tariff lookup, historic comparison, and admin-managed tariff maintenance.

## Overview

The application is designed to help users:

- calculate electricity bills from consumption input
- reverse-calculate estimated consumption from a target bill
- view tariff rates by year, effective period, and customer category
- review historic tariff information across multiple periods
- print bill breakdowns for reporting and record keeping
- manage tariff, tax, and levy records through an admin dashboard

## Key Features

- User authentication and role-based access
- Tariff calculation for:
  - Residential
  - Non-Residential
  - SLT
  - EV Charging
- Tariff rate display with category-specific visual presentation
- Bill breakdown generation and print-ready output
- Historic tariff analytics and comparison views
- Admin dashboard for:
  - user creation and management
  - password management
  - tariff database updates
  - tax and levy updates
- PostgreSQL-backed storage with Supabase connectivity
- Built-in AI assistant (floating chat widget) powered by Google Gemini's free tier — answers "how do I…" questions about the app and explains the calculation currently on screen

## Technology Stack

- ASP.NET Core MVC
- Razor Views
- C#
- PostgreSQL / Supabase
- `Npgsql`
- HTML, CSS, and JavaScript

## Project Structure

- `Controllers/`:
  MVC controllers for authentication, calculation, catalog access, trends, and admin actions
- `Models/`:
  request models, view models, response models, and tariff record types
- `Services/`:
  core business logic, database repositories, password security, chart layout, and tariff calculation services
- `Views/`:
  Razor UI pages for users and administrators
- `wwwroot/`:
  static assets including stylesheets, scripts, and images
- `Sql/`:
  database setup and supporting SQL scripts
- `docs/`:
  supporting implementation notes and technical references

## Local Setup

### Prerequisites

- .NET 8 SDK
- Access to the PostgreSQL / Supabase database used by the application

### Configuration

1. Copy `appsettings.Development.example.json` to `appsettings.Development.json`.
2. Add your live PostgreSQL connection string to `appsettings.Development.json`.

Example:

```json
{
  "ConnectionStrings": {
    "SupabasePostgres": "Host=your-supabase-host;Port=5432;Database=postgres;Username=your-username;Password=your-password;SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

### AI Assistant (optional, free tier)

The app includes a floating AI assistant that answers questions about how the reckoner works and can explain the calculation currently on screen. It is powered by [Google Gemini](https://aistudio.google.com/apikey), which has a free tier — no card required.

To enable it:

1. Get a free API key from https://aistudio.google.com/apikey.
2. Set it either as an environment variable (recommended for deployment):
   ```
   GEMINI_API_KEY=your-key-here
   ```
   or in `appsettings.Development.json` for local development:
   ```json
   {
     "AiAssistant": {
       "GeminiApiKey": "your-key-here",
       "GeminiModel": "gemini-2.5-flash"
     }
   }
   ```

If no key is set, the assistant widget still appears but replies that it hasn't been enabled yet — the rest of the application is unaffected.

## Running the Application

```powershell
dotnet restore
dotnet build
dotnet run
```

After the application starts, open the local URL shown in the terminal.

## Security

- `appsettings.Development.json` is kept out of source control and should remain local to your machine.
- Do not commit live database credentials or other secrets to the repository.

## Repository Purpose

This repository contains the main application code for the PURC Electricity Tariff Calculator, including the user-facing interface, admin tools, tariff calculation engine, and database integration.
