# Design brief — Hanna's Habits

Written in roadmap step **M1** (2026-10-08) so later sessions read this file instead of re-importing the mockup.

- **Source:** claude.ai/design project "Hannas Habits UI-Redesign" (`a388ae78-c586-4176-a236-83ee88bef383`), file `Hannas Habits App.dc.html` (one interactive prototype with all screens), design system "Organic" (`_ds/organic-…/styles.css`, `readme.md`). Project last synced from the old frontend on 2026-10-07. `_ds_bundle.js` is empty (no components) and `support.js` is only the prototype runtime — neither carries design information. The project also holds `Hannas Habits.dc.html` and `Sidebar.dc.html` (recreations of the *old* screens, not read).
- **How it was read:** `DesignSync` read methods after `/design-login` (the `claude_design` MCP server itself was not configured in the session).
- The prototype is demo data + inline styles. It is a **visual reference, not code to copy**: rebuild it with Tailwind 4 tokens and typed components (F2+). Where it is wrong or incomplete, see "Gaps and deviations" below.

## 1. Design language ("Organic")

Warm, rounded, a little playful. Cream-and-sand ground, terracotta accent, sage second accent. Caprasimo display headings over Figtree body. Over-rounded containers, **pill** buttons/inputs/tags (`999px`), circles as the main shape (habit cells, mood buttons, resolution numbers, calendar days). Left-aligned layouts with air; no hairline-only geometry, no greys (warmth is the point). Icons: **Lucide, stroke-width 2.75**. Interactive states are themed (hover tint, pressed = one ramp step further, 2px accent `:focus-visible` ring) — never browser defaults. Disabled = 45 % opacity.

## 2. Tokens

### 2.1 Roles

| Token | Light | Dark |
|---|---|---|
| `bg` | `#f5ead8` | `#1d1a17` |
| `surface` | `#ebddc5` | `#2a2520` |
| `text` | `#201e1d` | `#f3e8d6` |
| `accent` (terracotta) | `#c67139` | `#d67f48` |
| `accent-2` (sage) | `#7a8a5e` | `#8fa073` |
| `divider` | `text` @ 16 % | `text` @ 18 % |

### 2.2 Ramps (100 → 900)

Three ramps, generated on one lightness scale. **The dark ramps are the light ramps reversed** (dark step *n* = light step *10−n*), so define the light values once and flip the order under `[data-theme="dark"]`.

| Step | neutral | accent | accent-2 |
|---|---|---|---|
| 100 | `#f9f4ed` | `#fff2eb` | `#f0fae1` |
| 200 | `#eee7db` | `#ffe1d0` | `#e1eecc` |
| 300 | `#dcd3c4` | `#ffc6a5` | `#ccdbb2` |
| 400 | `#c0b6a5` | `#f6a06b` | `#aebf92` |
| 500 | `#a19786` | `#d67f48` | `#8fa073` |
| 600 | `#82796a` | `#b2622d` | `#728157` |
| 700 | `#645c50` | `#8c491a` | `#56633f` |
| 800 | `#474238` | `#643312` | `#3d472b` |
| 900 | `#2e2b25` | `#402310` | `#272e1b` |

Usage: 100–300 tinted fills/hovers/borders, 500 base, 700–900 text on tints and pressed states. Paragraph-size accent text uses `accent-700`, not `accent` (contrast). Links: `accent-700`, hover `accent-800`.

### 2.3 App-specific tokens (not in the design system — defined in the prototype)

Mood scale (calendar days + mood picker) and habit "missed":

| Token | Meaning | Light bg / fg | Dark bg / fg |
|---|---|---|---|
| `--hh-m0` / `--hh-f0` | Great | `#728157` / `bg` | `#aebf92` / `#1d1a17` |
| `--hh-m1` / `--hh-f1` | Good | `#ccdbb2` / `#272e1b` | `#56633f` / `#f3e8d6` |
| `--hh-m2` / `--hh-f2` | Okay | `#dcd3c4` / `#2e2b25` | `#a19786` / `#1d1a17` |
| `--hh-m3` / `--hh-f3` | Low | `#ffc6a5` / `#402310` | `#b2622d` / `#f3e8d6` |
| `--hh-m4` / `--hh-f4` | Rough | `#d67f48` / `bg` | `#ffc6a5` / `#1d1a17` |
| `--hh-miss` | habit cell "missed" | `#ffe1d0` | `#8c491a` |

