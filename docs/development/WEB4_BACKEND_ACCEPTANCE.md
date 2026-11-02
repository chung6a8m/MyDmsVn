# WEB4 backend acceptance matrix — protocol, SQL, CI, real pilot

**Current selection — WEB4-06/B06 (2026-11-02): IN_PROGRESS / documentation candidate, Owner acceptance pending.** [Operator handoff](WEB4_OPERATOR_HANDOFF.md) and [B06 evidence](../evidence/web4/20261102-b06-operator-handoff.md) record the selected documentation-only scope and checks. No runtime/SQL/migration was run. This notice supersedes earlier B06 PLANNED selection wording; accepted B05 and earlier checkpoints below remain historical. Completion routing/archive waits for Owner acceptance under B06; same-key resend and full IIS/production remain unapproved.

**Current status — 2026-11-02:** B01/B02/B03/B04 are accepted/merged prerequisites. B05 is **DONE (bounded; Owner accepted; PR #84 merged)** after fresh reviews and exact-head CI PASS; [B05 evidence](../evidence/web4/20261102-b05-recovery-pilot.md) records final Chromium/Edge 11/11 browser cases each, independent physical SQL oracle/cleanup PASS, focused Security/Inventory/migration SQL 110/110 and hosted SQL-free 506 HttpHost + 62 fixture rows. [Pilot runbook](WEB4_RECOVERY_PILOT_RUNBOOK.md) owns the test-only interface and commands. B06/F05 remain PLANNED. This verifies the bounded lost-ack/reload/concurrency pilot; OS process-crash/Commit, product same-key resend, attended IME/Narrator, full IIS and production remain unverified or unapproved. Historical prerequisite evidence below keeps its original candidate boundaries.

**Previous B04 checkpoint:** B01 canonical contract scenarios have SQL-free corpus evidence; B4-CI-01 is PASS (bounded B02 Windows/net48 SQL-free hosted push/PR); B03 schema/journal is a merged prerequisite through PR #82/base `8f8091575bbdab4d85c97f14d44b82030d70927d`, with historical [B03 evidence](../evidence/web4/20261101-b03-schema-journal.md) preserved. B04 HTTP/context/status is DONE (bounded; Owner accepted; PR #83 merged 2026-11-02), with actual verification and unfinished gates owned by [new B04 evidence](../evidence/web4/20261102-b04-http-operations.md). B05 recovery/browser and B06 closeout remain PLANNED / NOT RUN; B04 runtime/test candidate `bd871518fd162927d938babd5535a2ddf91c2ad5` has 604 local passing rows, 0 failed/skipped, as recorded in its evidence. Hosted Windows CI is PASS at tested head `b225bee1370abac77d4e852302ca45572cd8d3be` ([run 41363414852](https://github.com/chung6a8m/MyDmsVn/actions/runs/41363414852)); merge `59ca21d16f7839c6d7c553395a367e465591652b` has the same tree. Owner accepted and merged B04 on 2026-11-02; B05 browser acceptance and production approval remain pending or unapproved. [B01 execution evidence](../evidence/web4/20261101-b01-contract.md) records actual counts; B4-CON-04 scenario vectors alone do not prove delayed-request serialization; actual B03/B04 SQL proof and remaining crash/browser limits belong to their task evidence. [B02 CI evidence](../evidence/web4/20261101-b02-windows-ci.md) owns exact runs/counts, not SQL/browser acceptance. **Owner:** [B01–B06 backend plan](../plans/20261101-003-web4-reliability-server.md). **Contract:** [operation/journal design](../design/WEB4_IDEMPOTENCY_JOURNAL_CONTRACT.md). The frontend-specific matrix is owned in ReactWebUI; these IDs are scenario identifiers, NOT discovered test names. Use [sole backend test policy](../TEST_POLICY.md) and [test owner commands](../../tests/README.md).

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

