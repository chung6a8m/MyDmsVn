# WEB6 Nonproduction Candidate Bootstrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and run a controlled nonproduction WEB6 candidate whose ordinary artifact keeps manual same-key resend OFF and whose pair, cutover, workload and recovery live selectors all PASS on one pinned frontend/backend candidate.

**Architecture:** A new backend-owned net48 command-line tool creates a separately authorized, protected bootstrap request; prepares immutable frontend/backend artifacts without live allocation; and invokes the existing live selectors serially with minimal per-process environments. The React repository supplies deterministic ordinary/candidate artifact manifests, while the backend selectors emit protected transition receipts that the orchestrator chains without weakening their existing fail-closed gates.

**Tech Stack:** C# / net48, Newtonsoft.Json, Microsoft.Data.SqlClient, xUnit, Windows ACL/file identity, Git worktrees, dotnet test/TRX, Node 22/npm 11, Vite 8, TypeScript/Vitest, Playwright Chromium/Edge, IIS Express, SQL Server schema12/schema13.

**Spec:** `docs/superpowers/specs/2026-11-10-web6-nonproduction-candidate-bootstrap-design.md`

Backend paths below are relative to the MyDmsVn checkout. Paths labeled **Frontend** are relative to the companion MyDmsVn.ReactWebUI checkout; the two repositories keep separate commits and PRs.

## Global Constraints

- Ordinary frontend builds omit `VITE_MYDMSVN_WEB6_CANDIDATE_ID`; manual same-key resend remains OFF.
- Candidate manual resend requires one canonical candidate ID shared by the frontend artifact, backend host and all protected receipts.
- Preserve no automatic mutation retry, no new operation GUID, no retry queue and no missing-status reinterpretation.
- Keep allocator, runtime, operator and maintenance SQL identities distinct; do not widen runtime/operator grants.
- Never install Windows features, certificates, browsers, Node, SQL Server, Docker or a VM automatically.
- Never adopt, mutate or clean historical quarantines or retained failed-attempt resources.
- Run pair, cutover, workload and recovery serially; stop after the first blocked/failed gate and never retry a destructive selector automatically.
- Only exact owned resources named by the current run receipt may be cleaned after process, queue and SQL quiescence proofs.
- A successful bootstrap is nonproduction evidence only; staging remains NOT ACCEPTED and production remains NO-GO.
- The completion target is all four selector families PASS with `0 fail / 0 mandatory skip`; workload `INSUFFICIENT_SAMPLE / NOT MEASURED` is incomplete.

## Review Focus

- A request attempts to widen candidate ID, root, commit, port or drill class beyond its Owner authority: reject before build/allocation (Task 2 tests).
- A path is a junction/symlink, changes identity, escapes the authorized parent or overlaps a known quarantine: reject before allocation and retain the current attempt record (Task 3 tests).
- A child runner times out, emits malformed TRX, skips a mandatory row or leaks a secret: stop once, redact output and produce no next-state receipt (Task 4 tests).
- Pair/cutover receipts come from different candidates/artifacts or are replayed/out of order: refuse Task 4 acceptance and do not start workload/recovery (Task 5 tests).
- Cleanup loses exact process/queue/SQL ownership or quiescence: transition to `FailedRetained`, preserve the new resources and perform no broader deletion (Task 5 tests).

---

### Task 1: Deterministic frontend ordinary/candidate artifact proof

**Files:**
- Create (Frontend): `scripts/web6-candidate-artifact.mjs`
- Create (Frontend): `tests/unit/http/web6CandidateArtifact.test.ts`
- Modify (Frontend): `package.json`
- Modify (Frontend): `tests/unit/http/web6CandidateBuildConfig.test.ts`

**Interfaces:**
- Consumes: `VITE_MYDMSVN_WEB6_CANDIDATE_ID` and an existing Vite `dist` directory.
- Produces: `npm run web6:artifact -- --dist <absolute-path> --mode <ordinary|candidate> [--candidate-id <id>] --output <path>`; canonical JSON `{ version, mode, candidateId, files, manifestSha256 }`, with sorted relative file paths and lowercase SHA-256 values. `manifestSha256` hashes the canonical `{ version, mode, candidateId, files }` payload, avoiding a self-referential file hash.

- [ ] **Step 1: Write failing unit tests**

