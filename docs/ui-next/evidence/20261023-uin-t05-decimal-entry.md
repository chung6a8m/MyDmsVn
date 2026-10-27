# UIN-T05 — native decimal entry and receipt adoption

Date: 2026-10-23. Scope: UIN-A06 and the affected UIN-A04 receipt admission paths. This is source and focused native-test evidence for T05; package, actual Login and dated Owner acceptance belong to T13.

## Candidate and behavior

| Slice | Exact revision | Result |
|---|---|---|
| UI T05a | `d36febd3270cfba01bec545e7cb92fce2be1e0b5` | `BootstrapDecimalInput` retains lexical pending text separately from accepted value; native grouping, selection, clipboard, history and IME-message handling use the T04 parser/projection. Existing formatted input defaults remain. |
| BSG T05b | `6b5883d9dbf797561cc12fff76cd7545f187087c` | Grid-owned decimal editor parses without committing, projects only after SourceGrid accepts, and resets from the current model on reuse/cancel. Pins the UI revision above. |
| App profiles | `421a119d697d6956516e63c1c442dfd53577c130` | Quantity, unit cost and unit price use the existing receipt bounds/scale and frozen culture. |
| Sales T05c | `e03cdc2126dfc14792853c1de5a557b3be2e2867` | Replaces the receipt numeric editor with the BSG adapter while retaining app-owned lexical admission, stable line identity, Save/Post/Back and R4 feedback. |
| Goods T05d | `b5faa616115cdde75339c6338b9e3461953ebf30` | Uses the same adapter; cancelled document rebuild clears the pending control before profile reconfiguration and refreshes saved-state presentation. Removes the unused app `ReceiptNumberEditor`. |
| Final native proof tests | `6579e281f411eb6ecb4be33d4f76a0b93b66984b` | Adds independent PerMonitorV2 observation to both app pilots and Light/Dark Goods rows. |

App gitlink consumes BSG `6b5883d9dbf797561cc12fff76cd7545f187087c`; nested BSG gitlink consumes UI `d36febd3270cfba01bec545e7cb92fce2be1e0b5`. SourceGrid remains `f4e457b43582bf01892f50bdc74aa480531e5944`. UI public API fingerprint is `702dbc491fa2022903e6fca0c6722375e34a6c154046967fc676d21251c6c779`. No Desktop.Application/transport DTO, schema or SQL change is part of T05. `ReceiptNumericInput` remains the app admission policy and oracle.

## Tests and measured native context

Interactive Windows NT 10.0.26300.0, SDK 8.0.426, VSTest 17.11.1, Release. App and net48 library testhosts used x86; net8.0-windows library tests used x64. Runs used actual TRX, guarded STA, bounded hang collection and serialized GUI execution. Selected rows were discovered and executed; counts below have zero failures or skips. TestResults are local ignored artifacts, not committed screenshots or raw private data.

| Candidate scope | Local TRX | Executed/result |
|---|---|---|
| UI T05a + T04/legacy/API gate, net48 | UI `TestResults/uin-t05a-final3-net48` | 81/81 PASS |
| UI same scope, net8.0-windows | UI `TestResults/uin-t05a-final3-net8` | 81/81 PASS |
| BSG full suite, net48 | BSG `TestResults/uin-t05b-final3-net48` | 256/256 PASS |
| BSG full suite, net8.0-windows | BSG `TestResults/uin-t05b-final3-net8` | 256/256 PASS |
| UI native interaction on measured 125%, net48 / net8.0-windows | UI `TestResults/uin-t05-ui-native125-net48` / `uin-t05-ui-native125-net8` | 9/9 each PASS |
| BSG decimal editor on measured 125%, net48 / net8.0-windows | BSG `TestResults/uin-t05-bsg-native125-net48` / `uin-t05-bsg-native125-net8` | 13/13 each PASS |
| App Sales/Goods, numeric oracle/profile, feedback/accessibility, net48 x86, native100 | App `TestResults/uin-t05-app-final100/uin-t05-app-final100.trx` | 327/327 PASS |
| Sales new grouping/admission, Light/Dark, native125 / native100 | App `TestResults/uin-t05-sales-observed125` / `uin-t05-sales-final-observed100` | 2/2 native125; 3/3 native100 including Sales document-reset regression |
| Goods new grouping/admission, Light/Dark, native125 / native100 | App `TestResults/uin-t05-goods-observed125` / `uin-t05-goods-final-observed100` | 2/2 native125; 4/4 native100 including saved-state and readonly regressions |
| Sales numeric error resolution, Light/Dark, native125 | App `TestResults/uin-t05-sales-validation125` | 4/4 PASS |

Before implementation, UI native stub gave 5 failed/1 passed and BSG stub gave 3 failed/3 passed; the Sales and Goods grouped-entry cases also failed against their former ungrouped editors. The first broad Sales attempt had one unrelated focus precondition failure while another GUI testhost was running; the isolated retry passed 4/4, then the serialized scope passed 153/153. The first Goods scope failed two real adapter lifecycle/presentation cases; the fixes above passed their targeted rerun 2/2, then Goods scope passed 219/219. These preliminary results are not counted as final PASS.

The user supplied real display settings. An independent observer with `PerMonitorV2` recorded `MONITOR_DPI=120`, `MONITOR_SCALE=125` for both Sales and Goods Light/Dark windows; it recorded `MONITOR_DPI=96`, `MONITOR_SCALE=100` for their final native100 runs. The app testhost reported `WINDOW_DPI=96` at both settings because its WinForms window is DPI virtualized. The observer measured physical Sales client `1500×950` and Goods client `1062×325` at 125%; monitor work area was `1920×1020`. These monitor measurements, together with native per-character tests, establish the display contexts; the virtualized window DPI alone does not. UI/BSG native125 tests ran in the same measured session. The IME case drives native composition messages; a physical IME-language typing session was not separately observed.

## Boundary

UIN-A06 and the T05 contribution to UIN-A04 are verified for the changed control, adapter and two receipt pilots at the tested source/test pins and native contexts. This does not assert packaged-application Login, attended visual sign-off, full UI-NEXT A14 integration or operational release acceptance. No SQL owner was selected because T05 does not change persistence.
