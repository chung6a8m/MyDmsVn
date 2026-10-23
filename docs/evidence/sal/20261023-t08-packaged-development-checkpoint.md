# SAL-T08 packaged development checkpoint — 2026-10-23

Status: task-review round1 fix candidate, awaiting scoped rereview. Feasible T08 work is authorized by the Owner's “Tiếp tục” under the recorded continuation ruling. T07 native125, attended review, frozen integration/package and final Owner acceptance remain OPEN. This document records development evidence, not feature or production acceptance.

## Baselines and scope

Initial clean BASE is `9d8a199a27bc505f58b5997fd611ce249c26c00d`; the T07 product checkpoint is `879ab7bc6046d598fe06be1d95614df2b02a5810`. No product, vendor, project or dependency-pin source changed in this checkpoint. BootstrapSourceGrid remains `2bd7874f7e80b22ef181763749c2a2938ab7f16e`, Bootstrap5WinFormUI `2be9a4d778fda7016207b75470020be6126f2f3f`, SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`.

The controller approved a genuine clean BASE development package before tracked edits, with the normal publisher and no SkipPublish or source commit:

```powershell
& ./deployment/release/Publish-LocalRelease.ps1 -Version '1.0.0-sal-t08-dev' -OutputRoot $t08Output *> '.local/t08/dev-package-original.log'
```

The original invocation resolved `$t08Output` to the dedicated empty worktree directory `.local/t08/dev-base9d8a199`. Manifest SHA256 is `44F51DDBD990D64909BA39E4C0E4380A15B82A27AA4459D680E2AEC41E018918`: clean source BASE, schema10, two net48/win-x86 packages, 205 files. Actual lengths/hashes are checked by package admission and independent controller audits. This package is not the final T08 candidate.

Actual historical schema9 execution uses the immutable BCF artifact source `95591071ea214be88813deb5227d216f1b0bca71`, manifest SHA256 `3376D54C49FAD98152CB5A8BC7F94EDFEF8F7E9D13B15A16A3A126D642347447`. Both executable PE headers and all manifest files are validated without assembly-identity unification. Historical artifacts/evidence were not changed.

## Implemented cases

The existing Desktop test owner/probe/owned SQL topology is extended; there is no new runner, global lease, connection fallback, test engine or production hook.

| Method in PackagedSalesAcceptanceTests.cs | Rows | Observable |
|---|---:|---|
| Current_and_historical_package_metadata_remain_distinct_in_one_process | 1 | Schema9/10 manifest distinction, current exact scripts, actual x86 Desktop/admin headers and package hashes |
| Actual_sales_list_accepts_native_numeric_page_size | 2 | Actual ordinary Login; owned native EDIT below Page size; normal validation and applied List |
| DirectSales_CostAndStockMatchIndependentOracle | 2 | Distinct duplicate Product rows; paid qty3@25 and gift qty2@40, price retained; Save/reload/Post; distinct Product-filter fixture excludes newest other-only11 and checks mixed whole16 versus matching2, pure3/golden75 and exact cursor/header order; independent cost45+30, revenue75/profit0/Q15/V225; later receipt cannot change stored cost |
| Two_actual_sales_clients_reject_stale_save_or_post_without_duplicate_effect | 4 | Two actual Logins, winner rowversion/full line snapshot before loser; losing Save input retained; exact winner version/lines/Q/V/ledger unchanged; stale Post one economic effect and authoritative reload |
| Actual_sales_direct_deny_clears_protected_presentation_without_mutation | 6 | Direct Read/Write/Post Deny beats role grant; protected public presentation cleared/locked; independent stored version/lines/Q/V/ledger unchanged |
| Actual_sales_sql_post_timeout_requires_authoritative_reload_and_one_explicit_retry | 2 | Owned runtime observed in real SQL WAITFOR; locked uncertain state; explicit authoritative reload and at most one checked retry; exact independent effects |
| Actual_packages_enforce_schema9_quiesced_admin_upgrade_and_schema10_runtime_boundary | 2 | Separate partial-refusal/pristine9 fixtures; real new Desktop9/partial refusal, restricted writer fencing/drain, admin upgrade/no-op with immutable legacy IDs/audit/token/journal and exact schema10 row/token/journal snapshots plus0scripts, runtime-admin refusal, genuine old Desktop10 refusal and current Login/relogin/restart |

All theme-dependent methods have separate Light/Dark rows. Discovery is 19 rows across seven methods. Packaged UIA SetValue evidence is separate from T07 per-character keyboard scope. Partial9 refusal never drops its added column to manufacture a pristine metadata baseline; the second fixture starts pristine9. Auth403 follows the existing scrub contract, while ordinary stale errors preserve losing input.

Changed test files are `tests/MyDmsVn.Desktop.Tests/{PackagedGuiAcceptanceTests,PackagedSalesAcceptanceTests,GuiPackageProbe,GuiSqlEnvironment,GuiSqlSalesEnvironment,GuiSqlSchemaTransition}.cs`. New public product interfaces: none. The shared current package/journal admission is updated narrowly from9 to10; receipt/no-write SQL oracles include value companions/Sales tables. Existing historical GUI-P5 evidence remains immutable.

## Actual development execution

All commands target `tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj`, Release/net48, with both `-p:PlatformTarget=x86` and `-- RunConfiguration.TargetPlatform=x86`. Numbered attempts build directly with `dotnet build ... -c Release -f net48 -p:PlatformTarget=x86 --no-restore`, then execute ordinary `dotnet test` with `--no-build --no-restore`, actual filters and TRX. Testhost reports32bit. No complete SQL-owner pair, complete Session/GUI or frozen package was run.

| Original attempt | Actual P/F/S | Meaning |
|---|---|---|
| Initial admission RED | 0/1/0 | Intended obsolete maximum9 assertion versus actual10, before SQL |
| Initial admission-green filename | 0/1/0 | SQL service setup failure, not GREEN; subsequent human service restoration recorded |
| Native focus compile scope | 2/0/0 | Existing owned focus recovery/refusal, no SQL |
| Initial Sales runs (three separate invocations) | each0/2/0 | SQL seed reserved identifier, then summary selector, then Save/list driver setup; not product behavior RED |
| Numbered01–02 | each0/1/0 | Explicit List Refresh placed at wrong driver boundary; original failures preserved |
| Numbered03–04 | each0/2/0 | Numeric editor and historical assembly identity seams |
| Numbered05 | 1/2/0 | Metadata PASS; numeric and schema9 seed setup failures |
| Numbered06 | 0/2/0 | Numeric UIA provider diagnostic; held writer blocked fixture access-mode change |
| Numbered07 | 1/1/0 | Native page size PASS; upgrade reached but no-op failed without diagnostic |
| Numbered08 | 1/1/0 | Full A05Light PASS; no-op PartialSchema after prohibited partial-DDL repair |
| Numbered09 | 1/0/0 | Independent partial/pristine fixtures prove full schemaLight row |
| Numbered10 complete new19 native100 | 13/6/0 | Four second-client Note-readiness failures and two separate UI/driver failures; original full run remains FAIL |
| Numbered11 four stale rows | 4/0/0 | Narrow finite semantic Note readiness matches existing receipt pattern; actual conflict/economic assertions reached |
| Numbered12 stronger four stale rows | 4/0/0 | Added independent winner rowversion/full line XML before/after loser |
| Numbered13 A05Light + DenyReadLight | 2/0/0 | Diagnostic isolated rerun; both pass, original two transient causes remain unresolved |
| Numbered14 existing affected callers | 7/0/0 | Topology1, receipt2, invalid2, disabled/direct-deny2; distinct from new19 |

Original complete19 command:

```powershell
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 -p:PlatformTarget=x86 --no-build --no-restore --filter 'FullyQualifiedName~Current_and_historical_package_metadata|FullyQualifiedName~Actual_sales_list_accepts|FullyQualifiedName~DirectSales_CostAndStockMatchIndependentOracle|FullyQualifiedName~Two_actual_sales_clients|FullyQualifiedName~Actual_sales_direct_deny|FullyQualifiedName~Actual_sales_sql_post_timeout|FullyQualifiedName~Actual_packages_enforce_schema9' --logger 'trx;LogFileName=t08-attempt10-new19-native100.trx' --results-directory '.local/t08/attempt10/results' -- RunConfiguration.TargetPlatform=x86
```

Later exact filters are `FullyQualifiedName~Two_actual_sales_clients` for11/12, `(FullyQualifiedName~DirectSales_CostAndStockMatchIndependentOracle|(FullyQualifiedName~Actual_sales_direct_deny&DisplayName~Read))&DisplayName~Light` for13, and `FullyQualifiedName~Disposable_gui_topology|FullyQualifiedName~Packaged_receipt_draft_reload_edit_post|FullyQualifiedName~Invalid_receipt_fields|FullyQualifiedName~Disabled_login_and_server_direct_deny` for14. Their original TRX names are respectively `t08-attempt11-stale-four.trx`, `t08-attempt12-stale-four-version-oracle.trx`, `t08-attempt13-a05-denyread-light-diagnostic.trx`, `t08-attempt14-existing-seven.trx`, in each `.local/t08/attemptNN/results`. Original logs/TRX remain local. Complete19 SHA256 is `27EAB3CBD9F10254F265C5B0B07445B708B06CBA82D5E525295851E74562B206`; stronger stale4 SHA256 is `753C5559108135C9D1BD79316653565C912EEFE4B5CFB88AA02136EB96E73767`; diagnostic2 SHA256 is `5E575381CE50BD0A7FE4A27CE515D7B78DE96AD6E63612E9A0251AEFF08A6154`; existing7 SHA256 is `70F5B348F7F6E2DE465928C7B9CEB3AB81530CDBD2C1C80100BCCD2250E580D7`.

The four stale failures occurred after first-client duplicate/gift reload and immediately after second-client asynchronous Open, before Note existed. The driver had read InputValue directly, unlike the existing receipt pattern's finite Find(Edit Note). The narrow readiness repair checks actual second-process ownership; no product change was needed. Both themes then pass losing-input and independent economic/version assertions.

The other original failures were DenyReadLight's first Product popup timeout before permission mutation, and A05Light after second Product commit/before Quantity completion. The original probe lacks enough failure-site/raw-state evidence to determine their causes. Isolated13 adds only method/row-stage and opt-in lookup diagnostics, with no business action/retry/delay change; UIA diagnostics can affect timing. Its Product editors were owned/focused/enabled/onscreen and both workflows completed. Attempt10 and13 have no overlapping case timestamps; assembly serialization and verified supervisor drain do not establish a transient cause. These two original failures remain unresolved/nonreproduced, not “fixed”. The final complete GUI must run both exact rows again on the frozen package.

## Provenance, resources and remaining work

Windows is10.0.26200; interactive session, SDK8.0.426, .NET Framework registry Release533509. Every packaged observation measured actual native100, monitor/window96DPI. No native125 emulation or attended marker was supplied. No new visual acceptance is claimed.

Raw local archives preserve source6 and test-output22DLL/PDB bytes before/after from attempt04 onward. Attempt01–03 have raw source on both sides, raw DLL/PDB before only and contemporaneously recorded post hashes pointing to then-live paths; later rebuilt live paths are not historical after bytes. The first six invocations lack contemporaneous raw source/DLL archives; no retroactive proof is claimed. Sales probes preserve their exact compiled source/executable before launch with hashes; legacy14 callers lack that additional dynamic before-launch archive and retain input-source/test-DLL proof. Raw TRX/captures/SQL names/connections are not committed.

SQL uses the designated non-production effective-master environment, existing owned allocation/principal identity checks, finite worker/process drain and tracked per-case cleanup. No prefix bulk-drop, fallback connection, privileged-client bootstrap or competing executor is used. Through14, all 57 resources are `DatabaseAndAllPrincipalsDroppedAfterVerifiedDrain`; no Desktop/GuiPackageProbe process remained at the final checkpoint. Failure supervisors verified cleanup. Native125, attended all-lookup/cost review and exact final Owner disposition remain NOT RUN/resource-blocked or OPEN as applicable.

The final independent archive audit rehashed every before/after raw source6 and22DLL/PDB archive for04–14: zero byte/manifest/prepost differences within each attempt. Current test DLL SHA256 is `75387C7619A50F7195FE135DD3ACDEB72A6BE5F0BBEADDB86BD4ED20C8987E25`; the current six source working-file hashes are retained in local `attempt14/source-after.csv`. Diagnostic13 compiled probe source SHA256 is `3E039BF21A6FE9015093BB6DD2985E08225371DF159EA6E7116F6560E2CA50B7`; its two actual executable hashes are `7D0F717577A5C73589C347E968B780FB2C5911AEDD3B8CBC5F58CD70D537F9BE` and `3237FFD38F96B4E98EB6F1686C9232A4440BC86BDAEB9D8BAFD36F366717CCF1`. All four archived files rehash correctly. The embedded-resource compiler writes source text, so its compiled-source raw bytes differ from the checked-in BOM source; no raw-byte identity is claimed between those forms. Numbered14 reused the exact unchanged13 source/test DLL.

Next boundary is task review of this test/docs candidate, then the single whole-branch source review/fix wave including deferred T01R3 diagnostics and T07R4 stale numeric footer. Only afterward may the frozen source/package and complete prescribed Application/Architecture, six-owner fresh/reuse pair, Session and feasible GUI/package evidence execute. Full SAL acceptance and operational production prerequisites remain separate and unaccepted.

## Task review round1 fixes

Review of candidate `0a789895798e2491df8ff26bb3b641f88d8b7380` identified two Important oracle gaps and one adjacent README Minor. These findings establish insufficient test discrimination, not production defects. Original results and provenance limits above remain unchanged.

R1: the original all-SAL-P List fixture could not distinguish missing Product membership from correct filtering, or matching-lines-only totals from whole-bill totals. The current actual UIA workflow creates three separate Draft fixtures after the unchanged A05 bill: pure matching total3, mixed matching paid2 plus other-Product paid14 = whole16, and newest other-only11. One-row Product-filtered pages must return exactly mixed/pure/golden in that order, totals16/3/75, no further Next, and exact Previous/First identities. Independent SQL checks the four stored headers, Product membership/line values, expected whole amounts and exact UIA action sequence against stored SaleIdDESC order. The second Product is seeded only for A05; the opening/posting/cost vector and schema9 fixture stay separate.

R2: deterministic primary-key-ordered XML with BINARY BASE64 captures all original receipt/line/ledger columns, including identities/audit/header token, the exact9 journal rows and balance key/Q/token before refusal/upgrade. Partial-refusal and pristine upgrade still own separate allocations. Desktop/admin refusals preserve these snapshots; the restricted writer-refusal uses the admitted migration identity to compare the previously read-only-captured snapshot. After upgrade, source/ledger/original journal and balance key/Q stay identical; only the existing balance token transitions for the expected V300 bootstrap. Before/after no-op, complete targeted source/ledger/balance/valuation/Sales/line rows and tokens plus all10journal rows must match exactly, and the actual admin result must say0scripts. Runtime-admin/old-runtime refusals also preserve this schema10 snapshot. The scope is targeted business rows/tokens/journal, without a global DBTS claim.

R3: testsREADME now correctly describes `MYDMSVN_GUI_P5_TEST_REVISION` as the package manifest sourceCommit; test-driver source revision/raw source and test-DLL hashes are recorded separately.

| Original round1 attempt | Selected / executed | P/F/S | Classification |
|---|---:|---|---|
| 15 — A05/List Light+Dark | 2/2 | 0/2/0 | Both reached new UIA header/whole-total assertions, then the new host SQL oracle failed because implementer used DTO name SaleNumber instead of persisted SaleNo. Original FAIL retained; not a product or intended behavior RED. |
| 16 — same2 after exact persisted-column repair | 2/2 | 2/0/0 | Complete stronger R1 oracle plus unchanged A05 accounting/later immutable-cost checks |
| 17 — schema Light+Dark, each two independent fixtures | 2/2 | 2/0/0 | Stronger legacy identity/audit/token/journal preservation and genuine no-op rows/tokens/journal/0scripts; actual old/current executables |

The exact original15/16 selector is `FullyQualifiedName~DirectSales_CostAndStockMatchIndependentOracle`;17 is `FullyQualifiedName~Actual_packages_enforce_schema9`. Each uses the same original direct Desktop project/Release/net48/x86/no-build/no-restore command flags above after a successful direct no-restore build (0warnings/errors). Logger/TRX names are `t08-attempt15-review-r1-a05-both.trx`, `t08-attempt16-review-r1-a05-both-oracle-repair.trx` and `t08-attempt17-review-r2-schema-both.trx`, under each `.local/t08/attemptNN/results`. Original TRX SHA256 values are respectively `C7FA4828D7C62DB0386B0B470EFFE265F1408678A094B12ED03777408349FAAC`, `7EF0590742A0B14C918B821A881BDE2DEF99DB7D5CDE94DBCE1B63741E2E5F03`, `2AEB811FF26796D261DE48CDB069B2F4D214F4EBC7E5D4359ACB9358A3ECBA41`.

Round1 audit rehashes all six source/22DLL-PDB raw before/after archives for15–17:0differences within each attempt. All24 exact dynamic probe prelaunch archived files rehash correctly. Current executed17 Desktop.Tests.dll SHA256 is `444C0266A2720FC3AAF16A5CF2F68261B3A5C06558CD6C70C5D5468FD6374E5A`, carrying base candidate0a789895 informational version; current six raw source hashes are local `attempt17/source-after.csv`. No post-fix-commit rebuild/package identity is claimed. All65 SQL resources are positively dropped after verified drain; no owned application/probe remains. Both immutable packages retain their original205file manifests/hashes. The two original10 UI transients remain unresolved/nonreproduced, and no full19PASS or frozen T08 acceptance is inferred. Task rereview and the final source review/fix wave still precede frozen execution.
