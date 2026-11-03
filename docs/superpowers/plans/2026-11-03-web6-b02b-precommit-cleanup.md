# WEB6 B02b Pre-Commit Cleanup Uncertainty Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every unverified pre-Commit rollback/session cleanup fail closed as sanitized UNKNOWN/ESCALATE without losing internal primary-failure context or changing Commit certainty guarantees.

**Architecture:** Replace the bare internal Commit enum with a bounded transaction result carrying outcome, primary failure, and cleanup-stage failure flags. The transaction boundary attempts every cleanup action and returns UNKNOWN when pre-Commit teardown is unverified; orchestration retains existing preflight, Commit and readback semantics.

**Tech Stack:** C# / .NET Framework 4.8, xUnit, Microsoft.Data.SqlClient, disposable SQL fixtures.

**Spec:** `C:/Users/pdchung/.codex/attachments/3912e3f1-9335-4e15-91ec-426e270ecce2/Pasted text.txt`

## Global Constraints

- Modify the existing PR #102 branch only; do not create another PR or merge.
- No SQL schema/procedure/grant/runtime HTTP/dependency/retry changes.
- No raw exception, credential, fingerprint or payload output.
- Protected SQL CI remains Owner-authorized and NOT RUN unless separately approved.
- Preserve manual resend OFF, B6-OPS-03/04 BLOCKED, staging NOT ACCEPTED and production NO-GO.

## Review Focus

- Every rollback/close/dispose action is attempted even after an earlier cleanup failure.
- Clean pre-Commit failure preserves the primary error/cancellation; unverified cleanup maps to UNKNOWN and never Commit.
- Commit-started uncertainty remains UNKNOWN even when cleanup fails.
- Known Commit still requires exact independent matched readback.
- UNKNOWN never re-executes mutation or creates a new DecisionId.

---

### Task 1: Verifiable pre-Commit cleanup outcome

**Files:**
- Modify: `tools/MyDmsVn.WebOperation.Operator/OperatorDecisionExecution.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationOperatorDecisionTests.cs`

**Interfaces:**
- Produces: internal transaction result with `Committed`/`Unknown`, primary failure, and bounded cleanup failure flags.
- Consumes: existing transaction lifecycle delegates and decision orchestration.

- [ ] Add seven focused tests for clean primary propagation, rollback/close/two dispose failures, all cleanup failures, and cancelled post-mutation rollback failure; verify RED.
- [ ] Implement the smallest cleanup accumulator and internal result; route UNKNOWN through existing sanitized application output.
- [ ] Run the complete decision class GREEN and commit.

### Task 2: Exact counts, evidence and final review

**Files:**
- Modify: `.github/workflows/backend-web6-b02b-sql-candidate.yml`
- Modify: `tools/MyDmsVn.WebOperation.Operator/README.md`
- Modify: `docs/evidence/web6/20261103-b02b-operator-decision.md`

**Interfaces:**
- Consumes: final decision discovery count and TRX hashes.
- Produces: strict protected workflow and bounded review-round evidence.

- [ ] Update operator-facing UNKNOWN semantics and exact protected decision count.
- [ ] Run locked restore/build and exact decision/inspect/resolution-crash/race discovery and execution; record hashes, SQL oracle and process drain.
- [ ] Update evidence, obtain an independent review of the whole PR diff, fix confirmed in-scope P0/P1/P2, then push the same branch and update PR #102 without merging.
