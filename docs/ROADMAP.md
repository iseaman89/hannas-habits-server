# Roadmap

Work plan to finish Hanna's Habits. One step = one session. Check the box when done, add a short entry to `docs/PROGRESS.md`, then start a fresh session.

**Start a new session with:** "Read CLAUDE.md and docs/ROADMAP.md and do the next open step."

Decisions already made (see CLAUDE.md): learning project (Clean Architecture/DDD/SOLID on purpose), repositories per aggregate root, MediatR 14 stays (free Community license key), frontend moves to TypeScript, new design from the mockup.

**Every step ends with:** build green (`dotnet build` / `npm run build` + lint), tests green if any exist, ROADMAP box checked, PROGRESS entry written. Then commit locally on `dev` (see CLAUDE.md "Git workflow"); never push.

## Things only the user can do (Claude cannot)

Both GitHub repos are **public** (checked 2026-10-08).

- [x] *(done 2026-10-08 by Claude, new password set + user-secret updated; note: role `iseaman` is shared by other local DBs — FTRTDb, HofladenDb, LicensePlateDb, TMSDb, ToDoDb, WordsDb — their configs need the new password)* Change the local Postgres password (old one is in the public history of `hannas-habits-server`: `appsettings.json` of WebApi, UserService, HannaHabitsService) and update the user-secret `ConnectionStrings:DbConnection`.
- [x] *(done 2026-10-08: user created a new secret and deleted the leaked one)* Rotate the Google OAuth client secret in Google Cloud Console. The same secret is public in **two** places: `client_secret_*.json` in `hannas-habits-ui` and `UserService/appsettings.json` in `hannas-habits-server`. (It was also printed once in a Claude terminal session on 2026-10-08.) The new backend/frontend do not need the secret for ID-token login — only the client id.
- [x] *(done 2026-10-08, key stored; **expires 2027-10-08 — renew the free Community key then**)* Register the free MediatR Community license key at https://luckypennysoftware.com (pricing page → Community) and store it as user-secret `MediatR:LicenseKey` (B1 reads this config key; production: env var `MediatR__LicenseKey`).
- [ ] Connect the `claude_design` MCP (`/design-login`) before step M1 — it is not available in every session.
- [ ] Trust the ASP.NET dev certificate once (`dotnet dev-certs https --trust`, asks for the macOS password) before F3. The API redirects http→https (`UseHttpsRedirection`) and a CORS preflight to the http URL gets a 307, so the frontend must call `https://localhost:7054` — and the browser rejects that until the certificate is trusted.

---

## Backend (`/Users/iseaman/RiderProjects/HannasHabits`)

