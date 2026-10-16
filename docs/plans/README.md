# Active phase roadmap — Codex entry point

**Date:** 2026-10-16. Execute the following items **in exactly this order**. The first plan remains active for context despite verified completion; it must not be reimplemented. No other plan in this directory is an active work item.

| Order | Active plan | State |
|---:|---|---|
| 1 | [20261016-001-task-gates-and-modular-tests.md](20261016-001-task-gates-and-modular-tests.md) | TESTGATE-T01–T03 **verified complete**, retained for reference |
| 2 | [20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8.md](20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8.md) | Accepted and merged as PR #32 at `8e310778`; duration comparison waived, not measured |
| 3 | [20261016-002-modular-test-projects.md](20261016-002-modular-test-projects.md) | Active TESTGATE-T04/T05; T04-A baseline verified at `c7bc4c8` |
| 4 | [20261016-003-permission-system-rebuild.md](20261016-003-permission-system-rebuild.md) | Proposed, dependent on modularized tests |
| 5 | [20261016-004-remaining-p5-acceptance-and-p6-hardening.md](20261016-004-remaining-p5-acceptance-and-p6-hardening.md) | Outstanding P5.1/P5.4 interactive checks, P6 T01–T04 operational gaps, P6 T05–T08 and G1–G5 |
| 6 | [20261008-008-phase-7-remote-api.md](20261008-008-phase-7-remote-api.md) | Future P7 API/HTTP parity; re-enable `net8.0` before implementing the net8 host |

## Acceptance, archiving and current TFM scope

- [Historical/superseded plans archive](archive/README.md) contains completed P0/P1/P2/P3/P3.1/P4, owner-accepted P4.1, completed SQLPERF, and the original P5/P6 plans **whose remaining tasks were explicitly transferred to item 5**. No unfinished P5/P6 check was falsely marked green by archiving.
- P4.1 closed by the owner's **2026-10-16 explicit acceptance** of existing automated L3 plus partial packaged manual UI. The 30-minute idle-expiry/relogin observation was not completed, and its specific waiver does not close P5/P6. See [verification](../P4_1_VERIFICATION.md).
- NET8-PAUSE T01–T05 runtime/build/package/GUI/restoration gates pass at `6b0ce337`, with reviewer gate fixes re-verified at `6b2ff49`; PR #32 merged at `8e310778`. The owner waived the duration comparison at `09d00bfa` because no matching baseline exists; it remains unmeasured and no speedup is claimed. Current `main` follows the accepted active matrix; branches created before the merge follow their checked-in declarations. Retired net8 tests are PARKED/NOT RUN, never PASS. Preserve explicit framework flags, SQL approval/inventory/ownership and the [full restoration procedure](../REACTIVATE_NET8.md).
- Use [Development Plan](../DEVELOPMENT_PLAN.md), [Codex policy](../CODEX_TEST_EXECUTION_POLICY.md), [Test Gate Matrix](../TEST_GATE_MATRIX.md) and root [AGENTS.md](../../AGENTS.md). Task-scoped L1/L2 verification prevents per-edit full SQL reruns; a single final strict full gate still requires approved real SQL and an exact frozen revision.

When handing off a task, report completed scope, exact test commands/results, missing gates and next active task. Do not restart historical SQLPERF or P4.1 without a new approved change request.
