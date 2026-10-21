# SAL-T02 — valued receipt, shared locks and reconciliation

**Executed/reviewed:** 2026-10-22. **Disposition:** T02-owned valued Goods Receipt/coordinator/reconciliation/stock-read checkpoint COMPLETE. Same independent review closed R1 and approved the frozen candidate with no open task findings. Sales Post, coherent deployment, full SAL/Owner/package acceptance remain incomplete.

## Candidate and environment

- Development branch `codex/sal-direct-sales`; planning branch `v1a-pro`. Product/test commit `47165ded31bee64c8fc9583e93a8a9232a4cd69d`, parent/review BASE `cafee9e0e047dcacaf02b577d9f37d987f968307`, 28 C# paths. T01 source `81e8aab965e1eebd7b2faa40c05182f6ef8cb8eb` and its immutable [evidence](20261022-t01-schema-bootstrap.md) remain separate; planning baseline remains `35ba2e8a3f37fb07095701eb94f4b56f7941c491`.
- Frozen working-byte source manifest SHA256 `074E58FDE2DCBE4B7B107A44FC29A8E13A9861AEAEE7A0929C42A062A5220793`. All 28 source files retain UTF-8 BOM/CRLF; committed blobs match after declared CRLF-to-LF normalization, with BOM retained. No source changes occurred between final verification and commit.
- Consumed/compiled binary manifest SHA256 `D0377DFF467FFAA26FCF7F88E91BB897A7D53DA1C62A60019716FD89EA840F35`, 68 paths. Independent final review and root rechecked all 28 source and 68 binary hashes. This includes compile-only GUI/provider/vendor output and is not a shipped-package manifest.
- Windows 11, net48/Release, SDK 8.0.425; pinned LINQ to DB 6.3.0. SQL Server 14.0.2130.4 Developer 64-bit, owned database collation SQL_Latin1_General_CP1_CI_AS. Inherited collation portability is not certified elsewhere. Designated nonproduction test configuration has effective master; no connection/instance/database/login/token identifiers are published.
- No schema/script/grant/project/dependency/package/vendor change. BootstrapSourceGrid `2bd7874f7e80b22ef181763749c2a2938ab7f16e`, Bootstrap5WinFormUI `2be9a4d778fda7016207b75470020be6126f2f3f`, SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944` remain pinned.

## Implemented source boundary

Goods Receipt takes the transaction-owned Shared MyDmsVn.InventoryValuation.v1 gate immediately after BEGIN, before header/reference access. It validates line/document/result limits before status transition. The repository exposes AcquirePostingGateAsync, LockValuationBalancesAsync and ApplyPostedInventoryAsync; old quantity-only APIs and callers are removed. The Infrastructure-internal coordinator retains the exact current transaction, admits every sorted stock key before the first movement, captures each ledger identity and immediately inserts its rounded value companion, then updates Q/V together with affected-row checks. Zero-cost stock receives real zero-valued companions. Foreign/completed context, missing gate or unadmitted keys fail closed.

Typed LINQ 6.3.0 sequential composite-PK point reads express UPDLOCK/HOLDLOCK and protect absent-key ranges. No raw balance-lock escape was needed. The only new native command is typed sp_getapplock on the same owned connection/current transaction, Transaction owner/public principal, 15-second acquisition wait and 30-second command bound. Negative return, timeout, cancellation and deadlock do not authorize continuation or implicit retry. Existing header/draft and count/page native seams remain source-owned.

StockValuationState validates Q/V precision/range and zero-Q/zero-V. Exact nullable average is null at Q0, otherwise checked round8(V/Q). Signed coefficient aggregates accept representable final totals despite wide intermediate cancellation. StockBalanceRow requires InventoryValue/AverageUnitCost; StockCardRow requires ValueChange and resolves SalesReceipt SaleNo. Invalid Q/V/average or missing companion fails closed. Inventory COUNT/OFFSET paging is unchanged; Sales keyset/query remains later work.

Reconciliation owns one SERIALIZABLE connection/transaction and takes Exclusive before reading ledger → valuation → balances → receipt/sales sources. It reports key presence, exact signed Q/V, missing/extra counterparts, source/sign/range, receipt rounded values and frozen sale cost/gift coherence without repair or historical recosting. Receipt/sale lines are indexed once. Cooperative cancellation spans materialized indexing/grouped analysis/source checks/output enumeration and completion; output order stays numeric warehouse/product. Sales source seeds exercise these diagnostics, not an implemented Sales Post.

## Independent SQL and cancellation proof

Independent sessions observe the gate blocking Post before OBJECT/KEY/PAGE/RID acquisition; every sorted stock key/range held before ledger insertion; reversed line order; a held first key while the second blocks; absent-key RangeS-U blocking INSERT; reversed multi-key workers waiting on the same first composite key while a higher key remains independently lockable. Both post/reconciliation orderings serialize at the common gate. Generated LINQ with the required hints/index and native waiting/held-key/blocker observations remain in local TRX; this summary publishes no allocation identities.

Actual Post is the low-priority victim of a native header/application-lock deadlock, preserving status/version/ledger. A separate adversarial caller observes sp_getapplock -3 and proves UoW disposal rolls back a sentinel update and releases another transaction lock. That arrangement differs from production gate-first order, which has its own handshake. Restricted runtime is impersonated on the same connection using checked-in provisioning; no db_owner/ALTER workaround or second runtime connection.

Review R1 originally found repeated whole-history per-header scans and uncancellable analysis retaining Exclusive after materialization. Its deterministic RED returned success instead of OperationCanceledException. The repair indexes sources once and checks cancellation throughout analysis. The test uses existing session-wrapper/pinned UseTracing Completed on the final SalesReceiptLines reader; no product hook/DI option/public API was added. An independent transaction confirms Shared is unavailable while Exclusive is held, then immediately obtainable after cancellation; the owned connection closes and source status/ledger remain unchanged. Focused GREEN 15 includes this boundary and normal/corrupt reconciliation plus the corrected lock observer; the final complete Inventory run includes the repaired case.

## Final frozen execution

Each final discovery identity set, including Theory display values, matches its actual TRX with zero missing/extra identities. All rows below passed with zero failed/skipped/not-executed/aborted/timed-out/warning outcomes. SQL owners ran sequentially: Inventory, Foundation, Security. Other scopes are non-GUI. The GUI project compiled with 0 warnings/0 errors; GUI tests were not executed.

| Scope | Discovered/selected/executed/PASS | FAIL/SKIP | TRX SHA256 |
|---|---:|---:|---|
| Complete Inventory | 91/91/91/91 |0/0| `DFC2DBADF91B70FCB09B471BF84AA72500DF2C415C8704A6DE17E27D0B8ABDAE` |
| Affected Application | 76/76/76/76 |0/0| `DB65691606B28F835932568FBC7A683DE4620CC467005D308B2F05C27CD58467` |
| Affected Foundation | 32/32/32/32 |0/0| `45B7226D21D4A2D844F27AECD43E26BF120CBE1A4A820FBDFBDD9A3F1A115503` |
| Security shared fixture | 1/1/1/1 |0/0| `67C8BBB3BC3C60A90DFC3B036B082C56FC6A60220562C16D804CBD503E1D88B0` |
| Inventory Session VM | 7/7/7/7 |0/0| `8C0DE3E4E97BC8D9D046AE6F28EDC7ACE109EF64CC778DFF4891CCA4B22AD048` |
| Complete Architecture | 13/13/13/13 |0/0| `D781C5375B8081CDC7E4938FA84566072DC98373DEB581BE7087E7B8B3AE7247` |

Inventory 91 includes 49 valuation rows; remaining rows are its current receipt/storage/query/fixture regressions. Application 76 is the four-class affected selector below. Foundation 32 is the explicit ownership/transaction/mapping/repository selector; Security 1 is the shared fixture Fact, whose internal forward/reverse scenarios are not additional TRX rows. Session 7 is InventoryViewModelTests; Architecture 13 is the complete project. Earlier development/focused runs overlap; no additive unique-case total is claimed.

Commands below are the recorded final3 invocations, not commands executed for this documentation update. Each discovery/build completed before no-build execution; locked Inventory restore occurred at task start, other locked restores are explicit below. All targets are Release/net48. Local discovery/log names follow `t02-final3-<scope>`; raw TRX remains local under `.local/t02-results/final3-<scope>/t02-final3-<scope>.trx`.

```powershell
dotnet test tests/MyDmsVn.Server.Infrastructure.Inventory.Tests -c Release -f net48 --no-restore --list-tests
dotnet test tests/MyDmsVn.Server.Infrastructure.Inventory.Tests -c Release -f net48 --no-build --no-restore --logger 'trx;LogFileName=t02-final3-inventory.trx' --results-directory .local/t02-results/final3-inventory
dotnet restore tests/MyDmsVn.Server.Application.Tests --locked-mode
dotnet test tests/MyDmsVn.Server.Application.Tests -c Release -f net48 --no-restore --filter 'FullyQualifiedName~GoodsReceiptApplicationTests|FullyQualifiedName~InventoryQueryApplicationTests|FullyQualifiedName~ContractEnvelopeTests|FullyQualifiedName~SalesValuationMathTests' --list-tests
dotnet test tests/MyDmsVn.Server.Application.Tests -c Release -f net48 --no-build --no-restore --filter 'FullyQualifiedName~GoodsReceiptApplicationTests|FullyQualifiedName~InventoryQueryApplicationTests|FullyQualifiedName~ContractEnvelopeTests|FullyQualifiedName~SalesValuationMathTests' --logger 'trx;LogFileName=t02-final3-application.trx' --results-directory .local/t02-results/final3-application
dotnet restore tests/MyDmsVn.Server.Infrastructure.Foundation.Tests --locked-mode
dotnet test tests/MyDmsVn.Server.Infrastructure.Foundation.Tests -c Release -f net48 --no-restore --filter 'FullyQualifiedName~ConnectionAndUnitOfWorkTests|FullyQualifiedName~PersistenceTransactionTests|FullyQualifiedName~LinqToDbConnectionTests|FullyQualifiedName~UnitOfWorkExceptionalCleanupTests|FullyQualifiedName~LinqToDbMappingTests|FullyQualifiedName~DirectSalesMappingTests|FullyQualifiedName~RepositoryLifetimeTests|FullyQualifiedName~RepositoryAndQueryIntegrationTests' --list-tests
dotnet test tests/MyDmsVn.Server.Infrastructure.Foundation.Tests -c Release -f net48 --no-build --no-restore --filter 'FullyQualifiedName~ConnectionAndUnitOfWorkTests|FullyQualifiedName~PersistenceTransactionTests|FullyQualifiedName~LinqToDbConnectionTests|FullyQualifiedName~UnitOfWorkExceptionalCleanupTests|FullyQualifiedName~LinqToDbMappingTests|FullyQualifiedName~DirectSalesMappingTests|FullyQualifiedName~RepositoryLifetimeTests|FullyQualifiedName~RepositoryAndQueryIntegrationTests' --logger 'trx;LogFileName=t02-final3-foundation.trx' --results-directory .local/t02-results/final3-foundation
dotnet restore tests/MyDmsVn.Server.Infrastructure.Security.Tests --locked-mode
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests -c Release -f net48 --no-restore --filter 'FullyQualifiedName~ApplicationDataFixtureReuseIntegrationTests' --list-tests
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests -c Release -f net48 --no-build --no-restore --filter 'FullyQualifiedName~ApplicationDataFixtureReuseIntegrationTests' --logger 'trx;LogFileName=t02-final3-security.trx' --results-directory .local/t02-results/final3-security
dotnet restore tests/MyDmsVn.Desktop.Session.Tests --locked-mode
dotnet test tests/MyDmsVn.Desktop.Session.Tests -c Release -f net48 --no-restore --filter 'FullyQualifiedName~InventoryViewModelTests' --list-tests
dotnet test tests/MyDmsVn.Desktop.Session.Tests -c Release -f net48 --no-build --no-restore --filter 'FullyQualifiedName~InventoryViewModelTests' --logger 'trx;LogFileName=t02-final3-session.trx' --results-directory .local/t02-results/final3-session
dotnet restore tests/MyDmsVn.Architecture.Tests --locked-mode
dotnet test tests/MyDmsVn.Architecture.Tests -c Release -f net48 --no-restore --list-tests
dotnet test tests/MyDmsVn.Architecture.Tests -c Release -f net48 --no-build --no-restore --logger 'trx;LogFileName=t02-final3-architecture.trx' --results-directory .local/t02-results/final3-architecture
dotnet restore tests/MyDmsVn.Desktop.Tests --locked-mode
dotnet build tests/MyDmsVn.Desktop.Tests -c Release -f net48 --no-restore
```

## Retained original outcomes and review disposition

| Development/frozen trial | Actual outcome and boundary |
|---|---|
| Initial receipt/reconciliation behavior RED |2 executed / 2 FAIL / 0 skip: missing value companion and incorrectly empty reconciliation. GREEN1 repaired both.|
| Query behavior RED |2 executed / 2 FAIL / 0 skip: missing authoritative value/average and missing-companion refusal.|
| Setup/compile trials |Missing assembly-owned SqlServerTheoryAttribute, obsolete tracing API, native @@SPID Int16/int fixture cast and CS9124 fixture capture are setup/compile failures, not intended behavior RED.|
| Lock observer trials |Incorrect ProductId-only/covering-index blockers and assumed immediate blocking-session order were corrected with exact composite-PK point blockers and actual held-key identity. Accounting/lock invariants were retained; originals remain local.|
| Candidate1 complete Inventory |90 executed, 89 PASS / 1 FAIL / 0 skip, 4m36s. Shared contamination seed omitted schema10 NOT NULL InventoryValue before its expected sentinel assertion. Required seed value 6 for Q2/cost3 is the narrow compatibility repair.|
| Candidate2 complete Inventory |90 executed, 89 PASS / 1 FAIL / 0 skip, 5m4s. Shared seed now passed; reversed-key observer assumed connection recording order equaled SQL wait-queue order. Both workers now match actual same-held-key identity; no prelock ledger writes/higher-key lockability/Q/V assertions were weakened.|
| Review R1 cancellation RED |1 executed / 1 FAIL / 0 skip, expected OperationCanceledException but old analysis returned success.|
| Review R1 scoped GREEN |15 executed / 15 PASS / 0 skip, covering cancellation, receipt/sale reconciliation and lock observer; not a substitute for final Inventory 91.|

Candidate1 full TRX SHA256 `384A2473A13E3E8C662C85FAA210DCD2FB96A74A162F4F70131BDE53E6C73988`; candidate2 `EADE50D0CB8AB51189277BAA13B9748ABE35C3879D533B8FC8949C45B474A8D3`; R1 RED `947BEDC4AD3D6375018D9C742038E96E3C58C8B7E0B1AFB62E4A0E7DE3E71E06`; R1 GREEN `C434B8E2AF695660731DB512013CF57BC82A63AD8E9A6F01BF5DD834E42F976A`. Later PASS does not rename the earlier failures.

The original candidate1 manifest report distinguished an LF logical-text digest from the actual CRLF file-byte digest; source entry hashes were unchanged. Final candidate3 and binary manifest hashes above are actual file-byte digests, not logical-text or Git tree hashes. No semantic/encoding delta occurred between final candidate3 verification and commit.

Same independent reviewer approved the three-file R1/observer repair, then independently checked final 28 source / 68 binary hashes and six discovery/TRX identity sets before closing the task gate. R1 is addressed; no new task findings remain. T01 R3 generic bootstrap diagnostics remains a deferred Minor; T02 does not resolve it.

## Resource and acceptance boundary

Existing SqlTestDatabase allocation identity/token guards own every disposable SQL resource. Workers/blockers/readers/transactions/connections drain before verified cleanup; incomplete drain would quarantine rather than drop. Inventory/Security APP observations record successful verify/cleanup/drop; Foundation/fresh cases returned without cleanup exceptions. No outstanding task-owned process/resource/quarantine remains. No global lease, new runner/approval file, prefix-based cleanup, raw SQL identity or secret was added to public evidence.

The shared contamination helper still probes its historical 15-table set, with explicit InventoryValue; it is not complete Sales contamination proof. [T01 Foundation evidence](20261022-t01-schema-bootstrap.md) retains its complete 18-mutable/19-total reset proof. T08 matched six-owner fresh/reuse integration remains open.

[SAL acceptance](../../DIRECT_SALES_ACCEPTANCE.md) records T02-owned portions of A17–A19/A26–A28/A30/A32 only. Every complete multi-task row remains open for Sales writer/T04, Local uncertainty/T06 or frozen T08 portions. Sale reconciliation seeds are not a production sale writer. Session tests/GUI constructor compilation do not establish native controls, Login or GUI/package acceptance.

T03 is IN PROGRESS per the [execution index](../../plans/README.md), outside this frozen source. Sales Post, coherent receipt/sale/reconciliation deployment, keyset queries, Local/UI, frozen integration/package and Owner acceptance remain incomplete. Schema10 and T01 SQL-login-only maintenance/trusted-administrator and inherited collation limits are unchanged. The checkpoint is not deployable; operational release prerequisites remain **UNACCEPTED / UNSCHEDULED**.
