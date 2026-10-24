# Test coverage — MyDmsVn

**Updated:** 2026-10-24. This document owns behavioral coverage; [TEST_POLICY](TEST_POLICY.md) alone governs execution scope and [tests/README](../tests/README.md) has direct commands. The [UI-NEXT roadmap](ui-next/20261023-active-roadmap.md) remains the sole active queue, originally activated from `v1a-pro-ui-next@7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639`. UIN-T00–T13 retain their recorded bounded verification. Current work is the [DateInput correction UIN-D01–UIN-D03](ui-next/20261024-dateinput-unified-compact-correction.md): implementation **NOT STARTED**, runtime checks **NOT RUN**, and implemented-candidate Owner visual acceptance pending.

The [archive boundary](archive/20261023-pre-ui-next/README.md) retires old queues without completing their open acceptance. [SAL-T08 evidence](evidence/sal/20261021-t08-integrated-acceptance.md) distinguishes product/package `f5039cb` from GUI test `4697744`; candidate-specific runnable automation passed, while native125, attended observations and dated Owner acceptance remain OPEN. R3/R4 source fixes are reviewed/approved. BCF/BCI/GUI-P5/L2DB/RESP retain their exact historical signed scopes and exceptions.

## Active UI-NEXT coverage

The [design and control policy](ui-next/20261023-design-and-control-policy.md) and [validation and handoff](ui-next/20261023-validation-and-handoff.md) own the current behavioral and evidence boundary. Preserve the following coverage and select affected cases for the current changed behavior:

- Safe error field metadata survives mapping; field and stable-row cell messages align with invalid visual state and clear correctly without losing raw input. Fieldless errors remain visible; duplicate Product rows and reorder cannot misroute a line error.
- Feedback belongs to the originating workspace instance/session/document generation. Tab switching, concurrent operations, hidden tabs, close/reopen and logout reject stale callbacks and do not leak protected data or steal focus.
- Persistent recovery/error state outlives a transient success toast where required. Hidden-host retention and pending-message bounds are explicit; dismissing a visual does not silently resolve a business lock.
- Busy leases/lifecycle survive nested operations, cancellation and disposal without flicker, premature unlock or stale focus restoration. Query cancellation does not imply mutation rollback. Native keyboard and existing command admission continue to enforce Posted/busy/denied/recovery restrictions.
- Normal tab close resolves pending edits and honors app-owned dirty/recovery admission before disposal; rejected close retains buffers. Forced session cleanup remains independent of ordinary close veto. Shared close affordances preserve native selection, overflow, keyboard and accessibility semantics.
- Theme surfaces use semantic tokens while preserving font ownership, high contrast, Light/Dark behavior, DPI/layout and native accessible names/focus. New generic UI APIs preserve the shared library's supported targets and existing toast/input/spinner contracts.
- DateInput correction preserves caret/selection in the same logical day/month/year segment through repeated Up/Down and short-input normalization; rejected steps preserve text and selection. Unified Compact provides one border and an icon-only calendar trigger before/after opening, selecting and canceling; retain popup/focus/validation behavior and standalone CalendarPicker compatibility. Cover affected hosts, Light/Dark and measured native100/native125 using the correction plan's focused scope, with design approval separate from new-candidate runtime/Owner acceptance.

Use existing Application/Session/WinForms/vendor test owners. Proposed cases/selectors are not runnable until implemented and discovered. Archived SAL-T08, the full six-owner fresh/reuse round and old unfinished acceptance are excluded; broaden only for the actual changed dependency under TEST_POLICY.

## Preserved Sales and inventory behavior

The current source implements exact scale/precision-safe valuation, schema10 and verified exact9 admission, authorized Draft/Post/Get/List, independent duplicate/gift rows, shared receipt/sale/reconciliation locking, immutable Posted costs and filter-bound keyset queries. Preserve transaction atomicity, one economic effect under retries, independent-session concurrency, quantity/value conservation, session generations and uncertain-outcome recovery. [Business](DIRECT_SALES.md), [costing](DIRECT_SALES_COSTING.md), [query/UI](DIRECT_SALES_QUERY_AND_UI.md) and [SAL acceptance](DIRECT_SALES_ACCEPTANCE.md) retain the source rules and historical evidence; they do not schedule a new SAL integration round.

