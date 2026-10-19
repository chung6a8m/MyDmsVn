# History and documentation reset

**Reset date:** 2026-10-18 · **Branch assessed:** `v1a` · **Baseline:** [c7ad32a4b0040f87417973eabd6c12757c54f7d5](https://github.com/chung6a8m/MyDmsVn/commit/c7ad32a4b0040f87417973eabd6c12757c54f7d5).

This page is historical provenance, not an execution queue or a test policy. Current work is [GUI-P5 then REL/P6 after completed RESP/L2DB](plans/README.md). Runtime behavior is described in the current reference documents and source; test execution follows [TEST_POLICY](TEST_POLICY.md).

## Established baseline

| Area | Historical status / qualification | Immutable evidence |
|---|---|---|
| Foundations | P0–P4 implementation and automated foundations exist | [Original phase archive](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/archive/README.md) |
| P3.1 | Provisioning/synchronization was historically implemented, then superseded by SEC-REBUILD | [Historical P3.1 record](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/P3_1_VERIFICATION.md) |
| P4.1 | Owner accepted Login/session on 2026-10-16; full packaged idle-expiry/relogin interval was not observed and remains a specific accepted exception, not PASS | [P4.1 record](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/P4_1_VERIFICATION.md) |
| P5 / P6 | Business functionality and automated work exist; P5 interactive and P6 operational/release acceptance remain open | [Unfinished-work source](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/20261016-004-remaining-p5-acceptance-and-p6-hardening.md) |
| SQLPERF | Prior measurement/optimization completed; the then-rejected fixture reuse and retained fresh-database strategy describe the old baseline | [T04 decision](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/SQLPERF_T04_DECISION.md), [T06 measurements](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/SQLPERF_T06_VERIFICATION.md) |
| NET8-PAUSE | PR #32 merged at `8e310778`; product/SQL/Desktop net48 active. Duration comparison explicitly waived at `09d00bfa` because no matched baseline existed; no measured speedup follows from that waiver | [Pause verification](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/NET8_PAUSE_T05_VERIFICATION.md) |
| TESTGATE / modularization | Task/final tooling and modular test projects were implemented; modularization accepted at `973109f`. The framework was later removed; modular projects remain | [Core plan](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/20261016-001-task-gates-and-modular-tests.md), [Module extraction](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/20261016-002-modular-test-projects.md) |
| SEC-REBUILD | T00–T06 accepted in PR #34; automated, packaged GUI and owner walkthrough evidence recorded at `0303a911` | [Security rebuild verification](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/SEC_REBUILD_VERIFICATION.md) |
| Stabilization | PR #35 merged at `8cfa2535`; latest recorded reviewer-round-3 evidence is at `6d71b711`. Proposed STAB-T03/T04 manifest-v2 work was not implemented by that merge | [Stabilization source, including final review section](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/20261017-001-testgate-stabilization.md) |
| L2DB direction | Owner selected 1A + 2A + 3A with active net48; assessment/planning committed at the baseline above | [Original L2DB plan](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/20261018-001-linq2db-net48-persistence-migration.md) |

Historical totals describe their own revisions and scope. They are not new RESP/L2DB results. The owner's narrow P4.1 and duration-comparison exceptions do not waive other release, reset, SQL or runtime requirements.

## What was removed from working documentation

- Completed/superseded plans under `docs/plans`, the old `docs/plans/archive`, and duplicate implementation/spec narratives under `docs/superpowers`.
- The previous `docs/CODEX_TEST_EXECUTION_POLICY.md` and `docs/TEST_GATE_MATRIX.md`; their replacement is [TEST_POLICY.md](TEST_POLICY.md). Coverage and command references no longer define competing policies.
- Historical verification/benchmark narratives formerly alongside current root `docs` references. Their exact contents remain in the immutable baseline and Git history.
- The raw [project-idea.md](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/project-idea.md), whose outdated examples are superseded by the current PRD, architecture, contracts and ADRs.

Pending requirements were consolidated into [DEFERRED_WORK.md](DEFERRED_WORK.md), with original scope links and unchecked acceptance. Removal from the working tree does not delete Git history, waive unfinished work or revert implemented code. The first documentation reset retained executable test tooling. The owner's subsequent complete framework-removal decision below supersedes that retention; RESP now extends the remaining local helpers directly.

## Subsequent owner decision — complete SQL framework removal

On 2026-10-18, after [the documentation reset at a5ee124](https://github.com/chung6a8m/MyDmsVn/commit/a5ee124bed72f2994c4ee712e660b91d0dc2f953), the owner explicitly selected complete removal of:

- `tests/test-gates.v1.json`.
- `MyDmsVn.SqlTestHarness` and its framework tests.
- SQL runner, runner tests and all dependent command wrappers under `tests/tools/`.
- Framework-only invocation/exemption inventories and measured migration instrumentation.

Their [immutable pre-removal tests tree](https://github.com/chung6a8m/MyDmsVn/tree/a5ee124bed72f2994c4ee712e660b91d0dc2f953/tests) remains available for provenance. These files are removed from current execution, not relocated under another framework name. The unimplemented STAB-T03/T04 manifest-v2 work is cancelled by this decision.

Business SQL tests and their six module owners remain, alongside Application, Architecture and two Desktop owners: ten active net48 test projects. Direct dotnet test/Test Explorer execution uses a local fresh-database helper with live instance/allocation/marker checks before drop. No approval-file, global lease, external watchdog or automatic orphan recovery service is carried forward. See [current test commands](../tests/README.md).

**Historical state immediately after framework removal (2026-10-18):** two infrastructure plans were then pending, RESP and L2DB. They were implemented/accepted subsequently in PR #36/#37; framework removal **alone** did not implement them or grant release acceptance. The current OPEN acceptance plans are GUI-P5 and REL/P6.

## Recover a historical source

Use the immutable links above or `git show c7ad32a4b0040f87417973eabd6c12757c54f7d5:<repository-relative-path>`. The full [baseline documentation tree](https://github.com/chung6a8m/MyDmsVn/tree/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs) remains available. A shallow clone may need that commit fetched before using `git show`.

Read historical imperative text as the policy of its date. Current owner decisions, [AGENTS](../AGENTS.md), the current execution-plan index and TEST_POLICY control new work.
