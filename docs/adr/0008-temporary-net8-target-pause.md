# ADR 0008 — Temporary .NET 8 application and test target pause

**Status:** Accepted for the NET8-PAUSE implementation candidate  
**Date:** 2026-10-16  
**Implementation:** [NET8-PAUSE](../plans/20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8.md)

## Context

The application and SQL test graph currently repeats compatible work on `net48` and `net8.0`, while shared WinForms code and tests repeat on `net48` and `net8.0-windows`. The strict SQL acceptance path correctly runs each active SQL TFM sequentially, but that makes ordinary Codex feedback expensive. The owner approved the named NET8-PAUSE plan and its candidate-only Stage B transition on 2026-10-16. This decision does not retroactively change `main` or unrelated PRs.

The baseline is commit `62a92cf66cc94c71cd0506ad454bb4c8140b49c5`. The detailed project, logical-case and discovery inventory is recorded in [the T01 baseline](../NET8_PAUSE_T01_BASELINE.md).

## Decision

Temporarily make `net48` the only active application, SQL-test and WinForms target. Preserve all modern source and framework-specific branches for restoration.

- Change the genuine dual-target application libraries and tests to `net48` only.
- Keep `MyDmsVn.Desktop.App` as the active desktop host.
- Park `MyDmsVn.Desktop.AppCore` by removing it from the active solution and release/package matrix without deleting its project or source.
- Retarget `MyDmsVn.Architecture.Tests` to `net48`, because it directly references application libraries that become `net48` only and uses no required .NET 8-only runtime API.
- Keep `MyDmsVn.SqlTestRunner` and `MyDmsVn.SqlTestRunner.Tests` on tool-only `net8.0`. Their product dependency is the retained `netstandard2.0` SQL test harness; their fixture-build prerequisite must select active SQL TFMs explicitly and must not make tool-host `net8.0` count as application or SQL coverage.
- Keep `MyDmsVn.SqlTestHarness` on `netstandard2.0`.
- Do not modify vendored Bootstrap/SourceGrid projects. The active host consumes their declared `net48` target; their modern targets remain vendor-owned restoration capability.
- Publish only the `net48` desktop and a `net48` admin migrator during the pause. A modern desktop package is `PARKED / NOT RUN`, never passed.
- Store the reviewed active and historical framework profiles separately. Strict Task/Final validation compares requested execution frameworks to the active profile and preserves historical logical-case identities without executing or counting them as passed.

The Stage B exception is bound only to this plan and implementation candidate. It becomes the normal active matrix only after T01–T05 acceptance and merge. It cannot authorize unrelated business, schema, security or concurrency changes.

## Re-enable and rollback

Reactivation follows [the reviewed procedure](../REACTIVATE_NET8.md): restore recorded dual declarations (`net48;net8.0` and `net48;net8.0-windows`), return AppCore and the modern release package to the active graph, restore dual manifest/profile arrays and runner fixture builds, then execute fresh full dual-TFM SQL, GUI, architecture, build and package acceptance. Historical results are never relabeled as current proof.

Rollback is required if the tool/reference graph is incoherent, strict inventory guarantees weaken, watchdog or cleanup becomes ambiguous, active release coverage is unacceptable, or the reduced-TFM Final fails. Rollback restores the baseline declarations and gate profile before rerunning affected gates.

P7 `Server.Api` and claimed modern WinForms parity remain blocked until .NET 8 is reactivated and freshly verified, unless a separate accepted architecture decision replaces that runtime plan.

## Alternatives considered

- Keep dual targeting and rely only on L1/L2 selection: safe but does not deliver the approved target-scope reduction.
- Retarget the SQL runner to `net48`: rejected because the existing process-tree, inventory and modern tool implementation is already verified on `net8.0`; changing it adds risk without reducing application/SQL execution scope.
- Keep narrow `net8.0` compatibility builds in application libraries for Architecture tests: rejected because it weakens the requested removal and is unnecessary after retargeting Architecture tests to `net48`.
- Modify vendor targets: rejected because the repository consumes a valid vendor `net48` target and the plan forbids automatic vendor rewrites.

## Consequences and required tests

The .NET 8 SDK remains required by `global.json` and tool projects. The pause is not a .NET SDK downgrade or permanent runtime deprecation. Active tests must prove the complete `net48` source/discovery/TRX bijection, zero mandatory SQL skips, approved SQL ownership/cleanup, bounded GUI process-tree execution and active Release packaging. Restoration must be drilled in an isolated worktree before acceptance.

Affected documents include `AGENTS.md`, `README.md`, `docs/TECH_STACK.md`, `docs/PROJECT_STRUCTURE.md`, `docs/TEST_STRATEGY.md`, `docs/CODEX_TEST_EXECUTION_POLICY.md`, `docs/TEST_GATE_MATRIX.md`, `tests/README.md`, release guidance and downstream active plans.
