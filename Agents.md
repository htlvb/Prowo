# Agents.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Project Overview

Prowo is a Blazor WebAssembly application for managing school project weeks at HTLVB (Higher Technical School Lower Austria). Students can create, register for, and manage projects. Projects belong to events (e.g., "Projektwoche 2026"). The application uses Keycloak for OAuth2/OIDC authentication and PostgreSQL for data storage.

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
- PostgreSQL database with three main tables:
  - `event`: Project week events with title, date range, visibility/registration timestamps
  - `project`: Projects belonging to an event with organizer info (JSON), dates, attendee limit
  - `registration_event`: Registration history (JSON user data)
- Every project has a mandatory `event_id` (NOT NULL FK to `event`)
- `IEventStore` interface with implementations: `PgsqlEventStore` (production), `InMemoryEventStore` (tests)
- `IProjectStore` interface with implementations: `PgsqlProjectStore` (production), `InMemoryProjectStore` (tests)

### 3. Authorization
- Role-based access controlled via Keycloak roles:
  - `all-projects-editor`: Full access including event management
  - `project-creator`: Can create/edit/delete own projects
  - `report-viewer`: Can view attendee reports
  - `project-attendee`: Can register for projects
- Authorization policies: `CreateProject`, `UpdateProject`, `DeleteProject`, `CreateReport`, `AttendProject`, `SeeAllProjects`, `ManageEvents`

### 4. Registration Strategy
- `IRegistrationStrategy` with logical AND combination (`LogicalAndCombinationStrategy`) of:
  - `NoRegistrationBeforeRegistrationFromStrategy` — blocks registration before event's `registration_from`
  - `NoRegistrationAfterClosingDateStrategy` — blocks registration after project's `ClosingDate`
  - `NoRegistrationIfRegisteredStrategy` — prevents double registration
  - `NoWaitingListStrategy` — only `MaxAttendees` registrations count (configured per deployment)
  - `SingleRegistrationPerDayStrategy` — one project per day (configured per deployment)

### 5. Event Grouping
- Projects are grouped by event in the project list (`ProjectListDto.Events` is `IReadOnlyList<EventWithProjectsDto>`)
- Events have `VisibleFrom`: projects in the event are hidden from non-admins until this time
- Events have `RegistrationFrom`: registration button is replaced with "Anmeldung beginnt am..." until this time
- `all-projects-editor` users see all events regardless of `VisibleFrom`

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
This project does not use traditional migrations. Database is re-created from schema on container restart. Always append migrations at the end of `db-schema.sql` using `ALTER TABLE ... ADD COLUMN ...`. Never modify the `CREATE TABLE` definition. The `CREATE TABLE` is for fresh installs; appended `ALTER TABLE` statements migrate the production database without data loss.

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

## Event
- `Id`, `Title`
- `Start`, `End` (DateOnly — allowed date range for projects in this event)
- `VisibleFrom` (DateTime UTC — when projects become visible to attendees)
- `RegistrationFrom` (DateTime UTC — when registration opens)

## Project
- `EventId` (first parameter — mandatory FK to Event)
- `Id`, `Title`, `Description`, `Location`
- `Organizer` (ProjectOrganizer) + `CoOrganizers` array
- `Date` (DateOnly), `StartTime`, `EndTime` (TimeOnly), `ClosingDate` (DateTime UTC)
- `MaxAttendees`, `AllAttendees` (full list; first MaxAttendees are registered, rest are waiting)
- `PaymentInfo` (optional QR/IBAN payment data)

## UserStore
- Uses Keycloak to get users from groups
- Filters by class regex pattern
- `OrganizerGroupId` and `AttendeeGroupId` map Keycloak groups to app roles

## Keycloak Roles → App Roles
| Keycloak Role | Permissions |
|---------------|-------------|
| all-projects-editor | Full CRUD on projects and events, see all projects regardless of VisibleFrom |
| project-creator | Create/edit/delete own projects |
| report-viewer | View attendee reports |
| project-attendee | Register/deregister for projects |

# Testing

Integration tests use an in-memory server (`InMemoryServer`) with:
- `InMemoryProjectStore` and `InMemoryEventStore`
- `InMemoryUserStore` seeded from `FakeData.ProjectOrganizers` and `FakeData.ProjectAttendees`
- `FakeData.DefaultEvent` always seeded into `InMemoryEventStore` (wide date range, `VisibleFrom`/`RegistrationFrom` = `DateTime.MinValue`)
- Property-based tests use FsCheck via `CustomGenerators`

```bash
# Run all tests
dotnet test

# Run integration tests
dotnet test --project Prowo.WebAsm.Server.IntegrationTests/Prowo.WebAsm.Server.IntegrationTests.csproj

# Run a specific test file
dotnet test --filter "FullyQualifiedName~CreateProjectTests"
```

**DateTime conventions in tests:**
- Domain `ClosingDate` and event timestamps are UTC (`DateTimeKind.Utc`)
- Use `DateOnly.ToDateTime(TimeOnly, DateTimeKind.Utc)` instead of `DateTime.SpecifyKind` where possible
- `EditingProjectDataDto` closing date is user-local time (`DateTimeKind.Unspecified`) — the server converts it with `FromUserTime()`
- Event `start`/`end` dates in tests use relative dates (`DateOnly.FromDateTime(DateTime.UtcNow.AddDays(N))`) to stay consistent with relative `VisibleFrom`/`RegistrationFrom` offsets

# Important Files

- `Prowo.WebAsm/Server/Program.cs` — Server startup and dependency injection
- `Prowo.WebAsm/Client/Program.cs` — Client (Blazor) startup
- `Prowo.WebAsm/Server/Data/IProjectStore.cs` / `PgsqlProjectStore.cs` — Project data access
- `Prowo.WebAsm/Server/Data/IEventStore.cs` / `PgsqlEventStore.cs` — Event data access
- `Prowo.WebAsm/Server/Data/IRegistrationStrategy.cs` — Registration strategy implementations
- `Prowo.WebAsm/Server/Controllers/ProjectController.cs` — Project API endpoints
- `Prowo.WebAsm/Server/Controllers/EventController.cs` — Event CRUD API endpoints
- `Prowo.WebAsm/Shared/DataTransferObjects.cs` — Shared DTOs between client/server
- `db-schema.sql` — Full DB schema including migrations appended at the bottom
- `Prowo.WebAsm.Server.IntegrationTests/` — Integration tests

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
- OIDC audience validation is disabled (`TokenValidationParameters.ValidateAudience = false`)
- The project uses .NET 10
- When creating a new project, `Date`, `StartTime`, `EndTime`, `ClosingDate`, and `MaxAttendees` in `EditingProjectDataDto` are `null` — the user must fill them in the UI; the server validates and rejects nulls
- `EventId` is the first parameter in both the `Project` record and `EditingProjectDataDto`
- Deleting an event that has projects returns `409 Conflict`; the client shows the server's error message
- VSCode tasks are in `.vscode/tasks.json`; the `dev` compound task starts `start:database` and `watch:webapp` in parallel (`watch:webapp` chains `watch:tailwind` first); all shell tasks require `"type": "shell"`
