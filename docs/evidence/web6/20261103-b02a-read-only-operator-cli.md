# WEB6 B02a evidence — isolated read-only operator CLI

**Date:** 2026-11-03 (Asia/Bangkok). **State:** IMPLEMENTED / VERIFIED NONPRODUCTION for B02a only. B02b destructive terminal commands and races remain pending; manual resend remains OFF.

## Candidate and design

- Base: backend `fe790b14f0e1c2247fbe3f5a1fc519f293d63d5b` (merged B01 PR #99); paired frontend reference `92afef7c9090605a172ffc351d1e641d9df12100`.
- Tested implementation commit: `3f8d4ef054ea43eaf281d80b06cf33c730677d57` on `codex/web6-b02-operator-inspect`.
- Native Windows/net48, .NET SDK `8.0.427`, VSTest `17.11.1`, Microsoft.Data.SqlClient `7.0.2`; SQL Server `14.0.2140.2` Developer Edition (64-bit), engine edition 3.
- Fresh owned tests used exact schema13 and fingerprint `D0A86EA0CACAB3D68F1188A1166227DFA5DE452572D1719CD55A3FA3C16F02C9`.
- New console owner: `tools/MyDmsVn.WebOperation.Operator`. It references Application and Infrastructure only, not HttpHost, controllers or runtime DI. It adds no migration, grant or HTTP endpoint.

The executable accepts only `inspect --environment --database --epoch --actor --operation --fingerprint [--timeout-seconds]`. It validates an exact GUIDv4 operation, non-empty epoch, canonical positive actor and SHA-256; rejects system databases, attach/user-instance connections, target mismatch and ambiguous/duplicate options; disables pooling; checks both configured `Initial Catalog` and `DB_NAME()`; and has bounded open/command cancellation.

Inspection reuses `SqlWebOperationResolutionMaintenance.InspectAsync` inside an explicit read-committed transaction and always rolls back. The dedicated connection must already use the restricted schema13 operator principal. The tool cannot call `RecordNotExecutedAsync`, cannot create a claim/case, and never prints the connection string, credential, token, cookie, fingerprint or result/business bytes.

## Verification

Solution-level locked restore was attempted and stopped because three WinFormsKit vendor project files are not materialized in this worktree. Locked restore passed for both the CLI and affected Security Tests project graphs; no vendor state was changed.

| Gate/artifact | Executed | Pass | Fail | Skip | Result |
|---|---:|---:|---:|---:|---|
| CLI parser/output/exit/SQL integration — `TestResults/web6-b02a-final/b02a-operator-inspect-final.trx` | 34 | 34 | 0 | 0 | PASS |
| Existing resolution regression — `TestResults/web6-b02a-resolution-regression-final/b02a-resolution-regression-final.trx` | 24 | 24 | 0 | 0 | PASS |
| **Test total** | **58** | **58** | **0** | **0** | **PASS** |

Release build of the CLI passed with 0 warnings/0 errors. Direct executable probes passed: `--help` exit 0 and incomplete `inspect` exit 2. Project-reference inspection showed only Application and Infrastructure.

The SQL cases use fresh owned fixtures and prove restricted-operator COMMITTED, EXPIRED, CANCELLED and missing-row INDETERMINATE; runtime-principal denial; exact database/epoch/actor/fingerprint handling; application-lock timeout; unavailable SQL; cancellation; and bounded secret-free output. Before/after snapshots cover journal, terminal audit, Sales/Goods Receipt sources, balances, ledger and valuation and are byte-equal after inspection. All successful fixtures completed their normal owned cleanup; no B02a quarantine was created. Full solution build, IIS, browser and protected hosted CI were NOT RUN.

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
