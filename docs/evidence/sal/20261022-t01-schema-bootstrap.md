# SAL-T01 — schema10, validated bootstrap and reset checkpoint

**Executed/reviewed:** 2026-10-22. **Disposition:** T01-owned schema/bootstrap/admission/grants/mappings/reset checkpoint COMPLETE; independent task review approved spec compliance and quality. R1/R2 were addressed; R3 remains a deferred Minor. Full SAL, packaged upgrade/startup, Owner and production acceptance remain open.

## Candidate and environment

- Product/test/runbook commit: `81e8aab965e1eebd7b2faa40c05182f6ef8cb8eb`,26 files, from review base `707c522b04b29ed4558f057699a614ba0c5d1847`. T00 foundations remain `b5a2f3f059b301e26e01a83bd2a26490ac60dce9`; planning/shipped pre-SAL baseline remains `35ba2e8a3f37fb07095701eb94f4b56f7941c491` / schema9.
- Frozen26-file working-byte source manifest SHA256: `D0AD2FCACEA403C1B2B779903D430ECCC4E0F06773314E5135C755C5C410D4D0`. Git normalizes CRLF to LF; this hash identifies the recorded working-byte manifest, not a Git tree hash. Committed files match after that normalization.
- Script010 final working-byte SHA256: `2D1076EF11A4C17B60F8383341E9EA97D1163D41B2A019DDFEBDE25DB569787F`; final provisioning SQL: `153FA5D3DBEAC36F54BA653382FFFC0192C774D8BE3A5BCAB99ED1C59A3AA43E`.
- Windows11 `10.0.26200`, .NET Framework Release `533509`, SDK `8.0.425`, net48/Release, VSTest `17.11.1` x64. SQL Server `14.0.2130.4`, Developer Edition64; owned disposable database collation `SQL_Latin1_General_CP1_CI_AS`.
- Vendor pins unchanged: BootstrapSourceGrid `2bd7874f7e80b22ef181763749c2a2938ab7f16e`, Bootstrap5WinFormUI `2be9a4d778fda7016207b75470020be6126f2f3f`, SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`. Scripts001–009, dependency locks and net48 target graph remain unchanged.
- No package, GUI, actual Login or installed-host execution. T02 writer integration is a separate in-progress task; it is not included in this frozen T01 evidence.

## Implemented and verified boundary

Script010 proves existing Posted Goods Receipt lines, ledger identities/document/line keys/quantities/audits and complete balance keys in both directions before bootstrap. It requires positive supported receipt movements and valid source cardinality; each exact typed purchase product is rounded to four places before widened summation. Checked quantity/value conversion rejects overflow. Historical ledger/source rows and audit bytes remain unchanged; legitimate zero costs receive real zero-valued companions, never missing-value defaults.

DbUp owns script010 DDL, companion backfill, InventoryValue and its journal entry in the same per-script transaction. Independent before/after SQL snapshots and four injected stages (before/during/after bootstrap and journal insertion) establish rollback to intact9 and successful rerun. Earlier incomplete fresh001–009 prefixes remain recovery-required, not automatically admitted.

Schema10 adds SalesReceipts/Lines and a separate bounded noncycling sequence; duplicate Products remain independent, with unique `(SaleId,LineNo)` and a nonunique `(ProductId,SaleId)` membership index. Storage includes Unit/amount/precision and paired nullable cost checks; full Draft/Posted cross-table lifecycle and immutable sales snapshots remain T03/T04. StockValuationEntries is keyed/FK by LedgerId and append-only. Balances require nonnegative quantity/value and zero quantity implies zero value. Explicit LINQ mappings, provisioning/startup DML allowlists and the19-table/two-trigger/two-sequence reset boundary agree.

Runtime requires complete schema10. Migrator admits Empty/exact10 and separately artifact-validated exact9; older prefixes, partial/unknown/future history, altered definitions and unmanaged objects fail closed. Definition signatures exclude current identity/sequence values and cache policy while preserving semantic sequence bounds/increment/cycling. Actual cache-only9/10 cases and semantic-drift negatives retain the existing contract.

Exact9 admission requires pre-existing RESTRICTED_USER, explicit metadata/DMV visibility, supported mapped SQL-login identity inventory and positively drained sessions/requests/transactions/locks. Only the exact current DMV observation transaction under `@@TRANCOUNT=0` is excluded; unrelated current-session transactions are not blanket-exempt. A real pre-existing master SQL writer/reconnect case is covered. Migrator does not KILL clients, change mode/credentials or reopen MULTI_USER; restriction remains after success/failure. Maintenance identity and privileged administrators remain trusted. Windows/group/external/contained and other unsupported identity paths require separately designed/evidenced admission; this is not universal alternate-credential closure.

Reset restores each append-only protection independently on an ownership-verified bounded connection after the reset connection closes/drains. A damaged trigger cannot skip restoration of the other; uncertainty poisons admission. Fresh and Reuse run equivalent complete Sales/value contamination; journal/ownership, reseeded identities, both sequences and zero orphan companion are observed. Lifecycle tests retain exclusivity, worker drain, quarantine and independent cleanup semantics. Login-test cleanup tracks identity before CREATE and retains original/login/database errors while still attempting independent database cleanup.

## Final execution and encoding boundary

The full Migrations run preceded restoration of UTF-8 BOM on exactly two SQL files: provisioning and010. No semantic source/test change followed that run. Decoded SQL hashes and the actual pinned `DbUp.ScriptProviders.EmbeddedScriptProvider.GetScripts(null).Contents` hashes for all10 resources are identical before/after final rebuild. The two provider manifests share SHA256 `77DAB979FD70C0C7A7283791C4B887D9A77FE699A96DFCF19419C91D40FC96AA`.

Decoded/provider010 SHA256 is `5231AD0DCC66CAB7BD773935C7F97AAB1D3E3FCE7BEBF9D547D206EC8FA494FB`; decoded provisioning SHA256 is `CD7742DE2C7008919CA5877DF0E4DE4D41F10D511267F8DCD0514AA83910B81E`. Independent final review checked the26 source hashes,12 evidence artifacts and10 binary-manifest entries, the two-only BOM delta, provider equality and actual TRX. The scoped encoding ruling accepts focused rebuilt smoke without relabeling the full pre-BOM run as final bytes.

| Invocation / exact boundary | Discovered/selected/executed | PASS | FAIL | SKIP | Duration |
|---|---:|---:|---:|---:|---|
| Full Migrations, pre-BOM frozen source/binary |141|141|0|0|9m22s|
| Rebuilt final bytes, fresh install smoke |1|1|0|0|6s|
| Rebuilt final bytes, corrected exact9 upgrade/no-op selector |1|1|0|0|12s|
| Rebuilt final bytes, SqlPrincipalProvisioningTests |14|14|0|0|57s|
| Rebuilt final bytes, reset/lifecycle/mappings |34|34|0|0|6m2s|

Migrations141 reconcile to DatabaseMigration24, DirectSalesSchemaCompatibility34, DirectSalesBootstrap33, SchemaCompatibilityIntegration24, DirectSalesConstraint1, DirectSalesMaintenance7, DatabaseMigratorCommand4, SchemaCompatibilityPolicy8, DirectSalesMigration1, SecurityBaselineMigration2, SuperuserMaintenanceCommand2 and PermissionCatalogCommand1. This includes Facts and actual Theory rows, not141 claims of SQL-only cases. Foundation34 = reset14 + lifecycle17 + shared mappings2 + Sales mappings1. Runs overlap; no additive unique total is claimed. All five final TRX summaries are Completed with no non-Passed rows or RunInfo entries.

The first final-byte smoke filter included unmatched `FullyQualifiedName~Exact9_is_admitted`; actual discovery/execution was only the fresh row. The corrected exact9 run executed exactly `Exact9_runtime_refuses_and_restricted_admin_upgrade_preserves_mode_and_noop`. It is not counted as two cases in the first run.

Commands ran from the candidate checkout with successful locked restores/builds before `--no-restore`/`--no-build`; final discoveries used the same selectors and actual rebuilt outputs. Owners ran sequentially against designated nonproduction effective-master configuration.

```powershell
dotnet test tests/MyDmsVn.Server.Infrastructure.Migrations.Tests -c Release --no-build --no-restore --logger "trx;LogFileName=t01-migrations-final3.trx" --results-directory .local/t01-results

