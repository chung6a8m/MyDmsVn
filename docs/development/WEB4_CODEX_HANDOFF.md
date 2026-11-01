# Codex handoff — WEB4 backend, atomic journal and Windows CI

**Active selected direction; tasks B01–B06 PLANNED until source PRs/evidence.** Start with [AGENTS](../../AGENTS.md) → [AI_CONTEXT](../../AI_CONTEXT.md) → [plan selector](../plans/README.md) → [owning WEB4 backend plan](../plans/20261101-003-web4-reliability-server.md) → [idempotency design](../design/WEB4_IDEMPOTENCY_JOURNAL_CONTRACT.md) → [ADR 0014](../adr/0014-web4-durable-operations.md) → [acceptance](WEB4_BACKEND_ACCEPTANCE.md) → [TEST_POLICY](../TEST_POLICY.md). Frontend partner plan: https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/codex/activate-web4-reliability/docs/plans/20261101-002-web4-reliability-web.md.

## Source preflight before every task

1. Re-read remote main and task's owning plan/evidence; do not start from an old WEB3 worktree or assume documentation activation commit is implementation. Use fresh branch from current main and one focused PR per task.
2. Inspect exact namespaces, current code, locked package/SDK versions, SQL script path and existing test discovery. Proposed APIs and test names in this handoff are suggestions, not claims.
3. Preserve independent active CEX/WinForms selection, runtime WinFormsKit gitlink, Desktop exact10 and WEB3 historical bounded evidence. No source/tests/SQL/grants were changed by the WEB4 docs activation.
4. Smallest implementation to satisfy verified contract. Prefer regression tests for the failure boundary with RED/GREEN evidence. Record actual test counts and non-production ownership. A change touching old Sales/Receipt common paths triggers their focused regression.
5. Never claim success from missing/skipped tests, HTTP stubs as SQL proof, IIS Express as full IIS, or .NET wrapper counts as browser counts.

## Recommended PR sequence and prompts

**PR B01 — contract and transaction checkpoint.** Read current six handler UoW implementations, HttpHost wire/auth, Application auth/validation/audit and schema10/11 migration internals. Produce source reference matrix, signed-off handler transaction insertion proposal and canonical request/fingerprint fixture set owned by backend. Lock status wire, identity/permission controls and schema12 compatibility; update proposed ADR if a source finding requires it. Do not touch product runtime/SQL or claim tests not run. Frontend F01 consumes exact B01 fixture commit/hashes.

**PR B02 — Windows SQL-free CI** (may run after B01 in parallel with journal work). Add minimal .github/workflows/backend-web.yml on Windows, net48 locked restore/build/actual HttpHost tests; pinned actions and least privileges, fail on zero tests. Separate front-end CI and SQL operator gate. Check hosted push/PR runs on final reviewed head, not only YAML lint/local build. Record workflow ID/job and test rows.

**PR B03 — schema/journal atomicity.** Create approved Web-only schema12 opt-in migration and role/fingerprint/compatibility checks, journal interface and SQL repository bound to existing UoW transaction. Implement SQL key serialization and the immutable committed-result record with no cross-transaction completion; test partial fingerprint, under/overgrant, rollback, concurrent same-key. Do not add a new business/HTTP controller or rewrite valuation. Provide a precise Web host admission/reprovision runbook. B04 must use this API unchanged unless a reviewed contract correction is required.

**PR B04 — six APIs and operation status.** Extend strict controllers with Web operation metadata + protected status query, integrate same UoW/journal in existing authorized Application handlers. Require canonical key only in explicitly enabled WEB4 mode; preserve original body/status, Lossless ID/decimal, Write/Post-without-Read and no fabricated Post version. Add key mismatch, concurrent same key, current auth revoke and unknown status coverage; source-test local/SQL boundaries. Frontend F03 consumes pinned B04 API, never guesses error shapes.

**PR B05 — verified integration and oracle.** Augment existing test-only owned IIS Express/SQL browser fixture, extend independent source/ledger/value Q/V/COGS oracle and new WEB4 live selectors. Run focused migration/Auth/Inventory/Security SQL owners then production build + Chromium and Edge on separate fresh databases with normal TLS and actor/cleanup verification. Coordinate exact Web F04 candidate and independent current reviewed backend candidate. Preserve prior failures and report new results separately; no production/Full IIS claim.

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

