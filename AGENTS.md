# AGENTS.md

Guidance for AI coding agents (and humans) working in this repository.
Read this file fully before making changes. When in doubt, follow the conventions here over general defaults.

---

## 1. Project overview

Windows desktop line-of-business application backed by a local **SQLite** database file (one per Windows user).

> This repository is the SQLite edition of `house-bills` (which uses Microsoft SQL Server / LocalDB). The switch to SQLite was explicitly approved: the app is single-user per PC, and SQLite removes the database server, the installer prerequisites and admin rights. A shared household database or API mode would need a server database again (see §7).

**Stack (do not change without explicit approval):**

| Area | Choice |
|---|---|
| Language | **C#** (latest version supported by the target SDK). No VB.NET. |
| Runtime | **.NET 10 (LTS)** — `net10.0`; the WPF project targets `net10.0-windows10.0.19041.0` (Windows notification API) with `SupportedOSPlatformVersion` 10.0.17763 |
| UI | **WPF** with **MVVM** |
| MVVM toolkit | `CommunityToolkit.Mvvm` |
| Charts | `LiveChartsCore.SkiaSharpView.WPF` (LiveCharts2) — its WPF host pulls .NET Framework-only SkiaSharp/OpenTK packages, so `NU1701` is suppressed in the WPF project only |
| DI / hosting / config / logging | `Microsoft.Extensions.Hosting` (Generic Host) |
| Data access | **EF Core 10** (`Microsoft.EntityFrameworkCore.Sqlite`) |
| Raw SQL / reporting | **Dapper** on `Microsoft.Data.Sqlite` |
| API (when enabled) | **ASP.NET Core Web API** on .NET 10 |
| Tests | xUnit, FluentAssertions (or Shouldly), NSubstitute |

Never use: `System.Data.SQLite` or `System.Data.SqlClient` (legacy), EF6, .NET Framework 4.x, WinForms (unless explicitly asked), code-behind business logic.

---

## 2. Architecture principles

The UI is the most likely thing to change in the future (WPF → WinUI 3 / Avalonia / Blazor).
**Everything except the UI project must be UI-agnostic.**

1. **Layered, one-way dependencies.** Inner layers never reference outer layers.
2. **No business logic in the UI.** Views bind to ViewModels; ViewModels call services; services contain the rules.
3. **No UI types outside the UI project.** No `System.Windows.*`, `MessageBox`, `Dispatcher`, `Visibility`, `Brush` etc. in Core/Application/Infrastructure.
4. **Everything via DI.** No `new` for services, repositories or DbContexts; no static service locators.
5. **Async all the way** for I/O (DB, HTTP, file). Never `.Result` / `.Wait()`.
6. **The desktop client should be swappable to an API backend** without touching ViewModels (see §7).

---

## 3. Solution structure

```
src/
  MyApp.Domain/            # Entities, value objects, enums, domain rules. No dependencies.
  MyApp.Application/       # Use cases / services, interfaces (IRepository, IClock...), DTOs, validation.
                           # References: Domain only.
  MyApp.Infrastructure/    # EF Core DbContext, configurations, migrations, Dapper queries,
                           # implementations of Application interfaces.
                           # References: Application, Domain.
  MyApp.Wpf/               # WPF app: Views, ViewModels, converters, navigation, App host setup.
                           # References: Application (+ Infrastructure only in composition root).
  MyApp.Api/               # (optional) ASP.NET Core Web API. References: Application, Infrastructure.
  MyApp.Api.Client/        # (optional) Typed HTTP client implementing Application interfaces.
tests/
  MyApp.Domain.Tests/
  MyApp.Application.Tests/
  MyApp.Infrastructure.Tests/   # Integration tests against real SQLite database files
  MyApp.Wpf.Tests/              # ViewModel tests (no UI automation required)
```

Rules:
- `MyApp.Wpf` must **not** reference EF Core or Dapper types directly, except in the composition root (`App.xaml.cs` / `HostBuilderExtensions`) where services are registered.
- ViewModels depend on **Application interfaces**, never on `DbContext`.
- Shared build settings go in `Directory.Build.props`; package versions in `Directory.Packages.props` (Central Package Management).

---

## 4. Coding conventions

