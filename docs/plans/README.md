# Active phase roadmap — Codex entry point

**Date:** 2026-10-16. Execute the following items **in exactly this order**. The first plan remains active for context despite verified completion; it must not be reimplemented. No other plan in this directory is an active work item.

| Order | Active plan | State |
|---:|---|---|
| 1 | [20261016-001-task-gates-and-modular-tests.md](20261016-001-task-gates-and-modular-tests.md) | TESTGATE-T01–T03 **verified complete**, retained for reference |
| 2 | [20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8.md](20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8.md) | T01–T04 implemented; T05 envelope rerun pending after runner authorization fix `5b1813c` |
| 3 | [20261016-002-modular-test-projects.md](20261016-002-modular-test-projects.md) | Proposed TESTGATE-T04/T05, dependent on preceding gates |
| 4 | [20261016-003-permission-system-rebuild.md](20261016-003-permission-system-rebuild.md) | Proposed, dependent on modularized tests |
| 5 | [20261016-004-remaining-p5-acceptance-and-p6-hardening.md](20261016-004-remaining-p5-acceptance-and-p6-hardening.md) | Outstanding P5.1/P5.4 interactive checks, P6 T01–T04 operational gaps, P6 T05–T08 and G1–G5 |
| 6 | [20261008-008-phase-7-remote-api.md](20261008-008-phase-7-remote-api.md) | Future P7 API/HTTP parity; re-enable `net8.0` before implementing the net8 host |

## Acceptance, archiving and current TFM scope

- [Historical/superseded plans archive](archive/README.md) contains completed P0/P1/P2/P3/P3.1/P4, owner-accepted P4.1, completed SQLPERF, and the original P5/P6 plans **whose remaining tasks were explicitly transferred to item 5**. No unfinished P5/P6 check was falsely marked green by archiving.
- P4.1 closed by the owner's **2026-10-16 explicit acceptance** of existing automated L3 plus partial packaged manual UI. The 30-minute idle-expiry/relogin observation was not completed, and its specific waiver does not close P5/P6. See [verification](../P4_1_VERIFICATION.md).
- NET8-PAUSE T01–T04 are implemented; the T05 envelope on `78e5dbc` was invalidated by runner authorization fix `5b1813c` and must be rerun. Unrelated PRs/`main` follow their checked-in matrix. Retired net8 tests are PARKED/NOT RUN, never PASS. Preserve explicit framework flags, SQL approval/inventory/ownership and the [full restoration procedure](../REACTIVATE_NET8.md).
- Use [Development Plan](../DEVELOPMENT_PLAN.md), [Codex policy](../CODEX_TEST_EXECUTION_POLICY.md), [Test Gate Matrix](../TEST_GATE_MATRIX.md) and root [AGENTS.md](../../AGENTS.md). Task-scoped L1/L2 verification prevents per-edit full SQL reruns; a single final strict full gate still requires approved real SQL and an exact frozen revision.

When handing off a task, report completed scope, exact test commands/results, missing gates and next active task. Do not restart historical SQLPERF or P4.1 without a new approved change request.
