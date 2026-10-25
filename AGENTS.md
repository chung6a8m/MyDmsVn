# Codex instructions — MyDmsVn / v1a-pro-ui-next

Applies to the whole repository. Human-facing discussion is Vietnamese; technical documentation and identifiers are generally English unless an established runbook is Vietnamese.

## First read — current work selection (2026-10-25)

Read the [current execution index](docs/plans/README.md) first. It currently selects **no active execution plan** after the [2026-10-25 archive](docs/archive/20261025-pre-next-roadmap/README.md); the next roadmap has not been selected. Work only within explicit Owner authorization. When a new plan is selected, that plan owns its scope, task state and verification. Archived prompts, proposed successor phases and unfinished acceptance do not authorize implementation. Keep task progress out of this file and other entry pages.

[UI-NEXT](docs/ui-next/README.md) is a historical implementation/acceptance reference, not the current queue. Preserve its [T13 retained failures and bounded acceptance](docs/ui-next/evidence/20261024-uin-t13-integration.md), review-correction evidence and [DateInput candidate evidence](docs/ui-next/evidence/20261024-dateinput-unified-compact-correction.md). Its completed handoff prompts do not authorize continuing old PR branches or replaying tasks.

The [archive boundary](docs/archive/20261023-pre-ui-next/README.md) retires the previous execution queues. Their unfinished acceptance, full SAL integration repeats and production-release work are not automatically inherited by future work. Archiving does not complete or waive them. Preserve existing source behavior, historical evidence, original failures, exact source/test/package revisions and explicit accepted exceptions; the [former plan index](docs/archive/20261023-pre-ui-next/plans/README.md) is historical only.

