# BCF-T06 — comparison index for Owner review

**Status: preparation in progress; no Owner acceptance.** [Integrated evidence](../20261021-t06-integrated-acceptance.md) · [Acceptance ledger](../../../BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) · [Original reference register](../../../design-references/business-compact/README.md)

These candidate images are **hosted production-control captures with synthetic clients**, not photographs of packaged Login or SQL-backed data. Actual packaged Login, receipt/catalog workflows and current-candidate attended lookup observations are recorded separately in the integrated evidence. PrintWindow captures contain the measured client area; physical1200 captures use the owned, unoccluded screen client. No image is resized, retouched or synthesized to fill a missing viewport. Original design files remain unchanged.

## Matched fixtures and provenance

Products uses30 synthetic records across two pages, SP001 selected on page1, empty search, Name focus and zero grid scroll. Normal captures show the same30 records/selected identity in both themes; the result footer reports25 records on the current page. The944 captures retain an unsaved Unicode Name edit/range4+2, switch to the opposite theme and scroll the inspector to the final commands. Their filename theme is the **initial** mode: `Light-opposite-theme` is actually Graphite, and `Dark-opposite-theme` is actually Light.

Receipt normal captures use the same saved synthetic receipt41, date2026-10-21, Warehouse3/Employee5, opaque Version,18 lines and total13162.50 in both themes. The list is a separate presentation of the same retained control/session. Constrained dirty/invalid/recovery captures retain18 lines; successful authoritative Refresh gives Posted with one line/total10.00, a deliberately different state. Constrained filenames likewise name the initial mode; the actual theme is its opposite. Selection/scroll and geometry come from the corresponding TRX observations, rather than the image alone.

Native100 measures monitor96/window96 DPI. Native125 requires independently measured monitor120/window96 DPI; this process remains DPI-unaware, so physical screen pixels and logical paint units are distinct. Native125 evidence is PENDING. The strict1680×900 logical viewport is unavailable at125 on this display; the previously observed fitted1540×845 size is not credited as1680×900. Six strict wide rows remain NOT TESTED pending equipment or explicit bounded Owner exception.

## Reference01 — Products / Light

| Original design | Candidate native100,1680×900 client |
|---|---|
| [![Reference01](../../../design-references/business-compact/01-catalog-light.png)](../../../design-references/business-compact/01-catalog-light.png) | [![Products Light](native100/products-1680-Light.png)](native100/products-1680-Light.png) |

## Reference03 — Products / Graphite

| Original design | Candidate native100,1680×900 client |
|---|---|
| [![Reference03](../../../design-references/business-compact/03-catalog-graphite.png)](../../../design-references/business-compact/03-catalog-graphite.png) | [![Products Graphite](native100/products-1680-Dark.png)](native100/products-1680-Dark.png) |

## Reference02 — Goods Receipt

The final receipt fixture selects the Goods Receipts navigation item as the actual host does. Early captures with no selected navigation remain ignored trial output. These final frames use sourceec4a382, with provenance read after native capture settles layout/scroll.

| Original design | Candidate Light, native1001680×900 | Candidate Graphite, same fixture |
|---|---|---|
| [![Reference02](../../../design-references/business-compact/02-goods-receipt.png)](../../../design-references/business-compact/02-goods-receipt.png) | [![Receipt Light](native100/receipt-1680-Light-saved.png)](native100/receipt-1680-Light-saved.png) | [![Receipt Graphite](native100/receipt-1680-Dark-saved.png)](native100/receipt-1680-Dark-saved.png) |

The Light receipt uses reference02 for layout. Its Graphite counterpart also uses reference03 for palette direction; the Owner supplied no separate Dark receipt raster.

## Material differences requiring disposition

These differences are visible, not automatically waived by a native-control explanation or by passing behavioral tests.

| Area | Candidate compared with original designs | Current disposition |
|---|---|---|
| Shared composition | Grouped navigation, retained tabs/workspace and bottom session actions exist; standard status bar, underline tabs, smaller native text/heading metrics, tighter spacing and simpler borders differ from the raster | OPEN / Owner review |
| Products | Code/Name/Unit/Status, toolbar/results/right inspector and selected row exist; no ID primary column. Count30, actual25-row paging with Previous/Next, stacked title/count, textual status and separate Activate/Deactivate differ from the sample128/numbered pages/colored pills | OPEN / Owner review |
| Receipt | Detail has Back, title/number, Draft/Saved state, commands, metadata, full-width Note, multiline grid and totals; command grouping/position, text scale, native date display, status badge and separate Remove line differ from the raster/per-row trash | OPEN / Owner review |
| Copy and sample data | Existing English copy, unchanged mixed inventory navigation labels and synthetic data remain. No localization, new fields, avatar/profile data or exact sample totals are added | Scope boundary; Owner visual review remains OPEN |
| Graphite native surfaces | DateTimePicker field/drop arrow, list status ComboBox and native scrollbars can remain light. App-owned tab captions/bevel/focus and pinned lookup chevrons have their separately verified repairs | OPEN / control-specific review |
| Constrained view |944×600 intentionally scrolls/reflows, rather than compressing all fields into the wide reference layout; dirty input/selection and essential commands must remain reachable | OPEN / Owner review of actual captures and tests |
| Wide native125 | Six strict shell/Products/receipt rows cannot fit current hardware; no resized or clamped image establishes that target | NOT TESTED; bounded exception requires explicit Owner decision |
| T04 Minor | Generic catalog VM ErrorMessage can remain stale after a failed query followed by successful Refresh | Deferred behavior Minor; explicit final disposition OPEN |

## Capture integrity and remaining evidence

The [integrity register](capture-integrity.csv) currently contains22 reviewed native100 images: Products8 and receipt14. The [receipt state register](receipt-states-native100.txt) contains24 after-Capture records, including states not duplicated as comparison PNGs. SourceGrid custom offsets use its scrollbar units; document AutoScrollPosition uses pixels. For the1200→944 dirty state, active line11/column2 remains retained while first visible line4, grid scrollY3 and document scrollY−136 describe the actual rendered viewport; retention is not a claim that the active line is on screen. Both themes share the same data and transition sequence.

Native125 remains PENDING. Only reviewed synthetic images belong in this directory. No raw TRX, SQL allocation identity, connection, credential or unreviewed package screenshot is committed.