Inventory remains the existing SQL test owner for sales/value persistence. Do not create a new SQL harness/project or rerun all owners merely because a UI consumer changes.

## Coverage owners

| Area | Current project(s) | Behavior |
|---|---|---|
| Boundaries/contracts | `MyDmsVn.Architecture.Tests`, `MyDmsVn.Server.Application.Tests` | Reference rules, typed result/JSON semantics, validation, authorization and actor snapshots |
| Foundation | `MyDmsVn.Server.Infrastructure.Foundation.Tests` | Connection/UoW state, commit/rollback, ownership, provider behavior |
| Catalog | `MyDmsVn.Server.Infrastructure.Catalog.Tests` | Mapping, CRUD, paging/filtering, constraints and lookups |
| Inventory | `MyDmsVn.Server.Infrastructure.Inventory.Tests` | Receipt draft/post, optimistic concurrency, ledger/balance/card/reconciliation |
| Security | `MyDmsVn.Server.Infrastructure.Security.Tests` | SQL identity/permissions, superuser, administration/import, audit |
| Schema | `MyDmsVn.Server.Infrastructure.Migrations.Tests` | Clean/no-op migration, partial/unknown/future schema, maintenance principals |
| SQL infrastructure | `MyDmsVn.Server.Infrastructure.IntegrationTests` | Local fresh-database ownership/cleanup, configuration rejection and concurrency probes; inspect source to distinguish real SQL from pure tests |
| Desktop | `MyDmsVn.Desktop.Session.Tests`, `MyDmsVn.Desktop.Tests` | Pure session/ViewModel behavior and guarded WinForms interaction respectively |

Paths are under `tests/`. Product and all ten active test projects use `net48`; the former standalone SQL framework and its tests are removed. Historical product .NET 8 coverage is parked.

## Persistence and migrations

- Clean database applies each reviewed production migration; second unchanged invocation is a no-op. Partial, unmanaged, unknown and future schema states fail according to the manifest.
- An explicit UoW shares one connection/transaction across repositories and queries; uncommitted reads on that scope see its writes.
- Two writes commit together; a failure after the first rolls them back; disposal without commit rolls back and releases resources.
- Independently created UoWs do not exchange connections/transactions. Reject invalid state, nested transactions and use-after-disposal; cancellation has bounded cleanup.
- Catalog uniqueness, FKs, nullable fields, Unicode/no-NUL rules, SQL error mapping, deterministic filters/paging and inactive historical references remain covered.
- L2DB mapping/projection changes preserve precision/scale, DateTime handling, identity return values, rowversion bytes, parameterization and null semantics.

## Inventory

- Draft save/reload/update observes expected rowversion; stale updates do not replace accepted changes.
- Posting atomically changes receipt status, adds one movement per line and updates balances.
- Sequential and concurrent duplicate Post creates no duplicate ledger entries or balance increments.
- Different receipts posting to the same warehouse/product retain the complete sum; missing-balance-row creation is race-safe.
- Injected failure after ledger insertion rolls back receipt, ledger and balance together.
- Unauthorized posting leaves business state unchanged. Posted receipts remain immutable; append-only trigger behavior remains enforced.
- Balance/card filters, inactive historical references, UTC boundaries, stable ordering and document identity match the contract.
- Ledger/balance reconciliation observes one consistent database state, detects missing/extra keys, and preserves lock ordering against concurrent Post.
- Real concurrency tests coordinate independent SQL sessions; collection serialization must not replace those sessions with sequential calls.

## Permissions, identity and sessions

