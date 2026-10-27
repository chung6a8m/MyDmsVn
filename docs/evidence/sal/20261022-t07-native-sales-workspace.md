# SAL-T07 — bounded native Sales workspace checkpoint

Date: 2026-10-22. Development `codex/sal-direct-sales`; planning `v1a-pro`. Final source/test commit `879ab7bc6046d598fe06be1d95614df2b02a5810`; direct parent/review BASE `1949abab1a0892783e55afc863001cf020a4f06e`, previous product/test `5fbd6f5c3022ccb05c770648e4967747d73602fa`. Planning baseline remains `35ba2e8a3f37fb07095701eb94f4b56f7941c491`. This is a bounded reviewed development checkpoint. **Full T07 remains IN PROGRESS / OPEN** because mandatory native125 evidence is unavailable. [The serial execution index](../../archive/20261023-pre-ui-next/plans/README.md) remains T00 → T08; T08 is NOT STARTED. No Owner order change, full GUI, package, actual Login, attended cost review, Owner acceptance or operational release is established.

## Source, review and formatting boundary

Tested candidate3 freezes exactly 17 C# paths with FILEBYTE manifest SHA256 `BE2BDF8BC267A48FADB0B2CEDB25937A77975D6A56F32F0F2E29A329AD22EFE7`; raw source bodies are retained locally. Final candidate4 differs solely by removal of one trailing blank CRLF line from `tests/MyDmsVn.Desktop.Tests/SalesReceiptGridTests.cs`; its manifest SHA256 `EB354709AD17EA303D82C8F7E7DFF73B98E26A7AB54D803959E9D9918055C1B2`. All other 16 paths are byte-identical, and the changed file equals candidate3 after removing exactly that last CRLF. No build/test was rerun for this formatting-only change. Candidate3 source hashes, binary hashes and actual TRX remain candidate3 evidence; no candidate4 binary/source-checksum identity is asserted.

The final candidate3 review closeout, retaining the prior combined source review, is APPROVED for bounded source and executed evidence, SHA256 `3D02EF9A1C0AF6FEE3A782CE9A3874CD0EBA86EE7E1DAF0E84122DCDDB0E03F2`. Root approved the exact candidate4 formatting delta before the local commit. The same reviewer independently verified and approved that trivial delta afterward, SHA256 `35E5DFED4FB60BC2F607F82AAA83F64AE88BDC2F394EA95A7640F8B37E405D45`; this is post-commit verification, not a claimed pre-commit review or new execution. Candidate1 had three Important findings plus one Minor. R1–R3 are resolved by reviewed source and fresh regressions. R4 remains DEFERRED Minor: a copied numeric validation footer can survive Escape or a later correction. This checkpoint does not close validation accuracy or A50. Final whole-branch disposition remains required.

Recursive vendor pins remain `2bd7874f7e80b22ef181763749c2a2938ab7f16e` / `2be9a4d778fda7016207b75470020be6126f2f3f` / `f4e457b43582bf01892f50bdc74aa480531e5944`. No project, dependency, schema, permission key, SQL or vendor change is included. Root verified all 17 committed paths and normalized Git blobs against final candidate4 bytes, plus clean committed diff check and unchanged vendor pins. C# preserves BOM/CRLF; public Markdown preserves UTF-8 without BOM/CRLF. The first staged including-untracked diff check found the extra EOF blank line; the earlier tracked-only check did not inspect it. That formatting finding is retained honestly.

## Implemented behavior and reviewed repairs

SalesReceiptControl/Factory and SalesReceiptLinesGrid/Adapter compose the T06 VMs and existing app-owned pinned lookup, formatted numeric, parser, session and accessibility seams. Seven semantic columns are exactly STT/Product/Unit/Quantity/UnitPrice/IsGift/LineAmount. Manual selling price, gift price retention/zero revenue, strict bounds/precision and independent Guid ownership for duplicate Products and new SaleLineId0 rows remain. Cost/profit is read-only outside these columns; Draft null and authoritative Posted snapshots remain distinct. No pricing engine or copied calculator is added.

