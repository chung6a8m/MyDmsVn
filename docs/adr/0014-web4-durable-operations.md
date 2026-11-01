# ADR 0014 — WEB4 transaction-owned operation journal and durable browser drafts

**Date:** 2026-11-01. **Decision:** Owner SELECTED the Roadmap 04 direction; detailed SQL/wire design is PROPOSED pending source-verified WEB4-B01 checkpoint. **Implementation:** NOT STARTED. [Backend roadmap](../plans/20261101-003-web4-reliability-server.md) | [canonical candidate design](../design/WEB4_IDEMPOTENCY_JOURNAL_CONTRACT.md).

## Context

WEB2/WEB3 proved real Sales/Receipt/Inventory work through an existing net48 System.Web host, but unknown write responses are only recoverable inside a live browser session with positive read evidence; reloads/crashes lose volatile state and cannot disambiguate a committed write. Existing Sales/Receipt Application handlers each create and commit their own SQL UoW. HTTP middleware journaling outside those transactions would create a false guarantee. Backend GitHub Actions have no configured workflows. Full IIS and production security operations remain unapproved.

## Decisions and bounded rationale

1. **Source-owned atomicity:** journal claim, business effect and durable completion result use one connection, one existing explicit UoW transaction and one Commit. Application remains host-neutral; Web operation metadata is optional for Local invocations. Concurrency correctness is SQL-backed, not process-local.
2. **Opaque intent key:** client generates GUIDv4 for each immutable Create/Update/Post intent, server scopes by authenticated authoritative actor and SHA-256 canonical request fingerprint. Same key/payload may be recognized/replayed after CURRENT authorization; key with different payload must fail. No automatic unsafe write retry.
3. **Protected positive-evidence endpoint:** GET operation status is actor-scoped and permission-gated, returns minimum committed outcome. Absence, 403, 500, timeout or cancellation is NOT confirmation of rollback. A known Posted acknowledgement does not fabricate version or bypass the document Read grant.
4. **Web-only database change:** propose opt-in schema12 journal migration, fail-closed Web admission, immutable scripts001–011, desktop exact10 unchanged, restricted Web SQL role. Exact DDL/schema fingerprint/response persistence are finalized through B01 source and negative test corpus.
5. **Browser recovery:** frontend uses IndexedDB for explicitly classified draft state, per-actor partition/cleanup, transactional writes and user-reviewed restoration. Dockview layout still stores only safe descriptors; JWT/refresh secrets are never written to IndexedDB.
6. **CI first:** Windows hosted SQL-free net48 build/tests; SQL fixture tests remain explicit owned non-production runner until safe dedicated CI infrastructure is available.
7. **Bounded pilot, not release:** IIS Express HTTPS + real SQL/Chromium/Edge and physical oracle prove WEB4 only within selected scope. Full IIS, multiworker limiter policy, production audit retention, attended IME/accessibility and production go-live remain independent.

## Rejected approaches

- Controller-only/middleware-only journal writes before/after MediatR.Send; cannot be atomic with handler-owned UoW.
- Trusting an aborted fetch, missing operation row or a transient 404 as evidence no business commit occurred.
- Browser retrying an unknown Create/Update/Post with a fresh key, or replaying on restart before user review.
- Persisting unfiltered Auth/JWT/cookie data, entire Workspace tree or arbitrary server responses to localStorage/IndexedDB.
- Introducing a second transaction manager, broad SQL role grants, background queue/event bus, or requiring Web metadata from Desktop/Local.
- Marking IIS Express, local SQL or Windows shell tests as full IIS production acceptance.

## Consequences and checks

B01 must publish field-level schema/canonical contract and source transaction map before B03/B04. B03/B04 need concurrent SQL, permission, migration and rollback tests. Web F02/F03 own real IndexedDB and UI recovery. B05/F04 own fresh independent real browser/SQL fixture and strict ledger/COGS oracle. Status/approval records belong only to task plans and current evidence. ADR does not authorize a release or certify runtime.

