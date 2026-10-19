# Development plan — v1a after RESP/L2DB

**Updated:** 2026-10-19. The authoritative work queue is [docs/plans/README.md](plans/README.md). The accepted persistence/test infrastructure and the remaining product release work are distinct milestones.

## Current implementation and completed plans

| Work | State | Meaning |
|---|---|---|
| P0–P4 foundation and Local Login/session | Implemented; historical scoped exceptions retained | Clean Architecture, Application handlers/authorization, Local adapter, controlled DbUp, net48 WinForms |
| P5 business code | Implemented, **interactive acceptance OPEN** | Four catalogs, Goods Receipt Draft/Post, stock ledger/balance/card, concurrency and SQL invariants |
| RESP fixture reuse | **COMPLETE** | Respawn reset/seed/reuse on reviewed real-SQL business cases plus fresh lifecycle/principal/migration exceptions; [accepted evidence](RESPAWN_VERIFICATION.md#pr-36-review-follow-up) |
| L2DB persistence replacement | **COMPLETE in plan scope** | LINQ to DB for Catalog/Inventory, typed native SqlClient for Identity/Security/operations; T07 correctness/package/limited authenticated Local smoke [accepted](L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke) |
| RELEASE-MANIFEST-01 | **CLOSED narrowly** | Shipped schema support metadata reads real policy for 001–009; not clean-install/production acceptance |

There are ten active net48 test projects and two shipped net48/x86 package directories (desktop and admin migrator). AppCore/net8.0-windows is parked. SQL tests use direct `dotnet test`/Test Explorer with positively owned fresh and Respawn fixtures. The previous standalone SQL framework, runners/manifests/approval files and permission-synchronization workflow are removed.

## Remaining implementation and acceptance order

| Order | Plan | Objective | Required exit |
|---:|---|---|---|
| 3 | [GUI-P5](plans/20261019-001-full-winforms-gui-p5-acceptance.md) | Complete unattended STA reliability, all GUI control/test gaps, real packaged-login E2E, attended catalog/lookup and receipt/stock workflows | GUI-P5-T00–T06 evidence; both `REMAIN-P5-GUI-01/02` PASS, exact unfiltered GUI/Session results and resource cleanup |
| 4 | [REL / P6](plans/20261019-002-local-v1-p6-release-acceptance.md) | Build/test clean release package; verify Windows/SQL topology, principal/config/audit, schema/client skew, controlled upgrade, backup/restore, recovery, full regression and owner decisions | REL-T00–T06, five G1–G5 gates supported by frozen candidate artifacts, actual evidence and named owner sign-off |

REL preparation can overlap GUI-P5; the final REL G4 requires GUI-P5. P6 is **NOT READY FOR PRODUCTION** until its full release matrix is accepted. Historical NET8-PAUSE duration waiver and partial P4.1 idle observation do not waive P5/P6 requirements.

## Source of truth and execution

[TEST_POLICY](TEST_POLICY.md) is the sole execution policy; [TEST_STRATEGY](TEST_STRATEGY.md) owns coverage; [tests/README](../tests/README.md) provides direct project commands. Select affected tests during task work; full real SQL/GUI and installed-package scenarios are milestone-specific. Do not revive deleted test-gate tooling or add a global runner/lease. GUI needs interactive Windows; SQL needs explicitly approved non-production resources and verified database ownership. Missing/aborted/skipped mandatory scenarios block acceptance.

Record task, commit, candidate package hashes, Windows/TFM/SQL configuration, actual selected/discovered/executed test cases and Theory rows, PASS/FAIL/SKIP/BLOCKED, UI observations and positively owned cleanup. Accept only evidence from the candidate and scope named by each gate; Markdown-only refresh is not runtime validation.

## Later backlog

[P7 HTTP/remote, net8 reactivation and new business modules](DEFERRED_WORK.md) remain deferred. Current schema migration admits only Empty/Compatible; prior prefix 001–008 is not a supported automatic upgrade. Legacy credential support/exclusion, signing and advertised OS/SQL matrix are open explicit release decisions owned by REL-T00/T06.