The same control/editor/grid adapter survives guarded List/Detail presentation. Header/line buffers are resolved before Save/Post decisions; dirty/invalid/recovery Back rejects without losing raw input, admitted query-only Back cancels presentation, and Posted/busy/denied/uncertain states guard all edit routes including gift/accessibility callbacks. Product Unit resolution uses current-catalog metadata fenced by row, selection generation, document, operation and session; it preserves manual price and rejects stale data after Save, reorder/removal, replacement, denied response or disposal. Authoritative saved Unit is not overwritten by an older catalog response.

R1 propagates an actual List 401/403 through control-wide denial, public editor scrub and native/header/Unit cancellation. The added `SalesReceiptListViewModel.SetAccess(bool)` is UI admission only: false invalidates/cancels active transport and queued publication and clears protected page/selection/cursors; true reopens only the original still-current authenticated session, without query, old DTO restoration or session rebinding. Fresh authorized List/Get restores presentation. Private original-ID Post recovery remains locked through scrub and denied reload; New/arbitrary Load and Post replay cannot bypass it. The new seam carries a bounded future consolidation cost; an incorrect generation reset could resurrect stale protected state, so revoke/restore transport/queued success/error and replacement-session regressions are retained.

R2 makes existing Detail activation refresh permission decisions without unrelated List/catalog refresh or an unadmitted buffer boundary. Native valid `2.125`, invalid `1.00001` and pending Product query preserve raw text, caret/range/focus, committed model and row/control identity across activation and canceled delayed permission refresh. R3 uses complete control Busy consistently for header availability, searches/selection, date/note, Unit callbacks and command admission, separately tested under held List, dirty Save and Post. These repairs preserve independent Post without Write.

## Actual fresh candidate3 results

Windows 11 Home Single Language 10.0.26200/build 26200; SDK 8.0.425; net48 Release/x86. All four scopes executed serially on frozen candidate3 bytes. Root and reviewer reconciled exact sorted discovery-name and actual TRX-name sets, every result Passed and all nonpass counters zero. Counts are per invocation, not an additive unique total.

| Scope | Discovered / executed / PASS | TRX SHA256 | Matched output DLL bodies |
|---|---:|---|---:|
| Affected Sales native100/hosted plus six same-data layouts |110 /110 /110|`4F8D31480B9E9F22F3AA55F665EE2A3B2DAD05FEAFB4AE845293D6DF26DBAB62`|102|
| Shared ReceiptNumericInput / shell / composition |55 /55 /55|`5AD0D9BFF29C1BAB57A1B97403F2A4EECCEE23DFBD64F8DD3DD8B5FA60E2B02B`|102|
| Complete Desktop.Session |174 /174 /174|`6EB3926E4CBBED940431A18CCCA07615163B6B227468C5FEDE9BCFDD0ADEF549`|35|
| Complete Architecture |13 /13 /13|`A0A59A96946155BC9B2C70C6D1670351C6293D6F40A20E05B9B00958BC52D755`|64|

Each scope has matched 17-file source-before/source-after/live/raw source archive, and pre/post/live/raw output DLL checks. Full captured output graphs are 102/102/35/64, not package manifests. Shared 55 comprises 36 parser, 16 shell, 2 DI and 1 Sales composition rows; it does not mean 55 new cases. Complete Session 174 broadens because the approved List access-admission seam changes Desktop.Application. Fresh Session/Architecture are real executions; candidate2 had no contemporaneous full relevant source-graph prehash for a strong reuse claim.

Native 110 contains 67 measured SAL100 rows plus 43 hosted control/grid/accessibility rows. The 67 are 60 native workflows at 1200×760, six separate size/theme layouts and one full physical-monitor inventory. All 67 have post-disposal cleanup outputs covering tracked operations/source completions and actual disposed forms. Native EDIT HWND ownership, individual WM_CHAR input, raw text/caret/range/focus and committed effects are observed. The printable Space checkbox action uses an owned KEYDOWN → CHAR → KEYUP sequence; F4 and arrow-key actions are separate native-key workflows without a CHAR requirement. Whole Text/UIA assignment is not substituted. Neither 110 independent DPI contexts nor every keyboard workflow at every size is claimed.

