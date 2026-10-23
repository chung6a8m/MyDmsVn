# BCF-T05 — guarded receipt List/Detail presentation

**Date:** 2026-10-21. **Status:** IMPLEMENTED / focused verification PASS for all runnable cases; six strict wide native125 capture rows remain NOT TESTED. Full BCF/Owner acceptance remains OPEN. **Baseline:** `1ec4703944f15cd01554c821903468ea5b9800ab`, isolated branch `codex/bcf-t00-native-lookup-proof`. [Plan](../../archive/20261023-pre-ui-next/plans/20261021-001-business-compact-lookup-and-visual-parity.md), [design](../../BUSINESS_COMPACT_REFINEMENT.md), [acceptance](../../BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md).

## Implemented boundary

Goods Receipts starts in List; successful New/open enters Detail. The same control, editor, document and inline adapter survive presentation changes. Back is a navigation intent admitted before native focus validation, with mouse-down capture protecting pending Warehouse, Employee and inline Product input, including abandoned clicks. Dirty/pending data, busy and uncertain-Post states reject Back; clean Posted inspection is permitted. Admitted query-only Back cancels request generations and presentation without selecting a highlighted candidate. A denied list remains cleared and cannot restore protected cached data.

List search, warehouse/status filters, page, selected result identity and scroll survive Back/resume. Unchanged result projection retains its cells; changed result projection restores selection by ID without acquiring native editor focus. Reopening the retained clean receipt does not fetch/reload or reset line identity/selection. Explicit New/load/Refresh remain document boundaries.

Detail uses the available workspace, three/two/one metadata columns and a spanning Note, title/number/status/saved-state hierarchy, Post primary, Save secondary, line count/Add/Remove above five existing line columns, and a prominent total plus visible status/error footer. Numeric safety widths, precision and parser contracts are unchanged. The opaque Version remains synchronized for concurrency, outside the visible tree, with explicit disposal. List mode also closes edit admission for cached accessibility objects without replacing the adapter.

No API/schema/permission/module, SQL or vendor change. Both product and tests remain net48/x86. No package, actual Login, new attended receipt observation, full-project gate or Owner visual acceptance is claimed. T06 is NOT STARTED. Historical T02 attended evidence, T03 wide125/chrome limits and T04 stale catalog-query-error Minor remain in their original ledgers.

## Development evidence and review

- Initial nine new guard rows fail for the missing presentation API/buttons, then pass 9/9. A preceding compile typo is setup failure, not product RED.
- First GREEN attempt has seven passes and two Product native-focus precondition failures. The inline EDIT already owns focus; the generic driver refocused the composite wrapper. Retaining existing native EDIT focus fixes setup without relaxing HWND/process/EDIT ownership or native caret assertions.
- The result-cell retention regression first has an invalid sticky-dirty fixture assumption, then produces intended `Assert.Same` RED. Stable row projection fixes it. The final case uses 25 rows, real search/warehouse/status/page actions, row 21 and nonzero scroll.
- Broader trials preserve old split/focus assertions as failures before deliberately updating them for List/Detail. A Light outline Refresh focus cue loses contrast; the existing pixel test reproduces it, and the app-owned cue now uses the theme text color for outline buttons.
- One fresh read-only review reports an Important List edit-admission gap and a Minor Saved label ignoring numeric buffers. The initial cached setter already throws because native focus fails; no mutation is claimed as observed. A strengthened accessibility ReadOnly-state assertion is RED, then GREEN after mode-aware admission. Numeric-buffer Saved-state RED becomes GREEN after pending-data calculation and candidate-change notification; query-only input remains Saved. The targeted correction run passes 15/15. Early popup-highlight and Escape preconditions are retained as setup failures, not product RED.
- Product/header canceled queries and ignored-cancellation late responses, three/two/one-column native text/caret/range/focus preservation, Posted/denial/recovery, cached accessibility state, list/document identity and numeric rejection have focused coverage.
- The first frozen source `8aea161e7b78d824949cd114f33165801474c07d` passes 300/300 native100 rows. A later ownership assertion exposes the unparented VersionLabel disposal omission. One disposal RED and one already-passing reload regression are preserved separately. Capture setup is corrected to use admitted Save after dirty navigation; direct VM.Load had produced an artificial stale workflow message. Final candidate and its results are recorded below.

