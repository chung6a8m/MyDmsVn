# WEB6 Cutover Deadline Hierarchy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a finite, cancellation-aware nonproduction WEB6 cutover control hierarchy that permits legitimate host transitions, never acknowledges early, and remains below the approved 60-minute selector ceiling.

**Architecture:** A backend-owned budget algebra validates one action-clamped request allowance inside a frontend-owned cumulative scenario deadline. Cooperative work receives linked cancellation; noncooperative synchronous edges are never abandoned and remain contained by the exact owned selector process. The cutover session journals stable/transitioning/uncertain state and admits reset only after a joined, stable handler.

**Tech Stack:** .NET Framework 4.8/C#, xUnit, Microsoft.Data.SqlClient, System.Management, named pipes, Node 22.14, TypeScript, Playwright 1.64.1, Vitest 5.0.5.

**Spec:** `docs/superpowers/specs/2026-11-13-web6-cutover-deadline-hierarchy-design.md`

## Global Constraints

- Ordinary same-key resend remains OFF; no automatic retry, replay, early success acknowledgement or production deployment.
- The cutover selector gate is 3,600 seconds; exact cutover process admissibility is 3,660 seconds for teardown only.
- Scenario totals are 420/420/690 seconds; reset reserves are 180/180/60 seconds; browser pack is 1,560 seconds; per-browser controller is 1,620 seconds; aggregate parent is 3,300 seconds.
- Action caps are host on/off and `restoreEpoch` 180 seconds, `sameEpochRestart` 210, `committedUnavailable` 60, `revokeGrantOn`/`revokeGrantOff`/`verifyNoCutoverMutation` 45, and `resetCutover` 180 further clamped by reset reserve.
- `requestAllowanceMs <= 5000` is rejected before connect/write; server work allowance is exactly `requestAllowanceMs - 5000`.
- Success remains one correlated final frame after handler and post-verification; diagnostics remain allowlisted and contain no stack, path, token, candidate/operation/deployment/SQL identity, raw payload or raw exception.
- No implementation task may run SQL, IIS, Chromium, Edge or deployment, or inspect/query/clean/adopt any retained attempt04–21 resource.
- Frontend commits precede the backend exact reviewed pin commit. Old frontend SHA must be blocked and the new exact SHA must be `PIN_MATCHED` through fresh prepare tests.
- Worktree line endings, net48 target, current dependency pins, protected SQL/runtime principal separation and retained-resource policy remain unchanged.

## Review Focus

- Delayed connect plus a partial response must consume, never refresh, the absolute request allowance and settle once without a second write — Task 5 transport tests.
- A timeout during a noncooperative WMI/commit edge must prohibit reset/later requests until join or owned-process containment — Tasks 2–4 tests.
- Cancellation after physical host launch but before admission must journal `Uncertain`, not infer the previous stable state — Tasks 2–3 tests.
- Successful third-scenario reset must preserve the restored host and intentional in-body `revokeGrantOff` evidence — Tasks 2 and 5 tests.
- Primary handler failure plus Stop/drain failure must preserve both failures while retaining allocations — Tasks 2 and 4 tests.

---

### Task 1: Backend budget algebra and strict request contract

