# Codex instructions — MyDmsVn / v1a-pro

Applies to the whole repository. Human-facing discussion is Vietnamese; technical documentation and identifiers are generally English unless an established runbook is Vietnamese.

## First read — current roadmap (2026-10-21)

After merging PR #40 into `v1a-pro` at `6040d7bcfca698fc9daba8268bb94e4454e8eacb`, the owner selected [Business Compact refinement (BCF)](docs/plans/20261021-001-business-compact-lookup-and-visual-parity.md) as the **sole active roadmap**. Read [the design](docs/BUSINESS_COMPACT_REFINEMENT.md), [acceptance ledger](docs/BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md), [three reference images](docs/design-references/business-compact/README.md) and [execution index](docs/plans/README.md). BCF-T00 is COMPLETE as a test-only RED checkpoint: [15 intended native-key failures](docs/evidence/bcf/20261021-t00-native-typing.md). BCF-T01 binding repair is COMPLETE with [focused hosted Windows evidence](docs/evidence/bcf/20261021-t01-lookup-binding.md). BCF-T02 is COMPLETE / lookup gate PASS with [native100/125 Light/Dark and attended evidence](docs/evidence/bcf/20261021-t02-lookup-gate.md). T03 shell/theme is implemented with partial verification: [evidence and native125 wide-viewport blocker](docs/evidence/bcf/20261021-t03-chrome-followup.md). T03 remains OPEN; T04 Products is IMPLEMENTED / focused Windows verification PASS; see [T04 evidence](docs/evidence/bcf/20261021-t04-products-inspector.md). T05 receipt presentation is IMPLEMENTED / final focused verification in progress; T06 remains NOT STARTED. Visual/full BCF Owner acceptance and release acceptance remain ungranted.

Execute T00 native per-key reproduction, T01 binding repair and T02 focused Windows gate for exactly `WarehouseFilterLookup`, `WarehouseLookup` and `EmployeeLookup`. **T02 must pass at native100/125 in Light/Dark, with attended lookup observations, before visual implementation.** Then T03 shared shell/theme, T04 Products content/inspector, T05 guarded receipt List/Detail presentation and T06 frozen integrated comparison/Owner acceptance. This explicitly expands the historical BCI catalog exclusion and always-visible receipt split; no new business fields or localization is implied.

BCI-T00–T06 are COMPLETE / OWNER ACCEPTED and merged through PR #40. Preserve its [final signed scope](docs/BUSINESS_COMPACT_ACCEPTANCE.md#final-t06-acceptance-and-handoff--2026-10-21), exact candidate/test/package revisions, failures and original evidence. BCF repairs pre-existing list/header binding defects and adds new visual obligations; old acceptance does not certify those obligations. Operational release prerequisites remain unaccepted/unscheduled.

RESP, L2DB and GUI-P5 are completed historical baselines, not tasks to restart. GUI-P5's [final signed scope](docs/GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20) supersedes earlier OPEN checkpoints; keep product/test revision distinctions and the T05 Warehouse 125% Dark Down NOT TESTED exception. Its accepted external-editor flow does not substitute for BCI or BCF evidence.

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

## BCF implementation boundaries

Use the pinned SourceGrid lookup/formatted adapters and existing app-owned inline grid/session/parser/accessibility code. Preserve pending versus committed lookup semantics, historical header options, active-only optional list-filter choices, strict numeric value/precision bounds, stable row identity, cancellation and session disposal.

Correct ordinary lookup rebinding without reselecting an unchanged committed identity. Native per-character proof must observe raw text, caret/range, focus and committed model/filter effects, including canceled preceding operations and shared warehouse notifications. Whole-string Text/UIA assignment is not per-key evidence. Keep explicit cancellation on admitted document boundaries.

Preserve full editability checks for Posted, busy, access denial and `RequiresPostOutcomeReload`; commit buffers before Save/Post decisions. New Back navigation must use navigation admission before focus validation, preserve rejected edits, cancel admitted query-only presentation and permit clean Posted inspection. Keep the same document/control/adapter across presentation modes; uncertain-Post reload and opaque rowversion remain authoritative, with no implicit Post retry.

BCF adds no API/schema/permission/module/runtime scope. Vendor changes must address a proven narrow seam, preserve compatibility/ownership, have their own evidence and be pinned explicitly; no blanket dependency upgrades or editing a submodule without a traceable upstream change. Do not silently replace the grid.

## Implementation and test protocol

1. Declare current task, changed behavior, exact source/vendor baseline, target/environment and smallest affected test scope.
2. Inspect checked-in source, project/lock files and relevant tests before implementation. For a docs-only change, verify diff, local links, planned/source API distinctions, roadmap/status and encoding; do not run SQL/GUI merely for Markdown.
3. Add meaningful behavior/error-boundary tests. Use focused direct project/filter runs during implementation; broaden for affected shared behavior, the BCF-T02 input gate and BCF-T06 integration milestone. [TEST_POLICY](docs/TEST_POLICY.md) governs.
4. Use ordinary `dotnet test`/Test Explorer and actual TRX. The old gate manifest, SqlTestHarness, standalone SQL runner/tests, approval files and global lease/fingerprint tooling remain removed.
5. SQL: explicitly designate non-production `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` with effective master; run owners sequentially, verify allocation identity/ownership token before cleanup, drain workers/connections and quarantine uncertainty. Never bulk-drop by prefix or use application databases.
6. GUI: interactive Windows, serialized STA, WinFormsTestGuard exception/DataError capture, finite waits and disposal. StaTest cannot kill a noncooperative STA; fatal/hang/real-package probes use the existing GUI-owned bounded subprocess and verified cleanup. Test-session injection does not prove actual Login.
7. Record source/test/package hashes, environment, actual selected/discovered/executed Facts/Theory rows, outcomes, resources, sanitized evidence and remaining risks. Previous counts or missing/skipped mandatory cases never establish a new PASS.

## Editing and completion

Follow `.editorconfig` and `.gitattributes`: Markdown UTF-8 without BOM and CRLF in the working tree, normalized LF in Git blobs; C# retains its declared BOM/CRLF. Preserve Vietnamese diacritics, pinned submodules and historical evidence. Do not commit local connections, raw proprietary TRX/SQL identifiers or unreviewed screenshots.

BCF completion requires its new frozen UI/package evidence and explicit comparison/Owner acceptance against all three reference designs. Operational release acceptance remains outside the active roadmap and unaccepted. Report the actual boundary reached; do not reactivate deleted plans or turn a design document into product/production acceptance.
