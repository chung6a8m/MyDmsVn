# WEB4 backend acceptance matrix — protocol, SQL, CI, real pilot

**Status:** PLANNED test scenarios, no results. **Owner:** [B01–B06 backend plan](../plans/20261101-003-web4-reliability-server.md). **Contract:** [operation/journal design](../design/WEB4_IDEMPOTENCY_JOURNAL_CONTRACT.md). The frontend-specific matrix is owned in ReactWebUI; these IDs are scenario identifiers, NOT discovered test names. Use [sole backend test policy](../TEST_POLICY.md) and [test owner commands](../../tests/README.md).

| ID | Gate/owner | Scenario and required assertion |
|---|---|---|
| B4-CON-01 | B01 corpus | Six canonical routes, method, route ID, expectedVersion, decimal/int64 strings, ordered lines, gift/duplicates and receipt constraints hash reproducibly |
| B4-CON-02 | B01 corpus | Same operation key and equivalent canonical request admitted; changed payload/route/actor/rowversion forbidden or independently actor-scoped |
| B4-CI-01 | B02 Windows Actions | net48 locked restore, build, SQL-free HttpHost and affected Application/Architecture tests; actual executed >0, 0 failing/required skips, no secret usage |
| B4-MIG-01 | B03 migrations | Web11→12 opt-in, fresh Web12, exact script journal, fingerprints and schema12 startup, partial/unmanaged/too-new fail closed |
| B4-MIG-02 | B03 desktop | Desktop exact10 unchanged; no mixed writer claim, no historic script mutation |
| B4-GRT-01 | B03 restricted role | Minimum journal rights, Web role rejects DELETE/DDL/CONTROL, Desktop/maintenance role boundaries preserved, startup refuses under/overgrant |
| B4-JRN-01 | B03 SQL transaction | Business rollback after any planned fault leaves no committed success, no ledger orphan; same Commit atomically persists result |
| B4-JRN-02 | B03 SQL transaction | Two concurrent calls same actor/key are serialized across separate connections; exactly one business effect and stable journal record |
| B4-JRN-03 | B03 SQL transaction | Two calls same key but different request scope/expectedVersion return deterministic conflict without mutation |
| B4-HTTP-01 | B04 SQL-free host | Required canonical GUIDv4 header, duplicate/empty/invalid key/unknown fields fail before handler; all six routes preserved |
| B4-HTTP-02 | B04 Web auth | Current session/grants rechecked for new/replayed/status; revoked account/permission returns 401/403; cross-actor row existence never leaked |
| B4-HTTP-03 | B04 readback | COMMITTED response is minimal, precise and authorized; absent/unknown never means safe to re-Post; Receipt Post ack never invents version |
| B4-HTTP-04 | B04 compatibility | Existing strict WEB2/WEB3 wire and Local handler behavior unchanged; no unkeyed fallback when WEB4 mode enabled |
| B4-RES-01 | B05 Security/Inventory SQL | Lost response AFTER physical Post commit and subsequent status: one source/ledger/value/balance effect and frozen COGS |
| B4-RES-02 | B05 Security/Inventory SQL | SQL rollback, timeout, deadlock victim, stale rowversion, lost response BEFORE/around commit; no automatic replay |
| B4-RES-03 | B05 Security SQL | Receipt unique product, Sales duplicated/gift lines, row identity and 200-line bound preserved |
| B4-LIVE-01 | B05 IIS Express/SQL | Browser production React→JWT/HTTP→Application→restricted SQL→independent physical oracle for both Chromium/Edge, fresh fixtures |
| B4-LIVE-02 | B05 recovery/browser | Web F04 verifies reload, IndexedDB, cross-tab actor fencing, one operation result, dirty other document and no replay |
| B4-OPS-01 | B06 runbook | Operator schema12/provisioning/rollback limits, journal inspection/redaction, retention safety, backup/restore, release blockers recorded |

### Gate execution and evidence rules

- B02: GitHub-hosted Windows SQL-free tests only. It must report actual workflows, runner, commit SHA and discovered/executed rows. A green YAML lint or local build is not hosted CI.
- B03/B04: use existing net48 Migrations/HttpHost/Security/Inventory test owners; no restored global SQL runner, no blanket test policy rewrite, no test-only shortcut around journal.
- B05: SQL nonproduction, explicit master input, owned fresh DB and SID-tracked runtime login, sequential SQL project owners, positively owned teardown and failed-cleanup quarantine. Backend security wrapper count is separate from Playwright browser case count; each browser uses a new DB.
- Run complete necessary Sales/Receipt/auth/valuation concurrency negative regression when those boundaries change. Keep evidence of RED and corrections; do not fabricate failures or count 0/skipped cases as PASS.
- Exact test selectors/paths, project names, source/test hashes, commands and result counts belong in new dated evidence. This matrix is not an execution log. Do not put credentials, bearer/refresh tokens, raw HAR, private traces, raw connection strings or production identifiers in repository.
- Attended IME/Narrator, full IIS, multi-worker production controls and production approval remain NOT RUN, never inherited from WEB3.

