# Progress log

Short, dated entries: what was done, key decisions (and why), what was touched. Newest at the bottom.

## 2026-10-08 — Project restart: full review of backend + frontend

**Done:** read-through of the new backend (`HannasHabits.*`) and the frontend (`hannas-habits-ui`); created `CLAUDE.md`. No production code changed. Backend builds (8 nullable warnings).

**State found:**
- New backend covers Auth (Identity + JWT + refresh), Habits, HabitRecords, DailyDiary (date + text only). Not yet ported from the old services: habit schedules (days of week), diary mood / physical+mental health / highlight / learned+grateful things / tasks, year resolutions, calendar endpoint.
- Frontend still targets the **old** microservice API (ports 7245/7047, `/user/{id}` routes, `/google`, `/completion`) and the old auth response shape → it needs rewiring, not just fixing.

**Top findings (priority order):**
1. Secrets in git: Google OAuth `client_secret*.json` in the frontend repo; local Postgres password in `appsettings.json` of backend (and the old services). → rotate, remove from git, use env / user-secrets.
2. `HabitRecordsController` has two `[HttpPut]` without route → ambiguous endpoint. Controllers bind GET/DELETE params from the body and use `[HttpGet("id")]` (literal) instead of `{id}`.
3. No CORS, no global exception handling (`throw new Exception("... not found")` → 500, `ValidationException` → 500).
4. Dead/dangerous code: custom unsalted-SHA256 `PasswordHasher` registered but unused (Identity has its own) → delete.
5. Frontend API layer: 4 axios instances, hard-coded URLs, token header repeated per call, errors swallowed with `console.log` (callers never see failures).
6. No tests for the new architecture.

**Planned order:** (0) cleanup + secrets → (1) backend Auth + Habits runnable end-to-end (controllers, CORS, error handling) → (2) frontend API client + auth + habit tracker against new API → (3) diary → (4) calendar + resolutions → (5) tests/CI/deploy.

