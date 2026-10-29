# Monitoring Dashboard

Real-time monitoring dashboard for a set of simulated servers. The repository contains a .NET 10 API organized into Domain, Application, Infrastructure, and WebAPI projects, plus a React/TypeScript single-page client.

## Implemented features

- User registration and login, JWT access tokens, rotated refresh tokens stored as hashes, and Admin/User role authorization.
- Server CRUD, current status, paged metric history, search, filtering, and sorting.
- Hangfire metric collection on a configurable 1-5 minute schedule. The collector generates simulated data; it does not read the host machine's performance counters.
- Configurable global and per-server alert rules, alert evaluation during collection, alert history, acknowledge/resolve operations, and SignalR notifications.
- Asynchronous HTML reports with status tracking, Hangfire continuation and timeout jobs, local file storage, and simulated email notifications.
- Hangfire job status/count endpoints and an Admin-protected Hangfire dashboard.
- SignalR dashboard/server subscriptions and in-memory connected-user presence.
- API versioning, OpenAPI/Swagger UI, rate limiting on authentication endpoints, structured console/file logging, Problem Details errors, and health check endpoints.
- React pages for login/registration, dashboard and charts, server details, alerts, reports, job monitoring, and Admin user management. The client uses React Query, Zustand, and SignalR.

## Technology

- .NET 10, ASP.NET Core Web API, EF Core 10, SQL Server, MediatR, FluentValidation, Hangfire, SignalR, Serilog, JWT bearer authentication.
- React 19, TypeScript 6, Vite, React Router, TanStack Query, Zustand, Recharts, Tailwind CSS.
- xUnit and EF Core InMemory for the current .NET unit tests; Vitest is configured for client tests.
- Docker Compose currently provisions SQL Server only.

## Prerequisites

- .NET 10 SDK.
- Node.js and npm (the versions in `client/package-lock.json` are the reproducible dependency source).
- Docker Desktop or a local SQL Server instance.
- `sqlcmd` to apply the checked-in database script when using SQL Server locally or in Docker.

## Repository structure

```text
src/Domain/              Entities, constants, and domain exceptions
src/Application/         Use cases, DTOs, interfaces, validators, MediatR behaviors
src/Infrastructure/      EF Core, authentication services, simulated integrations, Hangfire jobs
src/WebAPI/              Controllers, middleware, SignalR, health checks, API composition root
tests/Monitoring.Tests/  .NET unit tests
client/                  React + TypeScript application
db/schema.sql            SQL Server schema and demonstration data
docs/ARCHITECTURE.md     Architecture and runtime flows
AI-USAGE.md              AI assistance log and verification notes
```

## Local setup

### 1. Start SQL Server and create the schema

Docker Compose includes SQL Server 2022 and a persistent named data volume. Copy the example environment file, replace its placeholder with a strong local-only password, then start the database:

```powershell
Copy-Item .env.example .env
# Edit .env and set SA_PASSWORD to a strong local password.
docker compose up -d sqlserver
```

Apply the idempotent schema and demo data script. Substitute the same local password when prompted or in the command; do not commit `.env`:

```powershell
sqlcmd -S localhost,1433 -U sa -C -i db/schema.sql
```

The schema script creates `MonitoringDb`, tables, constraints, indexes, roles, alert rules, sample servers, disks, and historical metrics. The API uses a database-first schema and does not contain EF migrations or automatically create/upgrade the database.

### 2. Configure API secrets

The WebAPI project already has a User Secrets ID. Keep the JWT signing key in User Secrets or environment-specific configuration; the application requires at least 32 UTF-8 bytes and intentionally does not put a key in `appsettings.json`.

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=MonitoringDb;User Id=sa;Password=<your-local-password>;TrustServerCertificate=True;Encrypt=False" --project src/WebAPI
dotnet user-secrets set "Jwt:Key" "<generate-a-private-random-key-of-at-least-32-bytes>" --project src/WebAPI
```

Replace both angle-bracket placeholders before running the commands. To create seed accounts, also set `Seed:Admin:UserName`, `Seed:Admin:Email`, `Seed:Admin:Password` and optionally the corresponding `Seed:User:*` values using User Secrets. Seed account passwords should be unique local/deployment secrets. The seeder ensures Admin and User roles exist; seed accounts are optional.

### 3. Run the API and client

In one terminal:

```powershell
dotnet run --project src/WebAPI --launch-profile http
```

The HTTP API listens at `http://localhost:5077`; Swagger UI is at `http://localhost:5077/swagger` in Development. OpenAPI JSON is at `/openapi/v1.json`. The health routes are `/health/live`, `/health/ready`, and `/health`.

