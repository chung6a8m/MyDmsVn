# Business Compact design references

**Registered:** 2026-10-21. These are the three original design images supplied by the owner during PR #40 review and selected for the BCF follow-up. Files are copied byte-for-byte; the repository names are stable and Markdown-safe.

[Historical BCF plan](../../archive/20261023-pre-ui-next/plans/20261021-001-business-compact-lookup-and-visual-parity.md) · [Design](../../BUSINESS_COMPACT_REFINEMENT.md) · [Acceptance](../../BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md)

[Current Business Compact policy](../../design/BUSINESS_COMPACT.md) carries the durable visual/control contract. The image register and original BCF interpretation below retain their historical scope; they do not approve a later implementation candidate.

## Reference register

| File | Target | Pixels | SHA-256 |
|---|---|---|---|
| [01-catalog-light.png](01-catalog-light.png) | Products catalog, Business Compact Light | 1672 x 941 | `f6d09382503f6dc8a7ac8f1e4d23cddeb9b597f6f110058dd30fd8574ec681a1` |
| [02-goods-receipt.png](02-goods-receipt.png) | Goods Receipt detail, Business Compact Light | 1672 x 941 | `91645a56c978c3f2e8002bb311a91ff879c0bf6794cf58730707020c1687149b` |
| [03-catalog-graphite.png](03-catalog-graphite.png) | Products catalog, Graphite Dark | 1672 x 941 | `34ad34cc9445f74dfd347b0704f14abc4192fdb3b8711d86a5679c7f8445a859` |

## Interpretation

The reference images define visual composition, information hierarchy, density and Light/Graphite direction. Their example data, Vietnamese labels, sample counts and decorative details do not change existing contracts or authorize new business fields. Existing English UI copy remains in this roadmap. Reference 02 defines the receipt layout; reference 03 supplies the Graphite palette direction for the receipt's Dark counterpart.

Reference 02 depicts a detail-focused receipt surface. BCF makes the existing receipt list reachable through Back to receipts in the same workspace while applying the existing dirty/pending-input/recovery guards. The new design explicitly supersedes BCI's always-visible list/detail split and catalog-content exclusion; BCI's signed evidence remains historical.

The images are design inputs, not screenshots of implemented MyDmsVn, runtime verification, or exact pixel/percentage-similarity promises. Native geometry, keyboard accessibility, culture-specific numeric constraints and actual DPI remain acceptance obligations. Future evidence must use the comparison fixtures and capture provenance described in the acceptance document; do not count PrintWindow padding as application layout.

## Original attachment names

- `01-catalog-light.png`: MyDmsVn compact product catalog workspace-1.png.
- `02-goods-receipt.png`: MyDmsVn inventory receipt workspace-3.png.
- `03-catalog-graphite.png`: MyDmsVn graphite catalog workspace-2.png.
