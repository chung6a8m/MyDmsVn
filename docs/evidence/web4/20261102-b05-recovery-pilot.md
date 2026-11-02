# WEB4 B05 — recovery fixture and integrated pilot

Date: 2026-11-02, Asia/Bangkok. **IN PROGRESS; B05-A fixture prepared, B05-B live execution pending.** Owner selected frontend WEB4-05/F04 and explicitly approved the bounded backend fixture and nonproduction SQL/IIS Express pilot. [Owning plan](../../plans/20261101-003-web4-reliability-server.md) · [Runbook and private interface](../../development/WEB4_RECOVERY_PILOT_RUNBOOK.md).

## Candidate and scope

Backend base `ca26a6c91bbbfe21807b9519bd2c4a7974f0e3d9`, fresh branch `codex/web4-05-failure-pilot-backend`; B05-A initial fixture source `57563dd61181991749977c2e2db0606ede719215`, corrected SQL-to-JSON projection source `52b6f42325eae1f93445a7536a789351b91c26ac`. Frontend initial base `b7d36c1813d94da2a6c6c68617d1c596136300d4`; final mutually pinned candidates pending. B04 runtime remains `bd871518fd162927d938babd5535a2ddf91c2ad5`; B01 corpus `1c603c6ac57e59a3f7b4bc6ca976b2d531c079aa`, manifest SHA256 `0acab190e7fe126d53c13c0510a1958365e5a6e1fda78a4321da78297e0b3f1a` unchanged.

Test-only extension to the existing Security.Tests owned IIS Express fixture: exact12/`web4` mode, new private manifest, same-user ACL/token-limited administrative pipe, strict result/journal/source union, independent physical source/ledger/value/moving-average oracle. Existing WEB2/WEB3 selectors, production source/schema/grants/dependencies, TLS configuration and shared process supervisor are unchanged. New backend worktree uses Git fallback because the desktop managed-worktree tool rejected a backend source outside the frontend chat's workspace.

## SQL-free preparation

Locked restore and Release/net48 build succeeded on Windows with SDK8.0.427 and VSTest17.11.1 x64. First selected validator run **56 passed, 0 failed/skipped** (22 new WEB4 result/control rows plus34 existing integrated/receipt result and framing rows). Final B05-A source confirmation `TestResults/web4-b05-fixture-ready2/b05-fixture-ready2.trx` also executed **56/56 PASS, 0 failed/skipped**, exit0, after the12-check completion set and complete actor/cleanup assertions were present. This is one56-row suite, not112 distinct scenarios.

```powershell
dotnet restore tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --filter 'FullyQualifiedName~WebRecoveryPilotResultTests|FullyQualifiedName~WebIntegratedPilotResultTests|FullyQualifiedName~WebReceiptPilotResultTests|FullyQualifiedName~WebFrontendPortBridgeTests' --logger 'trx;LogFileName=b05-fixture-pure.trx' --results-directory TestResults/web4-b05-fixture-pure
```

Actual TRX remains local. This is a test fixture change, not a product behavior fix; no production TDD claim is made. Read-only resource inspection found existing IIS Express, localhost44321 certificate binding and no listener on44321/18081. The configured nonproduction master connection was detected without displaying its value. No SQL connection, host process, certificate installation or browser pilot was started during preparation.

## Development attempt and correction

The coordinator's first Chromium live invocation used the initial10-check fixture DLL and exact published React assets. Browser **9 passed, 1 failed**; .NET wrapper **0 passed, 1 failed**, with no cleanup error. Failure was a frontend test locator matching both a Dockview region and its document section before the valuation Post. The prior completed browser cases are development evidence, not a final all-gates PASS; physical final oracle did not run after the browser failure.

A preparation rebuild overlapped that owned live invocation and failed with `MSB3027/MSB3021` because its testhost held the Security test DLL. It executed0 tests and did not replace the active DLL. The agents stopped rebuilding until the live process drained. The following serialized `b05-fixture-ready2` build/run succeeded as recorded above. The source now requires12 completion IDs, including same-key concurrency and live cross-tab; final integrated reruns remain separate evidence.

Chromium attempt2 executed **10 passed, 1 failed**, wrapperFAIL: the last scenario exhausted the shared fixture account's ordinary10-login/5-minute limiter. Frontend reassigned no-write quota/security scenarios to the existing `alice` actor; the limiter and production configuration were unchanged. Chromium attempt3 executed **11/11 browserPASS** but wrapperFAIL at exact journal comparison, so it was not an overall pilot PASS. All three failed wrapper attempts reported no cleanup error.

