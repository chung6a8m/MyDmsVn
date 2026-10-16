# TESTGATE-T03 Manifest and Multi-project Gate Orchestration Design

**Status:** Draft pending written-spec review
**Date:** 2026-10-16
**Plan:** `docs/plans/20261016-001-task-gates-and-modular-tests.md`
**Task:** TESTGATE-T03

## Purpose

TESTGATE-T03 adds a checked-in, fail-closed contract for task-scoped and final test execution. It provides the stable logical-case identity and guarded multi-project orchestration that TESTGATE-T04 and TESTGATE-T05 require before moving tests between assemblies.

This change extends the existing `MyDmsVn.SqlTestRunner` executable and keeps PowerShell as a thin entry point. It reuses the T02 `pr-acceptance` path, process-tree watchdog, SQL preflight, TRX parsing, and ownership reconciliation instead of creating a second safety path.

The v1 manifest describes only projects and gates that exist at the implementation revision. It does not create or predeclare proposed T04/T05 test projects.

## Scope

### In scope

- A versioned checked-in gate manifest.
- `Task` and `Final` modes exposed through `tests/tools/Invoke-TestGate.ps1`.
- Exact manifest-owned project, TFM, module, category, selector, prerequisite, resource, timeout, strict-mode, and evidence declarations.
- Stable logical-case identity independent of physical project, assembly, TRX GUID, and generated run ID.
- Independent source, expanded discovery, and executed-TRX reconciliation per TFM and globally across selected projects.
- Fail-closed SQL serialization across local processes and projects for one approved SQL instance.
- Bounded cancellation, abandoned-owner recovery, and existing owned-database reconciliation.
- Sanitized aggregate evidence tied to the tested revision and manifest fingerprint.
- Negative tests for unknown, empty, stale, ambiguous, incomplete, unsupported, and unsafe configurations.
- Policy, matrix, plan, and operator documentation updates supported by implementation evidence.

### Out of scope

- Creating or moving Desktop or SQL test projects; those remain T04/T05.
- Adding CI jobs or requiring CI to repeat local acceptance.
- Changing production code, database schema, migrations, permission behavior, or SQL credentials.
- Enabling SQL or GUI parallelism.
- Implementing a Desktop GUI watchdog.
- Replacing the distinct T02 multi-pass `baseline` benchmark.
- Publishing raw TRX, approval files, ownership journals, connection strings, test arguments, local paths, or run identifiers.

## Chosen architecture

The existing C# runner becomes the authoritative parser, validator, orchestrator, identity engine, and evidence producer. `Invoke-TestGate.ps1` resolves repository-level invocation inputs and delegates to one runner command. It cannot override manifest-owned project, framework, category, or selector fields.

This design was selected over a PowerShell-first implementation because schema validation, cross-process locking, process ownership, identity reconciliation, and adversarial tests need one typed fail-closed implementation. A separate executable was rejected because it would duplicate the T02 runner's safety path and increase the T03 surface without improving isolation.

The main components are:

- `GateManifestLoader`: strict JSON parsing and schema-version validation.
- `GateManifestValidator`: semantic validation against the repository and requested mode.
- `TestGateCoordinator`: ordered build, discovery, execution, reconciliation, and reporting.
- `LogicalCaseInventory`: source method signatures, reviewed classification, and stable row mappings.
- `LogicalCaseReconciler`: source-to-discovery-to-TRX bijection and global duplicate detection.
- `SqlExecutionLease`: approved-instance mutex and durable local recovery lease.
- `TestGateReportExporter`: sanitized per-gate and aggregate JSON evidence.

Existing `OwnedProcessRunner`, `BenchmarkRunCoordinator`, `EnvironmentPreflightGate`, `BenchmarkOwnershipGate`, discovery/TRX parsers, and SQL acceptance exporter remain the execution primitives where applicable.

## Manifest v1

### Location and strict parsing

The manifest is checked in at `tests/test-gates.v1.json`. Its top-level `schemaVersion` is `1`. Unknown JSON fields, duplicate IDs, missing required fields, unsupported enum values, absolute paths, paths escaping the repository, or unsupported schema versions are hard failures.

