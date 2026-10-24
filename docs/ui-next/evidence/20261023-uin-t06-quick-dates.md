# UIN-T06 — typed quick dates and required receipt headers

Date: 2026-10-23. Scope: the T06 contribution to UIN-A07, with UIN-A04/A03 admission and error regressions. T11 owns nullable Sales filters; T13 owns packaged Login and dated Owner disposition. This record covers source and focused test evidence, not production release acceptance.

## Candidate and behavior

| Slice | Exact revision | Behavior |
|---|---|---|
| UI T06a | `5a2d506176c0b78af7066ae07401d1f8b3187d9d` | Strict fixed-DMY `d`, `d/M`, `d/M/yyyy` parser, nullable/bounded profile, captured reference date and non-clamping day/month/year steps. |
| UI T06b final | `78c1673e1125b72ba282b6ab9254bafc642cec80` | Native text plus existing themed CalendarPicker; narrow `SelectionAccepted` event distinguishes an explicit same-date activation. Pending text survives ordinary projection, popup dismissal restores it, and native Tab/F4/Alt+Down/Escape/IME/theme/disposal paths and the popup reference across local midnight are covered. |
| BSG integration pin | `d28cc41d4d8ba5505c9dcc1cf09071e5f028556f` | Pins the UI T06b revision; no BSG editor/registry behavior changed. |
| App Goods T06c | `4939f6b` | Required date input in Goods Receipt; Save/Post and navigation admit pending date, while unrelated state updates preserve text. |
| App Sales T06d | `33e7d7a` | Same required date contract in Sales; existing permission, busy, unknown-Post and R4 feedback admission remains owned by the app. |
| App accessible errors and final pin | `9af5d245215fa2c934ca208ccd6f5a9941a87bad`, `655d4a23448e8d443aed21cd1227a882e7d6cfa2` | Local date failures present readable accessible text and field error state; final app gitlink consumes BSG above. |

SourceGrid remains `f4e457b43582bf01892f50bdc74aa480531e5944`. The reviewed additive UI API fingerprint is `82b06f761848921f9f1116db1d235a482dde830579e6c79fd10207a24138fdf5`. `BootstrapDatePicker`, DateTime/UTC Stock Card behavior, Sales From/To filters, Desktop.Application and transport DTOs remain outside this change.

## Tests and native context

Interactive Windows NT 10.0.26300.0, SDK 8.0.426, VSTest 17.11.1. App testhost is net48/x86. Library `net48` and `net8.0-windows` were built and exercised; the final net48 UI/BSG runs used x86 and net8 used x64. Test sessions are serialized STA with WinFormsTestGuard, finite waits and actual local ignored TRX artifacts. Counts below are selected/discovered/executed rows, with no skips where marked PASS.

| Scope | Local TRX | Result |
|---|---|---|
| UI T06a parser plus legacy formatter, net48 and net8 | `TestResults/uin-t06a-green-net48`, `TestResults/uin-t06a-green-net8` | 23/23 PASS on each TFM |
| UI date control, CalendarPicker, native DatePicker and public API on final UI pin, net48/x86 and net8/x64 | `TestResults/uin-t06-source-final-ui-net48`, `TestResults/uin-t06-source-final-ui-net8` | 127/127 PASS on each TFM |
| BSG full suite on final UI pin, net48/x86 and net8/x64 | `TestResults/uin-t06-source-final-bsg-net48`, `TestResults/uin-t06-source-final-bsg-net8` | 256/256 PASS on each TFM |
| Goods/Sales affected GUI regressions on app net48/x86 | `TestResults/uin-t06-app-final2/uin-t06-app-final2.trx` | 157/157 PASS |
| Local date accessibility and independent R4 feedback check | `TestResults/uin-t06-accessible-errors/uin-t06-accessible-errors.trx` | 4/4 PASS |
| Goods/Sales Light/Dark native100 on final app/UI/BSG pins | `TestResults/uin-t06-source-final-native100/uin-t06-source-final-native100.trx` | 4/4 PASS |
| Goods/Sales Light/Dark native125 on final app/UI/BSG pins | `TestResults/uin-t06-source-final-native125/uin-t06-source-final-native125.trx` | 4/4 PASS; independent PerMonitorV2 observer measured 120 DPI (125%) on every row |
| UI date/calendar keyboard and focus at native125, net48/x86 and net8/x64 | `TestResults/uin-t06-native125-ui-net48/uin-t06-native125-ui-net48.trx`, `TestResults/uin-t06-native125-ui-net8/uin-t06-native125-ui-net8.trx` | 64/64 PASS on each TFM |
| Stock Card/session regression | `TestResults/uin-t06-stockcard-regression/uin-t06-stockcard-regression.trx` | 9/9 PASS; Stock Card source unchanged |

The app GUI scope includes four native per-character Goods/Sales Light/Dark rows at each measured display scale. The independent `PerMonitorV2` observer recorded `MONITOR_SCALE=100`, `MONITOR_DPI=96` at native100 and `MONITOR_SCALE=125`, `MONITOR_DPI=120` at native125 on all four rows per run. Native125 physical work area was 1920x1020; observed physical windows were 1875x1063 and 1500x950, with native client sizes 1855x1013 and 1480x901. The x86 testhost window reported virtualized `WINDOW_DPI=96` at both scales; monitor DPI establishes the native125 context. Each character was observed through a native edit handle with raw candidate, caret/range, focus and unchanged model until Enter; accepted requests carry an exact date at midnight with unspecified kind. The direct UI tests cover same-date activation, child calendar-button opening, popup dismissal, Tab, F4, Alt+Down, staged Escape, wheel, IME composition messages, disjoint profile reuse, reference date across midnight, theme switch and disposal. Physical IME-language typing was not separately observed.

Initial T06a stub behavior had 16 failing parser rows before implementation; the new CalendarPicker acceptance case and changed public API gate also failed before their narrow fixes. A broad app run exposed a real feedback regression: unconditional clearing of the local message removed an unrelated R4 operation message. Clearing is now limited to the date error; its targeted rerun and the subsequent 157-row scope passed. Those preliminary failures are not counted as final PASS.

## Verification boundary

T06 is VERIFIED for its bounded source/native scope: strict quick-date grammar, shared themed calendar composition, and required Goods/Sales receipt headers at measured Light/Dark native100/native125. The first net8 native125 command selected an unbuilt x64 output directory and ran no tests; the corrected command used the existing net8 build and passed 64/64. No SQL owner or cleanup is involved because T06 changes no persistence. Full UIN-A07 remains open for nullable Sales filters in T11. Package Login, attended observations and dated Owner acceptance remain with T13.
