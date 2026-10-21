# BCF-T06 — frozen integrated comparison and acceptance

**Status: COMPLETE / OWNER ACCEPTED — 2026-10-21, within the final bounded scope below.** Automated matrices, failed trials, attended observations and explicit exceptions remain separately attributed. Historical T02 attended reports remain candidate-specific. Operational release prerequisites remain UNACCEPTED / UNSCHEDULED.

[Plan](../../plans/20261021-001-business-compact-lookup-and-visual-parity.md) · [Acceptance ledger](../../BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) · [Original reference register](../../design-references/business-compact/README.md)

## Candidate and execution boundary

The isolated branch is `codex/bcf-t00-native-lookup-proof`. T06 starts at `95591071ea214be88813deb5227d216f1b0bca71`; product behavior is unchanged from T05 `991530f0a3505b10294f2821e8ab434c2f1c8a25`. T06 initially changes test drivers only. Product, Desktop and test targets remain net48; shipped package and GUI test host are Windows x86. SDK 8.0.425 is tooling. No API, schema, permission or business-field change is introduced.

| Dependency | Frozen source |
|---|---|
| BootstrapSourceGrid | `2bd7874f7e80b22ef181763749c2a2938ab7f16e` |
| Bootstrap5WinFormUI | `2be9a4d778fda7016207b75470020be6126f2f3f` |
| SourceGrid | `f4e457b43582bf01892f50bdc74aa480531e5944` |

The clean x86 package was produced through `Publish-LocalRelease.ps1 -Version 1.0.0-bcf-t06 -OutputRoot TestResults/bcf-t06-package-9559107`. Its source is `95591071ea214be88813deb5227d216f1b0bca71`, dirty=false. The manifest contains **205 files: Desktop133 and Admin72**; its SHA-256 is `3376D54C49FAD98152CB5A8BC7F94EDFEF8F7E9D13B15A16A3A126D642347447`. Separate desktop/admin packages and existing secret/configuration checks remain intact. The local manifest and raw logs remain ignored. During the final native125 run, all205 manifest entries are rechecked against their actual length and SHA-256, with no drift; the manifest hash is unchanged.

The package source pin and test assembly revision are distinct. `MYDMSVN_GUI_P5_TEST_REVISION` is the existing package-source admission variable despite its historical name; it is not relabeled as the driver source revision.

| Artifact | SHA-256 |
|---|---|
| Packaged MyDmsVn.Desktop.App.exe | `7CF85A3E5CF4DC2EBE12CF7FAA3140B770306DEBEDBE7E60BB1CD4C49A287165` |
| Packaged MyDmsVn.Desktop.WinForms.dll | `7AD87522D2A3BBFF0528F127BF71C5D9D57F1BCCC20C7CEAAA33F10A901A9AC8` |
| Packaged MyDmsVn.Server.DbMigrator.exe | `D00634DE30AB3DCA18E475FE78152107CF35660686EDA9BBA5D7FB77EA4B94E1` |
| Hosted MyDmsVn.Desktop.WinForms.dll | `D8D848A665126121079CDA86C24CCF7B55A21D6B56459021CDE6ADDFCF46DB8B` |
| Hosted MyDmsVn.BootstrapSourceGrid.dll | `D37A6195D6B090061B22AF526FBCF8A58192BD74A99BC422D49AA80DCA3149FD` |
| Hosted MyDmsVn.Bootstrap5WinFormUI.dll | `10256548B0F967F94162B9ADE431DD630F54201280F576DE35D023DB1503788A` |
| Native100 broad Desktop.Tests.dll, test source9bb2e64 | `4477E9AD2C7201831D7663E69C38D06A428F6B4C0DBC8E21F43EC967BD06A4AC` |
| Native100 repaired49+Dark116 Desktop.Tests.dll, source46e52a8 | `13F410E68B74004B333DFB8CABBFDE7AE3AE166ACA80F4696E5F6427A76AFA7E` |
| Initial capture telemetry+attended100 Desktop.Tests.dll, source6e164e8 | `6E2861730DDD395DB7B156B5027EA87FE692AD8714289D2B3CB2A92CF5CDCA76` |
| Corrected capture provenance Desktop.Tests.dll, sourceec4a382 | `4D37738EC8904553B380E82E63CEF03DB2EB1A09AA9BE26162A00FD396FCC89E` |
| Attended foreground-safe transition Desktop.Tests.dll, source7518e7b | `039538D221B632926B095DE6A764FC903A206ADF715A49974C8E33E346B99251` |
| Desktop.Session.Tests.dll, source9559107 | `0902F37F56D9B395C0D28F41EB0B5BA0CE9F8AE0A9A27C211BCF906563B1506B` |

