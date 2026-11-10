# WEB6 controlled nonproduction candidate bootstrap design

**Status:** Proposed for Owner approval. This document does not enable manual resend, authorize a live run, or change staging/production disposition.

## Intent

Turn the existing WEB6 B05/F04 fail-closed harness into a repeatable, backend-owned nonproduction candidate workflow. The workflow prepares the private inputs that are currently absent, builds one pinned backend/frontend pair, and runs the existing live gates serially without weakening them.

The desired outcome is a candidate artifact in which user-confirmed same-key resend can be exercised under the existing SQL, native browser and IndexedDB proofs. Ordinary builds remain OFF. A successful candidate run is evidence for a later explicit Owner pilot decision; it is not staging acceptance or production approval.

## Goals

1. Add one controlled bootstrap/orchestration entry point that validates prerequisites before any SQL, IIS Express or browser allocation.
2. Produce immutable, protected inputs for the existing pair, cutover, workload and recovery selectors instead of bypassing their environment and receipt checks.
3. Build frontend candidate assets with one canonical `VITE_MYDMSVN_WEB6_CANDIDATE_ID` and run the backend with the same ID.
4. Execute the live gates in dependency order and preserve exact pass/fail/skip counts, native browser evidence, SQL ownership, hashes and resource disposition.
5. Keep ordinary same-key resend OFF and preserve fail-closed behavior on missing, stale or mismatched authority.
6. Leave a sanitized public summary plus protected private receipts sufficient to reproduce or audit the run without committing credentials, connection strings or proprietary test payloads.

## Non-goals and anti-goals

- No automatic HTTP mutation retry, new operation GUID, server retry queue or reinterpretation of missing status.
- No production deployment, staging acceptance, full IIS claim, Narrator claim or SLA invention.
- No runtime or operator privilege widening and no shared runtime/operator SQL principal.
- No adoption, cleanup or mutation of historical quarantines or failed-attempt resources.
- No automatic installation of Windows features, global certificate bindings, browsers, Node, SQL Server, Docker or a VM.
- No generic cleanup by database prefix, directory pattern or process name.
- No single command that both creates destructive authority and silently treats the resulting run as Owner-approved.

## Current source boundary

The frontend already has the correct product switch: `web6CandidateConfig.ts` enables manual resend only when the build contains a canonical `VITE_MYDMSVN_WEB6_CANDIDATE_ID`; an ordinary build has no ID and remains disabled. Backend candidate hosting separately requires `MYDMSVN_WEB6_RESEND_ELIGIBILITY=receipt-v1` and the same candidate ID.

The backend live selectors already fail closed:

- pair requires explicit candidate opt-in, private candidate root, pinned frontend checkout/publish tree, TLS thumbprint, browser project, SQL administrator test connection and SQL approval file;
- cutover additionally requires pinned old/current frontend and backend artifacts, a protected build-authority receipt, isolated SQL control parent and explicit current/restored epochs;
- workload additionally requires a protected Task 4 acceptance receipt, actor cohort and workload root;
- recovery additionally requires that Task 4 receipt and a dedicated recovery root.

Those contracts remain authoritative. The bootstrap creates and passes their inputs; selectors continue to validate them independently.

## Approaches considered

### 1. Backend-owned controlled bootstrap and serial orchestrator — selected

Add a small backend test-support console/command that prepares protected manifests, immutable worktrees/artifacts and task-owned roots, then invokes the existing selectors in order. This keeps SQL/IIS ownership and cleanup policy beside the backend harness that already owns them, while the frontend remains an artifact producer and browser proof consumer.

Tradeoff: this adds orchestration code and receipt schemas that require focused tests. It has the lowest risk of operator drift and provides the most repeatable evidence.

### 2. Manual runbook provisioning

An operator could create every directory, authority file, build and environment variable by hand and invoke the four filters. This changes less code but is error-prone, hard to reproduce and likely to produce mismatched candidate IDs or stale receipts.

### 3. Make each selector self-provisioning

Each live test could discover and create its own prerequisites. This looks convenient but blurs preflight, allocation, execution and cleanup; duplicates ownership logic; and makes a failed selector more dangerous. It was rejected.

## Proposed architecture

### 1. Explicit inputs

The bootstrap accepts two private files by absolute path:

1. an Owner authority receipt created in a separate, non-running `authorize` step; and
2. a run request that consumes that receipt.

The authority receipt binds the canonical candidate ID, approved task parent, SQL control parent, allowed origins/ports, destructive drill classes, exact source commits and expiry. Creating it requires an explicit Owner acknowledgement phrase and never starts a build, allocation or test. It uses canonical serialization, current-user protected ACLs and a separately supplied SHA-256. This is an auditable local Owner decision, not cryptographic proof of a remote identity. The run request cannot widen any field in the authority receipt.