- `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in `Directory.Build.props`.
- File-scoped namespaces. One public type per file. File name = type name.
- Naming: `PascalCase` types/members, `_camelCase` private fields, `camelCase` locals/parameters, `Async` suffix on async methods, `I` prefix on interfaces.
- Prefer `record` for DTOs and immutable data; `sealed` classes by default.
- Pass `CancellationToken` through all async service and data methods.
- Use `ILogger<T>` for logging; never `Console.WriteLine` or `Debug.WriteLine` in committed code.
- Configuration via `IOptions<T>` bound from `appsettings.json`; no hard-coded connection strings or magic values.
- Guard clauses over nested `if`s. Keep methods short and single-purpose.
- Follow `.editorconfig`; run `dotnet format` before committing.

---

## 5. UI layer (WPF + MVVM)

- Use `CommunityToolkit.Mvvm` source generators:
  - ViewModels inherit `ObservableObject` (or `ObservableValidator` for forms).
  - `[ObservableProperty]` for bindable state, `[RelayCommand]` for commands (async commands for I/O).
  - `[NotifyCanExecuteChangedFor]` / `[NotifyPropertyChangedFor]` instead of manual raising.
- **Code-behind** may only contain view-specific concerns (focus, animations, pure UI wiring). No data access, no business rules.
- Views are resolved via DI; ViewModels are injected (constructor) — no `new ViewModel()` in XAML for real screens. Design-time data via `d:DataContext` is fine.
- Navigation and dialogs go through abstractions (`INavigationService`, `IDialogService`) so ViewModels stay testable and UI-framework-independent.
- Cross-ViewModel communication via `IMessenger` (`WeakReferenceMessenger`), not static events.
- Long-running work must not block the UI thread; show busy state via a bound property (e.g. `IsBusy`).
- Use compiled/strongly typed bindings where possible; avoid `ElementName` spaghetti.
- Styles and resources in `Themes/` resource dictionaries; no inline repeated styling.
- Prefer the built-in .NET 10 WPF Fluent theme or a single chosen control library; do not mix control vendors.

---

## 6. Data layer (EF Core 10 + Dapper, SQLite)

### EF Core (default for CRUD and domain persistence)
- One `AppDbContext` in `MyApp.Infrastructure`, registered with `AddDbContextFactory` / `AddDbContext` as appropriate.
  - **WPF client:** use `IDbContextFactory<AppDbContext>` and create a short-lived context per operation (`await using var db = await factory.CreateDbContextAsync(ct);`). **Never** keep a long-lived DbContext per window or per app.
- Entity configuration via `IEntityTypeConfiguration<T>` classes — no data annotations on domain entities.
- Always configure explicitly: string max lengths, money conversion (see SQLite specifics), required/optional, indexes, keys.
- Read-only queries: `AsNoTracking()`; project to DTOs with `Select(...)` instead of loading full graphs.
- Avoid N+1: use projection or explicit `Include` deliberately; review generated SQL for non-trivial queries (`ToQueryString()`).
- Concurrency: every entity has a `RowVersion` concurrency token (`HasAppRowVersion()`); SQLite has no `rowversion`, so `AppDbContext` issues a new random token on every insert/update. Handle `DbUpdateConcurrencyException`.
- **Migrations** are the source of truth for schema:
  - `dotnet ef migrations add <Name> -p src/MyApp.Infrastructure -s src/MyApp.Wpf` (or `-s src/MyApp.Api`)
  - Review every generated migration before committing. Never edit an applied migration; add a new one.
  - The app creates/migrates its database at startup (`SqliteDatabaseInitializer`): the database is a file in the Windows user's own profile, so there is no shared schema to protect, and it is the only way the installer-based, per-user deployment works without manual steps. If a shared server database is ever added (API mode, §7), that server is deployed with idempotent scripts (`dotnet ef migrations script --idempotent`) and never migrated by the client.
  - SQLite can't alter columns in place; EF Core rebuilds the table for such changes. Review those migrations closely.

### Dapper (reporting, complex/performance-critical SQL, stored procedures)
- Lives in `MyApp.Infrastructure` behind Application interfaces (e.g. `IReportQueries`).
- **Always parameterized** — never string-concatenate user input into SQL.
- SQL in `const string` / raw string literals or `.sql` embedded resources, not built dynamically unless unavoidable (then whitelist identifiers).
- Map to DTO `record`s; don't return Dapper dynamics out of Infrastructure.
- Use `Microsoft.Data.Sqlite.SqliteConnection` from an injected connection factory.

### SQLite specifics
- **Money** is stored as INTEGER minor units (paras/cents) via `StoredAsMinorUnits()`; SQLite has no decimal type, and EF Core's default (TEXT) can't be summed or compared exactly in SQL. Dapper queries sum the integers and convert to `decimal` in C#.
- **Dates** (`DateOnly`) are stored as `'yyyy-MM-dd'` TEXT; compare them against parameters in the same format so range predicates stay index-friendly. Apply `strftime()` only after filtering.
- **Text comparison** is case-sensitive by default; names that must be unique regardless of case use `UseCollation("NOCASE")` (ASCII-only case folding).
- Integers come back from SQLite as `Int64`; map Dapper results to `long` and convert.
- Column max lengths are not enforced by SQLite; the domain validates them.

---

## 7. API layer (for multi-user / production deployments)

Direct desktop → SQL Server connections are acceptable only for single-user or trusted-LAN internal tools.
For anything with many users, the target architecture is:

```
WPF client  →  HTTPS  →  ASP.NET Core Web API  →  SQL Server
```

- The WPF app depends on Application interfaces. Two implementations may exist:
  - `Infrastructure` (direct EF Core/Dapper) — dev / single-user mode.
  - `Api.Client` (typed `HttpClient` via `IHttpClientFactory`) — production mode.
  Switching is a DI registration change, not a ViewModel change.
- No database credentials on client machines in API mode.
- API: authentication (Entra ID / OpenID Connect or JWT), authorization policies, input validation, ProblemDetails for errors, OpenAPI document.
- DTOs shared between API and client live in `MyApp.Application` (or a dedicated `Contracts` project).

---

## 8. Error handling & validation

- Validate input in the Application layer (FluentValidation or `ObservableValidator` for UI-level feedback + service-level checks). Never trust UI validation alone.
- Use exceptions for exceptional cases; use a result type (e.g. `Result<T>`) for expected business failures.
- Global unhandled exception handlers in `App.xaml.cs` (`DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`, `AppDomain.UnhandledException`) that log and show a friendly message.
- Never swallow exceptions silently. Never show raw exception text/stack traces to end users.

---

## 9. Security

- No secrets in source control. Use User Secrets in development, environment/config providers or a vault in production.
- Connection strings: `Encrypt=True`; use integrated/Windows auth or managed identities where possible.
- Least-privilege DB logins (no `sa`, no `db_owner` for the app).
- All SQL parameterized (EF Core and Dapper).
- Log no personal data or credentials.

---

## 10. Testing

- New business logic in Application/Domain **must** have unit tests.
- ViewModels are unit-tested by mocking Application interfaces and navigation/dialog services.
- Data access is tested with **integration tests against real SQLite database files** (created with the real migrations) — do not rely on the EF Core InMemory provider for query behaviour.
- Test naming: `MethodName_StateUnderTest_ExpectedBehavior`.
- All tests must pass before a change is considered done.

---

## 11. Commands

```bash
dotnet restore
dotnet build -c Release
dotnet test
dotnet format --verify-no-changes

