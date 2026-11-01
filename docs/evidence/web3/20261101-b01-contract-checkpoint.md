# WEB3-B01 — source and canonical corpus checkpoint, 2026-11-01

**Scope:** Owner explicitly added backend B01 to complete WEB3-01. Source/contract work only; no API/runtime implementation. The [owning plan](../../plans/20261101-002-web3-goods-receipt-inventory-http.md#2-web3-b01--authoritative-sourcewirepermission-freeze-milestone-01) remains IN_PROGRESS pending cross-repo review/acceptance, not merged/Owner-accepted. B02–B05 remain PLANNED. No shared push, PR, merge or production approval.

**Inspected source:** clean `main@8b599c782816d9cd64f04a45d809a2b8eb1235c8`; implementation branch `codex/web3-01-contract-freeze`. The local commit containing this file/corpus is the handoff revision; its exact SHA is reported with delivery and pinned in Web provenance after commit. No source, project, lockfile, HTTP, SQL grant/schema, Desktop/CEX or WinFormsKit change. Backend runtime still exposes Sales/Catalog only and sales-only capabilities.

**Environment:** Microsoft Windows NT 10.0.26300.0; PowerShell 7.6.9; Node v22.14.0. Browser/.NET/SQL versions are not claimed because none was invoked. The SQL-free checks use Node built-ins only, no package install, secret/config discovery, network or SQL connection. Test temporary files use a single mkdtemp-owned directory and verified path cleanup.

## Source conclusions and bounded deliverables

- [Source checkpoint](../../design/WEB3_B01_SOURCE_CHECKPOINT.md) records exact source anchors, permissions, pre-commit materialization, positive returned line IDs and replacement, full-detail list/order, SQL principal gap matrix and later-owner obligations.
- [Canonical field tables](../../contracts/web3-v1/README.md), [manifest](../../contracts/web3-v1/manifest.json), golden payloads and controls freeze string Int64 counts/IDs, field-specific decimal scales, explicit response nulls, required paging, 0..7 UTC fractional digits, SQL-unspecified UTC semantics and Post ack without version. Existing Sales/Local behavior remains unchanged.
- Existing test source was inspected, not executed. The new corpus validator executes typed/semantic errors, fixture hashes, exact BigInt arithmetic, 200-row expansions and ordered Create/Update snapshot identity checks. It is a test oracle, not a product codec or HTTP handler.
- Frontend consumes exact backend commit/bytes with provenance and separately records compatibility and missing F03-specific wrappers. Backend evidence does not own frontend task status or attest a completed live integration.

## Actual checks

Executed from repository root:

```powershell
node docs/contracts/web3-v1/validate-corpus.mjs
node --test docs/contracts/web3-v1/validate-corpus.test.mjs
git diff --check
```

| Check | Actual result |
|---|---|
| Golden typed/semantic fixture validation and SHA-256 | **32 fixtures PASS** |
| Positive structural/query/capability cases | **29 PASS** |
| Negative structural/strict JSON/query cases | **68 rejected as expected** |
| Scalar allow/reject cases | **85 PASS** (includes >2^53, Int64/int32 maxima/overflow, decimal scale/range, UTC/date/version) |
| Exact receipt rounding controls | **4 PASS**, half-away/tie/carry/normal cases |
| Deterministic actual row expansion | **601 rows constructed** across valid200 request + valid200 response + invalid201 request; separate one-line case also validated |
| Cross-response lifecycle assertions | **15 PASS** (header/ordered line snapshots, stable receipt/no, fresh line IDs, ack identity/no version) |
| Node test runner | **8 discovered, 8 executed, 8 PASS, 0 FAIL, 0 SKIP** |
| Diff, touched-file UTF-8/no-BOM/LF, local Markdown link targets and source/status review | **PASS: 47 changed files, 248 local Markdown link targets**; corpus-local Git attributes preserve LF hashes on Windows checkouts |

These counts describe contract fixtures/checks, not HTTP calls, browser cases, SQL tests or .NET wrapper rows. Do not add them to archived WEB2 evidence.

## Failures and review corrections retained

1. First corpus CLI trial failed `Expected comma`: strict HTTP lexical JSON parsing was incorrectly applied to control metadata containing an intentionally invalid numeric `1.5` scalar test value. The fix permits standard numeric JSON only while reading control metadata; HTTP payload parsing still rejects exponent/floating numeric tokens. Subsequent complete corpus/test runs pass.
2. Fresh source review found an impossible combined error example: Save validation/reference errors had been mixed with Post-only `GoodsReceipt.ValuationOutOfRange`. Split validator, reference-handler and Post valuation fixtures by their realizable source stage/route; fieldless mapping remains covered without implying a combined endpoint response.
3. Fresh review checked migration006: D10 formatting alone did not reveal sequence MAXVALUE9999999999/NO CYCLE. Canonical output now requires GR plus ten digits with positive sequence, and rejects zero/eleven-digit examples. No schema widening.
4. Root review added Create as well as Update header/ordered-line response-to-request equality. Inventory count/rows lack explicit snapshot isolation, so their semantic check does not incorrectly require count>=returned-row count. Scalar diagnostic names distinguish string and numeric inputs.

## Not run / remaining gates

**NOT RUN:** HTTP endpoints/mappers/real capability whitelist; .NET build or tests; SQL queries/writes/grants/migrations, independent Q/V/COGS oracle; IIS Express/full IIS; browser/IME/accessibility; production release. B01 changes none of those boundaries, so no SQL/GUI/full-suite run was selected under the [test policy](../../TEST_POLICY.md).

B02/B03 must implement strict mapper output including local explicit-null handling and actual separate permission enforcement, update both narrow Web grants and privilege validator with restricted-principal tests, and preserve Sales/Catalog/auth regressions. B04/B05 own fresh SQL/browser evidence, concurrency/rollback, unknown response/permission revocation and package acceptance. No automatic write replay, broad grant, durable draft journal or idempotency promise is introduced.

Cross-repo review can accept the technical contract checkpoint only after consuming the immutable corpus and recording both source SHAs; formal task closeout follows owning-plan merge/acceptance policy. This evidence supplies no Owner acceptance or production approval.
