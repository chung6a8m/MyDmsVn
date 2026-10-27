# UIN-T12 — bounded numeric paste and measured editing

Date: 2026-10-24. Scope: UIN-A13 on interactive Windows, net48/x86. This is source and native grid evidence; packaged Login and dated Owner review remain T13.

## Candidate and implementation

App baseline `4c9f8c329bcfc2e39df59f144ea188bfa5b3b221`; source/test candidate `d040b556534cf6cc480b64a8f34915ed3173ab64`. Reviewable slices: detached planner `7f19fdc`, atomic draft application `326c612`, native coordinator/performance `1b0426a`, two-theme DPI observations `e77d05b`, Goods no-write assertion `1480232`, and read-only Goods target rejection `d040b55`. BootstrapSourceGrid pin `a2c2cb749ac5656a56b1cfebfc7718b889d95152`, Bootstrap5WinFormUI pin `910c19493b36d1db2fb0b92b7fc436f263ec564d`, SourceGrid pin `f4e457b43582bf01892f50bdc74aa480531e5944`; no vendor pin/source change.

`Ctrl+Shift+V` on a selected Sales/Goods receipt numeric rectangle reads clipboard text. The adapter admits any active cell edit first, captures selected existing line references and the editor's current document/revision, then plans the whole TSV with T04 numeric profiles. The cap is 262,144 characters, one/two columns and selected existing rows. Invalid/ragged/readonly/stale input rejects the batch; Sales maps addressed parse errors into its T02 inline validation store, and both adapters expose the error through existing workspace feedback. A valid patch reaches the application once. The application preflights every value and total, rechecks identity/revision/editability, writes all backing values under the mutation gate, then publishes line/editor notifications. Save/Post remain explicit; no SQL/API write or row/Product creation occurs. The shortcut and command require the grid's UI thread. SourceGrid's per-cell clipboard writes are not used.

## Correctness verification

TRX files are local evidence, not committed proprietary output. Actual selected/discovered/executed rows below have zero failures/skips. The earlier declared125 probe correctly failed when physical display measured 96 DPI/100%; it is excluded from PASS.

| Scope | Local TRX | Outcome |
|---|---|---|
| Application atomic patch, stale ordinary edit and reentrant observer | `TestResults/t12-final-session/t12-final-session.trx` | 6/6 PASS |
| Detached TSV planner, direct project/filter run | `TestResults/t12-final-planner/t12-final-planner.trx` | 12/12 PASS |
| Planner, Sales/Goods native command, editor/grid/control regressions | `TestResults/t12-final-gui-d040/t12-final-gui-d040.trx` | 155/155 PASS |
| T03 lookup/inline/validation regressions on final candidate | `TestResults/t12-t03-d040-repeat/t12-t03-d040-repeat.trx` | 174/174 PASS |
| Sales/Goods Light/Dark paste on measured native100 | `TestResults/t12-paste-native100-d040/t12-paste-native100-d040.trx` | 4/4 PASS; four PerMonitorV2 observations at monitor 96 DPI/100% |
| Sales/Goods Light/Dark paste on measured native125 | `TestResults/t12-paste-native125-d040/t12-paste-native125-d040.trx` | 4/4 PASS; four PerMonitorV2 observations at monitor 120 DPI/125% |
| Release app x86 build | `dotnet build src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -c Release -p:Platform=x86 --no-restore -v:q` | PASS, zero warnings/errors |

One prior final-candidate T03 run (`t12-t03-d040.trx`) had 173/174 PASS: a native click hit a `Label` over the grid in the Light theme. The physical display still measured 100%/96 DPI. The affected Light/Dark method then passed 2/2 in isolation (`t12-t03-isolated.trx`), and the same 174-row selection passed on repeat. The first failure remains recorded as an intermittent host hit-test risk; no product change was inferred from it.

Release app executable SHA-256 `0E16E0F3500A7F188B1E805377D0DFC29C276BDF01F6A2FCEF9B837B283324D6`; Application DLL SHA-256 `1A174602D3F4BC3A2D26419386CC61A02809D0A3510D39DC5FEF3F1643BD6DF8`; WinForms DLL SHA-256 `1112D0861BDCD5CF114EB1CC5E18134C4F4793A07B8D7C00D805066CEAD067A3`; GUI test DLL SHA-256 `9685D0AECB986412B8C0AEFD324710D2677A4FECBFD8C6CF54AF9B2008D353B2`. SDK 8.0.426, VSTest 17.11.1, local non-production machine `LAPTOP-DLPF4MIR`. No SQL connection used.

## Performance observation

Opt-in `ReceiptEditingPerformanceTests` ran on final source/test candidate `d040b55` at `LAPTOP-DLPF4MIR`, Windows NT 10.0.26300.0, x86 process. Workload: build a grid with 1/50/200 Sales lines; edit and commit Quantity at the middle line through the native adapter, alternating 2.5/3.5; invoke projection separately; run 14 times, discard first two, report p50/p95 elapsed milliseconds and current-thread allocated bytes. Goods uses 25 lines and the same middle-line workload; this is an observation size, not a Goods capacity limit. Retained controls are counted recursively after forced GC; editor property events and grid invalidations are counted for all 14 iterations. Timing is not asserted as correctness.

| Workload | Edit/commit p50/p95 ms | Edit bytes p50/p95 | Projection p50/p95 ms | Projection bytes p50/p95 | Events / invalidations / controls |
|---|---:|---:|---:|---:|---:|
| Sales 1 | 3.85 / 4.37 | 20,252 / 20,404 | 0.01 / 0.03 | 928 / 1,068 | 392 / 132 / 10 |
| Sales 50 | 4.97 / 5.74 | 146,092 / 146,244 | 0.14 / 0.15 | 42,872 / 43,024 | 392 / 131 / 10 |
| Sales 200 | 6.38 / 10.08 | 531,292 / 531,772 | 0.79 / 1.02 | 171,272 / 171,424 | 392 / 131 / 10 |
| Goods 25 | 11.75 / 13.03 | 392,448 / 393,072 | 0.02 / 0.02 | 2,172 / 2,312 | 42 / 173 / 110 |

`TestResults/t12-perf-d040/t12-perf-d040.trx` records the final observations. Before the narrow Sales adapter change, the same fixture on a local pre-optimization source state measured Sales 200 edit/commit p50/p95 30.70/34.07 ms and 5,328,660/5,328,812 allocated bytes, with 523 grid invalidations. The earlier local source state was not pinned as a commit; only the final candidate's result is reproducible by revision. The demonstrated hotspot was `OnEditorChanged` scanning the grid on every broad editor property notification. It now refreshes editability for `SaleId`/`CanEdit`; line notifications own value projection. The final candidate reduced that workload to 131 invalidations and about 0.53 MB edit allocation. Focused grid/control regressions passed after the change.

Review thresholds for this named machine and frozen fixture, not CI pass/fail assertions: Sales 200 edit/commit p95 25 ms and p95 allocation 1 MB; Goods 25 p95 30 ms and p95 allocation 0.8 MB; live fixture control counts at the end of the run at most 12 Sales and 120 Goods. Recheck distribution and environment before treating a threshold breach as a regression. No general throughput/capacity claim is made.

## Boundary

This slice changes local draft presentation only. Receipt arithmetic, posting, status/permission/recovery gates, SQL schema, DTOs, vendor pins and package form remain governed by their existing contracts. The performance fixture measures one middle-row numeric edit and projection, not typing latency across every row or package-wide memory retention. T13 still owns packaged Login and bounded Owner review.
