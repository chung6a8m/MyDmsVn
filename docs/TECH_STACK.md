# Technical stack and compatibility baseline

**Current cutover:** L2DB-T00–T07 on net48, 2026-10-19; [T07 evidence](L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke). RESP and L2DB plan-specific acceptance are complete. Broader P5/P6 release gates remain deferred; historical evidence keeps its original provider/revision.

## Runtime matrix

| Area | Current state |
|---|---|
| Product/Application/Infrastructure/Contracts | Active `net48`. |
| Shared WinForms and Desktop.App | Active `net48`, Windows/.NET Framework 4.8. |
| SQL integration and other product test projects | All ten active test projects target `net48`; use actual project declarations and source cases. |
| Desktop.AppCore | Retained `net8.0-windows` source, parked outside the active solution/release. |
| Historical modern product/SQL/GUI cases | `PARKED / NOT RUN`; no current modern product coverage is claimed. |
| HTTP v2 and modern product runtime | Deferred backlog requiring a separate runtime decision before implementation. |

The user retains the repository's current net48 profile. Neither [RESP](plans/20261018-002-respawn-fixture-reuse.md) nor [L2DB](plans/20261018-001-linq2db-net48-persistence-migration.md) restores net8 product targets or upgrades to .NET 10. Windows verification requires the .NET Framework 4.8 developer targeting pack, the existing .NET 8 SDK for tooling and the repository's pinned source dependencies. [ADR 0008](adr/0008-temporary-net8-target-pause.md) records the runtime pause; [HISTORY](HISTORY.md) retains historical acceptance and the unmeasured duration comparison. No speedup is inferred from that waiver.

## Current packages and application architecture

`Directory.Packages.props`, each consuming csproj and its `packages.lock.json` are the authoritative dependency graph. A central pin can exist without an active consumer. Selected checked-in pins are:

| Package or group | Current central pin |
|---|---|
| `linq2db` | `6.3.0` |
| `Respawn` (test only) | `4.0.0` |
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

Current application persistence uses LINQ to DB and typed native SqlClient behind existing interfaces. DbUp owns controlled schema deployment. Microsoft DI composes the Local WinForms host; MediatR, FluentValidation and ErrorOr remain inside their defined Application/adapter boundaries. Cross-process DTOs and JSON are independent of SQL/UI providers. See [ARCHITECTURE](ARCHITECTURE.md), [PERSISTENCE](PERSISTENCE.md) and [CONTRACTS](CONTRACTS.md).

## Delivered dependency cutover

RESP pins Respawn 4.0.0 in test owners. L2DB pins linq2db 6.3.0 in Infrastructure with Microsoft.Data.SqlClient 7.0.2; mappings/provider objects stay behind existing interfaces. T06 removes all active RepoDb/Dapper references, central pins, initializer hooks and temporary adapters. Native test-only SQL oracles do not invoke product mappings or repositories. DbUp remains the schema owner.

Deliberate lock regeneration removes obsolete ORM subgraphs. Remaining requirements resolve Microsoft.Bcl.AsyncInterfaces from 10.0.11 to 10.0.3 and System.Collections.Immutable from 10.0.11 to 1.5.0; no other retained package version changes or broad upgrades. Parked AppCore locks retain historical nodes and were neither restored nor edited. Reactivation must regenerate those locks against the current central pins after a separate runtime decision.

## Dependency and build rules

- Reference a package only from projects that consume it. Review transitive assets, target compatibility, native SqlClient packaging and net48 binding redirects when changing dependencies.
- Generate affected lock changes deliberately, review them and verify locked restore on the actual active matrix. Do not refresh unrelated packages or infer modern-product coverage from the SDK used to build net48.
- Keep the existing central package-management settings. Resolve source-mapping problems explicitly; do not add blanket `NoWarn=NU1507` merely to hide them.
- Keep LINQ to DB mappings/provider types in Infrastructure. Do not introduce EF Core, another auth framework, T4/scaffolding bundles or a runtime provider-selection flag as part of these plans.
- Keep shared JSON conventions in `ApiJson`. Additional import/export/updater packages require an actual approved consumer; an unused central MiniExcel/Onova pin is not a feature implementation.
- Check the pinned BootstrapSourceGrid/WinForms source dependencies before changing UI APIs or targets. Modern C# syntax does not guarantee that a runtime API exists on net48.

## Test configuration and verification

[Test policy](TEST_POLICY.md) defines focused task scope and broader SQL/final milestones. Run test projects directly and preserve the local allocation/cleanup guards. Mandatory SQL acceptance with missing configuration, Windows or targeting resources is `BLOCKED / NOT RUN`, not PASS; a skipped optional/local diagnostic is not mandatory coverage.

`MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` explicitly identifies the designated non-production SQL instance and `master` catalog. The local IntegrationTests helper validates the live allocation before drop. There is no approval-file or external runner requirement. The product migrator instead requires an explicit application database and rejects system databases. Never confuse these configurations, print credentials or silently fall back to a developer/production database.

## Editing and future review

Follow `.editorconfig` and `.gitattributes`: Markdown is UTF-8 without BOM and CRLF in the working tree; Git normalizes text blobs. Preserve the existing C# BOM and vendor/submodule policies. The framework removal changes test infrastructure; RESP and L2DB-T00–T07 have scoped evidence; broader release acceptance remains deferred.

Vendor/runtime support and dependency advisories need a fresh review when a future runtime or production-release decision is scheduled. Such review is deferred work, not permission to upgrade frameworks or pins within RESP/L2DB.