All repository paths use normalized forward-slash relative paths. Project paths must exist, be unique within a selected gate, and match the project built and executed. Declared TFMs must be present in the checked-in project file; the runner does not silently substitute a compatible-looking TFM.

### Shape

The top-level document contains:

- `schemaVersion`
- `gates`
- `logicalCases`
- `identityMappings`
- `exemptions`

Each gate declares:

- `planId`, `taskId`, `mode`, and unique `gateId`.
- `project` and exact ordered `frameworks`.
- `module` and reviewed `classification`.
- `selection.kind` and exact patterns. V1 supports only explicit reviewed selection kinds; an unfiltered project selection is represented explicitly, not by an omitted filter.
- `buildPrerequisites` as repository-relative project paths and TFMs.
- `resources.sql` and `resources.gui`.
- `strictMode`.
- Per-command and whole-gate timeouts.
- Required evidence and reconciliation policies.

The implementation seeds only real T03 gates:

1. `Task / 20261016-001 / TESTGATE-T03`: the complete SQL runner test project on `net8.0`, classified as non-SQL test infrastructure.
2. `Final / 20261016-001 / TESTGATE-T03`: the complete SQL runner regression and the existing strict T02 SQL `pr-acceptance` gate for `MyDmsVn.Server.Infrastructure.IntegrationTests`, sequentially on `net48` then `net8.0`.

No entry names a proposed T04/T05 project. Later work adds entries atomically with the new real projects and reviewed identity mappings.

### Selection rules

Selection is owned by the manifest. The CLI cannot supply `--filter`, project, TFM, module, or category overrides.

The validator rejects:

- An unknown plan/task/mode/gate.
- A selected task with zero gates.
- A gate with no project or no supported TFM.
- Empty patterns for a filtered selection.
- A selector that discovers zero logical cases.
- Discovered cases outside the reviewed selector.
- A category selector whose source inventory does not contain the declared category.
- A module/category/project combination not represented by the manifest.
- Options that would broaden a task invocation to a solution or unrelated project.

## Command contract

The repository entry point is:

```powershell
./tests/tools/Invoke-TestGate.ps1 `
  -Mode Task `
  -PlanId '20261016-001' `
  -TaskId 'TESTGATE-T03' `
  -ArtifactRoot <private-local-path>
```

`Final` uses the same plan and task identity and additionally requires the operator-managed SQL approval file. It resolves the exact Git commit and requires a clean committed working tree. Task mode may run on an uncommitted development tree, but its evidence is marked non-final and cannot satisfy L3.

The wrapper calls a runner command equivalent to:

```text
test-gate --manifest <path> --mode <Task|Final> --plan <id> --task <id>
          --repo <path> --artifacts <path> [--approval <path>] [--revision <sha>]
```

Argument parsing rejects duplicate switches, missing values, unknown switches, invalid mode spelling, and unexpected positional values. Usage errors return `64`. A valid but incomplete or failed gate returns `3`. Resource/preflight denial returns `2`. Success returns `0`.

The wrapper forwards the runner exit code without translating a failed gate into success.

## Logical-case identity v1

### Durable identity

The durable logical key is computed from:

```text
identity-version
+ TFM
+ normalized method signature
+ stable Fact/Theory row key
+ reviewed classification
```

The physical project path, assembly name, TRX test ID/GUID, generated run ID, discovery order, Theory row ordinal, and display name are excluded.

The normalized method signature contains the fully qualified declaring type, method name, generic arity, and normalized parameter types. Test method overloads therefore remain distinct at source level. If discovery output cannot be joined to exactly one normalized source signature, reconciliation fails as ambiguous.

Classification is a normalized sorted set. SQL cases use their reviewed SQL categories. Non-SQL cases use an explicit reviewed non-SQL classification. A category change therefore changes the logical key and requires a reviewed identity mapping rather than silently preserving identity.

### Facts and Theory rows

