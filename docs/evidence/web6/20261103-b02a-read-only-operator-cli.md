# WEB6 B02a evidence — isolated read-only operator CLI

**Date:** 2026-11-03 (Asia/Bangkok). **State:** IMPLEMENTED / VERIFIED NONPRODUCTION for B02a only. B02b destructive terminal commands and races remain pending; manual resend remains OFF.

## Candidate and design

- Base: backend `fe790b14f0e1c2247fbe3f5a1fc519f293d63d5b` (merged B01 PR #99); paired frontend reference `92afef7c9090605a172ffc351d1e641d9df12100`.
- Review-fix implementation commit: `62f1b390956db72330156edcbdbca097c3b304d0` on `codex/web6-b02-operator-inspect` (documentation follows in a separate commit).
- Native Windows/net48, .NET SDK `8.0.427`, VSTest `17.11.1`, Microsoft.Data.SqlClient `7.0.2`; SQL Server `14.0.2140.2` Developer Edition (64-bit), engine edition 3.
- Fresh owned tests used exact schema13 and fingerprint `D0A86EA0CACAB3D68F1188A1166227DFA5DE452572D1719CD55A3FA3C16F02C9`.
- New console owner: `tools/MyDmsVn.WebOperation.Operator`. It references Application and Infrastructure only, not HttpHost, controllers or runtime DI. It adds no migration, grant or HTTP endpoint.

The executable accepts only `inspect --environment --database --epoch --actor --operation --fingerprint [--timeout-seconds]`. It validates an exact GUIDv4 operation, non-empty epoch, canonical positive actor and SHA-256; rejects system databases, attach/user-instance connections, target mismatch and ambiguous/duplicate options; disables pooling; checks both configured `Initial Catalog` and `DB_NAME()`; and has bounded open/command cancellation. The public timeout is now 1–25 seconds, so the invocation deadline precedes the shared Infrastructure read command's 30-second timeout; raw SQL timeout `-2` is also mapped to exit 6. Caller cancellation remains exit 130, while environment or rollback/disposal failure remains exit 5.

Inspection reuses `SqlWebOperationResolutionMaintenance.InspectAsync` inside an explicit read-committed transaction and rolls back on success/error; cancellation closes the non-pooled connection. Before inspection the CLI checks the exact configured effective database user and original login, operator-role membership, absence of extra database roles/direct grants (apart from normal `CONNECT`), and absence of `db_owner`, database/server `CONTROL`, sysadmin or any other fixed server role. Unknown/ambiguous membership fails closed. The tool cannot call `RecordNotExecutedAsync`, cannot create a claim/case, and never prints the connection string, credential, token, cookie, fingerprint or result/business bytes. `environmentLabel` is accompanied by `environmentLabelSource=operator-supplied` and is not claimed as authoritative identity.

## Verification

Solution-level locked restore was attempted and stopped because three WinFormsKit vendor project files are not materialized in this worktree. Locked restore passed for both the CLI and affected Security Tests project graphs; no vendor state was changed.

| Gate/artifact | Executed | Pass | Fail | Skip | Result |
|---|---:|---:|---:|---:|---|
| Review-fixed CLI parser/output/exit/SQL integration and executable smoke — `TestResults/web6-b02a-review-final/b02a-operator-inspect-review.trx` | 39 | 39 | 0 | 0 | PASS |
| Existing resolution regression — `TestResults/web6-b02a-resolution-review/b02a-resolution-regression-review.trx` | 24 | 24 | 0 | 0 | PASS |
| **Independent final test total** | **63** | **63** | **0** | **0** | **PASS** |

Release/net48 build of the CLI at the review-fix implementation commit passed with 0 warnings/0 errors. The 39-case run includes an out-of-process Release executable using a generated, fresh SQL login on a fresh owned schema13 database: restricted COMMITTED inspection exits 0; missing operator role, an extra `db_datareader` role and `db_owner` exit 3; wrong target exits 5; stdout/stderr are bounded and secret/payload-free. The exact owned login SID/server identity and zero active sessions were checked before `DROP LOGIN`. A first RED smoke exposed an invalid assumption that every minimal fixture contains the runtime role; the final test instead proves generic extra-role rejection and its owned allocation/login cleaned normally.

The SQL cases use fresh owned fixtures and prove restricted-operator COMMITTED, EXPIRED, CANCELLED and missing-row INDETERMINATE; missing/elevated principal denial; exact database/epoch/actor/fingerprint handling; a real application-lock timeout; deterministic real SQL command timeout `-2`; cancellation before and during inspection; unavailable SQL; cleanup-failure mapping; and bounded secret-free output. The public value 26 is rejected, covering the deliberate under-30 upper bound without a 60-second test. Before/after snapshots cover journal, terminal audit, Sales/Goods Receipt sources, balances, ledger and valuation and are byte-equal after inspection/error/timeout. All final fixtures completed their normal owned cleanup; no B02a quarantine was created. Full solution build, IIS, browser and protected hosted CI were NOT RUN.

## Acceptance and review

| Gate | State | Evidence |
|---|---|---|
| B6-OPS-01 | VERIFIED NONPRODUCTION | Separate net48 CLI; help/exit/build; explicit exact DB; restricted operator path; no HttpHost/DI/admin controller |
| B6-OPS-02 | VERIFIED NONPRODUCTION | Read-only COMMITTED/EXPIRED/CANCELLED/INDETERMINATE, no result bytes, wrong identity/authority fail closed, missing row never becomes NOT_EXECUTED |
| B6-OPS-03 | PENDING B02b / NOT IMPLEMENTED | No `record-not-executed` command, dry-run or execute path exists in this PR |
| B6-OPS-04 | PENDING B02b / NOT IMPLEMENTED | B02a covers read lock timeout/authority/read-only oracle only; destructive decision races and failure audit remain future scope |

Self-review findings: P0 0, P1 0, P2 0 in B02a scope. CI and Owner acceptance are pending. This PR does not complete all of B02, elevate runtime SQL, enable resend, or claim production readiness.

## Usage and F02 relevance

Usage is documented in `tools/MyDmsVn.WebOperation.Operator/README.md`. The CLI is an operator read aid only: INDETERMINATE remains unknown and provides no resend admission. It adds no product API. Frontend F02 must continue to use the existing protected status/resolution contracts and keep **Gửi lại đúng thao tác** disabled until B04 plus the unapproved eligibility/age/claim/consent decisions and F02/F03 gates are complete.
