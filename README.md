# HouseBills

A Windows desktop app for keeping track of household bills: what's due, what's paid, what repeats every month, and where the money goes over the year.

Built with .NET 10, WPF (Fluent theme) and SQLite. Everything stays on your own PC, in a single data file.

> This is the SQLite edition of [house-bills](https://github.com/rdjakovic/house-bills) (SQL Server LocalDB). Same app and features; the database is a local file instead of a database server, which makes the installer about a third of the size, needs no administrator rights and installs in seconds.

---

## Features

- **Overview** (start page) — what's overdue and due in the next 7 days (with a shortcut to those bills), this month's total and how it compares with last month, the next bills to pay, and the last 12 months as a chart.
- **Bills** — add, edit and delete bills; record payments (date and amount) or mark a bill unpaid again. Filter by status, due-date range, category or payee, or search by text (description, payee, category or notes). Each bill is shown as **Overdue**, **Due soon** (within 7 days), **Upcoming** or **Paid**, with totals and the outstanding amount.
- **Recurring bills** — templates that repeat weekly, monthly, quarterly or yearly, with an optional end date. Bills are generated automatically for the next 31 days when the app starts (and on demand). Pausing and resuming a template doesn't back-fill the paused period; deleting a generated bill doesn't bring it back.
- **Payees and categories** — names are unique; anything still in use can't be deleted. Seven starter categories are created with the database.
- **Reports** — a column chart of each month against the previous year and a doughnut chart of spending per category, with the exact numbers in tables below. Charts follow the light/dark theme and the chosen language.
- **English or Srpski** — choose the language under **Settings** (bottom of the menu); it switches immediately and is remembered. Serbian also uses Serbian formats (`1.234,56 RSD`, `24.9.2026.`); English follows your Windows regional settings.
- **Backups** — a backup every day (the last 14 are kept), **Back up now…** to any folder or USB stick, and **Restore from backup…** in **Settings**. Restoring first saves your current data as a safety copy.
- **Keyboard and mouse shortcuts** — double-click or **Enter** on a row to edit it, **Delete** to delete it, **Ctrl+N** for a new item, **Esc** to close the editor, **F5** to refresh the bills list. Click a column header to sort.
- **Light or dark** — under **Settings**, choose *Same as Windows* (default), *Light* or *Dark*; it switches immediately and is remembered.
- **No lost edits** — if a record was changed in the meantime (e.g. in a second HouseBills window), you get a "reload and try again" message instead of silently overwriting that change.

---

## Installing (for users)

1. Run `HouseBills-Setup-<version>.exe` and follow the wizard. No administrator rights are needed: HouseBills is installed for your Windows user only (in `%LOCALAPPDATA%\Programs\HouseBills`).
2. If Windows shows **"Windows protected your PC"**, click **More info → Run anyway**. The installer isn't code-signed yet. (Copying the installer via USB stick or a network share instead of downloading it avoids this prompt.)
3. Start HouseBills from the Start menu. It creates your data file on first start.

Nothing else is installed: no .NET, no database server.

**Your data**

- Your bills are in one file: `%LOCALAPPDATA%\HouseBills\HouseBills.db`. Each Windows user on the PC has their own.
- **Backups:** HouseBills makes one automatically each day at startup in `%LOCALAPPDATA%\HouseBills\Backups` and keeps the last 14. For a copy somewhere safer (USB stick, OneDrive), use **Settings → Back up now…**; this works while the app is running.
- **Restore / moving to another PC:** on the other PC, install HouseBills and use **Settings → Restore from backup…** with your backup file. Your current data is first saved in the backup folder as `HouseBills-before-restore-….db`. Backups from an older HouseBills are upgraded automatically; a backup from a newer version is refused (update HouseBills first).
- Before HouseBills upgrades your data to a newer format (after an update), it saves a copy as `HouseBills-before-upgrade-….db` in the backup folder.
- Uninstalling HouseBills keeps your data, so reinstalling brings your bills back.
- Log files are in `%LOCALAPPDATA%\HouseBills\Logs` (one file per day, kept for 30 days) — useful when something goes wrong.
- Your language and theme choices are stored in `%LOCALAPPDATA%\HouseBills\preferences.json`. Without it, HouseBills starts in Serbian if Windows' display language is Serbian, otherwise in English.

---

## For developers

### Prerequisites

| Tool | Needed for | Install |
|---|---|---|
| Windows 10 (1809+) or 11, x64 | everything (WPF) | — |
| .NET SDK **10.0.401** or later 10.0.x (pinned in `global.json`) | build, run, test | `winget install Microsoft.DotNet.SDK.10` |
| **Inno Setup 6** | building the installer only | `winget install JRSoftware.InnoSetup` |

### Get started

```bash
git clone https://github.com/rdjakovic/house-bills-sqlite.git   # or: git clone git@github.com:rdjakovic/house-bills-sqlite.git
cd house-bills-sqlite
dotnet tool restore
dotnet build
dotnet run --project src/HouseBills.Wpf
```

On first start the app creates `%LOCALAPPDATA%\HouseBills\HouseBills.db` (the folder too) and applies all migrations. There is no manual database step.

To use a different database without editing files, override the connection string with an environment variable (it takes precedence over `appsettings.json`):

```powershell
$env:ConnectionStrings__HouseBills = "Data Source=%LOCALAPPDATA%\HouseBills\HouseBillsDev.db"
dotnet run --project src/HouseBills.Wpf
```

### Tests

```bash
dotnet test
```

- Tests use **xUnit v3** on **Microsoft.Testing.Platform** (enabled in `global.json`), with Shouldly and NSubstitute.
- `HouseBills.Infrastructure.Tests` run against real SQLite database files created in `%TEMP%\HouseBillsTests` with the app's own migrations (and deleted afterwards). Nothing to install or start.
- Tests run with the `en-US` culture (`tests/xunit.runner.json`), so assertions on English texts don't depend on the Windows display language.
- Run one project: `dotnet test --project tests/HouseBills.Domain.Tests`

### Before you commit

```bash
dotnet build -c Release          # warnings are errors
dotnet test
dotnet format --verify-no-changes
```

### Database migrations

Migrations in `src/HouseBills.Infrastructure/Persistence/Migrations` are the source of truth for the schema.

```bash
dotnet ef migrations add <Name> -p src/HouseBills.Infrastructure -s src/HouseBills.Wpf -o Persistence/Migrations
dotnet ef database update       -p src/HouseBills.Infrastructure -s src/HouseBills.Wpf
dotnet ef migrations script --idempotent -p src/HouseBills.Infrastructure -s src/HouseBills.Wpf -o artifacts/migrate.sql
```

- The app applies pending migrations itself at startup (`SqliteDatabaseInitializer`), so `database update` is optional. See `AGENTS.md` §6.
- SQLite can't do every schema change in place (e.g. altering a column); EF Core then rebuilds the table in the migration. Review such migrations carefully.
- Never edit a migration that has already been applied — add a new one.

### Build the installer

```bash
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1 -Version 1.0.0
```

This publishes the app self-contained for win-x64 and compiles `installer\HouseBills.iss`. Output: `artifacts\installer\HouseBills-Setup-1.0.0.exe` (~52 MB). SQLite's and SkiaSharp's native libraries (`e_sqlite3.dll`, `libSkiaSharp.dll`, `libHarfBuzzSharp.dll`) are part of the published app and only use DLLs that come with Windows.

Test installer changes on a **clean** Windows (e.g. Windows Sandbox), not only on your dev PC — the dev PC already has runtimes the target PC may not.

### Configuration (`src/HouseBills.Wpf/appsettings.json`)

| Setting | Default | Meaning |
|---|---|---|
| `ConnectionStrings:HouseBills` | `Data Source=%LOCALAPPDATA%\HouseBills\HouseBills.db` | SQLite database file; environment variables are expanded. |
| `Billing:GenerationLookaheadDays` | `31` | How far ahead recurring bills are generated (0–366). |
| `Backup:Folder` | `%LOCALAPPDATA%\HouseBills\Backups` | Automatic backups and safety copies; environment variables are expanded. |
| `Backup:AutomaticBackupsToKeep` | `14` | Daily automatic backups kept (1–365); only automatic ones are ever deleted. |
| `FileLogging:Directory` | `%LOCALAPPDATA%\HouseBills\Logs` | Log folder; environment variables are expanded. |
| `FileLogging:RetainedDays` | `30` | Log files older than this are deleted (1–3650). |
| `UserPreferences:FilePath` | `%LOCALAPPDATA%\HouseBills\preferences.json` | Where the language and theme choices are saved (not in `appsettings.json` by default; handy to override in tests). |
| `Logging:LogLevel` | `Information` | Standard .NET log levels; apply to the log file too. |

Every setting can be overridden with an environment variable, using `__` for `:` (e.g. `Billing__GenerationLookaheadDays=60`).

### Project structure

```
src/
  HouseBills.Domain/          Entities and business rules (bills, recurring schedules, money rules). No dependencies.
  HouseBills.Application/     Services, validation, Result types, repository and report interfaces.
  HouseBills.Infrastructure/  EF Core DbContext, configurations, migrations, repositories, Dapper reports, database setup.
  HouseBills.Presentation.Resources/  UI texts (Strings.resx + Strings.sr-Latn.resx); no UI-framework dependency.
  HouseBills.Wpf/             WPF views, view models (CommunityToolkit.Mvvm), navigation, dialogs, app host, logging, localization.
tests/                        One test project per layer; Infrastructure tests use real SQLite files.
installer/                    Inno Setup script and build script.
tools/generate-icon.ps1       Regenerates src/HouseBills.Wpf/Assets/HouseBills.ico.
```

Dependencies point inwards (Wpf → Application → Domain; Infrastructure implements Application interfaces and is referenced by the Wpf project only in its composition root, `Hosting/HostBuilderExtensions.cs`). ViewModels depend on Application interfaces only, so the database layer could later be swapped for an API client.

### Texts and translations

All user-facing text is in resource files, English plus a Serbian (Latin) translation next to it:

| Texts | Files | Used as |
|---|---|---|
| UI (buttons, labels, dialogs, enum names) | `src/HouseBills.Presentation.Resources/Strings.resx` + `Strings.sr-Latn.resx` | XAML: `{loc:Tr Key}` (updates live when the language changes); C#: `Strings.Key` |
| Validation and business errors | `src/HouseBills.Application/Resources/Messages.resx` + `Messages.sr-Latn.resx` | C#: `Messages.Key` |

- Add every new key to **both** files. Tests fail if a translation is missing or empty, if `{0}` placeholders differ between the languages, or if XAML uses a key that doesn't exist.
- The `Strings`/`Messages` classes are generated at build time (no Visual Studio designer files). The UI texts live in their own project because WPF's XAML pre-compilation can't see resource classes generated inside the WPF project.
- Show amounts with `Converter={StaticResource Money}` (not `StringFormat=C`), so they use the chosen language's formats.
- Don't rely on `CultureInfo.CurrentUICulture`/`CurrentCulture` for texts or formats in UI code; use `Strings`, `LocalizedStrings.Culture` and `LocalizedStrings.FormattingCulture` (the ambient cultures are async-local and don't reliably follow a language switch).

### Tech stack

.NET 10 · WPF with the Fluent theme · CommunityToolkit.Mvvm · Generic Host (DI, config, logging) · EF Core 10 (SQLite) · Dapper · LiveCharts2 (charts, on SkiaSharp) · Serilog (daily log files) · xUnit v3, Shouldly, NSubstitute · Inno Setup.

Package versions are pinned centrally in `Directory.Packages.props`. Coding conventions and rules for contributors (and AI agents) are in [`AGENTS.md`](AGENTS.md).

---

## Troubleshooting

| Problem | What to do |
|---|---|
| "Could not load bills…" on start | Check the latest file in `%LOCALAPPDATA%\HouseBills\Logs`. Usually the data folder isn't writable or the connection string is wrong. |
| `database is locked` in the log | Another program (a second HouseBills, a backup tool or a DB browser) has the file open for writing. Close it and try again. |
