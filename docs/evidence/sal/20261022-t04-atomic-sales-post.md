# SAL-T04 — reviewed atomic Sales Post checkpoint

Date: 2026-10-22. Development branch `codex/sal-direct-sales`; planning branch `v1a-pro`. Source/test commit `7eeafa4e46f5058a6c95155ad4cff27191315e90`, parent `3ed6c75add18b1766cf47fd3d146f348f58d9418`. Planning baseline remains `35ba2e8a3f37fb07095701eb94f4b56f7941c491`. T04 is COMPLETE in its bounded reviewed scope; [execution index](../../archive/20261023-pre-ui-next/plans/README.md) releases T05 IN PROGRESS. Full [SAL acceptance](../../DIRECT_SALES_ACCEPTANCE.md), Local/UI, frozen T08/package/Owner and operational release acceptance remain open. This development checkpoint is not deployable.

## Candidate and review

Root and reviewer approved candidate2 after closing Important R1. Its eight C# paths comprise SecurityAudit, ISalesReceiptWriteRepository, PostSalesReceipt, SqlSalesReceiptWriteRepository, SalesReceiptApplicationTests, SalesReceiptPostTests, SalesReceiptConcurrencyTests and GoodsReceiptQueryIntegrationTests. The final source manifest SHA256 is `ACC6D19662F3BB367308494077D79A2EEFF6A1D78E476C3D1FA2E30A8F1963FF`. Root independently verified all eight frozen source paths and the contemporaneous binary records. Candidate2 changes two test files relative to candidate1; product behavior is unchanged. No schema, project, vendor, GUI or package change is claimed.

Post independently requires Active Sales.Receipts.Post and the bound current actor. ID and exactly-eight-byte opaque version format validate before UoW. One owned transaction takes Shared before header access, distinguishes AlreadyPosted from stale Draft, locks lines/references, requires current ordinal Unit and takes every sorted stock key before mutation. Duplicate and gift demand share a locked Product Q/V pool. Exact T00 arithmetic freezes common unit cost and cumulative LineNo cost allocations, consumes all V on full depletion and retains zero-valued companions. Bounds and complete allocations validate before status/write effects.

The repository rechecks allocations, conditionally transitions status/rowversion, updates cost snapshots, pairs each negative quantity movement with its nonpositive valuation and applies Q/V deltas in the same UoW. Authoritative response/status/time/newVersion is materialized and its version validated before Commit. Safe success audit occurs after commit with businessOperationCommitted semantics; sink failure preserves committed success. There is no implicit Post retry, extra Read requirement for command success, hidden connection or new raw lock escape. Receipt Post and Exclusive reconciliation now share a coherent source protocol with sale Post; later workflow and frozen deployment acceptance are separate.

## Final executed evidence

Windows 10.0.26200, SDK8.0.425, VSTest17.11.1 x64, Release/net48. Root/reviewer independently reconciled actual TRX results and exact discovery identities. Counts are per scope, not additive unique totals. All scopes have zero failed/skipped/other outcomes.

| Scope | Executed / PASS | TRX SHA256 | Test DLL SHA256 |
|---|---|---|---|
| Focused Application | 148 / 148 | E7F0E7580A0BE266ED28BFF8CECEA480120FBC6175565B408DC144714C41FED8 | B00FF010012004E9B4310AC59A191893A0CBB5CB94B42F2084CA632B46A87F6E |
| Full Inventory | 169 / 169 | FDD232B2009238E28A66BC6BA9746306D2F3C4A9D0D35FA160E56B9BAFEBF2FE | 05B42539961DF332055E11B79218FBE818A1EF956160BD29A5072C4F9F255EA2 |
| Focused Foundation | 32 / 32 | 7217FE0FFB770DB23C312F1936873D5BE9F7E5CA2C4B58261AC81183A1574172 | 1A95E460C884BC7CA81AEC07E902EC92AB8B58C386BFAE15CC7EFBF85275A832 |
| Complete Architecture | 13 / 13 | CAEA2B8F60C72D1015AC9A3A69FC503FCB6C6A956997A573B99D59A49AAB2CEF | D3B22BE90439B187BB1B8469353BAB8DBB3A4995F6BE890E01A53F7FCEE5A4B8 |

Application includes75 SalesReceiptApplication,13 math,37 GoodsReceipt,5 SalesPermission,11 SalesContract,6 pipeline and1 registration rows. Inventory includes prior118 plus25 Post and26 concurrency/handshake rows (including four local R1 regressions). Final Inventory duration9m6; Foundation12s. No GUI execution or GUI/package binary evidence is implied.

Contemporaneous manifests contain **MyDmsVn DLL only**, respectively5/8/8/7 entries, captured after discovery/build and before execution. They are not external dependency/package manifests:

| Scope | Binary-manifest file SHA256 |
|---|---|
| Application | 3E39FFCC63D6B176C51D90DFD0F968055455BED12BB50D9584DC440DDFCF34F9 |
| Inventory | C48EA2EBF3500E0AABA34EE8C86D157844F1EC44320D1E2DF9429E4D62176530 |
| Foundation | BD60CB341934CBDDAC0C64CB65D575D48F3A8E549C645E7070D6E0E3C5B21070 |
| Architecture | 8968AC76852B992CEA260485D2DA5FE50C488A5FF1F737ABD218113B3BFD9F85 |

### Commands and selectors

Owners ran sequentially: Application, Inventory, Foundation, Architecture. For each corresponding `tests/MyDmsVn.Server.<owner>.Tests/MyDmsVn.Server.<owner>.Tests.csproj` (Inventory/Foundation use `Infrastructure.Inventory`/`Infrastructure.Foundation`; Architecture uses `MyDmsVn.Architecture.Tests`), the actual invocation was:

