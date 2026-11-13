# WEB6 cutover bounded deadline hierarchy

## Intent

Make the nonproduction WEB6 cutover selector capable of waiting for legitimate host transitions while remaining provably finite and fail-closed. Attempt21 proved that the current 15-second response-header timeout can close the control pipe before a successful server handler writes its response. The correction must not retry or replay a request, acknowledge success early, enable ordinary same-key resend, weaken ownership checks, access retained attempts, or authorize production.

## Goals

- Give every cutover control request an enforced server deadline and propagate cancellation through SQL, WMI/process, queue, health and response-write work.
- Make the frontend wait derive from the same action/scenario contract and keep final success acknowledgement after all server verification.
- Keep the entire two-browser cutover selector strictly below the Owner-approved 60-minute fail-closed ceiling.
- Preserve one connector invocation, at most one framed request write, exact correlation, sanitized diagnostics, once-only settlement and deterministic drain.
- Reserve enough time for a bounded reset after a failed or successful scenario without issuing three independent cleanup transitions.

## Non-goals and anti-goals

- No product runtime behavior, HTTP contract or production deployment changes.
- No progress/success acknowledgement before the cutover handler and post-verification complete.
- No automatic retry, replay, manual resend enablement or adoption of retained SQL/IIS/browser resources.
- No claim that every SQL command may consume its old individual 30-second timeout; the shared scenario deadline is authoritative and slow work fails closed.
- No best-effort background work after a request deadline. Cancellation must be observed and drain must prove completion or retain resources.

## Approaches considered

### Selected: shared cumulative scenario deadline with phase caps

Each Playwright scenario owns one cumulative monotonic deadline. Every control request carries only the remaining bounded duration. The server validates it against the action class, links it to controller cancellation, and passes it through all operations. A phase receives the smaller of its own cap and the remaining scenario time. The response is written only after the handler and post-verification complete.

This avoids multiplying every action's maximum when actions run sequentially: they consume one scenario budget instead of each obtaining a fresh full allowance.

### Rejected: larger client timeout only

This leaves SQL, WMI, process-output drain and the handler uncancelled, lets children outlive parents and can reproduce the broken-pipe failure at a later layer.

### Rejected: early acknowledgement or asynchronous progress protocol

An early success frame weakens the existing meaning of a successful control response. A progress protocol adds framing states without solving uncancelled server work. Neither is needed once the cumulative deadline is enforced end to end.

## Deadline contract

All durations are finite constants in one backend budget owner with source-contract tests mirrored by frontend constants. Arithmetic uses monotonic elapsed time; payloads carry a remaining duration, never a wall-clock timestamp.

| Scope | Maximum |
| --- | ---: |
| Lifecycle SQL snapshot phase | 15 seconds |
| SQL mutation/oracle phase | 45 seconds |
| Host switch phase, including drain/admission/queue/health | 150 seconds |
| Composite same-epoch restart action cap | 210 seconds |
| First scenario: cached WEB4/new host, including reset | 420 seconds |
| Second scenario: current client/old host, including reset | 420 seconds |
| Third scenario: restart/revoke/restore, including reset | 690 seconds |
| One browser pack | 1,560 seconds |
| Per-browser controller lifetime | 1,620 seconds |
| Two serial controller/browser packs and between-browser handoff | 3,300 seconds |
| Cutover selector gate | 3,600 seconds (60 minutes) |

Strict tests require:

`effective phase deadline <= action work deadline < scenario < browser pack < per-browser controller`,

`2 × per-browser controller < aggregate parent < selector gate`,

where every phase is clamped to the remaining action work deadline even when its nominal phase cap is larger. The selector gate remains below its explicit admissible maximum of 3,660 seconds. The 60-minute gate is the externally visible ceiling; the extra admissible minute exists only for selector-process teardown and must not be assigned to live work.

Each scenario owns one absolute monotonic timer. Scenarios one and two reserve their final 180 seconds for reset; scenario three reserves 60 seconds because a successful third scenario must preserve the restored host and only verify stable state. Body requests cannot consume the reserve. The request field is named `requestAllowanceMs`, not `remainingMs`, and is calculated immediately before connect as:

`min(actionCap, activeSectionDeadline - monotonicNow)`.

The action caps are: 180 seconds for the four host on/off actions and `restoreEpoch`; 210 seconds for `sameEpochRestart`; 60 seconds for `committedUnavailable`; 45 seconds for `revokeGrantOn`, `revokeGrantOff` and `verifyNoCutoverMutation`; and 180 seconds for `resetCutover` (further clamped by the scenario-specific reset reserve). The server work allowance is `requestAllowanceMs - 5000ms`. Values less than or equal to 5000ms are rejected without connect/write. The five seconds are exclusively reserved for response serialization, pipe write and client settlement.

The client starts one absolute response timer before invoking the connector, so connect/write time is charged. That timer remains active through prefix, body and final correlation validation; partial-frame inactivity cannot replace or extend it. The server independently clamps the received allowance to the allowlisted action cap and its local controller remainder. A relative payload does not claim cross-process clock synchronization; cumulative accounting is frontend-owned, while the server enforces a no-larger per-request bound.

## Request and cancellation flow

