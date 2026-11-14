# WEB6 B05 Paired Candidate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hardening audit/limiter và tạo một backend/frontend IIS Express/SQL candidate được pin, có browser/cutover/workload/recovery evidence cho WEB6 manual same-key resend.

**Architecture:** B05a sửa audit admission theo quyết định Owner, cung cấp protected file sink, characterise limiter một worker và đóng băng manifest/preflight trước live allocation. B05b mở rộng owned IIS Express/SQL harness hiện có để build đúng frontend candidate, chạy Chromium/Edge, oracle sáu lệnh, cutover, workload và restore; mọi tài nguyên đều create-new, pin identity, drain rồi mới cleanup. Frontend F04 plan sở hữu compiled ID và Playwright UI assertions.

**Tech Stack:** C# 12, .NET Framework 4.8, ASP.NET Web API 2/OWIN, xUnit 2, Microsoft.Data.SqlClient 7.0.2, SQL Server schema13, IIS Express, PowerShell 7, Node 22/npm 11, Playwright 1.64.

**Spec:** `docs/superpowers/specs/2026-11-10-web6-b05-paired-candidate-design.md`

## Global Constraints

- B03 waiver chỉ bỏ dependency block cho candidate bounded; không đổi 342/344 FAIL, hosted SQL NOT RUN hoặc retained gaps thành PASS.
- Không full IIS, remote host, reverse proxy, Docker/VM provisioning, multiworker claim hoặc production/staging enablement.
- Không automatic mutation retry, fresh same-key ID, runtime admin endpoint, 404→NOT_EXECUTED hoặc broad SQL grant.
- Chỉ dùng fresh owned database/private root; historical quarantine và probe directories không được inspect/adopt/cleanup.
- Candidate phải chạy `VerifyHost -> Admit` trước traffic; source SHA và published artifact hash là hai bằng chứng riêng.
- Live SQL/IIS/browser chạy tuần tự, finite timeout; ownership/quiescence không chắc chắn thì quarantine và dừng destructive action.

## Review Focus

- Audit sink lỗi trước successful authentication/authorized critical handler phải fail closed, còn denied path giữ denied — Task 1 có Application/HttpHost tests.
- Audit lỗi sau business commit phải giữ COMMITTED nhưng đánh dấu degraded/readiness FAIL — Task 1 có Sales/Receipt post tests.
- Worker thứ hai hoặc limiter state không xác định phải chặn candidate admission — Task 2 có concurrency/capacity/preflight tests.
- Not-observed và committed-response-lost phải được tạo ở đúng cut point, không nhờ runtime fault endpoint — Task 3 có browser + SQL oracle tests.
- Restore/cutover không được dùng exact12 binary với schema13 hoặc cleanup khi identity/quiescence sai — Tasks 4 và 6 có negative lifecycle tests.

---

### Task 1: Audit admission, protected sink và degraded readiness (B05a)

**Files:**
- Modify: `src/MyDmsVn.Server.Application/Identity/SecurityAudit.cs`
- Modify: `src/MyDmsVn.Server.Application/Security/AuthorizationBehavior.cs`
- Create: `src/MyDmsVn.Server.HttpHost/ProtectedFileSecurityAuditSink.cs`
- Create: `src/MyDmsVn.Server.HttpHost/SecurityAuditReadiness.cs`
- Modify: `src/MyDmsVn.Server.HttpHost/Startup.cs`
- Modify: `src/MyDmsVn.Server.HttpHost/WebHostOptions.cs`
- Modify: `tests/MyDmsVn.Server.Application.Tests/AuthenticationTests.cs`
- Modify: `tests/MyDmsVn.Server.Application.Tests/GoodsReceiptApplicationTests.cs`
- Modify: `tests/MyDmsVn.Server.Application.Tests/SalesReceiptApplicationTests.cs`
- Modify: `tests/MyDmsVn.Server.HttpHost.Tests/AuditTests.cs`
- Modify: `tests/MyDmsVn.Server.HttpHost.Tests/ConfigurationTests.cs`