- Authentication/active-account checks and code-registry Active status precede authorization, including superuser bypass. Unknown/deprecated keys deny.
- Configured superuser identity must match the protected current SQL binding; disabled/mismatched accounts fail closed. Administrator is an ordinary role.
- Ordinary direct Grant/Deny takes precedence over role grants; inactive roles and unassigned permissions deny.
- New keys change the code catalog without seeding or granting assignments. Registry collisions and missing use-case definitions fail.
- Administration/import preserves target revisions, catalog digest, protected targets, audit and atomic mutations. Stale preview/target changes are rejected.
- Legacy hash verification uses approved algorithm vectors; rehash and its failure behavior preserve valid credentials without exposing secret material.
- Local dispatch captures the admitted actor/generation. Logout/expiry and stale completions do not publish another user's state; already admitted operations retain their actor.
- No permission-sync, startup seeding or retired bootstrap workflow is reintroduced by UI-NEXT; preserve the existing security boundaries.

## RESP reset/lifecycle coverage

- Compatible package/provider loads in the real net48 test host. Reset works before the first business insert and after populated/failed tests.
- Migration runs once per reusable fixture. `dbo.SchemaVersions` and allocation ownership proof survive unchanged.
- Every mutable table is empty or deterministically seeded before the next test, including assignment/binding/security audit tables.
- The owned reset profile restores/verifies the StockLedger trigger, constraints, sequence contract and baseline identity behavior. Rowversion is treated as opaque and not rewound.
- Checkpoint belongs to one database/schema profile; metadata drift or unexpected tables/temporal objects reject reuse.
- Reset/seed/cleanup failure prevents subsequent test bodies from running on a poisoned fixture. Timed-out workers cannot overlap the next test.
- Independent testhosts allocate distinct owned databases; interrupted allocations require manual operator review. Run SQL projects sequentially and preserve existing assembly guards. Catalog has no assembly guard; its accepted explicit collection serializes every shared-fixture consumer. There is no external lease or automatic orphan recovery.
- Every original logical test and Theory row remains accounted for. Dedicated migration/schema/principal/fault tests continue to use fresh databases.

## Desktop and release

Pure ViewModel/session checks use deterministic data and clocks. Controls/message-loop/focus tests use STA and serialized GUI resources with bounded disposal and exception capture. No headless exception, DataError, printer, login or file dialog may stall the testhost.

Guarded STA/control tests, real-login packaged GUI Automation E2E with independent SQL assertions, and attended walkthroughs are separate evidence. The completed [GUI-P5 plan](archive/20261023-pre-ui-next/plans/20261019-001-full-winforms-gui-p5-acceptance.md) retains its inventory, bounded process work, sign-offs and T05 Down NOT TESTED exception. The [BCI design](BUSINESS_COMPACT_UI.md) and [acceptance record](BUSINESS_COMPACT_ACCEPTANCE.md) describe implemented inline editing, lookup, focus, accessibility, validation, dirty-state, read-only and session/permission behavior with candidate-specific completed evidence. Preserve that behavior; it is no longer merely proposed. Historical results cannot establish new UI-NEXT acceptance.

**Preserved lookup and inline behavior:** native per-character proof observes raw text, caret/range, focus and committed model effects across response rebinding, cancellation and shared notifications. Whole Text/WM_SETTEXT/UIA assignment is insufficient for a native typing claim. Preserve pending/committed identity, historical options, navigation admission, dirty/numeric/recovery guards, stable row identity, disposal and session replacement. Existing BCF tests are regression tools when affected, not a restarted T00–T06 queue.

**Historical visual acceptance:** [BCF acceptance](BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) retains its native contexts, matched references, exact package/test revisions and signed limitations. UI-NEXT defines fresh focused theme/layout/input/accessibility evidence in its own validation document; no former broad matrix is inherited automatically. Clean-machine installation, backup/recovery and signed release gates remain [UNACCEPTED / UNSCHEDULED prerequisites](DEFERRED_WORK.md#release-prerequisites).

P7 contract/HTTP parity, bearer authentication and modern-target reactivation remain [out of scope](DEFERRED_WORK.md#out-of-scope-directions). Any future activation needs its own approved scope and coverage decision. Do not create an API host to satisfy a test listed here.
