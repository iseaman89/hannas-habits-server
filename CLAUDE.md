# Hanna's Habits

Habit tracker + daily diary + year resolutions + calendar. Started ~2 years ago, paused (mostly because of the frontend), resumed in Oct 2026 with the goal to **finish it**.

Communicate with the user in **German**. Code, identifiers, commit messages and docs in English.

## Why the project looks the way it does (learning project)

This is a **learning project**. Clean Code, DDD and Clean Architecture are used on purpose, even where they are heavier than a habit tracker strictly needs. So:

- Do **not** suggest flattening the architecture just because the app is small. Follow the existing structure.
- Apply **SOLID** and other fitting patterns/architecture practices (CQRS, pipeline behaviors, factory methods, value objects, specification, repository where it pays off, ...). When introducing or applying a pattern, say in one or two sentences *why* — and say so honestly if something is over-engineering.
- Business rules/invariants belong in the **Domain**, not in handlers or controllers.
- Prefer small, reviewable steps (one vertical slice at a time) over big-bang rewrites.

## Repositories

| Part | Path | Remote |
|---|---|---|
| Backend (.NET 8) — this repo | `/Users/iseaman/RiderProjects/HannasHabits` | `iseaman89/hannas-habits-server` |
| Frontend (React 18 + Vite 6 + Tailwind 4; plain JS today, **migrating to TypeScript**) | `/Users/iseaman/WebstormProjects/hannas-habits-ui` | `iseaman89/hannas-habits-ui` |

The frontend is a separate git repo outside this working directory. Add it with `--add-dir` / `/add-dir` when working on it; where `/add-dir` is unavailable, work on it with absolute paths via Bash/Read/Edit (done for F0). It has its own short `CLAUDE.md` that points back here.

## Backend architecture

Solution `HannasHabits.sln` = 4 production projects (dependency rule points inwards) + 4 test projects (below):

```
WebApi ──► Application ──► Domain
   └─────► Infrastructure ──► Application
```

- `HannasHabits.Domain` — entities (`Habit` aggregate root with `HabitRecord`, `DailyDiary`, `Resolution`), value objects (`HabitTitle`, `HabitSchedule`: sealed records, valid by construction via `Create`), `EntityBase`, enums. No dependencies. Private setters + static `Create` factory + invariants inside the entity.
- `HannasHabits.Application` — CQRS with **MediatR**: one folder per use case (`Command|Query`, `Handler`, `Validator`, `Dto`). **FluentValidation** via `ValidationBehaviour` pipeline. **Mapster** (`IRegister` per feature). Abstractions: repositories + query interfaces per feature, `IUnitOfWork`, `ICurrentUser`, `IIdentityService`, `IJwtTokenService`, `IGoogleTokenVerifier`. Auth is a use-case folder like the others (`Auth/Commands/{Register,Login,GoogleLogin,Refresh,Revoke,RevokeAll}`); all token-issuing endpoints answer with one `AuthResult` (`{ user, tokens }`).
- `HannasHabits.Infrastructure` — EF Core + PostgreSQL (`ApplicationDbContext`, `IEntityTypeConfiguration<T>` per entity, migrations), ASP.NET Identity (`ApplicationUser`, lockout 5 failures/15 min), JWT access tokens + rotating refresh tokens (stored as SHA-256 hash, replay of a used token revokes all sessions of the user), Google ID-token verification.
- `HannasHabits.WebApi` — controllers (thin: only `IMediator.Send`), composition root (`Program.cs`).

Every query/command filters by the current user's id (data isolation) — keep this.