# EF Core
dotnet tool restore                      # dotnet-ef pinned in .config/dotnet-tools.json
dotnet ef migrations add <Name> -p src/MyApp.Infrastructure -s src/MyApp.Wpf
dotnet ef database update         -p src/MyApp.Infrastructure -s src/MyApp.Wpf
dotnet ef migrations script --idempotent -p src/MyApp.Infrastructure -s src/MyApp.Wpf -o artifacts/migrate.sql
```

---

## 12. Rules for AI agents

- **Respect layer boundaries** in §2–3. If a change seems to require breaking them, stop and explain why instead of doing it.
- Do not add NuGet packages or change the stack without saying so explicitly in your summary, with a reason. Pin versions via `Directory.Packages.props`.
- Do not modify existing, already-applied migrations or delete migration files.
- Do not run destructive database commands (`DROP`, `TRUNCATE`, `database drop`, mass `DELETE/UPDATE`) against any non-local database.
- Keep changes small and focused; don't reformat or refactor unrelated code in the same change.
- After changes: build, run tests, run `dotnet format`. Report what you ran and the result.
- When generating SQL (EF or Dapper), consider indexes and query plans for large tables and mention performance implications.
- Prefer clarity over cleverness; add XML doc comments on public Application interfaces.
- If requirements are ambiguous, ask before implementing rather than guessing.

---

## 13. Upgrade policy

- Stay on the current **LTS** .NET release (currently .NET 10, supported to Nov 2028). Plan the move to the next LTS within ~6 months of its release.
- Keep EF Core major version aligned with the .NET version.
- Keep UI-framework-specific code confined to `MyApp.Wpf` so a future UI swap (WinUI 3, Avalonia, Blazor) only replaces that project.
