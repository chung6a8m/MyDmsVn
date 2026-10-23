# SAL-T03 — authorized Sales Draft persistence and detail

**Executed/reviewed:** 2026-10-22. **Disposition:** T03 Draft Create/Update/Get checkpoint COMPLETE; independent specification/quality review and final evidence closeout APPROVED without new Critical/Important/Minor findings. Sales Post/List/Local/UI, frozen T08 and Owner/package acceptance remain incomplete. This checkpoint is not deployable.

## Candidate and environment

- Development branch `codex/sal-direct-sales`; planning branch `v1a-pro`. Source/test commit `1164199bb7c7500c61fe41ed21eabb58c7b3a2a7`, parent/review BASE `f8c336ae496d0c93c1146b0636a2c3b8121fd7ca`. T02 product baseline remains `47165ded31bee64c8fc9583e93a8a9232a4cd69d`; planning baseline remains `35ba2e8a3f37fb07095701eb94f4b56f7941c491`.
- Frozen working-byte source manifest SHA256 `F6752567FF80AECFDC6E735F303ED3AAF0B6487E84C57DC009B4A5D6ED4E3B92`, 11 C# paths. Final binary manifest SHA256 `EEEB2DBD3B40450EC2CFC3C87E9F12F86E7B903FB4737A7AABC052815D8563B3`, 51 consumed test/product/LINQ/SqlClient entries. Independent reviewer/root verified all source and binary entries at closeout. These are consumed test outputs, not a shipped package.
- C# UTF-8 BOM/CRLF normalization preceded freeze/final builds. No source bytes changed after final verification; Git blobs match after declared CRLF-to-LF normalization, retaining BOM. Whitespace/scope checks passed. No schema/script/project/lock/package/vendor change.
- Windows 11 Home Single Language 10.0.26200, active net48/Release, SDK 8.0.425, LINQ to DB 6.3.0. Designated nonproduction effective-master SQL Server 14.0.2130.4 Developer 64-bit, SQL_Latin1_General_CP1_CI_AS. Other-server/collation portability remains uncertified. No connection/host/database/login/token identifiers are published.
- Vendor pins unchanged: BootstrapSourceGrid `2bd7874f7e80b22ef181763749c2a2938ab7f16e`, Bootstrap5WinFormUI `2be9a4d778fda7016207b75470020be6126f2f3f`, SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`. Existing T00/T01/T02 evidence and T01 deferred R3 remain separate and unchanged.

## Implemented authority and persistence boundary

Create/Update use existing AuthorizedActorApplicationRequest with independent Active Write; GetById independently requires Read. Post-only grant authorizes neither operation. Current account/binding/direct-Deny rules are preserved; bound AuthorizedUserId supplies the actor. Successful Write returns its existing authorized command-result DTO without an extra Read grant or protected postcommit Get. This does not authorize the separate Read query or implement the Post endpoint.

Validation rejects missing IDs/date, Note beyond 1000 or NUL, empty/null/201-line buffers, invalid identity/Qty18,4/Price19,4 precision/sign/range and exact line/document/Product aggregate overflow before UoW creation. Duplicate Products remain independent; nongift zero price is valid; gift retains entered price with zero revenue. Save derives Unit from current active Product and uses T00 exact revenue math. The scoped date-only JSON converter remains the T00 implementation, rerun here; there is no new serializer, Local adapter or end-to-end Local proof.

Write repositories borrow the same caller-owned connection/current transaction. HOLDLOCK reference reads retain active state/Unit through Save; Products use sorted point reads. Update locks the header, rejects missing/Posted/stale state after request-version format validation and validates canonical stored lines and the entire retained-ID set before mutation. Repeated zero IDs become distinct new rows; positive IDs must be unique and owned. Omitted rows are removed, retained IDs/Created audits survive, and validated positive temporary LineNo 201–400 permit safe final 1–N resequencing under the existing uniqueness constraint. This does not expand the 200-line business limit.

Explicit typed LINQ mutation allowlists exclude immutable number/Created fields, SQL-owned identity/default/version and posting/cost fields. NEXT VALUE FOR SalesReceiptNumberSequence is the narrow SQL-specific exception on the same owned transaction, 30s command bound, with explicit 1–9,999,999,999/no-cycle bounds and no exhaustion fallback. Server numbers are SA plus ten digits; rollback gaps are valid. Complete Draft response/newVersion/assigned ordered line IDs/labels/amounts materialize before Commit; no fallible postcommit fetch constructs successful Write output. Draft costs/profit/posting time remain null and no stock/value tables are changed.

GetById owns one short SERIALIZABLE LINQ session/transaction. Header, bounded ordered lines, current labels/active flags and exact all-line totals materialize before commit/disposal. Historical inactive labels and persisted Unit/frozen costs remain readable; invalid stored order/amount/status/cost shape fails closed. Posted read fixtures seed source rows explicitly and do not implement or certify Sales Post. List/cursor/keyset/Local/UI remain later tasks.

## Independent behavior proof

Native SQL oracles establish distinct generated IDs, retained creation audit, immutable SaleNo, opaque version changes, exact rounded revenue/totals, no stock/value effects, active-reference refusal, foreign/repeated IDs and canonical order before any mutation. A 200-row reverse retains all identities without transient uniqueness failure. Sequence tests establish separate numbering, last legal number, exhaustion refusal and rollback gaps. Injected header/line INSERT/UPDATE/DELETE failures roll back the aggregate.

Independent-session competing Saves wait on the locked header and only one expected version wins; cancellation leaves original version/amounts. Unit reference blocking proves catalog Unit is retained through Save and a subsequent Save explicitly captures current reviewed Unit. A recorded detail session is SERIALIZABLE on one connection, retains its header lock while blocked on Product, drains/releases database locks on cancellation and permits subsequent reload. These are actual SQL observations, not mock-only transaction claims.

## Final frozen execution

Four actual discovery identity sets, including Theory display values, match executed TRX sets with no missing/extra identities. All rows passed, zero failed/skipped/not-executed/aborted/timed-out/warning outcomes. Compiler discovery/build logs have zero compiler warnings/errors. SQL owners were serialized; no GUI, Local/Login or package execution was performed.

| Scope | Discovered/selected/executed/PASS | FAIL/SKIP | TRX SHA256 |
|---|---:|---:|---|
| Affected Application | 127/127/127/127 |0/0| `BD0D744AA058665A5DF0B8F7B74E69B3A291844A4519FBE6777E5F62BCFD03BA` |
| Complete Inventory | 118/118/118/118 |0/0| `2232CC29AEC07B55BE64BE5A661BEDFFDD969753E503A43BBA53FCD30E56B067` |
| Affected Foundation | 32/32/32/32 |0/0| `C1C2ACA5A3104FF0FB9F47F3FC32A57AA4EA1ECA5075D8E692860B8B88C499E5` |
| Complete Architecture | 13/13/13/13 |0/0| `8AA1BB0018C97BFAA9CF3DDED0042FBCA1204729AD35D26F0C368E70DDEC2353` |

Application 127 = SalesReceiptApplication 54 + other Sales foundation 29 (contracts 11 / math 13 / permissions 5) + affected existing 44 (GoodsReceipt 37 / pipeline 6 / registration 1). Inventory 118 = prior Inventory 91 rerun + Sales Draft 25 + mappings 2 (one local/one SQL). Foundation 32 covers the explicit ownership/connection/transaction/mapping/repository selector; Architecture 13 is complete. These are 290 final invocations across different scope responsibilities, not full SAL or additive unique development coverage. Inventory's full run took 6m14s; the original SQL 20 run with two oracle failures remains distinct.

Commands below are recorded executions from the candidate checkout, not commands run for this documentation update. Application/Inventory locked restores succeeded at task start without later dependency changes; Foundation/Architecture restores are explicit below. Discovery compiled current Release/net48 output before no-build execution. Raw TRX stays local under the exact result paths below.

```powershell
dotnet test tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj -c Release -f net48 --no-restore --list-tests --filter 'FullyQualifiedName~Sales|FullyQualifiedName~GoodsReceiptApplicationTests|FullyQualifiedName~ServerApplicationRegistrationTests|FullyQualifiedName~ApplicationPipelineTests'
dotnet test tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj -c Release -f net48 --no-restore --no-build --filter 'FullyQualifiedName~Sales|FullyQualifiedName~GoodsReceiptApplicationTests|FullyQualifiedName~ServerApplicationRegistrationTests|FullyQualifiedName~ApplicationPipelineTests' --logger 'trx;LogFileName=t03-final-application.trx' --results-directory TestResults/sal-t03-final-application
# SQL commands explicitly loaded the designated User-scope test connection into the process without printing it.
dotnet test tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj -c Release -f net48 --no-restore --list-tests
dotnet test tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj -c Release -f net48 --no-restore --no-build --logger 'trx;LogFileName=t03-final-inventory.trx' --results-directory TestResults/sal-t03-final-inventory
dotnet restore tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj -c Release -f net48 --no-restore --list-tests --filter 'FullyQualifiedName~ConnectionAndUnitOfWorkTests|FullyQualifiedName~PersistenceTransactionTests|FullyQualifiedName~LinqToDbConnectionTests|FullyQualifiedName~UnitOfWorkExceptionalCleanupTests|FullyQualifiedName~LinqToDbMappingTests|FullyQualifiedName~DirectSalesMappingTests|FullyQualifiedName~RepositoryLifetimeTests|FullyQualifiedName~RepositoryAndQueryIntegrationTests'
dotnet test tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj -c Release -f net48 --no-restore --no-build --filter 'FullyQualifiedName~ConnectionAndUnitOfWorkTests|FullyQualifiedName~PersistenceTransactionTests|FullyQualifiedName~LinqToDbConnectionTests|FullyQualifiedName~UnitOfWorkExceptionalCleanupTests|FullyQualifiedName~LinqToDbMappingTests|FullyQualifiedName~DirectSalesMappingTests|FullyQualifiedName~RepositoryLifetimeTests|FullyQualifiedName~RepositoryAndQueryIntegrationTests' --logger 'trx;LogFileName=t03-final-foundation.trx' --results-directory TestResults/sal-t03-final-foundation
dotnet restore tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj -c Release -f net48 --no-restore --list-tests
dotnet test tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj -c Release -f net48 --no-restore --no-build --logger 'trx;LogFileName=t03-final-architecture.trx' --results-directory TestResults/sal-t03-final-architecture
```

## Original failures and review disposition

| Retained trial | Actual outcome and interpretation |
|---|---|
| Initial Application behavior RED |34 executed:6 PASS/28 FAIL/0skip. Existing permission pipeline passed; validation/detail behavior was absent in approved compile-only scaffold returning SalesReceipt.NotImplemented. Compilation/type existence/empty discovery were not RED.|
| Initial Draft SQL RED |20 executed:0 PASS/20 FAIL/0skip, actual Create/identity/persistence expectations failed at NotImplemented. Each owned allocation was disposed; scaffold is fully replaced.|
| First Application GREEN |34/34 PASS/0skip.|
| First SQL GREEN attempt |20 executed:18 PASS/2 FAIL/0skip. Two new oracle arrangements used unbracketed reserved LineNo; test SQL corrected without product change. Original run is not renamed 20 PASS.|
| Mapping repair compilation |xUnit2031 Where-before-Assert.Single compilation failure; corrected test overload only. Not behavior RED and no test-result claim.|
| Focused SQL/mapping repair |6/6 PASS/0skip, historical detail/ownership/canonical order/mapping.|
| Incremental boundaries/detail read |4/4 and1/1 PASS/0skip: competing Save/cancel, Unit blocking,200-row reverse and one-connection consistent detail cancellation. Later green-only cases are not original RED.|
| Format tooling |Scoped whitespace formatting emitted a generic workspace-load warning before freeze; retained locally. Final actual compiler/test diagnostics are authoritative.|

Application RED TRX SHA256 `37523A46077BFB11964A18675DF75613C9A9DADB89014AAA3F71A493D8D2B10F`; SQL RED `458815BE1A02301CE20D17A441EAA1B7C37504C5E6161A130AFE24A3A92C3DE7`; first SQL GREEN attempt `20D63A42584EEF25751BE9CEE16DC8AB29C9C7646386CC5A263506EC08C82B1B`; focused repair `8C0A62B6DE584B8D653FCA25529713F8D2DD334B39695B5C9F8CE5F7C5D0A7BC`. Original logs/TRX and exact local trial index remain retained, unpublished. No T03 finding was waived/deferred; same independent reviewer approved source quality then independently verified final 11 source / 51 binary hashes and all four actual discovery/TRX sets.

## Acceptance and resource limits

A01 and A21 PASS only at exact reviewed source/test `1164199bb7c7500c61fe41ed21eabb58c7b3a2a7`, executed/reviewed 2026-10-22, under this environment. A01 combines actual Create/Update boundary validation, required active-reference refusal and independent valid Draft persistence. A21 combines server recomputation/immutable bounded numbering/gaps/Note checks with T00 date/JSON/allowlist tests actually rerun inside final Application 127. This does not introduce a new T03 serializer or establish complete Local/Owner/T08 acceptance.

[SAL acceptance](../../DIRECT_SALES_ACCEPTANCE.md) records only T03-owned portions for other rows: independent Draft/Get authorization, retained/new line IDs, current Unit, null Draft costs, stale/Posted update refusal and consistent historical detail. Client row identity/native UI, Post UnitChanged/cost allocation, full session uncertainty, List/keyset and frozen T08 portions remain open. Source-seeded Posted detail is not an implemented stock-decreasing sale writer.

All 26 new T03 SQL invocation allocations (25 Draft plus one mapping SQL) completed existing positive identity/token guarded disposal after actual workers/readers/transactions drained; the other mapping case is pure local. Inventory APP reports successful verify/cleanup/drop; affected Foundation completed without cleanup exception. Concurrency cleanup releases controllers, cancels/drains actual workers and quarantines uncertainty instead of blind drop. No task-owned worker/connection/process or unresolved/quarantined allocation remained at closeout. No prefix cleanup, forced drop, new harness/lease/approval tool, raw SQL identifiers or secrets are published.

T04 is IN PROGRESS per the [execution index](../../archive/20261023-pre-ui-next/plans/README.md), outside this frozen T03 source. Sales Post, coherent deployment, List/keyset, Local/UI, frozen six-owner fresh/reuse/package evidence and explicit Owner disposition remain open. T01 deferred R3 generic bootstrap diagnostics, SQL-login-only exact9/trusted-administrator and inherited collation portability limits remain unchanged. Operational release prerequisites remain **UNACCEPTED / UNSCHEDULED**.
