# Road Accident Analysis Dashboard — C# Rebuild

**Date:** 2026-08-15
**Status:** Approved for planning
**Constraint:** Under one week to completion.

## Context

The existing project is a PHP + SQLite application with a static HTML dashboard.
It has three defects that matter more than any code-quality issue:

1. **There is no accident data.** `users` is the only table. Every figure on the
   dashboard is a hardcoded JavaScript literal — monthly totals, hotspot
   coordinates, and the "real-time" feed alike. The README promises a tool that
   "transforms raw crash data into actionable insights"; nothing is transformed.
2. **`predict.php` performs no prediction.** It multiplies three fixed factors
   (`3 × area × month × weather`) and can return only six distinct scores. Its
   own comment says "for demo purposes."
3. **`app.py` has never run.** It loads `accident_model.pkl` and renders
   `templates/index.html`; neither file exists in the repository. It crashes on
   import, and no PHP file calls it. The project appears to have a Python ML
   component that does not exist.

A fourth constraint decided the stack: **PHP is not installed on the development
machine.** The existing application cannot be run or debugged locally. `dotnet`
(10.0.301 and 9.0.306) and Python 3.13.14 are both available.

## Decision

Rebuild the web application in ASP.NET Core. Retain Python for model training
only. Retire all `.php` files and `app.py`.

Auth in C# is not a contained change: PHP sessions and ASP.NET cookie
authentication cannot gate each other, so moving login to C# moves the whole web
layer. This is accepted deliberately, not incidentally.

### What survives

The dashboard's HTML, CSS, and JavaScript — Chart.js, Leaflet, the theme toggle,
and the eight-section navigation — carry over as Razor views and `wwwroot`
assets. The feature map and the `users` schema carry over unchanged.

### What is removed

`index.php`, `login.php`, `logout.php`, `register.php`, `create-account.php`,
`dashboard.php`, `db.php`, `predict.php`, `seed_db.php`, and `app.py`. The
tracked `database.sqlite` is untracked and gitignored.

## Phases

The three phases are sequential and independently demonstrable. If time runs
out, it must run out inside phase 3, never inside phase 1.

| Phase | Deliverable |
|-------|-------------|
| 1 | Authentication — login, register, logout, an authorized empty dashboard |
| 2 | Dashboard UI reading real accident data from SQLite |
| 3 | Trained model replacing the fake prediction |

---

## Phase 1 — Authentication

### Project structure

One project, deliberately flat. Falcon's Clean Architecture layout
(ApplicationCore / SharedKernel / Infrastructure) is explicitly **not** copied:
that structure costs several days of scaffolding before the first login screen,
which this deadline cannot absorb.

```
RoadSafety.Web/                 .NET 10 (LTS), Razor Pages
  Program.cs                    auth wiring
  Data/AppDbContext.cs
  Data/User.cs
  Pages/Index.cshtml(.cs)       login
  Pages/Register.cshtml(.cs)
  Pages/Dashboard.cshtml(.cs)   [Authorize]
  Pages/Shared/_Layout.cshtml
  wwwroot/                      css/js carried over from the PHP project
RoadSafety.Tests/               xUnit
```

.NET 10 over .NET 9 because it is the LTS release.

The project is created inside the existing repository, and the PHP files are
deleted in the same commit, so history records a deliberate migration rather
than an abandoned codebase.

### Data model

The `User` entity preserves the existing schema exactly:

| Field | Type | Notes |
|-------|------|-------|
| `Id` | int | identity |
| `FullName` | string | required |
| `ForceNumber` | string | required, **unique**, login identifier |
| `Email` | string | required, unique |
| `PasswordHash` | string | required |
| `Station` | string | required |
| `CreatedAt` | DateTime | UTC |

Force-number-as-login is retained: it is correct domain modelling for a police
system, not an accident of the original code.

EF Core with SQLite, one migration. `database.sqlite` is gitignored.

A seed officer (`ZP-00001` / `Password123!`) is inserted by the migration so a
working demo login always exists. Seeding happens once, in the migration — not
on every connection as `db.php` did.

### Authentication mechanics

Cookie authentication. ASP.NET Core Identity is **not** used: it generates a
large volume of code the student did not write and would struggle to defend
under examination.

