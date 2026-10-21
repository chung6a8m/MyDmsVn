# BCI-T06 attended captures

**COMPLETE — explicit overall Owner review accepted 2026-10-21.** See the [final signed scope](../../BUSINESS_COMPACT_ACCEPTANCE.md#final-t06-acceptance-and-handoff--2026-10-21). The first image group below uses candidate1 product/package09e92d3 and Desktop testf858ec9. Candidate2 sourcee20b8c7 images are labeled in their separate section. These model-reviewed images show synthetic owned test-fixture data only; no Login/password window, connection string or real SQL allocation identity. See [acceptance](../../BUSINESS_COMPACT_ACCEPTANCE.md#87-bci-t06-frozen-integrated-ui-acceptance--2026-10-20) and [attended worksheet](../../BUSINESS_COMPACT_WALKTHROUGH.md).

PrintWindow captures the owned app client before OS composition. At100 the canvas and unscaled content share client units. At125 the observer-reported larger canvas contains unscaled app content with black padding; these original captures are not cropped or rescaled, and PNG dimensions do not establish physical display scaling. A separate bounded observer independently verifies target executable/parent/PID/HWND and native monitor scale. Physical window pixels and monitor/target DPI below are separate measurements. Offscreen cells have unavailable/nonfinite bounds; those are excluded from geometry claims.

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

## Candidate2 attended125 attempt1 — TIMED OUT, 2026-10-21

Same frozen product/teste20b8c7. Owner confirmed actual Login and saved/reopened/edited Draft3.125@4; readonly SQL verified saved Draft/no stock. Light and Dark editor/layout/numeric observations and SQL recovery/Post checks were completed before the deadline, but the owned app reached its30-minute timeout before normal exit was recorded. This attempt is **TIMED OUT / cleanup verified**, never a full PASS. Its completed observations remain OBSERVED only; at that checkpoint the required attended125 matrix was assigned to two fresh30-minute replay groups. The completed A+B2 replays are recorded below. No final acceptance is inferred.

The125 PrintWindow boundary differs from a physical desktop screenshot: the reported canvas1480×901 contains the app's unscaled content at top left with black padding. Original pixels are preserved. Separate measurements give native monitor120DPI/125%, target HWND96 and physical window1500×950. Therefore this image records app state/content, while physical visibility/scaling depends on native measurements and direct Owner observations; no black padding is attributed to the app's on-screen layout.

| Image | Context | Captured UTC | Reported canvas / physical window pixels | SHA256 |
|---|---|---|---|---|
| [125-Light-Draft-candidate2.png](125-Light-Draft-candidate2.png) | Light saved Draft3.125@4 | 2026-10-20T22:38:51.2690022Z | 1480x901 / 1500x950 | `0D51A23311B99C970BE7D44F96D3CB4D3E4BDFED1ECF5AE106A0F7E03D9EC13C` |

Additional reviewed attempt1 captures:

| Image | Context | Captured UTC | Reported canvas / physical window pixels | SHA256 |
|---|---|---|---|---|
| [125-Light-Invalid-candidate2-attempt1.png](125-Light-Invalid-candidate2-attempt1.png) | Light Invalid, attempt1 OBSERVED only | 2026-10-20T22:46:23.5562028Z | 1920x991 / 1940x1040 | `2C898274A6A49F7E73FC5B2B8CDD9ABD92DE607273D912FD7A48658AC7747823` |
| [125-Dark-Invalid-candidate2-attempt1.png](125-Dark-Invalid-candidate2-attempt1.png) | Dark Invalid, attempt1 OBSERVED only | 2026-10-20T22:48:05.1495933Z | 1920x991 / 1940x1040 | `A7FBDDB67C8D8269607CC536F57AE7EF1464B438E19F66E590C360B46DFAF590` |
| [125-Dark-Draft-candidate2-attempt1.png](125-Dark-Draft-candidate2-attempt1.png) | Dark Draft, attempt1 OBSERVED only | 2026-10-20T22:54:00.8991163Z | 1920x991 / 1940x1040 | `37C7973C79A6C4D2991773B45DD90850D2DA04CD3BC48166D11BE98EE05D6A29` |
| [125-Dark-Recovery-candidate2-attempt1.png](125-Dark-Recovery-candidate2-attempt1.png) | Dark Recovery, attempt1 OBSERVED only | 2026-10-20T22:57:50.0115920Z | 1920x991 / 1940x1040 | `C59497867A293236DBA0368534AE0CEAC084DD5BBDDA2D320EA752E9CEF67A53` |
| [125-Dark-Posted-candidate2-attempt1.png](125-Dark-Posted-candidate2-attempt1.png) | Dark Posted, attempt1 OBSERVED only | 2026-10-20T23:04:14.4937599Z | 1920x991 / 1940x1040 | `CCB356F1CA58A4853F73C0F7A161F4D7DE28B1AC17C632F7EC8467F938C720F9` |

The attempt1 Recovery capture was originally requested with a Light filename but visually showed Dark; the original pixels/metadata were retained under an actual-Dark name and only credited as Dark. No Light capture or foreground focus is inferred from that image. The Posted image records Dark. At that checkpoint a fresh recovery/Posted group still had to confirm/capture both themes; completed B2 below supplies those new observations.

## Candidate2 attended125 replay group A — completed editor/layout scope

Fresh owned30-minute editor/layout session, same frozene20b8c7 bytes, native125/monitor120/HWND96. These reviewed captures belong to replay group A, not the timed-out attempt. Bounded UIA actually exposes the active Quantity editor as Edit/ValueReadonlyFalse and computed Total row1 as ValueReadonlyTrue in both images. The125 unscaled PrintWindow/black-padding boundary above still applies. Owner completed the requested both-theme checks, including Dark Down; readonly SQL verified saved Draft/no stock. The owned app exited normally and process/SQL drain plus positive cleanup passed. At that checkpoint groupB and final review were separate pending obligations; B2 below completes recovery/Posted, with explicit final Owner review later accepted in the linked final signed scope.

| Image | Context | Captured UTC | Reported canvas / physical window pixels | SHA256 |
|---|---|---|---|---|
| [125-Light-Invalid-candidate2-A.png](125-Light-Invalid-candidate2-A.png) | GroupA Light raw8.00001/error | 2026-10-20T23:16:40.5234266Z | 1480x901 / 1500x950 | `B3D89BD44665D11D57F1A05DB7170DCCFE73998BA2749E799F765ABA40BCAC5F` |
| [125-Dark-Invalid-candidate2-A.png](125-Dark-Invalid-candidate2-A.png) | GroupA Dark raw8.00001/error | 2026-10-20T23:17:43.3060988Z | 1480x901 / 1500x950 | `0A00D641D32ED9826411EF17651EF2ADA7F8D696A1745C0570E15D8DAE89C3BD` |

GroupA final saved Draft, after both-theme editor/layout checks and temporary-row removal; independent readonly SQL verified oneGUI-PROD3.125@4/no stock. Owner subsequently closed by X without an unsaved-data warning; normal app exit/drain and positive SQL cleanup passed.

| Image | Captured UTC | Reported canvas / physical window pixels | SHA256 |
|---|---|---|---|
| [125-Dark-Draft-candidate2-A.png](125-Dark-Draft-candidate2-A.png) | 2026-10-20T23:20:14.5995728Z | 1920x991 / 1940x1040 | `969AC94B57FA2353187BFED5B969662E082AA56FA022227D940D9CA9F69D13DA` |
## Candidate2 attended125 replay B2 — completed recovery/Posted scope

Fresh owned30-minute session on unchanged sourcee20b8c7/candidate2, native125/monitor120/HWND96. Owner completed actual Login/Save/New/reopen/edit and explicitly confirmed uncertain-Post locks plus Status→Tab visible Refresh focus in each theme. Runtime WAITFOR was observed; readonly SQL verified unchanged Draft/no stock before fault removal and after explicit Enter reload. Owner confirmed Draft/Post restored, then performed one explicit retry. SQL verified exactly one movement/balance3.125. Owner confirmed Posted/F2 Quantity readonly/four mutation locks in Dark, then completed the same requested check in Light. Bounded UIA exposes Product/Quantity/Unit cost/Total as ValueReadonlyTrue in both Posted captures.

Each reviewed original image below comes from a distinct successful v2 attempt; no stale artifact or failed B1 capture is substituted. The125 PrintWindow black-padding boundary still applies; visible focus is the Owner's foreground observation, not inferred from a background capture. Owner closed normally by X; app exit0/drain and positive SQL cleanup passed. All181 actual resource ledgers were Dropped and no owned app remained. B2 has no failed capture marker. Group A plus B2 complete the attended125 observations; explicit overall Owner review was accepted on 2026-10-21. Failed earlier trials retain their original outcomes.

| Image | Actual context | Captured UTC | Reported canvas / physical window pixels | SHA256 |
|---|---|---|---|---|
| [125-Light-Recovery-candidate2-B2.png](125-Light-Recovery-candidate2-B2.png) | Light Recovery | 2026-10-20T23:51:21.1005272Z | 1480x901 / 1500x950 | `DAFE18D3963C56C6BC35FCD778C87ABBFB20F509256990698956F7419BF7A1A4` |
| [125-Dark-Recovery-candidate2-B2.png](125-Dark-Recovery-candidate2-B2.png) | Dark Recovery | 2026-10-20T23:53:22.8637356Z | 1480x901 / 1500x950 | `54A274A52D184C23345EF45620FDD26E9E67DD3ED03D6DEBB6E289589163A8E6` |
| [125-Dark-Posted-candidate2-B2.png](125-Dark-Posted-candidate2-B2.png) | Dark Posted | 2026-10-20T23:55:45.6727105Z | 1480x901 / 1500x950 | `7B147AF1D17D664C631CB3BB7377EE58CD39B3C03CD47A0EA5929EF2DB5373CE` |
| [125-Light-Posted-candidate2-B2.png](125-Light-Posted-candidate2-B2.png) | Light Posted | 2026-10-20T23:56:34.7256687Z | 1480x901 / 1500x950 | `5834136A7FE7A996970CA006091F752B0AA92FD68094C31B3526933F5425BFDE` |