**Files:**
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CutoverBudgets.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateManifest.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateCutoverTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidatePairBudgetTests.cs`

**Interfaces:**
- Produces: `Web6CutoverBudgets` constants and `ActionCap(string action) -> TimeSpan`.
- Produces: immutable `Web6CutoverRequest` with `Action`, `Operation`, `CommittedSetup`, `RequestAllowance`, and `WorkAllowance`.
- Produces: `Web6CutoverControl.Validate(JObject frame, string token) -> Web6CutoverRequest`.

- [ ] **Step 1: Write failing budget tests** asserting every exact constant/action cap, `420+420+690=1530 < 1560 < 1620`, `2*1620=3240 < 3300 < 3600 < 3660`, and effective phase clamp `<= WorkAllowance`.
- [ ] **Step 2: Run the focused budget tests** with `dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --filter "FullyQualifiedName~Web6CandidatePairBudgetTests"`; expect failures for missing constants/action mapping.
- [ ] **Step 3: Implement `Web6CutoverBudgets`** as the sole backend numeric owner; reject unknown actions rather than provide a default.
- [ ] **Step 4: Write failing frame tests** for missing/non-integer/excessive/action-incompatible `requestAllowanceMs`, exact `5000`, `5001`, response reserve subtraction, and exact field sets for `resetCutover` and operation/proof actions.
- [ ] **Step 5: Run the request tests** with the same project command and `--filter "FullyQualifiedName~Web6CandidateCutoverTests"`; expect the legacy string return and field set to fail.
- [ ] **Step 6: Implement `Web6CutoverRequest` and strict validation**; accept only integer milliseconds `>5000` and `<= ActionCap(action)` and compute work allowance by subtracting exactly five seconds.
- [ ] **Step 7: Run focused GREEN and Release/net48 build**; expect all selected rows pass with zero warnings/errors.
- [ ] **Step 8: Commit** as `test(web6): define bounded cutover request budgets`.

### Task 2: Controller cancellation, session journal and conservative reset

**Files:**
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebRecoveryPilotControl.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CutoverSession.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebRecoveryPilotControlTests.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CutoverSessionTests.cs`

**Interfaces:**
- Consumes: `Web6CutoverRequest`, `Web6CutoverBudgets` from Task 1.
- Produces: `IWeb6CutoverRuntime` methods with final `CancellationToken` parameter.
- Produces: `Web6CutoverSession.Handle(Web6CutoverRequest request, CancellationToken token) -> Task<JObject>`.
- Produces: internal transition journal `Stable`, `Transitioning`, `Uncertain`; `resetCutover` is admitted only from `Stable` after the prior handler joined.
- Produces: `StartCutover(Guid, Func<Web6CutoverRequest,CancellationToken,Task<JObject>>)`.

- [ ] **Step 1: Write failing session-state tests** covering success, cancellation/failure before and after a runtime side effect, stable first/second reset to current, stable third reset preserving restored, in-body grant restoration, uncertain reset refusal, and no later action after a faulted Serve loop.
- [ ] **Step 2: Run focused RED** with `dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --filter "FullyQualifiedName~Web6CutoverSessionTests|FullyQualifiedName~WebRecoveryPilotControlTests"`; expect missing token/journal/reset behavior.
- [ ] **Step 3: Add token parameters and journal transitions**; set `Transitioning` before runtime work, `Stable` only after post-verification, and `Uncertain` on every exceptional exit.
- [ ] **Step 4: Implement linked request cancellation** from per-browser controller token and `WorkAllowance`; recheck deadline before success response and give response write only the reserved five seconds.
- [ ] **Step 5: Implement finite Stop/join behavior** that never issues reset concurrently with an active/unjoined delegate and preserves primary plus drain failures.
- [ ] **Step 6: Run focused GREEN and Release/net48 build**; assert exact-once response, no success after cancellation, no abandoned test delegate and complete aggregate failures.
- [ ] **Step 7: Commit** as `test(web6): bound cutover controller and session state`.

### Task 3: Propagate deadlines through the live cutover runtime