**Decisions (user, same day):**
- Frontend moves to **TypeScript** (types ideally generated from the backend's Swagger/OpenAPI).
- Backend uses the **Repository pattern** (one repository per aggregate root, defined in Application, implemented in Infrastructure) instead of `IApplicationDbContext` in handlers — chosen for the learning goal.
- MediatR: **open**. Facts checked: 12.5.0 is the last Apache-2.0 release; 13+ is dual RPL/commercial; Community edition is free for individuals/companies < $5M gross annual revenue, non-profits, education and non-production, but a (free) license key must be registered, otherwise only log warnings appear. Options: keep MediatR 14 + free key / pin 12.5.0 / switch to MIT-licensed `martinothamar/Mediator` (near-identical API, `ValueTask`).

## 2026-10-08 — Plan written, step B0 done (hygiene & secrets)

**Decisions:** keep MediatR 14 with the free Community license key (project is personal, revenue far below the $5M limit); work backend first, then frontend, one roadmap step per session; new design comes from the claude.ai/design mockup, imported in step M1 (the `claude_design` MCP was not connected in this session, `DesignSync` can't import mockups).

**Done (B0):**
- `docs/ROADMAP.md` created with all backend/frontend steps; `CLAUDE.md` points to it.
- `.gitignore`: `.idea/`, `.DS_Store`, `*.DotSettings.user`; untracked the 11 IDE/macOS files (still in git history).
- `HannasHabits.WebApi/appsettings.json`: DB password removed → now in user-secrets (`ConnectionStrings:DbConnection`); stale `JwtSettings`/`Serilog` sections removed (code reads `Jwt:` from user-secrets; Serilog isn't installed).
- Deleted unused unsalted-SHA256 `PasswordHasher` + `IPasswordHasher` + DI registration; deleted the commented-out old DbContext at the bottom of `ApplicationDbContext.cs`.
- Verified: solution builds; `dotnet ef migrations list` (Development) reaches Postgres via the secret and lists 3 applied migrations.

**Not committed** (changes are staged/unstaged in the working tree). **User still has to:** rotate DB password + update the secret, rotate Google client secret, register the MediatR Community key (needed in B1), connect `claude_design` MCP before M1 (see ROADMAP "Things only the user can do").

**Next:** B1 — Foundations (exceptions → ProblemDetails, CORS, Swagger JWT, JwtOptions, MediatR key, warnings).

## 2026-10-08 — User actions 2 and 3 done by Claude (on the user's request)

- Postgres: password of role `iseaman` changed (`ALTER ROLE`), user-secret `ConnectionStrings:DbConnection` updated; verified new password works, old one is rejected, `dotnet ef migrations list` works. Role `iseaman` owns 6 other local DBs (FTRTDb, HofladenDb, LicensePlateDb, TMSDb, ToDoDb, WordsDb) → those projects need the new password too. A scan of `/Users/iseaman/RiderProjects` and `WebstormProjects` (appsettings/.env/yml) found the old password only in the legacy `UserService` and `HannaHabitsService` of this repo (deleted in B10).
- MediatR Community license key stored as user-secret `MediatR:LicenseKey` (valid until 2027-10-08). B1 wires it into `AddMediatR`.
- Still open for the user: rotate the Google client secret (public in two repos), connect the `claude_design` MCP before M1.
- Note: the new DB password was typed into the chat by the user (public-looking, not random) — fine for a local DB, but don't reuse it elsewhere.

**Next:** B1 — Foundations.

## 2026-10-08 — Google secret rotated, git workflow agreed, B0 committed

- User created a new Google client secret and deleted the leaked one → rotation done. The new secret isn't needed anywhere yet (backend verifies ID tokens with the client id only).
- Git rules agreed with the user (now in CLAUDE.md "Git workflow"): commits allowed after a finished step, **only on `dev`, never push**.
- Backend: branch `dev` already existed and was checked out; B0 + docs committed there.
- Frontend: no `dev` existed → created `dev` from `origin/main` (local `main` is 6 commits behind; upstream already removed `client_secret_*.json` and rewrote the README). Did **not** switch branches or touch the user's uncommitted WIP; F0 handles that.

## 2026-10-08 — B1 done (foundations)

**Done:**
- **Exceptions:** `DomainException` (Domain), `NotFoundException` / `ConflictException` / `ForbiddenException` (Application/Common/Exceptions). All 10 `throw new Exception("… not found")` in the handlers replaced. Entities throw `DomainException` (was `ArgumentException`) with English messages. Other users' data is reported as 404, not 403, on purpose (don't leak existence) → `ForbiddenException` has no user yet.
- **`GlobalExceptionHandler`** (`IExceptionHandler` + `IProblemDetailsService`, WebApi/ExceptionHandling): `ValidationException`→400 (`errors` keyed camelCase, so they map to form fields), `DomainException`→400, NotFound→404, Conflict→409, Forbidden→403, `UnauthorizedAccessException`→401, anything else→500 *without* details (logged at Error). Needed `Logging:LogLevel:Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware = None` in `appsettings.json`: .NET 8's middleware logs every exception as Error even when an `IExceptionHandler` handled it (fixed in .NET 9), our handler logs instead.
- **WebApi composition root:** new `HannasHabits.WebApi/DependencyInjection.cs` (`AddWebApi`) like the other layers; `Program.cs` is now ~25 lines. CORS from `Cors:AllowedOrigins` (Development: `http://localhost:5173`; empty = nothing allowed), `UseHttpsRedirection`, Swagger with JWT bearer scheme.
- **`JwtOptions`** (Infrastructure/Identity) via options pattern + data annotations + `ValidateOnStart` (`Key` ≥ 32 chars, `AccessMinutes` default 15, `RefreshDays` default 30). `JwtTokenService` and the JwtBearer setup both read `IOptions<JwtOptions>` instead of `IConfiguration`. Verified: a 5-char key makes the app refuse to start with an `OptionsValidationException`.
- **MediatR license key** read from `MediatR:LicenseKey`; startup log confirms "valid license key … Community edition, expires 2027-10-08".
- **Warnings 8 → 0:** `GetAllDailyDiariesQuery` returned `List<…>?` but its handler `List<…>`; entities `Title`/`Text` via `[MemberNotNull]` on the setter method + `= null!` in the EF-only private ctor (comment says so; B4's value objects will replace this); `ipAddress` parameters `string? = null` instead of `string = null!`.

**Verified at runtime** (API on https profile, throw-away user, deleted again afterwards): missing habit → 404 `application/problem+json`; empty title → 400 with `errors.title`; no token → 401; preflight from `http://localhost:5173` → 204 + `access-control-allow-origin`, from a foreign origin → no CORS header; Swagger JSON contains the `Bearer` scheme + global requirement.
**Not verified over HTTP** (not reachable yet, covered by B9 tests): 409/403, `DomainException`→400, 401 from `UnauthorizedAccessException`.

**Findings for the next steps (also in ROADMAP):**
1. **Blocker for B2:** the DB still has the legacy `Users` table and `FK_Habits_Users_UserId`; `POST /api/habits` → 500 (FK violation) for every real user. Model snapshot is stale (`has-pending-model-changes` = true). Needs a migration (now first bullet of B2). I did not create/apply one in B1 (scope + it's a DB design decision: no FK vs. FK to `AspNetUsers`).
2. `HabitRecordsController` has two `[HttpPut]` without route → `/swagger/v1/swagger.json` returns 500, so the Swagger page didn't work before either. (I checked the Bearer scheme with a temporary `ResolveConflictingActions`, removed again.) Also `GET /api/habits` returns 415 (binds the query from the body). Both are B2.
3. Frontend must call `https://localhost:7054` in dev: http→https redirect turns a preflight to `http://localhost:5016` into a 307, and the dev cert is not trusted yet (`dotnet dev-certs https --trust` — added to "things only the user can do").

**Next:** B2 — Controllers & REST routes (start with the migration).