- A Fact has stable row key `fact`.
- A Theory row has a reviewer-owned opaque row ID stored in the manifest.
- A hash of the current normalized discovery display is stored only as a physical row locator. It is not part of the durable logical key and is never exported as raw arguments.
- Row IDs are unique within a normalized method signature and remain stable across project moves and discovery reordering.
- Duplicate display locators, duplicate row IDs, an unrecognized row, a missing expected row, or a locator matching more than one discovered row is a hard failure.
- If a provider cannot produce deterministic distinguishable discovery rows, each row requires an explicit reviewed opaque mapping. Unsupported nondeterministic rows block the gate.

### Staleness and mappings

Each logical method entry stores a reviewed-code fingerprint separately from its durable identity. Source, test data, classification, project/build files, or referenced reviewed boundary changes can invalidate the inventory without needlessly changing the logical key.

Method-scoped exemptions are explicit manifest entries containing the normalized method signature, reviewed non-SQL classification, rationale, and reviewed-code fingerprint. An exemption that no longer resolves uniquely, acquires a SQL category, or has a stale fingerprint is rejected.

`identityMappings` records intentional old-to-new logical key changes, including TFM or classification changes. Mappings must be one-to-one, versioned, reviewer-visible, and reference existing endpoints. Unmapped disappearance, duplicate destination, cycles, or ambiguous many-to-one mappings fail validation.

V1 starts with no speculative T04/T05 mappings.

## Independent inventory and reconciliation

For every selected project and TFM, the runner obtains three inventories:

1. **Source inventory:** reflection and reviewed-source analysis produce normalized signatures, Fact/Theory kind, categories, and fingerprints.
2. **Expanded discovery inventory:** a bounded list-tests process produces the actual expanded cases. The runner joins each discovered case to one source method and one reviewed row mapping.
3. **Executed inventory:** a complete per-project/per-TFM TRX is parsed independently and joined to the same logical keys.

The gate requires a bijection:

```text
checked-in logical inventory = source = discovery = executed TRX
```

The comparison preserves multiplicity and is performed first per project/TFM and then globally for each TFM across all projects in the gate. It rejects missing, extra, duplicate, ambiguous, skipped mandatory, failed, wrong-TFM, wrong-category, incomplete-TRX, stale-fingerprint, and duplicate-global-owner cases.

Aggregate counts are evidence summaries only. Equal counts cannot substitute for logical-key reconciliation.

The strict SQL path continues to require its existing classification audit, migration audit, preflight, watchdog, per-pass inventory reconciliation, and ownership gate. T03 adds global logical reconciliation around that path; it does not weaken or bypass any T02 check.

## SQL process serialization and recovery

### Lock scope

Before any real-SQL gate starts, the runner loads and validates the approval file and derives a named mutex key from the approved SQL instance fingerprint. The raw fingerprint and approval contents are not printed.

The lock is instance-wide rather than project- or TFM-specific. This is a conservative serial fallback: separate projects, separate runner invocations, and `net48`/`net8.0` cannot overlap against the same approved instance. Within one final invocation, frameworks remain ordered `net48`, then `net8.0`.

The mutex is acquired before SQL preflight and retained through execution, timeout/cancellation handling, descendant termination, ownership reconciliation, report finalization, and durable lease completion.

### Durable lease

A private local lease record is stored outside the repository under the current operator's local application-data directory. It contains only data required for recovery: schema version, hashed resource key, owning PID, invocation ID, start time, private artifact location, requested gate identity, and completion state. It is never published or committed.

Normal contention waits only up to the manifest's resource timeout. Timeout or cancellation returns a failed/blocked gate without starting SQL.

If the mutex reports an abandoned owner, the next holder enters recovery-only mode before it may execute tests:

1. Confirm the previous owner process is no longer live.
2. Validate the durable lease shape and locate its private ownership evidence.
3. Run the existing ownership reconciliation/recovery path for that invocation.
4. Require zero unresolved owned databases and no ambiguous/missing journal required for proof.
5. Mark the prior lease recovered and only then begin the new preflight.

Missing, corrupt, mismatched, or inconclusive recovery evidence fails closed. The runner does not guess database ownership or delete an unverified database.

Cancellation and watchdog failure keep the mutex until descendants are terminated/drained and ownership reconciliation completes or records a blocking recovery artifact.

