# WEB4 B05 — recovery fixture and integrated pilot

Date: 2026-11-02, Asia/Bangkok. **VERIFIED CANDIDATE; B05-A fixture and B05-B integrated verification complete, Owner acceptance/merge pending.** Owner selected frontend WEB4-05/F04 and explicitly approved the bounded backend fixture and nonproduction SQL/IIS Express pilot. [Owning plan](../../plans/20261101-003-web4-reliability-server.md) · [Runbook and private interface](../../development/WEB4_RECOVERY_PILOT_RUNBOOK.md).

## Candidate and scope

Backend base `ca26a6c91bbbfe21807b9519bd2c4a7974f0e3d9`, fresh branch `codex/web4-05-failure-pilot-backend`; B05-A initial fixture source `57563dd61181991749977c2e2db0606ede719215`, corrected SQL-to-JSON projection source `52b6f42325eae1f93445a7536a789351b91c26ac`, quoted SQL identifier correction `4d3c75e3ec5c0cb2f165418137d800e5ae5c0762`. Frontend initial base `b7d36c1813d94da2a6c6c68617d1c596136300d4`; final frontend browser/test source `79c7e5a6117029dbc8e6968976b71fda103c88a3`, documentation head `9d9fd84aad5143b03012f8fd82f4c2b23a8cdbef`, paired with backend fixture source `4d3c75e3ec5c0cb2f165418137d800e5ae5c0762`. B04 runtime remains `bd871518fd162927d938babd5535a2ddf91c2ad5`; B01 corpus `1c603c6ac57e59a3f7b4bc6ca976b2d531c079aa`, manifest SHA256 `0acab190e7fe126d53c13c0510a1958365e5a6e1fda78a4321da78297e0b3f1a` unchanged.

Test-only extension to the existing Security.Tests owned IIS Express fixture: exact12/`web4` mode, new private manifest, same-user ACL/token-limited administrative pipe, strict result/journal/source union, independent physical source/ledger/value/moving-average oracle. Existing WEB2/WEB3 selectors, production source/schema/grants/dependencies, TLS configuration and shared process supervisor are unchanged. New backend worktree uses Git fallback because the desktop managed-worktree tool rejected a backend source outside the frontend chat's workspace.

## SQL-free preparation

Locked restore and Release/net48 build succeeded on Windows with SDK 8.0.427 and VSTest 17.11.1 x64. First selected validator run **56 passed, 0 failed/skipped** (22 new WEB4 result/control rows plus 34 existing integrated/receipt result and framing rows). Final B05-A source confirmation `TestResults/web4-b05-fixture-ready2/b05-fixture-ready2.trx` also executed **56/56 PASS, 0 failed/skipped**, exit0, after the 12-check completion set and complete actor/cleanup assertions were present. This is one 56-row suite, not 112 distinct scenarios.

```powershell
dotnet restore tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --filter 'FullyQualifiedName~WebRecoveryPilotResultTests|FullyQualifiedName~WebIntegratedPilotResultTests|FullyQualifiedName~WebReceiptPilotResultTests|FullyQualifiedName~WebFrontendPortBridgeTests' --logger 'trx;LogFileName=b05-fixture-pure.trx' --results-directory TestResults/web4-b05-fixture-pure
```

Actual TRX remains local. This is a test fixture change, not a product behavior fix; no production TDD claim is made. Read-only resource inspection found existing IIS Express, localhost 44321 certificate binding and no listener on 44321/18081. The configured nonproduction master connection was detected without displaying its value. No SQL connection, host process, certificate installation or browser pilot was started during preparation.

## Development attempt and correction

The coordinator's first Chromium live invocation used the initial 10-check fixture DLL and exact published React assets. Browser **9 passed, 1 failed**; .NET wrapper **0 passed, 1 failed**, with no cleanup error. Failure was a frontend test locator matching both a Dockview region and its document section before the valuation Post. The prior completed browser cases are development evidence, not a final all-gates PASS; physical final oracle did not run after the browser failure.

A preparation rebuild overlapped that owned live invocation and failed with `MSB3027/MSB3021` because its testhost held the Security test DLL. It executed 0 tests and did not replace the active DLL. The agents stopped rebuilding until the live process drained. The following serialized `b05-fixture-ready2` build/run succeeded as recorded above. The source now requires 12 completion IDs, including same-key concurrency and live cross-tab; final integrated reruns remain separate evidence.

