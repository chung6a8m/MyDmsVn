# P0 — Repository, solution and compatibility foundation

Status: **Not started**. This is the **first Codex implementation task**. Avoid inventing P1–P7 functionality while scaffolding.

## Objectives

Create buildable dual-target solution/project skeleton, DI roots, package management and safe automated test scaffolding. Validate pinned package compatibility on Windows with VS2022, `net48` and `net8.0`/`net8.0-windows`. No real business features or SQL schema in P0.

## Work items

### P0-T01 — Solution structure
- [ ] Create `MyDmsVn.sln` and `src/`, `tests/` per `docs/PROJECT_STRUCTURE.md`.
- [ ] Add Domain, Application, Infrastructure, Contracts, Desktop.Application, Desktop.WinForms, legacy and modern desktop hosts, and relevant Local adapter project. DbMigrator shell may be empty in P0; HTTP v2 projects can wait until P7.
- [ ] Set exact TFMs correctly; ensure net48 does not reference net8.0-only libraries.
- [ ] Document any unavoidable target-specific dependency difference.

### P0-T02 — Build and package baseline
- [ ] Add `Directory.Build.props` and `Directory.Packages.props` using `docs/TECH_STACK.md` pins (including 1.16.x RepoDb triplet).
- [ ] Reference only needed NuGet packages from each project; validate restore/TFM support.
- [ ] Generate consistent `packages.lock.json` where practical; central package pinning on.
- [ ] Preserve repo `.editorconfig` / `.gitattributes` (UTF-8/CRLF).
- [ ] Do **not** globally silence NuGet warnings to force restore.

### P0-T03 — Dependency rules + composition roots
- [ ] Establish reference boundaries and sample no-op abstractions without prematurely implementing CRUD.
- [ ] Demonstrate a Desktop view-model consuming an `IxxxApiClient` interface without referencing Server.Infrastructure.
- [ ] Create local host composition roots that can register actual implementation later.
- [ ] Verify no unsupported WinForms UI source-project ref is added blindly; inspect target frameworks first.

### P0-T04 — Test harness
- [ ] Select and pin versions for test SDK/framework in central props.
- [ ] Create architecture/reference tests and simple composition/service activation test.
- [ ] Provide STA test helper and modal-dialog suppression pattern (no default WinForms exception dialog on failure).
- [ ] Document test environment constraints and zero destructive database actions.

### P0-T05 — Verification and hand-off
- [ ] On Windows, run `dotnet restore MyDmsVn.sln` and `dotnet build MyDmsVn.sln -c Release`.
- [ ] Run appropriate `dotnet test MyDmsVn.sln -c Release`; record actual command/output and targets.
- [ ] Capture known test packages/platform gaps; do not state success if host build was not actually verified.
- [ ] Update README implementation checkboxes, `docs/DEVELOPMENT_PLAN.md` phase status and hand off P1.

## Deliverables

Buildable solution skeleton; correct project references; common package props; minimal DI composition; test projects; architecture test and explicit reproduction commands.

## Hard stop / anti-scope

No Server.Api host, sales-order module, business database migration, ambient transaction context, blanket package downgrade, forced target upgrade to .NET 10 or account migration guessing.

## Exit criteria

- Supported desktop targets restore/build on Windows.
- Architecture tests enforce Domain/Application boundaries and UI separation.
- At least one test proves desktop abstraction can be resolved without a real SQL connection.
- No AI claims of production-ready features or SQL tests (not written until P1).