## Evidence and sanitization

Every gate writes private raw evidence under the caller-provided artifact root and a separately reviewable sanitized aggregate JSON report.

The aggregate includes:

- Schema and identity versions.
- Plan/task/mode/gate and tested revision.
- Manifest fingerprint.
- Project-relative logical gate names and TFMs.
- Source/discovered/executed/passed/failed/skipped counts.
- Exit, timeout, preflight, lock, recovery, watchdog, inventory, and ownership outcomes.
- Duration and final completeness state.

The aggregate excludes:

- Connection strings and SQL instance identity/fingerprint.
- Approval IDs, approval content, and configuration hashes.
- Absolute/local paths.
- Raw test names, Theory arguments, display names, row locator hashes, TRX IDs, run GUIDs, PIDs, and database names.

Final evidence is valid only for the exact clean committed revision and manifest fingerprint. Changes to source, tests/Theory rows, SQL/migrations, manifest/inventory/mappings, runner/harness, project/build files, or execution inputs invalidate the applicable evidence.

## Failure behavior

The runner fails closed before executing tests when validation, selection, resources, identity, or prerequisites are incomplete. Once execution starts, any failed command, timeout, incomplete discovery/TRX, mandatory skip, reconciliation mismatch, lock/recovery failure, preflight denial, child leak, or unresolved ownership makes the gate incomplete.

The runner never silently:

- Expands `Task` to the solution or to `Final`.
- Drops an unsupported TFM.
- Reclassifies a case.
- Converts an empty selector into an unfiltered run.
- Treats list-tests or aggregate counts as execution proof.
- Falls back from the strict SQL runner to bare `dotnet test`.
- Publishes private raw evidence.

## Test strategy

Implementation follows TDD in small slices. Pure runner tests cover:

- Strict manifest JSON/schema parsing and path containment.
- Known Task/Final selection and rejection of unknown or empty selections.
- Omitted plan/task IDs, project/TFM validation, unsupported CLI options, category typos, and absent required approval.
- Normalized method signatures and physical-project-independent logical keys.
- Fact identity, Theory opaque mappings, reordered rows, duplicates, ambiguity, missing and extra rows.
- Classification normalization, drift, exemptions, stale fingerprints, and reviewed mappings.
- Per-project and global source/discovery/TRX reconciliation.
- Missing/corrupt/partial TRX, failure, skip, wrong TFM, and duplicate ownership.
- SQL mutex contention, bounded wait, cancellation, abandoned owner, valid recovery, and inconclusive recovery denial using injected process/lease/ownership abstractions.
- Sanitized reports and forbidden-field checks.
- Wrapper/runner command construction and exit-code propagation.

Task-level L2 runs only:

```powershell
dotnet test tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj -c Release -f net8.0
```

No real SQL is required for pure manifest, identity, or lock-state tests. If implementation changes the shared SQL harness itself, its `net48` and `net8.0` tests become required. Live focused SQL is used only if ownership/preflight behavior must be validated beyond pure injected tests.

After all task gates are green and gate-affecting inputs are frozen, execute one planned local `Final / 20261016-001 / TESTGATE-T03` invocation. It must include the strict unfiltered T02 SQL acceptance sequentially on `net48` and `net8.0`, with approved non-production SQL, zero mandatory skips, complete source/discovery/TRX reconciliation, watchdog success, and ownership cleanup. Raw evidence stays local.

## Documentation and completion criteria

Implementation updates the plan, test execution policy, test gate matrix, tests README, and proposed ADR 0006 only where verified behavior supersedes proposal text. It does not mark T04/T05 implemented.

T03 is complete only when:

- The checked-in v1 manifest and strict validator exist.
- Task and Final commands execute only their reviewed entries.
- Logical identities and Theory mappings reconcile source, discovery, and executed TRX exactly.
- Cross-process SQL locking and abandoned-owner recovery fail closed under negative tests.
- Sanitized evidence is bound to revision and manifest fingerprint.
- The entire runner test project passes on `net8.0`.
- One frozen-revision final invocation passes the required strict dual-TFM SQL gate, or T03 remains explicitly blocked/not verified.