dotnet test tests/MyDmsVn.Server.Infrastructure.Migrations.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~DirectSalesMigrationTests|FullyQualifiedName~Exact9_is_admitted" --logger "trx;LogFileName=t01-finalbytes-smoke.trx" --results-directory .local/t01-results

dotnet test tests/MyDmsVn.Server.Infrastructure.Migrations.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~Exact9_runtime_refuses_and_restricted_admin_upgrade_preserves_mode_and_noop" --logger "trx;LogFileName=t01-finalbytes-exact9-smoke.trx" --results-directory .local/t01-results

dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~SqlPrincipalProvisioningTests" --logger "trx;LogFileName=t01-security-finalbytes.trx" --results-directory .local/t01-results

dotnet test tests/MyDmsVn.Server.Infrastructure.Foundation.Tests -c Release --no-build --no-restore --filter "FullyQualifiedName~ReusableSqlFixture|FullyQualifiedName~LinqToDbMappingTests|FullyQualifiedName~DirectSalesMappingTests" --logger "trx;LogFileName=t01-foundation-finalbytes.trx" --results-directory .local/t01-results
```

Finalbytes Migrations test DLL SHA256 `170EAFB49193E6156AE31ADD5C9BC304A74FA1D34DBC0A9075365B56FCF67CF5`; Security test DLL `9388A8B4872EB49BD9B81BAD074C6E3BFA2220DD3113D436AF1A685B376A3988`; Foundation test DLL `198EB4EF798EE80FE5223DD47496D2997546663584AB7E6D28542E7928D4FDBC`. All consumed final DbMigrator bytes `7452ABBF8C13CE9CDCEF4BCB7093984D05AC7DC4C069305559E78DB32430377A`, Infrastructure `B05CE170C2F4EB43E20844B30D76A639C3F9413B44ED89C722D4FBF9A33A7B1D`; pinned DbUp core `96C392A421DE4B25581EDD7B030AF7EA0B802D840C292A575512BFAFE0F5C3F3`.

| Local TRX retained; raw contents not published | SHA256 |
|---|---|
| t01-migrations-final3.trx (pre-BOM) | `79536C33121D2E2E0214663A59185D8695230AA325D0A8BE457C7D851B1FEABB` |
| t01-finalbytes-smoke.trx | `33EA41B7C2E6CAEDCC1831DD27865A8313283FD7B5C5872F9E32CDA4839D12BC` |
| t01-finalbytes-exact9-smoke.trx | `36B172DDB0BA44EF6705B356C88F3788DE5D5AA054CF6529E8BCEBD0FF3415AA` |
| t01-security-finalbytes.trx | `84F127615E0EF88887364C3533A540C9F035960604FC98CEB1B53DA1B870F4B0` |
| t01-foundation-finalbytes.trx | `B4641E02398674DB2B2520C1A6F23A91EF698EE939BD510358FBE97DC0F36B67` |

## Original failures, repairs and review

| Retained development invocation | Actual outcome and disposition |
|---|---|
| Fresh schema RED |1 FAIL: expected3 Sales/value tables, actual0. Initial NETSDK1004 was setup failure, not behavior RED.|
| Fresh implementation trial |1 FAIL: SQL batch compilation for new InventoryValue and accidental PostedUnitCost rename; GO/column repair led to1 PASS.|
| Runtime10 RED / fingerprint captures |1 intended runtime refusal FAIL. Separate sql_variant/collation/PENDING captures were diagnostics, not PASS or behavior RED.|
| Exact9 admission RED / intermediate |2 intended FAIL; intermediate2 PASS/1 FAIL and DMV diagnostic1 FAIL. Exact-current implicit observation transaction was corrected; original errors retained.|
| Reset RED / intermediate |13 FAIL:12 stale16-table boundary failures and1 unquoted LineNo seed error. Static-call compile failure and later7 PASS/6 seed failures remain distinct. A later14-failure run hit evolving checker scalar metadata; fixed by its owner. Development reset14/lifecycle17 passed before the final combined rerun.|
| Grants / mapping |Grants RED1 FAIL/1 PASS exposed missing Sales sequence UPDATE; syntax trial1 FAIL/1 PASS; repaired2 PASS then14 PASS. Mapping RED1 FAIL expected dbo schema, actual null; repaired1 PASS.|
| DirectSales development |54 executed:48 PASS/6 FAIL. Unit whitespace,201-line legacy receipt and exact-current DMV issues were repaired; restricted-visibility fixture admission failure remained a separate setup boundary.|
| Required index / cost pair / strict9 inventory |Two meaningful index/cost-pair REDs and three strict9 inventory REDs; separate fingerprint capture not behavioral proof. Focused final definitions/checks6 PASS.|
| Full Migrations final1 |129 executed:128 PASS/1 FAIL,0skip. Existing Catalog NUL wrong-module fixture could not ALTER its function because010 added two dependent checks. Repair captured/dropped/restored/asserted exact definitions; focused1 PASS preserved its intended drift test.|
| Review R1/R2 RED |5 executed,5 FAIL: cache-only compatibility4 and independent cleanup1.|
| Review hash capture / R2 green |4 executed:2 cleanup PASS/2 expected old-fingerprint diagnostic FAIL after cache-signature correction.|
| Review R1/R2 green |15 executed,15 PASS: cache4, semantic sequence6, cleanup2, fresh/frozen fingerprints2 and real restricted writer/reconnect1.|

R1 sequence cache behavior and R2 independent owned login/database cleanup are addressed. **R3 generic bootstrap error diagnostics lack counts/safe identifiers: DEFERRED Minor for later whole-branch triage.** Rejection and rollback are verified; the diagnostic requirement is not declared satisfied. The inherited hardcoded column-collation portability limitation remains; this evidence does not certify another collation.

## Resources and acceptance limits

All SQL scenarios use existing generated positively owned `SqlTestDatabase` allocations with instance/allocation/token checks; no application database, manual prefix drop or secret is part of this evidence. Owners ran sequentially, workers/connections drained and guarded cleanup completed. The final environment observer also used CreateAsync/VerifyOwnershipAsync/DisposeAsync and verified owned teardown. Injected login-cleanup uncertainty was positively reverified and recovered by the test's independent safety cleanup. No outstanding cleanup failure or quarantined allocation remains. Raw connections, host/database/login/token identifiers, TRX and local manifests stay local.

[SAL acceptance](../../DIRECT_SALES_ACCEPTANCE.md) closes A10–A12 against this T01 scope. T01-owned portions of A09/A13–A16 have evidence; their full T08/package/coherent-writer portions stay open. Paired nullable storage does not establish the later cross-table Draft/Posted lifecycle. This checkpoint does not certify already-running old binaries, another collation or unsupported identity model.

T02 is IN PROGRESS in the [serial plan](../../plans/20261021-002-direct-sales-and-moving-average-costing.md); its work is outside this frozen source. Receipt/sale/reconciliation must cut over together before schema10 can be deployed. Sales services/Post/query, Local/session/UI, frozen six-owner fresh/reuse comparisons, actual Login, packaged startup/upgrade and explicit Owner acceptance remain later obligations. Operational release prerequisites remain **UNACCEPTED / UNSCHEDULED**.
