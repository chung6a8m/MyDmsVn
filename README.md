# MyDmsVn

Windows desktop-first SME business software for Sales and Inventory. Local v1 invokes Application handlers in process through the Local adapter.

## Current development status — v1a-pro-ui-next

**Updated:** 2026-10-25. The [current execution index](docs/plans/README.md) routes the Owner-requested Sidebar horizontal-scrollbar follow-up and the planned Security workspace; each plan owns task state and verification for its scope. The [archive boundary](docs/archive/20261025-pre-next-roadmap/README.md) preserves prior work and exact-candidate limits after merged PR #48. Read [AGENTS](AGENTS.md), [AI_CONTEXT](AI_CONTEXT.md) and the sole [TEST_POLICY](docs/TEST_POLICY.md) before acting on that scope.

[Documentation index](docs/README.md) · [Development-plan alias](docs/DEVELOPMENT_PLAN.md) · [Architecture decisions](docs/adr/README.md)

The current [UI Lab](docs/development/UI_LAB.md) remains Sales-only. The planned [Security workstream](docs/plans/20261025-002-security-administration-ui-lab-first.md) has a [UI/behavior specification](docs/design/SECURITY_ADMINISTRATION_UI.md) and [review guide](docs/development/SECURITY_UI_LAB_REVIEW.md). SQL-free review of the actual candidate and dated explicit Owner approval at SEC-T05 must precede SEC-T06 Local cutover or SEC-T07 SQL integration. Documentation and mockups do not approve that gate.

[UI-NEXT](docs/ui-next/README.md) is retained as a historical design and acceptance reference. Its [DateInput candidate evidence](docs/ui-next/evidence/20261024-dateinput-unified-compact-correction.md) and [T13 ledger](docs/ui-next/evidence/20261024-uin-t13-integration.md) retain exact revisions, original failures and bounded acceptance; their former prompts do not schedule new work.

The [pre-UI-NEXT archive](docs/archive/20261023-pre-ui-next/README.md) preserves earlier execution queues. SAL source is present in the merged baseline; [T08 evidence](docs/evidence/sal/20261021-t08-integrated-acceptance.md) records product/package `f5039cb` and separate GUI test `4697744`, with candidate-specific runnable automation PASS. Native125, attended observations and dated Owner acceptance remain OPEN. Routing cleanup neither completes nor waives those gaps. [Production release prerequisites](docs/DEFERRED_WORK.md#release-prerequisites) remain UNACCEPTED / UNSCHEDULED.

## Current implementation

- Product, WinForms and all eleven active test projects target `net48`. Desktop and separate admin migrator package as Windows x86 directories. AppCore/net8 remains PARKED / NOT SHIPPED.
- Clean Architecture, MediatR, FluentValidation, ErrorOr, explicit UnitOfWork, LINQ to DB plus typed native SqlClient, SQL Server and DbUp remain the current architecture.
- Test-only Respawn reusable owned fixtures, deterministic reset/seed, dedicated fresh exceptions and direct `dotnet test`/Test Explorer are implemented. The old SQL harness, runner, gate manifest and approval-file flow remain removed.
- WinForms uses CommunityToolkit.Mvvm and the pinned `vendor/MyDmsVn.WinFormsKit` checkout, which supplies the compatible Bootstrap5WinFormUI/BootstrapSourceGrid assemblies and pinned SourceGrid engine. Four catalogs, Goods Receipt and Sales Draft/Post, quantity/value ledger and stock inquiries, code-defined permissions and one configured superuser exist.
- Sales preserves manual prices, independent duplicate Product rows, gifts and moving-average cost frozen at actual Post. Receipt/sale/reconciliation share the reviewed transaction-owned valuation protocol. Development source requires schema10; historical schema9 package evidence remains separate.
- BCI/BCF compact layout, native inline editing and lookup repairs are implemented and accepted within their recorded bounded scopes. RESP, L2DB and GUI-P5 are completed baselines; preserve their exact evidence and exceptions. Historical acceptance does not establish acceptance for a later candidate or production release.

## Working with Codex

Read [AGENTS.md](AGENTS.md), [AI_CONTEXT.md](AI_CONTEXT.md), the [execution index](docs/plans/README.md), explicitly authorized scope and its relevant references, then the sole [TEST_POLICY.md](docs/TEST_POLICY.md). Verify changed behavior with focused source-owned tests. Archived tasks and prompts do not authorize new work; SAL integration and production-release tasks are not inherited. Documentation-only changes require diff/link/status/encoding checks, not SQL/GUI execution.

Shared UI owns reusable controls and rendering; MyDmsVn owns workspace/session identity, operation admission, dirty-close and recovery policies. Runtime, security, SQL ownership, transaction and test execution safeguards remain binding. [Retired/out-of-scope directions](docs/DEFERRED_WORK.md) define constraints without another execution queue; [History](docs/HISTORY.md) and original evidence retain provenance.

## Documentation

Start with the [documentation index](docs/README.md), [Architecture](docs/ARCHITECTURE.md), [PRD](docs/PRD.md), [Contracts](docs/CONTRACTS.md), [Technical stack](docs/TECH_STACK.md) and [test commands](tests/README.md).