Add `ordinary artifact reports candidateId null`, `candidate artifact requires and records the canonical ID`, `manifest is stable across enumeration order`, `path escape/symlink and unknown arguments are refused`, and `candidate build config accepts the boundary 1/64-character IDs but rejects slash, whitespace, leading punctuation and 65 characters`.

- [ ] **Step 2: Run the focused tests and verify RED**

Run: `npm run test:unit -- tests/unit/http/web6CandidateArtifact.test.ts tests/unit/http/web6CandidateBuildConfig.test.ts`

Expected: FAIL because `web6-candidate-artifact.mjs` and the script entry do not exist.

- [ ] **Step 3: Implement the artifact manifest command**

Use Node built-ins only. Resolve and pin the opened `dist` directory, enumerate regular files without following reparse/symbolic links, hash bytes, sort by `/`-normalized relative path, and atomically create a new output file. For candidate mode require the canonical ID and prove at least one emitted JS asset contains that exact build-time value; for ordinary mode reject an explicitly supplied ID. The backend prepare gate builds ordinary assets with the environment variable removed, so `candidateId: null` cannot be supplied by an inherited environment.

- [ ] **Step 4: Run frontend focused and full gates**

Run: `npm run typecheck` and `npm run test:unit`.

Expected: typecheck PASS; all Vitest files/tests PASS.

- [ ] **Step 5: Commit the frontend task**

Commit: `feat(web6): add deterministic candidate artifact proof`

### Task 2: Protected Owner authority and strict bootstrap request CLI

