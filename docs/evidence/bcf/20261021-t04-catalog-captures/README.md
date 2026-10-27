# BCF-T04 — matched Products captures

[Execution and limitations](../20261021-t04-products-inspector.md) · [Reference 01 Light](../../../design-references/business-compact/01-catalog-light.png) · [Reference 03 Graphite](../../../design-references/business-compact/03-catalog-graphite.png)

Source/test `29cd2a1077c8a385c570f4bd3a51b72a88d38357`, net48/x86 hosted WinForms. These are actual synthetic catalog controls in the real shell, not a shipped package, real Login or attended catalog session. The independent PerMonitorV2 observer measures actual monitor and target window DPI. All captures are reviewed before copying here; PNG bytes are copied without resizing or image edits. The integrity table below records the stored pixels.

For each wide/1200 pair: the same 30 synthetic products, page1 of2, total30, empty search, selected/inspected SP001, focused native Name editor and zero grid scroll. A second product has a deliberately long Unicode name; inactive products remain textual Inactive rows. The lower rows of the 25-row page are reached by the native grid scrollbar. Counts and page size come from the query/ViewModel, not the reference raster's128/18 sample.

Files containing `physical` use an owned/unoccluded foreground screen client capture with zero foreign overlays. Other files use PrintWindow sized in the target's actual paint-DPI client units; at native125 those logical pixels do not themselves prove physical125 rendering. The matching physical1200 images and independent DPI reports provide that separate evidence. Platform scrollbar bright chrome remains visible in Graphite and is an explicit limitation, not an accepted palette exemption.

The 944 captures deliberately switch to the opposite theme after retaining an unsaved Unicode Name edit and native selection4+2. They use the same editor HWND, focus and selected ID. Results are above the inspector; the inspector is scrolled down to show the final Activate/Deactivate group fully. The heading is therefore outside that constrained scroll position. Tests separately scroll every shared-catalog field/action fully into view. Names containing `Light-opposite-theme` show **Dark**; names containing `Dark-opposite-theme` show **Light**.

No 1680×900 logical capture at native125 is fabricated or credited. The current display cannot fit that target. The four strict wide shell/Products test rows remain unselected/NOT TESTED at that scale; the two older T03 wide-capture obligations remain OPEN. These images support the focused Products comparison; full reference disposition and frozen integrated Owner acceptance remain T06.

## Geometry

| Logical shell client | Catalog client | Toolbar | Results grid | Inspector scroll owner | Layout |
|---|---|---|---|---|---|
| 1680×900 | 1448×836 | 1448×76 | 1104×706 | 340×760 | Side by side |
| 1200×760 | 968×696 | 968×76 | 624×566 | 340×620 | Side by side |
| 944×600 | 712×536 | Reflowed title/search rows | 712×113 above footer | 695×251 visible scroll client | Top/bottom; inspector content433 high |

All geometry above is logical/window-DPI96. Error region is hidden in the matched selected-state captures; separate loading/error/validation cases verify its messages and command locks. No screenshot alone proves those commands or cancellation. Final TRX carries actual foreground positions and bounds; there is no screenshot-similarity score.

## Integrity register

| File | Stored pixels | Method | SHA-256 |
|---|---|---|---|
| [native100/products-1200-Dark.png](native100/products-1200-Dark.png) | 1200×760 | PrintWindow/window-DPI | `1E424F986CB45215CFAFDA5C0E64D8C247F280AD41D5863FF8BFB6557034521E` |
| [native100/products-1200-Light.png](native100/products-1200-Light.png) | 1200×760 | PrintWindow/window-DPI | `3D4DB1F68AA194E0A89EC1E3FB1A2A4535EB0B68684AAD3266459F73798F776B` |
| [native100/products-1680-Dark.png](native100/products-1680-Dark.png) | 1680×900 | PrintWindow/window-DPI | `773D095779B0A00CDD16107ADCB29563ED2D6A7BA9B272A86697D757E8712BD5` |
| [native100/products-1680-Light.png](native100/products-1680-Light.png) | 1680×900 | PrintWindow/window-DPI | `5BD152C90E42A0908BD5B532810F82062197648248B56837D678DFDA2CE0A9FF` |
| [native100/products-944-Dark-opposite-theme.png](native100/products-944-Dark-opposite-theme.png) | 944×600 | PrintWindow/window-DPI | `1CAD37DEFB13321FDCAC07F8E3FF8504449ED160F1BA86C0FADD1FE04D10F9E0` |
| [native100/products-944-Light-opposite-theme.png](native100/products-944-Light-opposite-theme.png) | 944×600 | PrintWindow/window-DPI | `CA3F02B68601C05385FB19E25693163826CD8FA0314364E446BC22570A2436AA` |
| [native100/products-physical-1200-Dark.png](native100/products-physical-1200-Dark.png) | 1200×760 | Foreground client | `52BB30C638B756055B0BC05339255A202E5E77E2C5C0D25278464481767260BC` |
| [native100/products-physical-1200-Light.png](native100/products-physical-1200-Light.png) | 1200×760 | Foreground client | `7BD9E18291E865A0D434685FAE00002C9EF395F5C702FEFFE0F63633B2043315` |
| [native125/products-1200-Dark.png](native125/products-1200-Dark.png) | 1200×760 | PrintWindow/window-DPI | `ED4C0FE4800663AF3D7F6CBA0A2A94DBF77F067BC333CFD5B412FE740EAA8834` |
| [native125/products-1200-Light.png](native125/products-1200-Light.png) | 1200×760 | PrintWindow/window-DPI | `69C00434B43A0C3006FC99012A8EA17B6A96FCE54C9957E9D37E5E6987A81DD7` |
| [native125/products-944-Dark-opposite-theme.png](native125/products-944-Dark-opposite-theme.png) | 944×600 | PrintWindow/window-DPI | `ACE04B16BD5B971112FF4145E72B70D6930B6AD684FCFA32E06C7EE6E505773A` |
| [native125/products-944-Light-opposite-theme.png](native125/products-944-Light-opposite-theme.png) | 944×600 | PrintWindow/window-DPI | `89DCE9A17509614286ACC39163B89D9BC098391A60EEC87E20939A96AB2E59BF` |
| [native125/products-physical-1200-Dark.png](native125/products-physical-1200-Dark.png) | 1500×950 | Foreground client | `3810DE9FF26D5DD0172C4E71303A4ED799FD3728AE3C9918C7C84EB9ABED7F0D` |
| [native125/products-physical-1200-Light.png](native125/products-physical-1200-Light.png) | 1500×950 | Foreground client | `A3E579205D2BA30BB4F50F171B07B2CCA724F5E839674F7383799D597B9DA8BF` |