The run request contains references, not embedded secrets:

- canonical candidate ID;
- exact backend and frontend source commits and repository roots;
- exact old backend/frontend commits required by cutover;
- approved task root and SQL control parent;
- paths to the existing SQL approval file and administrator test connection environment variable;
- existing localhost TLS certificate thumbprint;
- existing Node, npm/Playwright and IIS Express locations when discovery is ambiguous;
- selected browser projects (`chromium` and `edge`), actor cohort, bounded workload budget and recovery authorization;
- the protected Owner authority receipt path and expected SHA-256.

Both schemas reject unknown fields, relative paths, noncanonical candidate IDs and the known historical quarantine identifiers/roots. The run request also rejects an expired or mismatched authority receipt, roots outside its approved parents, source commits not named by it and drill classes it does not authorize. The existing SQL approval file remains a separate database-allocation authority and must also pass; neither receipt substitutes for the other. Secrets remain in the existing environment or protected external files and are never copied into the public receipt.

### 2. Preflight phase: zero allocation

`prepare` performs all checks that can be completed without creating SQL databases, HTTP queues or browser processes:

1. Canonicalize every path and prove that candidate, cutover, workload and recovery roots are distinct new children of the approved task parent.
2. Verify clean immutable Git identities. It creates detached task-owned worktrees for the exact source commits rather than using the mutable documentation worktrees. The currently hard-coded cutover current SHA remains validated by the selector; a later source change must update the reviewed authority contract rather than be inferred from HEAD.
3. Restore/build frontend and backend in staging directories, with locked dependencies. Frontend candidate assets are built with exactly one candidate ID; ordinary assets are also built without the variable and tested to remain OFF.
4. Hash complete backend publish and frontend `dist` trees, copy candidate `dist` into publish `wwwroot`, then remeasure exact file equality.
5. Validate, but do not install, IIS Express, Node, Playwright browser binaries and the existing localhost certificate. Certificate lookup must yield exactly one valid private-key certificate for the approved origin. No trust-store or global binding mutation occurs in bootstrap.
6. Validate the independent Owner authority receipt, SQL approval, effective master connection and known-server policy without allocating a database. Runtime, operator, maintenance and allocator identities remain distinct.
7. Write an immutable protected build-authority receipt containing source/tree/tool/artifact hashes, candidate ID, approved roots and expiry. Write its SHA-256 separately for selector input.

Any failure ends `prepare` with no live allocation and no acceptance receipt.

### 3. Run phase: existing selectors remain independent

`run` consumes the protected build-authority receipt and invokes named `dotnet test` filters as child processes. It passes a minimal per-process environment and never mutates the caller's persistent environment.

Execution is serial:

1. **Pair:** run Chromium, then Edge, against the same immutable pair but fresh owned SQL/browser fixture for each browser as required by the current harness. Both must pass. The orchestrator captures the selector-produced protected receipt and validates candidate ID and hashes.
2. **Cutover:** create only the old/current/restored task-owned artifacts and schema12/schema13 fixtures named by the existing cutover plan. Run the exact cutover selector and validate its receipt. No in-place downgrade occurs.
3. **Workload:** create the Task 4 acceptance receipt only from successful pair and cutover receipts plus the reviewed candidate hashes. Run smoke first, then bounded 1/4/8/16 cohorts. Missing 1,000 observations for a critical class remains `INSUFFICIENT_SAMPLE / NOT MEASURED`; it is never promoted to PASS.
4. **Recovery:** only after the same Task 4 receipt is accepted, run the dedicated backup/restore and host-interruption selector against a fresh owned recovery fixture. Report observed RPO/RTO; absence of measurement remains NOT RUN/NOT MEASURED.

The orchestrator stops on the first failed or blocked gate. It does not retry mutations or repeat a failed destructive selector automatically. A new attempt requires a new run ID and new owned roots while preserving the failed attempt's disposition.

### 4. Receipts and state machine

The bootstrap has explicit states:

`Requested -> Prepared -> PairPassed -> CutoverPassed -> WorkloadRecorded -> RecoveryRecorded -> CandidateVerified`

Any state can transition to `FailedRetained` or `BlockedNoAllocation`. It cannot skip forward. Each transition consumes the previous protected receipt and emits a new versioned receipt with:

- run/candidate ID and previous-receipt hash;
- exact source and artifact hashes;
- selector/filter, discovered/executed/pass/fail/skip counts and timestamps;
- owned resource identities and private disposition references;
- sanitized measurements/status;
- process drain, HTTP queue absence and SQL quiescence observations.

Receipts use canonical serialization, SHA-256 and current-user protected storage/ACLs, consistent with the existing WEB6 receipt model. Hashing proves immutability/integrity, not Owner approval or cryptographic identity.

### 5. Candidate feature boundary

There are three separate facts:

