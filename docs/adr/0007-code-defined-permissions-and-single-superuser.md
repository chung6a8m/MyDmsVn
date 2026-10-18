# ADR 0007 — Code-defined permissions and one configured superuser

**Status:** Implemented and accepted in PR #34; current-state clarification 2026-10-18.
**Original decision:** SEC-REBUILD, 2026-10-16.
**Evidence:** [History](../HISTORY.md). The rebuild is complete and is not an active plan.

## Decision

Use one code-owned permission registry, deliberate user/role assignments and one configured database-bound superuser. Application releases do not synchronize a SQL permission catalog or automatically grant/revoke assignments. Administrator remains an ordinary role.

Registry Active status and authenticated/current active-account checks occur before superuser bypass. Unknown/deprecated keys deny. A superuser is allowed only when configured normalized username, current account and singleton binding match. Ordinary direct-user decisions override active-role grants; otherwise access defaults to deny.

Modules supply canonical definitions to one deterministic composition. The Application catalog query and offline export use that same composition. New code keys confer no automatic ordinary-user access. Preserve collision, undeclared-key and lifecycle checks.

## Administration and bootstrap

Ordinary user/role/membership/permission changes occur through the authorized administration contract and named SQL procedures, with expected revisions and atomic security audit. Assignment import previews are bound to target revision/catalog digest, recomputed and reauthorized on Apply. Do not trust client-supplied deltas or turn import into synchronization/implicit conflict overwrite.

A maintenance-only bootstrap runs against an empty user store and unbound settings, creating a single active identity and protected binding with no grants. No default password, automatic startup seed or automatic re-enrollment exists. Runtime cannot directly modify the binding; protected rename/recovery uses explicit maintenance commands and verified current identity.

The retired provisioning/synchronization subsystem is not a maintained compatibility mode. Current schema/bootstrap/administration and the [operator runbook](../SECURITY_ADMINISTRATION_RUNBOOK.md) define the supported baseline. Historical P3.1 evidence does not prove the replacement's behavior; SEC-REBUILD has its own accepted evidence.

## Boundaries and consequences

Keep runtime and migration principals separate, preserve legacy password hash/salt compatibility, and enforce Application authorization regardless of UI. Local Mode still exposes SQL connectivity to a client-controlled process; a supplied actor ID is not a trusted remote identity. A real HTTP boundary is deferred, not silently added by this decision.

The [Security reference](../SECURITY.md), [Contracts](../CONTRACTS.md) and current migrations/stores describe implementation. RESP changes only test lifecycle and L2DB changes persistence internals while preserving these semantics. Their tests follow [TEST_POLICY](../TEST_POLICY.md); old SEC-REBUILD gate IDs remain historical runner configuration rather than new tasks.
