# P0 — Repository, solution and compatibility foundation

Status: **Complete (2026-10-08)**. P1 is the next implementation phase. P0 contains no P1–P7 business functionality or SQL schema.

## Objectives

Create buildable dual-target solution/project skeleton, DI roots, package management and safe automated test scaffolding. Validate pinned package compatibility on Windows with VS2022, `net48` and `net8.0`/`net8.0-windows`. No real business features or SQL schema in P0.

## Work items

### P0-T01 — Solution structure
- [x] Create `MyDmsVn.sln` and `src/`, `tests/` per `docs/PROJECT_STRUCTURE.md`.
- [x] Add Domain, Application, Infrastructure, Contracts, Desktop.Application, Desktop.WinForms, legacy and modern desktop hosts, and relevant Local adapter project. DbMigrator shell may be empty in P0; HTTP v2 projects can wait until P7.
- [x] Set exact TFMs correctly; ensure net48 does not reference net8.0-only libraries.
- [x] Document any unavoidable target-specific dependency difference.

### P0-T02 — Build and package baseline
- [x] Add `Directory.Build.props` and `Directory.Packages.props` using `docs/TECH_STACK.md` pins (including 1.16.x RepoDb triplet).
- [x] Reference only needed NuGet packages from each project; validate restore/TFM support.
- [x] Generate consistent `packages.lock.json` where practical; central package pinning on.
- [x] Preserve repo `.editorconfig` / `.gitattributes` (UTF-8/CRLF).
- [x] Do **not** globally silence NuGet warnings to force restore.

### P0-T03 — Dependency rules + composition roots
- [x] Establish reference boundaries and sample no-op abstractions without prematurely implementing CRUD.
- [x] Demonstrate a Desktop view-model consuming an `IxxxApiClient` interface without referencing Server.Infrastructure.
- [x] Create local host composition roots that can register actual implementation later.
- [x] Verify no unsupported WinForms UI source-project ref is added blindly; inspect target frameworks first.

### P0-T04 — Test harness
- [x] Select and pin versions for test SDK/framework in central props.
- [x] Create architecture/reference tests and simple composition/service activation test.
- [x] Provide STA test helper and modal-dialog suppression pattern (no default WinForms exception dialog on failure).
- [x] Document test environment constraints and zero destructive database actions.

### P0-T05 — Verification and hand-off
- [x] On Windows, run `dotnet restore MyDmsVn.sln` and `dotnet build MyDmsVn.sln -c Release`.
- [x] Run appropriate `dotnet test MyDmsVn.sln -c Release`; record actual command/output and targets.
- [x] Capture known test packages/platform gaps; do not state success if host build was not actually verified.
- [x] Update README implementation checkboxes, `docs/DEVELOPMENT_PLAN.md` phase status and hand off P1.

## Verification evidence

Verified on Windows on 2026-10-08 with .NET SDK 8.0.425 and the installed .NET Framework 4.8 targeting pack:

```powershell
dotnet restore MyDmsVn.sln --locked-mode --nologo
dotnet build MyDmsVn.sln -c Release --no-restore --nologo
dotnet test MyDmsVn.sln -c Release --no-build --no-restore --nologo
```

- Restore: succeeded for all 14 solution projects using checked-in lock files.
- Build: succeeded for both desktop hosts and all library/test target frameworks with 0 warnings and 0 errors.
- Tests: 26 passed, 0 failed, 0 skipped across Architecture (`net8.0`), Server.Application (`net48`, `net8.0`), and Desktop (`net48`, `net8.0-windows`).
- Not run by design: SQL integration tests, database provisioning/migration, interactive GUI smoke, Bootstrap UI/source-grid compatibility, and P1+ packages that are pinned centrally but not referenced in P0.
- Target-specific difference: WinForms projects use `net48;net8.0-windows`; non-UI shared projects use `net48;net8.0`; the two executable hosts target `net48` and `net8.0-windows` separately.

## Deliverables

Buildable solution skeleton; correct project references; common package props; minimal DI composition; test projects; architecture test and explicit reproduction commands.

## Hard stop / anti-scope

No Server.Api host, sales-order module, business database migration, ambient transaction context, blanket package downgrade, forced target upgrade to .NET 10 or account migration guessing.

## Exit criteria

- Supported desktop targets restore/build on Windows.
- Architecture tests enforce Domain/Application boundaries and UI separation.
- At least one test proves desktop abstraction can be resolved without a real SQL connection.
- No AI claims of production-ready features or SQL tests (not written until P1).
