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

## 2026-10-08 — B2 done (controllers & REST routes)

**Done:**
- **Migration `RemoveLegacyUsersAddHabitUserForeignKey`** (applied): drops `FK_Habits_Users_UserId` + the legacy `Users` table (1 legacy test row lost), adds `FK_Habits_AspNetUsers_UserId` (cascade). Decision: FK *to Identity's table*, configured in `HabitConfiguration` via `HasOne<ApplicationUser>().WithMany()` — Domain stays free of Identity, the DB guarantees referential integrity and deleting a user deletes their habits + records (verified). Snapshot is in sync again.
- **Migration `ClientGeneratedGuidKeys`** (applied, empty `Up`): `Id` is `ValueGeneratedNever()` in all three entity configurations. Reason: `EntityBase` assigns `Guid.NewGuid()` in the constructor; for a new `HabitRecord` that only reaches the context through the tracked `Habit.Records`, EF assumed "already exists" and issued an UPDATE → `DbUpdateConcurrencyException` (every `PUT …/records` returned 500, a bug older than B2).
- **Routes** (explicit lowercase, ids from route, payload from body via small request records in `WebApi/Models` with `ToCommand(...)`): `/api/auth/*`, `/api/habits[/{id}]`, `/api/habits/{habitId}/records[/{date}]`, `/api/daily-diaries[/{id}]`. `201 Created` + `Location` for POST, `204` for PUT/DELETE, `[ProducesResponseType]` everywhere; no body on GET/DELETE, no literal `[HttpGet("id")]`. Swagger JSON works again (200; was 500 from the duplicate `[HttpPut]`).
- **Records:** `GET …/records?from=&to=` (inclusive, optional, validated `from <= to`, filtered in the DB, ordered by date, `AsNoTracking`). `PUT …/records/{date}` is now idempotent (existing record is returned, `200`) instead of 409. `DELETE` of a day that is not marked stays 404.
- **Small fixes found while testing:** `HabitDetailsDto.CreatedAt` was `DateOnly` but the entity has `DateTime` → Mapster `InvalidCastException` (500 on `GET /api/habits/{id}`); now `DateTime` (UTC instant, no time-zone guessing). `CreateDailyDiaryDto` got an `Id` (needed for `Location`), `UpdateDailyDiaryCommand.Date` removed (the handler never used it).
- `HannasHabits.WebApi.http` rewritten: register/login/refresh → habits → records → diary → revoke (JetBrains HTTP Client syntax, response handlers store tokens/ids; host `https://localhost:7054`).

**Verified** (API on https profile, curl -k, throw-away users deleted afterwards incl. refresh tokens): full CRUD of habits/records/diaries with correct status codes, 400 (bad guid/date, `from > to`, empty title), 404 (unknown id), 401 (no token); user B gets 404 for every operation on user A's habit/records and an empty list; deleting user A removes their habit and records. DB is back to its previous state (1 user, 0 habits, 2 diaries).
**Not verified:** the `.http` file itself (no IDE here; it mirrors the curl calls). Dev certificate is still untrusted, so browsers/Rider's client may complain until `dotnet dev-certs https --trust`.