Hosted and published binaries have distinct build/version metadata and hashes; they are not claimed byte-identical. Their product source content is unchanged. The test-only9bb2e64 build uses `BuildProjectReferences=false`, with explicit runtime hash equality before/after; package bytes are retained independently.

The repaired drivers use ordinary New/Back/Open admission; they do not expose hidden editors or bypass document guards. Reopen waits for the actual asynchronous Detail editor. Product identity comes from committed inline accessibility Help and is still checked independently in SQL. The recovery keyboard probe now starts on the visible Back button and uses native Tab to Refresh, preserving the explicit native Enter reload and checked retry. No implicit Post retry or mutation-unlock shortcut is added.

## Preparation failures and corrections

These trials precede integrated verification and are retained separately. None supplies a full-suite PASS.

| Trial | Observed outcome | Disposition |
|---|---|---|
| Package expected125 at initial startup | Actual package monitor100; startup precondition failed | Run actual100 first; human scale change required before125 |
| Package theme-state reader | Actual Login succeeded, but UIA HelpText was the action tooltip | Read exact current-theme native MSAA Description through public `AccessibleObjectFromWindow`; validate owned HWND and release COM |
| Managed LegacyIAccessible diagnostic | net48 temporary probe compile failed | Unsupported diagnostic removed; not product RED |
| Receipt invalid / initial receipt setup | Initial List has no header/detail editor | Use admitted New before detail input |
| Products package driver | Old New label and ID column assumptions | Use Add product; obtain Product key from committed inline selection; independent SQL ID/effect assertions retained |
| Historical receipt real reopen | Immediate Note read raced asynchronous Open | Bounded wait for actual Note editor before checking persisted input; focused history Light1/1 PASS |
| Attended legacy driver | List setup failed with Input unavailable: Warehouse;1/1 FAIL | Observe filter in List, then admitted New and committed-header setup, then headers in Detail; human phase sends no automated query keys |
| Session default test-host platform |107 BadImageFormat failures,1 PASS | Configuration error; complete x86 rerun108/108 PASS, no skip |
| Boundary command without Release | Debug DLL not found | No test or SQL admission; corrected Release command |
| Complete native100, CatalogAcceptance | Six Product rows still read old positional Code/Name columns, now Name/Unit after intentional ID removal | Preserve original failures; use unique public headings to assert the same exact expected values, rows and cancellation/error effects; focused49-row repair verification PASS, with the original failures retained |

Theme/Login Light+Dark2/2, receipt-invalid Light+Dark2/2, catalog Light1/1, full receipt Save/reopen/edit/Post/stock Light1/1, direct-deny Light1/1 and real SQL uncertain-Post/reload/explicit-retry Light1/1 passed in preparation. These are scoped trials, not the final two-DPI integrated matrix. The three-row boundary trial retained its history failure alongside the two passes; the repaired history case later passed separately.

Every completed preparation allocation has terminal `DatabaseAndAllPrincipalsDroppedAfterVerifiedDrain`; failed owned-probe roots report cleanup verified=True. The designated non-production test connection has effective master, and its value is never included here. Allocations use existing independent migration/runtime/oracle identities and checked ownership; no prefix cleanup or application database is used.

## Integrated results

The complete Desktop.Session project discovers108 and executes108/108 PASS at x86, zero skipped. Raw TRX SHA-256: `2A02D336EEF02C01C12A6759E18067E8AA716A04EA0A2A0C2D0DDD8F6C0F7B99`. Its source/build is9559107 before the Desktop-test-only driver changes; no Session source/dependency behavior changed afterward.

Desktop initial discovery contains576 rows:575 automated and one explicitly opt-in attended case. Native125 additionally has six strict wide viewport rows unavailable on the Owner's display. The two fitted legacy Layout rows and four width-labeled ReceiptAcceptance rows remain runnable; they check reachability on the actual fitted host and do not assert an exact1680-wide client or substitute for strict captures. Native125 selects/discovers/executes569 rows with no name differences; only the six enumerated strict wide rows are excluded. The extra Dark selector discovers/executes116 with no name differences.

Only the lookup fixture consumes `MYDMSVN_BCF_THEME`; the extra Dark selector is101 binding+15 typing rows. The complete project includes its actual parameterized Light/Dark shell, layout, catalog, receipt and packaged theories. Setting that variable does not relabel every unparameterized control/inline test as Dark. Native-input observations, actual packaged Login and hosted captures remain separate kinds of evidence.

