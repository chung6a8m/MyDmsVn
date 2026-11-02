# WEB4 backend acceptance matrix — protocol, SQL, CI, real pilot

**Roadmap 04 is ARCHIVED / COMPLETED (bounded), Owner 2026-11-02.** Backend B01–B06 are DONE within their recorded scopes. Owner accepted B06/F05 and its limits, authorized final reviews/CI and merge of backend PR #85/frontend PR #22, then explicitly requested archival. Read the [archive decision](../archive/20261102-web4-reliability/README.md), [frozen completed plan](../archive/20261102-web4-reliability/plans/20261101-003-web4-reliability-server.md), [former heading anchors](../plans/20261101-003-web4-reliability-server.md) and [B06 acceptance evidence](../evidence/web4/20261102-b06-operator-handoff.md#owner-acceptance-and-merge--2026-11-02). No WEB4 task remains selected; future work needs a new explicit selection.

The [operator handoff](WEB4_OPERATOR_HANDOFF.md) and [host runbook](../../src/MyDmsVn.Server.HttpHost/README.md#web4-coordinated-mode-and-migration-procedure) remain technical references. Preserve explicit `legacy-web11`/exact11 versus `web4`/exact12 matching artifacts/principal, same-transaction journal, current actor/session/permission and epoch/fingerprint checks, original command outcomes and Desktop exact10. Same-key resend stays disabled; missing status remains UNKNOWN. Real process-crash/Commit, negative administrative unlock, operator rotation/compaction/backup rehearsal, attended IME/Narrator and full IIS/production remain unverified or unapproved. Original evidence/WEB1–03 and independently ACTIVE CEX remain unchanged.

This retained matrix defines historical scenario IDs, not test discovery or an execution queue. [B05 evidence](../evidence/web4/20261102-b05-recovery-pilot.md) owns the bounded SQL/browser pilot; [B06 evidence](../evidence/web4/20261102-b06-operator-handoff.md) owns accepted operational documentation and hosted SQL-free CI. B4-OPS-01 has document coverage; B4-OPS-02 operator rehearsal is not newly PASS. Use [TEST_POLICY](../TEST_POLICY.md) and [test commands](../../tests/README.md) for separately selected work.

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
| B4-HTTP-03 | B04 readback | COMMITTED returns **exactly the original permitted command outcome** (complete Create/Update Write response, Sales Post with version, Receipt Post without) after fresh auth; no extra Get/Read-only fields; absent/unknown never means safe to re-Post |
| B4-HTTP-04 | B04 compatibility | Existing strict WEB2/WEB3 wire and Local handler behavior unchanged; no unkeyed fallback when WEB4 mode enabled |
| B4-RES-01 | B05 Security/Inventory SQL | Lost response AFTER physical Post commit and subsequent status: one source/ledger/value/balance effect and frozen COGS |
| B4-RES-02 | B05 Security/Inventory SQL | SQL rollback, timeout, deadlock victim, stale rowversion, lost response BEFORE/around commit; no automatic replay |
| B4-RES-03 | B05 Security SQL | Receipt unique product, Sales duplicated/gift lines, row identity and 200-line bound preserved |
| B4-LIVE-01 | B05 IIS Express/SQL | Browser production React→JWT/HTTP→Application→restricted SQL→independent physical oracle for both Chromium/Edge, fresh fixtures |
| B4-LIVE-02 | B05 recovery/browser | Web F04 verifies reload, IndexedDB, cross-tab actor fencing, one operation result, dirty other document and no replay |
| B4-OPS-01 | B06 runbook | Operator schema12/provisioning/rollback limits, journal inspection/redaction, retention safety, backup/restore, release blockers recorded |

### Review-driven mandatory negative and compatibility gates

| ID | Gate/owner | Scenario and required assertion |
|---|---|---|
| B4-CON-03 | B01 corpus | Original success outcomes: Sales Create/Update full detail/version; Receipt Create/Update full detail/version and replacement line IDs; Sales Post with version; Receipt Post without version. 404/403/expired not a recovered success |
| B4-CON-04 | B01 corpus | Status 404 for persisted-but-not-dispatched intent; user-confirmed same-key immutable resend only; delayed initial request vs resend serialized, no fresh key/no automatic replay |
| B4-MIG-03 | B03 SQL migrations | Source-validated **exact11→12** path vs existing exact10→Web12, fresh12, fake/partial/spoofed11, schema9 and too-new; old 001–011 fingerprint immutable |
| B4-GRT-02 | B03 Security SQL | Server-authenticated Web database epoch stable across workers/restart and rotated after clone/rewind; same-origin, same numeric userId different database must not see old drafts |
| B4-JRN-04 | B03/B04 SQL | Immutable tombstone survives replay-payload expiry; status expired forbids new business effect, replay after expiry never starts Create/Post |
| B4-HTTP-05 | B04 SQL-free + SQL | Current Write/Post (not Read) re-authorizes replay/status and recovers command-specific exact original outcomes; revoked grant fails closed; post-commit audit failure still reports journal COMMITTED |
| B4-RES-04 | B05 Security/Inventory SQL | Never-dispatched browser intent then explicit same-key resend, concurrent delayed original and resend, exactly one posted document/ledger/value, no new key |
| B4-LOCAL-01 | B03/B04 SQL Local | **On exact desktop schema10** Local/Application Create/Update/Post Sales and Goods Receipt WITHOUT WebOperationMetadata; unchanged auth/rowversion/lines/rollback/Q/V/COGS, zero journal query or new table dependency |
| B4-LIVE-03 | B05-A→F04-A→B05-B/F04-B | Fixture/oracle ready independently of browser spec, then final paired acceptance on exact Web/backend commits and fresh owned Chromium/Edge SQL |
| B4-OPS-02 | B05/B06 privileged operator rehearsal | Exact actor/epoch/key/fingerprint matched, authoritative SQL journal+source+ledger/valuation checked; committed vs indeterminate vs expired tombstone distinguished; never unlock Edit without version or claim 404 is safe, retain audit trail |
| B4-CON-05 | B01 canonical corpus | Status envelope binds server-origin operationId/kind/method/routeScope/**original requestTargetId** (null for Create)/epoch/fingerprint, with committed resultDocumentId separate; frontend rederives hash from immutable request, not copied local metadata |
| B4-HTTP-06 | B04 HTTP + SQL | Six WEB4 writes and operation GET reject missing/malformed/duplicated/stale X-MyDmsVn-Deployment-Epoch against authoritative SQL; same origin + userId on switched database cannot commit/disclose |
| B4-HTTP-07 | B04 HTTP + SQL | GET readback rejects missing/mismatched expected X-MyDmsVn-Operation-Fingerprint without revealing committed outcome; correct hash + actor + current grant gets original result only |
| B4-RES-05 | B05 browser + SQL | Old tab with epoch A against database B, delayed status/POST or duplicate operation ID must remain fenced, no B write or recovered SETTLED; negative local/remote fingerprint drift checks |

### Gate execution and evidence rules

- B02: GitHub-hosted Windows SQL-free tests only. It must report actual workflows, runner, commit SHA and discovered/executed rows. A green YAML lint or local build is not hosted CI.
- B03/B04: use existing net48 Migrations/HttpHost/Security/Inventory test owners; no restored global SQL runner, no blanket test policy rewrite, no test-only shortcut around journal.
- B05 staging: **B05-A** prepares fixture/oracle after B04; **F04-A** prepares browser specs after F03 against B05-A protocol; **B05-B + F04-B** jointly run final acceptance. No circular task dependency. SQL nonproduction, explicit master input, owned fresh DB and SID-tracked runtime login, sequential SQL project owners, positively owned teardown and failed-cleanup quarantine. Backend security wrapper count is separate from Playwright browser case count; each browser uses a new DB.
- Run complete necessary Sales/Receipt/auth/valuation concurrency negative regression when those boundaries change. Keep evidence of RED and corrections; do not fabricate failures or count 0/skipped cases as PASS.
- Exact test selectors/paths, project names, source/test hashes, commands and result counts belong in new dated evidence. This matrix is not an execution log. Do not put credentials, bearer/refresh tokens, raw HAR, private traces, raw connection strings or production identifiers in repository.
- Attended IME/Narrator, full IIS, multi-worker production controls and production approval remain NOT RUN, never inherited from WEB3.