- [x] **B0 — Hygiene & secrets** *(done 2026-10-08; the legacy projects' `appsettings.json` still contain the old password until B10 — rotating the password is what actually fixes it).* Extend `.gitignore` (`.idea/`, `.DS_Store`), untrack IDE/OS files, remove the DB password from `HannasHabits.WebApi/appsettings.json` (move to user-secrets), delete the unused unsalted `PasswordHasher` + `IPasswordHasher` + DI line, delete commented-out old DbContext, remove stale config sections (`JwtSettings`, `Serilog`).
  *Done when:* app still builds and starts with the secret from user-secrets; no password in tracked files of the new projects.

- [x] **B1 — Foundations** *(done 2026-10-08, details in PROGRESS; verified at runtime: 404/400 ProblemDetails, CORS preflight from :5173, JwtOptions fail-fast, MediatR license accepted. Swagger Authorize scheme verified, but the Swagger page itself only works after B2 fixes the duplicate `[HttpPut]`).*
  - Exceptions: `DomainException`, `NotFoundException`, `ConflictException`, `ForbiddenException` in Application/Domain; replace every `throw new Exception("... not found")`.
  - Global `IExceptionHandler` → `ProblemDetails` (ValidationException→400 with errors, NotFound→404, Conflict→409, Unauthorized→401, unhandled→500 without details).
  - CORS (allowed origins from config, dev: `http://localhost:5173`), HTTPS redirection, Swagger with JWT bearer button.
  - `JwtOptions` via options pattern with `ValidateOnStart` (config section `Jwt:`); remove the null-warning in `Program.cs`.
  - MediatR license key registration. Fix nullable warnings (CS8631 in `GetAllDailyDiariesQuery`, CS8618 in entities via `required`/private-ctor pattern, CS8604).
  *Done when:* zero build warnings; a request for a missing habit returns 404 ProblemDetails; browser preflight from :5173 works.

- [x] **B2 — Controllers & REST routes** *(done 2026-10-08, details in PROGRESS; all endpoints verified over HTTPS incl. isolation between users, `/swagger/v1/swagger.json` = 200).*
  - **Do first — blocks end-to-end testing:** the DB still has the legacy `Users` table and `FK_Habits_Users_UserId` (from `InitialCreate`; the `User` entity no longer exists in code, `dotnet ef migrations has-pending-model-changes` says the snapshot is stale). Result: `POST /api/habits` returns 500 (FK violation) for every Identity user. Add a migration that drops that FK and the `Users` table (decide: no FK, or FK to `AspNetUsers` via the entity configuration — Domain must not reference Identity) and apply it. `DailyDiary` has no such FK. Also: until the duplicate `[HttpPut]` in `HabitRecordsController` is fixed, `/swagger/v1/swagger.json` returns 500.
  - Routes — proposal (adjust if needed):
    - `POST /api/auth/{register|login|refresh|revoke|revoke-all}` (Google comes in B5)
    - `GET /api/habits`, `GET /api/habits/{id}`, `POST /api/habits`, `PUT /api/habits/{id}`, `DELETE /api/habits/{id}`
    - `GET /api/habits/{habitId}/records?from=&to=`, `PUT /api/habits/{habitId}/records/{date}` (idempotent mark), `DELETE /api/habits/{habitId}/records/{date}`
    - Diary routes are designed in B6.
  - Route/body split: ids from route, payload from body (small request models in WebApi → map to commands); `201 Created` + `Location`, `204 NoContent` for update/delete; `[ProducesResponseType]`.
  - Replace the weatherforecast template in `HannasHabits.WebApi.http` with real requests (register → login → habits → records).
  *Done when:* every endpoint is callable from Swagger / the `.http` file; no `[HttpGet("id")]`, no body on GET/DELETE.

- [x] **B3 — Repositories, Unit of Work, current user.** *(done 2026-10-08, details in PROGRESS; build 0 warnings, 43-check HTTP smoke test incl. cross-user isolation all PASS, Application has no EF Core reference, not even transitive).*
  - `IHabitRepository`, `IDailyDiaryRepository` (Application) + implementations in `Infrastructure/Repositories`; `IUnitOfWork`.
  - `ICurrentUser` with non-null `UserId` (throws once) — removes the repeated `userId is null` check in every handler.
  - Refactor handlers to repositories; read side gets small query interfaces (e.g. `IHabitQueries`) that project straight to DTOs (`AsNoTracking`, `Select`) → Application no longer references EF Core (also drop unused `System.IdentityModel.Tokens.Jwt`, deprecated `FluentValidation.AspNetCore`).
  - Explain the trade-off (command vs. query side) to the user while doing it.
  *Done when:* `HannasHabits.Application.csproj` has no EF Core reference; handlers never touch a DbContext.

- [x] **B4 — Domain hardening.** *(done 2026-10-08, details in PROGRESS; build 0 warnings, 42-check HTTP smoke test + deterministic race harness (21) + domain/converter checks (38) all PASS. Decided: keep `Include` of all records — see the comment in `HabitRepository`).*
  - Move invariants into the aggregate: `Habit.MarkCompleted` rejects duplicates, `Habit.UnmarkCompleted(date)` (B3 already added a minimal version returning `bool`, because without a `HabitRecords` DbSet the handler needs a way to remove a record through the aggregate; B4 refines it); `DailyDiary` unique per user+date → Conflict instead of DB exception. (Since B2 `MarkCompleted` is idempotent in the handler; two *concurrent* PUTs for the same day still hit the unique index → 500. Handle the race, e.g. catch the unique violation and return the existing record.)
  - Value objects (fill the empty `ValueObjects` folder): `HabitTitle`, `HabitSchedule` (set of `DayOfWeek`), later `Mood`/`Percentage`.
  - Port **habit schedules** (days of week) from the old model; validator for `Description` (max 500).
  - Consistent English domain messages; EF configuration + migration.
  - Think about aggregate size (`Habit` loads all records) and decide: keep `Include` or query records by date range.
  *Done when:* rules live in Domain, handlers only orchestrate; new migration applies cleanly.

- [x] **B5 — Auth into Application + Google login.** *(done 2026-10-08, details in PROGRESS; build 0 warnings, 78-check HTTP smoke test PASS (incl. replay + 10 parallel refreshes → exactly one 200), IdentityService harness (Google create/find/conflict + races) 15/15 PASS ×8 runs. Decided: no silent linking of a Google login to an existing password account (409) — see F4/open points).*
  - Move register/login/refresh/revoke logic out of `AuthController` into commands (`IIdentityService` abstraction in Application, Identity stays in Infrastructure).
  - Refresh tokens stored hashed, rotation with correct `ReplacedByToken`, reuse detection, lockout on failures, `TimeProvider` instead of `DateTime.UtcNow`.
  - `POST /api/auth/google` (verify Google ID token, create/link user) — the frontend has a Google button.
  - Stable auth response contract for the frontend (user + access/refresh token + expiry).
  - Validation errors with one key style: FluentValidation errors come back camelCase (`errors.title`), but MVC's implicit `[Required]` on non-nullable request properties produces `errors.Title` when the property is missing from the body. Add validators for the auth requests, then set `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes` (or an `InvalidModelStateResponseFactory` that camelCases keys) so there is one validation path.
  *Done when:* AuthController only sends commands; tokens in DB are hashed.

- [ ] **M1 — Mockup → design brief** *(needs claude_design MCP, see above).* Import the mockup, then write `docs/DESIGN.md`: screens, components, design tokens (colors, fonts, spacing), and — important — the **list of features/fields the new design needs from the API** (moods, health bars, streaks, stats, …). Later sessions read `DESIGN.md` instead of re-importing the mockup.
  Mockup: https://claude.ai/design/p/a388ae78-c586-4176-a236-83ee88bef383?file=Hannas+Habits+App.dc.html
  Also decide here whether the legacy `Priority` (Normal/High, `HannasHabits.Data.Shared/Enums/Priority.cs`) is needed — B4 deliberately did not port it.
  Files to focus on: `Hannas Habits App.dc.html`; also read `_ds/organic-a7e3cd91-5f19-4c95-8f8c-81a864dbaea6/_ds_bundle.js`, `_ds/organic-a7e3cd91-5f19-4c95-8f8c-81a864dbaea6/styles.css`, `support.js`.
  *Done when:* `docs/DESIGN.md` exists and B6–B8 are adjusted to it.

- [ ] **B6 — Daily diary (extended).** Old model to port (`HannaHabitsService/Models/DailyDiary.cs`, `DailyTask.cs`): mood (Domain already has `Mood` enum 1–5), physical + mental health (0–100), highlight, learned things[], grateful things[], tasks[] (title + done). Adjust to `DESIGN.md`.
  Pattern from B4 for `Mood` / health percentages: `sealed record` value object in `Domain/ValueObjects` (private ctor, `Create` validates, `const` limits reused by validators + EF config) + `ValueConverter` in `Infrastructure/Persistence/Converters`. B4 already made `CreateDailyDiary` answer 409 for a second entry per day (pre-check `ExistsForDateAsync` + unique index → `DuplicateEntryException`); the upsert-by-date route replaces that.
  Also: `DailyDiary.UserId` still has no FK to `AspNetUsers` (B2 only added it for `Habits`; adding it to a table with data fails on orphans). The DB holds one orphan test diary ("some text", owner = the deleted legacy user `6c6bba06-…`) — delete it first (ask the user), then add the FK like in `HabitConfiguration`. The diary routes are `/api/daily-diaries[/{id}]` by id until then.
  Routes (proposal): `GET /api/daily-diaries/{date}`, `PUT /api/daily-diaries/{date}` (upsert), `DELETE …/{date}`, `GET /api/daily-diaries?from=&to=` (calendar view: dates with entries).
  *Done when:* frontend-relevant fields roundtrip; migration applied; ids no longer needed by the frontend (date is the key).

- [ ] **B7 — Year resolutions.** Port `YearResolution` + `Resolution` (aggregate per user+year, items with title/done, summary). CRUD + `GET /api/resolutions` / `GET /api/resolutions/{year}`.

- [ ] **B8 — Habit statistics (only if the design needs them).** (`HabitSchedule.IncludesDay` / `Days` from B4 give the scheduled days; the DB stores the schedule as a bit mask, so “scheduled today” is computed in memory, not in SQL.) Port `CompletionRate` / `DaysOfWeekCounter` from `HannasHabits.Data.Shared` as pure domain logic (testable), expose via a query (completion rate per month, streaks).

- [ ] **B9 — Tests.** (Blueprints from B5: `JwtTokenService` with `FakeTimeProvider` — rotation chain (`ReplacedByTokenId`), expiry, replay of a used token revokes all sessions, replay vs. plain logout (only the former is a theft alarm), two scopes + `Barrier` for the xmin concurrency path; `IdentityService` with a fake `IGoogleTokenVerifier` — create / find by subject / 409 for an existing password account / parallel first sign-ins → one user (three different paths can fire: pre-existing login, Identity's duplicate check, unique index); handlers with fake `IIdentityService`; lockout counts. Note Identity itself uses `DateTimeOffset.UtcNow`, so lockout expiry cannot be faked.) (Blueprints from B4: value objects + `Habit` invariants incl. `HabitSchedule` equality and `HabitScheduleConverter` round trip for masks 1..127; race tests with two scopes and a `Barrier` inside a wrapping `IUnitOfWork`, which forces “both load, then both save”: Mark → same record, Unmark → `ConflictException`, diary create → `DuplicateEntryException`; DB check constraint `CK_Habits_Schedule`.) Domain unit tests (entities, value objects), Application handler tests (fake repositories), integration tests (`WebApplicationFactory` + Testcontainers Postgres), architecture tests (NetArchTest: Domain depends on nothing, Application not on Infrastructure).

- [ ] **B10 — Cleanup & delivery.** (Also: expired/revoked `RefreshTokens` rows are never deleted — add a periodic cleanup, e.g. a `BackgroundService` or a SQL job, and decide the retention; keep rows of rotated tokens for at least `RefreshDays`, otherwise replay detection forgets them.) Delete legacy projects (`HannaHabitsService/`, `UserService/`, `UserService.Tests/`, `HannasHabits.Data.Shared/`) after everything is ported, rewrite `README.md` (it still describes microservices), WebApi `Dockerfile` + `docker-compose.yml` (API + Postgres), GitHub Actions (build + test).

> Frontend can start after **B0–B6** are done (B7–B10 can run later or in parallel).

---

## Frontend (`/Users/iseaman/WebstormProjects/hannas-habits-ui`, separate repo)

Work with `--add-dir /Users/iseaman/WebstormProjects/hannas-habits-ui` or open Claude Code in that folder (then copy the relevant bits of CLAUDE.md there in F0).

- [ ] **F0 — Hygiene & secrets.** *First:* the frontend working tree has the user's **uncommitted old WIP** (`CreateHabit.jsx`, `HabitsTable.jsx`, `HabitTrackerContainer.jsx`, new `Dockerfile`, deleted `.devcontainer/`, `.idea/vcs.xml`) and local `main` is 6 commits behind `origin/main` (the user already deleted `client_secret_*.json` and rewrote the README on GitHub; the file still exists on disk locally). Ask the user what to do with the WIP (commit / stash / discard), then `git switch dev` (branch already exists, based on `origin/main`). Then: make sure `client_secret_*.json` is gone from disk + in `.gitignore` (also `.idea/`, `.DS_Store`), `.env.example` with `VITE_API_URL` and `VITE_GOOGLE_CLIENT_ID` (Google client id out of the source), remove unused deps (`@material-tailwind/*`, `react-calendar`; keep `date-fns` — will be used), remove all `console.log` (36, one leaks the Google token), Dockerfile: `npm ci` + nginx SPA fallback (`try_files … /index.html`). Add a short `CLAUDE.md` for this repo pointing to the backend one.

- [ ] **F1 — Tooling: TypeScript.** `tsconfig` (strict), rename `.js/.jsx` → `.ts/.tsx` incrementally, ESLint typescript config + Prettier, path alias `@/`, Vitest + React Testing Library, generate API types from the backend OpenAPI (`openapi-typescript`) with an npm script. Decide form library (Formik+Yup today; `react-hook-form` + `zod` is the TS-friendly option).

- [ ] **F2 — Design system from the mockup.** Using `docs/DESIGN.md` (and the mockup if needed): Tailwind 4 `@theme` tokens, fonts, light/dark, base UI components (`Button`, `Card`, `Modal`, `Field`, `Toast`, `ProgressBar`, …) in `src/shared/ui`, each small and typed.

- [ ] **F3 — App skeleton.** Feature-based folders (`src/features/{auth,habits,diary,calendar,resolutions}`, `src/shared/{ui,api,lib}`), router (react-router 7), providers: TanStack Query, Auth, Theme (one shared `ThemeContext` — current `useTheme` keeps separate state per component), one `ToastContainer`. **One** axios client: base URL from env, request interceptor adds the token, response interceptor handles 401 → refresh → retry → logout. **The refresh must be single-flight** (one shared in-flight promise; all parallel 401s wait for it): since B5 a refresh token works exactly once, and a second use — also a parallel one — counts as theft and revokes all sessions of the user (→ forced logout). Services throw errors (no more `console.log` + `undefined`).

- [ ] **F4 — Auth feature.** (Contract since B5: register/login/google/refresh all return `{ user: {id, userName, email}, tokens: {accessToken, refreshToken, accessTokenExpiresAt, refreshTokenExpiresAt} }`; errors are ProblemDetails — 400 `errors.<camelCase field>`, 401 wrong credentials/invalid token, 409 email taken, 429 locked out after 5 failed logins (15 min). **After a page reload one `POST /api/auth/refresh` returns user + new tokens** — no `/me` endpoint needed; store the refresh token, keep the access token in memory. `POST /api/auth/revoke` is idempotent (204 always). Google: send the ID token as `{ "idToken": … }` to `POST /api/auth/google`; a 409 means a *password* account with that email exists → tell the user to sign in with the password.) Login/register against the new contract, Google login (known bug: `GoogleButton.jsx` uses `useGoogleLogin` but reads `credentialResponse.credential` — that field only exists with the `GoogleLogin` component / ID-token flow; with `useGoogleLogin` it is `undefined`. Match it to what B5 verifies on the backend), token storage + refresh (access token short-lived), `ProtectedRoute`, logout; user info from the API, not from `localStorage` keys.

- [ ] **F5 — Habits feature.** (API contract since B4: `schedule` is `number[]`, `0 = Sunday … 6 = Saturday` — the same as JS `Date#getDay()` / date-fns `getDay`; optional on POST (omitted = every day), required on PUT.) Month tracker with `PUT/DELETE …/records/{date}` per click (optimistic update, no "Save all" loop), one `HabitForm` for create + edit (replaces the 90 % duplicate `CreateHabit`/`EditHabit`), delete confirm. Dates as `yyyy-MM-dd` (date-fns), never `toISOString()`.

- [ ] **F6 — Daily diary feature.** Route `/diary/:date` (replaces router `state` + the `id === -1` sentinel), form components get `value`/`onChange` instead of `setDailyDiary`+`setShowButton`, dirty state derived from form state, save with toast on success/error.

- [ ] **F7 — Calendar.** Year overview, days with entries highlighted (range query), click → `/diary/:date`.

- [ ] **F8 — Resolutions.** Fix the crash when a year has no resolution yet, create/update per year, stable list keys (no index keys).

- [ ] **F9 — Polish & delivery.** Loading/empty/error states, accessibility (labels, focus, keyboard — the icons are currently clickable `<img>`s), responsive check, component + hook tests, production Docker/nginx, README.
