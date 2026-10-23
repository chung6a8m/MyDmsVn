# SAL-T05 — reviewed Sales keyset queries

Date: 2026-10-22. Development branch `codex/sal-direct-sales`; planning branch `v1a-pro`. Source/test commit `f9149a6a7ca35d26163fac0745101774246891a7`, parent `0b6c5f961cceed447f3ddf4b5e8a44fb17e2bacc`. Planning baseline stays `35ba2e8a3f37fb07095701eb94f4b56f7941c491`. T05 is COMPLETE in its reviewed query scope. [Execution index](../../archive/20261023-pre-ui-next/plans/README.md) records T06 IN PROGRESS; Local/VM/UI, frozen T08 SQL/UI/package/Login/attended/Owner and operational release acceptance remain open. This development checkpoint is not deployable.

## Frozen candidate and review

Combined review found no product blocker and one Important test-containment issue, T05-R1. Scoped candidate2 review closed R1 with no new findings; final evidence review APPROVED the bounded checkpoint. Candidate2 has eight source/test paths and manifest SHA256 `AE102A3596FA7730A592CFD14A5E8F2A5DCA115E8F8CC7AB40B4F29442768A3E`. Root independently verified the frozen sources and archives. The only candidate1→candidate2 delta is the reviewed paging-test loop/cancellation repair; product files are unchanged.

The eight paths are Application Sales `ISalesReceiptQueryService.cs`, `SalesReceiptQueries.cs`, `SalesReceiptCursor.cs`; Infrastructure Sales `SqlSalesReceiptQueryService.cs`; Application tests `SalesReceiptApplicationTests.cs` and `SalesReceiptQueryContractTests.cs`; Inventory tests `SalesReceiptQueryTests.cs` and `SalesReceiptQueryOwnershipTests.cs`. The existing fake receives the required List interface method. GetById and existing DTO/DI/mapping remain unchanged. No schema/index/project/vendor/package/Local/UI change is claimed. LINQ6.3.0, MicrosoftSqlClient7.0.2 and Newtonsoft13.0.4 remain pinned; vendor revision remains `2bd7874f7e80b22ef181763749c2a2938ab7f16e`.

## Implemented behavior

List independently requires current Active Sales.Receipts.Read through the existing actor/account/permission pipeline. Normalization validates inclusive unspecified calendar dates, optional positive IDs, exact Draft/Posted status, trimmed blank→null Search, Search256/NUL/Unicode and PageSize1–200/default50. Direct query-service validation and pre-cancellation occur before admitting an owned connection; cursor errors preserve Request.Cursor/Validation.InvalidCursor and public `cursor` field mapping.

The cursor is canonical unpadded base64url, bounded4096 encoded/3072 decoded bytes, strict UTF8 and a flat exact JSON schema: v/order/direction/anchor/pageSize/filterHash. Supported v1/SaleIdDesc/next|previous, positive Int64 anchor, exact primitive types and normalized-filter/PageSize binding are required. Duplicate/unknown/missing/coerced/nested/extension JSON rejects. Exact size limits with legal trailing JSON whitespace are accepted; noncanonical terminal bits reject. SHA256 binds fixed-order FromDate/ToDate/CustomerId/WarehouseId/EmployeeId/Status/ProductId/Search/PageSize with invariant dates/numbers and explicit nulls. It provides consistency, neither secrecy/authenticity nor authorization; reconstructing a cursor never grants Read.

Typed LINQ combines AND header filters, Product Any/EXISTS and literal substring over SaleNo/Note/customer Code/Name. Historical inactive labels remain visible. Header filter/comparison/order/Take(n+1) executes server-side before bounded label formatting. Code and Name are projected separately and combined only for retained rows in C#, avoiding inherited mixed-column collation concatenation without moving filtering/paging out of SQL.

First/Next use SaleId DESC with strict <anchor. Previous uses >anchor ASC, trims the extra row before reversing. Cursors use retained displayed endpoints; same-filter bounded Any proves opposite availability when needed. Missing/deleted/nonmatching anchor rows remain legal comparisons. Empty pages have null cursors; client recovery remains T06. Identity order is not commit/business-date order and navigation promises no cross-request snapshot.