| Invocation | Actual result | Raw TRX SHA-256 |
|---|---|---|
| Native100 complete automated selection,9bb2e64 |575 discovered/executed;566 PASS,9 FAIL,0 skipped; discovery/execution name differences0 | `D7481A888F884812B10C7FD54A557E22F4CBF01B86F2EE71C01B0FE366B4B361` |
| Native100 repaired Catalog/Accessibility/receipt-capture/direct-deny scope,46e52a8 |49/49 PASS,0 skipped; all9 failed names included | `638020C34493DFC3146AA323B1924709F1837FFC586E47009914FC482A854B81` |
| Native100 additional actual Dark lookup,46e52a8 |116 discovered/executed;116 PASS,0 skipped | `F8D3A7570BF96741CB35DFD6FC0154D0E5726E1B5B855D28E7D785FC0FE8CD74` |
| Native125 complete runnable selection,7518e7b |569 discovered/executed;569 PASS,0 skipped;20 packaged rows PASS; six strict wide rows NOT TESTED | `20FBEDCDE17CEF0AE8B25364909EE4E0559E1131727590AA9B20CB23F8A11E10` |
| Native125 additional actual Dark lookup,7518e7b |116 discovered/executed;116 PASS,0 skipped | `2B72F5563BCC61BE93922CE0863683A190D101EE29BC48FE64930CF81FFBFD65` |

The native100 full run remains an unfiltered **FAIL**, retained intact. A case-name join with the49-row repair has575 unique originally selected cases, each with a subsequent applicable PASS, and no unresolved name; it is a **reconciled result across two stated test revisions**, not a new575/575 TRX. Runtime/vendor/package bytes are unchanged across that repair. The20 packaged rows reconcile18 initial PASS plus the two repaired direct-deny PASS. All19 SQL allocations in the complete run and both allocations in each direct-deny repair reached verified terminal cleanup. Native100 Light and Dark each supply116 independent observer-cleanup records. The [sanitized case register](20261021-t06-case-results.csv) contains names/outcomes/durations only, preserving the nine original failures separately from repair rows.

Source6e164e8 adds capture-state telemetry only and initially passes4/4 receipt capture rows. Sourceec4a382 corrects the read order: native Capture pumps layout/scroll messages, so provenance is read afterward, with first visible line/column and only visible scroll owners. The corrected x86 invocation passes4/4, zero skipped; TRX SHA-256 `5C4B4342024AF2BC6C8C9808089C1D8C76CCB6B47BC47214E84696A44D30EAF5`. Its preceding command omitted x86 and failed framework assembly loading before GUI/SQL admission; that setup trial remains separate. The24 final state records replace the initial before-Capture offsets; for the1200→944 dirty state they observe selection11;2, line scrollY3/first visible line4 and document scrollY−136. No production behavior change is involved.

Native125 measures monitor120/window96 DPI, physical window1895×1105 and native client1875×1056 for the lookup observer; it is not a DPI-aware product conversion. Light and additional Dark each have116 verified observer-cleanup records. All19 native125 SQL allocations reach terminal verified drain/drop; no owned GUI/test-host process remains before the attended session. The full runnable round takes21m34s, extra Dark1m35s. The case register now contains1539 invocations including overlaps/repair/Session/capture rows, not1538 unique mandatory cases.

## Current-candidate attended evidence

Native100 has four separate human reports on the unchanged9559107 package. The controller records the following observations (UTC+07); no report is inherited from T02.

| Surface / theme | Human report | Session boundary |
|---|---|---|
| Filter / Light | “Xác nhận: ổn” | r2, recorded18:12:26 |
| Filter / Dark | “Xác nhận: ổn với Dark” | r2, recorded18:13:58 |
| Warehouse+Employee / Light | “Xác nhận: 2 ô đều ổn” | Detail-only7518e7b, recorded18:21:03 |
| Warehouse+Employee / Dark | “Xác nhận: 2 ô ổn với Dark” | Detail-only7518e7b, recorded18:23:36 |

The initial unattended-wait session timed out at the List phase after10m18s, with verified process/SQL cleanup and no human report credited. Sessionr2 observes both reported Filter themes, then fails during automated committed-header setup: after the human returns to chat, the native foreground guard correctly refuses the setup F4. Cleanup is verified; its independent no-inventory assertion was **not reached**. The valid prior Filter observations remain partial attended evidence; this failed whole-session case is not relabeled PASS.

