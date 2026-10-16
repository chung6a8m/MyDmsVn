# Development Plan — MyDmsVn v1 → v2

**As of 2026-10-16:** P0–P4.1 foundational implementation is complete. **P4.1 is closed by explicit product-owner acceptance**, supported by automated L3 and attended packaged Login/manual UI evidence; the long idle-expiry/relogin observation was not completed and is recorded as an accepted *P4.1-only* evidence exception in [P4.1 verification](P4_1_VERIFICATION.md). P5 implementation/automated gates are complete, but its two interactive acceptance checks remain open; P6 release gates are open. TESTGATE-T01–T03 have been implemented. All target frameworks in the checked-in code remain unchanged until NET8-PAUSE is implemented.

## Active roadmap — execute in this exact order

| Order | Plan | Purpose / actual status |
|---:|---|---|
| 1 | [TESTGATE-T01–T03 — task gates](plans/20261016-001-task-gates-and-modular-tests.md) | **Verified/complete; retained ACTIVE for mandatory context and acceptance policy**, do not rerun T01–T03 by default |
| 2 | [NET8-PAUSE — temporary second-TFM removal](plans/20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8.md) | **Proposed/not implemented**; pause `net8.0` / WinForms `net8.0-windows` in multi-target projects; preserve configurable TFM tooling and restore path |
| 3 | [TESTGATE-T04/T05 — modular test projects](plans/20261016-002-modular-test-projects.md) | Proposed; execute only after verified task/final and TFM-scope transition |
| 4 | [SEC-REBUILD — permission system](plans/20261016-003-permission-system-rebuild.md) | Proposed; replace legacy permission system only after T04/T05 verification |
| 5 | [P5 GUI acceptance + P6 local hardening remainder](plans/20261016-004-remaining-p5-acceptance-and-p6-hardening.md) | **Open**; remaining two P5 manual UI gates + P6 T01–T04 operational gaps and T05–T08 release checks |
| 6 | [P7 — remote API](plans/20261008-008-phase-7-remote-api.md) | Not started; requires completed P6 and restored `net8.0` support (unless independently revised and approved) |

The [active plan index](plans/README.md) is the canonical Codex starting point. The [historical plan archive](plans/archive/README.md) contains P0/P1/P2/P3/P3.1/P4/P4.1, SQLPERF, completed P5 implementation and the original full-scope P6 plan. **Archive relocation does not prove unfinished GUI/release tasks passed.**

## Operating and verification rules

- Read `AGENTS.md`, this roadmap and the active task plan before editing; do not start a later numbered plan if earlier implementation/gates are blocked.
- Inner-loop L1/L2 checks are task-focused; use only the relevant project/selector and genuinely needed real-SQL categories. No routine full-solution SQL testing after each edit.
- Run manifest-owned final acceptance **once after inputs freeze**, subject to rerun when gate-affecting inputs change. Use strict unfiltered real SQL, complete logical-case inventory/discovery/TRX reconciliation, watchdog, explicit instance approval, owned-db cleanup and zero mandatory skips.
- Today the repository still targets both SQL TFMs. The NET8-PAUSE plan **does not change that fact**: after its implementation is accepted, run the explicit approved *active* framework matrix, retaining historical dual-target evidence and reactivation procedure.
- Do not mark blocked/manual/unperformed tests green. P4.1's specific owner acceptance does not waive P5/P6 conditions; P7 is a separate future scope with an independently enforceable HTTP boundary.
- Leave SQLPERF's completed benchmarking and fixture safety decision in the archive. DbUp, RepoDb, Dapper, UnitOfWork, platform security constraints and package pins are unchanged by this documentation PR.

## Historical phase completion record

| Phase | Scope and historical status | Source / remaining work |
|---|---|---|
| P0 | Foundation **complete**, 2026-10-08 | [Archive](plans/archive/20261008-002-phase-0-foundation.md) |
| P1 | Persistence **complete**, 2026-10-08 | [Archive](plans/archive/20261008-003-phase-1-persistence.md) |
| P2 | Application pipeline **complete**, 2026-10-09 | [Archive](plans/archive/20261008-004-phase-2-application-pipeline.md) |
| P3 | Security foundation **complete**, 2026-10-09 | [Archive](plans/archive/20261008-005-phase-3-security.md) |
| P3.1 | Initial security provisioning and sync **complete**, 2026-10-14 | [Archive](plans/archive/20261013-001-initial-security-provisioning-permission-sync.md); supersession planned by SEC-REBUILD, not yet implemented |
| P4 | Desktop shell foundation **complete**, 2026-10-09 | [Archive](plans/archive/20261008-006-phase-4-desktop.md) |
| P4.1 | Real Login/session **owner-accepted complete**, 2026-10-16 | [Archive](plans/archive/20261013-002-desktop-login-session.md); long idle-expiry UI observation unverified/accepted exception |
| P5 | Business features/automated gates implemented; **interactive acceptance open** | [Archived implementation plan](plans/archive/20261008-001-sales-inventory-foundation.md); [active remaining P5 GUI checks](plans/20261016-004-remaining-p5-acceptance-and-p6-hardening.md) |
| P6 | Detailed T01–T04 implementation/automated checks completed; **operational and release validation open** | [Archived original](plans/archive/20261008-007-phase-6-local-hardening.md); [active remaining P6 work](plans/20261016-004-remaining-p5-acceptance-and-p6-hardening.md) |
| SQLPERF | T01–T06 **complete**, 2026-10-15 | [Archive](plans/archive/20261014-001-sql-integration-test-performance-optimization.md) |
| P7 | Not started | [Active P7](plans/20261008-008-phase-7-remote-api.md) |

## Work item completion report

For each implementation task record the changed scope/files, selected task gate with actual commands, supported TFMs and executed/discovered/passed/failed/skipped counts, SQL ownership/watchdog/approval outcome (sanitized), manual/operational results, blockers and the next task ID. Historical evidence stays attached to its original revision. Changes to supported runtime/release targets require explicit product-owner decision and a reversible migration path.
