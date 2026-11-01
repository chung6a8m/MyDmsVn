# Roadmap 04 backend — Reliable mutations, operation journal, and CI

**Selected by Owner:** 2026-11-01. **State:** ACTIVE ROADMAP / ALL IMPLEMENTATION TASKS PLANNED (NOT RUN). **Repository:** chung6a8m/MyDmsVn; companion frontend plan: https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/codex/activate-web4-reliability/docs/plans/20261101-002-web4-reliability-web.md. **Source baseline:** main at 8114cc92267f8eb8b59ff75f5c9ca6b9c7a37ddd. Reinspect current main and existing test owners before starting each task. This document-activation PR does not implement or verify runtime, SQL, browser, CI, or IIS.

## 0. Mission, constraints, ownership

Roadmaps WEB1–WEB3 are archived. WEB3 proved bounded Receipt → Inventory → Sales against non-production SQL through net48/IIS Express/HTTPS. WEB4 creates repeatable backend CI and a durable, queryable, transactionally correct idempotency boundary for the SIX existing Web mutation operations (Sales and Goods Receipt: Create, Update Draft, Post), then verifies failure/reload/recovery jointly with the Web client.

**Preserve:** net48, existing System.Web/Web API 2/Katana host, bearer JWT + authoritative SQL session + HttpOnly Secure Strict refresh cookie, same-origin HTTPS/CSP/CSRF/Origin, MediatR/Application authorization, explicit UoW and SQL transaction ownership, linq2db, DbUp, schema10 desktop admission, schema11 Web history, decimal/int64 string wire, receipt unique-product vs Sales duplicate/gift rules, moving-average stock ledger and frozen COGS. The independently selected CEX-A01–A05 WinForms workstream stays ACTIVE and untouched.

**Not selected:** full IIS/production acceptance, distributed rate-limiter redesign, complete operator audit retention deployment, Web payments/returns/transfers/stock adjustment, changes to Local transport or business costing, automatic failed-write retry, switching from net48, blanket dependency upgrades, new SQL test framework, hosted public SQL with secrets. The WEB2 full-IIS exception is not a WEB4 PASS.

Authoritative ownership: this plan owns B01–B06; frontend companion owns F01–F05. [Backend operation contract](../design/WEB4_IDEMPOTENCY_JOURNAL_CONTRACT.md) owns journal/wire/transaction rules, [ADR 0014](../adr/0014-web4-durable-operations.md) records the proposed design direction, [backend acceptance matrix](../development/WEB4_BACKEND_ACCEPTANCE.md) owns SQL/HTTP cases and [Codex handoff](../development/WEB4_CODEX_HANDOFF.md) owns execution order. This is the ONLY new backend task-state owner. Tests and evidence remain in [existing test policy](../TEST_POLICY.md) and [new evidence folder](../evidence/web4/README.md).

## 1. Milestone and PR map

| Milestone | Backend owning task | Frontend dependency | Gate |
|---|---|---|---|
| WEB4-01 Contract/preflight | B01 source/transaction/schema/permissions checkpoint | F01 consumes B01 v1 contract | No runtime/SQL change until atomicity design is source-proven |
| WEB4-02 CI foundation | B02 Windows net48 SQL-free checks | Existing frontend CI preserved | Real hosted PR/push CI, nonzero tests, no credentials |
| WEB4-03 Operation journal | B03 web-only schema + journal and B04 secured idempotent Web mutation/status APIs | F03 requires B04 contract | SQL atomic admission, replay, conflicts, no business duplicate effects |
| WEB4-04 Durable recovery | B04 endpoint/status handoff | F02 IndexedDB, F03 UI/operation integration | New client never automatically reissues an unknown write |
| WEB4-05 Failure pilot | B05 focused SQL/pure owners, host fixture + independent oracle | F04 browser test on production artifacts | Chromium/Edge + fresh owned SQL, no hidden failures |
| WEB4-06 Operations/closeout | B06 runbook, evidence/PR review, handoff | F05 frontend closeout | Owner acceptance of bounded scope, not production |

