# MyDmsVn

Windows desktop-first SME business software, beginning with Sales & Inventory Foundation. Local v1 invokes Application handlers in process through the Local adapter.

## Current roadmap — v1a-pro

**Updated:** 2026-10-21. PR #40 is merged at `6040d7bcfca698fc9daba8268bb94e4454e8eacb`. **BCF-T00–T06 are COMPLETE / OWNER ACCEPTED** in the [final bounded scope](docs/evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21). No new implementation roadmap is selected.

| Completed plan | State | Handoff |
|---|---|---|
| [Lookup correctness and visual parity](docs/plans/20261021-001-business-compact-lookup-and-visual-parity.md) | COMPLETE / OWNER ACCEPTED | Preserve exact candidate evidence, visual differences, six wide125 NOT TESTED, deferred T04 Minor and native100 composite limitation |

[Execution index](docs/plans/README.md) owns order. Read the [BCF design](docs/BUSINESS_COMPACT_REFINEMENT.md), [acceptance ledger](docs/BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) and [three original references](docs/design-references/business-compact/README.md). The focused Windows lookup gate must pass before visual implementation. Final BCF evidence and explicit Owner exceptions are recorded; production release prerequisites remain UNACCEPTED / UNSCHEDULED.

## Current implementation

- Product, WinForms and all ten active test projects target `net48`. Desktop and separate admin migrator publish as Windows x86 directories. AppCore/net8 remains PARKED / NOT SHIPPED.
- Clean Architecture, MediatR, FluentValidation, ErrorOr, explicit UnitOfWork, LINQ to DB plus typed native SqlClient, SQL Server and DbUp. BCF changes no provider/runtime/API/schema contract.
- Accepted test-only Respawn 4 reusable owned fixtures, deterministic reset/seed, dedicated fresh exceptions and direct `dotnet test`/Test Explorer. The old SQL harness, runner, gate manifest and approval-file flow are removed.
- WinForms uses CommunityToolkit.Mvvm, Bootstrap5WinFormUI and BootstrapSourceGrid. Four catalogs, Goods Receipt Draft/Post, stock ledger/balances/card, code-defined permissions and one configured superuser exist.
- PR #40 delivers Business Compact and real inline Product/Quantity/Unit-cost editors through the app-owned receipt grid adapter. BCI is [complete in its signed UI scope](docs/BUSINESS_COMPACT_ACCEPTANCE.md#final-t06-acceptance-and-handoff--2026-10-21); its evidence remains historical. The list/header lookup binding defects remain to be repaired by BCF.
- RESP, L2DB and GUI-P5 are completed baselines. Preserve their exact revisions and accepted exceptions, including GUI-P5's Warehouse 125% Dark Down NOT TESTED result; no old result transfers automatically to BCF.
- Release schema metadata reflects schema 9/scripts 001–009. Production installation, recovery, support and release sign-off remain [unaccepted/unscheduled prerequisites](docs/DEFERRED_WORK.md#release-prerequisites).

## Working with Codex

Read [AGENTS.md](AGENTS.md), [AI_CONTEXT.md](AI_CONTEXT.md), the [active plan index](docs/plans/README.md), the BCF design/acceptance documents, and the sole [TEST_POLICY.md](docs/TEST_POLICY.md). Use focused tests during implementation; BCF-T02 gates visual work and BCF-T06 requires a new frozen Windows/package candidate and comparisons against all three designs. Documentation-only changes require document checks, not a new SQL/GUI run.

Completed plans and evidence remain historical references. [Retired/out-of-scope directions](docs/DEFERRED_WORK.md) are not a second execution queue; [History](docs/HISTORY.md) records their provenance.

## Documentation

[Docs index](docs/README.md) · [Development status](docs/DEVELOPMENT_PLAN.md) · [Architecture](docs/ARCHITECTURE.md) · [Persistence](docs/PERSISTENCE.md) · [Security](docs/SECURITY.md) · [Test policy](docs/TEST_POLICY.md) · [Release packaging](docs/LOCAL_RELEASE_PACKAGING.md)