Source7518e7b removes automatic query setup keys between human phases. New uses existing admission and a bounded Detail-editor wait; the human selects initial committed headers before typing. A validated `both`/`detail` test-only scope permits a separate header session without repeating the Filter observations. No foreground guard is weakened or bypassed. The Detail-only native100 case passes1/1 in5m21s with observed Light/Dark, application/output drain, independent no-receipt/inventory assertions and `DatabaseAndAllPrincipalsDroppedAfterVerifiedDrain`; raw TRX SHA-256 `A177D74BD146084E84C814973DEB7DEEC60C099DE5663B9D0AFAC09A3D19AF99`. No GUI process remains after that session.

Native100 human evidence is therefore composite across r2 and the repaired Detail-only session, not one all-three PASS Fact or an independent SQL no-write certification of r2. Snapshots supplement the reports: Filter Light ends empty/range0+0; Filter Dark ends with its committed label/range0+0. Header snapshots end with committed labels; Dark Warehouse has range0+30. All snapshots report focus false after returning to chat. They are not retroactively described as pending-gui or per-key recordings.

Native125 has one all-three owned session on the same frozen9559107 package and7518e7b driver. Human reports (UTC+07, recorded at the following snapshot acknowledgements) are separate from automation:

| Surface / theme | Human report | Recorded time |
|---|---|---|
| Filter / Light | “Filter ổn ở 125% Light” |19:01:33 |
| Filter / Dark | “Filter ổn ở 125% Dark” |19:02:15 |
| Warehouse+Employee / Light | “Hai ô ổn ở 125% Light” |19:03:00 |
| Warehouse+Employee / Dark | “Hai ô ổn ở 125% Dark” |19:04:19 |

The attended case passes1/1, zero skipped, in10m25.491s; raw TRX SHA-256 `EB1DDB02A9FF83DDFDB51E1AC66286BB3B28B284D9062C28001FE5C1852F890A`. Both List and Detail acknowledge actual Light/Dark. The independent `ATTENDED_NO_RECEIPT_OR_INVENTORY_WRITES=PASS`, application/output drain, terminal `DatabaseAndAllPrincipalsDroppedAfterVerifiedDrain` and zero remaining owned GUI/test processes are verified. No keyboard query is injected during the human phase.

Native snapshots supplement these reports: Filter ends with its committed label in both themes; Warehouse/Employee end with committed labels in Light; Dark Warehouse has its committed label and Employee ends empty. All ranges are0+0 and all focus observations are false after returning to chat. These snapshots are not pending-gui or per-key recordings and do not override the separate human typing reports. No Save/Post is authorized by a walkthrough; markers coordinate snapshots/session disposal and never supply a human outcome or Owner approval.

## Acceptance reconciliation and final disposition

| Obligations | Current-candidate evidence | Remaining boundary |
|---|---|---|
| A01–A03 | LookupTyping tests: first character, canceled preceding completion, response between keys, middle insertion and range replacement; native text/caret/focus/committed identity assertions | Native125 automated PASS plus separate all-three human reports |
| A04–A09 | LookupBinding tests: query versus commit, shared warehouse notifications, historical/active-only choices, cancellation/generation, native popup keys/mouse, query-only and rejected navigation | Native100/125 automated evidence reconciled; no whole-string setup counted as per-key proof |
| A10–A12 | Binding, inline/accessibility, control and Session cases: Posted/busy/denied/recovery locks, late callbacks/disposal, current identity, raw numeric input and retained row/editor ownership | Discovery/execution names reconciled; no unresolved automated case |
| A13 | Native100 Light/Dark automated lookup evidence plus the separately documented composite attended observations | Native125 all-three attended1/1 PASS; native100 composite failed-r2/no independent no-write assertion is explicitly accepted in the final disposition below |
| A14–A17 | All three unchanged references and matched Products/receipt images in the comparison index, plus composition/interaction cases | Material hierarchy, breadcrumb, icon, density, palette and command differences are explicitly Owner accepted below |
| A18 | Catalog loading/error/cancel, native focus/tab, lookup popup/accessibility, invalid numeric, readonly/recovery and theme-switch cases; corresponding state captures | Graphite native surfaces and deferred T04 error Minor remain explicit visual/behavior boundaries |
| A19 | Back admission/mouse-down/abandoned-click, pending Warehouse/Employee/Product, Posted/denied/recovery tests; actual package Save/Post/uncertain-reload workflows and independent SQL effects | Native12520 packaged rows PASS with positive owned cleanup |
| A20 | Measured100/125, logical1200×760 and944×600 state/reachability; native1001680×900 captures | Six strict wide125 rows NOT TESTED; fitted legacy hosts do not substitute |
| A21 | Heading-derived grid assertions, native tab/accessibility, numeric alignment and actual reviewed images | Typography/icon/spacing and native accessory differences are explicitly Owner accepted by control/state below |
| A22 | Frozen package source9559107, distinct test revisions/hashes, actual Login/catalog/receipt/permissions/recovery scenarios, owned resources | Native125 integration and all-three attended PASS; native100 composite limitation remains explicit |
| A23 | Sanitized case register, failed trials, source/package distinction, comparison/integrity/state registers | Explicit Owner acceptance recorded below; T06 COMPLETE in bounded scope |