1. The test creates a scenario deadline before its first UI/control action and reserves the final reset allowance inside that same budget.
2. `requestCutoverControl` writes one request containing the integer `requestAllowanceMs` computed above.
3. `Web6CutoverControl.Validate` rejects missing, non-integer, excessive or action-incompatible values before dispatch.
4. `WebRecoveryPilotControl.Serve` creates a linked request token from controller cancellation and `requestAllowanceMs - 5000ms`. It passes that token to `Web6CutoverSession.Handle` and the runtime.
5. Runtime methods accept the token. SQL opens/commands use it and set command timeout to the smaller whole-second remainder and the existing 30-second cap. Polling, WMI enumeration, process waits/output drain, HTTP.sys checks, health requests and sleeps all use the same remaining deadline.
6. On cooperative deadline/cancellation, the handler stops and the server emits no success frame. A noncooperative edge is never abandoned behind `Task.WhenAny`: the controller becomes unavailable, waits for that exact delegate to return, and permits no reset or later request. `Stop` performs a separately bounded join. Failure to prove join/drain retains all allocations and delegates final termination to the owned selector-process containment boundary.
7. Only a completed handler receives the correlated `ok: true` response. The frontend validates the existing success frame exactly once.

## Phase behavior

- Lifecycle and SQL helpers take an explicit `CancellationToken`; `CancellationToken.None` is forbidden on the cutover path by source-contract tests.
- WMI queries use finite management/enumeration options where supported. Synchronous WMI enumeration, file measurement and `SqlTransaction.Commit` are treated as noncooperative edges: no racing task is dropped. After the call returns, the deadline is checked before any next phase or success response. If it does not return, the exact protected cutover selector process is the containment boundary and is terminated only by its verified owned-process/job identity at the 60-minute gate; the attempt is retained.
- The candidate selector runner gains an exact cutover-only 3,660-second admissible process route so the 3,600-second gate plus teardown can run. Ordinary, pair, workload and recovery ceilings are unchanged.
- Process stdout/stderr drain gets an explicit bound. If it cannot drain, host identity is uncertain and resources are retained.
- Polling helpers clamp each inner operation and delay to the remaining phase time, preventing tail overshoot.
- Each browser owns its existing separate controller. Its 1,620-second timer cancels the active cooperative delegate, not only the named pipe. A separate 3,300-second aggregate parent covers both serial browser/controller packs and their handoff. `Stop` has a finite join allowance and preserves the original failure together with any drain failure.

## Reset and cleanup

Replace the three frontend `afterEach` cleanup calls with one allowlisted `resetCutover` control action. The session records `Stable`, `Transitioning(prior,target)` and `Uncertain` explicitly. It enters `Transitioning` before physical effects, becomes `Stable` only after admission/health/post-verification, and becomes `Uncertain` on cancellation or failure. Reset is admitted only after the prior handler has joined and state is `Stable`; `Transitioning`, `Uncertain`, a faulted Serve loop or an unjoined delegate refuses reset and retains the attempt without another transition.

For successful scenarios one and two, reset may perform the single proven transition back to the current host and final lifecycle verification within the 180-second reserve. For successful scenario three, reset preserves the restored host and only verifies the expected stable restored admission within its 60-second reserve. The intentional in-body `revokeGrantOff` remains and must establish `grant-restored` before `restoreEpoch`. A UI-only assertion failure while the controller is idle and `Stable` may use reset; a control-handler failure may not.

Reset remains best-effort in `afterEach` and cannot hide the primary test failure. If reset or controller drain is uncertain, the attempt remains retained.

The old individual host-off cleanup actions remain accepted for the pinned transition corpus but are not used by the revised cleanup hook. The intentional in-body `revokeGrantOff` remains. All strict frames require `requestAllowanceMs`; field-less old frames are rejected rather than receiving an unbounded compatibility default.

## Diagnostics and security

- Extend the existing sanitized diagnostic only with allowlisted `deadline`/`phase` categories; do not emit pipe paths, tokens, candidate IDs, SQL identities, operation/deployment IDs, raw payloads, raw exceptions or stacks.
- Deadline failures distinguish `scenario-exhausted`, `action-deadline`, `controller-cancelled`, `response-timeout` and `drain-uncertain` without including duration sources or resource identifiers.
- Approval remains new per live attempt, purpose `web6-cutover`, at most 24 hours and current-user-only ACL. This design itself performs no live allocation.

## Verification

- RED/GREEN pure tests for delayed but successful responses beyond 15 seconds, body/reset deadline separation, request allowance sampling before connect, exact five-second response reserve, every action cap and strict hierarchy arithmetic.
- Fake-clock cancellation tests for SQL open/command, WMI/process wait/output drain, queue/health polling tail, controller cancellation, finite Stop join and every stable/transitioning/uncertain reset state. Tests must prove a noncooperative delegate is never abandoned or followed by reset; native provider termination remains a live acceptance limit.
- Transport tests retain one connect/one write, no retry/replay, sanitized error output, late-event single settlement and timer/listener cleanup.
- Source-contract tests reject `CancellationToken.None` in the cutover call graph and prove every live control call uses the shared deadline helper.
- Full affected frontend unit/typecheck and backend locked Release/net48 builds; no SQL, IIS, browser or deployment during implementation.
- Frontend commits first; backend then pins the exact reviewed frontend SHA and tests new-pass/old-blocked prepare behavior.

## Acceptance boundary

Source tests and review can establish only a bounded candidate. Native Chromium/Edge cutover, workload and recovery remain open until a separately Owner-authorized fresh live attempt reaches exact `CandidateVerified`. Ordinary same-key resend stays OFF, staging remains unaccepted and production remains NO-GO.
