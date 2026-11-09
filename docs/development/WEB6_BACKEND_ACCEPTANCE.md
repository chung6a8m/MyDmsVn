# WEB6 backend acceptance — SQL correctness, operator recovery and paired candidate

**Selected 2026-11-03; gate states below own current acceptance. B03 checkpoint 2026-11-09: bounded R2e/R2f remediation accepted; full candidate and protected CI remain BLOCKED.** This is a mandatory acceptance contract, not a report. [Owning plan](../plans/20261103-001-web6-recovery-quality-backend.md) · [safety design](../design/WEB6_RECOVERY_OPERATION_PROTOCOL.md) · [candidate runbook](WEB6_CANDIDATE_RUNBOOK.md). Old WEB5 test evidence remains at its original source pins and is not a WEB6 PASS.

## 1. Interpretation and evidence requirements

Use `PASS` only after **actual selected test execution** and independent oracle; `FAIL` for observed failure; `BLOCKED` for prerequisite/policy barrier; `NOT RUN` for unexecuted; `N/A` only for a justified non-applicable design branch. A gate can be `DONE (bounded)` with explicitly accepted exceptions but cannot become a full production release gate through exception relabeling.

Every test must record repository SHA, inspected source tree, built artifact hashes, CLI/browser/SQL toolchain, SQL schema/epoch/principal, auth mode and site origin, command selection, TRX/reporter hash, discovered/executed/pass/fail/skip/retry, physical oracle, teardown state and known quarantines. Distinguish unit/SQL/Linux browser/Windows native/attended/full IIS. A skipped category is never a PASS. Full IIS and unperformed Narrator are Owner-accepted NOT RUN only in bounded IIS Express scope.

## 2. Gates

