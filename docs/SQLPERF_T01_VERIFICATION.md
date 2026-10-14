# SQLPERF-T01 verification — 2026-10-15

## Outcome

Gate T01 **passed** on commit `cd3374ff295b86c2e38ea665c16cbbed5b356621`. The approved non-production SQL Server completed one labeled warm-up and three accepted measured passes for each target framework, with the same source inventory, no failed or skipped mandatory SQL cases, complete timing/migration reconciliation, and clean ownership reconciliation.

SQLPERF-T02 may use this revision as its unchanged baseline. This result does not complete P4.1, authenticated P5 acceptance, or P6 release gates.

## Sanitized environment

| Item | Recorded value |
|---|---|
| Git SHA | `cd3374ff295b86c2e38ea665c16cbbed5b356621` |
| .NET SDK | `8.0.425` |
| OS | Windows NT `10.0.26200.0` |
| SQL Server | `14.0.2130.4`, Developer Edition (64-bit) |
| SQL maximum server memory | 1536 MB |
| Approval fingerprint | `2d160e60c982…` |
| Source/discovery inventory | 210 method/cases per TFM |
| Executed TRX cases | 213 per run, including expanded theory rows |

The raw connection string, approval-file path, SQL host identity, database names, journals, TRX files, and timing JSONL remain ignored and local under `.sqlperf/`.

## Commands and actual results

| Gate | Actual result |
|---|---|
| Runner preflight with the User-scope approval loaded into the process | Exit 0 before any benchmark DDL |
| Release solution build | Passed, 0 warnings / 0 errors |
| SQL test harness, `net48` / `net8.0` | 50 / 50 passed on each TFM |
| Runner tests, including `OwnedProcessRunnerTests` | 68 / 68 passed |
| Accepted baseline | 8 / 8 runs valid; every TRX 213 passed, 0 failed, 0 skipped/not-executed |
| Ownership reconciliation | 143 records per accepted run; all `Disposed`; no recovery artifact |
| Full Release regression after baseline | Application 133 / 133 per TFM; Desktop 200 / 200 per TFM; Architecture 5 / 5; Integration 213 / 213 per TFM; Harness 50 / 50 per TFM; Runner 68 / 68 |

The final regression command was:

```powershell
dotnet test MyDmsVn.sln -c Release --no-build --no-restore -m:1 --logger "console;verbosity=minimal"
```

Both SQL TFMs ran sequentially in the accepted benchmark. No accepted run contains a mandatory skip.

## Instrumentation overhead

Matched full-suite runs used the same commit and inventory with timing disabled and enabled. All four runs passed 213 / 213 with positively reconciled ownership.

| TFM | Timing off | Timing on | Delta |
|---|---:|---:|---:|
| `net48` | 555,013.198 ms | 546,949.497 ms | -1.45% |
| `net8.0` | 534,405.665 ms | 536,900.892 ms | +0.47% |

Both absolute deltas are below the 5% Gate T01 threshold. The negative `net48` delta is normal run-to-run variation, not a performance claim.

## Accepted baseline

| TFM | Warm-up TRX | Measured TRX elapsed | Runner wall-clock p50 / p95 |
|---|---:|---:|---:|
| `net48` | 545,006 ms | 547,012; 554,377; 554,749 ms | 554,981.459 / 555,336.485 ms |
| `net8.0` | 544,075 ms | 542,117; 532,577; 540,124 ms | 540,718.071 / 542,674.892 ms |

Sanitized measured-run stage totals and the highest class contributions were:

| TFM | Database create | Migration | Database drop | Largest class contributions |
|---|---:|---:|---:|---|
| `net48` | 142,307.986 ms | 572,207.408 ms | 31,386.496 ms | Schema compatibility 31.221%; goods receipt 25.931%; migration 10.401%; principal provisioning 8.037% |
| `net8.0` | 145,590.012 ms | 565,881.874 ms | 31,741.113 ms | Schema compatibility 30.580%; goods receipt 26.600%; migration 10.406%; principal provisioning 8.379% |

The report labels DMV snapshots **unavailable** because no snapshot was captured. Preflight verified the SQL-version-specific permission, but this evidence makes no DMV wait, CPU, I/O, or memory delta claim and does not substitute zero values.

## Invalid-run and cleanup evidence

An initial baseline exposed Win32 error 1175 (`ERROR_UNABLE_TO_REMOVE_REPLACED`) while atomically replacing the ownership journal. A second matched-overhead attempt reproduced it after the initial short retry. These runs were rejected and never included in the baseline or overhead calculation.

The journal writer now retries only error 1175 within a bounded two-second window; all other I/O failures still fail closed. A deterministic test fails the first five replace attempts before succeeding on both TFMs. The child-process fixture also launches its descendant without a visible shell and proves the descendant is terminated, eliminating the stray PowerShell/Windows Terminal side effect.

The two invalid artifact roots contained five remaining live test databases: two quarantined crash-window records and three positively owned `Created` records. Before cleanup, each candidate was revalidated against the strict generated-name form, current approval ID/fingerprint, live database metadata, ownership marker run ID, and ownership token. A fresh preflight passed; exactly those five databases were dropped and a post-check found zero live test databases. Historical invalid journals remain unchanged as audit evidence.

Negative guardrail tests for missing/expired approval, live-identity mismatch, wrong effective database, and changed-server disposal assert that zero destructive SQL is issued. They passed as part of every current full integration run on both TFMs.

## Go / no-go

**GO for SQLPERF-T02.** Use commit `cd3374ff295b86c2e38ea665c16cbbed5b356621` and the accepted medians above as the matched baseline. Preserve the dual-TFM, real-SQL, inventory, watchdog, negative-mutation, and ownership gates. Re-establish a baseline if the source inventory or measurement environment changes materially.