**Files:**
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/MyDmsVn.Web6.CandidateBootstrap.csproj`
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/Program.cs`
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/BootstrapCommand.cs`
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/BootstrapAuthority.cs`
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/BootstrapRequest.cs`
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/ProtectedJsonFile.cs`
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/Properties/AssemblyInfo.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateBootstrapAuthorityTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj`
- Modify: `MyDmsVn.sln`

**Interfaces:**
- Consumes: explicit `authorize` arguments and a later strict request JSON.
- Produces: `BootstrapCommand.RunAsync(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken token)`; `BootstrapAuthority.ReadProtected(string path, string sha256, DateTimeOffset now)`; `BootstrapRequest.ReadStrict(string path, BootstrapAuthority authority)`.

- [ ] **Step 1: Write failing authority/request tests**

Cover exact-key JSON parsing; duplicate/unknown/missing fields; canonical candidate IDs; SHA/commit formats; UTC expiry; loopback HTTPS ports; exact source commits; allowed drill set `{pair,cutover,workload,recovery}`; absolute distinct roots; request widening; changed ACL/content/file identity; wrong digest; and the explicit acknowledgement phrase `I AUTHORIZE WEB6 NONPRODUCTION DESTRUCTIVE DRILLS`.

Pin the authority keys to `version`, `candidateId`, `taskParent`, `sqlControlParent`, `origins`, `drillClasses`, `backendSha`, `frontendSha`, `oldBackendSha`, `oldFrontendSha`, and `expiresAtUtc`. Pin the request keys to `version`, `candidateId`, `backendRepository`, `frontendRepository`, `taskRoot`, `sqlControlParent`, `sqlApprovalFile`, `tlsThumbprint`, `nodePath`, `iisExpressPath`, `browserProjects`, `actorCohort`, `workloadSeconds`, `recoveryAuthorized`, `authorityPath`, and `authoritySha256`.

- [ ] **Step 2: Run the focused backend tests and verify RED**

Run: `dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj --filter FullyQualifiedName~Web6CandidateBootstrapAuthorityTests --no-restore`

Expected: FAIL because the bootstrap types/project do not exist.

- [ ] **Step 3: Implement the net48 CLI and protected models**

Support only:

- `authorize --input <strict-json> --output <new-file> --acknowledge <exact-phrase>`
- `prepare --request <strict-json> --authority <path> --authority-sha256 <64-lowercase-hex>`
- `run --prepared <path> --prepared-sha256 <64-lowercase-hex>`

`authorize` writes canonical UTF-8 without BOM using CreateNew, a protected current-user-only ACL and write-through, then prints only `status=AUTHORIZED sha256=<digest>`. It must not build, allocate or run tests. Return stable exit codes for usage, refused authority, blocked preflight, failed retained run and success.

- [ ] **Step 4: Run focused tests and CLI help smoke**

Run the focused test command, then `dotnet run --project tools/MyDmsVn.Web6.CandidateBootstrap/MyDmsVn.Web6.CandidateBootstrap.csproj -- --help`.

Expected: focused tests PASS; help exits 0 and contains no environment values or private paths.

- [ ] **Step 5: Commit the backend task**

Commit: `feat(web6): add protected candidate bootstrap authority`

### Task 3: Zero-allocation preflight and immutable artifact preparation

**Files:**
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/BootstrapPathPolicy.cs`
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/BootstrapToolchain.cs`
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/BootstrapGitWorktree.cs`
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/BootstrapArtifactBuilder.cs`
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/BootstrapPreflight.cs`
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/PreparedCandidateReceipt.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateBootstrapPreflightTests.cs`
- Modify: `tools/MyDmsVn.Web6.CandidateBootstrap/BootstrapCommand.cs`

**Interfaces:**
- Consumes: `BootstrapRequest`, `BootstrapAuthority`, process/file/certificate/SQL-preflight adapters.
- Produces: `BootstrapPreflight.PrepareAsync(BootstrapRequest request, CancellationToken token) -> PreparedCandidateReceipt`; a protected canonical receipt plus SHA-256, and no SQL database/HTTP queue/browser allocation.

- [ ] **Step 1: Write failing preflight tests**

Use fake adapters to cover dirty/wrong Git commits; unauthorized/overlapping roots; junction/symlink and file-identity swaps; deny-listed quarantine roots; missing/ambiguous IIS Express, Node, Playwright browser or certificate; certificate without private key; SQL connection not targeting effective master; expired SQL approval; build nonzero/timeout; ordinary artifact unexpectedly carrying an ID; candidate ID mismatch; publish/dist hash drift; and success with zero allocator/host/browser calls.

- [ ] **Step 2: Run focused tests and verify RED**

Run: `dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj --filter FullyQualifiedName~Web6CandidateBootstrapPreflightTests --no-restore`

Expected: FAIL because preflight types are absent.

- [ ] **Step 3: Implement path, toolchain and detached-worktree validation**

Create only new children under the authorized parent. Use argument-list-safe child processes for `git worktree add --detach`, `git status --porcelain`, restore/build and frontend artifact commands. Validate existing tools/certificate/SQL approval without installing or allocating. Pin opened directory/file identities through measurement and reject reparse points.

- [ ] **Step 4: Implement ordinary and candidate artifact preparation**

Build ordinary frontend with the candidate variable removed from the child environment and candidate frontend with the exact authorized ID. Build Release backend, copy candidate `dist` into the private publish `wwwroot`, measure full trees before/after, and emit the existing exact cutover build-authority JSON fields plus the broader prepared receipt. Do not use mutable documentation worktrees as candidate sources.

- [ ] **Step 5: Run focused tests and a no-allocation prepare fixture**

Run the focused test command and a fixture-backed `prepare` whose adapters use temporary Git repositories and fake tool binaries.

Expected: PASS; no SQL database, IIS Express process, HTTP queue or browser process appears.

- [ ] **Step 6: Commit the backend task**

Commit: `feat(web6): prepare immutable candidate artifacts`

### Task 4: Protected run state and bounded selector process runner

**Files:**
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/BootstrapRunState.cs`
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/SelectorProcessRunner.cs`
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/SelectorResult.cs`
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/RunTransitionReceipt.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateBootstrapRunnerTests.cs`

**Interfaces:**
- Consumes: `PreparedCandidateReceipt` and `SelectorInvocation { Name, Filter, Environment, Timeout, TrxPath }`.
- Produces: `SelectorProcessRunner.RunAsync(SelectorInvocation, CancellationToken) -> SelectorResult`; `ProtectedGateReceipt { Gate, CandidateId, BackendSha, FrontendSha, FrontendDistSha256, PrivatePath, Sha256 }`; `BootstrapRunState.Accept(SelectorResult, ProtectedGateReceipt) -> RunTransitionReceipt`.

- [ ] **Step 1: Write failing runner/state tests**

Cover the exact state chain `Requested -> Prepared -> PairPassed -> CutoverPassed -> WorkloadRecorded -> RecoveryRecorded -> CandidateVerified`; refused skipped/replayed/out-of-order transitions; candidate/hash mismatch; nonzero exit; malformed/missing TRX; discovered/executed mismatch; mandatory skip/fail; timeout with process-tree termination; output redaction for connection strings/JWT/cookies/private paths; and proof that a failed selector is invoked exactly once.

- [ ] **Step 2: Run focused tests and verify RED**

Run: `dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj --filter FullyQualifiedName~Web6CandidateBootstrapRunnerTests --no-restore`

Expected: FAIL because runner/state types are absent.

- [ ] **Step 3: Implement bounded child execution and TRX parsing**

Invoke `dotnet test` with an exact fully qualified filter, unique results directory and TRX logger. Construct a new allowlisted environment per child instead of inheriting all `MYDMSVN_*` variables. On timeout terminate the owned process tree, record uncertainty, and require later disposition proof; never launch the same selector again automatically.

- [ ] **Step 4: Implement protected transition receipts**

Each canonical receipt includes run/candidate ID, previous receipt hash, source/artifact hashes, selector/filter, discovered/executed/pass/fail/skip, UTC times, sanitized disposition and private owned-resource reference. Only `0 fail / 0 mandatory skip` can advance functional states; workload/recovery measurements remain separate fields.

- [ ] **Step 5: Run focused tests**

Expected: all runner/state tests PASS, including redaction and one-attempt assertions.

- [ ] **Step 6: Commit the backend task**

Commit: `feat(web6): add bounded candidate selector runner`

### Task 5: Exact selector adapters, acceptance receipts and verified disposition

**Files:**
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/Web6SelectorPlan.cs`
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/Web6CandidateBootstrapOrchestrator.cs`
- Create: `tools/MyDmsVn.Web6.CandidateBootstrap/BootstrapDisposition.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateBootstrapOrchestratorTests.cs`
- Modify: `tools/MyDmsVn.Web6.CandidateBootstrap/BootstrapCommand.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebHostIisExpressTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebHostIisExpressTests.Cutover.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebHostIisExpressTests.Workload.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateRecoveryTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateManifest.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateMetrics.cs`

**Interfaces:**
- Consumes: `PreparedCandidateReceipt`, existing selector gates and exact protected gate outputs.
- Produces: `Web6SelectorPlan.Create(PreparedCandidateReceipt)`, `Web6CandidateBootstrapOrchestrator.RunAsync(...) -> RunTransitionReceipt`; protected pair-browser, cutover/Task4, workload and recovery gate receipts.

- [ ] **Step 1: Write failing orchestration tests**

Assert exact order: pair Chromium, pair Edge, cutover, workload, recovery. Assert minimal environment maps contain every current required variable and no unrelated secret. Cover mismatched browser pair receipts, stale/different cutover receipt, Task4 creation before both pair browsers/cutover, workload insufficient sample, recovery missing RPO/RTO, first-gate stop, and `FailedRetained` when process/queue/SQL cleanup proof is incomplete.

- [ ] **Step 2: Run focused tests and verify RED**

Run: `dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj --filter FullyQualifiedName~Web6CandidateBootstrapOrchestratorTests --no-restore`

Expected: FAIL because selector planning/orchestration is absent.

- [ ] **Step 3: Add selector-owned protected output contracts**

Extend each existing live selector with one required bootstrap output handle when invoked by the orchestrator. Write acceptance only after its existing SQL/browser/oracle and cleanup-preflight checks pass. Preserve standalone fail-closed behavior. Task4 receipt must retain the existing exact schema (`mode=web6-cutover`, `status=ACCEPTED`) and bind both browser receipts, cutover result, SQL ownership and candidate manifest hashes before workload/recovery can read it.

Use `MYDMSVN_WEB6_BOOTSTRAP_RUN_ID`, `MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_PATH` and `MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_SHA_PATH` as the only additive selector-output variables. The orchestrator pre-creates and pins the private parent; the selector atomically creates the output and digest files and refuses an existing target.

- [ ] **Step 4: Implement exact environment adapters and serial orchestration**

Use the current fully qualified tests:

- `WebHostIisExpressTests.Web6_paired_candidate_runs_six_manual_resends_in_real_browsers_and_SQL`
- `WebHostIisExpressTests.Web6_pinned_cutover_runs_protected_IIS_and_browser_lifecycle`
- `WebHostIisExpressTests.Web6_paired_candidate_runs_bounded_workload_profiles`
- `Web6CandidateRecoveryTests.Web6_candidate_recovery_rehearses_distinct_owned_restore_before_service_admission`

Pass opt-in variables only for the current gate. Generate fresh distinct owned roots and SQL allocations per selector as required; never reuse a failed attempt. Require workload 1/4/8/16 profiles and at least 1,000 observations for each critical operation class, then require observed recovery RPO/RTO fields before `CandidateVerified`.

- [ ] **Step 5: Implement verified cleanup/quarantine disposition**

Drain child/browser/IIS processes, prove exact queue absence, verify SQL original allocation token/database identity/quiescence/oracle, and delete only receipt-listed resources. Any uncertainty writes `FailedRetained` and stops deletion. Add an explicit deny-list assertion for historical WEB5/WEB6 quarantine records.

- [ ] **Step 6: Run focused and affected backend gates**

Run the four bootstrap-focused classes, then:

- `dotnet test tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj --no-restore`
- `dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj --no-restore`
- `dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj --filter "Category!=Web6PairedCandidate&Category!=Web6CutoverPreflight&Category!=Web6BoundedWorkload&Category!=Web6CandidateRecovery" --no-restore`

Expected: all selected non-live tests PASS; no mandatory case is silently skipped within the selected scope.

- [ ] **Step 7: Commit the backend task**

Commit: `feat(web6): orchestrate protected candidate acceptance`

### Task 6: Live candidate execution, evidence and final review

**Files:**
- Modify: `docs/development/WEB6_CANDIDATE_RUNBOOK.md`
- Modify: `docs/evidence/web6/20261110-b05b-paired-candidate.md`
- Modify (Frontend): `docs/evidence/web6/20261110-f04b-measured-quality.md`
- Modify only if status changes are justified: `docs/plans/20261103-001-web6-recovery-quality-backend.md`
- Modify only if status changes are justified (Frontend): `docs/plans/20261103-001-web6-recovery-quality-web.md`

**Interfaces:**
- Consumes: exact-head bootstrap executable, protected Owner/SQL authorities and existing machine prerequisites.
- Produces: one final `CandidateVerified` receipt and sanitized backend/frontend evidence with exact hashes and live counts.

- [ ] **Step 1: Create the private authority and request**

Use explicit Owner-approved candidate ID, roots, commits, ports, drill set and expiry. Keep files outside Git. Record only their SHA-256 and sanitized identifiers in evidence.

- [ ] **Step 2: Run `prepare` and verify zero-allocation output**

Confirm ordinary frontend artifact OFF, candidate artifact ID match, clean detached source pins, complete publish/dist hash equality, existing certificate/browser/tool pins and SQL effective-master approval. If blocked, fix only the named prerequisite and rerun with a new attempt record; do not claim completion.

- [ ] **Step 3: Run the orchestrator once for the candidate**

Expected terminal evidence:

- pair Chromium: `1 pass / 0 fail / 0 skip`;
- pair Edge: `1 pass / 0 fail / 0 skip`;
- cutover: `1 pass / 0 fail / 0 skip`;
- workload: `1 pass / 0 fail / 0 skip`, with required 1/4/8/16 measured profiles and sample floor;
- recovery: `1 pass / 0 fail / 0 skip`, with observed RPO/RTO;
- ordinary artifact manual resend: OFF;
- final state: `CandidateVerified`.

Any other outcome is intermediate and keeps this task open.

- [ ] **Step 4: Record truthful cross-repository evidence**

Record source/tree/artifact hashes, tool/browser/SQL environment, filters, discovered/executed/pass/fail/skip, workload measurements, RPO/RTO, SQL/business oracle, resource cleanup/retention and remaining full-IIS/Narrator/staging/production limits. Do not commit secrets, private paths or raw proprietary TRX.

- [ ] **Step 5: Run fresh exact-head verification**

Frontend: `npm run typecheck && npm run test:unit`.

Backend: Application, HttpHost, bootstrap-focused Security tests, then the four live selector families through the orchestrator. Run `git diff --check` in both repositories.

Expected: all requested gates PASS and both worktrees contain only intended changes.

- [ ] **Step 6: Request independent whole-branch review**

Reviewer checks the approved design, this plan, frontend/backend diffs, receipts/evidence and retained safety boundaries. Resolve all P0/P1 and justified P2 findings with focused RED→GREEN tests.

- [ ] **Step 7: Commit, push and update the two draft PRs**

Use separate backend/frontend commits, push `codex/web6-b05-operations` and `codex/web6-f04-paired-cutover`, cross-link exact heads and keep both PRs draft until review is clean. Do not merge or declare pilot enabled without the separate Owner decision referencing the verified receipt.
