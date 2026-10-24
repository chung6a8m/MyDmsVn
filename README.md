# MyDmsVn

Windows desktop-first SME business software for Sales and Inventory. Local v1 invokes Application handlers in process through the Local adapter.

## Current roadmap — v1a-pro-ui-next

**Updated:** 2026-10-24. **UI-NEXT is the sole ACTIVE roadmap**, originally activated from merged PR #43 at `7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639`. UIN-T00–T13 are VERIFIED in their recorded bounded scopes and exact revisions, including the retained failures and acceptance limits in the [T13 ledger](docs/ui-next/evidence/20261024-uin-t13-integration.md). Current work is the **DateInput Unified Compact correction, UIN-D01–UIN-D03**, continuing PR #45 on `codex/uin-t13`. The Owner approved visual option 01 on 2026-10-24; correction implementation is **NOT STARTED**, runtime checks are **NOT RUN**, and acceptance of the implemented candidate remains pending.

[Active roadmap](docs/ui-next/20261023-active-roadmap.md) · [Design and control policy](docs/ui-next/20261023-design-and-control-policy.md) · [Implementation plan](docs/ui-next/20261023-implementation-plan.md) · [Validation and handoff](docs/ui-next/20261023-validation-and-handoff.md)

[DateInput approved design](docs/ui-next/20261024-dateinput-unified-compact-design.md) · [Active correction plan](docs/ui-next/20261024-dateinput-unified-compact-correction.md) · [Codex handoff prompt](docs/ui-next/20261024-dateinput-unified-compact-codex-prompt.md)

The [archive boundary](docs/archive/20261023-pre-ui-next/README.md) retires previous execution queues and excludes their unfinished work from UI-NEXT. SAL source is present in the merged baseline; [T08 evidence](docs/evidence/sal/20261021-t08-integrated-acceptance.md) records product/package `f5039cb` and separate GUI test `4697744`, with candidate-specific runnable automation PASS. Native125, attended observations and dated Owner acceptance remain OPEN in the archive. Archiving does not complete or waive those gaps. Production release prerequisites remain UNACCEPTED / UNSCHEDULED.

## Current implementation

- Product, WinForms and all ten active test projects target `net48`. Desktop and separate admin migrator package as Windows x86 directories. AppCore/net8 remains PARKED / NOT SHIPPED.
- Clean Architecture, MediatR, FluentValidation, ErrorOr, explicit UnitOfWork, LINQ to DB plus typed native SqlClient, SQL Server and DbUp remain the current architecture.
- Test-only Respawn reusable owned fixtures, deterministic reset/seed, dedicated fresh exceptions and direct `dotnet test`/Test Explorer are implemented. The old SQL harness, runner, gate manifest and approval-file flow remain removed.
- WinForms uses CommunityToolkit.Mvvm, Bootstrap5WinFormUI and BootstrapSourceGrid. Four catalogs, Goods Receipt and Sales Draft/Post, quantity/value ledger and stock inquiries, code-defined permissions and one configured superuser exist.
- Sales preserves manual prices, independent duplicate Product rows, gifts and moving-average cost frozen at actual Post. Receipt/sale/reconciliation share the reviewed transaction-owned valuation protocol. Development source requires schema10; historical schema9 package evidence remains separate.
- BCI/BCF compact layout, native inline editing and lookup repairs are implemented and accepted within their recorded bounded scopes. RESP, L2DB and GUI-P5 are completed baselines; preserve their exact evidence and exceptions. Historical acceptance does not establish new UI-NEXT or production-release acceptance.

## Working with Codex

Read [AGENTS.md](AGENTS.md), [AI_CONTEXT.md](AI_CONTEXT.md), the [active roadmap](docs/ui-next/20261023-active-roadmap.md) and its design/validation documents, then the sole [TEST_POLICY.md](docs/TEST_POLICY.md). Verify changed behavior with focused source-owned tests. Archived SAL integration and production-release tasks are not inherited. Documentation-only changes require diff/link/status/encoding checks, not SQL/GUI execution.

Shared UI owns reusable controls and rendering; MyDmsVn owns workspace/session identity, operation admission, dirty-close and recovery policies. Runtime, security, SQL ownership, transaction and test execution safeguards remain binding. [Retired/out-of-scope directions](docs/DEFERRED_WORK.md) define constraints without another execution queue; [History](docs/HISTORY.md) and original evidence retain provenance.

## Documentation

Start with the [documentation index](docs/README.md), [Architecture](docs/ARCHITECTURE.md), [PRD](docs/PRD.md), [Contracts](docs/CONTRACTS.md), [Technical stack](docs/TECH_STACK.md) and [test commands](tests/README.md).