**Interfaces:**
- Adds: `SecurityAuditOutcome.Admitted` cho authorized critical request trước handler; post-commit `Succeeded` vẫn riêng.
- Adds: `SecurityAuditUnavailableException` chỉ thoát ra khi pre-admission `Succeeded/Admitted` không persist; `Denied/Failed/Disabled/RehashFailed` giữ original denial/failure.
- Adds: `ISecurityAuditReadiness` với snapshot `Healthy | Degraded`, correlation-safe reason code và last failure time; không chứa exception text/path/payload.
- Adds: `ProtectedFileSecurityAuditSink` ghi một fixed-field JSON line/event bằng append + flush; path chỉ từ validated host option và candidate root.
- Config: candidate mode yêu cầu protected audit path; ordinary noncandidate host giữ explicit existing TraceSource behavior.

- [ ] **Step 1: Viết RED policy tests**

Test names/assertions:

- `successful_login_fails_closed_when_required_audit_cannot_persist` — không trả user/token/session admission.
- `invalid_login_remains_invalid_when_audit_sink_fails` — vẫn unauthorized, readiness degraded.
- `authorized_audited_request_does_not_enter_handler_when_admission_audit_fails` — handler call count 0.
- `denied_authorization_remains_denied_when_audit_sink_fails`.
- `committed_sales_and_receipt_post_preserve_success_when_audit_fails_but_readiness_degrades` — SQL/UoW commit count 1, response success, degraded true.
- `protected_file_sink_writes_only_allowlisted_fields_and_surfaces_acl_io_failure`.

- [ ] **Step 2: Chạy RED**

```powershell
dotnet test tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj -c Release -f net48 --filter 'FullyQualifiedName~AuthenticationTests|FullyQualifiedName~SalesReceiptApplicationTests|FullyQualifiedName~GoodsReceiptApplicationTests'
dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --filter 'FullyQualifiedName~AuditTests|FullyQualifiedName~ConfigurationTests'
```

Expected: FAIL đúng vì writer hiện swallow mọi sink exception và chưa có sink/readiness/config mới.

- [ ] **Step 3: Implement policy tối thiểu**

Enrich correlation trước write. Khi sink lỗi: luôn report + mark degraded; rethrow sanitized `SecurityAuditUnavailableException` chỉ cho pre-admission positive outcome; negative outcome giữ denial; `businessOperationCommitted=true` không throw. `AuthorizationBehavior` ghi `Admitted` chỉ cho `ISecurityAuditedRequest` sau permission check nhưng trước bind/handler. Protected sink từ chối relative/reparse/unowned parent, serialize fixed allowlist, flush mỗi event và không log raw exception/path.

- [ ] **Step 4: Chạy GREEN và complete affected projects**

Run lại Step 2, sau đó complete Application.Tests và HttpHost.Tests. Expected: nonzero discovery, all PASS, zero skipped; record exact counts.

- [ ] **Step 5: Commit**

```powershell
git add src/MyDmsVn.Server.Application/Identity/SecurityAudit.cs src/MyDmsVn.Server.Application/Security/AuthorizationBehavior.cs src/MyDmsVn.Server.HttpHost/ProtectedFileSecurityAuditSink.cs src/MyDmsVn.Server.HttpHost/SecurityAuditReadiness.cs src/MyDmsVn.Server.HttpHost/Startup.cs src/MyDmsVn.Server.HttpHost/WebHostOptions.cs tests/MyDmsVn.Server.Application.Tests/AuthenticationTests.cs tests/MyDmsVn.Server.Application.Tests/SalesReceiptApplicationTests.cs tests/MyDmsVn.Server.Application.Tests/GoodsReceiptApplicationTests.cs tests/MyDmsVn.Server.HttpHost.Tests/AuditTests.cs tests/MyDmsVn.Server.HttpHost.Tests/ConfigurationTests.cs
git commit -m "feat(web6): fail closed on audit admission"
```

### Task 2: Single-worker limiter gate và sealed candidate manifest (B05a)

**Files:**
- Modify: `tests/MyDmsVn.Server.HttpHost.Tests/AuthEndpointTests.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateManifest.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateSafetyTests.cs`
- Create: `docs/development/WEB6_CANDIDATE_MANIFEST_TEMPLATE.md`

**Interfaces:**
- Produces: `Web6CandidateManifest.CreateNew(root, inputs)`, `SealArtifacts(...)`, `VerifyBeforeAdmission()`, `RecordDisposition(...)`; manifest immutable sau seal và chỉ chứa sanitized hashes/IDs.
- Manifest pins: backend/frontend SHA+tree, lock/contract hashes, backend DLL/Web.config hash, frontend file-by-file dist root hash, schema13 fingerprint, candidate ID, IIS config/cert/origin, SQL ownership receipt hash, browsers/selectors/budgets.
- Preflight requires one expected IIS worker/controller/queue; second worker, dirty tree, artifact drift, missing audit destination hoặc unknown ownership returns deny before traffic.