| ID | Owner | Mandatory evidence / stop rule | Current |
|---|---|---|---|
| B6-SRC-01 | B01 | Exact prior backend/frontend main SHA, frozen WEB4 48 and WEB5 40 corpus hashes, schema13 fingerprint, source+dependency inventory, candidate publish provenance | NOT RUN |
| B6-SRC-02 | B01 | Original intent/state/authorization/response and expiry policy + threat matrix, separate manual-resend vs new intent vs GET; documented Owner approval before capability enable | NOT RUN |
| B6-OPS-01 | B02 | Operator CLI distinct from runtime, compile/help/exit codes, dedicated SQL operator principal and explicit current target; no web DI admin/controller | NOT RUN |
| B6-OPS-02 | B02 | Read-only exact inspect returns COMMITTED/EXPIRED/CANCELLED/INDETERMINATE hints, no business bytes; wrong fingerprint/actor/epoch denied; no row never reported NOT_EXECUTED | NOT RUN |
| B6-OPS-03 | B02 | `record-not-executed --dry-run` no changes; `--execute` only with exact typed confirmation, matched immutable audit + CANCELLED one SQL Commit, re-inspection, no false success after uncertain Commit | **PASS / OWNER ACCEPTED BOUNDED NONPRODUCTION 2026-11-09** — exact `2d47687` protected run `42543717784` passed 59+40+26+2, restricted SQL-login, unchanged source and owned cleanup; provider-level fault injection retained as `OWNER_ACCEPTED_BOUNDED_GAP` |
| B6-OPS-04 | B02 | Worker/operator-first transaction race, duplicate decision, lock timeout, failed transaction, wrong principal/role/session, audit sink fail; SQL journal/stock/source oracle | **PASS / OWNER ACCEPTED BOUNDED NONPRODUCTION 2026-11-09** — exact protected restricted-EXE/six-kind/race/oracle counts passed with zero fail/skip/retry; process/session/database drain and owned removal verified, quarantine false |
| B6-OPS-05 | B03 | COMMITTED-result-unavailable independently verified source/ledger/COGS, sanitized support case, no fabricated version/SETTLED/unlock; write-without-Read scenario | **BLOCKED** — [blocked local candidate](../evidence/web6/20261109-b03-maintenance-recovery.md); protected exact-HEAD CI NOT RUN and Owner acceptance pending |
| B6-MNT-01 | B03 | Exact COMMITTED expired compaction preview+execute, audit and rollback, permanent tombstone, no DELETE/no secret logs, untouched unrelated rows/ledger; `CompactAsync` database CONTROL requirement addressed by separate approved maintenance identity or reviewed narrowly scoped SQL permission model (never web/operator role escalation) | **BLOCKED** — [blocked local candidate](../evidence/web6/20261109-b03-maintenance-recovery.md); protected exact-HEAD CI NOT RUN and Owner acceptance pending |
| B6-MNT-02 | B03 | Owned schema13 full backup/restore to another owned DB, epoch rotation before writer admission for clone/rewind only, old tokens and epoch denied, current admitted, fail-closed rollback, full cleanup | **BLOCKED** — [blocked local candidate](../evidence/web6/20261109-b03-maintenance-recovery.md); protected exact-HEAD CI NOT RUN and Owner acceptance pending |
| B6-MNT-03 | B03 | Durable operational audit destination, ACL/retention/redaction, evidence of pre/post-Commit audit and delivered/unavailable sinks with published failure policy | **BLOCKED** — [blocked local candidate](../evidence/web6/20261109-b03-maintenance-recovery.md); protected exact-HEAD CI NOT RUN and Owner acceptance pending |
| B6-RPL-01 | B04 | Six Sales/Receipt Create/Update/Post same-key replay after Commit+lost response, original bytes/version/line IDs, one journal and one business effect | NOT RUN |
| B6-RPL-02 | B04 | Six original/resend-first contention plus OS death before/near/after Commit, independent SQL fence/quiescence, no double source/ledger/stock/valuation/COGS effect | NOT RUN |
| B6-RPL-03 | B04 | Six CANCELLED operator-first and original-first races, exact actor/epoch/key/fingerprint under lock, no unauthorized re-execution | NOT RUN |
| B6-RPL-04 | B04 | Expired/unavailable result, wrong actor/grant/session/epoch/target/fingerprint, old host/mode and disabled capability fail closed; 404 alone never authorizes | NOT RUN |
| B6-RPL-05 | B04 | Transport/server no automatic HTTP mutation retry on 401/timeout/5xx/reload; read-only status retry is distinct; potential additive eligibility contract version reviewed and pinned | NOT RUN |
| B6-HOST-01 | B05 | Published exact owned IIS Express HTTPS: PR #95 queue/controller/worker/config/route admission before requests and after cutover; verified absence before owned cleanup | NOT RUN |
| B6-HOST-02 | B05 | Real direct-host JWT/cookie/CSRF/Origin/CSP/HSTS, exact13 mode/principal, forwarded spoof, refresh/permission/epoch and auth limiter boundaries | NOT RUN |
| B6-HOST-03 | B05 | Single-worker topology proven; measured limiter under attack/concurrency and sink outage; no multiworker or durable-audit claim without corresponding test | NOT RUN |
| B6-COMP-01 | B05 | Actual cached old/new client/host, original v1 UNKNOWN, additive v1 protected positive read, legacy v2 unsupported behavior, refresh/logout and matched rollback; no schema13 downgrade | NOT RUN |
| B6-PAIR-01 | B05 | Immutable same candidate frontend/backend built assets, schema13 and restricted SQL, Chromium and Edge exact six-command + manual-resend + operator workflow with independent physical oracle and no unresolved critical findings | NOT RUN |
| B6-LOAD-01 | B05 | Per-operation command p50/p95/p99, count, timeouts/errors, throughput, workers/SQL locks/pool/journal growth, baseline and bounded scenario, sample size and source hash recorded | NOT RUN |
| B6-REC-01 | B05 | Application-level backup/restore, observed RPO/RTO, alert response, UNKNOWN aging, fail-closed downtime and activity after recovery; not just SQL backup seconds | NOT RUN |
| B6-REL-01 | B06 | Candidate manifest/version/hash + paired CI/SQL/browser/runbook/limits/exception provenance complete and not conflated across artifacts | NOT RUN |
| B6-REL-02 | B06 | Named Owner-approved numeric target versus measured final candidate for latency, errors, throughput, journal, RPO/RTO, alert, unresolved age | NOT RUN |
| B6-REL-03 | B06 | Separate approval for nonproduction resend enable, staging and production; no implicit GO by docs merge or CI | NOT RUN |

