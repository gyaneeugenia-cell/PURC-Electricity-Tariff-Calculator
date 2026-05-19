# Python to C# Logic Mapping

This note explains how the existing Python reckoner maps into the new C# structure.

## Current Python flow

The existing app follows this path:

1. `app.py` renders the Streamlit interface
2. The user selects:
   - year
   - tariff period
   - category
   - mode (`Consumption` or `Total bill`)
3. `app.py` dispatches to a year-specific calculator such as:
   - `residential_2024_db`
   - `non_residential_2024_db`
   - `slt_2024_db`
4. Each calculator:
   - fetches tariff components from PostgreSQL
   - fetches tax
   - fetches levies
   - computes the bill
5. Reverse mode brute-forces kWh until the bill matches
6. Historic trend repeats the same forward calculation across multiple years

## New C# flow

The C# migration keeps the same business flow but separates it into clearer layers:

1. `Controllers/`
   - receive HTTP requests
2. `PostgresTariffRepository`
   - gets tariff rows from Supabase PostgreSQL
3. `TariffCalculationService`
   - applies the tariff formulas
4. JSON response or future UI
   - displays results, trends, and exports

## Formula families found in the Python engine

The Python code does not really have 20 unrelated algorithms. It mostly repeats a small number of patterns:

- `1998 / 2001 residential fixed-charge style`
  - fixed charge or base charge
  - energy only after a threshold
  - special levy handling in 1998
- `2002 to 2021 residential banded style`
  - lifeline band
  - standard service charge after the lifeline threshold
  - higher bands after 300 and 600 kWh
- `2022 / 2023 residential revised banding`
  - 0 to 30
  - 31 to 300
  - 301 to 600
  - above 600
- `2024 residential`
  - 0 to 30
  - 31 to 300
  - above 300
- `2025 / 2026 residential`
  - 0 to 30
  - 0 to 300
  - above 300
- `Non-residential`
  - service charge
  - incremental energy blocks
  - levies and tax
- `SLT`
  - service charge by level
  - optional demand charge by level
  - energy rate by level
  - levies and tax

## Why the C# engine uses a central service

Your supervisors were right that if the algorithm is correct, it should be translatable into another language.

That is exactly what this C# version is doing:

- the database remains the same
- the algorithm stays the same
- only the implementation language changes

The C# service is therefore based on:

- database category id resolution
- period-to-year-id resolution
- category rule parsing
- one reusable forward calculator
- one reusable reverse search
- one reusable historic trend generator

## Supervisor requirements already considered in the new structure

- Admin can create/update/delete users
- Admin can assign privileges
- Tariff edits can be audited
- Deleted or modified tariff rows can be traced by user and time
- Historic trend remains part of the application
- The system remains deployable over the web

## Main migration caution

The current Python app contains UI behavior, business rules, and database concerns in a single large file plus many repeated engine files.

The C# migration should keep separating them:

- UI layer
- service layer
- repository layer
- audit/admin layer

That separation will make your defense explanation much stronger because you can explain both:

- the algorithmic logic
- the system architecture
