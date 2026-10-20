# Codex instructions — MyDmsVn / v1a-pro

Applies to the whole repository. Human-facing discussion is Vietnamese; technical documentation and identifiers are generally English unless an established runbook is Vietnamese.

## First read — current roadmap (2026-10-20)

The owner selected [Business Compact and inline Goods Receipt editing (BCI)](docs/plans/20261020-001-business-compact-inline-goods-receipt.md) as the **sole active roadmap** and requested removal of all older unimplemented plans. Read [the design](docs/BUSINESS_COMPACT_UI.md), [acceptance matrix](docs/BUSINESS_COMPACT_ACCEPTANCE.md) and [execution index](docs/plans/README.md). BCI-T00 native grid/editor/accessibility proof is complete in its retained test-host scope; see [observed evidence and decision](docs/BUSINESS_COMPACT_ACCEPTANCE.md#81-bci-t00-native-editor-proof--2026-10-20). BCI-T01 lossless scalar numeric parser is complete; see [scalar evidence](docs/BUSINESS_COMPACT_ACCEPTANCE.md#82-bci-t01-lossless-scalar-conversion--2026-10-20). BCI-T02 projection/session foundation is complete in its adapter-host scope; see [evidence](docs/BUSINESS_COMPACT_ACCEPTANCE.md#83-bci-t02-stable-projection-and-cell-sessions--2026-10-20). BCI-T03 lookup/command integration is complete in its recorded focused control-test scope; see [evidence](docs/BUSINESS_COMPACT_ACCEPTANCE.md#84-bci-t03-lookup-and-command-integration--2026-10-20). BCI-T04 editable native accessibility is complete in its scoped hosted/package evidence; see [evidence](docs/BUSINESS_COMPACT_ACCEPTANCE.md#85-bci-t04-editable-native-accessibility--2026-10-20). BCI-T05 Business Compact layout is complete in its scoped hosted native 100%/125% evidence; see [T05 layout evidence](docs/BUSINESS_COMPACT_ACCEPTANCE.md#86-bci-t05-business-compact-layout--2026-10-20). BCI-T06 is in progress; [current frozen evidence](docs/BUSINESS_COMPACT_ACCEPTANCE.md#87-bci-t06-frozen-integrated-ui-acceptance--2026-10-20) records the exact candidate and pending native125/attended gates. Do not mark BCI complete until those gates and explicit review exist.

RESP, L2DB and GUI-P5 are completed historical baselines, not tasks to restart. GUI-P5's [final signed scope](docs/GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20) supersedes earlier OPEN checkpoints; keep product/test revision distinctions and the T05 Warehouse 125% Dark Down NOT TESTED exception. Its accepted external-editor flow does not certify a future inline grid.

The unimplemented REL/P6 plan and .NET 8 reactivation procedure are deleted; the old future-work checklists are retired. [Release prerequisites](docs/DEFERRED_WORK.md#release-prerequisites) remain unaccepted/unscheduled constraints, not an active plan. Do not infer production readiness from retirement or the documentation PR.

## Documentation authority

Read in order: (1) this file and [AI_CONTEXT](AI_CONTEXT.md), (2) [plan index](docs/plans/README.md), the active task and its design/acceptance documents, (3) sole [TEST_POLICY](docs/TEST_POLICY.md), [TEST_STRATEGY](docs/TEST_STRATEGY.md), [tests/README](tests/README.md), (4) relevant [Architecture](docs/ARCHITECTURE.md), [Persistence](docs/PERSISTENCE.md), [Contracts](docs/CONTRACTS.md), [Security](docs/SECURITY.md), [Data model](docs/DATA_MODEL.md), [PRD](docs/PRD.md) and runbooks. [History](docs/HISTORY.md) and completed evidence are not current execution instructions.

Source code/actual project graph and explicit owner decisions override stale examples. Plan/spec documentation alone is not implemented behavior or runtime verification. Label proposed types/APIs and record missing evidence honestly.

## Current code invariants

- Product, SQL, Desktop and all ten active test projects target `net48`. Shipped Windows x86 `MyDmsVn.Desktop.App.exe` and separate admin `MyDmsVn.Server.DbMigrator.exe` are the active package forms. AppCore/net8.0-windows remains PARKED / NOT SHIPPED; the newer SDK is tooling.
- Local WinForms invokes Desktop.Application interfaces via the in-process Local adapter and Server.Application. Domain/Application remain independent of Infrastructure/WinForms/HTTP. Public DTOs stay transport-neutral.
- Persistence is LINQ to DB for Catalog/Inventory plus typed native SqlClient for other SQL consumers. Explicit UoW owns one connection/current transaction; typed `unitOfWork.Repository<TRepository>()` resolves interfaces. No ambient/static/global UoW, hidden connections, provider fallback or uncontrolled nested transactions. DbUp owns scripts/schema.
- Posting modifies receipt status, append-only ledger and derived balances atomically; optimistic rowversion and idempotent concurrent retries remain. Independent SQL checks establish transaction/concurrency behavior.
- Permissions require a code-defined Active key and current active identity; unknown/deprecated deny before superuser, configured superuser matches protected DB binding, ordinary direct Deny wins over role grants. No permission sync, automatic Administrator grants or privileged client bootstrap.
- Preserve PasswordHash/PasswordSalt and algorithm-aware verification. Legacy release support/exclusion requires explicit verified choice. Do not include secrets in logs, packages, screenshots or source.
- Admin migration/maintenance and limited runtime SQL identities stay separate. Desktop does read-only schema preflight for scripts 001–009; the migrator admits Empty/Compatible, not unsupported old-prefix auto upgrade. Local direct-SQL retains its documented trust limitation.

## BCI implementation boundaries

Use the pinned SourceGrid lookup/formatted adapters first. Proposed app-owned grid/session/parser/accessibility classes must preserve committed versus pending lookup semantics, historical options, strict numeric value/precision bounds, correct row identity, cancellation and session disposal.

Before adding editors, replace active-cell-destructive redraw and gate edit-start/commit with full editability, including Posted, busy, access denial and `RequiresPostOutcomeReload`. Commit buffers before Save/Post decisions; preserve uncertain-Post reload, opaque rowversion and no implicit Post retry. Do not reuse read-only accessible-cell semantics for writable cells.

BCI adds no API/schema/permission/module/runtime scope. Vendor changes must address a proven narrow seam, preserve compatibility/ownership, have their own evidence and be pinned explicitly; no blanket dependency upgrades or editing a submodule without a traceable upstream change. Alternative grid selection is an explicit recorded decision, not silent fallback.

## Implementation and test protocol

1. Declare current task, changed behavior, exact source/vendor baseline, target/environment and smallest affected test scope.
2. Inspect checked-in source, project/lock files and relevant tests before implementation. For a docs-only change, verify diff, local links, planned/source API distinctions, roadmap/status and encoding; do not run SQL/GUI merely for Markdown.
3. Add meaningful behavior/error-boundary tests. Use focused direct project/filter runs during implementation; broaden for affected shared behavior and the BCI integration milestone. [TEST_POLICY](docs/TEST_POLICY.md) governs.
4. Use ordinary `dotnet test`/Test Explorer and actual TRX. The old gate manifest, SqlTestHarness, standalone SQL runner/tests, approval files and global lease/fingerprint tooling remain removed.
5. SQL: explicitly designate non-production `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` with effective master; run owners sequentially, verify allocation identity/ownership token before cleanup, drain workers/connections and quarantine uncertainty. Never bulk-drop by prefix or use application databases.
6. GUI: interactive Windows, serialized STA, WinFormsTestGuard exception/DataError capture, finite waits and disposal. StaTest cannot kill a noncooperative STA; fatal/hang/real-package probes use the existing GUI-owned bounded subprocess and verified cleanup. Test-session injection does not prove actual Login.
7. Record source/test/package hashes, environment, actual selected/discovered/executed Facts/Theory rows, outcomes, resources, sanitized evidence and remaining risks. Previous counts or missing/skipped mandatory cases never establish a new PASS.

## Editing and completion

Follow `.editorconfig` and `.gitattributes`: Markdown UTF-8 without BOM and CRLF in the working tree, normalized LF in Git blobs; C# retains its declared BOM/CRLF. Preserve Vietnamese diacritics, pinned submodules and historical evidence. Do not commit local connections, raw proprietary TRX/SQL identifiers or unreviewed screenshots.

BCI completion requires its new frozen UI/package evidence. Operational release acceptance remains outside the active roadmap and unaccepted. Report the actual boundary reached; do not reactivate deleted plans or turn a design document into product/production acceptance.
