# WEB3-04 — bounded Receipt pilot fixture preparation

**Owner authorization:** 2026-11-01, "Cho phép mở rộng fixture và chạy nghiệm thu thật", for the frontend WEB3-04 real acceptance dependency. **State:** fixture implemented; first owned HTTPS/SQL attempt failed before browser startup, acceptance PENDING / NOT PASS. The preparation checks and first failure below remain separate. This does not mark B04 or all B05 complete.

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

Build succeeded with 0 warnings and 0 errors. Discovery found exactly three packaged-host cases: original System.Web smoke, original Sales browser selector and new `Published_React_Receipt_browser_uses_real_HTTPS_SQL_and_independent_receipt_stock`. Discovery executed **zero tests**. No new runtime PASS is claimed; the fixture's required real-SQL and browser validation remains pending.

Documentation checks: `git diff --check` passed; 20 local Markdown links across the three changed Markdown files resolved. All four changed files follow repository encoding/EOL rules (C# UTF-8 BOM/CRLF; Markdown UTF-8 without BOM/CRLF).

Read-only inspection found an existing `0.0.0.0:44321` TLS binding; no binding/certificate was installed or changed. The designated SQL connection exists in this process (value not printed); publish directory, TLS test thumbprint and browser-directory inputs were initially unset. No IIS, SQL allocation or browser was started. Build/discovery and documentation checks alone do not establish seed/oracle execution.

## Remaining acceptance and retained boundaries

Run the new selector once per Chromium/Edge against fresh positively owned SQL fixtures after coordinator review, using the stable Web production build and both real pilot spec files. Record each candidate, wrapper/browser counts, first failures/corrections, independent SQL reconciliation and verified cleanup in this new evidence record. A skipped, missing or empty browser selection is NOT RUN.

B04 Receipt→Sale→later Receipt valuation, Inventory read UI, all B05/F06 acceptance, attended IME/accessibility, full IIS/production and durable mutation recovery remain outside this bounded fixture checkpoint. Existing WEB2 and B02/B03 results remain their original candidate-specific evidence.

## Focused review correction — numeric receipt identity

Review base: `c4d516d62645514133bb9647abaa2daf215b5ef5`. The reviewer identified that string-unique `"1"` and `"01"` could both parse to receipt 1 while count/aggregate checks failed to prove receipt 2 was included. This was a defect in the fixture's identity proof, not production behavior.

Receipt result IDs now require canonical positive int64 string tokens, are parsed once, reject duplicate numeric IDs and must exactly equal the physical SQL Posted receipt ID set. The validated numeric array preserves result-row order for later physical source/ledger checks. The comparison/parsing helper remains in the test fixture assembly; no test-only helper was added to production.

Focused pure `WebReceiptPilotResultTests` exercise the reported alias/omission, a same-count different set, duplicate numeric IDs, noncanonical/overflow/numeric JSON IDs, order preservation and MaxInt64. Before the fix, the old comparison behavior produced 12 executed cases: 10 FAIL, 2 PASS, 0 SKIP. After the fix, 12 PASS, 0 FAIL/SKIP. These cases allocate no SQL or host resources.

```powershell
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --filter FullyQualifiedName~WebReceiptPilotResultTests --logger 'trx;LogFileName=receipt-result-red.trx' --results-directory TestResults/web3-04-result-red
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --filter FullyQualifiedName~WebReceiptPilotResultTests --logger 'trx;LogFileName=receipt-result-green.trx' --results-directory TestResults/web3-04-result-green
```

Both selected commands built the affected net48 Security target successfully. Raw TRX stays local/ignored. SQL/IIS/browser execution remains NOT RUN pending coordinated pilot; the pure correction tests do not establish live seed/oracle acceptance. The correction is ready for scoped rereview before that pilot.

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

Parent triage authorized the minimal correction: both modes now pass the existing five-minute budget at the shared callsite. `GuiOwnedProcess`, its maximum and cleanup policy remain unchanged. No frontend or production/business source changed. No constant-mirroring test was added. The affected Release/net48 Security build succeeded with 0 warnings/errors; existing `WebReceiptPilotResultTests` reran with 12 PASS, 0 FAIL/SKIP using `--no-restore --no-build`, logger `receipt-result-timeout-regression.trx` and results path `TestResults/web3-04-timeout-regression`. `git diff --check` passed. These pure tests do not prove the live timeout correction; Chromium attempt 2 must use a fresh owned fixture and different results path after scoped rereview. Edge remains dependent on its successful result and cleanup.