SAL source is present in the merged baseline. R3/R4 source fixes were reviewed/approved at `f5039cb`; [T08 integration evidence](docs/evidence/sal/20261021-t08-integrated-acceptance.md) records product/package `f5039cb` and separate GUI test `4697744`, with candidate-specific core/SQL/package/native100 runnable automation PASS. Native125, attended observations and dated Owner acceptance remain OPEN in the archive. Full SAL acceptance and production readiness are not inferred. [BCF](docs/BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md), [BCI](docs/BUSINESS_COMPACT_ACCEPTANCE.md), RESP, L2DB and GUI-P5 remain completed only within their recorded signed scopes and exceptions. [Release prerequisites](docs/DEFERRED_WORK.md#release-prerequisites) remain UNACCEPTED / UNSCHEDULED.

## Documentation authority

Read in order: (1) this file and [AI_CONTEXT](AI_CONTEXT.md), (2) the [execution index](docs/plans/README.md), explicitly authorized task and its relevant design/verification references, (3) sole [TEST_POLICY](docs/TEST_POLICY.md), [TEST_STRATEGY](docs/TEST_STRATEGY.md), [tests/README](tests/README.md), (4) relevant [Architecture](docs/ARCHITECTURE.md), [Persistence](docs/PERSISTENCE.md), [Contracts](docs/CONTRACTS.md), [Security](docs/SECURITY.md), [Data model](docs/DATA_MODEL.md), [PRD](docs/PRD.md) and runbooks. The archive, [History](docs/HISTORY.md) and historical evidence are not current execution instructions.

The reading route is self-contained. Record decisions needed for this repository in its local architecture/ADR documents and authorized plans. Historical references to a separate proposal/documentation repository do not assign current decision ownership, require reading or synchronization, or add approval/publication prerequisites. Explicit Owner instructions determine scope; external proposal contents and PR status do not activate or expand it. Pinned dependency API/compatibility documentation and exact-candidate evidence remain relevant when a task needs them.

Source code/actual project graph and explicit owner decisions override stale examples. Plan/spec documentation alone is not implemented behavior or runtime verification. Label proposed types/APIs and record missing evidence honestly.

## Current code invariants

- Product, SQL, Desktop and all eleven active test projects target `net48`. Shipped Windows x86 `MyDmsVn.Desktop.App.exe` and separate admin `MyDmsVn.Server.DbMigrator.exe` are the active package forms. AppCore/net8.0-windows remains PARKED / NOT SHIPPED; the newer SDK is tooling.
- Local WinForms invokes Desktop.Application interfaces via the in-process Local adapter and Server.Application. Domain/Application remain independent of Infrastructure/WinForms/HTTP. Public DTOs stay transport-neutral.
- Persistence is LINQ to DB for Catalog/Inventory plus typed native SqlClient for other SQL consumers. Explicit UoW owns one connection/current transaction; typed `unitOfWork.Repository<TRepository>()` resolves interfaces. No ambient/static/global UoW, hidden connections, provider fallback or uncontrolled nested transactions. DbUp owns scripts/schema.
- Posting modifies receipt status, append-only ledger and derived balances atomically; optimistic rowversion and idempotent concurrent retries remain. Independent SQL checks establish transaction/concurrency behavior.
- Permissions require a code-defined Active key and current active identity; unknown/deprecated deny before superuser, configured superuser matches protected DB binding, ordinary direct Deny wins over role grants. No permission sync, automatic Administrator grants or privileged client bootstrap.
- Preserve PasswordHash/PasswordSalt and algorithm-aware verification. Legacy release support/exclusion requires explicit verified choice. Do not include secrets in logs, packages, screenshots or source.
- Admin migration/maintenance and limited runtime SQL identities stay separate. Development Desktop preflight requires scripts 001–010; the migrator admits Empty/exact10 and separately validated exact9 only under the documented SQL-login-only restricted maintenance boundary. Prefixes001–008 and partial/unknown schemas remain refused. Shipped pre-SAL evidence remains schema9. Local direct-SQL retains its documented trust limitation.

## Implemented Sales and inventory constraints

Preserve the Owner's seven sales columns and independent duplicate-Product rows. Selling prices are manual; gifts retain price but have zero revenue and full quantity/cost. Exclude payments, receivables, returns and new pricing/tax workflows. Cost is moving average per warehouse/product at actual Post, immutable afterward.

Use the shared exact fixed-point arithmetic, full-depletion and cumulative allocation rules. Receipt Post, sale Post and reconciliation must retain the common transaction-owned barrier and sorted stock-key locks together. Schema10 requires validated schema9 bootstrap, explicit migration admission, schema/grant/reset coverage and no mixed old-client writers. Keep historical schema9 package facts distinct from implemented schema10 source and its archived, incomplete acceptance boundary. Receipt/sale/reconciliation already share the source protocol; no mixed writer deployment is authorized.

Sales queries use LINQ to DB, Product `Any` membership and `SaleId DESC` keyset pages, with bound/validated cursors and all-line totals. No new OFFSET contract, mandatory count or arbitrary page jump. Existing module pagination remains source-owned. Raw SQL exceptions require a narrow SQL-specific reason and the same owned connection/transaction.

## Preserved UI and session boundaries

Use the pinned SourceGrid lookup/formatted adapters and existing app-owned inline grid/session/parser/accessibility code. Preserve pending versus committed lookup semantics, historical header options, active-only optional list-filter choices, strict numeric value/precision bounds, stable row identity, cancellation and session disposal.

Correct ordinary lookup rebinding without reselecting an unchanged committed identity. Native per-character proof must observe raw text, caret/range, focus and committed model/filter effects, including canceled preceding operations and shared warehouse notifications. Whole-string Text/UIA assignment is not per-key evidence. Keep explicit cancellation on admitted document boundaries.

Preserve full editability checks for Posted, busy, access denial and `RequiresPostOutcomeReload`; commit buffers before Save/Post decisions. New Back navigation must use navigation admission before focus validation, preserve rejected edits, cancel admitted query-only presentation and permit clean Posted inspection. Keep the same document/control/adapter across presentation modes; uncertain-Post reload and opaque rowversion remain authoritative, with no implicit Post retry.

Implemented Sales contracts/permissions, schema10/grants/reset, valued receipt posting, Exclusive reconciliation, Draft persistence/detail and atomic Sales Post remain source contracts. Preserve stable line IDs, authoritative responses materialized before commit and the coherent receipt/sale/reconciliation protocol. The archive records remaining native125/attended/Owner acceptance gaps. Vendor changes must address a proven narrow seam, preserve compatibility/ownership, have their own evidence and be pinned explicitly; no blanket dependency upgrades or editing a submodule without a traceable upstream change. Do not silently replace the grid.

## UI ownership

The current consumer checkout is `vendor/MyDmsVn.WinFormsKit`, containing the compatible UI/adapter projects and pinned SourceGrid engine. Read its actual gitlink and upstream instructions before a dependency change. Former nested BootstrapSourceGrid/UI paths in archived records are historical, not current checkout instructions.

The shared UI library owns generic rendering, native semantics, theme tokens and control lifecycle. MyDmsVn owns workspace identity, session/document generations, dirty-close admission, validation mapping, operation cancellation and uncertain-Post recovery. Keep WinForms controls out of Desktop.Application and public transport DTOs. Reuse the existing toast container, alert, validation state and spinner before proposing a narrow shared primitive; new public APIs require traceable upstream work and explicit pins.

Originating workspace instance and session/document lifetime govern async presentation; the selected tab does not identify a completed operation's owner. Closing/signing out must invalidate late callbacks and clear sensitive presentation. Busy visuals do not authorize commands, cancel a committed transaction or replace existing editability checks. Preserve raw edit buffers and recovery state; never replay Post automatically.

## Implementation and test protocol

1. Declare current task, changed behavior, exact source/vendor baseline, target/environment and smallest affected test scope.
2. Inspect checked-in source, project/lock files and relevant tests before implementation. For a docs-only change, verify diff, local links, planned/source API distinctions, roadmap/status and encoding; do not run SQL/GUI merely for Markdown.
3. Add meaningful behavior/error-boundary tests. Use focused direct project/filter runs for the changed behavior and the explicitly authorized scope (and a selected plan when present) recorded through the [execution index](docs/plans/README.md). Broaden only for an affected shared contract or a named current task gate. Archived SAL/BCF gates and the old six-owner fresh/reuse round are not inherited. [TEST_POLICY](docs/TEST_POLICY.md) governs.
4. Use ordinary `dotnet test`/Test Explorer and actual TRX. The old gate manifest, SqlTestHarness, standalone SQL runner/tests, approval files and global lease/fingerprint tooling remain removed.
5. SQL: explicitly designate non-production `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` with effective master; run owners sequentially, verify allocation identity/ownership token before cleanup, drain workers/connections and quarantine uncertainty. Never bulk-drop by prefix or use application databases.
6. GUI: interactive Windows, serialized STA, WinFormsTestGuard exception/DataError capture, finite waits and disposal. StaTest cannot kill a noncooperative STA; fatal/hang/real-package probes use the existing GUI-owned bounded subprocess and verified cleanup. Test-session injection does not prove actual Login.
7. Record source/test/package hashes, environment, actual selected/discovered/executed Facts/Theory rows, outcomes, resources, sanitized evidence and remaining risks. Previous counts or missing/skipped mandatory cases never establish a new PASS.

## Editing and completion

Follow `.editorconfig` and `.gitattributes`: Markdown UTF-8 without BOM and CRLF in the working tree, normalized LF in Git blobs; C# retains its declared BOM/CRLF. Preserve Vietnamese diacritics, pinned submodules and historical evidence. Do not commit local connections, raw proprietary TRX/SQL identifiers or unreviewed screenshots.

Completion is assessed against the explicitly authorized scope and, when present, the sole plan selected by the [execution index](docs/plans/README.md), with actual candidate evidence and explicit limitations. A no-active-plan state does not complete archived acceptance. Historical UI-NEXT/SAL/BCF acceptance and operational release acceptance remain separate. Report the actual boundary reached; do not reactivate archived/deleted plans or turn a design document into product/production acceptance.
