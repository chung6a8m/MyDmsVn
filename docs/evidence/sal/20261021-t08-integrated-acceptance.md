# SAL-T08 — frozen integrated evidence / acceptance OPEN

**Executed:** 2026-10-23. **Status:** feasible frozen integration recorded; full SAL/T07/T08 and dated Owner acceptance remain OPEN. This file uses the evidence path reserved by the 2026-10-21 plan; it does not backdate execution or acceptance.

Read with the [acceptance ledger](../../DIRECT_SALES_ACCEPTANCE.md), [active plan](../../plans/20261021-002-direct-sales-and-moving-average-costing.md), [development checkpoint](20261023-t08-packaged-development-checkpoint.md) and [final source fix wave](20261023-t08-final-source-fix-wave.md). Original T00–T07 and BCF/BCI/GUI-P5 evidence remains candidate-specific history. Production release prerequisites remain [UNACCEPTED / UNSCHEDULED](../../DEFERRED_WORK.md#release-prerequisites).

## Frozen identity and execution boundary

Original source/test-driver revision is `f5039cb21bae3df4e9995150e42689635b8d307b`, clean before every original frozen scope. The task review, whole-branch source review and final scoped rereview approved this source before integration. The final scoped rereview report SHA256 is `7E73A5919106E9B36D0EE13E7D3B5AA18EECF5931DD16CD2C530A15EA644FFEA`. Approval resolves the original T01 R3 / T07 R4 source findings; it does not substitute for runtime or Owner acceptance. After two original full GUI infrastructure failures, the separately reviewed test-only repair is `4697744063eab8484aeb2df2f0a6d23f42e2ca12` (two test files and the optional archive README note). Product/vendor source and the actual package remain f503. The later GUI scopes use this new test identity explicitly; no original result is renamed.

| Identity | Exact revision / SHA256 |
|---|---|
| BootstrapSourceGrid | `2bd7874f7e80b22ef181763749c2a2938ab7f16e` |
| Bootstrap5WinFormUI | `2be9a4d778fda7016207b75470020be6126f2f3f` |
| SourceGrid | `f4e457b43582bf01892f50bdc74aa480531e5944` |
| Frozen 2323-file recursive source manifest | `A8DA2F1494683C601F09C748A5FC4F4530C89F0BBF424921FC18FB38B52C99C3` |
| Original f503 GUI test DLL | `9825C8D97DDE49BD771A7DA5C6A8A6019920A0C4AB62147B246432C26ACFE798` |
| Final release manifest | `8DDAD51D40E00C15571B26D2040E305D005200B44BD38A6F66CB2B22D0D3C0AA` |
| Final Desktop executable | `5C52E1AC71A8A19922D1B76A8247E4E590C270A16468EE1575F4649E287AAA26` |
| Final admin executable | `9A630F8EFB675447924EEEC025706CABE7DE49800F09510BD2B41B6AF794CB9F` |
| Source `010_DirectSalesAndValuation.sql` | `E901E81CB9DEE4544DC6BA338DEA9C895565B1356F14305A5856156DB51527EA` |
| Genuine historical schema9 package manifest | `3376D54C49FAD98152CB5A8BC7F94EDFEF8F7E9D13B15A16A3A126D642347447` |

Raw source bodies, every owner output before/after, original logs/TRX, complete current/historical package file manifests, dynamic Sales probe source/executables and capture/resource evidence remain local under `.local/t08/final-f5039cb`, `.local/t08/final-gui-4697744` and the existing ignored fixture evidence folders. These local files contain details unsuitable for a source commit. Hash manifests are supported by retained raw bytes, not retrospective source reconstruction. No source/test/vendor edit or rebuild occurred during any individual frozen execution scope. The authorized test-only change between the original and revised GUI candidates is recorded separately below. Publisher custom Version changes product output metadata; separately versioned package assemblies are not asserted binary-equal to the original test output dependencies.

Observed environment: interactive Windows 10.0.26200, .NET Framework Release533509, SDK8.0.426, VSTest17.11.1, actual net48 x86 testhost; SQL Server14.0.2140.2 / EngineEdition3. The explicitly designated non-production connection opened effective `master`, without alternate connections or privileged service workarounds. Its value, server/database/principal names and ownership tokens remain private. The service was restored by the human before these frozen runs; earlier stopped-service/setup failures remain in the development record.

## Original commands and complete project results

The submitted restore/build commands were:

```powershell
dotnet restore MyDmsVn.sln --locked-mode --nologo -p:RestoreDisableParallel=true
dotnet build MyDmsVn.sln -c Release -p:PlatformTarget=x86 --no-restore --nologo
```

Both exited0; the build recorded0warnings/0errors. Every test scope used its explicit `.csproj`, `-c Release -f net48 -p:PlatformTarget=x86 --no-build --no-restore`. Discovery additionally used `--list-tests -- RunConfiguration.TargetPlatform=x86`. Execution used `--logger "trx;LogFileName=t08-final-<run>.trx" --results-directory ".local/t08/final-f5039cb/runs/<run>/results" -- RunConfiguration.TargetPlatform=x86`; the run name is the exact first column below. Reuse, Session and GUI additionally used `--logger 'console;verbosity=normal'`; complete fresh SQL used the default console verbosity. This describes the submitted command expansions, not independently observed process argv. Original expanded logs, selected/discovered case inventories and TRX names are retained locally.

| Run / explicit project below `tests/` | Discovered / executed | Passed / failed / skipped | Original TRX SHA256 |
|---|---|---|---|
| application / MyDmsVn.Server.Application.Tests |327/327|327/0/0|`BB786BB7EF84206ABC66DDDFC0D51B986248ECAB400916501D8239CA814EBE43`|
| architecture / MyDmsVn.Architecture.Tests |13/13|13/0/0|`1C7E2E1DBEBCAE405A1D44A7C1EFF6282AD7634212ADCD56BFA64B25637111A0`|
| fresh-foundation / MyDmsVn.Server.Infrastructure.Foundation.Tests |65/65|65/0/0|`B6391B10F10A192F777607BAD253D88D0E406E04E488A8AF9E91CAA01BDD21E9`|
| fresh-catalog / MyDmsVn.Server.Infrastructure.Catalog.Tests |19/19|19/0/0|`7B62ED1BEF38DAA85E139A4DDB13BCED7EC34DE1295A98930FCACCEEAD8F18DE`|
| fresh-inventory / MyDmsVn.Server.Infrastructure.Inventory.Tests |189/189|189/0/0|`9CA5BCDC4FF37F09346586BCEE6466E92449E20EC1C6E544A7A91862D1EAB888`|
| fresh-security / MyDmsVn.Server.Infrastructure.Security.Tests |62/62|62/0/0|`393EE9CA98C79F24AEC69EB8B3A47631AA0A9863104C86130FAF47868B47E171`|
| fresh-migrations / MyDmsVn.Server.Infrastructure.Migrations.Tests |141/141|141/0/0|`C32D1900FBD4B73FE67EAF38E43DEF88B517C1E38E84085C13F7CDBF94B307F7`|
| fresh-integration / MyDmsVn.Server.Infrastructure.IntegrationTests |59/59|59/0/0|`FCFD82435B997E9D995C72B07C2D3F89EF32108AF2C2E2B77D850031F1DB733F`|
| reuse-foundation / MyDmsVn.Server.Infrastructure.Foundation.Tests |65/65|65/0/0|`23707D94E2BB091321C9637A302E86BA9F718F875BF0A64BBE8C4E4AF0AF2555`|
| reuse-catalog / MyDmsVn.Server.Infrastructure.Catalog.Tests |19/19|19/0/0|`416CF512B9D22F48994344645677CB4F92F2FE1FF0124CB47C8B9B9C64E788B8`|
| reuse-inventory / MyDmsVn.Server.Infrastructure.Inventory.Tests |189/189|189/0/0|`AEE1C9E4E857741BC71A910F0B3AC6D2F0FE7C768D652769C2D4A973EC651C8A`|
| reuse-security / MyDmsVn.Server.Infrastructure.Security.Tests |62/62|62/0/0|`29BEA001A0A587B49B9080978DCE2E121623FC9A8C4B338B031C27BB1D206362`|
| reuse-migrations / MyDmsVn.Server.Infrastructure.Migrations.Tests |141/141|141/0/0|`4DB46C5F8CD1C5954DF44573162DFFC88E7063427A55760B7B2DF633811FFD18`|
| reuse-integration / MyDmsVn.Server.Infrastructure.IntegrationTests |59/59|59/0/0|`33BFFE5EFAE9C6888E547C947AA4240E5CCB6AD0C75C66D7C01C37228E387D4D`|
| session / MyDmsVn.Desktop.Session.Tests |174/174|174/0/0|`D2095B2E4AE561DC9FAD1E1C4866F05F2A10815DF8A2619F39A2EAD5A33F3343`|
| gui-native100-light / MyDmsVn.Desktop.Tests |711/710|709/1/1|`3A5243A3EA60FF371A4F392C34120066ECE2A2E212B0ACA4AADAB163865B2EAE`|
| gui-native100-dark / MyDmsVn.Desktop.Tests |711/710|709/1/1|`580BC412B7E5EB34CB343E421E281619270DEF1F93A7FDCE617CC7396C4A9B9B`|

No complete scope used a filter. All discovery/result multisets are reconciled, including duplicate display names; there is no empty-filter PASS. The attended result is `NotExecuted`, whereas the original Light TRX raw `Counters.notExecuted` says0. The table reports the actual result row/console skip; the original counter discrepancy remains retained. Contexts are separate, not summed into a unique-case or all-GUI PASS count.

The six SQL owners ran serially in README order Foundation→Catalog→Inventory→Security→Migrations→Integration with `MYDMSVN_SQL_TEST_FIXTURE_MODE=fresh`, then the same serial six with `reuse`. Both opt-in fault settings were unset. Each half has535/535PASS; every owner has identical actual case multisets and compiled output bytes across modes. Dedicated fresh exceptions remain fresh in either mode. There was no third complete SQL pass.

## SQL ownership, reset and cleanup

Existing owned allocation/token/identity checks, tracked workers/connections, guarded cleanup and quarantine-on-uncertainty remained in use. Participating APP fixture observations below are successful actual phase events; console-only fixture events are recorded by origin and not double-counted with TRX case/summary copies.

| Owner / mode | allocate / migrate / initialize-reset / drop | reset / seed / verify / verify-after / cleanup |
|---|---|---|
| Catalog fresh |30each|30each|
| Inventory fresh |25each|25each|
| Security fresh |28each|28each|
| Catalog reuse |1each|30each|
| Inventory reuse |1each|25each|
| Security reuse |1each|28each|

Foundation, Migrations and Integration emit no aggregate APP phase events. Their executed ownership/lifecycle/reset/drain/drop assertions and awaited disposal are the evidence; no aggregate allocation count or independent instance-wide orphan inventory is invented. Their dedicated concurrency/error allocations are not counted as shared APP fixtures. Final GUI resource deltas and process cleanup are recorded below. No uncertain allocation was automatically dropped by prefix or a guessed connection.

## Package and actual Login workflows

After Session and a bounded native resource observer, the ordinary inspected publisher was invoked on clean source with a dedicated absolute empty target:

```powershell
& ./deployment/release/Publish-LocalRelease.ps1 -Version '1.0.0-sal-t08-f5039cb' -OutputRoot 'C:/Users/pdchung/.codex/worktrees/sal-direct-sales/CleanArchitecture/.local/t08/final-f5039cb/package'
```

It exited0 with0warning/error lines, performed the actual locked restore and both x86 net48 publishes, and used no SkipPublish/source commit bypass. The manifest records source=f503, dirty=false, schema minimum=maximum10/scripts001–010. The two package directories' combined205actual files match every length/SHA/path containment check, without extras. The genuine historical schema9 package source is `95591071ea214be88813deb5227d216f1b0bca71`; its original205files/manifest are preserved. Historical binaries were copied into owned configuration directories for execution; the original other-worktree package was never modified. Current and historical packages were hashed before and after GUI execution.

The new packaged inventory is implemented in `PackagedSalesAcceptanceTests.cs` and is19actual rows, each present in both complete context inventories: metadata1; native PageSize seam2; A05 create/save/reload/post/list2; stale Save/Post4; direct Read/Write/Post Deny6; observed SQL Post wait/timeout and authoritative reload2; genuine schema9/partial refusal/admin upgrade/schema10 runtime boundary2. Every nonmetadata row uses actual packaged Login, finite owned UIA/process probes and independent SQL outcomes. These19rows are automation, not attended or per-character lookup proof.

The A05 oracle independently starts from a consistent Posted receipt/ledger/value Q20/V300. Paid qty3@25 plus giftqty2 retains manual prices and separate persisted identities, revenue75/cost45+30/profit0, balanceQ15/V225. A later receipt5@30 producesQ20/V375 without changing sale snapshots. The list fixture separately includes a matching bill whose other-Product paid line contributes14 to a whole-bill total16, and an other-only bill excluded by the actual Product filter; exact ordered headers and totals are asserted. Stale Save compares the winner's exact rowversion/complete persisted lines before/after the loser conflict; stale Post verifies one economic effect. Ordinary conflicts retain losing raw input. Authentication/permission loss clears protected presentation and leaves SQL unchanged; same-session private recovery identity is not transferred into a new Login/workspace.

Schema tests keep partial-refusal and pristine9-upgrade fixtures independent. Identity-rich deterministic legacy source/ledger/audit/token/journal snapshots are unchanged after refusal and upgrade, allowing only the specified bootstrap balance value/token transition. Complete10 no-op compares exact data/tokens/journal and actual0scripts. Old9 runtime really rejects10; a new exact-schema check is not substituted for an old executable. Admin maintenance is separate from least-privilege runtime, with old clients quiesced. The uncertain-Post row observes a real SQL wait/timeout, locks the editor, reloads authoritative state and permits at most one explicit retry; it does not claim an unobserved post-commit outcome.

## Native GUI contexts, failure and bounded diagnostics

Independent owned PerMonitorV2 observers measured one1920×1080 monitor, workarea1920×1032,96×96DPI/native100; owned client1200×760/window96DPI. No scale/emulation/system setting was changed. Global `MYDMSVN_BCF_THEME=Light|Dark` supplies the named context; source/theory-specific themes remain authoritative, so the context does not relabel every row's explicit theme. Both complete invocations set existing expected-scale variables to100, final package/revision/historical-package variables, and distinct SAL capture directories. Attended and trace/fault settings were unset.

Original complete Light remains **FAIL**. Existing infrastructure Fact `GuiOwnedProcessTests.Packaged_lookup_keyboard_recovers_its_owned_window_from_a_background_editor` failed first at `owned-keyboard-foreground:TimeoutException`: owned foreground=True, requested root foreground=False, editor focused=True, activation requested/granted=True. F4 was not posted because the foreground safety precondition failed. The synthetic target's visible owned distraction (`other.Show(target)`) may redirect activation; exact handle/owner-chain state was not captured, so this is an unproved hypothesis, not a product regression diagnosis. Secondary `DescendantsOutlivedParent` was followed by verified owned job-tree termination/output drain. No named owned process remained.

An authorized unchanged-byte focused diagnostic used exact filter `FullyQualifiedName~Packaged_lookup_keyboard_recovers_its_owned_window_from_a_background_editor|FullyQualifiedName~Packaged_lookup_input_preserves_value_when_the_editor_refuses_keyboard_focus`, run name `gui-foreground-unchanged-pair` with the same direct flags above. Both2/2passed, TRXSHA256 `64B050D3506C4975100390C455989F1689745E437B9C024C4A8F680FA119B2A1`. This does not fix/explain/rename the original complete Light failure. At that diagnostic boundary no source was changed and no full Light rerun had yet been performed. A local pre-Dark transcript guard incorrectly expected successful legacy probe stdout in TRX and failed before discovery/execution; it was corrected by invoking the existing resource observer, not by changing source or display scale.

Bounded resource observers used exact filter `FullyQualifiedName~Independent_observer_records_actual_monitor_window_bounds_and_process_ownership` with the same direct flags, run names `gui-resource-observer` and `gui-resource-observer-pre-dark`. Each1/1PASS: initial SHA256 `524F083FA078689C26FD94EA4464AAAFC2166B6748BB4493F3C85071B39809F5`; pre-Dark `B5F2B17FCBA8A78C54B88A142149E7A7C3F9F05DB3C2D93D877C035AC33074AD`. These extra finite preflights are separate from full GUI/actual Login evidence.

Original Dark reproduced the same foreground/root mismatch and retained709PASS/1FAIL/1attendedNotExecuted. No product source changed. Light owner output grows174→192files solely from18generated BCI PNG captures:0existing-file changes/0removed. Dark stays192files, with exactly2uncertain-state PNG changes and0added/removed. No DLL/EXE/PDB drift exists in either context. Raw audit43archives/8105files has0mismatch/0unexpected. Each context's24Sales probe archive pairs across18nonmetadata rows rehash correctly against retained prelaunch markers. Final and historical packages each retain all205manifest-matching files after Dark. GUI resource delta is39new terminal-dropped allocations per complete context; all65preexisting development and then104pre-Dark records remain unchanged,143terminal records total. Named remaining processes0 after each complete context. Fixed filenames were archived per context before later overwrites; raw screenshots remain local, unreviewed images are not committed. Hosted layout captures at1680×900/1200×760/944×600 in both themes are distinct from actual packaged workflows and do not prove every keyboard workflow at every size.

All19new packaged rows passed in each original complete Light and Dark context. The two development UI transient causes (A05Light audit and ReadDenyLight Product popup) remain unknown even when the exact rows later pass. No automated GUI total substitutes for native125 or a human observation. The post-freeze test-only foreground investigation/repair and any new GUI executions require a separate test identity below; they never rename these original complete failures.

## Separately reviewed test-driver repair and fresh GUI scopes

Metadata-only diagnostic instrumentation ran the complete `GuiOwnedProcessTests` class21/21PASS, SHA256 `D3D1E1C9FE872C66FDB64127D3B27FFCEDCA167A975A455A5BA02A3792323FED`. It directly observed foreground and GUI-thread active window still on the owned distraction immediately after the activation call returned; target activation/focus arrived later. This observation supports the missing synchronization boundary, without establishing every causal detail of the two earlier failures. Microsoft's [cross-input-queue activation explanation](https://devblogs.microsoft.com/oldnewthing/20161118-00/?p=94745) describes the same asynchronous completion boundary. Temporary WM/GUI-thread diagnostic instrumentation was archived and removed before the permanent fix. The class's SQL-quarantine cases are synthetic constructors/fail-stop cases before SQL I/O, not real database allocations.

The permanent test-probe fix waits at most5seconds for the exact ownership-validated target root to become foreground after its existing single activation request, before UIA SetFocus. The final exact-root+editor-focus guard remains; activation and native/business keys are never retried. The two focus Facts retain stdout and optionally copy raw target/driver source/executable bytes before their focus launches when `MYDMSVN_GUI_PROBE_ARCHIVE_DIRECTORY` is an explicitly configured absolute directory; unset performs no archive writes. Ordinary tests contain no task-specific output path.

Before the final test revision, the corrected focused class21/21PASS has TRXSHA256 `90EC468C6CEB1A4FFD24FBD96E36FEC441749C996CC2C05A659246988B1DFAD5`; related actual-Login lookup-history, inline receipt, Sales native PageSize and A05/list each Light/Dark total8/8PASS, TRXSHA256 `1F099D9040F275258517630C36E889643942C7E3EF7443F9F9DE2CA93EC085CF`. Their exact class filter is `FullyQualifiedName~GuiOwnedProcessTests`. The packaged filter is `FullyQualifiedName~Same_process_lookup_messages_preserve_history_reject_inactive_new_choices_and_reopen_fresh|FullyQualifiedName~Packaged_inline_receipt_flow_reconciles_with_readonly_sql|FullyQualifiedName~Actual_sales_list_accepts_native_numeric_page_size|FullyQualifiedName~DirectSales_CostAndStockMatchIndependentOracle`. Original expanded commands and individual cases remain local. A prior21/21PASS before the archive-option review correction remains separately retained, SHA256 `4616EA40BA08D1CA31B7774C83D500530F19C842255B6AB5527353EFB1B75B9B`. These focused results do not substitute for a full context.

After commit4697744, the inspected ordinary command `dotnet build tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -p:PlatformTarget=x86 --no-restore --no-dependencies --nologo` exited0/0warnings/errors. Only Desktop.Tests.dll/PDB changed; all167other DLL/EXE/PDB dependencies compare byte-identical to original f503. New test DLL SHA256 `F1458CAF80C9F52D9F31E0005625A99E35BBBDDDCB16175067A2A9D45CC0B464`. The separate2323-file raw source manifest is `C42C333D4D0FDE144B3F1582514311C13EEA10801FB99B815F6174417245FFD1`, differing from f503 only in those two test files and README. No product metadata override, SQL pair, Session or publisher rerun occurred.

The revised independent observers each1/1PASS, initial SHA256 `9B1D2052E4969E250D112955694CFA8A44964CA81961138865C8B88226D79826` and pre-Dark `C78DC30ED28C40D336A911B1113A26724C2BFE479162C24DAF2A15EEEE96A76B`, remeasured the same actual sole native100 display and verified cleanup. Native125 was still unavailable. Revised full commands retain the earlier explicit Desktop project, Release/net48/both x86/no-build/no-restore flags, no filter, with `--logger 'trx;LogFileName=t08-revised-gui-native100-<context>.trx' --logger 'console;verbosity=normal' --results-directory '.local/t08/final-gui-4697744/runs/gui-native100-<context>/results' -- RunConfiguration.TargetPlatform=x86`. Discovery uses the same project/flags plus `--list-tests -- RunConfiguration.TargetPlatform=x86`. Package source/revision variable remains f503; test source is4697744. The legacy `MYDMSVN_GUI_P5_TEST_REVISION` means the package manifest sourceCommit, not the separate test-source revision. Per-context SAL capture and optional focus-probe directories are separately retained. Original full FAILs remain in the table above.

| Revised full context / test source4697744, unchanged f503 product/package | Discovered / executed | Passed / failed / attended NotExecuted | Original TRX SHA256 |
|---|---|---|---|
| gui-native100-light |711/710|710/0/1|`025E21907F0BB2E106242F92C1606BA4ECBC9DBD5537AACB240B88CBCD734AD3`|
| gui-native100-dark |711/710|710/0/1|`5B7214F1A25F66D6A7433360EC61AB91F27B558E7E49BA4D3AE7046D76A2FF93`|

Both exit0, with0missing/unexpected discovered result rows. Each complete context actually passes all19new Sales package rows and all21GuiOwnedProcessTests, including recovery and refusal. The attended result remains NotExecuted; both raw TRX counters report notExecuted=0 despite that actual row. This is PASS for the710 runnable automated rows per context, not a PASS for missing mandatory native125 or attended scope. The two original full failures are retained as RED evidence for the reviewed test-driver synchronization repair; observed diagnostic ordering and later PASS support that repair, without reconstructing unrecorded original window state or claiming a product defect. The two earlier development UI transients remain unexplained even though the exact cases pass here.

Each revised scope retains identical2323source hashes and the same test DLLF1458; all167other DLL/EXE/PDB dependencies remain byte-identical to f503. Output stays192files:0added/removed, exactly2changed uncertain-state PNG captures and0noncapture/binary drift. Final raw audit11archives/4243files reports0mismatch/unexpected. Per context,24Sales probe archive pairs across18nonmetadata rows match retained prelaunch markers and frozen probe bodies;4optional focus archive pairs/8raw files match8stdout markers and their frozen source bodies. The focus pairs include copies of the shared synthetic target before each focus launch; they are not four unique executables or proof of the earlier class-process launch. An unanchored artifact-parser regex initially mistook FOCUS_PROBE markers for Sales markers; that derived JSON is preserved, and separate exact-line-prefix audits confirm0missing/bad archives. This was a postprocessor error, not a runtime failure.

Revised Light creates39new terminal-dropped GUI SQL records,151→190; Dark creates another39,190→229. Every new record is DatabaseAndAllPrincipalsDroppedAfterVerifiedDrain; all previous record SHA/length/state values remain unchanged. Final total229retained GUI resource records has0nonterminal/quarantined records, and no named testhost/app/package/inline/process probe remains. These are tracked fixture records, not an independent instance-wide orphan inventory. Each context's121capture files, including24SAL PNGs, are retained before later overwrite. Final current/historical packages again match all205actual files each, unchanged manifests8DDA/3376 and0extras/drift. No product rebuild, second package publish or third SQL pass was needed for the test-only fix.

## A01–A55 frozen reconciliation

The table maps the actual complete-project inventory to required behavior. `PASS` is the stated frozen automated scope only; any required missing native125/attended/Owner portion keeps the whole row OPEN. AP=Application; IN=Inventory SQL; MI=Migrations SQL; SE=Security SQL; FO=Foundation SQL; SS=Desktop.Session; GU=Desktop GUI; PK=new actual-Login packaged rows above. Class names identify implemented Facts/Theory families inside those reconciled inventories, not proposed selectors. Previous task evidence remains linked from the ledger.

| Row | Actual implemented evidence family (core/product f503; revised GUI test4697744) | Frozen disposition |
|---|---|---|
| A01 | AP SalesContractTests/SalesReceiptApplicationTests; IN SalesReceiptDraftTests | PASS |
| A02 | AP SalesValuationMathTests; IN Draft/Post; GU SalesReceiptGridTests; PK duplicate/gift reload | native100/core PASS; full native125 OPEN |
| A03 | IN Draft identity/reorder; GU Grid/Control; PK independent duplicate/gift rows | native100/core PASS; full native125 OPEN |
| A04 | AP SalesValuationMathTests; IN SalesReceiptPostTests/InventoryValuationWriterTests | PASS |
| A05 | IN SalesReceiptPostTests; PK DirectSales_CostAndStockMatchIndependentOracle | PASS |
| A06 | AP exact allocation vectors; IN SalesReceiptPostTests | PASS |
| A07 | IN Post/Concurrency; PK later receipt and immutable sale snapshots | PASS |
| A08 | AP SalesReceiptPreviewTests; IN Post/Query; SS Editor; GU Control/Accessibility; PK Posted detail | PASS for automated native100; attended readability belongs A50 |
| A09 | MI DirectSalesMigrationTests/SchemaCompatibilityTests; PK genuine old/new schema boundary | PASS |
| A10 | MI DirectSalesMigrationTests/SchemaCompatibilityTests/MaintenanceTests | PASS |
| A11 | MI DirectSalesBootstrapTests; PK immutable legacy snapshot upgrade | PASS |
| A12 | MI DirectSalesBootstrapTests corruption/overflow boundaries | PASS |
| A13 | MI DirectSalesBootstrapTests.Injected_fault_rolls_back_every010_batch_and_rerun_succeeds; PK no-op/refusal snapshots | PASS |
| A14 | MI DirectSalesConstraintTests; IN Draft/Post/Mapping | PASS |
| A15 | SE runtime grants/permission cases; MI maintenance; PK ordinary Login/admin separation | PASS |
| A16 | FO ReusableSqlFixtureIntegrationTests/LifecycleIntegrationTests; six-owner matched pair | PASS |
| A17 | IN GoodsReceiptCommandIntegrationTests/InventoryValuationWriterTests; existing PK receipt regressions | PASS |
| A18 | AP InventoryQueryApplicationTests; IN InventoryValuationQueryTests; SS inventory | PASS |
| A19 | IN InventoryValuationReconciliationTests/ConcurrencyTests | PASS |
| A20 | AP SalesPermissionTests/permission families; SE authorization; SS LocalSalesReceiptApiClientTests; PK direct-deny6 | PASS |
| A21 | AP contract/date; IN SalesReceiptDraftTests/QueryTests | PASS |
| A22 | IN SalesReceiptDraftTests; GU Grid/Control; PK independent persisted identities | native100/core PASS; full native125 OPEN |
| A23 | IN Draft/Post/Concurrency reference/Unit boundaries | PASS |
| A24 | IN Draft/Post/Concurrency; SS Editor/Local; PK two-client stale4 | PASS |
| A25 | IN SalesReceiptPostTests independent all-table SQL oracles | PASS |
| A26 | IN InventoryValuationWriterTests/ConcurrencyTests and SalesReceiptConcurrencyTests | PASS |
| A27 | IN receipt/sales/reconciliation lock-order and absent-key concurrency | PASS |
| A28 | IN valued writer/Post fault matrix, rollback/version oracles | PASS |
| A29 | IN SalesReceiptConcurrencyTests; PK stale Post exact economic cardinality | PASS |
| A30 | IN InventoryValuationConcurrencyTests/SalesReceiptConcurrencyTests | PASS |
| A31 | IN SalesReceiptPostTests demand/range/overflow unchanged-state checks | PASS |
| A32 | IN lock timeout/cancel/deadlock; SS uncertainty; PK observed wait/reload/explicit retry | PASS |
| A33 | AP SalesReceiptQueryContractTests; IN SalesReceiptQueryTests | PASS |
| A34 | IN SalesReceiptQueryTests; PK distinct other-only/mixed whole-bill filter oracle | PASS |
| A35 | IN SalesReceiptQueryTests; SS List; PK PageSize/ordered navigation | PASS |
| A36 | IN SalesReceiptQueryTests; SS List; PK bidirectional pages | PASS |
| A37 | IN SalesReceiptQueryTests/QueryOwnershipTests live membership | PASS |
| A38 | AP SalesReceiptQueryContractTests; IN cursor/filter boundary cases | PASS |
| A39 | IN SalesReceiptQueryOwnershipTests consistent transaction/disposal | PASS |
| A40 | IN actual LINQ SQL/plans/IO,12observations in bounded1200-header workload | PASS within observed workload |
| A41 | SS LocalSalesReceiptApiClientTests session/actor/cancellation | PASS |
| A42 | SS SalesReceiptListViewModelTests generation/applied snapshot | PASS |
| A43 | SS List/ViewModel authorization/recovery; GU ControlAdmission; PK Read Deny | PASS |
| A44 | SS Editor; GU ControlAdmission/Grid/native input; PK timeout/reload | native100 automated PASS; native125 OPEN |
| A45 | GU SalesReceiptGridTests/AccessibilityTests/ControlTests; PK duplicate/gift | native100 automated PASS; native125 OPEN |
| A46 | GU SalesReceiptLookupNativeInputTests per-key owned EDIT observations | native100 automated PASS; native125 OPEN |
| A47 | GU LookupNativeInput/Grid/Control native key/mouse semantics | native100 automated PASS; native125 OPEN |
| A48 | GU ControlAdmission/ControlTests; SS List/Editor/session disposal | native100 automated PASS; native125 OPEN |
| A49 | GU SalesReceiptNativeEnvironmentTests size/theme captures and separate keyboard cases | native100 bounded PASS; native125 OPEN |
| A50 | GU Accessibility/native numeric validation; PK Draft/Posted detail; final R4 repair | automated native100 PASS; native125/attended OPEN |
| A51 | AP327/AR13 and exact535fresh +535reuse, source/vendor/output reconciliation | PASS |
| A52 | SS174PASS; revised complete GUI Light710/0/1 and Dark710/0/1, test4697744; original f503 full FAILs retained | native100 runnable automation PASS; native125 NOT RUN / OPEN |
| A53 | final clean x86 package; actual Login/limited runtime/admin/schema9/10 PK | PASS for automated package scope |
| A54 | no human session/markers created | NOT RUN / attended OPEN |
| A55 | this55-row reconciliation; no dated exact-scope Owner acceptance | OPEN |

A40's actual Fact is `Actual_LINQ_SQL_is_bounded_parameterized_and_plans_cover_deep_selective_both_directions`:12actual plan/IO observations,3commands across nonselective/selective and next/previous, logical reads2–1162, fixed synthetic1200headers. This is bounded observed workload evidence, not universal production-performance certification.

## Remaining decisions and handoff

Native125 and every required125workflow/sixsize-theme contexts remain NOT RUN / RESOURCE BLOCKED on the actual available display; strict1680×900logical125 requires2100×1125physical client plus chrome. No fitted/emulated viewport or old BCF exception supplies it. Full T07 remains OPEN. Attended all-lookup/native-edit/cost-profit/navigation observations and dated Owner acceptance are NOT RUN/OPEN. Original full Light/Dark foreground failures remain FAIL in their original source identities; reviewed test-only rendezvous4697744 has separate complete-context PASS evidence. The two development UI transient causes remain unknown. No accounting/atomicity/SQL ownership failure is observed in the passing frozen core scopes, but this is not full SAL completion or permission to deploy.

All original development/source-wave/setup failures and original TRX remain preserved. In particular, initial schema-admission RED and setup failures, reserved-column SQL fixture failures, attempt10 six UI failures, attempt15 two oracle-column failures, their exact focused repairs, and provenance limits are not erased by this candidate. No raw TRX/connection/SQL identity/proprietary screenshot is committed. No push/PR/merge/deploy was performed.