## Final verification

**Frozen product/test source:** `991530f0a3505b10294f2821e8ab434c2f1c8a25`. Windows NT 10.0.26200.0, SDK 8.0.425 selected by global.json, net48/x86, interactive serialized guarded STA. Native100 independent observers measure monitor 96/window 96 DPI. After the human Display Scale change, native125 measures monitor 120/window 96 DPI. No rebuild occurs between final native contexts; all listed artifact hashes are verified unchanged afterward.

| Frozen test-host artifact | SHA-256 |
|---|---|
| MyDmsVn.Desktop.Tests.dll | `C26A7C511B1FA810DFAC420E509092CD2DA0ADDA116D9A4044186076F50B1800` |
| MyDmsVn.Desktop.WinForms.dll | `4FB0C91F935138C4187321D1CB212421B467A502E399592178CFF5EDFD039C93` |
| MyDmsVn.Desktop.Application.dll | `13EF74A2EFBA1E597E83F6FD9B3CA3FBA1AA63BD0DF1DCDAAFE50226707D8942` |
| MyDmsVn.BootstrapSourceGrid.dll | `D37A6195D6B090061B22AF526FBCF8A58192BD74A99BC422D49AA80DCA3149FD` |
| MyDmsVn.Bootstrap5WinFormUI.dll | `10256548B0F967F94162B9ADE431DD630F54201280F576DE35D023DB1503788A` |
| SourceGrid.dll | `65B81600F2ECF92A2AD465401BE887B384DCEAA417201B1DE55DFC3B4237D7F0` |

