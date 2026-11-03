# WEB5 B05 Owned-Host Admission Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a fail-closed, test-only IIS Express ownership admission gate that proves the configured HTTP.sys request queue is attached only to the exact fixture-owned worker before any workload or cutover can run.

**Architecture:** Extend the existing owned-resource supervisor with explicit Verify Host and Admit phases. A bounded `netsh http show servicestate view=requestq verbose=yes` reader parses HTTP.sys request-queue state, then correlates the exact registered HTTPS URL, controller/worker PID, process executable/start identity, IIS Express command line/config/site and single-worker topology. A proxy-disabled TLS probe is supplemental and runs only after queue ownership admission. Historical race manifests receive a separate read-only forensic inspector; no cleanup API is added.

**Tech Stack:** C# 12 / net48, xUnit v2, IIS Express, HTTP.sys/netsh, Microsoft.Data.SqlClient, PowerShell, existing owned-process and SQL fixture infrastructure.

**Spec:** `docs/plans/20261102-001-web5-operational-safety-backend.md` (B05), `docs/development/WEB5_BACKEND_ACCEPTANCE.md` (B5-IIS-02/03/04), and the Owner continuation prompt dated 2026-11-03.

## Global Constraints

- Keep backend `net48`, ADR0015 additive compatibility, schema13 and `web5-resolution-v1` unchanged.
- Test-only ownership code must never be registered in runtime DI or shipped as a browser/admin endpoint.
- PID 4, socket ownership, a health response or a signed challenge alone is never admission proof.
- Ambiguity, malformed service state, queue/process replacement, timeout, cancellation or proxy use fails closed.
- No request may target the candidate until the exact HTTP.sys queue is admitted.
- Single-worker evidence is bounded to the admitted IIS Express fixture; make no multiworker claim.
- Historical SQL quarantine remains read-only; never drop by prefix or delete a private manifest.
- Same-key resend stays disabled; production remains `NO-GO / DEFERRED`.

## Review Focus

- Localized or structurally changed `netsh` output must be rejected, not partially accepted.
- PID reuse or IIS Express restart between snapshots must invalidate admission.
- A matching PID on a different queue/URL/config must be rejected.
- Proxy/environment routing must be disabled on the direct-host probe.
- Timeout/cancellation or a late worker must not advance to Admit or resource removal.

---

### Task 1: Add Verify Host and Admit to the owned supervisor

**Files:**
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web5OwnedSupervisor.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web5OwnedSupervisorTests.cs`

**Interfaces:**
- Produces: `IWeb5OwnedResources.VerifyHost(CancellationToken) -> Task<bool>` and `IWeb5OwnedResources.Admit(CancellationToken) -> Task`.
- Produces: ordered lifecycle `Allocate -> Start -> VerifyHost -> Admit -> StopAdmission -> Drain -> VerifyOwnershipAndQuiescence -> Oracle -> RemoveOwned -> VerifyAbsent`.

- [ ] **Step 1: Write failing ordering and failure-boundary tests**

Add assertions for the new phases, wrong ownership, VerifyHost timeout/cancellation, failed admission and concurrent cleanup. Assert no Admit after failed VerifyHost and no Oracle/Remove after any failure.

- [ ] **Step 2: Run the focused tests and observe RED**

Run: `dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --filter FullyQualifiedName~Web5OwnedSupervisorTests`

Expected: compile/test failure because the new interface phases do not exist.

- [ ] **Step 3: Implement the minimal supervisor lifecycle**

Add the two interface members, invoke both inside the existing bounded Start action chain and preserve quarantine/late-task behavior.

- [ ] **Step 4: Run focused tests GREEN**

Expected: all discovered `Web5OwnedSupervisorTests` pass with zero skipped.

- [ ] **Step 5: Commit**

Commit message: `test(web5): require host verification before fixture admission`

### Task 2: Parse and validate HTTP.sys request-queue ownership

**Files:**
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web5HttpServiceState.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web5HttpServiceStateTests.cs`

**Interfaces:**
- Produces: `Web5HttpServiceState.Parse(string) -> IReadOnlyList<Web5HttpRequestQueue>`.
- Produces: `Web5HttpServiceState.RequireOwnedQueue(snapshot, Uri, int) -> Web5HttpRequestQueue`.
- Produces: `Web5HttpServiceState.ReadAsync(TimeSpan, CancellationToken) -> Task<string>` using the absolute system `netsh.exe`, bounded output and no shell.

- [ ] **Step 1: Write parser/admission RED tests**

Cover exact IIS Express queue, PID 4 only, zero/multiple attached workers, wrong URL/port/scheme, controller mismatch, duplicate queue, malformed counts/IDs, oversized output, nonzero command exit and cancellation.

- [ ] **Step 2: Run focused tests and observe RED**

Run: `dotnet test ... --filter FullyQualifiedName~Web5HttpServiceStateTests`

Expected: compile failure because the parser/reader does not exist.

- [ ] **Step 3: Implement strict bounded parsing and exact admission**

Parse queue blocks, controller, process entries and registered URLs. Require exactly one matching active queue, one active attached process, controller PID and process PID equal to the owned PID, and exact normalized URL. Reject unknown/missing mandatory structure.

