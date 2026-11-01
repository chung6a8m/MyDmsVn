# Codex handoff — WEB4 backend, atomic journal and Windows CI

**B01 DONE (bounded contract), F01 consumer signoff recorded; Owner accepted; PR #80 merged 2026-11-01; B02–B06 PLANNED.** [B01 checkpoint](../design/WEB4_B01_SOURCE_CHECKPOINT.md) · [canonical corpus](../contracts/web4-v1/README.md) · [evidence](../evidence/web4/20261101-b01-contract.md). Start with [AGENTS](../../AGENTS.md) → [AI_CONTEXT](../../AI_CONTEXT.md) → [plan selector](../plans/README.md) → [owning WEB4 backend plan](../plans/20261101-003-web4-reliability-server.md) → [idempotency design](../design/WEB4_IDEMPOTENCY_JOURNAL_CONTRACT.md) → [ADR 0014](../adr/0014-web4-durable-operations.md) → [acceptance](WEB4_BACKEND_ACCEPTANCE.md) → [TEST_POLICY](../TEST_POLICY.md). Frontend partner plan: https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/plans/20261101-002-web4-reliability-web.md.

**Cross-repo link lifecycle:** activation PR #79 and frontend #18 are merged. Existing activation documents link to main; new B01/F01 artifacts use their task branch or exact published commit until merge. Never call a published contract candidate runtime acceptance or delete activation/task branches as an incidental step.

## Source preflight before every task

1. Re-read remote main and task's owning plan/evidence; do not start from an old WEB3 worktree or assume documentation activation commit is implementation. Use fresh branch from current main and one focused PR per task.
2. Inspect exact namespaces, current code, locked package/SDK versions, SQL script path and existing test discovery. Proposed APIs and test names in this handoff are suggestions, not claims.
3. Preserve independent active CEX/WinForms selection, runtime WinFormsKit gitlink, Desktop exact10 and WEB3 historical bounded evidence. No source/tests/SQL/grants were changed by the WEB4 docs activation.
4. Smallest implementation to satisfy verified contract. Prefer regression tests for the failure boundary with RED/GREEN evidence. Record actual test counts and non-production ownership. A change touching old Sales/Receipt common paths triggers their focused regression.
5. Never claim success from missing/skipped tests, HTTP stubs as SQL proof, IIS Express as full IIS, or .NET wrapper counts as browser counts.

## Recommended PR sequence and prompts

**PR B01 — contract and transaction checkpoint.** Read all six actual handler UoW implementations, particularly Sales/PostSalesReceipt.cs (Sales Post returns version and writes audit after Commit), Sales/SalesReceiptFeatures.cs, Inventory/GoodsReceiptFeatures.cs; inspect both SalesWireOutput.Post and InventoryWireOutput.Post, HttpHost wire/auth, Application auth/validation/audit and DatabaseMigrationRunner exact10/exact11 admission internals. Produce source reference matrix, signed-off handler transaction insertion proposal and canonical request/fingerprint fixture set owned by backend. Lock **original per-command outcome detail** (Create/Update full authorized Write result; Sales Post with version; Receipt Post without); status may return exactly original authorized Write fields, never additional Get-only data. Freeze status identity binding (operationId + kind + method/route/resource + canonical SHA-256 fingerprint + server epoch), required `X-MyDmsVn-Deployment-Epoch` for all WEB4 writes/status and `X-MyDmsVn-Operation-Fingerprint` for status; reject wrong/absent/mismatched headers before SQL execution/result disclosure. Frontend F01 must also scope Dockview **layout** (not just IndexedDB drafts) to epoch + actor. Lock durable tombstone policy, explicit user-confirmed same-ID resend versus operator reconciliation, identity/permission controls and exact11→12 schema12 compatibility; update proposed ADR if a source finding requires it. Do not touch product runtime/SQL or claim tests not run. Frontend F01 consumes exact B01 fixture commit/hashes.

**PR B02 — Windows SQL-free CI** (may run after B01 in parallel with journal work). Add minimal .github/workflows/backend-web.yml on Windows, net48 locked restore/build/actual HttpHost tests; pinned actions and least privileges, fail on zero tests. Separate front-end CI and SQL operator gate. Check hosted push/PR runs on final reviewed head, not only YAML lint/local build. Record workflow ID/job and test rows.

**PR B03 — schema/journal atomicity.** Create approved Web-only schema12 opt-in migration and role/fingerprint/compatibility checks, separate frozen Web11 admission inside DatabaseMigrationRunner (not journal-count-only), preserve exact Desktop10 and earlier exact10→Web maintenance flow; implement operator-controlled deployment epoch and minimum journal/epoch grants. Add journal interface/SQL repository bound to existing UoW transaction. Implement SQL key serialization and the immutable committed-result record with no cross-transaction completion; test partial fingerprint, under/overgrant, rollback, concurrent same-key, key tombstone/expiry, authenticated deployment epoch on clone/rewind; **mandatory SQL Local regression**: all six Sales/Receipt mutations without Web metadata on exact schema10 must not touch the Web journal. Do not add a new business/HTTP controller or rewrite valuation. Provide a precise Web host admission/reprovision runbook. B04 must use this API unchanged unless a reviewed contract correction is required.

**PR B04 — six APIs and operation status.** Extend strict controllers with Web operation metadata + protected status query, integrate same UoW/journal in existing authorized Application handlers including Sales/PostSalesReceipt.cs. Journal replay must reauthorize before returning the original command-specific result, without running mutable business validators again. Require canonical key only in explicitly enabled WEB4 mode; preserve original body/status, Lossless ID/decimal, Write/Post-without-Read and no fabricated Post version. Add key mismatch, concurrent same key, current auth revoke and unknown status coverage; require server-current DB epoch and expected fingerprint matching before journal readback, with negative same-origin/different-DB/late-tab cases; source-test local/SQL boundaries. Frontend F03 consumes pinned B04 API, never guesses error shapes.

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