Tests (xunit, **no mocking library**: hand-written in-memory fakes, xunit's own `Assert`): `HannasHabits.Domain.Tests` (pure unit tests), `HannasHabits.Application.Tests` (handlers/validators/pipeline against fakes), `HannasHabits.Architecture.Tests` (NetArchTest + reflection rules for the layers), `HannasHabits.Integration.Tests` (the real app via `WebApplicationFactory<Program>` on a real PostgreSQL in a Testcontainers container — **Docker must be running**; one container per run, a migrated template database copied per use, tests keep apart by user). A new use case gets: Domain test for its rule, handler test with fakes, validator test, API test (incl. isolation between two users), and — if it writes concurrently — a forced-race test (`RaceUnitOfWork` + `Barrier`).

Decision: persistence goes through **repositories** (one per aggregate root; interfaces in Application, implementations in Infrastructure) instead of handlers using a DbContext directly (done in B3; the read side uses small query interfaces that project to DTOs). Application has no EF Core reference.

Delivery (B10): `HannasHabits.WebApi/Dockerfile` (multi-stage, .NET 8, non-root; build context = repo root), `docker-compose.yml` (API + PostgreSQL; values from a git-ignored `.env`, template in `.env.example`; sets `Database__MigrateOnStartup=true`), `.github/workflows/ci.yml` (build + `dotnet test HannasHabits.sln` on ubuntu-latest, plus a Docker image build). The legacy services (`HannaHabitsService`, `UserService`, `Data.Shared`) were deleted after everything was ported; they are still in the git history.

Background work: `RefreshTokenCleanupService` (Infrastructure/Identity, a `BackgroundService`) deletes refresh-token rows `RetentionDays` (7) after they expired, at startup and every `IntervalHours` (6) — the rule lives in `RefreshTokenCleaner`. `Database:MigrateOnStartup=true` applies pending migrations at startup (off by default; only for a single instance — EF Core 8 takes no migration lock).

## Work plan

The step-by-step plan lives in **`docs/ROADMAP.md`** (backend B0–B10 incl. B5b, mockup M1, frontend F0–F9). One step per session: do the next open step, check it off, log it in `docs/PROGRESS.md`, then the user starts a fresh session. Decisions so far: repositories per aggregate root, MediatR 14 stays (free Community license key — MediatR is by Jimmy Bogard/Lucky Penny Software), TypeScript for the frontend, new design from the mockup — **`docs/DESIGN.md`** (tokens, screens, what the UI needs from the API; read it instead of re-importing the mockup; streaks make B8 required, `Priority` is dropped).

## Git workflow

- Work **only on branch `dev`** (create it if missing). Never commit to `main`.
- The user allows **local commits** once a roadmap step is finished and verified (build/lint/tests green). Small, focused commits with a clear message; end commit messages with the `Co-Authored-By: Claude …` line the harness provides. Stage explicit paths, never other people's/WIP changes, never secrets (scan the staged diff for keys/passwords before committing).
- **Never push** (and no force operations, no history rewrites) unless the user explicitly asks.
- Frontend repo: same rules; work on its `dev` branch (based on `origin/main`, F0 is committed there). The user's old uncommitted WIP is parked in `git stash` (`stash@{0}` on its `main`) — never drop or pop it without asking.

## Configuration & secrets

- Secrets live in `dotnet user-secrets` of `HannasHabits.WebApi` (only loaded when `ASPNETCORE_ENVIRONMENT=Development`; production uses env vars): `ConnectionStrings:DbConnection` (PostgreSQL), `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`, `MediatR:LicenseKey` (free Community key, expires 2027-10-08), `Google:ClientId` (public OAuth client id of the frontend — not secret, but kept here per environment; production env var `Google__ClientId`; the app refuses to start without it). `appsettings.json` holds only non-secret defaults. Never commit secrets, passwords or `client_secret*.json` files.
- Non-secret config: `Database:MigrateOnStartup` (default off), `RefreshTokenCleanup:IntervalHours` / `:RetentionDays` (defaults 6 / 7, validated on startup), `Cors:AllowedOrigins` (array; Development: `http://localhost:5173` in `appsettings.Development.json`; production via env vars `Cors__AllowedOrigins__0`, …; empty = no cross-origin access). The `Jwt:` section is bound to `JwtOptions` and validated on startup (`Jwt:Key` ≥ 32 chars; optional `Jwt:AccessMinutes` default 15, `Jwt:RefreshDays` default 30).
- Show secrets only masked (`dotnet user-secrets list | sed -E 's/=.*/= <hidden>/'`).
- Running `dotnet ef` against the real DB needs `ASPNETCORE_ENVIRONMENT=Development`.

## Commands

```bash
dotnet build HannasHabits.sln
dotnet run --project HannasHabits.WebApi          # http://localhost:5016, https://localhost:7054, Swagger at /swagger
dotnet test HannasHabits.sln                      # ~35 s; needs Docker (an old engine is handled, see TestEnvironment)
docker compose up --build                         # API on :8080 + PostgreSQL; needs a .env (copy .env.example)
dotnet ef migrations add <Name> -p HannasHabits.Infrastructure -s HannasHabits.WebApi
dotnet ef database update -p HannasHabits.Infrastructure -s HannasHabits.WebApi
# frontend (in its own repo): npm run dev | npm run build | npm run lint
```

## Session & context hygiene (keep token usage low)
 
### Keep a running project log
- When you finish a task, feature, or notable decision, append a short entry to `docs/PROGRESS.md` (create it if it doesn't exist yet): date, what was done, key decisions made and why, files/features touched. A few lines per entry is enough — summarize, don't paste code or full diffs.
- If you're unsure about current project state, prior decisions, or "why was it built this way" — check `docs/PROGRESS.md` (and this file) **before** asking the user. Only ask if it's genuinely not documented.
### Delegate heavy-output work to subagents
- Running the test suite, grepping/searching across the codebase, reading long log files or stack traces, or parsing large generated files → delegate to a subagent and have it report back only a short summary (pass/fail counts, relevant error lines, key findings), not the raw output.
- Only pull raw output into the main conversation when you specifically need to inspect it in detail.
### Don't re-read what you already have
- Don't re-view a file already read earlier in this session unless it may have changed since (e.g. after an edit).
- Don't re-explain project architecture that's already covered in this file — reference it instead of restating it.
### One task per session
- Treat each unrelated task/feature/PR as its own session. Don't chain unrelated work in one long-running session — start fresh (`/clear`) instead.
- After finishing a discrete phase of work (ideally right after a commit), run `/compact` rather than letting the session grow unchecked mid-task.
