# Technical stack and compatibility baseline

**Current source baseline:** `v1a`, commit `c7ad32a4b0040f87417973eabd6c12757c54f7d5`, refreshed 2026-10-18. Central pins and project declarations below describe checked-in files; this documentation refresh performs no restore, build or runtime verification.

## Runtime matrix

| Area | Current state |
|---|---|
| Product/Application/Infrastructure/Contracts | Active `net48`. |
| Shared WinForms and Desktop.App | Active `net48`, Windows/.NET Framework 4.8. |
| SQL integration and other product test projects | Active `net48`; use actual project declarations and reviewed manifest ownership. |
| Shared `MyDmsVn.SqlTestHarness` library | `netstandard2.0`, consumed by the active tests; not a runnable product target. |
| SQL runner and runner tests | Tool-only `net8.0`, with explicit framework selection; never counted as product/SQL net8 coverage. |
| Desktop.AppCore | Retained `net8.0-windows` source, parked outside the active solution/release. |
| Historical modern product/SQL/GUI cases | `PARKED / NOT RUN`; no current modern product coverage is claimed. |
| HTTP v2 and modern product runtime | Deferred backlog requiring a separate runtime decision before implementation. |

The user retains the repository's current net48 profile. Neither [RESP](plans/20261018-002-respawn-fixture-reuse.md) nor [L2DB](plans/20261018-001-linq2db-net48-persistence-migration.md) restores net8 product targets or upgrades to .NET 10. Windows verification requires the .NET Framework 4.8 developer targeting pack, the existing .NET 8 SDK for tooling and the repository's pinned source dependencies. [ADR 0008](adr/0008-temporary-net8-target-pause.md) records the runtime pause; [HISTORY](HISTORY.md) retains historical acceptance and the unmeasured duration comparison. No speedup is inferred from that waiver.

## Current packages and application architecture

`Directory.Packages.props`, each consuming csproj and its `packages.lock.json` are the authoritative dependency graph. A central pin can exist without an active consumer. Selected checked-in pins are:

| Package or group | Current central pin |
|---|---|
| `RepoDb` / `RepoDb.SqlServer` / `RepoDb.SqlServer.BulkOperations` | `1.16.0` / `1.16.1` / `1.16.2` |
| `Dapper` | `2.1.79` |
| `Microsoft.Data.SqlClient` | `7.0.2` |
| `dbup-sqlserver` | `7.2.0` |
| `CommunityToolkit.Mvvm` | `8.4.2` |
| `BCrypt.Net-Next` | `4.2.0` |
| `MediatR` / `MediatR.Contracts` | `12.5.0` / `2.0.1` |
| `FluentValidation` and its DI extensions | `11.12.0` |
| `ErrorOr` | `2.1.1` |
| `Newtonsoft.Json` | `13.0.4` |
| `Serilog` / `Serilog.Extensions.Logging` / `Serilog.Sinks.File` | `4.4.0` / `8.0.0` / `7.0.0` |
| `Microsoft.NET.Test.Sdk` / `xunit` / `xunit.runner.visualstudio` | `17.14.1` / `2.9.3` / `3.1.5` |

Current application persistence uses RepoDb, Dapper and explicit native SqlClient behind existing interfaces. DbUp owns controlled schema deployment. Microsoft DI composes the Local WinForms host; MediatR, FluentValidation and ErrorOr remain inside their defined Application/adapter boundaries. Cross-process DTOs and JSON are independent of SQL/UI providers. See [ARCHITECTURE](ARCHITECTURE.md), [PERSISTENCE](PERSISTENCE.md) and [CONTRACTS](CONTRACTS.md).

## Planned additions, not delivered dependencies

| Plan | Initial candidate | Required proof and scope |
|---|---|---|
| RESP, first | [Respawn `4.0.0`](https://www.nuget.org/packages/Respawn/4.0.0) | Prove the active net48/shared-harness dependency graph, reset correctness, ownership and exceptional fresh-database behavior against the current RepoDb/Dapper product. No floating latest version or TFM change. |
| L2DB, after accepted RESP | [linq2db `6.3.0`](https://www.nuget.org/packages/linq2db/6.3.0) | Prove actual net48 restore/build/runtime packaging with the pinned SqlClient, borrowed/owned resources, mappings and behavior parity before DAL conversion. |

These are explicit pilot candidates, not claims that either package is installed, runtime-tested or the newest release. Their plans own actual asset selection and compatibility evidence. A failed candidate blocks its dependent rollout; it does not authorize changing the product runtime. L2DB targets removal of active RepoDb/Dapper consumers after verified conversion, retaining only justified native SqlClient operations and DbUp. RESP is test-only.

## Dependency and build rules

- Reference a package only from projects that consume it. Review transitive assets, target compatibility, native SqlClient packaging and net48 binding redirects when changing dependencies.
- Generate affected lock changes deliberately, review them and verify locked restore on the actual active matrix. Do not refresh unrelated packages or infer modern-product coverage from tool-only net8 execution.
- Keep the existing central package-management settings. Resolve source-mapping problems explicitly; do not add blanket `NoWarn=NU1507` merely to hide them.
- Keep LINQ to DB mappings/provider types in Infrastructure. Do not introduce EF Core, another auth framework, T4/scaffolding bundles or a runtime provider-selection flag as part of these plans.
- Keep shared JSON conventions in `ApiJson`. Additional import/export/updater packages require an actual approved consumer; an unused central MiniExcel/Onova pin is not a feature implementation.
- Check the pinned BootstrapSourceGrid/WinForms source dependencies before changing UI APIs or targets. Modern C# syntax does not guarantee that a runtime API exists on net48.

## Test configuration and verification

[Test policy](TEST_POLICY.md) defines focused task scope and broader SQL/final milestones. Keep current guarded runner/manifest constraints until RESP explicitly adapts them. Mandatory SQL acceptance with missing configuration, approval, Windows or targeting resources is `BLOCKED / NOT RUN`, not PASS; a skipped optional/local diagnostic is not mandatory coverage.

`MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` and local approval resources identify the explicitly approved non-production SQL instance for test execution. The guarded runner uses the approved instance's `master` connection to create owned disposable databases. The product migrator instead requires an explicit application database and rejects system databases. Never confuse these configurations, print credentials or silently fall back to a developer/production database.

## Editing and future review

Follow `.editorconfig` and `.gitattributes`: Markdown is UTF-8 without BOM and CRLF in the working tree; Git normalizes text blobs. Preserve the existing C# BOM and vendor/submodule policies. This refresh changes documentation only.

Vendor/runtime support and dependency advisories need a fresh review when a future runtime or production-release decision is scheduled. Such review is deferred work, not permission to upgrade frameworks or pins within RESP/L2DB.
