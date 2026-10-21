# Test coverage — MyDmsVn

**Updated:** 2026-10-21. This document owns required behavioral coverage; [TEST_POLICY](TEST_POLICY.md) alone governs execution scope and [tests/README](../tests/README.md) has actual commands. Current production persistence is LINQ to DB and typed native SqlClient, with accepted test-only Respawn fixtures. L2DB-T07 accepted scoped correctness/package/limited Local smoke; its 139 Desktop GUI invocations were NOT RUN at that frozen revision. GUI-P5 and [BCI](BUSINESS_COMPACT_ACCEPTANCE.md) are completed history with their original evidence, exceptions and signed scopes retained. PR #40 is merged at `v1a-pro@6040d7bcfca698fc9daba8268bb94e4454e8eacb`. [BCF — lookup repair and visual parity](plans/20261021-001-business-compact-lookup-and-visual-parity.md) is COMPLETE / OWNER ACCEPTED; its [acceptance ledger](BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) and [final record](evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21) retain exact evidence and bounded exceptions.

## Active SAL coverage — bounded T00/T01 evidence; later obligations open

[SAL acceptance](DIRECT_SALES_ACCEPTANCE.md) owns traceable rows for the sole [active plan](plans/20261021-002-direct-sales-and-moving-average-costing.md). T00 pure foundations and [T01 schema/bootstrap](evidence/sal/20261022-t01-schema-bootstrap.md) have scoped evidence; the full coherent writer/workflow and frozen T08 scope remains open. New tests must establish:

- Exact arithmetic at scale/precision limits and midpoint ties, representable results with large intermediates, cumulative duplicate/gift allocation and zero residual at full depletion.
- Atomic schema10/bootstrap from exact validated schema9, source/ledger/balance bidirectional checks, rollback with journal, clean/no-op install and old/new/unsupported-client refusal.
- Valued Goods Receipt, sales Draft/Post, current actor/permissions, stale versions/line identities, immutable Posted data, grouped insufficient stock, injected rollback and one economic effect for repeated/concurrent Post.
- Independent-session sale/sale, sale/receipt, missing-key and reverse multi-key contention, reconciliation barrier, finite failures and quantity/value/source conservation.
- LINQ SQL translation/parameterization, Product Any without duplicated headers, complete receipt totals, keyset forward/reverse/look-ahead, malformed/mismatched cursors, live membership and cancellation.
- Local/VM session generations, raw buffers and uncertain-outcome recovery; seven-column UI with native per-key lookups, duplicate-row identity, gift checkbox, read-only cost and accessibility in required DPI/themes.
- Explicit grants/registry/mapping/fixture reset coverage, real Login package workflow, independent SQL results and positive resource cleanup on the frozen candidate.

The Inventory SQL owner remains the owner for sales/value persistence; do not create a new SQL harness/project. BCF coverage below describes completed historical obligations. Broadening and exact commands follow TEST_POLICY and the SAL plan.

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
- No permission-sync, startup seeding or retired bootstrap workflow is reintroduced by BCF; preserve the completed BCI boundaries.

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

Guarded STA/control tests, real-login packaged GUI Automation E2E with independent SQL assertions, and attended walkthroughs are separate evidence. The completed [GUI-P5 plan](plans/20261019-001-full-winforms-gui-p5-acceptance.md) retains its inventory, bounded process work, sign-offs and T05 Down NOT TESTED exception. The [BCI design](BUSINESS_COMPACT_UI.md) and [acceptance record](BUSINESS_COMPACT_ACCEPTANCE.md) describe implemented inline editing, lookup, focus, accessibility, validation, dirty-state, read-only and session/permission behavior with candidate-specific completed evidence. Preserve that behavior; it is no longer merely proposed. Historical results cannot establish SAL acceptance.

**BCF lookup coverage (T00–T02):** prove native per-character `g`, `u`, `i` input in exactly `WarehouseFilterLookup`, `WarehouseLookup` and `EmployeeLookup`, including null filter and unchanged committed header IDs. Control response completion and cancellation of a preceding search so binding/ApplyState occurs between keys; assert correct `gui`, caret/range and pending state after each step. Include middle insertion, range replacement, no query-only commit, actual Down/Enter/Escape/Tab/Shift+Tab, history/inactive options, latest-result fencing, New/select/Refresh guards, busy/denied/Posted/RequiresPostOutcomeReload, disposal and session replacement. Inline Product remains a reference/regression path. Verify native HWND ownership/focus and observe selection; whole `.Text`, WM_SETTEXT or UIA SetValue cannot replace per-key proof. T00 retains expected baseline product failures; proposed typing/binding test classes must be implemented before their selectors are usable. T02 must pass the focused Windows native100/125 matrix in both Light and Dark plus attended typing smoke before any T03 visual implementation.

**BCF visual/integration coverage (T03–T06):** focused checks first cover shared shell/Light/Graphite, Products catalog content/inspector and detail-focused receipt/commands against all three mockups. Require same-data comparisons, interactive states, layout/focus/error reachability at 1680×900 and 1200×760 logical targets plus 944×600, and native100/125 observations. T06 freezes revisions, runs complete Desktop.Session once and complete Desktop GUI at each native100/125 context, executes both-theme cases, repeats the lookup proof and records the bounded actual-Login package/attended scope and Owner review. Use independent SQL assertions where the selected package workflows require them; do not run the entire SQL suite for every binding/layout edit. [TEST_POLICY](TEST_POLICY.md) governs execution and [BCF acceptance](BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) maps the evidence. Clean-VM install/upgrade, credentials/diagnostics, backup/restore and signed G1–G5 remain [unaccepted, unscheduled release prerequisites](DEFERRED_WORK.md#release-prerequisites).

P7 contract/HTTP parity, bearer authentication and modern-target reactivation remain [out of scope](DEFERRED_WORK.md#out-of-scope-directions). Any future activation needs its own approved scope and coverage decision. Do not create an API host to satisfy a test listed here.
