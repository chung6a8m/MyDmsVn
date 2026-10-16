# NET8-PAUSE T01 baseline and candidate scope

**Plan:** `20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8`  
**Frozen baseline:** `62a92cf66cc94c71cd0506ad454bb4c8140b49c5`  
**Owner authorization:** explicit request on 2026-10-16 to implement the named plan; the candidate-only Stage B scope is the exact transition recorded here and in [ADR 0008](adr/0008-temporary-net8-target-pause.md).

## Baseline target and dependency inventory

| Project group | Baseline target | Candidate active target/status |
|---|---|---|
| `Contracts`, `SharedKernel`, `Server.Domain`, `Server.Application`, `Server.Infrastructure`, `Server.DbMigrator`, `Desktop.Application`, `Desktop.Infrastructure.Local` | `net48;net8.0` | `net48` |
| `Desktop.WinForms` | `net48;net8.0-windows` | `net48` |
| `Server.Application.Tests`, `Server.Infrastructure.IntegrationTests`, `SqlTestHarness.Tests` | `net48;net8.0` | `net48` |
| `Desktop.Tests` | `net48;net8.0-windows` | `net48` |
| `Desktop.App` | `net48` | `net48` active legacy host |
| `Desktop.AppCore` | `net8.0-windows` | Project/source retained; parked outside active solution/release matrix |
| `Architecture.Tests` | `net8.0` | Retarget to `net48` so its direct references remain compatible |
| `SqlTestRunner`, `SqlTestRunner.Tests` | `net8.0` | Retain as explicit tool-only exception |
| `SqlTestHarness` | `netstandard2.0` | Retain compatibility target |
| Vendored Bootstrap/SourceGrid projects | `net48;net8.0-windows` | Unmodified; active product graph consumes `net48` only |

The baseline solution contains 23 top-level projects including the three pinned vendor projects. The initialized vendor submodule is `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f`; nested vendor revisions remain pinned by that submodule. `global.json` remains on .NET SDK `8.0.400` with `latestFeature` roll-forward.

No active `net48` project references `Desktop.AppCore`. The only indispensable net8-only graph after the transition is runner tests → runner → `SqlTestHarness` (`netstandard2.0`). The runner test fixture currently forces both SQL TFMs and must be changed to the reviewed active SQL profile before post-pause evidence is accepted.

## Logical-case baseline

| Owner project | Manifest logical definitions | Expanded discovery |
|---|---:|---:|
| `SqlTestRunner.Tests` × tool-only `net8.0` | 160 | 193 |
| SQL integration × `net48` | 209 | 223 |
| SQL integration × `net8.0` | same 209 historical definitions | 223 |

The `net8.0` SQL discovery is historical after cutover and remains `PARKED / NOT RUN`. Logical IDs, normalized signatures, classifications and Theory-row locators are preserved. The candidate active profile must prove all 223 `net48` physical cases; counts alone are insufficient.

## Baseline gate finding and repair

The first clean-worktree T01 run exposed stale runner fingerprints at the frozen revision: the focused contract selection reported 12 passed and one failed, and the manifest-owned unfiltered Task reported 192 passed and one failed. Independent audit found 156 of 160 runner method fingerprints stale, while all 209 integration method fingerprints matched. A mechanical refresh changed only 201 `reviewedCodeFingerprint` fields (156 logical definitions plus 45 matching incomplete-fingerprint exemptions). It did not change a logical ID, row, classification, owner or framework.

After the refresh, the T01 focused selection passed 13/13 with zero skipped. This repair is prerequisite evidence, not a reimplementation or relabeling of historical Final results. A fresh manifest-owned Task remains required after the candidate manifest/profile work is complete.

## Candidate scope and fallback

The candidate may change only project/solution/package target scope, framework-selectable gate tooling, active/historical inventories, the required GUI watchdog, supporting tests and the documentation named by the plan. Business rules, schema/migrations, security behavior and production data flows are out of scope.

If any active project or essential tool cannot build, any inventory identity is lost, or any mandatory T05 gate is unavailable or fails, the candidate is `BLOCKED / NOT VERIFIED`. Restore the baseline target declarations and active dual profile; do not shrink tests or reinterpret historical evidence as a pass.
