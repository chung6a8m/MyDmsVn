# T03 follow-up capture review

[Execution/provenance](../20261021-t03-chrome-followup.md) · [Original checkpoint](../20261021-t03-shell-captures/README.md) · [Unchanged references](../../../design-references/business-compact/README.md)

Source `064760f`, BootstrapSourceGrid `2bd7874`, Bootstrap5WinFormUI `2be9a4d`, SourceGrid `f4e457b`. These are hosted synthetic fixtures, not shipped-package/actual Login evidence. The previous capture set remains unchanged.

Catalog comparisons use the same 18 products, selected SP001, page1, scroll origin and Name focus. Review shared shell against references 01/03: full Goods Receipts caption and theme-colored tab bands replace the original truncation/bright strips; navy navigation, module groups, current user and compact session actions remain. Products content/inspector composition is still pre-T04, so density/toolbar/inspector parity is not granted.

| Logical client / scale | Light | Graphite | Capture metadata |
|---|---|---|---|
|1680×900 /100%|[Client](catalog-shell-1680x900-native100-Light.png)|[Client](catalog-shell-1680x900-native100-Dark.png)|PrintWindow 1680×900; measured monitor/window 96 DPI; physical native client 1680×900|
|1200×760 /100%|[Physical](catalog-shell-physical-1200x760-native100-Light.png)|[Physical](catalog-shell-physical-1200x760-native100-Dark.png)|Direct CopyFromScreen 1200×760; measured monitor/window 96 DPI; no foreign overlays|
|944×600 /100%|[Client](catalog-shell-944x600-native100-Light.png)|[Client](catalog-shell-944x600-native100-Dark.png)|PrintWindow 944×600; measured monitor/window 96 DPI; essential session actions fixed, navigation/results scroll intentionally|
|1200×760 /125%|[Physical](catalog-shell-physical-1200x760-native125-Light.png)|[Physical](catalog-shell-physical-1200x760-native125-Dark.png)|Direct CopyFromScreen 1500×950; monitor 120 DPI/window96; no foreign overlays|
|944×600 /125%|[Client](catalog-shell-944x600-native125-Light.png)|[Client](catalog-shell-944x600-native125-Dark.png)|PrintWindow 944×600 at window96; independently measured physical native client 1180×750/monitor 120 DPI|
|1680×900 /125%|OPEN|OPEN|Available display cannot realize this logical client; no resized/substituted image|

[Pending Warehouse / physical100](pending-physical-WarehouseLookup-native100-Dark.png) shows the active popup, visible themed chevrons on all three lookups and the retained native date/status/scrollbar limits. Data are controlled synthetic references; no Save/Post or SQL is involved. Separate native text/range/HWND/focus/model assertions establish input continuity. This is not T05 receipt-composition acceptance.

[Keyboard overflow/focus /100](overflow-tabs-native100-Dark.png) shows the selected tab after twelve-tab native navigation and a theme switch; the complete keyboard focus cue survives. The native overflow arrows remain platform chrome. [Unfocused tab bands/caption /100](tab-edges-native100-Dark.png) complements the focused case: both native top rows and nonselected bottom band samples are dark, and the full caption has independent rendered-glyph coverage.

The independent observer verifies target PID/HWND, actual monitor DPI, native client bounds and window-DPI96 paint units. Logical images are direct PrintWindow output with no stretching/physical-buffer padding. Physical images require the full client within the real work area and reject foreign-process occlusion; the receipt popup is an admitted same-process layer. Screenshots never change Windows scale or replace per-key assertions. Native125 images above are actual measured/reviewed captures; no wide125 image is supplied.


[Pending Warehouse / physical125](pending-physical-WarehouseLookup-native125-Dark.png), [keyboard overflow/focus125](overflow-tabs-native125-Dark.png) and [unfocused bands/caption125](tab-edges-native125-Dark.png) repeat the technical fixture observations at the actual 125% monitor. They preserve the distinction between source 064760f runtime and the dfd29d2 test-only raster oracle; all three runtime DLL hashes are unchanged. Contrast-based cue assertions tolerate measured raster blending while retaining >=3:1 and the expected focus hue.

## Capture integrity

| File | Pixels | SHA-256 |
|---|---|---|
| catalog-shell-1680x900-native100-Dark.png | 1680×900 | DD3B3B61953FC24B7E20E99CC1BD9C2A18E88D37E99F46965C1866CDAC783B80 |
| catalog-shell-1680x900-native100-Light.png | 1680×900 | 10E1C08531847298B8E565A32398535DE64C571D3BBBC5DADDCF1EE6D9766DC9 |
| catalog-shell-944x600-native100-Dark.png | 944×600 | D9D7EE9334054530E195AB14810BA27B449758176752430C0FFC1BD51188509F |
| catalog-shell-944x600-native100-Light.png | 944×600 | 79E61B8D77534F823D78832879A72FD41855848ED96FC14CAE1AF353A1374F33 |
| catalog-shell-944x600-native125-Dark.png | 944×600 | 1B8E25CC1CA9E94F7836C943805864631DDFA6DC3CE911E4A115EAEDE85ABE06 |
| catalog-shell-944x600-native125-Light.png | 944×600 | 0D683C1E7167B4BBED14C06E261D29D864CED304C54E56D77463DC5F29333D12 |
| catalog-shell-physical-1200x760-native100-Dark.png | 1200×760 | DFF5F3EFCA9C1D949D53385301B32750A9CEFB920B52927A2EAC06513BAC78D1 |
| catalog-shell-physical-1200x760-native100-Light.png | 1200×760 | F7D671BE8AED6902AC2236605CC86164DE4C8A0DA81FB06AB7AAE6D3764ADD3C |
| catalog-shell-physical-1200x760-native125-Dark.png | 1500×950 | 906BA613E16DA4168C83B06694A104FC788ABC6E888501944C31F31B783E909C |
| catalog-shell-physical-1200x760-native125-Light.png | 1500×950 | 4DC3567A84D3B3BFFB96046AE13B84B097A14E3794B3C6CEEE92E73277BA9B57 |
| overflow-tabs-native100-Dark.png | 944×600 | B8BFB410EAE9B10C2C7D7255BE9846DE499E181A122365C2D0FA307FD03E2538 |
| overflow-tabs-native125-Dark.png | 944×600 | 998B02F386B5C4E3ABD50613558AC556506B418FC6C61EE4ED663D3CBFCCF1CA |
| pending-physical-WarehouseLookup-native100-Dark.png | 1200×760 | D168B9947B36C3EB6A9FC649FE02F363EBA74C5F40A7A64D1376BD7694EE0228 |
| pending-physical-WarehouseLookup-native125-Dark.png | 1500×950 | 31751425CA28C8A67809E49BBE997D80357664A6D054A1145737EF02BF227161 |
| tab-edges-native100-Dark.png | 1200×760 | 56AAB4F452F33CC6F8F4A14440E4FB302FBF6E952DAB143D0B79EDB7D643C4DF |
| tab-edges-native125-Dark.png | 1200×760 | DB52F1032522B9608F1CFB336044BCDBE70BAEDD5EEBA4521E6298DFDA44860F |