## Comparisons and Owner disposition

The [comparison index](20261021-t06-comparisons/README.md) contains40 reviewed images, an exact integrity register and36 after-Capture receipt-state records across both native scales. Hosted production-control captures are labeled separately from actual packaged Login/workflow observations. Owner disposition is recorded below. All three original reference files remain unchanged. Six strict wide125 captures remain NOT TESTED under the explicit bounded Owner exception below. Native rendering differences, the deferred T04 query-error Minor and deviations in composition/copy/paging receive explicit final disposition below; A23/T06 close in that bounded scope.

## Final T06 Owner acceptance — 2026-10-21

The Owner explicitly replied **“Tôi chấp nhận giao diện có sai khác cùng các ngoại lệ và bằng chứng”** after the three final review questions covering all three original designs, bounded exceptions and the native100 composite evidence. This is acceptance of the concrete comparison/evidence at review checkpoint `aee176b`, not an inference from earlier lookup reports. **BCF-T00–T06 are COMPLETE / OWNER ACCEPTED in this bounded UI scope.**

The accepted package source is `95591071ea214be88813deb5227d216f1b0bca71`, with product behavior unchanged from `991530f0a3505b10294f2821e8ab434c2f1c8a25`. Final Desktop test source is `7518e7ba9756aadecea6206dd061de24454c4c1b`; the native100 broad/repair/capture and Session revisions remain individually attributed above. Documentation closure does not rebuild or change the accepted package. All exact runtime/test/package hashes and vendor pins above remain authoritative.

| Reviewed boundary | Explicit Owner disposition |
|---|---|
| References01/02/03 and the40 reviewed candidate images | Accept the enumerated differences in hierarchy, native typography/spacing/chrome, missing separate breadcrumb, toolbar/search icons, paging/count/status, command grouping, copy/sample data, constrained reflow and outer showcase frame versus client capture |
| Graphite native surfaces | Accept the control-specific light DateTimePicker field/drop arrow, list status ComboBox and native scrollbars; this does not claim full palette or pixel identity |
| Six strict1680×900 logical native125 rows (shared shell, Products and receipt in Light/Dark) | Accept a bounded hardware exception; they remain **NOT TESTED**, never PASS. Native100 wide captures and fitted legacy125 hosts do not replace them |
| T04 stale-query-error Minor | Accept deferral: generic catalog VM ErrorMessage can remain after a failed query followed by successful Refresh; no repair is claimed |
| Native100 attended composite | Accept the separate Filter Light/Dark reports from failedr2 plus passing Detail-only header reports. Failedr2 retains its automation-transition failure and lacks an independent no-write assertion; cleanup was verified. No all-three native100 PASS Fact is invented |
| Native125 attended | Accept the separately reported all-three Light/Dark observations and1/1 owned-session PASS, independent no-write assertion and verified drain/cleanup |
| Native100 initial broad run | Preserve566 PASS/9 FAIL and subsequent49-row repair. The575 unique-case reconciliation across test revisions is accepted; the original unfiltered run remains FAIL |

A01–A23 are reconciled through their stated evidence and these explicit dispositions. Historical T03–T05 OPEN checkpoints are retained as provenance; this final decision closes their listed visual/evidence boundaries without changing raw outcomes. The1539 sanitized invocation rows include overlaps and retained failures, not1539 unique mandatory passes. No source, screenshot, timing, SQL assertion or missing viewport is fabricated by this acceptance.

This decision does **not** authorize push, merge, production deployment, upstream vendor merge, or operational release acceptance. Release prerequisites remain **UNACCEPTED / UNSCHEDULED**; net48/x86 and the separate admin/runtime identities remain unchanged. No next implementation roadmap is selected.