**Implemented in F2** (`src/shared/ui/theme.css` in the frontend; use these, not the mockup's names): roles `bg`, `surface`, `text`, `accent`, `accent-2`, `divider` (+ `accent-hover`, `accent-pressed`, `backdrop`); ramps `neutral-*`, `accent-*`, `accent-2-*` (dark = mirrored, automatically); mood fills `mood-great|good|okay|low|rough` with their text colour `on-mood-*` (= mockup `--hh-m0..m4` / `--hh-f0..f4`, **index 0 = great = API value 5** — the names avoid that flip); `miss` (= `--hh-miss`). Utilities: `bg-mood-great text-on-mood-great`, `bg-miss`, `rounded-card`, `font-display`, `text-page|dialog|card|kicker`.

### 2.4 Type, space, radius, elevation

- **Fonts** (Google Fonts): `Caprasimo` 400 for headings/buttons/numbers, `Figtree` 400/600/700 for body. Base 15 px / 1.55. Headings: line-height 1.12, letter-spacing −0.015em. Page title (`h1`) 44 px; login hero up to 88 px; card titles 17–19 px; dialog title 26 px.
- **Kicker** above page titles: 12 px, uppercase, tracking 0.1em, bold, `accent-700`.
- **Space** (1.10× density): 4.4 / 8.8 / 13.2 / 17.6 / 26.4 / 35.2 px (`space-1,2,3,4,6,8`). The prototype mostly uses literal px (card padding 22–26, page padding 40/44, gaps 18/22/26) — round to the scale where it does not change the look.
- **Radius:** sm 8, md 16, lg 28; cards and dialogs `lg × 1.15 ≈ 32 px`; sidebar 32; buttons/inputs/tags/pills 999.
- **Shadow:** sm `0 1px 2px`, md `0 3px 10px`, lg `0 12px 32px` of `neutral-900` @ 14/16/22 %. Dark: black @ 40/45/55 %.
- **Theme:** `data-theme="light|dark"`, persisted in `localStorage['hh-theme']`, default light (the prototype ignores `prefers-color-scheme` — F2 may default to the system setting). `color-scheme: dark` in dark. Dark overrides: primary-button hover = `accent-400`; dialog backdrop black @ 55 %.

## 3. App shell and routes

Two-column layout, full viewport height: **sidebar** 240 px (floating `surface` panel, radius 32, 14 px outer margin) + scrolling `main` (padding 40/44).

Sidebar (top → bottom): logo (sage circle with check icon + "Hanna's Habits" in two lines) → nav pills **Today** (sun), **Habits** (list-checks), **Calendar** (calendar), **Resolutions** (target) — active = `accent` fill + `bg` text → *(spacer)* → light/dark segmented switch (sun / moon) → user card (initial avatar, **display name**, **email**, log-out icon button).

| Route | Screen | Nav item |
|---|---|---|
| `/login` (login + register as one screen with a mode toggle) | Auth | — |
| `/diary/:date` (`yyyy-MM-dd`); `/` redirects to today's date | Today / Daily diary | Today |
| `/habits` | Habit month tracker | Habits |
| `/calendar` | Year calendar | Calendar |
| `/resolutions` | Year resolutions | Resolutions |

"Today" always jumps to the real today, also from a past diary day. "New habit" is a modal dialog over `/habits`, not a route.

## 4. Screens

All dates are the **client's local date** as `yyyy-MM-dd` (date-fns, never `toISOString()`); "today" is the client's, not the server's.

### 4.1 Auth (login / register)

Split screen: left a hero ("Hanna's Habits", tagline "Daily reflections, habits, and goals in one place.", three tags Daily diary / Habits / Resolutions) over decorative circles (terracotta-200, sage-200, a small sage-500 dot); right a card with the form. Light/dark switch top right.
- Login: Email, Password, **Log in**, divider "or", **Continue with Google**, link "New here? Create an account".
- Register adds a field **Your name** (→ display name) and the button reads **Create account**.
- Needs from the API: `AuthResult` (exists) + **display name** (done, B5b).
- Not designed: field validation errors, 401 / 409 / 429 (lockout) messages, loading state, forgot password. Use the same pill inputs with the error text under the field and a form-level message from the ProblemDetails `title`/`detail`.

### 4.2 Today / daily diary (`/diary/:date`)

Header: kicker "Daily diary", `h1` = "Wednesday, 7 October", a **Saved** tag (sage, check icon), prev/next-day icon buttons. Next is disabled on today (no diary for the future).

Cards, in grid order:
1. **Mood** — five circle buttons (50 px) with Lucide faces `laugh`, `smile`, `meh`, `frown`, `annoyed`; selected = filled with the mood colour (`--hh-m*`), others `bg` with `neutral-700` icon; label of the selection top right ("Great", "Good", "Okay", "Low", "Rough"). Radio-group semantics.
2. **Body** — big percentage (Caprasimo 22 px, sage) + slider 0–100, ends labelled **Drained** / **Energised**.
3. **Mind** — same, accent colour, ends **Foggy** / **Clear**.
4. **Highlight of the day** — borderless textarea inside the card, placeholder "What made today worth remembering?".
5. **Grateful for** and **Something I learnt** — two cards, each a list of short lines with a coloured dot (sage / accent-400) and a last ghost row "Add something…" (inline add).
6. **Today's habits** (sage-200 card, right column) — habits **scheduled on the viewed weekday**: round check (28 px, filled sage when done / sage ring when open), title, flame icon + streak number; header link "N of M →" goes to `/habits`. Click toggles the record for the *viewed* date. (The streak shown is always the current streak as of the real today, not of the viewed day.)
7. **Tasks** — checklist (rounded-square checkbox 22 px, done = accent fill + strikethrough), counter "N done", input row "Add task and press Enter".

There is **no save button**: the "Saved" tag implies **autosave** (F6: debounced write, one request in flight, status `saving… / saved / error`).

Data per day (document `PUT`): `mood` (1–5 or none), `body` 0–100, `mind` 0–100, `highlight`, `grateful[]`, `learned[]`, `tasks[{title, done}]`; plus habits-for-date (see §5).

### 4.3 Habits (`/habits`) — month tracker

Header: kicker "Habits", `h1` = "October 2026", primary button **+ New habit**. One card with a horizontally scrolling table (min-width ≈ 1060 px): header row = a column per day (pill with weekday letter + number, today highlighted in accent) and a "Streak" column; each habit = row with title, schedule label ("Every day", "Weekdays", "Weekends", else "Mon · Wed · Fri"), a trash icon button, 22 px circular day cells and a flame icon + streak number (Caprasimo 18 px).

Cell states: **done** (sage-500 fill + check) · **missed** (`--hh-miss` fill: scheduled, past, not done) · **due today** (2.5 px accent ring) · **future scheduled** (1.5 px neutral ring, not clickable) · **not scheduled** (small neutral dot, not clickable). Past and today cells toggle on click. Legend row below: Done / Missed / Due today / Not scheduled.

**New habit dialog:** title field ("What do you want to do?", placeholder "e.g. Stretch for 10 minutes"), seven round weekday toggles **M T W T F S S** (Monday first), preset tags **Every day / Weekdays / Weekends**, buttons Cancel and **Add habit · N×/week** (disabled without a title or without a day). UI index 0..6 = Mon..Sun ↔ API `(i + 1) % 7` (0 = Sunday).

### 4.4 Calendar (`/calendar`) — year overview

Header: kicker "Calendar", `h1` = "2026", legend of the five mood colours. Grid of 12 month cards (min 250 px, `surface`; the current month `neutral-100`): month name (Caprasimo), "N entries" (hidden if 0), a 7-column **Monday-first** grid of 26 px day circles. Day = diary entry → filled with its **mood colour**; past day without entry → 1 px `neutral-300` ring; future day → plain number in `neutral-500`; today → extra accent ring. Click a day → `/diary/:date`.

### 4.5 Resolutions (`/resolutions`)

Header: kicker "Resolutions", `h1` = "My 2026". Left card: numbered rows (44 px circle with the number — sage when kept, accent-200 when open), title (17 px bold), optional link line "Tracked by “<habit>”" (list icon, goes to `/habits`), and on the right a **Kept** tag (sage) or **Mark kept** secondary button (toggle). Last row: dashed-circle "+" and an input "Add a resolution and press Enter". Right card (260 px): a **donut** (conic gradient, accent over neutral-300) with the percentage of the year elapsed, caption "of 2026 is behind you", and "N of M resolutions kept so far — X days left." The percentage and days left are **computed on the client from today's date** (the prototype hard-codes 77 % / 85 days = day 280 of 365).

## 5. What the design needs from the API

Legend: **exists** · **change** (exists, needs a change) · **new**.

| Need (screen) | Status | Where |
|---|---|---|
| Register/login/Google/refresh/revoke, `AuthResult { user, tokens }` | exists | B5 |
| **Display name** (register field "Your name", sidebar): `displayName` on `RegisterCommand` (optional), from the Google `name` claim, in `AuthResult.user` (`{ id, userName, email, displayName }`) | done | **B5b** |
| Habits CRUD, `schedule: number[]` (0 = Sunday), mark/unmark `PUT/DELETE …/records/{date}` | exists | B2/B4 |
| Habits **overview** in one call for the month grid and the Today panel: `GET /api/habits/overview?from=&to=&asOf=` → `[{ id, title, schedule, startDate, completedDates[], currentStreak }]`. Grid: `from/to` = month; Today panel: `from = to = viewed date` (client filters by schedule). One query instead of 1 + N requests; streak must be server-side because it can span months. `from`/`to` required, `asOf` optional (default = server UTC date). | done | **B8** |
| **Current streak** (pure domain logic): walk back from `asOf` over *scheduled* days only; unscheduled days neither count nor break; `asOf` itself counts if done and is ignored (not a break) if still open; the first earlier scheduled day that is not done ends the streak; stop at `startDate`. Records after `asOf` and on unscheduled days are ignored. | done | **B8** |
| **Habit `startDate`** (`DateOnly`): the prototype draws every scheduled past day of a *new* habit as "missed". Cells before `startDate` must render as not-applicable, and streak/stats must stop there. `EntityBase.CreatedAt` is a UTC timestamp and can be off by a day in the user's local date, so use an explicit date (client sends it, default = server UTC date; optional on create; range 2000-01-01..2100-12-31; not editable yet). | done | **B8** |
| Daily diary as one document per user+date: `GET/PUT/DELETE /api/daily-diaries/{date}`; fields `mood` (1–5, nullable), `body`/`mind` (0–100, nullable), `highlight` (nullable), `grateful[]`, `learned[]`, `tasks[{title, done}]`. Replaces `Text` (required) — an entry may now consist of just a mood. | done | **B6** |
| Calendar range query `GET /api/daily-diaries?from=&to=` → `[{ date, mood }]` (slim: the colour needs the mood, not the whole entry) | done | **B6** |
| Resolutions per year; each item: `title`, `kept`, optional **habit link** (`habitId` + resolved title); stable order; `GET /api/resolutions/{year}` answers 200 with an empty list for a year without items (`[{ id, title, kept, habitId, habitTitle }]`; add/update/delete under `/api/resolutions/{year}/items[/{id}]`) | done | **B7** |
| Year progress donut / days left | — | client only |

Mood mapping (UI ↔ API ↔ domain `Mood`): Great = 5 `Excellent` · Good = 4 `Good` · Okay = 3 `Ok` · Low = 2 `Bad` · Rough = 1 `Terrible`. The API carries the number (higher = better); labels are UI wording. Prototype index 0 = Great.

## 6. Component inventory (input for F2 / feature folders)

`shared/ui`: `Button` (primary / secondary / ghost / icon / block), `Tag` (accent / accent-2 / neutral / outline), `Card`, `Field` + `Input` (pill) + `Textarea`, `Dialog` (backdrop + panel + title/body/actions), `PageHeader` (kicker + h1 + actions), `ThemeSwitch` (segmented sun/moon), `Slider` (native `<input type="range">` styled as the pill track with the ringed thumb), `ProgressDonut`, `Toast` (not designed, needed), `Spinner`/skeleton (not designed, needed).
Feature-level: `Sidebar` + `NavItem` + `UserCard`, `MoodPicker`, `ChipList` (dot list with inline add), `TaskList`, `HabitCheck` (28 px round toggle), `StreakBadge` (flame + number), `HabitGrid` + `HabitRow` + `DayCell`, `WeekdayToggle` (+ presets), `MonthCard` + `DayDot`, `ResolutionRow`, `SavedIndicator`.

## 7. Decisions made in M1

- **`Priority` (Normal/High) is dropped** — the design has no priority anywhere; it is not ported and disappears with the legacy projects (B10).
- **`YearResolution.Summary` is not ported** — no place for it in the design (YAGNI); it can be added later with the screen that shows it.
- **Streak is required**, so **B8 is no longer optional** and F5/F6 depend on it (see ROADMAP).
- **Diary is autosave-by-date** (no save button, no ids in the frontend): the document is addressed by date; full-document `PUT`.
- Legacy icon set (`src/assets/icons/*`, `Beleriand.ttf`) is replaced by Lucide + Caprasimo/Figtree.

## 8. Gaps and deviations (decide or fix while implementing)

Not designed in the prototype — needed anyway:
- **Month navigation** on Habits and **year navigation** on Calendar (headers are static text).
- **Edit habit** (only the create dialog exists; the same dialog can serve, F5), delete confirmation, a **description** field (the API has one, max 500; the dialog has none — keep optional or drop from the UI), the 150-char title limit.
- Edit/delete for tasks, grateful/learned items and resolutions; a **habit picker** to link a resolution to a habit (only pre-linked seed data is shown).
- Loading / empty / error states, toasts, autosave failure, offline.
- An entry with **no mood** (calendar colour): the prototype always has one. Proposal: filled `neutral-200` with `neutral-800` number.
- **Responsive/mobile**: the sidebar is a fixed 240 px and the habit grid has `min-width: 1060px` — a small-screen layout (bottom nav or drawer) is part of F9.
- The habit grid always renders 31 day columns; render the real month length.
- The dark theme is complete for roles and ramps, but check contrast of the mood colours on `surface` in dark.
- **Contrast (measured in F2):** light theme `bg` text on `accent` (primary button, active nav pill) is **3.0 : 1** (3.8 on the hover colour), `bg` on sage-600 3.5 : 1 — below WCAG AA for normal-size text (4.5), fine only for large text. The design's colours were kept; if it matters, use `accent-700` (≈ 6 : 1) as the button fill or darken `accent` — a design decision for the user. Dark theme and `accent-700` text on `bg`/`surface` pass (5.1–14 : 1).

- **Contrast of the habit cells (measured in F5, against the card `surface`):** light theme — *missed* 1.08 : 1 (practically invisible), *upcoming* ring 1.5 : 1, *done* fill 2.1 : 1, *due-today* ring 2.7 : 1, check icon on *done* 2.4 : 1; all below the 3 : 1 that WCAG 1.4.11 asks of graphics. Dark theme is fine except *missed* (2.2 : 1) — upcoming ring 3.5, done 5.4, due-today 5.1, check icon 6.1. The states are also told apart by shape (filled / ring / dot) and every cell has a text label, so nothing depends on colour alone *except missed vs. open-past*, which differ only by that pale fill. Design colours kept; if it matters, darken `--hh-miss` in the light theme (e.g. `accent-300`/`400`) and use a darker `done` — a design decision for the user.

- **Clear buttons (decided in F6):** the diary can be partly empty — mood, body and mind are nullable, and a day with nothing in it is deleted by the server. The mockup has no way to take a chosen mood or a slider value back (a radio group cannot be un-chosen), so each of the three cards got a small ✕ button next to its value, and an untouched slider rests dimmed at the middle with a dash instead of a number. Without them a day could never become empty again.
- **Add rows** (grateful / learned / tasks) add on Enter **and when the input loses focus**, and every line can be edited in place and removed (✕ on hover or focus); a line left blank is removed on blur. None of it is in the mockup.
- The habits card is titled **“Today’s habits”** on today and **“Habits of the day”** on any other day; the diary header has no “Today” button (the sidebar's Today does that).

Prototype shortcuts not to copy:
- Interactive `div`/`span` with `onClick` everywhere → real `button`s / `role="radio"` / `role="checkbox"` / `<input type="range">` with labels and keyboard support (the old UI had clickable `<img>`s — F9 lists this too).
- Inline styles and hard-coded demo data, `localStorage` only for the theme, "carry" streak offset (demo seed), fake login that accepts anything.
- The habit streak and the Today panel's "N of M" are computed in the prototype from local state; in the app they come from the overview query.

## 9. Known limitation worth stating

The schedule has no history: if a habit's schedule is edited, past cells are re-judged with the *new* schedule (missed/not-scheduled and the streak change retroactively; records on days that are no longer scheduled are kept but hidden and not counted). Modelling schedule versions would be over-engineering for this app; revisit only if it annoys in practice.
