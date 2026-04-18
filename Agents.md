# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Project Overview

Prowo is a Blazor WebAssembly application for managing school events at the last school week (School Week 2026, HTLVB - Higher Technical School Lower Austria). Students can create, register for, and manage events/projects. The application uses Keycloak for OAuth2/OIDC authentication and PostgreSQL for data storage.

# Architecture

## Project Structure

```
Prowo.sln
├── Prowo.WebAsm/             # Blazor WASM project (client-server structure)
│   ├── Client/               # Blazor frontend
│   ├── Server/               # ASP.NET Core server API + Razor pages
│   └── Shared/               # Shared DTOs and converters
├── Keycloak.AdminApi/        # Auto-generated Keycloak Admin API client
├── Prowo.Console/            # Console application for CLI tools
├── Prowo.ImportProjectsFromExcel/  # Tool for importing projects from Excel
├── Prowo.ImportProjectsFromMssql/  # Tool for importing from MSSQL
├── Prowo.SampleDataGenerator/     # Test data generator
└── Prowo.WebAsm.Server.IntegrationTests/  # Integration tests
```

## Core Components

### 1. Authentication (Keycloak OIDC)
- Server uses JWT Bearer authentication via Keycloak
- Client uses OidcAuthentication for WebAssembly
- Configuration in `appsettings.json`:
  - `Oidc.Authority`: Keycloak server URL
  - `Oidc.Audience`: "prowo"
  - `Keycloak.BaseUrl`: Keycloak admin URL
  - `Keycloak.RealmName`: "htlvb"

### 2. Data Storage
- PostgreSQL database with two tables:
  - `project`: Events with organizer info (JSON), dates, attendees limit
  - `registration_event`: Registration history (JSON user data)
- `IProjectStore` interface with implementations:
  - `PgsqlProjectStore` (production)
  - In-memory implementations (tests)

### 3. Authorization
- Role-based access controlled via Keycloak roles:
  - `all-projects-editor`: Full access
  - `project-creator`: Can create events
  - `report-viewer`: Can view attendee reports
  - `project-attendee`: Can register for events
- Users mapped to Keycloak groups: `Lehrer` (teachers), `Schueler` (students)
- Authorization policies check: `CreateProject`, `UpdateProject`, `DeleteProject`, `CreateReport`, `AttendProject`

### 4. Registration Strategy
- `IRegistrationStrategy` with logical combination of:
  - `NoRegistrationAfterClosingDateStrategy`
  - `NoRegistrationIfRegisteredStrategy`
  - `NoWaitingListStrategy`
  - `SingleRegistrationPerDayStrategy`
- `NoWaitingListStrategy` means only first `MaxAttendees` registrations count

# Development Workflow

## Building

```bash
# Initialize (first time only)
./init.sh

# Build tailwind CSS (first time only)
./build-tailwind.sh

# Run tests
dotnet test

# Build and run
dotnet run --project Prowo.WebAsm/Server/Prowo.WebAsm.Server.csproj
```

## Running

### Development
```bash
# Start server (requires PostgreSQL running)
dotnet run --project Prowo.WebAsm/Server/Prowo.WebAsm.Server.csproj

# Open browser to https://localhost:7206
```

### Docker
```bash
# Start PostgreSQL
docker compose up -d db

# Access database at localhost:5432 (adminer at localhost:8081)
```

### Docker Compose Full Setup
```bash
docker compose up
```

## Key Commands

```bash
# Run all tests
dotnet test

# Run integration tests only
dotnet test Prowo.WebAsm.Server.IntegrationTests/Prowo.WebAsm.Server.IntegrationTests.csproj

# Run a single test
dotnet test --filter "FullyQualifiedName~RegistrationTests"

# Build
dotnet build
dotnet build -c Release

# Run tailwind build
./build-tailwind.sh
```

## Database Setup

### Schema (auto-applied on container start)
```bash
docker compose up db
```

The `db-schema.sql` is automatically applied to the PostgreSQL container on startup.

### Migrations
This project does not use traditional migrations. Database is re-created from schema on container restart.

# Configuration

## appsettings.json (Server)
```json
{
  "Oidc": {
    "Authority": "https://id.htlvb.at/realms/htlvb",
    "Audience": "prowo",
    "TokenValidationParameters": {
      "ValidateAudience": false
    }
  },
  "Keycloak": {
    "BaseUrl": "https://id.htlvb.at/",
    "RealmName": "htlvb"
  },
  "UserStore": {
    "OrganizerGroupId": "6c766d94-3dec-4cf5-94f7-b327b40c56b2",
    "AttendeeGroupId": "3d6bfb52-6e94-4439-bff3-0813a500963a",
    "IncludedClasses": "^[0-9]+[A-Z](H|F)"
  },
  "ConnectionStrings": {
    "Pgsql": "Server=localhost;Database=prowo;User Id=prowo;Password=prowo;"
  }
}
```

## Keycloak Client Setup

Use `create-keycloak-client.sh` to create the OIDC client in Keycloak. This script:
- Creates a "prowo" client in the "htlvb" realm
- Adds roles: all-projects-editor, project-creator, report-viewer, project-attendee
- Assigns roles to users/groups (eggj, hoed, prai, Lehrer, Schueler)

# Data Models

## Project
- Title, Description, Location
- Organizer (JSON) + CoOrganizers array
- Date, StartTime, EndTime, ClosingDate
- MaxAttendees
- Registration history (from events)

## UserStore
- Uses Keycloak to get users from groups
- Filters by class regex pattern
- `OrganizerGroupId` and `AttendeeGroupId` map Keycloak groups to app roles

## Keycloak Roles → App Roles
| Keycloak Role | App Role | Permissions |
|---------------|----------|-------------|
| all-projects-editor | Editor | Full CRUD |
| project-creator | Creator | Create events |
| report-viewer | Viewer | View reports |
| project-attendee | Attendee | Register/deregister |

# Testing

```bash
# Run all tests
dotnet test

# Run integration tests
dotnet test --project Prowo.WebAsm.Server.IntegrationTests/Prowo.WebAsm.Server.IntegrationTests.csproj

# Run a specific test file
dotnet test --filter "FullyQualifiedName~CreateProjectTests"
```

# Important Files

- `Prowo.WebAsm/Server/Program.cs` - Server startup and dependency injection
- `Prowo.WebAsm/Client/Program.cs` - Client (Blazor) startup
- `Prowo.WebAsm.Server.IntegrationTests/` - Integration tests
- `Prowo.WebAsm/Server/Data/` - Data access layer (stores)
- `Prowo.WebAsm/Shared/` - Shared DTOs between client/server
- `Keycloak.AdminApi/` - Keycloak admin API client

# Deployment

```bash
# Build Docker image
docker build -t prowo:latest .

# Run container
docker run -p 80:80 -p 443:443 prowo:latest
```

# Notes

- The project uses Blazor WebAssembly with a hybrid client-server model
- All UI code is in the Client project, server only exposes API endpoints
- Tailwind CSS is built separately and copied to wwwroot
- The project expects PostgreSQL on startup (configured via connection string)
- OIDC audience validation is disabled (TokenValidationParameters.ValidateAudience = false)
- The project uses .NET 10 for the latest version
- When creating a new project, `Date`, `StartTime`, `EndTime`, `ClosingDate`, and `MaxAttendees` in `EditingProjectDataDto` are `null` — the user must fill them in the UI; the server validates and rejects nulls
- VSCode tasks are in `.vscode/tasks.json`; the `dev` compound task starts `start:database` and `watch:webapp` in parallel (`watch:webapp` chains `watch:tailwind` first); all shell tasks require `"type": "shell"`