Chromium attempt2 executed **10 passed, 1 failed**, wrapper FAIL: the last scenario exhausted the shared fixture account's ordinary 10-login/5-minute limiter. Frontend reassigned no-write quota/security scenarios to the existing `alice` actor; the limiter and production configuration were unchanged. Chromium attempt3 executed **11/11 browser PASS** but wrapper FAIL at exact journal comparison, so it was not an overall pilot PASS. All three failed wrapper attempts reported no cleanup error.

The attempt3 oracle failure was reproduced SQL-free through the actual `SqlOracleExtensions.Read` IDataRecord mapper and journal-to-JSON projection. For SQL NULL Create target, `row.RequestTargetId?.ToString(...)` implicitly constructed Newtonsoft `JValue(String, null)`, whereas parsed browser JSON carries `JValue(Null)`. Both serialize as JSON null but strict `DeepEquals` correctly distinguishes their token types. The regression first ran **1 failed, 0 passed** (`web4-b05-null-projection-red`), explicitly ExpectedNull/ActualString. The fixture now emits `JValue.CreateNull()` for absent Create targets and preserves exact metadata comparison unchanged. Six projection rows cover every command kind and null/string target; the corrected suite ran **62/62 PASS, 0 failed/skipped**, Release/net48 exit0 (`TestResults/web4-b05-projection-fixed/b05-projection-fixed.trx`). No product wire, runtime fingerprint or mutation behavior changed.

Chromium attempt4 (`TestResults/web4-f04-chromium-final`, despite the provisional directory name) executed **11/11 browser PASS in 47.7s**, then wrapper FAIL at SQL parsing: `LineNo` is a reserved identifier in the independent valuation query. Earlier exact journal/source/Posted/ledger checks had completed, but final valuation and overall pilot were not PASS. The correction brackets `rl.[LineNo]`, `sl.[LineNo]` and `AS [LineNo]`, matching the schema; all new raw SQL identifiers were inspected. Economic assertions and strict identity checks are unchanged. This actual live failure supplies the regression reproduction; no string-mirror test was added. Rebuild and selected pure suite again passed **62/62, 0 failed/skipped** (`web4-b05-sql-identifier-fixed`); final fresh live reruns are still required.

Attempt4's fixture-owned connection captured sanitized current SQL metadata in `sql-environment.json`: **SQL Server 14.0.2140.2, RTM, Developer Edition (64-bit)**. Only version/level/edition were retained; no host/database identifier or connection value is published. No cleanup failure was reported for attempt4.

## Interim Chromium verification and Edge correction

Backend source `4d3c75e3ec5c0cb2f165418137d800e5ae5c0762` subsequently completed the full Chromium fixture: **11/11 browser PASS in 50.4s**, **1/1 .NET wrapper PASS in 1m14s**, including the independent SQL journal/source/ledger/valuation oracle and ownership-checked cleanup. Actual TRX is `TestResults/web4-f04-chromium-verified/web4-chromium-verified.trx`. This is a verified Chromium checkpoint, not the final paired-browser acceptance.

The first Edge invocation then executed **2 passed, 1 failed, 8 not run** in 10.6s; its .NET wrapper failed in 33s (`TestResults/web4-f04-edge-verified/web4-edge-verified.trx`). The lost-ack test helper initiated reload before its interception callback finished reading the durable operation, causing `page.evaluate` execution context destruction. Frontend synchronization correction `79c7e5a6117029dbc8e6968976b71fda103c88a3` awaits the lost-response outcome before reload. No application behavior or backend fixture was changed. The final paired rerun below uses this matched test candidate; Edge is not inferred PASS from Chromium. No owned-cleanup failure was reported.

## Hosted SQL-free coverage

The existing `backend-web.yml` only built/ran HttpHost.Tests and could not exercise B05's new pure validators. It now additionally locked-restores/builds Security.Tests and runs only the four explicit result/control/framing validator classes. The SQL connection variable is explicitly empty, no secret is referenced, and no broad SQL owner, IIS fixture, browser or deployment executes. Both TRX files must exist and show nonempty complete passing execution with no skipped/aborted rows; the recovery result also must contain every selected class. Only count summaries are uploaded.

Workflow PowerShell syntax inspection passed. The guard was exercised locally against the actual 62-row fixture TRX and rejected a sample with the required recovery class absent; this is configuration validation, not another 62 executed tests.

