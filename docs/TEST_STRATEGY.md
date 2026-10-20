# Test coverage — MyDmsVn

**Updated:** 2026-10-20. This document owns required behavioral coverage; [TEST_POLICY](TEST_POLICY.md) alone governs execution scope and [tests/README](../tests/README.md) has actual commands. Current production persistence is LINQ to DB and typed native SqlClient, with accepted test-only Respawn fixtures. L2DB-T07 accepted scoped correctness/package/limited Local smoke; its 139 Desktop GUI invocations were NOT RUN at that frozen revision. Later [GUI-P5](plans/20261019-001-full-winforms-gui-p5-acceptance.md) acceptance is complete in its approved scope, with recorded exceptions retained. [BCI](plans/20261020-001-business-compact-inline-goods-receipt.md) is the sole active roadmap; T00–T03 have scoped implementation/evidence and T04–T06 remain pending; [Business Compact acceptance](BUSINESS_COMPACT_ACCEPTANCE.md) maps its required new evidence.

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
- No permission-sync, startup seeding or retired bootstrap workflow is reintroduced by BCI.

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

Guarded STA/control tests, real-login packaged GUI Automation E2E with independent SQL assertions, and attended walkthroughs are separate evidence. The completed [GUI-P5 plan](plans/20261019-001-full-winforms-gui-p5-acceptance.md) records historical inventory, bounded process work and both P5 sign-offs, including the T05 Down NOT TESTED exception. The [Business Compact design](BUSINESS_COMPACT_UI.md) and [acceptance specification](BUSINESS_COMPACT_ACCEPTANCE.md) define proposed inline edit, lookup, focus, accessibility, validation, dirty-state, read-only and session/permission coverage; no historical GUI result proves those new behaviors. [TEST_POLICY](TEST_POLICY.md) governs focused work and frozen-candidate native 100%/125% Light/Dark, real-package Login and independent SQL evidence. Clean-VM install/upgrade, credentials/diagnostics, backup/restore and signed G1–G5 remain [unaccepted, unscheduled release prerequisites](DEFERRED_WORK.md#release-prerequisites).

P7 contract/HTTP parity, bearer authentication and modern-target reactivation remain [out of scope](DEFERRED_WORK.md#out-of-scope-directions). Any future activation needs its own approved scope and coverage decision. Do not create an API host to satisfy a test listed here.

