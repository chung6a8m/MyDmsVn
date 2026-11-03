# WEB6 B02a Read-only Operator Inspector Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an isolated net48 operator CLI that inspects one exact operation through the restricted SQL operator procedure without performing DML or exposing secrets/business payload.

**Architecture:** A small console project under `tools/` owns strict argument parsing, target validation, finite cancellation, stable exit codes and sanitized output. Its SQL client opens only the connection string supplied through the dedicated operator environment variable, verifies the requested non-system database, begins a transaction, calls existing `SqlWebOperationResolutionMaintenance.InspectAsync`, and rolls back. It does not reference HttpHost, join runtime DI, call `RecordNotExecutedAsync`, or add a migration.

**Tech Stack:** C# 12, net48, Microsoft.Data.SqlClient 7.0.2, existing Application/Infrastructure operation types, xUnit SQL/security test owner.

**Spec:** `docs/plans/20261103-001-web6-recovery-quality-backend.md` §3 (B02 read-only subset), `docs/design/WEB6_RECOVERY_OPERATION_PROTOCOL.md` §4, `docs/development/WEB6_BACKEND_ACCEPTANCE.md` B6-OPS-01..02.

## Global Constraints

- B02a is inspect-only: no SQL terminal decision, audit insert, claim, ticket or business mutation.
- Missing/foreign rows return `INDETERMINATE`, never `NOT_EXECUTED`.
- Require explicit `--environment` and `--database`; read the connection only from `MYDMSVN_WEB_OPERATION_OPERATOR_CONNECTION_STRING`; never fall back to `master` or print connection data.
- Preserve separate runtime/operator/migration principals and existing schema13 procedure permissions.
- Keep B6-OPS-03/B6-OPS-04 pending for B02b.

## Review Focus

- Duplicate/unknown/malformed options must fail before opening SQL.
- Connection strings targeting `master` or a database different from `--database` must fail closed without identifier interpolation.
- Timeout, caller cancellation and SQL availability loss must have distinct bounded outcomes with secret-free errors.
- Wrong actor/epoch/fingerprint and runtime-principal denial must not disclose whether an operation exists.
- Successful inspect must leave journal, terminal audit, source, stock and ledger byte-for-byte/row-for-row unchanged.

---

### Task 1: Argument, help and exit-code contract

**Files:**
- Create: `tools/MyDmsVn.WebOperation.Operator/MyDmsVn.WebOperation.Operator.csproj`
- Create: `tools/MyDmsVn.WebOperation.Operator/InspectOptions.cs`
- Create: `tools/MyDmsVn.WebOperation.Operator/OperatorExitCode.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationOperatorInspectTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj`

**Interfaces:**
- Produces: `InspectOptions.TryParse(string[] args, out InspectOptions?, out string?)`, exact v4 GUID/positive actor/64-hex fingerprint/environment/database/timeout validation, and stable codes 0/2/3/4/5/6/130.

- [ ] Write SQL-free tests for `--help`, the exact `inspect` shape, invalid/duplicate/missing actor/epoch/operation/fingerprint/environment/database/timeout and unknown commands/options.
- [ ] Run the focused tests and verify RED because the parser/project does not exist.
- [ ] Implement only the parser and exit-code constants needed by the tests.
- [ ] Re-run focused tests and require GREEN.
- [ ] Commit parser and contract.

### Task 2: Sanitized application output and error mapping

**Files:**
- Create: `tools/MyDmsVn.WebOperation.Operator/OperatorInspectApplication.cs`
- Create: `tools/MyDmsVn.WebOperation.Operator/OperatorInspectionResult.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationOperatorInspectTests.cs`

**Interfaces:**
- Consumes: `IOperatorInspectionClient.InspectAsync(InspectOptions, CancellationToken)`.
- Produces: bounded `status=COMMITTED|EXPIRED|CANCELLED|INDETERMINATE` output with no result bytes, payload or credentials; maps denied/indeterminate/environment/timeout/cancel to stable exit codes.

- [ ] Write failing tests using a fake inspection client for help, authoritative statuses, indeterminate, denied, unavailable, timeout and caller cancellation; assert output excludes supplied secret markers.
- [ ] Run focused tests and verify expected RED.
- [ ] Implement the minimal application/formatter and exception mapping.
- [ ] Re-run focused tests and require GREEN.
- [ ] Commit application behavior.

### Task 3: Read-only SQL client and CLI composition

**Files:**
- Create: `tools/MyDmsVn.WebOperation.Operator/SqlOperatorInspectionClient.cs`
- Create: `tools/MyDmsVn.WebOperation.Operator/Program.cs`
- Create: `tools/MyDmsVn.WebOperation.Operator/README.md`
- Modify: `MyDmsVn.sln`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationOperatorInspectTests.cs`

**Interfaces:**
- Consumes: `SqlWebOperationResolutionMaintenance.InspectAsync(Guid,int,Guid,byte[],CancellationToken)`.
- Produces: explicit operator connection/session using a verified non-system target, finite connect/command cancellation, read transaction rollback and Ctrl+C cancellation.

- [ ] Add SQL tests for COMMITTED/EXPIRED/CANCELLED/INDETERMINATE, wrong DB target, runtime principal denial, restricted operator success, wrong actor/epoch/fingerprint, unavailable/timeout/cancellation and before/after physical snapshots.
- [ ] Run the SQL tests and verify RED because the SQL client/composition is missing.
- [ ] Implement the SQL client with no dynamic identifier, no fallback target and no DML; compose `Program` without HttpHost or runtime DI.
- [ ] Add the project to the solution and generate/update its locked package graph using locked-compatible versions only.
- [ ] Run SQL-free and SQL tests, then direct executable `--help`; require zero skip for mandatory SQL rows.
- [ ] Commit the executable and SQL proof.

### Task 4: Evidence, self-review and PR

**Files:**
- Create: `docs/evidence/web6/20261103-b02a-operator-inspect.md`
- Modify: `docs/evidence/web6/README.md`

**Interfaces:**
- Consumes: base/head SHA, build/test/TRX hashes/counts, SQL version/schema fingerprint, principal grants and physical no-change oracle.
- Produces: B6-OPS-01/B6-OPS-02 result; B6-OPS-03/B6-OPS-04 explicitly pending.

- [ ] Run locked restore, Release/net48 build, direct help/invalid invocation, focused SQL-free tests, focused real-SQL tests and complete affected test project required by TDD completion policy.
- [ ] Verify output contains no connection string/credential/payload marker and inspect performs no journal/audit/source/ledger change.
- [ ] Record exact counts, cleanup/quarantine, toolchain and P0/P1/P2 findings in dated evidence and update only the WEB6 index.
- [ ] Self-review the diff; fix all P0/P1 and disposition P2.
- [ ] Commit evidence, push `codex/web6-b02-operator-inspect`, and open the requested PR without merging.
