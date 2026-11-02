# Codex handoff — WEB4 backend, atomic journal and Windows CI

**Roadmap 04 is ARCHIVED / COMPLETED (bounded), Owner 2026-11-02.** Backend B01–B06 are DONE within their recorded scopes. Owner accepted B06/F05 and its limits, authorized final reviews/CI and merge of backend PR #85/frontend PR #22, then explicitly requested archival. Read the [archive decision](../archive/20261102-web4-reliability/README.md), [frozen completed plan](../archive/20261102-web4-reliability/plans/20261101-003-web4-reliability-server.md), [former heading anchors](../plans/20261101-003-web4-reliability-server.md) and [B06 acceptance evidence](../evidence/web4/20261102-b06-operator-handoff.md#owner-acceptance-and-merge--2026-11-02). No WEB4 task remains selected; future work needs a new explicit selection.

The [operator handoff](WEB4_OPERATOR_HANDOFF.md) and [host runbook](../../src/MyDmsVn.Server.HttpHost/README.md#web4-coordinated-mode-and-migration-procedure) remain technical references. Preserve explicit `legacy-web11`/exact11 versus `web4`/exact12 matching artifacts/principal, same-transaction journal, current actor/session/permission and epoch/fingerprint checks, original command outcomes and Desktop exact10. Same-key resend stays disabled; missing status remains UNKNOWN. Real process-crash/Commit, negative administrative unlock, operator rotation/compaction/backup rehearsal, attended IME/Narrator and full IIS/production remain unverified or unapproved. Original evidence/WEB1–03 and independently ACTIVE CEX remain unchanged.

**Historical execution notes below:** retained to explain the accepted source and task sequence. They are not prompts to start new work; use the current execution index for a separately selected follow-up. Earlier candidate/pending statements describe their dated checkpoints.

<a id="source-preflight-before-every-task"></a>

## Historical source preflight before every task

1. Re-read remote main and task's owning plan/evidence; do not start from an old WEB3 worktree or assume documentation activation commit is implementation. Use fresh branch from current main and one focused PR per task.
2. Inspect exact namespaces, current code, locked package/SDK versions, SQL script path and existing test discovery. Proposed APIs and test names in this handoff are suggestions, not claims.
3. Preserve independent active CEX/WinForms selection, runtime WinFormsKit gitlink, Desktop exact10 and WEB3 historical bounded evidence. No source/tests/SQL/grants were changed by the WEB4 docs activation.
4. Smallest implementation to satisfy verified contract. Prefer regression tests for the failure boundary with RED/GREEN evidence. Record actual test counts and non-production ownership. A change touching old Sales/Receipt common paths triggers their focused regression.
5. Never claim success from missing/skipped tests, HTTP stubs as SQL proof, IIS Express as full IIS, or .NET wrapper counts as browser counts.

<a id="recommended-pr-sequence-and-prompts"></a>

## Historical PR sequence and prompts

**PR B01 — contract and transaction checkpoint.** Read all six actual handler UoW implementations, particularly Sales/PostSalesReceipt.cs (Sales Post returns version and writes audit after Commit), Sales/SalesReceiptFeatures.cs, Inventory/GoodsReceiptFeatures.cs; inspect both SalesWireOutput.Post and InventoryWireOutput.Post, HttpHost wire/auth, Application auth/validation/audit and DatabaseMigrationRunner exact10/exact11 admission internals. Produce source reference matrix, signed-off handler transaction insertion proposal and canonical request/fingerprint fixture set owned by backend. Lock **original per-command outcome detail** (Create/Update full authorized Write result; Sales Post with version; Receipt Post without); status may return exactly original authorized Write fields, never additional Get-only data. Freeze status identity binding (operationId + kind + method/route/resource + canonical SHA-256 fingerprint + server epoch), required `X-MyDmsVn-Deployment-Epoch` for all WEB4 writes/status and `X-MyDmsVn-Operation-Fingerprint` for status; reject wrong/absent/mismatched headers before SQL execution/result disclosure. Frontend F01 must also scope Dockview **layout** (not just IndexedDB drafts) to epoch + actor. Lock durable tombstone policy, explicit user-confirmed same-ID resend versus operator reconciliation, identity/permission controls and exact11→12 schema12 compatibility; update proposed ADR if a source finding requires it. Do not touch product runtime/SQL or claim tests not run. Frontend F01 consumes exact B01 fixture commit/hashes.

**PR B02 — Windows SQL-free CI** (may run after B01 in parallel with journal work). Add minimal .github/workflows/backend-web.yml on Windows, net48 locked restore/build/actual HttpHost tests; pinned actions and least privileges, fail on zero tests. Separate front-end CI and SQL operator gate. Check hosted push/PR runs on final reviewed head, not only YAML lint/local build. Record workflow ID/job and test rows.

**PR B03 — schema/journal atomicity.** Create approved Web-only schema12 opt-in migration and role/fingerprint/compatibility checks, separate frozen Web11 admission inside DatabaseMigrationRunner (not journal-count-only), preserve exact Desktop10 and earlier exact10→Web maintenance flow; implement operator-controlled deployment epoch and minimum journal/epoch grants. Add journal interface/SQL repository bound to existing UoW transaction. Implement SQL key serialization and the immutable committed-result record with no cross-transaction completion; test partial fingerprint, under/overgrant, rollback, concurrent same-key, key tombstone/expiry, authenticated deployment epoch on clone/rewind; **mandatory SQL Local regression**: all six Sales/Receipt mutations without Web metadata on exact schema10 must not touch the Web journal. Do not add a new business/HTTP controller or rewrite valuation. Provide a precise Web host admission/reprovision runbook. B04 must use this API unchanged unless a reviewed contract correction is required.

**PR B04 — six APIs and operation status (DONE (bounded; Owner accepted and merged 2026-11-02)).** Extend strict controllers with immutable Web operation metadata tied to the validated request session, authenticated context and protected status; use the existing B03 same-UoW journal integration in authorized Application handlers including Sales/PostSalesReceipt.cs. Journal replay must reauthorize before returning the original command-specific result, without running mutable business validators again. Require `MYDMSVN_WEB_OPERATION_MODE` exactly `legacy-web11` (exact11/old client) or `web4` (exact12/journal principal/keyed client), with canonical keys required in WEB4 and keyed requests rejected in legacy mode; preserve original body/status, Lossless ID/decimal, Write/Post-without-Read and no fabricated Post version. Add key mismatch, concurrent same key, current auth revoke and unknown status coverage; require server-current DB epoch and expected fingerprint matching before journal readback, with negative same-origin/different-DB/late-tab cases; source-test local/SQL boundaries. Frontend F03 consumes pinned B04 API, never guesses error shapes.

**PR B05 — verified integration and oracle.** Split checkpoints without splitting task ownership: B05-A backend-only fixture/oracle after B04; F04-A frontend browser specs after F03/B05-A interface; B05-B/F04-B final integrated invocation on mutually pinned candidates. Augment existing test-only owned IIS Express/SQL browser fixture, extend independent source/ledger/value Q/V/COGS oracle and new WEB4 live selectors. Run focused migration/Auth/Inventory/Security SQL owners then production build + Chromium and Edge on separate fresh databases with normal TLS and actor/cleanup verification. Coordinate exact Web F04 candidate and independent current reviewed backend candidate. Preserve prior failures and report new results separately; no production/Full IIS claim.

**PR B06 — handoff/acceptance.** Reconcile runbooks and operator instructions, close current tasks only with actual evidence and Owner acceptance. Keep original WEB1–03 evidence and CEX queue unchanged; archive execution plans ONLY after accepted bounded completion, while keeping technical ADR/specs.

### Suggested report footer for each implementation PR

~~~text
Task/owner:
Main baseline / source commit / counterpart commit:
Changed boundary, preserved invariants:
Real commands and selected/discovered/executed test rows (PASS/FAIL/SKIP):
Windows/SQL/IIS/Chromium/Edge scope, actual fixture and cleanup:
Negative auth/rollback/replay/canonical cases:
Known failures and fixes:
NOT RUN / accepted limits:
Review findings, Owner disposition, next dependency:
~~~

Do NOT automatically move a PLANNED task to DONE after source commit. No repeated Owner approval is required for in-scope, technically settled decisions; new schema or security behavior beyond the approved B01 design, full IIS/production, broad grants, or moving away from net48 must be explicitly surfaced before expansion.



<a id="b03-implementation-handoff-to-b04"></a>

<a id="b03-merged-prerequisite-and-current-b04-handoff"></a>

## Historical B03 prerequisite and B04 handoff

The merged B03 source adds `Application.Operations.WebOperationContext`, `IWebOperationResultCodec` and UoW-resolved `IWebOperationJournal`; all six commands accept optional server-only metadata. Null metadata preserves Local exact10 without resolving a journal. The codec receives the original command result before Commit. B04 now supplies the pure, field-allowlisted frozen-v1 HTTP codec and strict operation admission; the SQL test codec is not the HTTP implementation. Preserve original Sales/Receipt Write detail, Sales Post version and unversioned Receipt Post acknowledgement. Frozen operation error codes map explicitly to the B01 HTTP status/envelope. Runtime/test source `bd871518fd162927d938babd5535a2ddf91c2ad5` has local verification in B04 evidence; replay also binds decoded DTO kind/result ID to immutable journal identity and Update/Post target. At that candidate checkpoint hosted CI and Owner acceptance/merge were pending; the subsequent B04 acceptance and final archive supersede that status.

`HttpHost.Startup` now selects both schema and restricted principal checks from required `MYDMSVN_WEB_OPERATION_MODE`: exactly `legacy-web11` uses `SqlWebSchema11CompatibilityChecker`/non-journal principal on exact11 with the old client; exactly `web4` uses `SqlWebSchemaCompatibilityChecker`/journal principal on exact12 with the keyed client. Missing/unknown mode refuses startup. Legacy mode rejects keyed writes; WEB4 rejects missing keys and never dispatches unkeyed null-context writes. The [host runbook](../../src/MyDmsVn.Server.HttpHost/README.md#web4-coordinated-mode-and-migration-procedure) owns the coordinated migration, reprovisioning, environment and artifact procedure, without granting rollout approval.

The B04 source routes are authenticated `GET /api/v1/operations/context` and protected `GET /api/v1/operations/{operationId}`. Context returns epoch, actor and `web4-v1`; status requires the current epoch and expected canonical fingerprint. The status `data` object has exactly nine frozen fields: `deploymentEpoch`, `operationId`, `operationKind`, `method`, `routeScope`, `requestTargetId`, `requestFingerprint`, `state`, `outcome`. Original Create request target stays null even after a result document ID is assigned. COMMITTED contains only the original command success envelope; RESULT_EXPIRED/RESULT_UNAVAILABLE contain null outcome. No Get/Read reconstructs it. Current SQL session/account/operation authorization and actor/epoch/fingerprint checks precede disclosure; missing status or request timeout never proves rollback or permits automatic retry.

Frontend F03 may consume the locally verified immutable B04 runtime/test candidate `bd871518fd162927d938babd5535a2ddf91c2ad5` with [B04 evidence](../evidence/web4/20261102-b04-http-operations.md) and its retained limits. Hosted Windows CI at `b225bee` is PASS and Owner accepted/merged B04 through PR #83 on 2026-11-02; local verification is not B05 browser acceptance. Keep B01 corpus pin `1c603c6ac57e59a3f7b4bc6ca976b2d531c079aa` and manifest SHA256 `0acab190e7fe126d53c13c0510a1958365e5a6e1fda78a4321da78297e0b3f1a` unchanged. Frontend base for this prerequisite is `68398accdd03a1ed78a2e3bec7c5cb2499457b6d`; its own F02/F03 evidence owns browser draft/recovery behavior. This B04 handoff did not itself select B05/B06; the later B05 acceptance is recorded below.

`SqlWebOperationMaintenance` is explicitly constructed by a privileged caller, never registered into runtime DI. Compaction records an audited attempt under the current database epoch and actor/key fences, retains the entry's original epoch/identity and permanent tombstone. No negative operator unlock is implemented. B06 still owns the completed operator rotation/retention/reconciliation runbook and production prerequisites. Historical [B03 evidence](../evidence/web4/20261101-b03-schema-journal.md) remains unchanged; [B04 evidence](../evidence/web4/20261102-b04-http-operations.md) owns new verification and remaining limits. The local HTTP/SQL verification does not itself assert Owner acceptance; the separate 2026-11-02 decision and merge are recorded in B04 evidence. B05 browser acceptance and production approval are not implied.

## Historical handoff after B04 acceptance

B04 is DONE (bounded), merged by PR #83 at `59ca21d16f7839c6d7c553395a367e465591652b`; the paired frontend PR #20 merged at `604c02e3cd1e959fd976fb80d194371c9ceec42b`. Start future selected work from current main, not the original B03 baseline. No B04 implementation remains queued. At that B04 checkpoint B05/B06 were PLANNED and unselected; same-key resend and full IIS/production gates remain closed. See [acceptance evidence](../evidence/web4/20261102-b04-http-operations.md#owner-acceptance-and-merge--2026-11-02).

<a id="current-handoff-after-b05-acceptance"></a>

## Historical handoff after B05 acceptance

B05 is DONE (bounded), accepted and merged through [PR #84](https://github.com/chung6a8m/MyDmsVn/pull/84) at `9ef084a61c1dd0e9f59be049fb5fbb51870e9444`; paired frontend [PR #21](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/pull/21) merged at `f7765f052a66242e978a38daa4c22221ea262411`. [B05 evidence](../evidence/web4/20261102-b05-recovery-pilot.md#owner-acceptance-and-merge--2026-11-02) records Owner authorization, final reviews, exact-head CI and real paired browser/SQL results. No B05 implementation remains queued. At this B05 checkpoint B06/F05 were PLANNED; their later accepted completion and archive are recorded above. New work requires its own selection from current main. Same-key resend stays disabled pending the process-crash/Commit gate; full IIS/production remain unapproved.
