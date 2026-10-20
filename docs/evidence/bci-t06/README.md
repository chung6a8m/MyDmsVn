# BCI-T06 attended captures

**IN PROGRESS — explicit Owner review pending.** Product/package09e92d3, Desktop testf858ec9. These model-reviewed images show synthetic owned test-fixture data only; no Login/password window, connection string or real SQL allocation identity. See [acceptance](../../BUSINESS_COMPACT_ACCEPTANCE.md#87-bci-t06-frozen-integrated-ui-acceptance--2026-10-20) and [attended worksheet](../../BUSINESS_COMPACT_WALKTHROUGH.md).

PrintWindow captures the owned app client before OS composition, in DPI-unaware client units; PNG dimensions do not establish physical display scaling. A separate bounded observer independently verifies target executable/parent/PID/HWND and native monitor scale. Physical window pixels and monitor/target DPI below are separate measurements. Offscreen cells have unavailable/nonfinite bounds; those are excluded from geometry claims.

| Image | Actual context | Captured UTC | Client units/PNG pixels | Physical window pixels | Monitor/window DPI | SHA256 |
|---|---|---|---|---|---|---|
| [100-Light-Draft-windowed.png](100-Light-Draft-windowed.png) | Light Draft, horizontal scroll right | 2026-10-20T20:14:45.0779748Z | 1184x721 | 1200x760 | 96/96 | `923FBE1E4BE092D381BF50BEDF1B7B27FA0FC5AB1E77CF8E86507721275527C0` |
| [100-Light-Invalid.png](100-Light-Invalid.png) | Light invalid raw Quantity8.00001 after Save | 2026-10-20T20:19:37.8976925Z | 1184x721 | 1200x760 | 96/96 | `6710F76CC89620D8716F62BD96A8A89D068B5681776465BDE3DAB2FFAD4C0249` |
| [100-Dark-Invalid.png](100-Dark-Invalid.png) | Dark invalid raw Quantity8.00001 after Save | 2026-10-20T20:24:36.1759124Z | 1184x721 | 1200x760 | 96/96 | `84CC139E4A202F1358D92C0EEA6FE3598145A02BF98603425544297074A72EF3` |
| [100-Dark-Draft-windowed.png](100-Dark-Draft-windowed.png) | Dark restored Draft after scroll/temp-row removal and Save | 2026-10-20T20:29:01.3821278Z | 1184x721 | 1200x760 | 96/96 | `E99A13DD8850E13E515D4F594D3479EDF5C879C0DAC09BD2EF5240319F8ABB32` |
| [100-Dark-Recovery-open-tab-issue.png](100-Dark-Recovery-open-tab-issue.png) | Dark maximized unknown Post outcome; Editor/Save/Post locked; Owner reports Refresh Tab issue OPEN | 2026-10-20T20:36:59.1504041Z | 1920x1009 | 1936x1048 | 96/96 | `34A25172C85D15F0282FD177BD6870D029A1294E46A376635A24219623B72F6B` |

## Automated Refresh focus diagnostic — native100, 2026-10-21

These four model-reviewed full-window PrintWindow captures are separate from the attended session above. Same frozen product/package09e92d3; temporary test-only capture driver snapshot is retained locally. Native SendInput Tab put UIA focus on Refresh; native Enter reloaded Draft, followed by independently checked explicit Post. Both Light/Dark cases passed2/2. Capture is1200×760 window units at native100, not a125 measurement. The selected grid-cell borders remain visible in both focused and unfocused images. Dark's inset focus color equals its primary button fill (`#6EA8FE`), explaining the missing visible cue despite functional keyboard focus. This diagnostic is not Owner sign-off; a product correction and new candidate/gates are pending.

| Image | Context | SHA256 |
|---|---|---|
| [100-Light-Refresh-focused-diagnostic.png](100-Light-Refresh-focused-diagnostic.png) | Light; focused; unknown Post outcome | `D0FA4E42F49EE15C00976519619309450D92FAB3F680F57E8BC40F4B3619F44B` |
| [100-Light-Refresh-unfocused-diagnostic.png](100-Light-Refresh-unfocused-diagnostic.png) | Light; unfocused; unknown Post outcome | `06F091A61063560B9376A66D7EDC8ABBAB72B29106F6151B11E7632A8F7ED5C5` |
| [100-Dark-Refresh-focused-diagnostic.png](100-Dark-Refresh-focused-diagnostic.png) | Dark; focused; unknown Post outcome | `7667B077D65D71C345070EB5562136DE9D80C378C01AFEF5FEF06F4E68B63321` |
| [100-Dark-Refresh-unfocused-diagnostic.png](100-Dark-Refresh-unfocused-diagnostic.png) | Dark; unfocused; unknown Post outcome | `C582CFD135389A9004991326C7754C944E781DC8F7D8896C3735B66612E0F55D` |