The attempt3 oracle failure was reproduced SQL-free through the actual `SqlOracleExtensions.Read` IDataRecord mapper and journal-to-JSON projection. For SQL NULL Create target, `row.RequestTargetId?.ToString(...)` implicitly constructed Newtonsoft `JValue(String, null)`, whereas parsed browser JSON carries `JValue(Null)`. Both serialize as JSON null but strict `DeepEquals` correctly distinguishes their token types. The regression first ran **1 failed, 0 passed** (`web4-b05-null-projection-red`), explicitly ExpectedNull/ActualString. The fixture now emits `JValue.CreateNull()` for absent Create targets and preserves exact metadata comparison unchanged. Six projection rows cover every command kind and null/string target; the corrected suite ran **62/62 PASS, 0 failed/skipped**, Release/net48 exit0 (`TestResults/web4-b05-projection-fixed/b05-projection-fixed.trx`). No product wire, runtime fingerprint or mutation behavior changed.

## Hosted SQL-free coverage

The existing `backend-web.yml` only built/ran HttpHost.Tests and could not exercise B05's new pure validators. It now additionally locked-restores/builds Security.Tests and runs only the four explicit result/control/framing validator classes. The SQL connection variable is explicitly empty, no secret is referenced, and no broad SQL owner, IIS fixture, browser or deployment executes. Both TRX files must exist and show nonempty complete passing execution with no skipped/aborted rows; the recovery result also must contain every selected class. Only count summaries are uploaded.

Workflow PowerShell syntax inspection passed. The guard was exercised locally against the actual62-row fixture TRX and rejected a sample with the required recovery class absent; this is configuration validation, not another62 executed tests or hostedCI proof. **Final hosted WindowsCI is PENDING** until publication and an observed workflow run.

## Focused Security SQL verification

The coordinator then ran the existing focused Security owner sequentially on the designated nonproduction master connection, completing at08:16 local time. Actual TRX `TestResults/web4-b05-sql-security/web4-b05-sql-security.trx` reports **86 discovered/executed, 86 passed, 0 failed/skipped**, duration6m43s, Release/net48, `--no-restore --no-build`. The opt-in frontend TestServer bridge was explicitly excluded because this milestone uses the separate real browser fixture; it is not a skipped mandatory row.

```powershell
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter '(FullyQualifiedName~WebOperationReadTests|FullyQualifiedName~WebOperationJournalTests|FullyQualifiedName~WebOperationHttpSqlTests|FullyQualifiedName~WebAuthHttpSqlTests|FullyQualifiedName~WebPrincipalProvisioningTests)&FullyQualifiedName!~Frontend_ports_recover' --logger 'trx;LogFileName=web4-b05-sql-security.trx' --results-directory TestResults/web4-b05-sql-security
```

These source-owned SQL cases cover same-key serialization and all-six HTTP replay, original authorized outcomes, status/epoch/fingerprint/current authority, tombstones, journal rollback, auth rotation and restricted principal provisioning. They do not establish OS process death at Commit. All resources are owned by the existing fixtures; no cleanup failure was reported.

The86 rows comprise journal22, protected read24, HTTP operations17, auth7 and principal provisioning16. Environment rechecked without SQL access: Windows`10.0.26300.0`, .NET SDK`8.0.427`, Framework`4.8.10008`/Release`534296`, IIS Express`10.0.26013.1000`, VSTest`17.11.1` x64. The designated SQL environment's last captured version is SQL Server`14.0.2140.2` RTM Developer Edition64 in [WEB3 evidence](../web3/20261101-b04-b05-integrated-pilot.md#3-prepared-production-artifact-and-environment); no new server-version query was performed or inferred from this run.

The coordinator's independent source review reported no remaining P1/P2 after corrections to browser coverage and sanitized failure diagnostics. This is source review, not final acceptance. B05 remains IN PROGRESS; paired final Chromium/Edge and focused Inventory/migration confirmations remain pending.

## Pending gates and limits

Required fresh independent Chromium/Edge IIS Express HTTPS/SQL runs, source/artifact hashes, independent oracle/cleanup, focused SQL owners, hosted CI and final review are PENDING. No B05/F04 acceptance or merge is claimed. Same-key resend stays disabled; absent status is not rollback proof. Process-crash/Commit, attended Telex/VNI/Narrator, full IIS and production remain NOT RUN/not approved. Historical evidence is unchanged.