Frontend companion gates [Frontend F6 acceptance](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/development/WEB6_WEB_ACCEPTANCE.md) are **independently owned**. Backend B04 alone cannot enable F02. Cross-repo candidate gate B6-PAIR-01 needs both owners' results and exact hashes.

## 3. SQL replay oracle for every mutation

**Physical check** (must run after writer/contender/operator drain): reconcile immutable journal and terminal audit actor/key/fingerprint/epoch; all source Header/Lines/Posted; stock balances, movement ledger, valuation and COGS; repeated Sales products/gifts and Receipt unique Product; exact business count before/after. Compare one command at a time, avoid SQL query under still-open transaction as the only outcome. Verify every restore/destructive test rechecks original login and DB identity; never silently connect to master or a non-owned DB.

| Case | Allowed journal effect | Forbidden |
|---|---|---|
| First original COMMITTED | One original matching COMMITTED row and value effect | Second business posting |
| Manual resend after COMMITTED | Original response replay, no new effect | New journal row/different operationId |
| Original killed before SQL Commit | No effect until separately consented manual attempt may commit under same key | Assuming status404 proves rollback or silently creating new key |
| Operator first durable CANCELLED | One immutable negative audit/tombstone; no business effect by late writer/resend | Convert CANCELLED to COMMITTED or delete tombstone |
| Original first COMMITTED, operator late | Retain original outcome; operator records no false negative | Force-negative after COMMITTED |
| Original result expired/compacted | EXPIRED tombstone, no original result projection, no reexecution | Rebuild/guess result version or post again |
| Wrong actor/key/fingerprint/epoch | Auth/key/epoch rejection with no business effect | Leaking other actor's result |

## 4. Running selected tests

Use current solution/project paths and existing test-policy selectors, not guessed custom commands. Recommended responsible projects:
- `tests/MyDmsVn.Server.HttpHost.Tests` for API/security/CSP/no-auto-retry/single-worker policy (SQL-free).
- `tests/MyDmsVn.Server.Infrastructure.Security.Tests` for real owner SQL, original crash processes, operator-fence and published IIS Express wrappers.
- `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests` and `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests` for valued ledger, schema13/migration and restricted principal.
- `tests/MyDmsVn.WebOperation.CrashHost` for actual OS process kill/fence; never replace this with in-memory TestServer.
- WEB6 operator CLI project/tests to be added in B02.

Protect all SQL integration with explicit opt-in, disposable resource creation identity, independent teardown and TRX discovery accounting. On Windows runner, use `dotnet test <actual-project>.csproj -c Release -f net48 --no-restore --filter <explicit-cases> --logger "trx;LogFileName=<run-specific>.trx"`. SQL-free CI must not silently skip SQL when claiming B6-RPL or B6-MNT. Frontend CI Chromium on Linux does not prove Windows IME/Narrator/Edge or IIS.

## 5. Acceptance and stop conditions