All-line grouped totals are bounded to retained header IDs, include gifts/duplicates/other Products and reject document overflow. No full-line list payload, header recovery grouping, N+1, required count, OFFSET or arbitrary page jump is introduced. Existing pagination elsewhere remains source-owned. Page, aggregates and probe run sequentially in one owned SERIALIZABLE connection/transaction with inherited30s command timeout/cancellation. Complete result materializes before commit; failure/cancellation disposes before return. No hidden connection, public provider object, new native product SQL escape or retry is introduced.

## Actual final execution and provenance

Windows10.0.26200, SDK8.0.425, Release/net48, VSTest17.11.1 x64. Root/reviewer reconciled actual discovery identities, immutable final2 TRX rows and hashes. Each complete project has zero failed/skipped/aborted/notExecuted outcomes; counts are per invocation, not additive unique totals.

| Complete scope | Discovered / executed / PASS | TRX SHA256 | Test DLL SHA256 |
|---|---|---|---|
| Application | 326 / 326 / 326 | A96CCA2DC27EDABC89891D26ABB2B515F2CF6B8CAB413AA404A3DA0475C68D59 | 6CD31D59F3045FC1929560990526223BE9DD87EEABC9F98E707C48ADB6679394 |
| Inventory | 189 / 189 / 189 | 7D974FD593138505C8D43394CE4066797A24CC57B55F3A30582B7A7038F6AAE6 | 250D35C312FA50F8A9D558ECA02D2B6A45EBFC99BAB46241925ACA4972997B65 |
| Architecture | 13 / 13 / 13 | 7D392B4104204124816E6C38DF6874EAD7D7DB91FD4410EE84DB11763F5B96F6 | 1A0B2230CEE358DC862AEEF530C659696268FD25F08781130CCB139F71789091 |

Application includes65 Sales query contract rows. Inventory includes20 query/ownership rows (12 query SQL and8 ownership, one local no-connection row) plus prior169 regressions. Four repaired size rows execute inside final189 as well as separate focused4. Inventory duration10m4; Application/Architecture1s each per console. No GUI execution/compilation/package proof is added here.

Final candidate2 has21 contemporaneous MyDmsVn DLL/EXE entries,5/9/7 per scope, captured immediately before no-build execution, immediately rehashed and raw-archived before the next build. Root/reviewer verified all21 live and raw archived entries against these manifests; these are product/test binary records, not a package/external-dependency manifest.

| Scope | Binary-manifest file SHA256 |
|---|---|
| Application | 735D65699527205B22CD623B1124502BA2AB18473499AD9ED43C0588686EF07D |
| Inventory | 37B8BD6307FC686F13B33C30D9C98BEE7F7D73570E205A181205F4F9FE870081 |
| Architecture | DB14584AECF52440012C9CF108CC3912B59808C520C386A26FD1F2004937CF25 |

Actual final runner used the following unfiltered sequence for Application, Inventory, Architecture, serially. Projects were respectively `tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj`, `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj`, `tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj`.

```powershell
dotnet restore <project> --locked-mode
dotnet test <project> -c Release -f net48 --no-restore --list-tests
# Capture MyDmsVn*.dll/*.exe File/Bytes/SHA256 before no-build execution.
dotnet test <project> -c Release -f net48 --no-restore --no-build --logger "trx;LogFileName=t05-final2-<scope>.trx" --results-directory TestResults/sal-t05-final2-<scope>
# Immediately rehash and archive matching raw binary bytes before next build.
```

Raw TRX/SQL/plans, proprietary identifiers and local binary archives remain uncommitted. The documentation checkpoint reads existing artifacts only and runs no build/test/SQL/GUI.

## Query/index and ownership proof

The retained actual SQL/STATISTICS XML/IO observations use a bounded synthetic1200-header/3600-line workload, anchor600,size3, duplicate lines, Product every7th and literal Search every17th header. Unfiltered Next599/598/597 and Previous603/602/601; selective Next595/476/357 and Previous952/833/714. Nonempty cursor requests issue three sequential commands, with no header count.