**Files:**
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebHostIisExpressTests.Cutover.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web5IisExpressHostAdmission.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web5HttpServiceState.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web5IisExpressHostAdmissionTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web5HttpServiceStateTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CutoverSnapshotTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateCutoverTests.cs`

**Interfaces:**
- Consumes: tokenized `IWeb6CutoverRuntime` from Task 2.
- Produces: explicit budget/token helpers for lifecycle SQL, mutation/oracle SQL, host switch, WMI/process, queue and health phases.
- Produces: observable runtime transition result used by the Task 2 journal; no physical-effect inference from stale flags.

- [ ] **Step 1: Write failing pure/fake tests** for SQL open/command timeout clamping (never `CommandTimeout=0`), polling tail clamp, netsh/process output-drain bound, WMI deadline recheck, transaction-commit deadline recheck, and cancellation after launch before admission producing uncertain state.
- [ ] **Step 2: Run focused RED** with `dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --filter "FullyQualifiedName~Web6CutoverSnapshotTests|FullyQualifiedName~Web6CandidateCutoverTests|FullyQualifiedName~Web5IisExpressHostAdmissionTests|FullyQualifiedName~Web5HttpServiceStateTests"`; expect `CancellationToken.None`, default command overloads and tail overshoot assertions to fail.
- [ ] **Step 3: Propagate the explicit token** through every cutover runtime call; replace string/default SQL overloads with commands carrying the token and a positive timeout clamped to remaining work.
- [ ] **Step 4: Clamp polling and child operations** to the smaller phase/action remainder; bound owned process kill/wait/output drain and never kill an unverified PID.
- [ ] **Step 5: Contain noncooperative edges**: do not race/drop synchronous WMI, file measurement or transaction commit; check the deadline immediately after return and prohibit later work/ack on expiry.
- [ ] **Step 6: Add source-contract tests** proving the cutover call graph has no `CancellationToken.None` or default SQL command path and every switch/lifecycle/oracle call receives the request token.
- [ ] **Step 7: Run focused GREEN, broader affected Security.Tests and locked Release/net48 builds**; record exact discovered/executed/pass/fail/skip counts and hashes.
- [ ] **Step 8: Commit** as `test(web6): propagate cutover deadlines through runtime`.

### Task 4: Exact cutover-only selector containment and hierarchy

**Files:**
- Modify: `tools/MyDmsVn.Web6.CandidateBootstrap/SelectorProcessRunner.cs`
- Modify: `tools/MyDmsVn.Web6.CandidateBootstrap/Web6CandidateBootstrapOrchestrator.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CutoverSelectorTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateBootstrapOrchestratorTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebHostIisExpressTests.Cutover.cs`

**Interfaces:**
- Consumes: backend budget constants from Task 1 and bounded Stop/drain from Tasks 2–3.
- Produces: exact cutover selector gate `3600s`, exact process admissibility `3660s`, per-browser controller `1620s`, aggregate parent `3300s`; all ordinary/pair/workload/recovery routes unchanged.

- [ ] **Step 1: Write failing admission tests** proving only the exact cutover filter/project/name receives 3600/3660 seconds and every neighboring/default selector retains its prior ceiling.
- [ ] **Step 2: Write failing topology tests** for two separate per-browser controllers under one 3300-second aggregate parent, bounded between-browser handoff and primary+drain failure preservation.
- [ ] **Step 3: Run focused RED** with `dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --filter "FullyQualifiedName~Web6CutoverSelectorTests|FullyQualifiedName~Web6CandidateBootstrapOrchestratorTests"`; expect old 1500/1800 cutover constants and missing aggregate parent to fail.
- [ ] **Step 4: Implement exact cutover-only containment** and owned-process termination/join using the existing verified job/PID identity; never widen ordinary runner admission.
- [ ] **Step 5: Run focused GREEN and locked CandidateBootstrap/Security Release builds**; expect zero warnings/errors.
- [ ] **Step 6: Commit** as `test(web6): contain cutover under sixty minute gate`.

### Task 5: Frontend cumulative scenario deadlines and single reset

**Files:**
- Create: `tests/pilot/web6-cutover-deadline.ts`
- Create: `tests/unit/http/web6CutoverDeadline.test.ts`
- Modify: `tests/pilot/web6-control-transport.ts`
- Modify: `tests/pilot/web6-cutover-control.ts`
- Modify: `tests/pilot/web6-cutover-live.spec.ts`
- Modify: `tests/unit/http/web6ControlTransport.test.ts`
- Modify: `tests/unit/http/web6CutoverControl.test.ts`
- Modify: `tests/unit/http/web6CutoverPilot.test.ts`

**Interfaces:**
- Consumes: exact constants/action map from the approved spec and backend Task 1 contract.
- Produces: `createCutoverScenarioDeadline(totalMs: number, resetReserveMs: number, now?: () => number)` with `bodyAllowance(action)` and `resetAllowance()` returning action-clamped milliseconds or refusing `<=5000`.
- Produces: `requestCutoverControl(..., requestAllowanceMs: number)` with one absolute timer armed before connector invocation and retained through full response validation.

- [ ] **Step 1: Write failing fake-clock deadline tests** for 420/180, 420/180 and 690/60 scenarios; body cannot consume reset; every action cap is exact; elapsed connect/write time is charged; `5000` refuses before connector/write and `5001` is admitted.
- [ ] **Step 2: Write failing transport tests** for delayed connect, delayed 16-second valid response, partial prefix/body, response at the absolute boundary, late callbacks, one connect/write and timer/listener cleanup.
- [ ] **Step 3: Run focused RED** with `npm.cmd run test:unit -- tests/unit/http/web6CutoverDeadline.test.ts tests/unit/http/web6ControlTransport.test.ts tests/unit/http/web6CutoverControl.test.ts tests/unit/http/web6CutoverPilot.test.ts`.
- [ ] **Step 4: Implement the monotonic scenario helper and absolute transport timer**; never refresh the timer on partial data and retain the short finite inactivity diagnostic guard without letting it preempt the absolute allowance.
- [ ] **Step 5: Update the live spec** to include `requestAllowanceMs`, use totals/reserves 420/180, 420/180, 690/60, replace only the three `afterEach` cleanup calls with `resetCutover`, and retain intentional in-body `revokeGrantOff`.
- [ ] **Step 6: Add source-contract tests** proving all live calls use the scenario helper, all actions are covered, no legacy direct socket path remains, and reset errors remain best-effort without hiding primary failures.
- [ ] **Step 7: Run focused GREEN, `npm.cmd run typecheck`, then full `npm.cmd run test:unit`**; expect zero failures/skips.
- [ ] **Step 8: Commit frontend** as `test(web6): bound cutover scenarios and reset`.

### Task 6: Exact frontend pin, integrated pure verification and evidence

**Files:**
- Modify: `tools/MyDmsVn.Web6.CandidateBootstrap/PreparedCandidateReceipt.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateBootstrapPreflightTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6CandidateBackendSourceTests.cs`
- Modify: `docs/development/WEB6_CANDIDATE_RUNBOOK.md` only if the executable timeout/reset contract is described there.
- Modify: `docs/evidence/web6/README.md` only to link the new bounded-candidate evidence; do not claim native PASS.

**Interfaces:**
- Consumes: exact frontend commit from Task 5 and all backend interfaces from Tasks 1–4.
- Produces: exact reviewed frontend pin and fresh-prepare inverse new/old admission proof.

- [ ] **Step 1: Write pin RED tests** requiring Task 5 frontend SHA to be `PIN_MATCHED` and previous `447e39f03b2b22398d30324c8cec992fc93ce83c` to be `BLOCKED_REVIEWED_FRONTEND_PIN`.
- [ ] **Step 2: Run pin RED**; expect exactly the two inverse rows to fail before changing the pin.
- [ ] **Step 3: Update the exact pin and source-contract expectations** without changing any other dependency or gitlink.
- [ ] **Step 4: Run backend integrated pure suites** for budgets, control/session, runtime propagation, selector/orchestrator, preflight/pin and snapshot; record actual TRX counts/hashes.
- [ ] **Step 5: Run locked restore and Release/net48 builds** for Server.Application, HttpHost, CandidateBootstrap and Security.Tests; expect zero errors/warnings.
- [ ] **Step 6: Re-run frontend typecheck/full unit at the exact pinned SHA**, run `git diff --check`, verify both worktrees clean except intended commits, and record source/tree hashes.
- [ ] **Step 7: Update sanitized source-only evidence** with exact limits: no SQL/IIS/browser/native acceptance, attempt21 retained, `CandidateVerified` absent, resend OFF, staging not accepted, production NO-GO.
- [ ] **Step 8: Commit backend** as `test(web6): pin bounded cutover deadline candidate`.

## Final review and live boundary

After all six task reviews pass, perform one whole-branch cross-repository review against the design and this plan. Do not run a live attempt during implementation or review. A fresh attempt22 requires a new explicit Owner authorization, a new protected `web6-cutover` approval valid no more than 24 hours with current-user-only ACL, new root/allocation, exact reviewed heads and the existing one authorize/prepare/run rule.