1. **Ordinary artifact:** always built without `VITE_MYDMSVN_WEB6_CANDIDATE_ID`; manual resend is OFF.
2. **Harness candidate artifact:** built with the candidate ID so pair/cutover browser tests can exercise manual resend. It is usable only inside the owned nonproduction harness with matching backend eligibility and candidate receipts.
3. **Owner-approved nonproduction pilot:** admission is a later explicit Owner decision referencing the final verified receipt. The bootstrap does not create this decision and cannot deploy or expose the artifact outside the owned test origin.

The backend remains fail-closed if candidate ID, feature eligibility, actor/grant/epoch/fingerprint or receipt binding differs. Frontend remains PROTECTED on 401/403/404/409/5xx, timeout, storage failure, cross-tab conflict or candidate mismatch.

### 6. Cleanup and quarantine

Cleanup is a verified disposition phase, not a `finally` block that assumes ownership:

1. stop child runners and admitted IIS Express workers;
2. prove exact process command/config identity and HTTP queue absence;
3. drain browser contexts and test workers;
4. prove SQL allocation token, original creation identity, database ID, quiescence and expected business/journal oracle;
5. remove only resources listed in this run's protected ownership receipt.

If any proof is missing, the run transitions to `FailedRetained`; the exact new resources are recorded privately and no destructive cleanup continues. Historical quarantine lists are deny-listed and never enumerated for deletion.

## Repository changes

### Backend repository

- Add a net48-compatible test-support console or narrowly scoped command project under the existing test tooling structure; it must not be referenced by runtime host projects or shipped packages.
- Add strict bootstrap request/build-authority/run-receipt models and canonical validation.
- Reuse existing candidate filesystem, protected receipt, owned SQL, host admission, workload and recovery helpers; extract only narrow shared helpers when tests prove the seam.
- Add process orchestration with bounded timeouts, sanitized output and exact test-count parsing.
- Update the candidate runbook and B05 evidence with actual results and retained limits.

### Frontend repository

- Keep the current build-time flag and runtime behavior unchanged unless tests expose a narrow defect.
- Add/extend build tests proving ordinary `dist` is OFF and candidate `dist` embeds only the canonical candidate ID.
- Provide a deterministic artifact-manifest command or script if the backend cannot consume the existing Vite manifest directly.
- Update F04 evidence with exact artifact hashes and live Chromium/Edge outcomes.

No frontend UI control is added for enabling the flag at runtime.

## Error handling and observability

- Console output contains stage, run ID, selector, counts and sanitized disposition only.
- Connection strings, JWT keys, cookies, payloads, database names where policy treats them as private, and absolute private-control paths are excluded from Git evidence.
- Child-process timeout is a failed retained attempt until process/queue/SQL disposition is independently established.
- A test result with any failed or skipped mandatory row cannot produce the next acceptance receipt.
- Workload and recovery measurements are reported independently from functional gate status.

## Verification strategy

Implementation follows RED-GREEN focused tests before a live run.

1. Pure tests for strict request parsing, path containment, deny-listed roots, expiry, candidate ID matching, canonical receipts and state transitions.
2. Failure-injection tests showing missing tools/certificates/approval, dirty or wrong Git commits, hash drift and stale receipts fail before allocation.
3. Child-process contract tests using fake runners for timeout, nonzero exit, count mismatch, mandatory skip, output redaction and no automatic retry.
4. Build tests proving ordinary frontend artifact OFF and candidate artifact ON only for the pinned ID.
5. Existing backend Application/HttpHost and frontend typecheck/unit suites.
6. Exact live pair/cutover/workload/recovery selectors, serially, only when the protected request is supplied. Record actual `pass/fail/skip`; never substitute pure tests for live evidence.
7. Independent subagent code review before push, followed by fresh verification from both exact heads.

## Completion boundary

This work is complete only when the bootstrap source and focused tests pass, the ordinary artifact is demonstrably OFF, and all four existing live selector families PASS with `0 fail / 0 mandatory skip` on one pinned candidate. Workload must also contain the required measured profiles; `INSUFFICIENT_SAMPLE / NOT MEASURED` does not complete the target. A truthful BLOCKED, FAIL or NOT MEASURED result is an intermediate disposition only. If an external prerequisite is unavailable, implementation pauses at the exact proven boundary and resumes after that authority or installation is supplied; it is not reported as completed.

Only an all-required-gates verified receipt can be presented for a separate Owner decision to enable the bounded nonproduction pilot. Staging stays NOT ACCEPTED and production remains NO-GO until B06/F05 and later explicit decisions.

## Builder-owned details

The implementation plan may choose project/file names, internal CLI syntax and helper extraction boundaries. It may not change receipt semantics, gate order, allocation/cleanup safety, ordinary-build OFF behavior or the Owner decision separation without returning for design approval.
