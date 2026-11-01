# WEB3-04 — Receipt fixture and bounded HTTPS/SQL/browser acceptance

**Owner authorization:** 2026-11-01, "Cho phép mở rộng fixture và chạy nghiệm thu thật", for the frontend WEB3-04 real acceptance dependency. **State:** bounded F03/F04 fixture and real HTTPS/SQL/browser prerequisite verified on Chromium and Edge. The two earlier failed Chromium attempts remain recorded separately; this does not mark B04 or all B05 complete, approve production or constitute Owner closeout.

## Candidate and scope

Base `main`: `6e960787a9cd5a656a3a5cab0fe0bf0194a0f24c`, actual merge of PR #76. B02/B03 executable source still matches tested implementation `d2e7e3f3709a7e97cf91c7d4558c560303fa498d`. Existing B02/B03 documentation's premerge wording is historical; this inspection uses actual Git state. Branch: `codex/web3-04-receipt-pilot`.

Native worktree creation was attempted but rejected because this frontend chat's roots exclude the backend repository. The authorized isolated fallback is `C:/Users/pdchung/.codex/worktrees/web3-04-receipt-pilot/MyDmsVn`; backend main was not edited.

Only the existing `WebHostIisExpressTests` fixture changes. A separate Receipt selector shares its SQL allocation, migrations/provisioning, TLS/host, browser process supervision and owned cleanup. Default Sales-only selection remains separate. Receipt actors have explicit ordinary permissions, including Write/Post without Read. The added Product uses a separate stock key so existing Sales costs remain unchanged. No production/business source, schema, provisioner/grant algorithm, dependencies, CEX or historical WEB2 evidence changes.

The [new runbook](../../development/WEB3_RECEIPT_PILOT_RUNBOOK.md) owns mode/manifest/results and execution details. It documents separate receipt-key SQL proof, complete unique Posted set reconciliation and preservation of Sales source/COGS checks. This is not the ordered same-key B04 cross-module scenario.

## Preparation checks actually performed

Windows NT 10.0.26300.0, .NET SDK 8.0.427, Node v22.14.0, net48 Release; locked project restore succeeded. Commands from the isolated backend worktree:

```powershell
dotnet restore tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj --locked-mode
dotnet build tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --list-tests --filter FullyQualifiedName~WebHostIisExpressTests
```

Build succeeded with 0 warnings and 0 errors. Discovery found exactly three packaged-host cases: original System.Web smoke, original Sales browser selector and new `Published_React_Receipt_browser_uses_real_HTTPS_SQL_and_independent_receipt_stock`. Discovery executed **zero tests**. At this preparation checkpoint, no runtime PASS was claimed and real-SQL/browser validation was still pending; subsequent attempts and results are recorded below.