## Original submitted commands and provenance

Root verified the authentically retrieved four original `exec_command` submissions (chunks `fd8ed5`, `8fe273`, `224677`, `bb5045`) against retained helper bindings and direct command bodies. Addendum SHA256 `77A39ABABB480CBFF5B066F3453C4EE128ABE8C1159E74E0DE77098BB81226E8`. This is submitted-call/helper provenance, not an independent OS argv trace or restored command history. No command was rerun for retrieval; no complete restore history is asserted.

The ignored helpers are local artifact capture transcripts around ordinary direct project calls, not a new test engine, gate, SQL runner or global lease. Their common verbatim build/discovery/test body is:

```powershell
dotnet build $project -c Release -f net48 --no-restore -p:PlatformTarget=x86 *> "$evidence/$Tag-build.log"
dotnet test $project -c Release -f net48 --no-build --no-restore -p:PlatformTarget=x86 --list-tests --filter $Filter -- RunConfiguration.TargetPlatform=x86 *> "$evidence/$Tag-discovery.log"
dotnet test $project -c Release -f net48 --no-build --no-restore -p:PlatformTarget=x86 --filter $Filter --logger "trx;LogFileName=$Tag.trx" --results-directory "$evidence/$Tag-results" -- RunConfiguration.TargetPlatform=x86 *> "$evidence/$Tag.log"
```

All helpers define `$evidence = '.superpowers/sdd/20261021-002-direct-sales-and-moving-average-costing'`. The actual Tag/project/filter bindings are:

| Tag | Project under `tests/` | Actual Filter |
|---|---|---|
| `t07-candidate3-native100` | `MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj` | `FullyQualifiedName~SalesReceiptControlTests\|FullyQualifiedName~SalesReceiptControlAdmissionTests\|FullyQualifiedName~SalesReceiptGridTests\|FullyQualifiedName~SalesReceiptAccessibilityTests\|FullyQualifiedName~SalesReceiptLookupNativeInputTests\|FullyQualifiedName~SalesReceiptNativeEnvironmentTests` |
| `t07-candidate3-shared` | `MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj` | `FullyQualifiedName~ReceiptNumericInputTests\|FullyQualifiedName~FoundationShellFormTests\|FullyQualifiedName~WinForms_composition` |
| `t07-candidate3-session` | `MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj` | Empty string, complete project |
| `t07-candidate3-architecture` | `MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj` | Empty string, complete project |

Native submission explicitly set `MYDMSVN_SAL_EXPECTED_SCALE='100'` and `MYDMSVN_SAL_CAPTURE_DIRECTORY` to the distinct candidate3 capture folder beneath the evidence directory. All calls used the development worktree; actual helper paths and complete submitted strings stay locally retained. No candidate3 restore ran; project/reference/package files and the existing locked restore graph were unchanged. Compile PlatformTarget=x86 and test RunConfiguration.TargetPlatform=x86 are both explicit. VSTest 17.11.1 CLI x64 banner is distinct from the observed 32-bit native test process and measured window/monitor DPI.

## Same-data visuals and physical resource boundary

Candidate3 produced 24 local PNGs, eight each at 944×600, 1200×760 and 1680×900, Light/Dark: Detail/List/error focus/popup in each context. Capture manifest SHA256 `D5040B2AA3FFD8A3546FB3AEB96E0C5B448B62C3B81576E0448466763DE8D7A9`. Every List capture asserts actual projected Lines 3 / Total 14.375 before capture. Detail contains independent ordinary/gift duplicate Product 7 plus Product 8, total 14.3750; gift price 12.5 is retained with revenue 0. Root inspected all 24 via six contact sheets; reviewer separately viewed compact Light List and Dark Detail. Seven columns, amounts/total, focus/errors/popups are reachable in this bounded technical visual QA. R4 stale footer remains visible after Escape. This is not Owner or attended cost acceptance.

