# MyDmsVn

Windows desktop-first SME business software for Sales and Inventory. Local v1 invokes Application handlers in process through the Local adapter.

## Current development status — main

**Updated:** 2026-10-28. The selected [Business Compact conformance and independent UI Labs roadmap](docs/plans/20261027-001-business-compact-and-independent-ui-labs.md) is complete within its recorded bounded scope. The original inspected [`main@8de4451573e8` baseline](https://github.com/chung6a8m/MyDmsVn/commit/8de4451573e8a9a033d7e5a0b07e5f22ef3018b9) contains [PR #53](https://github.com/chung6a8m/MyDmsVn/pull/53); the bounded runtime/test handoff candidate is `8ec1ff71ef0a6a7cf4befa88bbd7bed2616827a0`. Separate Sales/Security launchers, bounded native Owner decisions and physical-100/125 product-host parity are recorded in the [current candidate evidence](docs/evidence/ui-labs/20261027-business-compact-and-independent-ui-labs.md). The [plan index](docs/plans/README.md) remains the sole selector; no further task is selected. [AGENTS](AGENTS.md), [AI_CONTEXT](AI_CONTEXT.md) and the sole [TEST_POLICY](docs/TEST_POLICY.md) govern work.

The durable [Business Compact policy](docs/design/BUSINESS_COMPACT.md) makes toolkit choice and native visual review explicit. The [independent Lab specification](docs/design/INDEPENDENT_UI_LABS.md) defines the implemented separate Sales/Security startup, shared product theme/typography/content geometry and module-owned runtime graphs. `MyDmsVn.Desktop.UiLab` is the shared library; `MyDmsVn.Desktop.UiLab.Sales` and `MyDmsVn.Desktop.UiLab.Security` are the direct net48 WinExe launchers. Native pilot/final approvals and the later SQL-free product-host parity remain bounded to their recorded candidates and exceptions.

[Documentation index](docs/README.md) · [Development-plan alias](docs/DEVELOPMENT_PLAN.md) · [Architecture decisions](docs/adr/README.md)

The [UI Lab](docs/development/UI_LAB.md) hosts Sales and the SQL-free Security candidate directly. Current UIL evidence records bounded Security pilot/final approval, focused native-focus confirmation and SQL-free product-shell parity on runtime/test candidate `8ec1ff71ef0a6a7cf4befa88bbd7bed2616827a0`. The earlier reviewed Security workspace has a separate dated Owner approval at SEC-T05 (2026-10-26) and subsequent bounded Local/real-SQL integration and handoff. Its original [UI specification](docs/design/SECURITY_ADMINISTRATION_UI.md), [UI Labs review guide](docs/development/SECURITY_UI_LAB_REVIEW.md), [candidate evidence](docs/evidence/security/20261025-security-ui-lab.md) and [Local/SQL evidence](docs/evidence/security/20261025-security-local-sql.md) remain authoritative for that historical coverage. Incomplete SEC-T04/native-review and attended SEC-L01–SEC-L12 observations are not implicitly accepted. [Sidebar evidence](docs/evidence/sidebar/20261025-sidebar-horizontal-scrollbar.md) supports only its reviewed hosted-shell scope.

[UI-NEXT](docs/ui-next/README.md) remains historical: [DateInput correction evidence](docs/ui-next/evidence/20261024-dateinput-unified-compact-correction.md) and [T13 ledger](docs/ui-next/evidence/20261024-uin-t13-integration.md) preserve candidate-specific results. Earlier [SAL T08](docs/evidence/sal/20261021-t08-integrated-acceptance.md) retains native125, attended and Owner acceptance OPEN. Archival neither carries old tasks forward nor waives failures. [Release prerequisites](docs/DEFERRED_WORK.md#release-prerequisites) remain UNACCEPTED / UNSCHEDULED.

## Current implementation

- Product, WinForms and all eleven active test projects target `net48`. Desktop and separate admin migrator package as Windows x86 directories. AppCore/net8 remains PARKED / NOT SHIPPED.
- Clean Architecture, MediatR, FluentValidation, ErrorOr, explicit UnitOfWork, LINQ to DB plus typed native SqlClient, SQL Server and DbUp remain the current architecture.
- Test-only Respawn reusable owned fixtures, deterministic reset/seed, dedicated fresh exceptions and direct `dotnet test`/Test Explorer are implemented. The old SQL harness, runner, gate manifest and approval-file flow remain removed.
- WinForms uses CommunityToolkit.Mvvm and the pinned `vendor/MyDmsVn.WinFormsKit` checkout, which supplies the compatible Bootstrap5WinFormUI/BootstrapSourceGrid assemblies and pinned SourceGrid engine. Four catalogs, Goods Receipt and Sales Draft/Post, quantity/value ledger and stock inquiries, code-defined permissions and one configured superuser exist. The reviewed Security workspace is now used through the production Local composition, with bounded SQL evidence; the SQL-free Lab includes Security.
- Sales preserves manual prices, independent duplicate Product rows, gifts and moving-average cost frozen at actual Post. Receipt/sale/reconciliation share the reviewed transaction-owned valuation protocol. Development source requires schema10; historical schema9 package evidence remains separate.
- BCI/BCF compact layout, native inline editing and lookup repairs are implemented and accepted within their recorded bounded scopes. RESP, L2DB and GUI-P5 are completed baselines; preserve their exact evidence and exceptions. Historical acceptance does not establish acceptance for a later candidate or production release.

## Working with Codex

Read [AGENTS.md](AGENTS.md), [AI_CONTEXT.md](AI_CONTEXT.md), the [execution index](docs/plans/README.md), explicitly authorized scope and its relevant references, then the sole [TEST_POLICY.md](docs/TEST_POLICY.md). Verify changed behavior with focused source-owned tests. Archived tasks and prompts do not authorize new work; SAL integration and production-release tasks are not inherited. Documentation-only changes require diff/link/status/encoding checks, not SQL/GUI execution.

Shared UI owns reusable controls and rendering; MyDmsVn owns workspace/session identity, operation admission, dirty-close and recovery policies. Runtime, security, SQL ownership, transaction and test execution safeguards remain binding. [Retired/out-of-scope directions](docs/DEFERRED_WORK.md) define constraints without another execution queue; [History](docs/HISTORY.md) and original evidence retain provenance.

## Documentation

Start with the [documentation index](docs/README.md), [Architecture](docs/ARCHITECTURE.md), [PRD](docs/PRD.md), [Contracts](docs/CONTRACTS.md), [Technical stack](docs/TECH_STACK.md) and [test commands](tests/README.md).