| Workload | Page / aggregate / probe logical reads |
|---|---|
| Unfiltered Next | 26 / 24 / 2 |
| Unfiltered Previous | 26 / 24 / 2 |
| Selective Next | 896 / 24 / 150 |
| Selective Previous | 1162 / 24 / 150 |

Observed clustered header seeks/Top, Product membership `IX_SalesReceiptLines_ProductId_SaleId` seeks and retained-line `UX_SalesReceiptLines_SaleId_LineNo` access/lookups/Stream Aggregate support this workload. No schema/index addition or universal constant-cost/performance threshold is inferred. Substring and residual filter costs, inherited collation and T08 frozen workloads remain explicit.

Ownership cases positively observe the same connection/DbTransaction and Serializable isolation across page/aggregate/probe. Concurrent header update receives1222 until read completion; blocked aggregate cancellation closes the owned read; delayed lower identity commit blocks then appears at its key position. Success/cancel/injected-fault release owned connection/locks. Workers are tracked with finite waits and cleanup; uncertainty quarantines rather than drops.

A33–A39 are bounded PASS for this exact reviewed candidate/date in the [acceptance ledger](../../DIRECT_SALES_ACCEPTANCE.md). A40 has only its T05 query/index portion verified and stays open for full T08 scope. No A41–A44 Local/session/VM, native UI or Owner outcome is implied.

## Original failures and candidate boundaries

- Intended Application RED64=7PASS/57FAIL, real SQL membership RED1FAIL and missing paging behavior RED6FAIL remain their actual invocations. Application then64PASS. Initial/intermediate development SOURCE and BIN hashes/raw archives were **NOT CAPTURED contemporaneously**; final frozen archives cannot reconstruct those bytes.
- The first implementation SQL6 failed on real inherited Code/Name collation concatenation, then6PASS after bounded C# label formatting. Expanded SQL10 and later contract65/query-combined19 passed; later regression expansion does not invent a retroactive RED.
- Ownership trace API usage caused compiler errors under pinned6.3.0 (no executed RED); documented per-instance UseTracing replaced obsolete-as-error setters. Ownership8=6PASS/2FAIL exposed unquoted workload LineNo and an unused-index-path blocking observer; quoted LineNo and actual aggregate data no-op UPDATE repaired the two observer/setup cases, then focused2PASS. No timeout increase or skipped case substituted.
- Candidate1 complete Application326/Inventory189/Architecture13 all passed in their own runs. Important R1 found that successful repeated cursors could leave test navigation loops unbounded despite per-command timeout. Candidate2 adds fixture-derived hop caps, nonempty/strict boundary progress and a shared30s cancellation budget while retaining exact sequence/anchor assertions. Focused sizes1/3/11/50 passed4/4 (TRX SHA256 `662C840BDE8EDF22F76F590C2D1B28BB27DEF2C1F58FF5BB05CA798641798B05`). There was no product change or artificial RED for this review repair.
- Candidate1 final Inventory test DLL hash **was captured pre-execution**: `4ACBB562B103D5771B83E84F4B810D2BBA43243D601016FE4C32DDEAB2553204`. Its raw bytes were **not archived** and no post-run rehash preceded the R1 rebuild. Do not label that hash missing, reconstruct old bytes or substitute candidate2's DLL. Candidate1 TRX hash `99C2AFDCD9344EB3E4974B186E98E790D8FAE77210EBEF8C4110511D15A0347A` remains separate from final2. Candidate2 full189 is a new actual pass, not candidate1 full189 plus focused4 relabeled.

Final executor/root observations report exit0, no active testhost/pending execution, successful identity/token-owned cleanup and tracked worker/connection drain. Actual Inventory fixture output records APP drop succeeded=True; no cleanup failure, uncertain allocation, quarantine or forced deletion is reported. These process/resource observations are attributed to executor/root; documentation/reviewer did not connect to SQL. A DMV snapshot alone is not disposal proof.

T00–T04 public evidence and T01 migration runbook remain immutable, including their original failures and provenance gaps. T01 R3 generic bootstrap diagnostics remains deferred Minor. Exact9 SQL-login-only/trusted-administrator, inherited collation, Local direct-SQL trust, mixed-client/deployment and operational release boundaries remain. T06–T08/full SAL/Owner/package acceptance is not established by this checkpoint.
