# WEB6 B02a evidence — isolated read-only operator CLI

**Date:** 2026-11-03 (Asia/Bangkok). **State:** IMPLEMENTED / LOCAL VERIFIED NONPRODUCTION for B02a only; PROTECTED SQL CI PENDING / NOT RUN. B02b destructive terminal commands and races remain pending; manual resend remains OFF.

## Candidate and design

- Base: backend `fe790b14f0e1c2247fbe3f5a1fc519f293d63d5b` (merged B01 PR #99); paired frontend reference `92afef7c9090605a172ffc351d1e641d9df12100`.
- Final review-fix implementation commits: `13a108a0e5ae51284c33ea0e73eeacffb223b05b` plus exact effective-permission follow-up `3bdcde8eb7bbc1ad7c072b2e43e7a8bfa5b01432` on `codex/web6-b02-operator-inspect`.
- Round-2 protected SQL workflow commit: `b014da6473becae0df837373edb9cb5c7b0d84c7`; no CLI/runtime source changed.
- Native Windows/net48, .NET SDK `8.0.427`, VSTest `17.11.1`, Microsoft.Data.SqlClient `7.0.2`; SQL Server `14.0.2140.2` Developer Edition (64-bit), engine edition 3.
- Fresh owned tests used exact schema13 and fingerprint `D0A86EA0CACAB3D68F1188A1166227DFA5DE452572D1719CD55A3FA3C16F02C9`.
- New console owner: `tools/MyDmsVn.WebOperation.Operator`. It references Application and Infrastructure only, not HttpHost, controllers or runtime DI. It adds no migration, grant or HTTP endpoint.

The executable accepts only `inspect --environment --database --epoch --actor --operation --fingerprint [--timeout-seconds]`. It validates an exact GUIDv4 operation, non-empty epoch, canonical positive actor and SHA-256; rejects system databases, attach/user-instance connections, target mismatch and ambiguous/duplicate options; disables pooling; checks both configured `Initial Catalog` and `DB_NAME()`; and has bounded open/command cancellation. The public timeout is now 1–25 seconds, so the invocation deadline precedes the shared Infrastructure read command's 30-second timeout; raw SQL timeout `-2` is also mapped to exit 6. The console process arms an independent watchdog at that same deadline, so synchronous provider rollback/close/disposal cannot keep the dedicated CLI process alive beyond the contract; forced deadline termination remains exit 6 and explicitly says SQL outcome unknown. Caller cancellation that completes first remains exit 130; an ordinary provider/environment failure before the deadline remains exit 5.

Inspection reuses `SqlWebOperationResolutionMaintenance.InspectAsync` inside an explicit read-committed transaction and rolls back on success/error; cancellation closes the non-pooled connection, with the process watchdog as the hard boundary. Before inspection the CLI checks the exact configured effective database user and original login, direct role/grant shape and every explicit database/server permission row contributed by `sys.user_token`/`sys.login_token` against an exact class/object/state allowlist. Runtime/migration/fixed roles, nested roles, `PUBLIC`/operator over-grants, DML or broad reads, schema/database/server/custom-securable authority, grant option and unexpected execute/reference authority fail closed; normal system-catalog reads, system endpoint connects and the two existing schema13 operator procedure executes are the only catalogued exceptions. The tool cannot call `RecordNotExecutedAsync`, cannot create a claim/case, and never prints the connection string, credential, token, cookie, fingerprint or result/business bytes. `environmentLabel` is accompanied by `environmentLabelSource=operator-supplied` and is not claimed as authoritative identity.

## Verification

Solution-level locked restore was attempted and stopped because three WinFormsKit vendor project files are not materialized in this worktree. Locked restore passed for both the CLI and affected Security Tests project graphs; no vendor state was changed.

| Gate/artifact | Executed | Pass | Fail | Skip | Result |
|---|---:|---:|---:|---:|---|
| Round-2 exact CLI parser/output/exit/SQL integration and executable smoke — `TestResults/web6-b02a-round2-final/b02a-round2-final.trx` | 40 | 40 | 0 | 0 | PASS in 34s; exact class independently discovered 40 |
| Round-2 existing resolution regression — `TestResults/web6-b02a-round2-resolution/b02a-round2-resolution.trx` | 24 | 24 | 0 | 0 | PASS in 1m35s; independently discovered 24 |
| **Independent final test total** | **64** | **64** | **0** | **0** | **PASS** |

Release/net48 build of the CLI at the final review-fix implementation commit passed with 0 warnings/0 errors. The 40-case run includes an out-of-process Release executable using a generated, fresh SQL login on a fresh owned schema13 database: restricted COMMITTED inspection exits 0; missing/direct/nested role, `db_owner`, operator-role over-grant, `PUBLIC` DML over-grant and a custom user-defined-type `CONTROL` grant exit 3; wrong target exits 5; a real application-lock block exits 6 and the process ends in under four seconds; stdout/stderr are bounded and secret/payload-free. The exact owned login SID/server identity and zero active sessions were checked before `DROP LOGIN`. A deterministic timer test proves the hard process-deadline callback fires once. All temporary grants/role nesting/custom type are reverted inside the owned database before its normal guarded cleanup.

The SQL cases use fresh owned fixtures and prove restricted-operator COMMITTED, EXPIRED, CANCELLED and missing-row INDETERMINATE; missing/elevated/effectively over-granted principal denial; exact database/epoch/actor/fingerprint handling; a real application-lock timeout; deterministic real SQL command timeout `-2`; cancellation before and during inspection; unavailable SQL; ordinary cleanup-failure sanitization; and bounded secret-free output. The hard process watchdog surrounds all of `Main`; its one-shot timer and an actual blocked executable exit are verified, while a provider cleanup call that never returns was not separately fault-injected. The public value 26 is rejected, covering the deliberate under-30 upper bound without a 60-second test. Before/after snapshots cover journal, terminal audit, Sales/Goods Receipt sources, balances, ledger and valuation and are byte-equal after inspection/error/timeout. All final fixtures completed their normal owned cleanup; no B02a quarantine was created. Full solution build, IIS and browser were NOT RUN.

## Protected exact-candidate SQL CI handoff

`.github/workflows/backend-web6-b02a-sql-candidate.yml` is an Owner-tag-triggered Windows/net48 workflow using the protected `web5-sql-candidate` environment and the existing private SID-owned LocalDB lifecycle. It pins both checkouts to the tag SHA, performs locked restore and Release/net48 build, requires nonempty discovery of exactly 40 cases from `WebOperationOperatorInspectTests`, and requires actual TRX execution 40/40/0/0 with no retry. The actual TRX must contain only that class and exactly one `Release_executable_uses_a_real_restricted_login_and_never_changes_SQL` smoke. That smoke creates an owned SQL login, runs the actual Release EXE through the restricted principal, exercises denial and timeout exit codes, proves byte-equal read-only SQL snapshots, drains login/session/process owners, and drops only the exact owned login. Sanitized artifacts contain only counts, capability booleans and lifecycle metadata.

The current [GitHub `windows-2022` image inventory](https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md) lists the Visual Studio LocalDB runtime. [Microsoft's LocalDB documentation](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb?view=sql-server-ver16) describes it as a local Windows SQL Server Express engine and describes SQL-authenticated logins for shared instances. An isolated local SQL Server 2019 LocalDB probe additionally observed `IsLocalDB=1`, `IsIntegratedSecurityOnly=0`, successfully created and connected with a SQL login, then stopped and deleted only its random owned instance. This local probe establishes feasibility, not hosted-run acceptance. The workflow therefore independently fails closed unless the allocated hosted instance reports LocalDB plus SQL authentication enabled, and the real executable smoke must still pass.

**PROTECTED SQL CI PENDING / NOT RUN.** No tag or protected-environment approval was created. Only Owner `chung6a8m` may push `web6-b02a-sql-candidate-*` at the final reviewed PR HEAD and approve the environment. The run URL, exact SHA, runner image/SQL version, sanitized artifact digest/counts and lifecycle result are required before the Roadmap SQL-CI portion of B6-OPS-01/02 can close. The two ordinary SQL-free checks do not substitute for this gate.

## Acceptance and review

| Gate | State | Evidence |
|---|---|---|
| B6-OPS-01 | LOCAL VERIFIED NONPRODUCTION / PROTECTED SQL CI PENDING | Separate net48 CLI; help/exit/build; explicit exact DB; restricted operator path; no HttpHost/DI/admin controller; exact-candidate protected workflow prepared but NOT RUN |
| B6-OPS-02 | LOCAL VERIFIED NONPRODUCTION / PROTECTED SQL CI PENDING | Read-only COMMITTED/EXPIRED/CANCELLED/INDETERMINATE, no result bytes, wrong identity/authority fail closed, missing row never becomes NOT_EXECUTED; protected actual-EXE SQL-login proof NOT RUN |
| B6-OPS-03 | PENDING B02b / NOT IMPLEMENTED | No `record-not-executed` command, dry-run or execute path exists in this PR |
| B6-OPS-04 | PENDING B02b / NOT IMPLEMENTED | B02a covers read lock timeout/authority/read-only oracle only; destructive decision races and failure audit remain future scope |

Round-2 self-review findings: P0 0, P1 0, P2 0 in B02a source scope. Protected SQL CI and Owner acceptance are pending. This PR does not complete all of B02, elevate runtime SQL, enable resend, or claim production readiness.

## Usage and F02 relevance

Usage is documented in `tools/MyDmsVn.WebOperation.Operator/README.md`. The CLI is an operator read aid only: INDETERMINATE remains unknown and provides no resend admission. It adds no product API. Frontend F02 must continue to use the existing protected status/resolution contracts and keep **Gửi lại đúng thao tác** disabled until B04 plus the unapproved eligibility/age/claim/consent decisions and F02/F03 gates are complete.
