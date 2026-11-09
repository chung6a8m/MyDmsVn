# WEB6 B02b Review Findings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix PR #102 P2-01 cleanup masking Commit uncertainty and P2-02 DecisionId/session semantic coupling without changing SQL authorization or Roadmap scope.

**Architecture:** Move decision orchestration and Commit cleanup into small internal, delegate-driven state machines so every phase and fault can be tested without provider hooks. Add a session-free immutable terminal identity overload in infrastructure while retaining the existing `WebOperationContext` overload for callers.

**Tech Stack:** C# / .NET Framework 4.8, Microsoft.Data.SqlClient, xUnit, SQL Server disposable fixtures, GitHub Actions.

**Spec:** `C:/Users/pdchung/.codex/attachments/dab6d529-d216-47fd-9e83-98963d3b4a15/Pasted text.txt`

## Global Constraints

- Work directly on `codex/web6-b02-operator-decision`; update PR #102, do not merge.
- No schema13, frozen contract, SQL permission, HTTP runtime/DI, dependency, retry, DecisionId regeneration, B03/F02/B05/manual-resend change.
- Preserve finite deadline/cancellation and fail closed after Commit uncertainty.
- Use only fresh owned disposable SQL fixtures; do not inspect or alter historical quarantines.
- Keep B6-OPS-03/04 BLOCKED, manual resend OFF, staging NOT ACCEPTED and production NO-GO.

## Review Focus

- Commit throws while Close/transaction Dispose/connection Dispose also throw: result remains UNKNOWN and cleanup stages are attempted once.
- Commit returns but post-Commit inspection throws or mismatches: result is UNKNOWN, never success.
- Pre-Commit failure/cancellation: no Commit/re-execute and cleanup cannot replace the primary fail-closed outcome.
- Original OperationId and audit DecisionId remain distinct GUIDv4 identities with unchanged epoch/kind/target/fingerprint SQL parameters.
- Existing B02a permission guard, terminal/crash regression and both six-kind B04 races remain unchanged.

---

### Task 1: Session-free terminal identity

**Files:**
- Modify: `src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationResolutionMaintenance.cs`
- Modify: `tools/MyDmsVn.WebOperation.Operator/RecordNotExecutedOptions.cs`
- Modify: `tools/MyDmsVn.WebOperation.Operator/SqlOperatorDecisionClient.cs`
- Test: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationOperatorDecisionTests.cs`

**Interfaces:**
- Produces: `WebOperationTerminalIdentity` and a compatible `RecordNotExecutedAsync` overload that does not accept SessionId.
- Consumes: existing schema13 stored procedure and exact CLI option values.

- [ ] Add a parser regression rejecting identical operation/decision GUIDs and strengthen the SQL oracle for every original identity field; run focused tests and observe RED.
- [ ] Add the immutable session-free identity and overload; keep the existing overload delegating for compatibility.
- [ ] Change the CLI to use the session-free overload; run the focused tests GREEN.
- [ ] Commit the Task 1 slice.

### Task 2: Commit lifecycle state machine

**Files:**
- Create: `tools/MyDmsVn.WebOperation.Operator/OperatorDecisionExecution.cs`
- Create: `tools/MyDmsVn.WebOperation.Operator/Properties/AssemblyInfo.cs`
- Modify: `tools/MyDmsVn.WebOperation.Operator/SqlOperatorDecisionClient.cs`
- Test: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationOperatorDecisionTests.cs`

**Interfaces:**
- Consumes: Task 1 terminal identity overload.
- Produces: internal phase orchestration and Commit boundary with explicit `Committed` / `Unknown` outcomes.

- [ ] Add eight fault-injection regressions for Commit/Close/Dispose, post-inspect failure/success, preflight failure, cancellation-before-Commit and no re-execute; run and observe RED due missing state-machine API.
- [ ] Implement the smallest delegate-driven state machine and transaction cleanup boundary; no cleanup exception may escape after Commit begins.
- [ ] Route the real SQL client through it and run all decision tests GREEN.
- [ ] Commit the Task 2 slice.

### Task 3: Counts, evidence and complete verification

**Files:**
- Modify: `.github/workflows/backend-web6-b02b-sql-candidate.yml`
- Modify: `docs/evidence/web6/20261103-b02b-operator-decision.md`
- Modify: `tools/MyDmsVn.WebOperation.Operator/README.md` only if the external contract changes.

**Interfaces:**
- Consumes: final focused test discovery count and TRX provenance.
- Produces: exact protected allowlist/count and bounded PR evidence.

- [ ] Update the workflow from 41 to the actual increased decision count without loosening discovery/execution/skip/retry guards.
- [ ] Run locked restore, Release/net48 build, exact decision/inspect/resolution-crash/race discovery and execution, SQL oracle, process census and cleanup checks.
- [ ] Update evidence with P2 root causes, RED/GREEN, final SHA/binary/TRX hashes, protected-CI blocker and residual risk.
- [ ] Obtain one independent whole-branch review, fix all confirmed in-scope P0/P1/P2, then push the same branch and update PR #102 without merging.