- [ ] **Step 1: Viết RED limiter/manifest tests**

Limiter tests phủ subject/IP limits, window boundary, 4096-capacity, parallel callers và independent subjects; manifest tests phủ create-new, duplicate/tamper/extra field/reparse/ACL drift, mismatched source/artifact, second worker, missing audit file và exact-owner-only disposition.

- [ ] **Step 2: Chạy RED**

```powershell
dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --filter 'FullyQualifiedName~AuthEndpointTests'
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --filter 'FullyQualifiedName~Web6CandidateSafetyTests'
```

Expected: new manifest tests FAIL vì interface chưa tồn tại; limiter characterization phải nêu mọi behavior hiện có trước khi sửa.

- [ ] **Step 3: Implement manifest/preflight tối thiểu**

Reuse `SqlTestOwnershipManifest` safety conventions nhưng không trộn SQL allocation record với candidate manifest. Dùng create-new, canonical private root, no reparse, flush/readback, SHA-256, exact allowed children và explicit lifecycle states. Không thêm shared/distributed limiter; chỉ fail candidate nếu không chứng minh một worker.

- [ ] **Step 4: Chạy GREEN**

Run lại Step 2. Expected all PASS; không SQL allocation cho pure manifest tests.

- [ ] **Step 5: Commit**

```powershell
git add tests/MyDmsVn.Server.HttpHost.Tests/AuthEndpointTests.cs tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateManifest.cs tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateSafetyTests.cs docs/development/WEB6_CANDIDATE_MANIFEST_TEMPLATE.md
git commit -m "test(web6): gate the paired candidate manifest"
```

### Task 3: Owned IIS Express/SQL + Chromium/Edge WEB6 candidate (B05b/F04b bridge)

**Files:**
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebHostIisExpressTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebHostIisExpressTests.Recovery.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebRecoveryPilotControl.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebRecoveryPilotResultTests.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateOracle.cs`
- Consume frontend: `playwright.pilot.config.ts`, `tests/pilot/web6-manual-resend-live.spec.ts`

**Interfaces:**
- Adds opt-in test `Web6_paired_candidate_runs_six_manual_resends_in_real_browsers_and_SQL` guarded by explicit frontend checkout, owned SQL and candidate root settings.
- Producer emits private manifest `version=2`, `mode=web6-candidate`, exact candidate ID and artifact hashes; starts host with `web5`, eligibility `receipt-v1`, matching candidate ID and protected audit path.
- Oracle validates six operation identities, journal state/result bytes, document/header/lines, Posted, stock/ledger/valuation/COGS, audit readiness and exactly one business effect.

- [ ] **Step 1: Viết RED result/oracle/fixture tests**

Add pure result validator cases for missing/duplicate six kinds, dispatch count !=1, wrong candidate/hash, extra secret fields, browser count skip/retry, audit degraded và cleanup ambiguity. Add owned-SQL oracle test using a controlled completed six-kind fixture.

- [ ] **Step 2: Chạy RED focused**

Run pure validators first; then owned SQL selection only with approved env. Expected FAIL vì WEB6 mode/producer/oracle chưa tồn tại. Missing SQL/frontend is BLOCKED, never skip-to-PASS.

- [ ] **Step 3: Extend candidate fixture**

Build backend Release và frontend candidate artifact with exact ID; hash before copy and re-hash published root. Create protected audit destination. Allocate/migrate schema13 and separate runtime/operator identities. Start IIS Express, run PR #95 admission, then browser. Fault injection remains Playwright-only: route abort before fetch for absent; route fetch+abort for COMMITTED lost. Never add HTTP control endpoint.

- [ ] **Step 4: Run Chromium GREEN**

Run exact fully-qualified candidate test configured for Chromium. Expected nonzero browser discovery, all mandatory rows PASS, audit healthy, one worker, six one-effect oracles, process/queue drain, owned cleanup verified.

- [ ] **Step 5: Run Edge GREEN trên identical pair**

Repeat with Edge without rebuilding source/artifacts. Expected identical manifest hashes and oracle properties, zero skip/retry.

- [ ] **Step 6: Commit**

```powershell
git add tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebHostIisExpressTests.cs tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebHostIisExpressTests.Recovery.cs tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebRecoveryPilotControl.cs tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebRecoveryPilotResultTests.cs tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateOracle.cs
git commit -m "test(web6): run the paired resend candidate"
```

### Task 4: Pinned old/new cutover matrix (B05b/F04a)

**Files:**
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateCutoverTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateManifest.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebHostIisExpressTests.cs`