- [ ] **Step 4: Run focused tests GREEN and deliberate mutation RED**

Temporarily remove the attached-process equality check, confirm the wrong-worker test fails, restore it, and rerun GREEN.

- [ ] **Step 5: Commit**

Commit message: `test(web5): verify HTTP sys request queue ownership`

### Task 3: Bind queue evidence to the exact IIS Express process and direct request

**Files:**
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web5IisExpressHostAdmission.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web5IisExpressHostAdmissionTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebHostIisExpressTests.cs`

**Interfaces:**
- Produces: `Web5IisExpressHostAdmission.Capture(Process, string configPath, string siteName, Uri origin) -> Web5IisExpressIdentity`.
- Produces: `VerifyAsync(identity, CancellationToken) -> Task<Web5HttpRequestQueue>` and `VerifyAbsentAsync(identity, CancellationToken) -> Task`.
- Produces: proxy-disabled `HttpWebRequest` for all direct-host fixture traffic.

- [ ] **Step 1: Write RED tests for process/config/queue replacement**

Cover wrong executable, relative/changed config, command-line mismatch, start-time/PID replacement, queue worker replacement, endpoint relay through a proxy and missing queue after start.

- [ ] **Step 2: Run RED**

Expected: new tests fail before implementation.

- [ ] **Step 3: Implement exact process identity and multi-snapshot verification**

Capture PID/start time/executable/config SHA-256/site/origin, re-read the live process before each snapshot, require single-worker queue ownership before admission and after the direct TLS probe, and require queue absence after the owned process stops. Set `Proxy = null` and disable redirects.

- [ ] **Step 4: Add an explicit opt-in live IIS Express ownership test**

Start only the installed IIS Express binary inside the existing owned-process boundary with a private config/site. Verify the exact HTTPS queue before the request, while the request task is in flight, after the response, and absent after stop. Use no SQL and no foreign endpoint.

- [ ] **Step 5: Run SQL-free suite and opted-in live test GREEN**

Expected: focused pure tests pass; live wrapper executes nonzero and reports exact PID/queue/start identity without secrets.

- [ ] **Step 6: Commit**

Commit message: `test(web5): admit only the owned IIS Express queue`

### Task 4: Add read-only historical quarantine forensics

**Files:**
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web5OriginalRaceForensics.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web5OriginalRaceForensicsTests.cs`

**Interfaces:**
- Produces: strict private-manifest parsing and a read-only SQL observation containing server/database ID/create time/ownership-marker presence plus two bounded session/lock snapshots.
- Does not produce any delete, DROP, trigger removal, manifest removal or cleanup-authority method.

- [ ] **Step 1: Write RED tests for malformed/foreign/incomplete manifests**

Cover ACL/reparse refusal, filename-operation mismatch, mixed databases, duplicate keys/triggers, missing ownership marker and unstable session/lock observations.

- [ ] **Step 2: Run RED**

- [ ] **Step 3: Implement strict read-only inspection**

Query only exact manifest identities. Report whether cleanup authority can be proven; since historical manifests do not contain the allocation ownership token/database creation tuple, expected disposition remains BLOCKED unless independent evidence supplies them.

- [ ] **Step 4: Run focused tests GREEN; inspect historical quarantine read-only if the exact private parent is available**

Do not mutate either allocation or manifest.

- [ ] **Step 5: Commit**

Commit message: `test(web5): add read-only quarantine forensics`

### Task 5: Verify candidate and record bounded evidence

**Files:**
- Create: `docs/evidence/web5/20261103-b05-owned-host-admission.md`
- Modify only if supported by actual results: `docs/evidence/web5/README.md`, `docs/plans/20261102-001-web5-operational-safety-backend.md`, `docs/development/WEB5_BACKEND_ACCEPTANCE.md`, `docs/development/WEB5_CODEX_HANDOFF.md`

**Interfaces:**
- Consumes: Tasks 1-4 code and test outputs.
- Produces: exact source/tree, environment, commands, discovered/executed/pass/fail/skip, deliberate RED, live queue evidence, forensic disposition and residual gate status.

- [ ] **Step 1: Run focused SQL-free verification**

Run the supervisor, HTTP service state, host admission, backup safety, original-first control and forensic tests with TRX; require nonzero discovery and zero skip/fail.

- [ ] **Step 2: Run the explicit live ownership wrapper**

Require exact owned IIS Express process/queue proof, proxy-disabled HTTPS probe and verified queue/process absence. If environment policy blocks the official read, record `BLOCKED` and do not substitute PID 4/health/challenge.

- [ ] **Step 3: Run affected complete Security test project only if shared test infrastructure changed**

Do not run broad SQL. SQL opt-in is separate and only on a fresh owned fixture.

- [ ] **Step 4: Request independent P0/P1/P2 review and fix Important findings with RED->GREEN tests**

- [ ] **Step 5: Write evidence and status updates**

Keep B05 `IN PROGRESS / NOT ACCEPTED` unless every applicable gate is actually proven. Preserve full IIS/Narrator exceptions, proxy N/A, quarantine blockers and production `NO-GO / DEFERRED`.

- [ ] **Step 6: Commit**

Commit message: `docs(web5): record owned host admission evidence`