In another terminal:

```powershell
cd client
npm ci
$env:API_TARGET = "http://localhost:5077"
npm run dev
```

Open the Vite URL printed in the terminal (normally `http://localhost:5173`). Vite proxies `/api`, `/hubs`, and `/hangfire` to `API_TARGET`. The Vite config's fallback target is `https://localhost:7000`, while the checked-in API launch profile uses ports 5077 and 7288; set `API_TARGET` as above when running the checked-in profile.

## Configuration reference

- `ConnectionStrings:DefaultConnection`: SQL Server database used by EF Core and Hangfire storage.
- `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenMinutes`, `Jwt:RefreshTokenDays`: non-secret token settings in `src/WebAPI/appsettings.json`; `Jwt:Key` belongs in User Secrets or the environment.
- `Monitoring:CollectionIntervalMinutes`: recurring collection interval; the runtime clamps this to 1-5 minutes.
- `Cors:AllowedOrigins`: browser origins allowed by the API. The checked-in default is the Vite development origin.
- `Reports:StoragePath`: report files are stored under the API content root by default.
- `Seed:Admin:*` and `Seed:User:*`: optional seed user credentials. Values are read from configuration and should be stored as secrets.
- `RateLimiting:AuthPermitPerMinute`: per-client-IP request limit for auth routes.

Environment variables use the standard ASP.NET Core double-underscore form (for example `ConnectionStrings__DefaultConnection` and `Jwt__Key`).

## Authentication and authorization

Register through `POST /api/v1/auth/register`; new registrations receive the User role. Admin accounts must be provisioned through seed configuration or assigned by an Admin. Protected API routes require a Bearer access token. Refresh tokens rotate on refresh and only their hashes are stored. The SignalR hub at `/hubs/monitoring` accepts the access token through its `access_token` query parameter for the WebSocket handshake. The client stores the refresh token in browser local storage.

## Background jobs and real-time updates

Hangfire uses the same SQL Server connection as the application database. A recurring job simulates metric collection, evaluates alert rules, saves metrics/alerts, and then publishes SignalR events. Report requests enqueue generation; a continuation sends a simulated email and publishes final status, while a delayed job marks stale reports failed. The dashboard is available at `/hangfire` and is Admin-only outside Development. API job monitoring routes require Admin authorization.

SignalR events are sent from `/hubs/monitoring`. Clients can subscribe to the dashboard or a server; alerts and reports are broadcast to their corresponding groups. Presence is held in process memory, so it is per API instance.

## Tests and build

From the repository root:

```powershell
dotnet test tests/Monitoring.Tests/Monitoring.Tests.csproj
cd client
npm ci
npm run build
npm test
```

The .NET suite includes 22 unit tests for authentication, seeding, metrics collection, and alert evaluation, plus 5 HTTP integration scenarios for authentication, authorization, server creation, and report scheduling. The API tests use ASP.NET Core TestServer with EF Core InMemory and a no-op report scheduler; they do not start the production SQL Server/Hangfire composition. `npm test` currently exits with “No test files found”; the client test runner is configured, but test cases have not been added.

## Docker

`docker-compose.yml` starts only SQL Server. There is no API or frontend Dockerfile and no full-stack Compose deployment in this repository. Follow the local run steps above for the API and client. The named SQL volume persists database data; stop the database with `docker compose stop sqlserver` or `docker compose down` without removing volumes.

## Known limitations and incomplete assessment items

- Metrics are random simulated values, not measurements from monitored hosts. Disk entities exist in the schema, but collection currently records metric rows rather than per-drive disk samples.
- No EF migration history is maintained; `db/schema.sql` is the database setup source.
- Report files use local disk storage and simulated email writes to logs. Multi-instance deployments need shared/object storage and a real mail integration.
- Presence uses an in-memory tracker and SignalR has no Redis backplane; multiple API instances will not share presence or hub broadcasts.
- Test coverage is limited: no coverage report or substantial frontend test suite. The HTTP integration tests use TestServer directly; WebApplicationFactory and full production startup are not covered.
- The API build succeeds but currently emits four API-versioning/OpenAPI analyzer warnings (`AV0029`/`AV0030`); only API version v1 is configured today.
- The assessment also asks for deployment documentation, screenshots/demo media, and a CI/CD pipeline or deployment guide. These are not present in the current repository; this task adds the requested README and architecture guide only.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for layer responsibilities, request and job flows, relationships, and configuration details.
See [AI-USAGE.md](AI-USAGE.md) for the project's AI assistance log.