**Interfaces:**
- Consumes exact archive trees: frontend `ea104582a5aa4bcdf0d04999296a30a3c43e450e`, backend `a3f3db16381a38bdf0b76e8ab36cb55bec02bbd2`.
- Uses separate exact12 database/host for old backend; current schema13 database never receives exact12 binary.
- Produces cutover manifest handles cho frontend Task 4 và physical admission/oracle disposition per pair.

- [ ] **Step 1: Viết RED lifecycle tests**

Cover wrong historical SHA/tree, old binary/schema13 refusal, dirty archive worktree, artifact hash drift, second listener, same-epoch restart và restored-epoch stale admission.

- [ ] **Step 2: Chạy RED pure preflight**

Expected FAIL vì cutover coordinator chưa tồn tại; không allocate SQL trong pure tests.

- [ ] **Step 3: Implement cutover coordinator**

Build archives in isolated worktrees, pin hashes, create separate compatible DBs, admit one host at a time, drain before switching. Current frontend→old host must see v3 absent/OFF; cached WEB4→new compatible host uses v1 only. Code deploy không rotate epoch; restore/rewind mới rotate.

- [ ] **Step 4: Chạy current/old Chromium và Edge matrix**

Run serially through frontend F04 Task 4. Expected all mandatory markers PASS, no schema downgrade, no mutation from lifecycle, exact cleanup disposition.

- [ ] **Step 5: Commit**

```powershell
git add tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateCutoverTests.cs tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateManifest.cs tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebHostIisExpressTests.cs
git commit -m "test(web6): verify compatible candidate cutover"
```

### Task 5: Bounded workload metrics (B05b)

**Files:**
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateMetrics.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateWorkloadTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateManifest.cs`

**Interfaces:**
- Produces: per-class `{ count, p50Ms, p95Ms, p99Ms, errors, timeouts, throughputPerSecond }` bằng monotonic duration + nearest-rank.
- Critical classes: `Sales.Create`, `Sales.Update`, `Sales.Post`, `Receipt.Create`, `Receipt.Update`, `Receipt.Post`; supporting classes auth refresh, protected status/eligibility, stock read tách riêng.
- Profiles: smoke, rồi concurrency 1/4/8/16; one admitted worker only; desired 1,000 observations/class trong bounded budget.

- [ ] **Step 1: Viết RED metrics tests**

Hand-derived literal samples pin nearest-rank p50/p95/p99, class isolation, error/timeout accounting, insufficient-sample label, clock monotonicity và no pooling. Mutation check: wrong percentile/class must fail test.

- [ ] **Step 2: Chạy RED/GREEN metrics helper**

Implement helper tối thiểu sau RED; run pure tests until PASS.

- [ ] **Step 3: Viết RED workload lifecycle test**

Assert workload refuses unadmitted host, >1 worker, audit degraded, dirty artifact, missing actor cohort hoặc unknown SQL ownership; stop on duplicate effect/deadlock/resource runaway.

- [ ] **Step 4: Implement và chạy smoke**

Reuse admitted candidate HTTP clients and independent SQL snapshots. Smoke verifies all classes and teardown before measurement.

- [ ] **Step 5: Chạy measurement profiles**

Run profiles serially. Record actual sample count, p50/p95/p99, error/timeout, CPU/RAM, SQL lock/deadlock/pool observation and journal bytes. Nếu class không đạt đủ mẫu trong budget, gắn `INSUFFICIENT_SAMPLE / NOT MEASURED`; không rerun chỉ để làm đẹp số.

- [ ] **Step 6: Commit**

```powershell
git add tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateMetrics.cs tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateWorkloadTests.cs tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateManifest.cs
git commit -m "test(web6): measure the paired candidate"
```

### Task 6: Candidate-specific backup/restore và service RPO/RTO (B05b)

**Files:**
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateRecoveryTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateOracle.cs`
- Reuse: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6MaintenanceRecoveryFixture.cs`
- Reuse: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/SqlTestDatabase.cs` (`SqlTestOwnershipManifest` is linked into the security test owner)