[Hosted Windows CI run 41372747630](https://github.com/chung6a8m/MyDmsVn/actions/runs/41372747630), job `125046518791` (`http-host`), is **PASS** for [PR #84](https://github.com/chung6a8m/MyDmsVn/pull/84) head `be93fc095e7ecd85bc133f79fc2a3c128c26fb66`. Logs show the actual checkout was synthetic merge `c548a950a2667778222ff6021c8d3d99eb0b962a` into base `ca26a6c91bbbfe21807b9519bd2c4a7974f0e3d9`. Locked restore and Release/net48 builds passed with zero warnings/errors; **506/506 HttpHost rows and 62/62 pure validator rows passed, 0 failed/skipped**. Both nonempty-complete guards and selected-class checks passed. The runner was Windows Server 2022, SDK 8.0.427, Framework release 533509. These are hosted SQL-free results, separate from local SQL/browser gates; final documentation-head CI remains to be recorded after publication.

## Focused Security SQL verification

The coordinator then ran the existing focused Security owner sequentially on the designated nonproduction master connection, completing at 08:16 local time. Actual TRX `TestResults/web4-b05-sql-security/web4-b05-sql-security.trx` reports **86 discovered/executed, 86 passed, 0 failed/skipped**, duration 6m43s, Release/net48, `--no-restore --no-build`. The opt-in frontend TestServer bridge was explicitly excluded because this milestone uses the separate real browser fixture; it is not a skipped mandatory row.

```powershell
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter '(FullyQualifiedName~WebOperationReadTests|FullyQualifiedName~WebOperationJournalTests|FullyQualifiedName~WebOperationHttpSqlTests|FullyQualifiedName~WebAuthHttpSqlTests|FullyQualifiedName~WebPrincipalProvisioningTests)&FullyQualifiedName!~Frontend_ports_recover' --logger 'trx;LogFileName=web4-b05-sql-security.trx' --results-directory TestResults/web4-b05-sql-security
```

These source-owned SQL cases cover same-key serialization and all-six HTTP replay, original authorized outcomes, status/epoch/fingerprint/current authority, tombstones, journal rollback, auth rotation and restricted principal provisioning. They do not establish OS process death at Commit. All resources are owned by the existing fixtures; no cleanup failure was reported.

The 86 rows comprise journal 22, protected read 24, HTTP operations 17, auth 7 and principal provisioning 16. Environment rechecked without SQL access: Windows `10.0.26300.0`, .NET SDK `8.0.427`, Framework `4.8.10008`/Release`534296`, IIS Express `10.0.26013.1000`, VSTest `17.11.1` x64. The final fixture-owned metadata capture independently confirmed SQL Server `14.0.2140.2`, RTM, Developer Edition (64-bit); no separate SQL connection was opened for environment discovery.

The coordinator's independent source review reported no remaining P1/P2 after corrections to browser coverage and sanitized failure diagnostics. This is source review, not Owner acceptance. Final paired browser and focused Inventory/migration results follow below.

## Final paired HTTPS/browser and physical SQL verification

The final pair used frontend browser/test source `79c7e5a6117029dbc8e6968976b71fda103c88a3`, unchanged production assets, and backend fixture source `4d3c75e3ec5c0cb2f165418137d800e5ae5c0762`. Each invocation created its own fresh schema12 database, restricted SID-owned login and IIS Express host with normal trusted HTTPS, one browser worker and zero retries.

| Browser | Actual browser rows | .NET wrapper | Independent SQL oracle / owned cleanup |
| --- | --- | --- | --- |
| Chromium | **11/11 PASS**, 50.6s, 0 skipped | **1/1 PASS**, 1m14s | **PASS / PASS** |
| Edge | **11/11 PASS**, 1.1m, 0 skipped | **1/1 PASS**, 1m29s | **PASS / PASS** |

TRX paths are `TestResults/web4-f04-chromium-complete/web4-chromium-complete.trx` and `TestResults/web4-f04-edge-complete/web4-edge-complete.trx`. Local sanitized wrapper logs are TEMP `web4-f04-live-chromium-complete.log` / `web4-f04-live-edge-complete.log`; the owned run directories are `iis-ec296dda6f5244d3a13d029a0845de02` / `iis-564f50728b4647dc9fafddfe3597c41d`. Each browser result contains **22 committed operations, 8 Posted documents and all 12 completion IDs**. Completion IDs are not extra tests, and the wrapper rows are separate from browser rows.

The oracle compared the complete physical actor/key journal union, all six command identities/fingerprints/epochs, every source/Create and Posted/Post relationship, exact one-to-one source/ledger/value rows and absence of Draft/orphan/extra stock movements. It independently recomputed moving average in actual posting order. Integrated receipts 10@10 + 10@20, Sale4 at frozen cost60, then backdated Receipt4@30 yield Q20/V360; the unrelated warehouse remains Q3/V120. A same-key concurrent Create produces one duplicate-product sale (7 paid + 13 gift), conflicting payload returns409, and its recovered lost-ack Post depletes Q20/V360 to zero with paid/gift COGS126/234 and unchanged earlier Sale cost60. The separate Receipt post has Q2/V40 and Sales post Q2/cost30. No product valuation helper or browser-reported balance substitutes for this SQL recomputation.

The host/process, login/session SID and database cleanup assertions passed without secondary failure. Private manifest and temporary public CA files were removed. Actual browser runtime: Playwright1.64.1, Chromium156.0.8078.4/revision1248 and Edge156.0.4314.58; Node22.14.0 and npm11.6.0. Both owned SQL metadata files independently report14.0.2140.2 / RTM / Developer Edition (64-bit). Windows/.NET/IIS versions are recorded above. Existing localhost44321 TLS binding/thumbprint `507B9A85A6D859303D032C9828756BC64E3EB340` was reused, with no trust/binding changes or TLS bypass.

Published artifact SHA256:

| Artifact | SHA256 |
| --- | --- |
| `bin/MyDmsVn.Server.HttpHost.dll` | `4ae478874f7bb9aeac6706b364516431f67601acb4ef13bd28017ca314b8d586` |
| Test fixture `MyDmsVn.Server.Infrastructure.Security.Tests.dll` | `a4cc6e90083e0cd950e49b9c383d11ed1c27cc6a959d2463280ac017cd101b37` |
| Published `wwwroot/index.html` | `7e9452a3c4fcdcd82f8c13bfd21a1ae84434efded224511de332cb30f217f093` |
| Published production JS `index-BJ1J40gf.js` | `7293ae7ad17a1b21f860abc849993aba81dd04ef383cf482daa99293c8c39fb6` |
| Published production CSS `index-B31yTrPj.css` | `1ca4f8307032398d70ecd50fbbae1d104f3d30fbf6cd87b767560d1b1be802a0` |

All nine published `wwwroot` files matched the frontend production build byte-for-byte. The SHA256 of their sorted relative-path/lowercase-SHA256 manifest, UTF-8 LF lines with no final newline, is `6b82b239c09b41357dd5ee974583cbd3348b3fdc8e6d68834d0e61d1e8e37fc8`.

## Focused Inventory and migration SQL confirmation

After both browser fixtures drained, the coordinator ran the remaining SQL owners sequentially, with a locked restore followed by Release/net48 selected tests. Inventory `WebOperationCommandTests` executed **7/7 PASS**, 0 failed/skipped, 34s; migrations `WebAuthMigrationTests` executed **17/17 PASS**, 0 failed/skipped, 1m26s. Actual TRX paths are `TestResults/web4-b05-inventory-final/web4-b05-inventory-final.trx` and `TestResults/web4-b05-migrations-final/web4-b05-migrations-final.trx`; local logs are TEMP `web4-b05-inventory-final.log` / `web4-b05-migrations-final.log`.

```powershell
dotnet restore tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj -c Release -f net48 --no-restore --filter FullyQualifiedName~WebOperationCommandTests --logger 'trx;LogFileName=web4-b05-inventory-final.trx' --results-directory TestResults/web4-b05-inventory-final
dotnet restore tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/MyDmsVn.Server.Infrastructure.Migrations.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/MyDmsVn.Server.Infrastructure.Migrations.Tests.csproj -c Release -f net48 --no-restore --filter FullyQualifiedName~WebAuthMigrationTests --logger 'trx;LogFileName=web4-b05-migrations-final.trx' --results-directory TestResults/web4-b05-migrations-final
```

The distinct focused SQL total is **110 passed (Security86 + Inventory7 + migrations17), 0 failed/skipped**, excluding the two browser wrapper rows. Inventory covers transaction-owned command/replay/concurrency boundaries; migrations cover exact schema/grant/admission boundaries. No cleanup failure was reported.

## Disposition and retained limits

B05/F04 is a **VERIFIED CANDIDATE**, with independent review reporting no remaining actionable P1/P2. Backend [PR #84](https://github.com/chung6a8m/MyDmsVn/pull/84) and the paired frontend candidate await final-head hosted CI, Owner acceptance and merge. The earlier hosted run retains its exact source limit; documentation publication does not imply a new CI result. B06/F05 operational closeout remains PLANNED; no roadmap archive or production acceptance is claimed.

Same-key resend UI remains disabled; absent status is not rollback proof. OS process-crash/socket loss at Commit, attended Telex/VNI/Narrator, full IIS and production remain NOT RUN/not approved. Lost browser acknowledgements and transactional concurrency above do not establish the separate process-crash/Commit gate. Production source/schema/grants/dependencies and historical WEB1–03 evidence are unchanged.
