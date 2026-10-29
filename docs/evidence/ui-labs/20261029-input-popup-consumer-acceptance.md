# WinFormsKit input/popup consumer acceptance — candidate 2026-10-29

**Scope:** [selected consumer plan](../../plans/20261029-001-input-popup-consumer-acceptance.md) on MyDmsVn. Toolkit Owner approved the canonical 100%/125% Light/Dark visual set on [WinFormsKit PR #3](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/pull/3). This report records a separate app candidate. **Automated consumer gates and inspected UI Lab host surfaces PASS. On 2026-10-29 the Owner replied “Consumer acceptance: Tôi approve.” for the presented app candidate `003db0a` and its comparison board; consumer acceptance is APPROVED/CLOSED within this selected scope.**

## Candidate and boundaries

| Item | Exact candidate |
|---|---|
| App base | `main@7e4dc2ae8ca2d5087d14206ad5fbb1bdb085fea5` |
| App code/test/pin commit | `95916e33650648ef9753223605b5e679d6944d2a`; `src` tree `a5083a939408501cdf86e40fc6548e2b4323e5a4`, `samples` tree `53f7fe35de808c5c6d732791a3da753e31ae2bea`, `tests` tree `900e01fa440760c563edad23820ad88763562735` |
| WinFormsKit gitlink | Old `a5dd3cec6daba71943549afac628e13727c00da5` → exact candidate `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf`; toolkit production source execution revision `c060415141cfa3bf1867593326741edad2de0ae9` |
| Nested SourceGrid | `f4e457b43582bf01892f50bdc74aa480531e5944`, unchanged |
| App target | `net48`; UI Labs and selected tests on interactive Windows; toolkit `net8.0-windows` native gates belong to the separate PR #3 evidence |
| Build inputs | SDK `8.0.427`, locked restore, warm package cache; no SQL server, actual Login, package or release execution |

Only the toolkit gitlink, selected plan/index and one consumer regression changed. No app production `src` or sample source changed. The new theory checks Security catalog TextBox and native ComboBox equal height with actual HWNDs and verifies Refresh wraps within a narrow host without clipping in both themes. At `DeviceDpi=96` the measured filter inputs were 32 px high; wide `1200` host placed Refresh at `(365,3)`, narrow `550` host placed it at `(3,41)`. Product-host parity tests additionally cover `1200×760 → 952×657` and `900×600 → 652×497` in Light/Dark.

## Commands and results

Run from the isolated app worktree. GUI tests were serialized, STA with finite pumping and `--blame-hang-timeout 5m`. TRX files under `TestResults/input-popup-consumer-*` are local ignored raw results.

```powershell
git submodule update --init --recursive
git -C vendor/MyDmsVn.WinFormsKit fetch origin codex/plan-input-popup-rendering-20261029
git -C vendor/MyDmsVn.WinFormsKit checkout --detach 433e5b53f979e37cc6b69bad6dfcde0975c7ecaf
dotnet restore MyDmsVn.UiLab.sln --locked-mode
dotnet build MyDmsVn.UiLab.sln -c Release --no-restore -v minimal
dotnet restore tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj --locked-mode
dotnet build tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore

$uiFilter = 'FullyQualifiedName~InputPopupConsumerAcceptanceTests|FullyQualifiedName~Role_catalog_toolkit_surfaces_keep_read_only_theme_focus_and_copy|FullyQualifiedName~Role_catalog_render_preserves_selected_definition_native_row_and_filter_identity|FullyQualifiedName~Role_catalog_same_owner_render_keeps_native_member_and_catalog_rows_scroll_and_identity|FullyQualifiedName~Role_catalog_native_filter_retains_caret_range_key_and_enter_never_writes|FullyQualifiedName~Sidebar_security_matches_direct_lab_content_and_retains_editor_on_theme_toggle'
$lookupFilter = 'FullyQualifiedName~Cell_editors_and_popups_remain_usable_after_scroll_resize_and_theme_change|FullyQualifiedName~Popup_mouse_selects_non_first_result_once|FullyQualifiedName~Back_click_with_non_first_highlight_preserves_native_range_when_rejected|FullyQualifiedName~Native_lookup_keyboard_cancel_selection_and_admitted_disposal_are_owned|FullyQualifiedName~Theme_switch_preserves_the_same_inline_lookup_session_native_text_and_caret'
dotnet test tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj -c Release -f net48 --no-build --no-restore --filter $uiFilter --blame-hang --blame-hang-timeout 5m --logger 'trx;LogFileName=consumer-ui-100.trx' --results-directory TestResults/input-popup-consumer-ui-100
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-build --no-restore --filter $lookupFilter --blame-hang --blame-hang-timeout 5m --logger 'trx;LogFileName=consumer-lookups-100.trx' --results-directory TestResults/input-popup-consumer-lookups-100
dotnet test tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj -c Release -f net48 --no-build --no-restore --filter $uiFilter --blame-hang --blame-hang-timeout 5m --logger 'trx;LogFileName=consumer-ui-125-rerun.trx' --results-directory TestResults/input-popup-consumer-ui-125-rerun
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-build --no-restore --filter $lookupFilter --blame-hang --blame-hang-timeout 5m --logger 'trx;LogFileName=consumer-lookups-125.trx' --results-directory TestResults/input-popup-consumer-lookups-125
dotnet test tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --filter $uiFilter --blame-hang --blame-hang-timeout 5m --logger 'trx;LogFileName=consumer-ui-100-x86.trx' --results-directory TestResults/input-popup-consumer-ui-100-x86 -- RunConfiguration.TargetPlatform=x86
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --filter $lookupFilter --blame-hang --blame-hang-timeout 5m --logger 'trx;LogFileName=consumer-lookups-100-x86.trx' --results-directory TestResults/input-popup-consumer-lookups-100-x86 -- RunConfiguration.TargetPlatform=x86
```

Locked restore and Release solution build PASS with zero warnings/errors. The focused old-pin baseline was 7/7; same selected 7/7 passed immediately after the new pin. Candidate selected rows:

| Run | Discovered | Executed | Pass | Fail | Skip |
|---|---:|---:|---:|---:|---:|
| 100% Security + product-host, net48 | 14 | 14 | 14 | 0 | 0 |
| 100% Sales/GoodsReceipt Lookup + editor, net48 | 25 | 25 | 25 | 0 | 0 |
| 125% Security + product-host, net48, clean rerun | 14 | 14 | 14 | 0 | 0 |
| 125% Sales/GoodsReceipt Lookup + editor, net48 | 25 | 25 | 25 | 0 | 0 |
| 100% Security + product-host, `PlatformTarget=x86` | 14 | 14 | 14 | 0 | 0 |
| 100% Sales/GoodsReceipt Lookup + editor, `PlatformTarget=x86` | 25 | 25 | 25 | 0 | 0 |

Lookup selection contains GoodsReceipt mouse choice 3 rows and rejected Back preservation 2 rows, Sales native key/cancel/disposal 16 rows and theme/caret 2 rows, plus 2 editor/scroll/resize rows. Security selection contains the new geometry theory 4 rows, catalog focus/copy/theme/selection/scroll 6 rows and product-host parity 4 rows.

**Retained failed attempt:** The first 125% Security run discovered/executed 14 and produced 12 pass, 2 fail, 0 skip. `NativeLookupTypingDriver.Activate` expected its test form to own the foreground HWND, but found `198152`, the Windows Settings window used to change Scale. Settings was closed, then the exact 14-row gate reran and passed 14/14. No source/test assertion was weakened; the failed TRX remains in ignored `TestResults/input-popup-consumer-ui-125/consumer-ui-125.trx`.

Final x86-built test DLL SHA-256: UI `42f0d6d5233fc15d4d6f134eaa5f6a6a336461d685b4221bac9ce0639b762f4b`; Desktop `899ef65187b6718d2fd2214128bf30c4466def3ebc0d48ae5448b226c605e897`. UI Lab launcher SHA-256: Security `0da5464d1a1bc79525d5c7d1cb23405e69b334949be946597e31b29507ab7c56`, Sales `740bc925c26c91fa0d44a5742f054aa93e0a8d4306bcec280a2188fbf62273ac`.

## Interactive host observations

Windows Settings showed Scale **100%** during the first capture and **125% (Recommended)** before the second capture. The Labs were closed and relaunched after switching to 125%; Windows was restored to 100% afterward. At 125%, physical screenshots enlarged from approximately `1235×843` to `1545×1054`, while each net48 Lab reported `DeviceDpi: 96` and `Physical scale: unverified` in its own status text. The Windows Scale selection establishes the display setting; the Lab's 96 value is retained as an app diagnostic, not claimed as a native 120-DPI app measurement. No independent monitor-DPI API probe was run in this consumer pass.

The interactive Security catalog showed TextBox/ComboBox aligned, Refresh reachable, selected permission row and detail panel intact in Light/Dark at both physical scales. The native lifecycle ComboBox popup opened with all three options visible. Sales `S01Draft` showed the Customer Lookup popup anchored under its field, with row/header/footer and focus visible in both themes at both scales. The sample had one result (`1/1`); multiple-row selection and cancellation are covered by the native test rows above, not inferred from the screenshot. GoodsReceipt was verified by native tests, not captured as a visual sample. No clipping was observed in the presented surfaces; a screenshot cannot prove keyboard, accessibility or disposal behavior.

The local comparison board is `artifacts/verification/20261029-input-popup-consumer/index.html` (served during review at `http://127.0.0.1:8766/index.html`). Its 14 JPG captures and local raw TRX are ignored and are not part of the Git candidate. The board presents Security catalog and Sales Customer Lookup at 100%/125% × Light/Dark, plus selected native ComboBox popup frames. Captures contain only SQL-free Lab fixtures.

## Owner decision and remaining boundaries

- **PASS for this candidate:** exact toolkit pin and SourceGrid provenance, locked restore/build, focused native/geometry/product-host tests at 100% and 125%, x86 net48 repeat at 100%, interactive SQL-free Lab observation and review board preparation.
- **APPROVED/CLOSED for this selected consumer scope, 2026-10-29:** after review of the app board, the Owner replied **“Consumer acceptance: Tôi approve.”** The decision applies to app evidence candidate `003db0aaf2cc06e5ec1c126b5dc5e04d89870e48` (code/test/pin `95916e33650648ef9753223605b5e679d6944d2a`) with exact WinFormsKit pin `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf`. This documentation update records that decision without changing the reviewed source, tests or gitlink.
- **Outside this selected scope:** actual Login/SQL, package/release, physical 150%/200%, complete Security scenario matrix, Sales attended business acceptance and toolkit pairwise matrix. None is inferred as PASS from this consumer check.