**Interfaces:**
- Produces service timeline: failure detected, backup complete, restore complete, host admitted, authenticated health, fresh write accepted; RTO từ detection→trusted service, RPO từ independent business/journal comparison.
- Restores into different fresh owned DB, rotates epoch before writer admission, reprovisions restricted identities, rejects old writer/receipt/status.

- [ ] **Step 1: Viết RED recovery lifecycle tests**

Cover same-database restore refusal, no epoch rotation, old principal retained, backup/manifest mismatch, host admission before restore oracle, stale writer accepted, uncertain process/connection drain và cleanup identity mismatch.

- [ ] **Step 2: Chạy RED focused**

Run pure safety cases first; owned SQL case only with explicit env. Expected missing coordinator FAIL/BLOCKED.

- [ ] **Step 3: Implement restore rehearsal**

Use new owned allocation + backup artifact in private root; verify exact source ownership before backup and destination ownership after restore. Rotate epoch only on restored clone. Start pinned host only after schema/principal/oracle checks. Do not reuse B03 failed candidate or its quarantines.

- [ ] **Step 4: Chạy GREEN service recovery**

Interrupt admitted host, perform restore, reject old epoch/receipt, authenticate, execute fresh current-epoch command, run full journal/document/stock/ledger/value/COGS oracle. Record separate backup, restore, detection, RTO/RPO observations.

- [ ] **Step 5: Commit**

```powershell
git add tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateRecoveryTests.cs tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateOracle.cs
git commit -m "test(web6): rehearse candidate service recovery"
```

### Task 7: Full affected gates, evidence và B05 handoff

**Files:**
- Create: `docs/evidence/web6/20261110-b05a-operations.md`
- Create: `docs/evidence/web6/20261110-b05b-paired-candidate.md`
- Modify: `docs/evidence/web6/README.md`
- Modify: `docs/plans/20261103-001-web6-recovery-quality-backend.md`
- Modify: `docs/development/WEB6_BACKEND_ACCEPTANCE.md`
- Modify: `docs/development/WEB6_CANDIDATE_RUNBOOK.md`

**Interfaces:**
- Consumes exact source/artifact hashes, TRX/count JSON, SQL/browser/cutover/workload/recovery results và frontend F04 evidence.
- Produces honest B6-PAIR/SEC/LOAD/REC dispositions; manual resend ordinary OFF, staging NOT ACCEPTED, production NO-GO.

- [ ] **Step 1: Locked restore và complete affected SQL-free gates**

Run complete Application.Tests và HttpHost.Tests Release/net48. Run Security.Tests pure candidate/safety selections with nonzero discovery. Record exact counts; no historical totals.

- [ ] **Step 2: Run owned SQL/IIS/browser selections tuần tự**

Run Tasks 3–6 exact selectors, never in parallel. Require explicit SQL/frontend/candidate-root env, zero unexplained skip/retry, process/queue/connection drain và exact cleanup/quarantine record.

- [ ] **Step 3: Reconcile frontend/backend evidence**

Verify frontend SHA/tree/dist hash/candidate ID/browser counts equal backend manifest. Verify six classes, one effect, cutover pins, workload sample labels, audit health và recovery timeline.

- [ ] **Step 4: Ghi evidence từ kết quả thật**

Mỗi failed attempt/rerun/resource disposition được giữ. Full IIS/Narrator NOT RUN/Owner exception; proxy N/A chỉ cho direct topology; multiworker NOT ACCEPTED. Không tuyên bố pilot enable nếu chưa có quyết định Owner riêng.

- [ ] **Step 5: Hygiene và frozen-boundary checks**

Run `git diff --check`, link/UTF-8 checks, contract hash recount, verify WEB4/WEB5 archives/corpora unchanged, CEX untouched, no secret/raw payload in Git.

- [ ] **Step 6: Commit**

```powershell
git add docs/evidence/web6 docs/plans/20261103-001-web6-recovery-quality-backend.md docs/development/WEB6_BACKEND_ACCEPTANCE.md docs/development/WEB6_CANDIDATE_RUNBOOK.md
git commit -m "docs(web6): record B05 paired candidate evidence"
```

- [ ] **Step 7: Independent review**

Review audit semantics, host/SQL ownership, candidate pinning, no-auto-retry, metrics math, recovery oracle, cleanup và evidence accuracy. P0/P1/P2 behavioral fixes bắt đầu bằng RED tests và lặp affected gates.

