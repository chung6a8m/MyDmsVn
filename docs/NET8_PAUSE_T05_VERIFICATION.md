# NET8-PAUSE T05 candidate verification

**Status:** Runtime, build/package, GUI and restoration gates passed; same-protocol duration comparison remains open.
**Frozen candidate revision:** `6b0ce337978809cefa7ddaf54d74940fef062e49`
**Plan:** [NET8-PAUSE T05](plans/20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8.md)

## Scope and commands

The candidate profile activates `net48` for application, SQL integration and Desktop GUI projects. Product `net8.0` / `net8.0-windows` coverage remains `PARKED / NOT RUN`; the SQL test runner and its tests remain tool-only `net8.0`. The reviewed manifest fingerprint for this envelope was `bd67d42d88350fdc7deacabbbce341ae59326b58c202b1002629e194c0d3bfe5`.

At the frozen revision, the following gates were run:

- Manifest Task `NET8-PAUSE-T05`: 25/25 passed, 0 skipped.
- Focused runner authorization regression selection (`PrAcceptanceRunTests`): 10/10 passed after the `5b1813c` fix.
- Locked restore: `dotnet restore MyDmsVn.sln --locked-mode` passed.
- Release solution build: `dotnet build MyDmsVn.sln -c Release --no-restore` passed with 0 warnings and 0 errors.
- Local release package: `Publish-LocalRelease.ps1 -Version 0.0.0-net8pause` produced both net48 desktop and admin migrator packages and a release manifest.
- Manifest Final `NET8-PAUSE-T05`: non-GUI runner 222/222, Application 135/135, SQL harness 52/52, Architecture 5/5, all with 0 skips. These are unfiltered registered project gates.
- Strict SQL `pr-acceptance`: one unfiltered `net48` pass; 223 source methods, 223 discovered cases and 223 executed/passed, 0 failed/skipped; preflight, watchdog, ownership reconciliation and cleanup passed. Duration: 534.6 seconds.
- GUI D2: 230 source methods, 231 discovered and 231 executed; watchdog and GUI lease clean. Duration: 17.7 seconds.
- Restore drill: an isolated temporary worktree restored the dual-target project/solution/package/manifest state. Locked restore and full Release build passed with 0 warnings/errors; Server.Application was `net48;net8.0`, Desktop.Tests was `net48;net8.0-windows`, and the SQL net8 inventory bound completely (223 discovered / 223 bound). The temporary worktree was removed after verification.

The reportable aggregate results are above. Raw TRX, SQL timing records, approval file, ownership journals and temporary package/drill artifacts remain local and are not part of this repository change. The product net8 targets are not claimed as currently supported or tested by this candidate envelope.

## Duration comparison and remaining item

Candidate SQL Final duration was 534.6 seconds and GUI D2 was 17.7 seconds. The T01 baseline does not contain a comparable dual-TFM Final measurement under the same protocol and environment. Historical SQLPERF benchmark durations are a different protocol and are not a valid comparison. Therefore no speedup claim is made, and the plan's same-protocol comparison checkbox remains open pending comparable baseline evidence.

The earlier candidate envelope at `78e5dbc8bd26823b3f43c73daf68d49a1d025182` is superseded: review found that the single-framework `net48` exception was too broad for direct runner callers. Commit `5b1813cedeba41331d9dac13e52f57978fd0e3de` restricts the exception to manifest-authorized Final execution. The fresh envelope recorded here includes that fix.