The independent PerMonitorV2 observer enumerated exactly one physical 1920×1080 display, work area 1920×1032, native 96×96 DPI/100%; window 96 DPI. Every mandatory native125 workflow and all six native125 size/theme contexts remain **NOT RUN / RESOURCE BLOCKED**. Strict wide125 1680×900 logical requires 2100×1125 physical client plus chrome, exceeding this display. No scale emulation, stretched artifact, display setting change or inherited BCF size exception is used. Missing resources are not skipped-to-PASS; silence does not authorize a matrix or execution-order change.

## Candidate history and original failures retained

Candidate1 exact 14-path manifest `F93E788636D87675CC3E0AF2BC36F99F54446A06CDF6CF6401D720D71D0C1AFE`, real 82/55/13 PASS and 24 captures remain distinct. Its review found R1–R3 despite those passes. Candidate2 exact 17-path manifest `67E11E1B945DCC5720DDC4CB35E2C9E9F37250939CDB0E8E06E807D389B8EA96`, real 110/55/174/13 PASS and 24 captures remain distinct. Candidate2 List showed 1 line / total 4 while Detail showed 3 / 14.375; those captures do not establish same-data comparison. Correct R2 intentionally stopped implicit Detail activation refresh; the synthetic fixture omitted an explicit admitted List query.

Candidate3 changes only that fixture: explicit List admission/query plus projected display assertions. New oracle RED 6 failed expected 3 / actual 1 in every size/theme, TRX SHA256 `B7CFBFB3C6B2098EEAC8711F7FFEE3A11D417DD7A2C12B6F4A1916D6800982B5`; this is capture-oracle/fixture RED, not a product defect. Fresh candidate3 evidence replaces no historical artifact and fixes no R4 behavior.

Local records preserve every actual development/setup/partial invocation and SHA. Meaningful originals include missing-grid 7 FAIL, missing-control 4 FAIL, current Product Unit omission, stale catalog overwriting saved Unit, compact offscreen-money 2 FAIL then partial 1 PASS / 1 FAIL, denial/busy/native-reactivation and revoke/restore regressions. Separate setup/oracle failures remain labeled: inaccessible collection event/DatePicker/nullability/fixture compile errors; WinForms guard constructed too late; invalid Posted fixture missing timestamp; held-refresh native timeouts; clean-Save busy fixture; incomplete mouse/Space message sequences. These are not intended product RED. Bare CHAR lacked the pinned grid's keydown snapshot; final owned KEYDOWN→CHAR→KEYUP needs no vendor patch. First grid RED/GREEN have no source-after manifest; later artifacts do not reconstruct that missing provenance. Earlier development labels before post-disposal task tracking are not independent cleanup proof.

## Acceptance and remaining obligations

The [acceptance ledger](../../DIRECT_SALES_ACCEPTANCE.md) records bounded source/native100/hosted portions of A02/A03/A08/A22/A44–A50 and related denial/session/uncertainty routes. Full A44–A50 remain OPEN: native125 and frozen/attended portions are absent and R4 validation accuracy is deferred. No automatic multi-task closure or full T07 exit follows these passes. Full GUI, SQL/T08, package/actual Login, attended cost/native and explicit Owner disposition remain NOT RUN; T08 remains NOT STARTED behind T07.

All owned completed test/observer work was drained, 67 measured fixtures reported post-disposal cleanup, bounded observer cleanup was verified and the final inventory found no active testhost or GUI/inline probe process. No SQL resources were allocated by this task. Public documentation work performs only diff/link/status/encoding checks; it executes no build/test/SQL/GUI.

T00–T06 and BCF/BCI/GUI-P5 evidence remain immutable with exact source/test/package revisions, failures and accepted limitations. T01 R3 generic bootstrap diagnostics remains DEFERRED Minor; exact9 SQL-login-only admission, trusted pre-existing RESTRICTED_USER maintenance, inherited collation portability and Local direct-SQL trust remain explicit limits. No mixed old/new writer deployment is accepted. [Operational release prerequisites](../../DEFERRED_WORK.md#release-prerequisites) remain UNACCEPTED / UNSCHEDULED; this development checkpoint is not deployable.