- **Login** — look up by `ForceNumber`, verify via `IPasswordHasher<User>`
  (ASP.NET's built-in PBKDF2), then `HttpContext.SignInAsync` with claims for
  name and station.
- **Authorization** — `[Authorize]` on the dashboard page; `LoginPath = "/"`.
- **Logout** — `SignOutAsync`, redirect to login.
- **Failed login** — a single generic message ("Invalid credentials") that does
  not reveal whether the force number exists.

### Defects fixed as a consequence

| Defect in the PHP version | Resolution |
|---|---|
| No CSRF protection anywhere | Razor Pages emit and validate antiforgery tokens automatically |
| `register.php` validated nothing — any string was an email | `[Required]`, `[EmailAddress]`, `[MinLength(8)]`, `[Compare]` on the bound model |
| `login.php:42` echoed `$e->getMessage()` to the browser | Exceptions logged server-side; generic message returned |
| `db.php` ran `CREATE TABLE` and re-seeded on every connection | EF Core migrations, applied once |

### Testing

An xUnit project with four tests, chosen for value rather than coverage:

1. A correct password authenticates.
2. An incorrect password does not.
3. A duplicate force number is rejected at registration.
4. An anonymous request to `/Dashboard` redirects to login.

Test 4 covers the security-critical path and is the one worth citing in the
report.

### Phase 1 scope boundary

`Dashboard.cshtml` is a near-empty authorized page whose only job is to prove
the cookie works. No charts, no map, no data. Dashboard content is phase 2.

---

## Phase 2 — Dashboard UI on real data

### Dataset

UK STATS19 (Department for Transport road safety data), one year — roughly
100,000 collisions in a file of tens of megabytes. Exact row count and file size
are confirmed on download.

STATS19 was chosen over the Kaggle US Accidents dataset for three reasons:

1. **Its severity field is a genuine injury outcome** (1 Fatal / 2 Serious /
   3 Slight). The US Accidents `Severity` column encodes *traffic-delay impact*,
   not injury. A project claiming to predict accident severity on that target is
   misrepresenting its own model.
2. US Accidents is ~7.7M rows / ~3GB and requires a sampling step first.
3. US Accidents lacks casualty and vehicle counts, which two existing dashboard
   fields require.

STATS19 columns map onto controls the original dashboard already had:

| Existing UI control | STATS19 column |
|---|---|
| Severity filter | `accident_severity` |
| Weather dry/rainy | `weather_conditions` |
| District / area | `local_authority_district` |
| Month | derived from `date` |
| Hotspot map | `latitude` / `longitude` |
| Vehicles / injuries | `number_of_vehicles` / `number_of_casualties` |

**Provenance caveat.** The data is British, not Zambian; Zambia does not publish
incident-level open crash data. The README and report must state that the
methodology is demonstrated on UK open data and that the pipeline accepts any
dataset providing these fields. Stated openly this demonstrates awareness of
data provenance; discovered by an examiner it discredits the project.

### Schema and ingestion

An `Accidents` table mirroring the columns above, populated by a one-off
importer that reads the STATS19 CSV.

**Open item:** the exact column names are confirmed against the real CSV headers
before the schema is written. The names above follow DfT's documented convention
but are not yet verified against a downloaded file.

### Charts

The hardcoded arrays in `dashboard.html` — monthly totals at line 817 and
hotspots at line 912 — are replaced by data read from the `Accidents` table and
served to the Chart.js and Leaflet code.

**Open item:** whether phase 2 ports the existing `dashboard.html` markup or
designs a new UI is undecided. This spec assumes a port, because reusing working
Chart.js and Leaflet code is by far the cheaper path under the deadline. A
redesign is viable but must be costed before phase 2 begins.

The real-time incident feed remains a simulation and is **labelled as such in
the UI**. An honest label costs nothing and removes a question that cannot be
answered well.

---

## Phase 3 — The model

### Approach: train offline in Python, score in C#

`train_model.py` trains a multinomial logistic regression on the STATS19 data,
prints evaluation metrics for the report, and exports the learned coefficients
to `model.json`. The C# application loads that JSON and computes the softmax at
request time.

This was chosen over two alternatives:

- **A Flask service called over HTTP** — requires a second process running
  during the demonstration. If Flask is not up, the prediction page fails in
  front of the examiner, and there is no time to build a fallback.
- **Shelling out via `Process.Start("python", ...)`** — pays interpreter startup
  on every request and is fragile with Windows path quoting.

Training is genuinely performed in Python on real data; only scoring moves to
C#. This is a common production pattern, and implementing the softmax requires
the student to understand the model rather than treat it as a black box.

### Evaluation

STATS19 severity classes are heavily imbalanced — Slight vastly outnumbers
Fatal. **Raw accuracy will therefore look flatteringly high and must not be
reported alone.** `train_model.py` prints per-class precision and recall and a
confusion matrix, on a held-out test split.

---

## Explicitly out of scope

Cut deliberately, and the report should say so rather than leave them looking
unfinished:

- Persistence for accident reports (currently a JavaScript array lost on reload)
- The RTSA vehicle-registration lookup
- Decomposing the 1,106-line `dashboard.html` into partials beyond what the
  Razor migration requires naturally
- Any Clean Architecture layering

## Success criteria

1. A user registers, logs in, and reaches the dashboard; an anonymous visitor
   cannot.
2. Every figure displayed on the dashboard traces to a row in the `Accidents`
   table.
3. The prediction page returns output from a model trained on real data, and the
   student can explain how the score is computed.
4. `README.md` documents setup, the seed login, and the UK-data caveat.