```powershell
dotnet restore <project> --locked-mode
dotnet test <project> -c Release -f net48 --no-restore --list-tests [--filter <selector>]
# Capture File/Bytes/SHA256 for bin/Release/net48/MyDmsVn*.dll before execution.
dotnet test <project> -c Release -f net48 --no-restore --no-build --logger "trx;LogFileName=t04-candidate2-final-<scope>.trx" --results-directory TestResults/sal-t04-candidate2-final-<scope> [--filter <selector>]
```

Application selector: `FullyQualifiedName~Sales|FullyQualifiedName~GoodsReceiptApplicationTests|FullyQualifiedName~ServerApplicationRegistrationTests|FullyQualifiedName~ApplicationPipelineTests`.

Foundation selector: `FullyQualifiedName~ConnectionAndUnitOfWorkTests|FullyQualifiedName~PersistenceTransactionTests|FullyQualifiedName~LinqToDbConnectionTests|FullyQualifiedName~UnitOfWorkExceptionalCleanupTests|FullyQualifiedName~LinqToDbMappingTests|FullyQualifiedName~DirectSalesMappingTests|FullyQualifiedName~RepositoryLifetimeTests|FullyQualifiedName~RepositoryAndQueryIntegrationTests`.

Inventory and Architecture are unfiltered. Raw TRX, proprietary SQL identities and local manifests remain uncommitted. This documentation checkpoint ran no build/test/SQL/GUI.

## Actual behavior and bounded acceptance

Independent persisted oracles establish paid/gift Q20/V300→Q15/V225, revenue75/cost45+30/profit0, common unit15 and gift price25 with zero revenue. Tiny Q3/V0.0002 allocates0.0001/0 at common0.00006667, leaves Q1/V0.0001, then drains Q/V to0 with exact total value and a zero companion. Later backdated receipt5@30 leaves Q20/V375 without changing prior sale cost/unit/version. Maximum/full-depletion, line/group/document/result overflow, insufficient demand, Unit/reference refusal, restricted runtime, immutable Posted and audit failure cases execute.

Concurrency covers gate-before-table, sorted first/second keys, no premature mutation, same-document Post/Post and Save/Post orderings, competing sales, receipt/sale both orders, never-inserted range protection, reconciliation both orders, reversed two-sale ordering, timeout/cancellation/deadlock/no retry and post-status cancellation. Fault injection after status/cost/ledger/value/balance and before commit independently verifies complete rollback.

A04–A07, A23 and A25–A31 are bounded PASS for this reviewed candidate/date in the [ledger](../../DIRECT_SALES_ACCEPTANCE.md). A23 is Application/SQL reviewed Save/Post/reference flow, not native UI proof. A02/A03/A08/A20/A22/A24/A32 and multi-owner T08 rows remain open for their full scope. Prior T01/T03 row dispositions retain their own evidence; this checkpoint does not replace frozen T08 revalidation.

## Original failures and repair boundaries

- Initial PostNotImplemented RED3 then GREEN3 is retained. Actual Application development run67=66PASS/1FAIL exposed an invalid authoritative returned version; an explicit precommit guard repaired it, then67PASS.
- Four reserved-LineNo SQL oracle syntax failures, absent ghost-key fixture diagnostic and reverse adjacent-document-line-range diagnostic remain separate original failures. Fixture/oracle repair is not retrospectively product PASS.
- Candidate1 full Application148 passed, but full Inventory165=164PASS/1FAIL hit the existing receipt timing test. Candidate1 Inventory TRX SHA256 is `7151344FFDD8B3DE3FEAB8F5CFEAEC419716854BE008FBDFD2273504CED9C96D`. Its test DLL hash was **NOT CAPTURED before rebuild** and cannot be reconstructed or replaced with candidate2's hash. The old receipt run lacked tracked reader await, so successful database drop alone is not independent proof of its reader drain.
- Important R1 concerned an unbounded pre-cleanup signal await in the absent-key test. Four worker-fault/cancel/scenario-cancel/timeout tests produced RED4, then GREEN4 after finite signal/worker/timeout racing; focused repair6 passed. RED TRX hash `5D723E3DDE889994813EAD2912ED2AE5CBC4B982DE587E1A59DC55F7D975BE99`; GREEN `A475FAFD9CD1F4E33FED0489C1C6BF0A7110C985580B5AC83E0C071F787F7CD8`; focused6 `59322733FCFDBC4F09AB82E324C65C4CD638B74215A65055ECD1DA07DD80B17A`.
- Candidate2 replaces the old receipt Delay150ms phase assumption with native header/line lock observation before expected1222. It tracks cancellation and bounded drain, releases/closes the blocker before reader drain and preserves primary versus cleanup errors. No production timeout or query behavior changed to make the test pass.

Final executor/root observations report successful identity/token-owned APP cleanup, bounded workers/connections drain, exited sessions and no outstanding cleanup exception/quarantine. These resource observations are attributed to executor/root; the documentation agent did not connect to SQL. Final candidate2 proof does not relabel earlier failures or fill missing old binary/drain evidence.

T00–T03 public evidence and the T01 migration runbook remain immutable. T01 R3 generic bootstrap diagnostics stays deferred Minor; exact9 SQL-login-only maintenance, trusted-administrator boundary, inherited collation portability and Local direct-SQL trust limits remain. No release/Owner acceptance, package proof or full SAL completion is claimed.