- No B02 execute command before operator identity/DB target and explicit authorization. No mixing runtime and operator principal. No user-supplied DB name/SQL identifier interpolation in command without strict allowlist and owned identity.
- No B05 live workload until host `VerifyHost/Admit` and SQL allocation ownership pass. Timeout during cleanup means **QUARANTINE**, not best-effort DROP. Never touch the two historical WEB5 quarantines.
- No B06 GO with B6-REL-02 blanks; no automatic mutation retries ever. A bounded pass despite missing full IIS is named `VERIFIED NONPRODUCTION`, not `STAGING ACCEPTED`.
- Any unresolved P0/P1 issue in same-key idempotency, scope/auth, actor/epoch, journal/stock, SQL teardown or service recovery blocks pilot enable and acceptance. P2 requires explicit disposition rather than converting to green.

## B03 bounded candidate limits — 2026-11-09

The [candidate record](../evidence/web6/20261109-b03-maintenance-recovery.md) owns actual source, discovery/TRX counts and hashes. Local execution does not accept B6-OPS-05 or B6-MNT-01..03. Positive current proof is restricted to SalesPost/ReceiptPost. The four current Draft Create/Update kinds remain UNKNOWN because schema13 has no original rowversion/line witness. Superseded Draft proof requires complete unambiguous retained-committed lineage ending in immutable Post and cannot exclude unjournaled Local/admin delete-recreate.

Restore admission proves restricted-runtime journal Admit+Complete only; full HTTP/business handlers and service RPO/RTO are NOT RUN. Provider/network/process-crash compaction evidence is NOT RUN and remains an explicit gap, not PASS. The Task 2 Minor (reject default DateTimeOffset before evidence creation) remains deferred. TEST_CLEANUP_ONLY is capability-gated hygiene after exact injected-fault assertions and never changes a quarantined workflow into success. Manual resend OFF; staging NOT ACCEPTED; production NO-GO.

**Historical B03 final-run stop (2026-11-09):** [candidate evidence](../evidence/web6/20261109-b03-maintenance-recovery.md) preserves 344 executed, 342 passed / 2 failed / 0 skipped. A net48 path-length failure in the last race arrangement caused its guard to retain a new database and refuse further admission. Fresh quiescence does not replace the missing original allocation tuple or authorize cleanup. The later remediation below does not convert that candidate to PASS.

Three earlier SQL-free 8/9 safety attempts retained synthetic ACL-drift cases without SQL allocation. Their failures and all later failed-attempt resources remain retained; no historical SQL, synthetic or diagnostic quarantine was adopted or cleaned.

## B03 subsequent remediation — 2026-11-09

[Subsequent bounded evidence](../evidence/web6/20261109-b03-maintenance-recovery.md#subsequent-remediation-20261109) independently accepted **R2e NativeSafety: 9/9, 60 ordered ACL markers and seven immutable checkpoints**, with verified candidate/checkpoint removal and process drain; and **R2f ATTEMPT_05: focused SQL ownership-manifest 1/1**, with bound Intent/Original/Complete, native receipt, fresh independent SQL absence, subject-control absence, exact candidate-root removal and process drain. R2f's administrative master allocator preflight remains **not restricted-runtime/operator proof**. Its three witness records/private controls remain retained. Neither gate replaces the failed full candidate or makes seven NativeSafety phases interchangeable with 28 B03Candidate phases.

**Full 12-selection / 28-checkpoint candidate and protected workflow remain BLOCKED.** Current GitHub-hosted ephemeral storage has no reviewed durable private Intent/Original destination and acknowledgment before SQL side effects; final sanitized hashes cannot replace that witness, and raw private identifiers must not be uploaded. Exact-Fact supervision, full reconciliation and pre-SQL preflight wiring remain incomplete. The Owner authorized **design only** for a self-hosted runner with a persistent encrypted evidence volume: **NOT IMPLEMENTED / NOT VERIFIED**, with no provisioning, runtime, cleanup or dispatch authority.

B6-OPS-05 and B6-MNT-01..03 remain BLOCKED. Provider/network/process-crash compaction proof, deferred Minors and existing recovery/attended acceptance limits remain unchanged; all quarantine/no-cleanup dispositions stand. Manual resend OFF; staging NOT ACCEPTED; production NO-GO.
