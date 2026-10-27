# T03 reviewed shell captures

[Execution evidence](../20261021-t03-shell-theme.md) · [Reference images](../../../design-references/business-compact/README.md)

These synthetic hosted captures review only shared shell/theme work. The real catalog is still composed by its pre-T04 implementation. Products are the same 18 synthetic records, selected SP001, page1, scroll origin, with Name focused. Reference01 supplies Light composition and reference 03 Graphite; neither image changes business fields or UI language. No Owner acceptance is recorded here.

| Logical client / native scale | Light | Graphite | Physical observation |
|---|---|---|---|
|1200×760 /125%|[Logical](catalog-shell-1200x760-native125-Light.png)|[Logical](catalog-shell-1200x760-native125-Dark.png)|1500×950: [Light](catalog-shell-physical-1200x760-native125-Light.png), [Graphite](catalog-shell-physical-1200x760-native125-Dark.png)|
|944×600 /125%|[Logical](catalog-shell-944x600-native125-Light.png)|[Logical](catalog-shell-944x600-native125-Dark.png)|Independent observer:1180×750 native client; PrintWindow paints944×600 at window-DPI96|
|1680×900 /125%|OPEN|OPEN|OS clamps requested logical client to1540×845; no larger monitor resolution available|
|1680×900 /100%|[Logical](catalog-shell-1680x900-native100-Light.png)|[Logical](catalog-shell-1680x900-native100-Dark.png)|1680×900 native client, monitor/window96 DPI|
|1200×760 /100%|[Logical](catalog-shell-1200x760-native100-Light.png)|[Logical](catalog-shell-1200x760-native100-Dark.png)|1200×760: [Physical Light](catalog-shell-physical-1200x760-native100-Light.png), [Physical Graphite](catalog-shell-physical-1200x760-native100-Dark.png)|
|944×600 /100%|[Logical](catalog-shell-944x600-native100-Light.png)|[Logical](catalog-shell-944x600-native100-Dark.png)|944×600 native client, monitor/window96 DPI|

The logical PNGs are direct PrintWindow output at the target's96-DPI paint units, not downsampled physical images. Physical PNGs are direct guarded CopyFromScreen output at measured monitor120 DPI. Neither capture technique changes Windows Display Scale. Failed/padded development images are excluded.

Review against01/03: brand/module icons and groups, navy selection/navigation, bottom user/theme/sign-out area, reduced standalone chrome and Graphite app-owned surfaces are visible. At944×600 the module list intentionally scrolls, with session actions fixed and reachable. The catalog result scrollbar allows later rows; Products toolbar/inspector hierarchy remains T04. Screenshots do not prove every interaction: the guarded tests separately verify width, identity, focus, keyboard activation and reachability.

Remaining native deviations are explicit: tab captions can truncate and bright native horizontal strips remain around header edges; FlowLayoutPanel/SourceGrid scrollbars stay light at constrained sizes. In the separate real receipt lookup fixture, native DateTimePicker/status ComboBox and lookup accessory squares also stay light. Full chrome parity is not claimed. No private native child replacement or vendor pin change is introduced; integrated disposition remains OPEN/T06.

Source/test hashes, exact measured contexts, failed trials and missing evidence belong to the execution page. Native100 rows above are measured/reviewed actual captures. All three sizes pass in both themes at100%.


[Pending Warehouse / Graphite physical fixture](pending-physical-WarehouseLookup-native125-Dark.png) records the existing receipt control inside the changed shell at125% (1500×950physical client). It is synthetic controlled lookup data with no SQL or writes. The popup is an admitted same-process layer, native raw-text/range/focus assertions run separately, and the image documents remaining light accessories/native chrome. This is not T05 receipt composition evidence.

## Capture integrity

| File | Pixels | SHA-256 |
|---|---|---|
| catalog-shell-1200x760-native100-Dark.png | 1200×760 | 98B5BBD7122402F8D0DBFF5F887B8B60281C38FB1ECAD99CFD466A435DA3688E |
| catalog-shell-1200x760-native100-Light.png | 1200×760 | 605FCDD9943A27DB1A2754BB1B62482567F09F30C3C1FFAB30D3B6F4877CDB39 |
| catalog-shell-1200x760-native125-Dark.png | 1200×760 | 8FB24CA40A945B3B749866EF17DAA4222E646996D871531BA2C2E5A1A25A4E1B |
| catalog-shell-1200x760-native125-Light.png | 1200×760 | 68FA055353B4092955918838C720D04921956D0F37543E0EFC3004DD4AFAB2B5 |
| catalog-shell-1680x900-native100-Dark.png | 1680×900 | A63AFD8B020A74968345544D95C44E390233F4D1B59A9D760D473EBC3586DB36 |
| catalog-shell-1680x900-native100-Light.png | 1680×900 | CF0BA5D1A8D50C9C06768E8949A39659A76A693081EA9EEFCA3986B83D6250CA |
| catalog-shell-944x600-native100-Dark.png | 944×600 | 1183DB6456D6426E0BF7AEA8FD246FE980151E4AB7A96E8E7A07D7FED489507D |
| catalog-shell-944x600-native100-Light.png | 944×600 | 8907855F7BB51BBC4AC47B4A54BFED80F4FD9EE21E817FEB8A727B0D0AEE97F4 |
| catalog-shell-944x600-native125-Dark.png | 944×600 | 26221BA646CC1772108EE439CA8093EDA141EBB3257B70E307D2AE3C2FB46747 |
| catalog-shell-944x600-native125-Light.png | 944×600 | FCC324BAA5FEB88AA95783BDA39287716215925EE94CE893205FD790E85E3137 |
| catalog-shell-physical-1200x760-native100-Dark.png | 1200×760 | 7F8C2318886C78E4DD6C64EEB145FCA6AC2D9BF2E501A7A3349AD3C7D1008F2C |
| catalog-shell-physical-1200x760-native100-Light.png | 1200×760 | 256C1729AFAB8C498B81EC47AE91D45D95DE34144AEBB43C0A512B4191988C7B |
| catalog-shell-physical-1200x760-native125-Dark.png | 1500×950 | DFCA0DE5BAFABD77889B7A0EF5BE800CA7BAC04128845EFEDF0D5EE3ED1AB222 |
| catalog-shell-physical-1200x760-native125-Light.png | 1500×950 | 3053A4411874E6D580B0DD4666B107108CF416A89C2B690254DC588EAAF345F4 |
| pending-physical-WarehouseLookup-native125-Dark.png | 1500×950 | 3D63A4E45A445392E51D57D01146C07897CB26C1C8944C0E0E535B223F04DB6C |