Vendor pins remain BootstrapSourceGrid `2bd7874f7e80b22ef181763749c2a2938ab7f16e`, Bootstrap5WinFormUI `2be9a4d778fda7016207b75470020be6126f2f3f` and SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`.

| Frozen invocation | TRX definitions / executed / passed | Outcome |
|---|---|---|
| Native100 affected scope; parameterized composition/layout cover both themes | 301 / 301 / 301 | PASS |
| Native100 supplementary scope with Dark declaration | 185 / 185 / 185 | PASS; 116 measured Dark lookup rows plus 69 default-theme supplemental guard/inline rows |
| Native125 runnable affected scope; parameterized composition/layout cover both themes | 295 / 295 / 295 | PASS within runnable scope; six strict wide rows unselected/NOT TESTED |
| Native125 measured Dark lookup scope | 116 / 116 / 116 | PASS; all 116 rows are measured Dark lookup cases |

All four invocations have zero failed, skipped/not-executed, timed-out or aborted rows: 897 executed passes in total. Each has 116 native lookup observer records with verified owned cleanup and fixture drain, 464 across native100/125 Light/Dark. Binding101 and Typing15 use distinct clean-disposal log formats; both are reconciled. The Dark environment variable is consumed by GoodsReceiptLookupFixture, not by every test class: **only those 116 lookup rows in each supplementary invocation establish Dark lookup evidence**. The 69 nonparameterized native100 supplemental control/inline rows do not establish Dark coverage. Native125 runs only the 116 measured lookup rows for its Dark supplement. Both actual receipt themes have separate composition cases in the affected scope. This distinction does not alter historical ledgers or reuse their PASS counts.

Discovery independently lists 301 affected rows, 295 runnable native125 rows and 185 native100 supplementary rows; its 116-row lookup subset matches the native125 Dark invocation. All selected names match the corresponding TRX exactly, with no missing or extra rows. The affected classes are Layout11, Refinement31, Catalog14, FoundationShell16, GoodsReceiptControl48, InlineAccessibility6, InlineEditor15, LookupBinding101, LookupTyping15, InventoryAccessibility14, named session-expiry1 and ReceiptAcceptance29. Native125 removes only six strict wide Refinement rows: Light/Dark shell, Products and receipt composition at 1680. The two fitted legacy 1680 layout rows remain selected and cannot certify a full 1680×900 logical client. All owned observers finish with verified cleanup; no testhost or product/probe GUI process remains after the final round. No SQL resource is allocated.

| Local TRX under TestResults/ | SHA-256 |
|---|---|
| bcf-t05-frozen-native100/desktop.trx | `20A7333FAAAE443DE7DA90672CCBE541342B4A485B996BBDBE1B8647AEEDFF0A` |
| bcf-t05-frozen-dark-native100/lookup.trx | `B775F9190B9A760E651C02CF4207F7AE6728AC80A0465E64AB8181902AFACDC4` |
| bcf-t05-frozen-native125/desktop.trx | `96FE334C66AB3D91C72BE8EF6EC2F8E4D96BD80136001AA39E93881D4A5045C6` |
| bcf-t05-frozen-dark-native125/lookup.trx | `76D6990FD551C4CE7787506A2FE6805D821AB6A6C54DCE10795CF56CCA89F427` |

Raw proprietary TRX/logs stay local/ignored. The commands are ordinary direct project execution, not a new gate/runner. Build the frozen source with `dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --filter $scope --logger 'trx;LogFileName=desktop.trx' --results-directory <context-directory> -- RunConfiguration.TargetPlatform=x86`; subsequent contexts use `--no-build --no-restore` on those same outputs. Declare `MYDMSVN_BCF_EXPECTED_SCALE=100|125`, matching `MYDMSVN_BCI_EXPECTED_SCALE`, and `MYDMSVN_BCF_THEME=Light|Dark`. Independent observers measure actual scale; declarations do not simulate DPI.

```powershell
$scope = 'FullyQualifiedName~GoodsReceiptControlTests|FullyQualifiedName~GoodsReceiptInline|FullyQualifiedName~GoodsReceiptLookup|FullyQualifiedName~BusinessCompactLayoutTests|FullyQualifiedName~ReceiptAcceptanceTests|FullyQualifiedName~InventoryAccessibilityAcceptanceTests|FullyQualifiedName~BusinessCompactRefinementTests|FullyQualifiedName~FoundationShellFormTests|FullyQualifiedName~CatalogControlTests|FullyQualifiedName~Session_expiry_clears_private_workspace'
$limited = "($scope)&((FullyQualifiedName!~Shared_shell_capture_preserves_catalog_identity_and_reachable_session_at_logical_viewports&FullyQualifiedName!~Product_catalog_composition_matches_reference_in_both_themes&FullyQualifiedName!~Receipt_composition_and_commands_match_reference)|DisplayName!~1680)"
$supplement100 = 'FullyQualifiedName~GoodsReceiptLookup|FullyQualifiedName~GoodsReceiptInline|FullyQualifiedName~GoodsReceiptControlTests'
$darkLookup = 'FullyQualifiedName~GoodsReceiptLookup'
```

## Captures and remaining acceptance

The [capture register](20261021-t05-receipt-captures/README.md) records 26 reviewed final-candidate synthetic screens:14 native100 and 12 native125. Saved reference comparisons have 18 lines on the same receipt 41, date 2026-10-21, warehouse 3, employee 5, opaque Version, total 13162.50 and both themes. Query/option resolution displays the synthetic Choice captions; these are not new business fields or localization. Dirty/invalid/recovery captures preserve 18 lines and show rejected navigation, raw numeric text and uncertain-Post recovery. Successful Refresh uses the fixture's authoritative Posted response (one line, total 10.00), so it is a distinct state, not a claim of identical multiline data.

Native100 includes 1680×900 and 1200×760 saved comparisons and 1200 list views; native125 includes 1200×760 saved/list comparisons. Both include 944×600 constrained states after resize/theme switch. Error/recovery text and commands stay visible; metadata/grid may scroll intentionally, preserving the existing numeric minimum widths. PrintWindow images retain their measured logical client geometry; independent observers validate actual native monitor scale and cleanup. The images are not resized to impersonate a larger viewport.

Native125 full 1680×900 is unavailable on the owner's current display; six strict wide rows remain NOT TESTED, with fitted legacy layout results kept distinct. T03 native caption/accessory chrome limits remain visible, including native DatePicker treatment in Dark; no pixel-for-pixel parity is claimed. Full BCF comparison and explicit Owner disposition remain T06; operational release prerequisites remain unaccepted/unscheduled.