Sequence: activate docs PRs; B01/F01 contract; B02 may proceed independently after B01 with a separate PR; B03 then B04; F02 may develop against the frozen contract; F03 requires B04; B05 + F04 integrated acceptance; B06 + F05. NO broad branch that combines all tasks into a single unreviewable PR. Each implementation task uses a fresh branch from current main and narrow PR into main, with counterpart SHA and prerequisites recorded.

## 2. B01 — Source audit, immutable reliability contract (PLANNED)

**Read exact sources first:**
- src/MyDmsVn.Server.HttpHost/{SalesControllers.cs,InventoryControllers.cs,SalesWireInput.cs,InventoryWireInput.cs,Web2Pipeline.cs,AuthController.cs,RequestServices.cs,WebHostOptions.cs}
- src/MyDmsVn.Server.Application/{Sales/SalesReceiptFeatures.cs,Inventory/GoodsReceiptFeatures.cs,Persistence/IUnitOfWork.cs}; inspect permission and validation behaviors, result DTO construction and audit pipeline.
- src/MyDmsVn.Server.Infrastructure/Persistence/{SqlUnitOfWorkFactory.cs,ISqlExecutionContext.cs,SqlPersistenceBuilder.cs}; Transactions start INSIDE Application handlers. An HTTP-only journal wrapper CANNOT prove atomicity.
- src/MyDmsVn.Server.Infrastructure/Operations/{SchemaCompatibility.cs,SqlWebSchemaCompatibilityChecker.cs,SqlSchemaDefinitionFingerprint.cs,SqlWebRuntimePrincipalValidator.cs}; src/MyDmsVn.Server.DbMigrator and deployment/sql/Provision-MyDmsVnWebPrincipals.sql.
- docs/contracts/web2-v1 and docs/contracts/web3-v1; existing schema11 Web SQL/HTTP evidence; source status of Web session audit and login limiter.

**Deliverables:**
1. A source→handler→UoW→journal repository→HTTP result/permission map for all six mutations, including precommit DTO/rowversion, Post without version, audit behavior and previously returned HTTP statuses.
2. A narrow, explicit design for inserting request key admission + result capture inside the SAME existing command transaction. Prefer a small optional application-level Web operation context plus UoW-owned journal repository. No ambient global token/session, nested transaction, request actor from the client, or controller SQL.
3. Resolve deterministic request fingerprint construction using parsed canonical wire (method + canonical route/resource + business fields/expectedVersion + mutation kind), distinct from client editing/nonce state; publish corpus with positive/negative vectors and manifest/hashes BEFORE API runtime. Reject unknown/future fields and semantically different replay.
4. Freeze operation-key format, statuses, response/envelope, permission admission and protected status lookup, concurrent in-flight behavior, unavailable/expired result semantics and safe retention. Status absence is NOT proof of rollback.
5. Verify web migration path exact11→tentative12 and exact desktop10 remains frozen; inspect schema fingerprint and SQL role before prescribing final script name/permissions. If source disproves the proposed implementation, correct the design with explicit ADR record, not an unreviewed implementation shortcut.
6. Define candidate and test ownership, cross-repo pinned SHA, scope, compatibility sequencing and degradation when status service/IndexedDB unavailable. Preserve existing Web clients until coordinated release; don't silently allow missing key under an enabled WEB4 mode.

**Exit:** reviewed and pinned backend canonical contract + frontend F01 consumer signoff; planned B03/B04 source paths and required grants are verified; no runtime, migration, CI, SQL or browser PASS asserted from this checkpoint. No repeat Owner approval needed for in-scope technical choices; escalate only an actual change of scope/guarantees.

## 3. B02 — Windows/net48 backend CI, SQL-free (PLANNED)

