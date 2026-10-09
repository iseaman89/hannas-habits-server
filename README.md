# Hanna's Habits – Backend 🧠

The **backend** of *Hanna's Habits*: a habit tracker with a daily diary, year resolutions and a calendar. One ASP.NET Core 8 Web API on PostgreSQL.

🔗 **Frontend repository**: [hannas-habits-ui](https://github.com/iseaman89/hannas-habits-ui)  
🔗 **Main project overview**: [hannas-habits](https://github.com/iseaman89/hannas-habits)

It is also a **learning project**: Clean Architecture, DDD, CQRS and SOLID are used on purpose, even where a habit tracker would get by with less. The decisions and the step-by-step history are in [`docs/`](docs).

---

## What it does

| Area | Endpoints |
|---|---|
| Auth | `POST /api/auth/{register, login, google, refresh, revoke, revoke-all}` – JWT access tokens (short-lived) + rotating refresh tokens |
| Habits | `GET/POST /api/habits`, `GET/PUT/DELETE /api/habits/{id}`, `GET /api/habits/overview?from=&to=&asOf=` (month grid + current streaks in one call) |
| Habit records | `GET /api/habits/{habitId}/records?from=&to=`, `PUT/DELETE /api/habits/{habitId}/records/{date}` |
| Daily diary | `GET/PUT/DELETE /api/daily-diaries/{date}` (one document per user and day, autosave-friendly), `GET /api/daily-diaries?from=&to=` (mood per day for the calendar) |
| Year resolutions | `GET /api/resolutions/{year}`, `POST /api/resolutions/{year}/items`, `PUT/DELETE /api/resolutions/{year}/items/{id}` (an item can link to a habit) |

Every request is scoped to the signed-in user. Errors are `ProblemDetails` (400 with `errors.<field>`, 401, 404, 409, 429). Swagger is served at `/swagger`; [`HannasHabits.WebApi/HannasHabits.WebApi.http`](HannasHabits.WebApi/HannasHabits.WebApi.http) has a runnable request for every route.

## Architecture

```
WebApi ──► Application ──► Domain
   └─────► Infrastructure ──► Application
```

| Project | Role |
|---|---|
| `HannasHabits.Domain` | Entities (`Habit` aggregate with its records, `DailyDiary`, `Resolution`), value objects, the streak rule. No dependencies. |
| `HannasHabits.Application` | Use cases with MediatR (one folder per command/query: handler, validator, DTO), FluentValidation pipeline, repository and query interfaces. |
| `HannasHabits.Infrastructure` | EF Core + PostgreSQL, ASP.NET Identity, JWT/refresh tokens, Google ID-token check, repositories, migrations. |
| `HannasHabits.WebApi` | Thin controllers, error mapping, CORS, Swagger, composition root. |

The dependency rule is enforced by the architecture tests, not just by convention.

## Run it with Docker Compose

Needs Docker. This starts the API and a PostgreSQL database; the schema is created on startup.

```bash
cp .env.example .env      # fill in POSTGRES_PASSWORD, JWT_KEY, GOOGLE_CLIENT_ID (the file explains each value)
docker compose up --build
```

The API answers on <http://localhost:8080> (Swagger: <http://localhost:8080/swagger>). Data lives in the `db-data` volume; `docker compose down -v` deletes it.

The container speaks plain HTTP. For HTTPS in a real deployment put a reverse proxy in front of it; allow the frontend's origin with `CORS_ORIGIN`.

## Run it for development

You need the .NET 8 SDK and a PostgreSQL database, e.g.:

```bash
docker run -d --name hh-db -e POSTGRES_PASSWORD=dev -e POSTGRES_DB=hannashabits -p 5432:5432 postgres:17-alpine
```

Secrets go into [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) (only read when `ASPNETCORE_ENVIRONMENT=Development`, which `dotnet run` sets), never into a tracked file:

```bash
cd HannasHabits.WebApi
dotnet user-secrets set "ConnectionStrings:DbConnection" "Host=localhost;Port=5432;Database=hannashabits;Username=postgres;Password=dev"
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
dotnet user-secrets set "Jwt:Issuer" "hannas-habits"
dotnet user-secrets set "Jwt:Audience" "hannas-habits-ui"
dotnet user-secrets set "Google:ClientId" "<OAuth client id of the frontend>"
dotnet user-secrets set "MediatR:LicenseKey" "<free Community key>"   # optional
cd ..

ASPNETCORE_ENVIRONMENT=Development dotnet ef database update -p HannasHabits.Infrastructure -s HannasHabits.WebApi
dotnet run --project HannasHabits.WebApi        # http://localhost:5016, https://localhost:7054
```

The browser needs to trust the ASP.NET dev certificate once (`dotnet dev-certs https --trust`), because the API redirects HTTP to HTTPS.

## Configuration

Set as user-secrets, `appsettings*.json`, or environment variables (`Section__Key`, e.g. `Jwt__Key`).

| Key | Required | Meaning |
|---|---|---|
| `ConnectionStrings:DbConnection` | yes | PostgreSQL connection string |
| `Jwt:Key` | yes | Signing key, at least 32 characters |
| `Jwt:Issuer`, `Jwt:Audience` | yes | Token issuer / audience |
| `Jwt:AccessMinutes`, `Jwt:RefreshDays` | no | Token lifetimes (15 minutes / 30 days) |
| `Google:ClientId` | yes | OAuth client id of the frontend (public); Google ID tokens for any other client are rejected |
| `MediatR:LicenseKey` | no | Free [Community key](https://luckypennysoftware.com); without it MediatR logs a warning |
| `Cors:AllowedOrigins` (array) | no | Origins of the frontend; empty = no cross-origin access |
| `Database:MigrateOnStartup` | no | `true` applies pending migrations at startup (off by default; fine for one instance, see below) |
| `RefreshTokenCleanup:IntervalHours`, `:RetentionDays` | no | How often expired refresh tokens are deleted (6 hours) and how long they are kept after expiry (7 days) |

The `Jwt:`, `Google:` and `RefreshTokenCleanup:` sections are validated at startup: a missing or invalid value stops the app right away instead of failing at the first login.

`Database:MigrateOnStartup` is convenient for one instance with its own database. EF Core 8 takes no lock while migrating, so with several instances apply the migrations as a separate deployment step instead.

## Tests

```bash
dotnet test HannasHabits.sln        # ~35 s, needs Docker
```

| Project | What it checks |
|---|---|
| `HannasHabits.Domain.Tests` | Value objects, entities, the streak rule (table-driven plus seeded random cases against an independent reference) |
| `HannasHabits.Application.Tests` | Handlers, validators and the pipeline against hand-written in-memory fakes |
| `HannasHabits.Architecture.Tests` | The layer rules (NetArchTest + reflection) |
| `HannasHabits.Integration.Tests` | The real app on a real PostgreSQL ([Testcontainers](https://testcontainers.com/)): every endpoint, data isolation between users, migrations (every `Down`), forced race conditions |

There is no mocking library on purpose: fakes keep a test about state instead of call sequences.

[GitHub Actions](.github/workflows/ci.yml) builds and tests every push and pull request to `main` and `dev`, and checks that the Docker image builds.

## Docs

- [`docs/ROADMAP.md`](docs/ROADMAP.md) – the work plan, step by step
- [`docs/PROGRESS.md`](docs/PROGRESS.md) – what was done in each step and why
- [`docs/DESIGN.md`](docs/DESIGN.md) – design language and screens, and what the UI needs from the API

---

## 🧑‍💻 Author

**Yevgen Panych** – Umschüler zum Fachinformatiker AE

📫 [LinkedIn](https://www.linkedin.com/in/yevgen-panych)  
🌐 [Portfolio](https://panych.site)