Documentation checks: `git diff --check` passed; 20 local Markdown links across the three changed Markdown files resolved. All four changed files follow repository encoding/EOL rules (C# UTF-8 BOM/CRLF; Markdown UTF-8 without BOM/CRLF).

Read-only inspection found an existing `0.0.0.0:44321` TLS binding; no binding/certificate was installed or changed. The designated SQL connection existed in the process (value not printed); publish directory, TLS test thumbprint and browser-directory inputs were initially unset. No IIS, SQL allocation or browser was started at this preparation checkpoint. Build/discovery and documentation checks alone did not establish seed/oracle execution.

## Preparation checkpoint — remaining acceptance and retained boundaries

At preparation, the remaining step was to run the new selector once per Chromium/Edge against fresh positively owned SQL fixtures after coordinator review, using the stable Web production build and both real pilot spec files. The subsequent sections record those candidates, wrapper/browser counts, first failures/corrections, independent SQL reconciliation and verified cleanup. A skipped, missing or empty browser selection never establishes acceptance.

B04 Receipt→Sale→later Receipt valuation, Inventory read UI, all B05/F06 acceptance, attended IME/accessibility, full IIS/production and durable mutation recovery remain outside this bounded fixture checkpoint. Existing WEB2 and B02/B03 results remain their original candidate-specific evidence.

## Focused review correction — numeric receipt identity

Review base: `c4d516d62645514133bb9647abaa2daf215b5ef5`. The reviewer identified that string-unique `"1"` and `"01"` could both parse to receipt 1 while count/aggregate checks failed to prove receipt 2 was included. This was a defect in the fixture's identity proof, not production behavior.

Receipt result IDs now require canonical positive int64 string tokens, are parsed once, reject duplicate numeric IDs and must exactly equal the physical SQL Posted receipt ID set. The validated numeric array preserves result-row order for later physical source/ledger checks. The comparison/parsing helper remains in the test fixture assembly; no test-only helper was added to production.

Focused pure `WebReceiptPilotResultTests` exercise the reported alias/omission, a same-count different set, duplicate numeric IDs, noncanonical/overflow/numeric JSON IDs, order preservation and MaxInt64. Before the fix, the old comparison behavior produced 12 executed cases: 10 FAIL, 2 PASS, 0 SKIP. After the fix, 12 PASS, 0 FAIL/SKIP. These cases allocate no SQL or host resources.

```powershell
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --filter FullyQualifiedName~WebReceiptPilotResultTests --logger 'trx;LogFileName=receipt-result-red.trx' --results-directory TestResults/web3-04-result-red
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --filter FullyQualifiedName~WebReceiptPilotResultTests --logger 'trx;LogFileName=receipt-result-green.trx' --results-directory TestResults/web3-04-result-green
```

Both selected commands built the affected net48 Security target successfully. Raw TRX stays local/ignored. At that pure-test correction checkpoint, SQL/IIS/browser execution was still NOT RUN pending scoped rereview and the coordinated pilot; those pure tests did not establish live seed/oracle acceptance.

## Chromium attempt 1 — fixture process-budget failure

Parent granted the exclusive SQL owner slot after scoped fixture rereview was clean. Backend fixture source: `b0f48bec19edbffda1e147e1e45bd260bab07aa7`; frontend production candidate: `66764fb1687519ea502dcc9c4e3a2e4ecb99fc67`. The existing publish at `publish/web3-04-receipt-b0f48be-host` was made with locked host restore and current Release build (no SkipBuild); published host DLL matched compiled SHA-256 `3A028277ACC9A85F1DEF89A0FCA991C2282E017786B0089E198B240BD76EC031`. Nine frontend dist files were copied and each published hash matched its source; ignored `TestResults/web3-04-live-preparation/web-artifact-sha256.json` retains the full artifact list.

| Stable frontend asset | SHA-256 |
|---|---|
| `assets/index-CZjqufWD.js` | `F8B2B9ED18D9FA81448F248D5AB035BA2DCE4D6FD97122C69D5B77212C1B33DA` |
| `assets/index-BFtattde.css` | `EE89832BFAEC4202E6DD71295535774051F18E3B089C165987468542EC41B875` |

Read-only gate verified the actual `Microsoft.Data.SqlClient` parser sees the existing designated test input with explicit master, nonempty instance, no attachment and UserInstance disabled; no instance/configuration was changed or secret printed. IIS Express version `10.0.26013.1000`; Playwright `1.64.1`; installed Edge `156.0.4314.58`. Existing LocalMachine certificate chain was trusted; ports were free. The process-local publish/TLS/browser-directory/project inputs followed the runbook; browser project was Chromium and both specs were selected for the intended 15 cases (8 Sales + 7 Receipt).

```powershell
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter FullyQualifiedName~Published_React_Receipt_browser_uses_real_HTTPS_SQL_and_independent_receipt_stock --logger 'trx;LogFileName=receipt-chromium.trx' --results-directory TestResults/web3-04-receipt-chromium
```

Actual result: **one .NET wrapper FAIL, zero PASS/SKIP; zero browser tests executed**. The fixture allocated/migrated its disposable schema11 database and reached the healthy HTTPS host, then passed a ten-minute combined-mode timeout to `GuiOwnedProcess.RunAsync`. That existing supervisor rejects budgets above five minutes before `NativeChild.Start`, producing `ArgumentOutOfRangeException(timeout)`. This is a fixture callsite defect, not a frontend or business failure; no browser SQL acceptance is claimed.

Cleanup: the sole inner error was the invalid timeout, with no cleanup failure. Existing finally awaited exact IIS termination/drain, SID-owned login cleanup and ownership-checked database disposal. Post-run inspection found no IIS/testhost/vstest processes, both ports free, and temporary manifest/public PEM removed. Browser result, browser output and screenshots did not exist because Node was never started. The ignored first-attempt TRX remains at the command's results path. Edge was not started.

Parent triage authorized the minimal correction: both modes now pass the existing five-minute budget at the shared callsite. `GuiOwnedProcess`, its maximum and cleanup policy remain unchanged. No frontend or production/business source changed. No constant-mirroring test was added. The affected Release/net48 Security build succeeded with 0 warnings/errors; existing `WebReceiptPilotResultTests` reran with 12 PASS, 0 FAIL/SKIP using `--no-restore --no-build`, logger `receipt-result-timeout-regression.trx` and results path `TestResults/web3-04-timeout-regression`. `git diff --check` passed. These pure tests did not prove the live timeout correction; at triage, Chromium attempt 2 was held for scoped rereview, a fresh fixture and a different results path. Edge was kept dependent on Chromium success and cleanup.

## Chromium attempt 2 — pilot logout synchronization failure

Reviewed fixture correction: `ed532fccb267cb5e543bd6b19bbf016265b255e1`; production frontend and pilot tests still at `66764fb1687519ea502dcc9c4e3a2e4ecb99fc67`. A fresh owned fixture ran all 15 selected browser cases. Actual browser outcome: **14 PASS, 1 FAIL, 0 SKIP**; .NET wrapper: **1 FAIL, 0 PASS/SKIP**, approximately 113 seconds. All eight Sales and the first six Receipt cases passed. The final Read-only actor case timed out at username input while immediately navigating after logout. Parent triage identified the test synchronization gap: the pilot must await logout200 and rendered login before its next `page.goto`. No frontend runtime change or weakened permission check was needed.

The browser nonzero exit prevented the backend oracle from executing. Three browser Post records totaled Receipt Q4.0001/V80.0020, but **this attempt has no independent SQL oracle PASS**. Existing finally awaited exact IIS/process drain, SID-owned login and ownership-checked database cleanup; no cleanup failure accompanied the browser error. Post-run processes were absent, ports free, manifest/PEM removed. Ignored artifacts were preserved before another browser could overwrite the frontend output:

- `TestResults/web3-04-receipt-chromium-attempt2/receipt-chromium-attempt2.trx`
- `TestResults/web3-04-receipt-chromium-attempt2/browser-artifacts/`
- The fixture's ignored `browser-output.log` and `browser-result.json` in its unique publish run directory.

Edge was held until triage/review and a successful fresh Chromium fixture. The reviewed five-line pilot-only fix is `3fbbe60bb0896d344f3e0a7d4ac0b8d0e3b1d516`; comparison against runtime SHA66764fb changed only `tests/pilot/web3-receipt-live.spec.ts`. Production assets were neither rebuilt nor recopied after that test-only change.

## Successful bounded Chromium and Edge acceptance

**Exact tested identities:** frontend production source/build `66764fb1687519ea502dcc9c4e3a2e4ecb99fc67`; browser pilot tests `3fbbe60bb0896d344f3e0a7d4ac0b8d0e3b1d516`; backend fixture `ed532fccb267cb5e543bd6b19bbf016265b255e1`. The host executable/business source remains B02/B03 runtime `d2e7e3f3709a7e97cf91c7d4558c560303fa498d`; published DLL and frontend artifact hashes remain those recorded for attempt 1. Source SHA and published bytes were checked before execution. Later evidence commits change documentation only.

Each command used the existing explicit nonproduction master input, process-local published root, reviewed frontend checkout, existing trusted TLS thumbprint and exactly one project. No credentials/SQL input/signing key were printed, copied into served Web files or committed. Chromium and Edge ran sequentially with separate fresh database/login/host fixtures, no test retry and no change to TLS, Origin, rate limiting or process supervision.

```powershell
$env:MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY = 'C:/Users/pdchung/.codex/worktrees/web3-04-receipt-pilot/MyDmsVn/publish/web3-04-receipt-b0f48be-host'
$env:MYDMSVN_WEB_TEST_TLS_THUMBPRINT = '507B9A85A6D859303D032C9828756BC64E3EB340'
$env:MYDMSVN_WEB_TEST_BROWSER_DIRECTORY = 'C:/Users/pdchung/.codex/worktrees/web3-04-goods-receipt/ReactWebUI'
$env:MYDMSVN_WEB_PILOT_PROJECT = 'chromium'
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter FullyQualifiedName~Published_React_Receipt_browser_uses_real_HTTPS_SQL_and_independent_receipt_stock --logger 'trx;LogFileName=receipt-chromium-attempt3.trx' --results-directory TestResults/web3-04-receipt-chromium-attempt3

$env:MYDMSVN_WEB_PILOT_PROJECT = 'edge'
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter FullyQualifiedName~Published_React_Receipt_browser_uses_real_HTTPS_SQL_and_independent_receipt_stock --logger 'trx;LogFileName=receipt-edge-attempt1.trx' --results-directory TestResults/web3-04-receipt-edge-attempt1
```

| Invocation | Actual browser result | Actual .NET wrapper | Oracle / cleanup |
|---|---|---|---|
| Chromium attempt 1, backend b0f48be | 0 executed | 1 FAIL, 0 SKIP | No browser oracle; owned cleanup completed |
| Chromium attempt 2, backend ed532fc / old pilot | 14 PASS, 1 FAIL, 0 SKIP | 1 FAIL, 0 SKIP | Oracle not reached; owned cleanup completed |
| Chromium attempt 3, reviewed pilot3fbbe60 | **15 PASS, 0 FAIL/SKIP**, browser29.4s | **1 PASS, 0 FAIL/SKIP**, approximately53s | Independent SQL oracle PASS; owned cleanup PASS |
| Edge fresh attempt 1, same reviewed candidate | **15 PASS, 0 FAIL/SKIP**, browser36.1s | **1 PASS, 0 FAIL/SKIP**, approximately59s | Independent SQL oracle PASS; owned cleanup PASS |

The successful runs each executed the same eight existing Sales and seven Receipt cases; these are not thirty distinct business scenarios. Receipt coverage is real UI Create→replacement Update→Post beside a dirty Sales session, preserved UI row identity/new server line ID, exact decimals and ack without version, stale409 preserving edits, committed Create/Update/Post response loss with single dispatch and unknown close/logout lock, Write/Post without Read followed by actual Get403, and server403 against Read-only actor Create/Update/Post. Sales retains its real HTTPS/auth/cookie/CSP, refresh/actor isolation, 200 duplicate/gift lines, rowversion and unknown/Post acknowledgement regressions. Attended IME/accessibility is not inferred from these automated cases.

### Independent physical SQL outcomes

For **each** successful fresh fixture, the oracle read the physical Posted receipt ID set and required exact equality with the three canonical unique browser result IDs. It verified source lines, header/ledger actors and timestamp, one economic movement and matching valuation per Posted line, independent decimal rounding and the dedicated Receipt key balance. All source/ledger/valuation/stock equalities passed:

- Receipt: three Posted receipts, three source lines and three ledger/valuation companions; **actual Q4.0001/V80.0020 equals expected Q4.0001/V80.0020**. Breakdown: UI receipt Q2.0001/V40.0020, lost-response Post Q1/V20, Write/Post-without-Read Q1/V20.
- Sales: three Posted sales, 203 source lines/matching ledger-value companions; posted quantity203.0001 and frozen COGS3045.0015. Original stock key **actual Q796.9999/V11954.9985** equals opening Q1000/V15000 minus those totals. Receipt additions did not alter these costs.
- Only the two expected balance keys existed. Draft-only/rejected operations were not counted as Post, duplicated/missing Posted receipt identities would fail exact set reconciliation, and no browser-derived stock projection replaced the SQL check.

The oracle runs before the wrapper returns success; values above are matched physical SQL assertions, not merely sums from a browser response. This separate-key scenario does **not** verify B04's ordered same-key Receipt→Sale→later Receipt revaluation/immutability matrix, concurrency or full rollback matrix.

### Environment, artifacts and cleanup

Windows NT `10.0.26300.0`; .NET SDK `8.0.427`; net48 / VSTest `17.11.1`; Node `22.14.0`; IIS Express `10.0.26013.1000`; Playwright `1.64.1`; Chromium/headless-shell `156.0.8078.4` revision1248; Edge `156.0.4314.58`. A sanitized read-only query on the same designated master input observed SQL ProductVersion **14.0.2140.2**, ProductLevel **RTM**, **Developer Edition (64-bit)**, EngineEdition3; that metadata connection was disposed and no server/database identifier was published.

All four attempted pilot invocations allocated their own disposable schema11 database and restricted SID-tracked login. All completed the existing awaited IIS termination/drain, owned login cleanup and ownership-token-checked database disposal; neither failed invocation had an additional cleanup failure. After each invocation, inspection found no IIS/testhost/vstest worker, free44321/18081 ports and removed manifest/public PEM. Successful wrapper completion also includes the awaited disposal checks. No orphan was adopted or dropped by prefix and no application database was used. The SQL owner slot was exclusive throughout; no other SQL owner was run in parallel.

Ignored TRX, browser logs/results and safe UI screenshots remain per attempt. Successful browser artifact directories were copied to `TestResults/web3-04-receipt-chromium-attempt3/browser-artifacts` and `TestResults/web3-04-receipt-edge-attempt1/browser-artifacts` before output reuse. No trace/video/HAR or credential artifacts are committed. Final documentation verification: `git diff --check` passed; all 16 local links in the two final changed Markdown files resolved; UTF-8 without BOM and CRLF were verified. Only evidence and the owning bounded checkpoint changed after the successful executable candidate; no executable rerun was needed for those Markdown edits.

**Remaining gates:** B04 integrated same-key stock/cost/rollback/concurrency acceptance, Inventory F05 and whole F06/B05 completion, attended Telex/VNI/Narrator, full IIS, operational release and production remain NOT RUN / outside this bounded prerequisite. Owner acceptance/merge decisions remain separate from the recorded automated PASS.