**Implement:** .github/workflows/backend-web.yml (proposed). On pull_request, push to main/codex/**, workflow_dispatch; windows-2022 or pinned suitable supported Windows runner; contents:read only, pinned actions, no pull_request_target, no publish/deploy, no secrets. Honor global.json 8.0.400/latestFeature, net48 targeting pack and existing locked restore constraints. Use source-confirmed csproj/sln and test filters; do not invent class names or rely on zero discovery.

**Suggested commands (validate on real Windows toolchain):**
~~~powershell
dotnet restore src/MyDmsVn.Server.HttpHost/MyDmsVn.Server.HttpHost.csproj --locked-mode
dotnet restore tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --no-restore --logger "trx;LogFileName=web-host-ci.trx"
~~~

Add focused SQL-free Application/architecture tests when source/test ownership justifies. Assert actual TRX count >0, no mandatory skip, record runner/.NET SDK/test discovery and failure outputs; upload only sanitized, secret-free diagnostics if needed. SQL-backed tests remain operator-owned Windows non-production tests, NOT CI PASS. Frontend's hosted Web checks remain in the other repo; do not duplicate Linux/Chromium here.

**Exit:** hosted PR and push run at final code head show build + selected nonzero net48 tests PASS, no credentials or SQL allocation. If GitHub runner cannot support the target, record blocked status; never substitute Linux compile or local .NET tests for Windows hosted evidence.

## 4. B03 — Web-only schema 12 and transaction-owned journal (PLANNED)

**Prerequisite:** B01 canonical schema/atomicity design merged. Tentative migration name 012_WebOperationJournal.sql; final path/name must match inspected DbMigrator and schema-source conventions. Do NOT edit scripts 001–011 or desktop CurrentScripts. Web checker must admit exact new Web script+fingerprint and reject schema10/11 when WEB4-enabled host expects 12, too-new and partial/unmanaged variants. DbMigrator opt-in --web must have safe 11→12 admission; no automatic Desktop migration; preserve exact10 desktop behavior and refuse mixed desktop/schema12 writer claims.

Suggested physical fields, refined by B01: OperationId uniqueidentifier (GUIDv4), ActorUserId int, OperationKind/RouteScope, RequestHash binary(32), ResourceDocumentId bigint NULL, OutcomeCode and minimal replay-safe result metadata/payload, CreatedAtUtc/CompletedAtUtc; PK/unique actor+operation key and explicit checks/indexes. Choose stored payload type/size only after measuring actual authoritative Create/Update detail and required precision. No session token, password, refresh cookie, access JWT, full request raw body or uncontrolled error text. Enforce least privilege for a separate Web runtime journal access path; no DELETE to runtime, no db_owner, no desktop grant widening.

Journal claim/read/finish MUST use uow.Repository<...> bound to ISqlExecutionContext.Connection AND Transaction. Use transaction-owned lock/key serialization and unique constraint, no application-static semaphore; honor lock ordering before stock-gate/receipt header locks. A journal row indicating committed completion MUST be committed atomically with business changes, including line/ledger/balance/value and stored replay result. A rollback leaves no committed success. SQL errors or commit acknowledgement loss yield UNKNOWN on client, not a second Post. Never leave a committed processing row unless B01 separately specifies safe terminal recovery semantics.

**Tests:** web migration fresh and 11→12; partial script/history/fingerprint fail closed; principals normal and overgrant; transaction rollback/fault injection; same-key competing writes; same-key different fingerprint; schema10 desktop baseline unaffected. No broad upgrades.

## 5. B04 — Six idempotent Web mutation APIs and readback (PLANNED)

**Prerequisite:** B03 journal and B01 pinned protocol. Keep Sales/Receipt controller routes, successful JSON envelope/status/precision and existing Application validation. Extend via shared narrow Web idempotency coordinator/command metadata, not copy/paste business handling. Enforce key format and canonical fingerprint after strict wire decode; reject malformed/missing keys in WEB4-enabled mode. Legacy Web11/old client compatibility requires explicit coordinated deployment mode, not silent fallback.

Six requests: POST Sales Create; PUT Sales Draft; POST Sales Post; POST Goods Receipt Create; PUT Receipt Draft; POST Receipt Post. A key belongs to exactly one actor and one immutable request fingerprint; same actor/key+same payload returns the completed outcome without another side effect (after fresh authorization); reused key with different route/body/version returns deterministic Idempotency.KeyConflict. Concurrent duplicate requests must serialize safely across workers; no duplicate source, ledger or stock valuation. Re-check CURRENT authoritative session, account, revoked permissions and operation-owner before replay/status. Authorization must be enforced before exposing stored results; never interpret JWT grants or UI capability as authority. Read permission is not implicitly required to verify a previously authorized Write/Post acknowledgement; status returns minimal allowed info, not an unauthorized full detail.

Proposed protected query: GET /api/v1/operations/{operationId}, keyed by current authenticated actor; distinguish committed result and not-observed/unknown, with no implication that 404 means safe to retry. Strict no-cache/no-store, no query secrets or cross-actor existence leak. Response and error schema stay within canonical wire/error design. On known successful Post, status is terminal Posted without fabricating a version or forcing a Get; unknown is locked on client until positive evidence. Server must not auto retry deadlock victim, timeout, loss of response, conflict or failed writes. Revalidate existing Sales/Receipt guest/denied/outcome-unknown/409/Get403 regression.

**Exit:** reviewed HTTP unit+authorization tests and real SQL same-key/resend/conflict tests, no drift in existing WEB2/WEB3 wire fixtures, no business cost rewrite. Backend HTTP success or replay must not claim browser acceptance.

## 6. B05 — Focused SQL + integrated HTTPS browser/physical oracle (PLANNED)

**Prerequisites:** B04 released as pilot candidate and F03 UI operation integration ready. Extend existing owned Security.Tests IIS Express fixture in test-only mode, retaining Sales/Receipt/WEB3 selectors. Do not restore removed SQL runner or use prefix-based drop. Each Chromium/Edge invocation: separately owned fresh schema12 SQL DB, SID-checked restricted login, app-pool/host process tree and signed origin; fixture owns guard/cleanup; normal certificate trust, no TLS bypass. Test published React artifact, not fake Lab or Vite.

Backend own SQL oracle must independently check original source IDs, all Posted IDs (exact union), ledger/value companion, balance Q/V, frozen COGS/profit, no duplicates, no Draft movement and correct audit/actor after all failure cases. Add scoped test for:
- concurrent same-key Create/Update/Post (two connections/actors where appropriate), key/body mismatch, 409 rowversion, permission revocation, rerun after response loss;
- commit before lost HTTP acknowledgement vs rollback before commit, crash/timeout/deadlock, status absence not reissuing write;
- Receipt → Sales → backdated Receipt same key, other warehouse isolation, duplicate Sales/gift, Receipt unique products, multi-document dirty sessions, actor swap;
- journal constraint/retention/permission fail closed, cross-tab/same-user fresh session allowed only by designed policy.
- host startup exact schema12 and principal reprovision; non-op routes unaffected.

Frontend F04 owns keyboard/IndexedDB/reload DOM. Backend B05 owns SQL, fixture, physical oracle and cleanup. Count browser cases separately from .NET wrapper rows; repeated candidates are not distinct scenarios. Record failures and corrections; run final candidate Chromium and Edge with fresh independent fixtures. Full IIS and attended Telex/VNI/Narrator remain NOT RUN unless separately selected.

## 7. B06 — Runbook, handoff and closeout (PLANNED)

Write deploy/admission runbook for Web11→12, restricted principal delta, coordinated frontend/backend artifact rollout, journal outcome inspection WITHOUT secret leakage, operator-backed retention strategy (no automatic deletion/reuse of old keys without a safe tombstone/expiry protocol), backup/restore impact and recovery. Record security audit/limiter/full-IIS as explicit production prerequisites, NOT PASS. Frontend partner records IndexedDB controls and user-visible recovery.

Update docs/plans/README.md and all routing only after scope Owner acceptance. Freeze completed execution docs in archive per existing policy; preserve unchanged evidence in docs/evidence/web4 with exact task/source/runner/candidate; old WEB1–03/CEX/WinForms documents and runtime pin remain untouched.

## 8. Mandatory acceptance and stop rules

Status transitions: PLANNED → IN_PROGRESS only after source/review checkpoint; DONE only for verified approved scope; BLOCKED/NOT RUN never PASS. PR merge is not testing and docs activation is not runtime implementation. Protect security and cost invariants even if a simpler implementation would make tests green. Never widen privileges or silently change the schema contract for Desktop. After an uncertain SQL commit NEVER automatically replay the mutation. Keep every lifecycle proof on the exact candidate and prove test count is nonzero.

**Bounded completion requires:** B01 authoritative protocol; B02 hosted CI; B03 schema+atomic journal; B04 six APIs/status; B05 local real SQL + HTTPS browser oracle jointly with Web F04; B06 operational reference and Owner disposition. Release/full IIS remain NOT APPROVED regardless of completion.

