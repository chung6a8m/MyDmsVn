# MyDmsVn

Windows desktop-first SME business software, beginning with Sales & Inventory Foundation. Local v1 invokes Application handlers in process through the Local adapter.

## Active roadmap — v1a-pro

**Updated:** 2026-10-20. The owner selected **Business Compact + inline Goods Receipt editing (BCI)** as the sole active roadmap and retired all older unimplemented plans.

| Active plan | State | Next task |
|---|---|---|
| [Business Compact and inline Goods Receipt](docs/plans/20261020-001-business-compact-inline-goods-receipt.md) | Design/planning documented; implementation and new runtime acceptance **NOT STARTED** | BCI-T00: prove the existing grid/editor/accessibility integration on Windows |

[Execution index](docs/plans/README.md) owns task order. Read the [UI and interaction design](docs/BUSINESS_COMPACT_UI.md) and [acceptance matrix](docs/BUSINESS_COMPACT_ACCEPTANCE.md) with the plan. The current receipt still uses read-only line cells and external editors; this documentation change does not implement inline editing.

## Current implementation

- Product, WinForms and all ten active test projects target `net48`. Desktop and separate admin migrator publish as Windows x86 directories. AppCore/net8 remains PARKED / NOT SHIPPED.
- Clean Architecture, MediatR, FluentValidation, ErrorOr, explicit UnitOfWork, LINQ to DB plus typed native SqlClient, SQL Server and DbUp. No provider/runtime migration is part of BCI.
- Accepted test-only Respawn 4 reusable owned fixtures, deterministic reset/seed, dedicated fresh exceptions and direct `dotnet test`/Test Explorer. The old SQL harness, runner, gate manifest and approval-file flow are removed.
- WinForms uses CommunityToolkit.Mvvm, Bootstrap5WinFormUI and BootstrapSourceGrid. Four catalogs, Goods Receipt Draft/Post, stock ledger/balances/card, code-defined permissions and one configured superuser exist.
- RESP and L2DB are completed in their approved scopes. [GUI-P5](docs/GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20) is also complete in its signed scope: preserve its frozen revisions and the T05 Warehouse 125% Dark Down NOT TESTED exception. This accepted external-editor workflow is the BCI regression baseline.
- Release schema metadata correctly reflects schema 9/scripts 001–009. Production installation, recovery, support and release sign-off remain unaccepted; retiring their old plan is not a production certification. See [release prerequisites](docs/DEFERRED_WORK.md#release-prerequisites).

## Working with Codex

Read [AGENTS.md](AGENTS.md), [AI_CONTEXT.md](AI_CONTEXT.md), the [active plan index](docs/plans/README.md), the BCI design/acceptance documents, and the sole [TEST_POLICY.md](docs/TEST_POLICY.md). Run affected tests during implementation; BCI final acceptance requires a new frozen Windows/package candidate and the actual cell-editing evidence defined by its matrix. Documentation-only changes require document checks, not a new SQL/GUI run.

Completed plans and their evidence remain historical references. [Retired/out-of-scope directions](docs/DEFERRED_WORK.md) are not a second execution queue; [History](docs/HISTORY.md) records their immutable provenance.

## Documentation

[Docs index](docs/README.md) · [Development status](docs/DEVELOPMENT_PLAN.md) · [Architecture](docs/ARCHITECTURE.md) · [Persistence](docs/PERSISTENCE.md) · [Security](docs/SECURITY.md) · [Test policy](docs/TEST_POLICY.md) · [Release packaging](docs/LOCAL_RELEASE_PACKAGING.md)
