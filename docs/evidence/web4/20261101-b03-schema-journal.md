# WEB4-B03 — schema12 and transaction-owned journal

**Selected:** 2026-11-01. **Verification:** 2026-11-01–02, Asia/Bangkok (+07:00). **State:** VERIFIED CANDIDATE; Owner acceptance/merge pending. [Owning B03 plan](../../plans/20261101-003-web4-reliability-server.md), [accepted source/design checkpoint](../../design/WEB4_B01_SOURCE_CHECKPOINT.md), [acceptance matrix](../../development/WEB4_BACKEND_ACCEPTANCE.md), [test owners](../../../tests/README.md#web4-b03-schema-and-transaction-journal-checks). This record adds no implicit PASS to B04/B05/B06, frontend recovery or production.

## Candidate and implemented boundary

[Draft PR #82](https://github.com/chung6a8m/MyDmsVn/pull/82). Source/test/deployment commit **`e2976715bb106cc22c702e60ca2dd4536d6894ae`**, base `a9fef520236340f024b192da75d4b6a3fbd77503`, branch `codex/web4-b03-journal`; B01/B02 accepted/merged. Later documentation-only commits do not alter the executable candidate. Frontend accepted source remains `68398accdd03a1ed78a2e3bec7c5cb2499457b6d`, unchanged. Canonical B01 corpus stays pinned at `1c603c6ac57e59a3f7b4bc6ca976b2d531c079aa`, manifest `0acab190e7fe126d53c13c0510a1958365e5a6e1fda78a4321da78297e0b3f1a`.

- Opt-in `012_WebOperationJournal.sql`: persistent singleton epoch, actor/operation key, immutable request/result identities, bounded result bytes, seven-day expiry and permanent terminal tombstones. The schema12 definition fingerprint was measured from actual implemented DDL on an owned SQL fixture: **`50552E3ADC1AF4C68591D9C582B5340ED4744BB6883F085E8A7EC7053308BC87`**. Frozen exact11 admission supports11→12; exact10→011→012 remains maintenance-only. Scripts001–011, Desktop manifest/exact10 and dependency pins are unchanged.
- All six Application commands accept optional server-only operation context. The same existing UoW connection/transaction owns epoch HOLDLOCK → actor/key transaction applock → current session/account/permission reads held through commit → original result insert → single business Commit. Matching replay bypasses dynamic business work and Post audit. Local null context never resolves the Web journal. No independent auth connection or automatic mutation retry is added.
- Original Create/Update DTOs are captured before commit, including original versions/line identities/labels; Sales Post keeps version and Receipt Post keeps its unversioned acknowledgement. Results require valid UTF8 and at most1,048,576 bytes. Runtime expiry works before physical cleanup; compaction preserves old-epoch identity and key tombstone under the current database epoch/key fences. Operator maintenance requires its own transaction, CONTROL and audit-attempt callback; no negative operator unlock is implemented.
- Provisioning and runtime admission check effective minimum and maximum permissions, including inherited/column DENY/overgrant and grant-option delegation. Runtime receives only SELECT epoch and SELECT+INSERT journal, with no epoch mutation/journal update/delete authority.
- A reproduced migration session-lock release race is corrected with a narrow pinned-DbUp connection-manager decorator: release the lock on its still-open connection before DbUp disposes it, with fallback cleanup if initialization fails after ownership transfer. Concurrent acquire stays timeout0/fail-fast; no retries.

**Rollout boundary:** current unkeyed HttpHost retains frozen exact11 schema and principal admission. It cannot start on12. The new schema12 checker/journal are B04 prerequisites; B04 must implement mandatory context/header admission, production allowlisted wire codec, protected context/status routes and coordinated host switch. The B03 test codec is not that HTTP implementation.

## Final local verification

Windows, SDK8.0.427, Release/net48; locked restores passed. SQL used the configured designated test environment only through existing disposable ownership-checked fixtures. SQL executions were sequential; one earlier Security compilation overlapped a Migration process tail but failed before any Security SQL body started. Raw TRX/logs remain ignored local diagnostics, never committed or uploaded.

| Existing owner | Passing selected rows | Evidence and scope |
|---|---:|---|
| Infrastructure.Migrations.Tests | 24 | `web4-b03-migrations-final/migrations-final.trx`:17 Web schema cases,2 manager lifecycle cases,4 retained lock/no-op cases,1 historical prefix compatibility case. Refined transfer-observation assertion then reran the same2 lifecycle cases:2/2 PASS. |
| Infrastructure.Security.Tests | 57 distinct |38 journal/principal rows +19 frozen11 HTTP+realSQL smoke rows. Expanded invocation had37 PASS/20 FAIL; the corrected21-row invocation passed21/21 and covers all20 failed rows plus one repeated timeout row. Root reconciled TRX test IDs: all57 have passing evidence, no additional cases invented by summing reruns. |
| Infrastructure.Inventory.Tests | 25 | `web4-b03-inventory/inventory.trx`:7 new command/fault/size rows plus18 targeted existing Local rollback/concurrency rows. |
| Infrastructure.Foundation.Tests | 4 | `web4-b03-foundation/foundation.trx`: frozen11 reset1 and exceptional UoW cleanup3. |
| Server.Application.Tests | 143 | `web4-b03-application/application.trx`: selected pipeline, context, Sales/Receipt validation, permission and superuser owners. |

These are **253 distinct selected test rows**, including SQL and SQL-free checks, with0 remaining failed/required skipped rows. This is not an entire-solution run. Root independently parsed actual TRX counters/results and matched reruns by test ID (display names can collide for truncated theory values). Hosted HttpHost counts below are separate.

### Actual SQL assertions

The full six-command Web scenario creates/updates/posts Receipt then Sales, retains original Receipt replacement line IDs/null labels/true flags and Sales labels/versions, then deactivates/renames Product and replays all six original intents with identical test-codec outcomes. No Read grants are present. The independent SQL oracle finds one posted receipt and one posted sale,4 ledger and4 valuation rows, quantity24/value72, Sales frozen cost18 and revenue75. The Local exact10 scenario runs the same six commands through a UoW guard that rejects any journal resolution; Web tables are absent and history remains10. These are two test rows exercising twelve command paths, not twelve separately discovered cases.

Receipt precommit codec/oversize/journal failures and Sales journal-insert failure roll back status/version/lines or ledger/value/COGS with the journal. The maximum200-line shape and exactly1MiB result are accepted; oversized results fail before commit. Original Local rollback/duplicate/gift/concurrency assertions remain selected.

Independent SQL connections positively observe DMV blocking before release: same-key replay, changed fingerprint/kind, rollback, epoch rotation and stale epoch rejection, post-wait session/account/direct-Deny/membership/role/grant/superuser-binding revocation, held-authority protection (including absent direct-Deny/settings ranges), cross-actor/session isolation, finite timeout/cancellation and compaction after epoch rotation. Cancellation tests await the actual worker and prove zero effects after explicit transaction rollback; client cancellation alone is never rollback evidence.

### Commands and selectors

For each named owner under `tests/<owner>/<owner>.csproj`, run `dotnet restore <project> --locked-mode`, then:

```powershell
dotnet test <project> -c Release -f net48 --no-restore --filter '<selector>' --logger 'trx;LogFileName=<name>.trx' --results-directory TestResults/<directory>
```

Exact final selectors:

- Migrations: `FullyQualifiedName~WebAuthMigrationTests|FullyQualifiedName~MigrationConnectionManagerTests|FullyQualifiedName~Competing_migrator_is_rejected|FullyQualifiedName~Successful_migration_releases_session_lock|FullyQualifiedName~Losing_the_migration_lock_session|FullyQualifiedName~Clean_migration_applies_once|FullyQualifiedName~Interrupted_old_security_baseline`. Lifecycle refinement: `FullyQualifiedName~MigrationConnectionManagerTests`.
- Security expanded: `FullyQualifiedName~WebOperationJournalTests|FullyQualifiedName~WebPrincipalProvisioningTests|FullyQualifiedName~WebAuthHttpSqlTests|FullyQualifiedName~WebSalesHttpSqlTests|FullyQualifiedName~WebGoodsReceiptHttpSqlTests`. Corrections: `FullyQualifiedName~Cancelled_or_timed_out_key_wait|FullyQualifiedName~WebAuthHttpSqlTests|FullyQualifiedName~WebSalesHttpSqlTests|FullyQualifiedName~WebGoodsReceiptHttpSqlTests`.
- Inventory: `FullyQualifiedName~WebOperationCommandTests|FullyQualifiedName~Failure_at_draft_write_boundaries|FullyQualifiedName~Competing_saves_wait_on_locked_header|FullyQualifiedName~Failure_after_each_write_rolls_back|FullyQualifiedName~Before_commit_disposal_rolls_back|FullyQualifiedName~Paid_and_gift_duplicates_freeze|FullyQualifiedName~Failed_update_rolls_back_header_version|FullyQualifiedName~Ledger_failure_rolls_back_posted|FullyQualifiedName~Stale_or_denied_post_leaves|FullyQualifiedName~Concurrent_posts_for_same_receipt`.
- Foundation: `FullyQualifiedName~WebAuthResetTests|FullyQualifiedName~UnitOfWorkExceptionalCleanupTests`.
- Application: `FullyQualifiedName~ApplicationPipelineTests|FullyQualifiedName~WebOperationContextTests|FullyQualifiedName~SalesReceiptApplicationTests|FullyQualifiedName~GoodsReceiptApplicationTests|FullyQualifiedName~PermissionAuthorizationTests|FullyQualifiedName~SuperuserAuthorizationTests`.

## RED, corrections and independent review

The initial schema12 test executed1 SQL row against baseline and failed expected12/actual11,0 skips. Fingerprint discovery had one refused raw001–012 bootstrap (existing maintenance admission), then three intentional mismatches against the pending constant while measuring DDL; the final freeze test passes. Early review fixed nullable expiry CHECK comparisons that SQL could accept as UNKNOWN, grant-option delegation, old-epoch compaction and partial fixture-construction cleanup; final SQL negatives cover these boundaries.

Initial migration execution14/15 PASS exposed an immediate10→Web lock failure. The isolated case failed again before correction. Inspection of pinned dbup-core6.1.1 (`41228ce747979fb52e1a59e3eec5823e6de869c8`) confirmed disposal ordering; explicit release fixed the race without retries. Final lifecycle tests directly prove the transfer callback ran before initialization failed, connection/key cleanup, release-before-dispose, combined failure aggregation and idempotent disposal.

Initial six-command execution2/6 PASS had4 test-assertion failures: the existing Application pipeline returns ErrorOr instead of propagating codec/SQL exceptions. Assertions and the closed frozen-code mapping were corrected; final Inventory25/25 passes. Expanded Security37/57 PASS had19 frozen11 fixture composition failures (default minimum12 validator) and1 cancellation exception-shape mismatch. The fixture now explicitly selects schema11 mode, matching Startup; the cancellation test accepts observed SqlClient cancellation Number0 only with a requested token (or OCE), while retaining actual worker drain/zero-effects assertions. Corrected21/21 PASS resolves every failed row.

Compiler-only fixes included the existing GoodsReceiptDto `Id` identity versus Post `ReceiptId`, the owner-local SQL theory attribute and synchronous UoW test-guard method. UTF8 handling/formatting was corrected before source freeze; these are not SQL passes.

Independent spec/quality review found one P2 affecting two historical fixture predicates: negative script exclusions admitted new012 into a six-script fixture. Both now positively select frozen001–006; affected gates pass. Follow-up review accepted those fixes and the direct lifecycle tests; **no findings remain open**. Reviewer performed read-only source review, not SQL execution. Historic evidence, source scripts and dependency pins remain unchanged.

## Hosted CI

Source push [run41331146730](https://github.com/chung6a8m/MyDmsVn/actions/runs/41331146730), job124921327168, at exact `e2976715bb106cc22c702e60ca2dd4536d6894ae`: **PASS415/415 HttpHost tests,0 failed/skipped**, with TRX total=executed=passed=415. Locked restore, Release/net48 build, complete-test guard and sanitized count-artifact upload all passed on attempt1. Windows2022, SDK8.0.427, Framework release533509, runner image20261026.347.1. No workflow, dependency pins or hosted SQL configuration changed.

The documentation follow-up retains this exact executable tree. Final documentation-HEAD push/PR checks are tracked by [PR82 checks](https://github.com/chung6a8m/MyDmsVn/pull/82/checks) and must be current when Owner authorizes merge; source push evidence is not represented as a different HEAD. Hosted415 is independent of the local253 and proves no hosted SQL/browser acceptance.

## Cleanup, disposition and limits

Owned fixtures enforce exact ownership at teardown; workers are tracked and drained before connection/database disposal, with body/cleanup failures aggregated. All completed final scopes have successful teardown; no cleanup failure, quarantine or retained allocation is reported. Existing frozen11 HTTP tests use in-memory OWIN TestServer with realSQL, not IIS/browser processes. No raw connections, database identifiers, credentials, tokens or proprietary TRX are committed. Temporary diagnostics remain ignored locally. Root documentation diff, local links and UTF8/no-BOM/CRLF checks pass at publication; source preservation/encoding checks also pass.

B03 is a verified implementation candidate awaiting Owner acceptance/merge. The WEB4-03 roadmap also depends on B04 APIs; this B03 result does not close B04. B04–B06 and frontend F02–F05 remain PLANNED. No production HTTP codec/status/readback, browser resend/recovery, real process crash/backup-restore rehearsal, IIS Express/browser pilot, full IIS/production, encryption/backup-retention deployment or negative operator unlock is accepted. B04 must measure its actual allowlisted codec; B05/B06 own integrated failure/operational rehearsals. Current HttpHost11 stays paired with its existing unkeyed client. No merge or production deployment is performed by this task.
