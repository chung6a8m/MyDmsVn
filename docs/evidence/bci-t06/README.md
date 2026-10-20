# BCI-T06 attended captures

**IN PROGRESS — explicit Owner review pending.** The first image group below uses candidate1 product/package09e92d3 and Desktop testf858ec9. Candidate2 sourcee20b8c7 images are labeled in their separate section. These model-reviewed images show synthetic owned test-fixture data only; no Login/password window, connection string or real SQL allocation identity. See [acceptance](../../BUSINESS_COMPACT_ACCEPTANCE.md#87-bci-t06-frozen-integrated-ui-acceptance--2026-10-20) and [attended worksheet](../../BUSINESS_COMPACT_WALKTHROUGH.md).

PrintWindow captures the owned app client before OS composition, in DPI-unaware client units; PNG dimensions do not establish physical display scaling. A separate bounded observer independently verifies target executable/parent/PID/HWND and native monitor scale. Physical window pixels and monitor/target DPI below are separate measurements. Offscreen cells have unavailable/nonfinite bounds; those are excluded from geometry claims.

| Image | Actual context | Captured UTC | Client units/PNG pixels | Physical window pixels | Monitor/window DPI | SHA256 |
|---|---|---|---|---|---|---|
| [100-Light-Draft-windowed.png](100-Light-Draft-windowed.png) | Light Draft, horizontal scroll right | 2026-10-20T20:14:45.0779748Z | 1184x721 | 1200x760 | 96/96 | `923FBE1E4BE092D381BF50BEDF1B7B27FA0FC5AB1E77CF8E86507721275527C0` |
| [100-Light-Invalid.png](100-Light-Invalid.png) | Light invalid raw Quantity8.00001 after Save | 2026-10-20T20:19:37.8976925Z | 1184x721 | 1200x760 | 96/96 | `6710F76CC89620D8716F62BD96A8A89D068B5681776465BDE3DAB2FFAD4C0249` |
| [100-Dark-Invalid.png](100-Dark-Invalid.png) | Dark invalid raw Quantity8.00001 after Save | 2026-10-20T20:24:36.1759124Z | 1184x721 | 1200x760 | 96/96 | `84CC139E4A202F1358D92C0EEA6FE3598145A02BF98603425544297074A72EF3` |
| [100-Dark-Draft-windowed.png](100-Dark-Draft-windowed.png) | Dark restored Draft after scroll/temp-row removal and Save | 2026-10-20T20:29:01.3821278Z | 1184x721 | 1200x760 | 96/96 | `E99A13DD8850E13E515D4F594D3479EDF5C879C0DAC09BD2EF5240319F8ABB32` |
| [100-Dark-Recovery-open-tab-issue.png](100-Dark-Recovery-open-tab-issue.png) | Dark maximized unknown Post outcome; Editor/Save/Post locked; Owner reports Refresh Tab issue OPEN | 2026-10-20T20:36:59.1504041Z | 1920x1009 | 1936x1048 | 96/96 | `34A25172C85D15F0282FD177BD6870D029A1294E46A376635A24219623B72F6B` |

## Automated Refresh focus diagnostic — native100, 2026-10-21

These four model-reviewed full-window PrintWindow captures are separate from the attended session above. Same frozen product/package09e92d3; temporary test-only capture driver snapshot is retained locally. Native SendInput Tab put UIA focus on Refresh; native Enter reloaded Draft, followed by independently checked explicit Post. Both Light/Dark cases passed2/2. Capture is1200×760 window units at native100, not a125 measurement. The selected grid-cell borders remain visible in both focused and unfocused images. Dark's inset focus color equals its primary button fill (`#6EA8FE`), explaining the missing visible cue despite functional keyboard focus. This diagnostic is not Owner sign-off; at this diagnostic checkpoint a product correction and new candidate/gates were pending. The corrected candidate2 and its attended follow-up are recorded below.

| Image | Context | SHA256 |
|---|---|---|
| [100-Light-Refresh-focused-diagnostic.png](100-Light-Refresh-focused-diagnostic.png) | Light; focused; unknown Post outcome | `D0FA4E42F49EE15C00976519619309450D92FAB3F680F57E8BC40F4B3619F44B` |
| [100-Light-Refresh-unfocused-diagnostic.png](100-Light-Refresh-unfocused-diagnostic.png) | Light; unfocused; unknown Post outcome | `06F091A61063560B9376A66D7EDC8ABBAB72B29106F6151B11E7632A8F7ED5C5` |
| [100-Dark-Refresh-focused-diagnostic.png](100-Dark-Refresh-focused-diagnostic.png) | Dark; focused; unknown Post outcome | `7667B077D65D71C345070EB5562136DE9D80C378C01AFEF5FEF06F4E68B63321` |
| [100-Dark-Refresh-unfocused-diagnostic.png](100-Dark-Refresh-unfocused-diagnostic.png) | Dark; unfocused; unknown Post outcome | `C582CFD135389A9004991326C7754C944E781DC8F7D8896C3735B66612E0F55D` |

## Candidate2 attended recovery and Posted — native100, 2026-10-21

Frozen package `0.1.0-bci-t06.2`, product/test source `e20b8c7f43dc73a9613d7f04d0d52158809663cf`. Owner confirmed actual Login and saved/reopened/edited Draft3.125@4. Controlled SQL WAITFOR was observed. Owner explicitly confirmed Dark Refresh's visible focus after Status→Tab while mutations stayed locked, then Enter cleared the unknown-outcome message and restored Draft/Post. Independent readonly SQL verified unchanged Draft/no stock before and after explicit reload. One explicit Post retry succeeded; SQL verified exactly one movement/balance3.125. Owner explicitly confirmed Posted, F2 Quantity readonly and disabled Save/Post/Add/Remove in both themes. After these captures, Owner closed normally by X; app exit/drain and positive SQL cleanup passed. All118 resource ledgers were Dropped at that checkpoint (6 historicalT04 +112 T06).

These reviewed client captures record Recovery locks and Posted state. They were taken after returning to chat; they do not establish an active-window focus cue. The foreground cue confirmation is the Owner's direct observation, supported separately by the rendered contrast and native-keyboard package tests. Native monitor100/window96; client/physical window dimensions below remain separate observations. No125 or overall BCI acceptance is inferred.

| Image | Context | Captured UTC | Client units / physical window pixels | SHA256 |
|---|---|---|---|---|
| [100-Light-Recovery-candidate2.png](100-Light-Recovery-candidate2.png) | Light Recovery | 2026-10-20T21:21:37.8236043Z | 1184x721 / 1200x760 | `75E3057C9E750C85D6046056314BF4AC086AD90882241BF2D6C81BF21C818FEB` |
| [100-Dark-Recovery-candidate2.png](100-Dark-Recovery-candidate2.png) | Dark Recovery | 2026-10-20T21:23:56.6506475Z | 1184x721 / 1200x760 | `A07D4B0EE4E622D4959B0255E4C1AE2BBEF1A2BD12CE595A2FA5AACD55177C0C` |
| [100-Dark-Posted-candidate2.png](100-Dark-Posted-candidate2.png) | Dark Posted | 2026-10-20T21:27:25.6858946Z | 1184x721 / 1200x760 | `B504FC94CABCE0B0359A5AF740D31A5E1715076EA1A074EBE8EEE30FC47DE07D` |
| [100-Light-Posted-candidate2.png](100-Light-Posted-candidate2.png) | Light Posted | 2026-10-20T21:29:03.5486741Z | 1184x721 / 1200x760 | `7B47AD0A01B390173A34D80E9692A8D664CFB58030124B71170EA56BCA53F33E` |

The bounded UIA metadata is retained locally without publishing unavailable offscreen bounds. Candidate1 Invalid captures in each theme expose `Quantity row 1 editor` as native Edit, enabled/focusable, ValueReadonlyFalse; Quantity cell is writable and Total is readonly. Candidate2 Posted captures expose Product/Quantity/Unit cost/Total row1 as ValueReadonlyTrue, matching the Owner's F2 readonly observations. These are observed semantics of those actual sessions, not125 evidence or a replacement for the full native UIA test gate.