**Findings → ROADMAP:**
1. Validation key style differs: FluentValidation → `errors.title`, MVC implicit `[Required]` (property missing in body) → `errors.Title` (B5 note).
2. Two concurrent `PUT …/records/{date}` for the same day still hit the unique index → 500 (B4 note).
3. `DailyDiary.UserId` has no FK yet; one orphan test diary of the deleted legacy user blocks it (B6 note, needs the user's OK to delete).
4. `AuthController` still answers `400 "Invalid credentials"` as plain string for a failed login (should be 401 ProblemDetails) — B5 moves it into commands anyway.

**Next:** B3 — Repositories, Unit of Work, current user.

## 2026-10-08 — B3 done (repositories, unit of work, current user)

**Done:**
- **Application abstractions:** `ICurrentUser` (non-null `UserId`, throws `UnauthorizedAccessException` once → the `userId is null` check is gone from all 12 handlers), `IUnitOfWork`, command side `IHabitRepository` / `IDailyDiaryRepository` (tracked aggregates; every lookup takes the owner's id → data isolation is visible in the signature and still enforced in the query), read side `IHabitQueries` / `IDailyDiaryQueries` (project straight to DTOs). Interfaces live next to their feature (`Habits/`, `DailyDiaries/`), the generic ones in `Common/Interfaces`.
- **Infrastructure:** `Repositories/` (`HabitRepository`, `DailyDiaryRepository`), `Queries/` (`HabitQueries`, `DailyDiaryQueries`: `AsNoTracking` + `Select` into the DTO records), `Services/CurrentUser`. `ApplicationDbContext` implements `IUnitOfWork` (the DbContext already *is* a unit of work; the interface only keeps EF out of Application). `GetRecordsAsync` is one round trip and returns `null` when the habit is unknown/foreign (→ 404) vs. an empty list for a habit without records in range.
- **All 12 handlers** use repositories/queries + `IUnitOfWork`; no handler touches a DbContext or `Microsoft.EntityFrameworkCore` any more. Mapster only remains on the command side (`CreateHabitDto`, `CreateDailyDiaryDto`, `HabitRecordDto`); the now unused read-side mappings were removed.
- **Domain:** minimal `Habit.UnmarkCompleted(date)` (returns `bool`). Reason: with one repository per aggregate root there is no `HabitRecords` DbSet to remove a record from, so the aggregate must do it (EF deletes the orphaned required child). B4 refines it.
- **Packages:** `System.IdentityModel.Tokens.Jwt` moved from Application to Infrastructure (only `JwtTokenService` uses it); `FluentValidation.AspNetCore` dropped. That exposed two hidden transitive dependencies, now declared explicitly: `Microsoft.Extensions.Configuration.Abstractions` in Application and `<FrameworkReference Include="Microsoft.AspNetCore.App" />` in Infrastructure (it uses `IHttpContextAccessor`).
- Hardening on the way: a malformed user-id claim is now a 401 instead of a `FormatException` (500).

**Verified:** build 0 warnings/0 errors; `has-pending-model-changes` = none (no migration needed); end-to-end smoke test over HTTPS (43 checks, all PASS): habit CRUD, mark/unmark/re-mark days incl. idempotency and the DB row really deleted, records `from`/`to`/empty range/`from>to`, diary CRUD, 404 for every cross-user operation, delete cascade. Throw-away users deleted afterwards, DB back to 1 user / 0 habits / 2 diaries.

**Finished after the user's OK to delete:** removed the dead `IApplicationDbContext.cs`, `IUserContextService.cs` (Application) and `UserContextService.cs` (Infrastructure), then `Microsoft.EntityFrameworkCore` from `HannasHabits.Application.csproj`. Rebuilt (0 warnings), `dotnet list package --include-transitive` shows no EF Core in Application, smoke test re-run (all PASS), throw-away users deleted again.

**Next:** B4 — Domain hardening.

## 2026-10-08 — B4 done (domain hardening)

**Done:**
- **Value objects** (`Domain/ValueObjects`, `sealed record`s: value equality for free, so no `ValueObject` base class — that would be over-engineering here): `HabitTitle` (trimmed, not empty, ≤ `MaxLength` 150) and `HabitSchedule` (non-empty set of `DayOfWeek`, stored internally as a bit set so equality is order-insensitive; `Daily`, `Days`, `IncludesDay`). An instance can only exist if it is valid. The limits are `const`s reused by the FluentValidation validators and the EF configuration (one source of truth instead of three `150`s).
- **`Habit` aggregate:** `Title`/`Schedule` are value objects; `Schedule` defaults to every day; `Description` is trimmed, blank → `null`, ≤ 500 (`Habit.DescriptionMaxLength`). `Update(title, description, schedule)` replaces the old `Rename`/`UpdateDescription` wrappers. Records: `RecordOn(date)` (query), `MarkCompleted` throws `DomainException` for a day that is already completed, `UnmarkCompleted` (now `void`, B3's `bool` is gone) throws for a day that is not completed. `HabitRecord.Create` is `internal`, so a record can only come into existence through the aggregate.
- **Division of labour:** the Domain protects the invariants; the *API semantics* stay in the handlers — `PUT …/records/{date}` is idempotent (`RecordOn(date) ?? MarkCompleted(date)`), `DELETE` of an unmarked day is 404 (checked with `RecordOn` before). Validators stay as the first line with per-field messages (`errors.title`, `errors.description`, `errors.schedule`), the Domain enforces the same rules again.
- **Habit schedules ported** (`schedule: number[]`, 0 = Sunday … 6 = Saturday = JS `getDay()`; optional on POST → every day, required on PUT; in list/details/create DTOs; deduplicated + ascending). Not ported on purpose: legacy `Priority` (Normal/High) — decided in M1 with the design.
- **Persistence:** `HabitTitleConverter` / `HabitScheduleConverter` (`Persistence/Converters`). The schedule is **one `integer` bit mask** (Mon–Fri = 62, daily = 127) — the encoding lives only in the converter, the Domain exposes days. Check constraint `CK_Habits_Schedule` (`BETWEEN 1 AND 127`) mirrors the “at least one day” rule for rows that bypass the app. Migration `AddHabitSchedule` (applied): adds the column, hand-edited `defaultValue: 127` so existing rows become “every day” (the generated `0` would have violated the check constraint); no schema change for `Title`. Read side: queries project with `h.Title.Value` / `h.Schedule.Days` (EF evaluates those on the client in the final projection — works, but it is the price of value objects on the query side).
- **Races → no more 500:** `ApplicationDbContext` implements `IUnitOfWork.SaveChangesAsync` **explicitly** and translates a Postgres unique violation (`23505`) into the new `DuplicateEntryException : ConflictException` and `DbUpdateConcurrencyException` into `ConflictException` (both 409 through the existing handler). Explicit on purpose: ASP.NET Identity talks to the context directly and relies on the raw EF exceptions (its own concurrency handling), so it must not see translated ones. `MarkCompletedCommandHandler` catches `DuplicateEntryException` (two concurrent PUTs for the same day) and answers with the winner's record (read side, `AsNoTracking`), so the idempotent contract holds under concurrency.
- **Diary:** a second entry for the same user+day → 409 `ConflictException` via `IDailyDiaryRepository.ExistsForDateAsync` (instead of a DB exception). Honest note: “unique across all diaries” can't be enforced inside a single `DailyDiary`, so it is a repository check + the unique index as safety net; a domain service/specification would be over-engineering. B6 replaces this with upsert-by-date.
- **Aggregate size decision:** keep `Include(h => h.Records)` for mark/unmark. ~365 rows per habit and year are cheap for years, and a complete aggregate lets the Domain enforce rules across records later (streaks). Growth is absorbed on the read side (range query). Escape hatch if it ever hurts: `Include(h => h.Records.Where(r => r.Date == date))` — documented in `HabitRepository`.

**Verified** (API on https profile, throw-away users deleted afterwards): 42-check HTTP smoke test (default/explicit/deduplicated schedule, trimmed title, whitespace description, every validation boundary 150/151 and 500/501, update incl. missing/empty schedule → 400, idempotent mark, re-mark, 404 after unmark, 10 parallel PUTs × 6 days → always 200 + same record, 8 parallel DELETEs × 3 days → exactly one 204 and no 5xx, diary 409 + 8 parallel POSTs × 3 days → one 201 / seven 409, cross-user isolation, swagger.json 200). Because plain parallel curl rarely hits the tiny race window (the pre-checks caught nearly all), the race paths were additionally forced with a throw-away harness (two scopes, a `Barrier` in a wrapping `IUnitOfWork` so both load before either saves): 21/21 — including a counter proving the unique index was really hit 5×. Plus 38 domain/converter checks (equality, bounds, converter round trip for all masks 1..127) and the DB constraint (0 and 128 rejected, 127 accepted; rolled back). `has-pending-model-changes`: none. DB afterwards: 1 user / 0 habits / 0 records / 2 diaries. The harnesses were scratch code outside the repo; B9 turns them into real tests (blueprint in the ROADMAP).
**Not verified:** the `.http` file itself (no IDE here; it mirrors the curl calls).

**Findings → ROADMAP:** (1) `JwtTokenService` still uses `ApplicationDbContext` directly — B5 moves it behind abstractions. (2) Missing request properties still give PascalCase error keys for non-nullable request-record properties (`errors.Schedule` for a PUT without `schedule`) — already in B5's list. (3) Kestrel logs `SslStream … Bad address` “unhandled” connection errors when many parallel curl connections are aborted; harmless noise from the test client, not from the app.

**Next:** B5 — Auth into Application + Google login.

## 2026-10-08 — B5 done (auth into Application + Google login)

**Done:**
- **Auth is a use-case folder** (`Application/Auth/Commands/{Register,Login,GoogleLogin,Refresh,Revoke,RevokeAll}`, each `Command` record + `Handler` + `Validator`); `AuthController` only does `IMediator.Send`. New abstractions: `IIdentityService` (register, authenticate, external sign-in; Identity stays in Infrastructure), `IGoogleTokenVerifier`, `IJwtTokenService` reshaped; `IdentityUserDto`/`TokenPair` moved to `Application/Auth`. `ICurrentUser` replaces the claim parsing in the controller.
- **One response contract:** register, login, google and refresh all return `AuthResult { user, tokens }`. Refresh returning the user means a page reload needs one call, no `/me`.
- **Refresh tokens:** stored as SHA-256 hash only (plain SHA-256 is fine because the token is 512 random bits — unlike the passwords the removed unsalted hasher handled). Rotation with a correct `ReplacedByTokenId` chain (the old code stored an unrelated random string), **replay detection**: a used token presented again revokes every session of the user (logged as warning). Concurrent use of one token is caught with Postgres' `xmin` as concurrency token → the loser gets 401, never two valid successors. Plain logout is *not* a theft alarm. `RefreshToken` got behaviour (`Create`, `IsActive`, `Rotate`); it stays in Infrastructure (a security artefact, not part of the habit model).
- **Lockout:** `lockoutOnFailure: true`, 5 failures → locked 15 min → 429 ProblemDetails. Failed login is now 401 ProblemDetails (was a plain-string 400), same message for unknown email and wrong password.
- **`TimeProvider`** (singleton `TimeProvider.System`) instead of `DateTime.UtcNow` in the token service; the unused `ipAddress` parameters are gone (nothing stored them; also avoids keeping personal data we don't use).
- **Google login** `POST /api/auth/google { idToken }`: `GoogleJsonWebSignature` checks signature/expiry/issuer/**audience = `Google:ClientId`** and `email_verified`; `GoogleOptions` with `ValidateOnStart` (new user-secret `Google:ClientId`, value taken from the frontend source — it is public). New user = no password, `EmailConfirmed`, Google login attached in one transaction.
- **Decision — no silent linking:** a Google login whose email belongs to an existing *password* account gets 409, it is not linked. Registration doesn't confirm emails, so linking would let whoever pre-registered someone else's address keep access after the real owner signs in with Google (pre-hijacking). Proper options later: explicit "link Google" for a signed-in user, or email confirmation first.
- **Contract changes to be aware of:** `POST /api/auth/revoke` is idempotent (204 for unknown/already revoked/foreign tokens — `NotFoundException` would have echoed the token into the log, and logout shouldn't fail on an expired token; a foreign token is silently ignored, so no information leaks); only the caller's own tokens can be revoked. The one validation path: `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes` → a missing property is now `errors.title`/`errors.schedule` (camelCase, FluentValidation) instead of `errors.Title`.
- **Migration `HashRefreshTokens`** (applied): `Token`/`IsRevoked`/`ReplacedByToken` → `TokenHash` (unique), `RevokedAt`, `ReplacedByTokenId`; index on `UserId`; FK to `AspNetUsers` (cascade). Hand-edited: **deletes the existing rows first** (2 rows, plain-text tokens can't become hashes; an empty hash would also break the unique index) → every session must sign in once more; the generated `AddColumn xmin` no-op was removed (system column, Npgsql emits no SQL for it).
- Google-library quirk found by the test: for input that is not a JWT it throws raw Newtonsoft `JsonReaderException` (→ would have been a client-triggerable 500); the verifier now maps everything except transport errors to 401.
- Docs: CLAUDE.md (abstractions, config key, stale pre-B3 remarks about `IApplicationDbContext` fixed), `.http` (Google request, single-use note), ROADMAP notes for F3/F4/B9/B10.

**Verified:** build 0 warnings; `has-pending-model-changes` none. HTTP smoke test (78 checks, all PASS, throw-away users deleted): hashed storage (64 chars, = SHA-256 of the token, raw token absent), register 200/409 (also other case)/400 (weak password, bad email, `{}`, no body), login 200/401/401-same-message/400, lockout 4×401 → 429 → correct password still 429, other users unaffected, rotation chain in the DB, replay → 401 + successor + other session + all revoked, 10 parallel refreshes of one token → exactly one 200 and nine 401, revoke/revoke-all incl. isolation between users, Google invalid/empty/missing/forged → 401/400, regression of habits endpoints + swagger. Scratch harness for `IdentityService` against the real DB (15 checks, 8 runs): create/find/same subject with changed email/409 for Google and password accounts without side effects/Google-only user cannot password-login/8 parallel first sign-ins → one user, one login/8 parallel registrations → one ok + seven `ConflictException`. The harness first **failed 3 of 8 parallel sign-ins** (conflict raised by the email pre-check, then by Identity's own duplicate check, although it was the same person) → create path simplified to one flow: any duplicate while creating → look the Google login up again, else 409.
**Not verified:** a real Google ID token (needs a browser sign-in; first end-to-end test comes with F4). Which of the inner race paths fired in the harness is not instrumented; B9 forces them with a `Barrier`.

**Findings → ROADMAP:** (1) F3 refresh must be single-flight (stricter than before: one parallel refresh = forced logout). (2) Refresh-token rows are never cleaned up (B10). (3) Optional "link Google to my account" endpoint if Hanna's existing password account should get Google login. (4) Identity's default password policy (≥ 6 chars) is weak; tighten with `options.Password` if wanted. (5) Access tokens live ~20 instead of 15 minutes because of JwtBearer's default 5-minute clock skew (`ClockSkew = TimeSpan.Zero` if it matters).

**Next:** M1 needs the `claude_design` MCP (see ROADMAP) — if it is not connected, the next buildable backend step is B6 (daily diary); the frontend can start after B6.

## 2026-10-08 — M1 done (mockup → design brief)

**Done:**
- Read the claude.ai/design project "Hannas Habits UI-Redesign" and wrote **`docs/DESIGN.md`**: design language ("Organic": warm, pill/circle shapes, Caprasimo + Figtree, Lucide stroke 2.75), tokens for light **and** dark (roles, three ramps — dark = light reversed —, app-specific mood scale `--hh-m0..4` and `--hh-miss`), shell + routes, all five screens (auth, Today/diary, Habits grid + new-habit dialog, Calendar, Resolutions), component inventory for F2, an **API-needs table** (exists / change / new), decisions, and a list of gaps and prototype shortcuts not to copy.
- **How it was read:** the session had no `claude_design` MCP server (`/mcp` showed only Claude Docs + Google Drive), but after `/design-login` the `DesignSync` tool could read the project (`get_project`/`list_files`/`get_file`; the project is a normal project, not a design-system project). Strictly read-only; nothing was written to the project. `_ds_bundle.js` is empty and `support.js` is only the prototype runtime.
- **Decisions:** (1) legacy `Priority` is **dropped** (the design has no priority). (2) `YearResolution.Summary` is **not ported** (no place in the design). (3) The diary is **one document per user+date with autosave** (the "Saved" tag, no save button) → full-document `PUT /api/daily-diaries/{date}`; `mood` becomes nullable and `Text` becomes the optional `highlight`. (4) **Streak is shown on two screens → B8 is required**, not optional; it must be server-side because a streak spans months.
- **Roadmap changes:** new small step **B5b — display name** (register field "Your name" + sidebar); B6 rewritten around the document/autosave model and the slim calendar projection `[{ date, mood }]`; B7 gets the resolution→habit link and the empty-year 200; B8 becomes "habit overview + streaks" with `Habit.CurrentStreak(asOf)`, `StartDate` and `GET /api/habits/overview?from=&to=&asOf=`; F2/F4–F8 got the design-specific notes; the dependency note says F4←B5b, F5←B8, F6/F7←B6, F8←B7.

**Findings from the mockup (all in DESIGN.md §8/§9):** (1) A *new* habit would show every past scheduled day as "missed" → needs a `StartDate` (not `CreatedAt`: UTC timestamp, off-by-one in the user's local date). (2) The schedule has no history, so editing it re-judges past cells and streaks — accepted limitation, modelling schedule versions would be over-engineering. (3) Not designed: month/year navigation, edit habit, delete confirms, loading/empty/error states, toasts, entry-without-mood colour, habit picker for resolutions, any mobile layout (fixed 240 px sidebar, 1060 px grid). (4) The prototype's interactive `div`s must become real buttons/inputs (accessibility, F9). (5) The year-progress donut and "days left" are client-side (the mockup hard-codes 77 % / 85 days = day 280 of 365).

**Not verified / not done:** docs only — no code, no build needed. The other two files of the project (`Hannas Habits.dc.html`, `Sidebar.dc.html`, recreations of the old UI) were not read. Two things were not decided and are listed for the sessions that need them: field length limits and the "empty entry is deleted" rule (B6), and whether marking a future/unscheduled day is also rejected on the server (B8).

**Next:** **B5b** (small: display name) then **B6** (daily diary); B8 before F5, B7 before F8. Frontend F0–F3 do not depend on any backend step and could run in parallel.

## 2026-10-08 — B5b done (display name)

**Done:**
- **`ApplicationUser.DisplayName`** (`string?`, `varchar(100)`, `AuthLimits.DisplayNameMaxLength` = one source for validator + EF config). `SetDisplayName` trims, blank → `null`, and cuts a too-long name instead of rejecting it (never inside a surrogate pair — a lone surrogate is invalid UTF-8): the register validator already refuses > 100, so only a *provider's* name gets there and a Google sign-in must not fail over a long name. `GetDisplayName()` = chosen name, else the email's local part. `null` in the DB means “none given”; the fallback is computed on read, not stored.
- **API contract:** `POST /api/auth/register` takes optional `displayName` (`RegisterCommand`/`RegisterRequest`, validator max 100 → 400 `errors.displayName`); `AuthResult.user` is now `{ id, userName, email, displayName }` for register, login, google and refresh. `displayName` is never empty.
- **Google:** `GoogleTokenVerifier` passes the `name` claim in `ExternalIdentity.DisplayName`. A new account takes it. An existing account **without** a name gets it filled in on its next Google sign-in (accounts created before B5b); an existing name is never overwritten (a rename feature may come later). The fill-in is best effort: if the update loses a race, the user entity is detached from the context — otherwise the token service's `SaveChanges` in the same scope would hit the failed, still “modified” entity and the login would end in a 500.
- **Refactor on the way:** `IdentityUserDto` was built by hand in two places (`IdentityService.ToDto`, `JwtTokenService.RefreshAsync`); now one internal extension `ApplicationUserMapping.ToDto()` — a new field can no longer be forgotten in one of them.
- **Migration `AddDisplayName`** (applied): one nullable column, no data change. `has-pending-model-changes`: none.
- Docs: `.http` (register body), ROADMAP (box + F4 contract note), DESIGN.md (“done”).

**Verified:** build 0 warnings. HTTP smoke test (26 checks, all PASS, 7 throw-away users deleted): register with name (trimmed) / without / blank / `null` → fallback to the local part, login + refresh return the name, 100 chars ok / 101 → 400 `displayName` and **no** account created, umlauts + emoji round trip, `displayName: 123` → 400, duplicate email still 409, PascalCase property names bind. Scratch harness for `IdentityService.SignInWithExternalAsync` against the real DB (22 checks, 3 runs, all PASS): new user with/without/blank name, 150-char name cut to 100, emoji across the boundary cut to 99 (valid text), emoji inside the limit kept, back-fill of a nameless account, no overwrite of an existing name, 8 parallel sign-ins of a nameless account → all succeed, and a **forced lost race** (a `DbCommandInterceptor` bumps the `ConcurrencyStamp` right before Identity's UPDATE) → login still succeeds, the same scope can `SaveChanges` afterwards, nothing written. Mutation check: without the `Detach` line that last check fails with `DbUpdateConcurrencyException` — so the test really covers it. DB afterwards: 1 user / 0 habits / 0 refresh tokens (as before).
**Not verified:** a real Google ID token (unchanged since B5; first end-to-end test comes with F4) — in particular that the real token carries the `name` claim (it needs the `profile` scope, which the standard Google sign-in requests). The harness is scratch code outside the repo; B9 turns it into real tests.

**Next:** **B6** (daily diary, extended) — it asks the user before deleting the orphan test diary.
