# WEB4 B04 — HTTP operation APIs

Date: 2026-11-02, Asia/Bangkok. **VERIFIED CANDIDATE; Owner acceptance/merge pending.** Owner explicitly selected B04 in this session as the prerequisite for frontend milestone WEB4-04 (F02/F03). [Owning plan](../../plans/20261101-003-web4-reliability-server.md), [execution checklist](../../development/WEB4_B04_IMPLEMENTATION_CHECKLIST.md), [canonical contract](../../contracts/web4-v1/README.md).

## Source and scope

Backend base `8f8091575bbdab4d85c97f14d44b82030d70927d` (PR #82 merged). B03 source `e2976715bb106cc22c702e60ca2dd4536d6894ae`. **B04 runtime/test source `bd871518fd162927d938babd5535a2ddf91c2ad5`**, branch `codex/web4-b04-http`; later evidence commits do not change that executable source. Web base `68398accdd03a1ed78a2e3bec7c5cb2499457b6d`. Canonical B01 remains `1c603c6ac57e59a3f7b4bc6ca976b2d531c079aa`, manifest SHA256 `0acab190e7fe126d53c13c0510a1958365e5a6e1fda78a4321da78297e0b3f1a`. Corpus/schema/dependencies are unchanged.

Implementation targets six keyed commands, original allowlisted result codec, protected authenticated context/status, current SQL permission/epoch fencing and explicit Web11/Web4 deployment mode. Production activation, browser durable draft acceptance and B05 pilot are separate. No automatic retry, negative absence-based settlement, broad grant or business valuation rewrite is authorized.

## Development verification ledger

- Baseline locked HttpHost.Tests restore and Release/net48 run: 415 passed, 0 failed/skipped, before B04 runtime changes. Local Windows SDK selected by global.json; raw TRX stays local.
- Mode RED: 5 expected failures because baseline accepted missing/unknown operation mode. After implementation, mode/lifetime selected 16 pass.
- Endpoint RED: 18 expected failures for six key admission paths, six operation/session metadata paths, legacy keyed-write rejection and five protocol-error mappings. No SQL proof from this test adapter.
- Codec RED: 60 expected failures against initial stubs. Codec GREEN: 60 passed, 0 failed/skipped against six original envelopes and canonical vectors. A duplicate-header test was corrected to construct both incoming values before first header parsing; production rejects the actual duplicate request.
- SQL readback RED: 1 expected failure against the read repository stub; owned fixture teardown completed. First read/journal run: 46 rows, 40 passed and six new fixture seed failures (missing explicit RolePermissions column list). The corrected six-kind selector passed 6/6; no production change was needed for that correction. All 24 read rows and 22 journal rows then had passing evidence; the final combined run below rechecks the final source.
- Status HTTP RED: five invalid-path/body/header cases failed with the expected missing-route 404; the legacy 404 case already passed (6 total). After route implementation, status cases pass.
- Endpoint integration correction: the capture-only fixture initially inherited AuthFixture's Read/Sales-Post-only grants (10 failures); it now explicitly supplies the four required mutation permissions. The first receipt permission literals were corrected to the existing PermissionKeys constants (three remaining fixture failures). Final mode/admission/status selection: 40 passed, 0 failed/skipped. No production authorization was weakened.
- HTTP/SQL RED: the first authenticated context request returned 404 (1 expected failure). Before the replay-identity correction, three valid-shape/wrong-document-ID cases returned incorrect replay200 instead of503, while status already returned RESULT_UNAVAILABLE/null. The other 13 initial HTTP/SQL rows gave 11 pass and two new test-oracle syntax failures: ORDER BY LineNo needed the existing bracketed [LineNo] spelling. Corrected only the oracle; assertions after those failure points were not claimed as executed.
- Independent review identified the replay result-ID issue above. The correction carries immutable SQL ResultDocumentId alongside bytes through WebOperationReplay; Application binds the decoded DTO kind/ID to that result and the Update/Post target before Commit. No SQL JSON or later Get is used. A second independent review found no unresolved P1/P2 in the runtime diff. This is a reviewed internal B03 journal return-type correction for B04; the frozen wire corpus and SQL schema are unchanged.
- Final SQL-free source: HttpHost.Tests Release/net48 **506 passed, 0 failed/skipped**; selected Application WebOperationContextTests **10 passed, 0 failed/skipped** (six new rows each exercise matching ID, mismatched ID and wrong DTO type). An initial OperationsController compile failed for a missing Application namespace import; it was corrected before these runs.

## Final candidate verification commands

Windows `10.0.26300.0`; .NET SDK `8.0.427` selected by the unchanged global.json; installed Framework release `534296`; target `net48`, Release; VSTest17.11.1 x64. No browser is involved in these backend tests. SQL uses the existing explicitly configured nonproduction master connection and ownership-checked fresh fixtures; connection strings, tokens and database identities are omitted. Each SQL process runs sequentially, with tracked workers drained before positively owned teardown. No global runner or prefix-based deletion was added.

Locked restores for HttpHost.Tests, Application.Tests, Security.Tests and Inventory.Tests succeeded before their rebuilt runs. Commands from the backend repository root:

```powershell
dotnet restore tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --no-restore --logger 'trx;LogFileName=host-final.trx' --results-directory TestResults/web4-b04-final
dotnet restore tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj -c Release -f net48 --no-restore --filter FullyQualifiedName~WebOperationContextTests --logger 'trx;LogFileName=application-final.trx' --results-directory TestResults/web4-b04-final
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --filter 'FullyQualifiedName~WebOperationReadTests|FullyQualifiedName~WebOperationJournalTests|FullyQualifiedName~WebOperationHttpSqlTests|FullyQualifiedName~WebAuthHttpSqlTests|FullyQualifiedName~WebSalesHttpSqlTests|FullyQualifiedName~WebGoodsReceiptHttpSqlTests' --logger 'trx;LogFileName=security-final.trx' --results-directory TestResults/web4-b04-final
dotnet restore tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj -c Release -f net48 --no-restore --filter FullyQualifiedName~WebOperationCommandTests --logger 'trx;LogFileName=inventory-final.trx' --results-directory TestResults/web4-b04-final
```

| Final owner/scope | Executed | Passed | Failed/skipped |
|---|---:|---:|---:|
| HttpHost.Tests complete SQL-free owner | 506 | 506 | 0/0 |
| Application WebOperationContextTests | 10 | 10 | 0/0 |
| Security selected real SQL/HTTP | 81 | 81 | 0/0 |
| Inventory WebOperationCommandTests | 7 | 7 | 0/0 |
| Total selected final rows | **604** | **604** | **0/0** |

Security breakdown confirmed from TRX: new read24, journal regression22, new HTTP16, legacy Auth7/Sales7/Receipt5. The final run includes all corrected fixture statements and the three previously RED result-ID cases. Inventory's seven rows include all six Local commands without Web metadata on exact schema10, all six Web12 capture/replay commands, precommit codec/oversize/journal rollback and the maximal Sales source shape. All owned fixtures drained workers and completed teardown without cleanup exceptions; no unresolved allocation was reported. Raw TRX/logs remain local and are not committed; early scratch artifacts were moved intact to the OS temporary directory.

Static checks: git diff --check passed; changed C# follows BOM/CRLF and Markdown no-BOM/CRLF working-tree policy; current documentation links/anchors resolve. The B01 manifest hash above was independently checked, and canonical corpora, migrations/deployment SQL, dependency locks, Desktop/CEX source and historical WEB1–03/B03 evidence are unchanged. The only migrator edit corrects help text from schema11 to the already implemented schema12 --web target.

## Scope and remaining gates

The HTTP/SQL fixtures use actual OWIN HTTP routing, strict codecs, JWT/SQL sessions, Application handlers, restricted runtime permissions and independent SQL source/ledger/value/COGS assertions. TestServer restart recreates the HTTP host and service provider against the same database. Acknowledgement suppression means the test retains the original response only as an oracle while a consumer recovers through status. Neither proves operating-system process death or a real socket loss at Commit.

No B05/F04 production React + IIS Express HTTPS/SQL/Chromium/Edge run, attended IME/Narrator, full IIS, production activation, retention/reconciliation procedure or real process crash was performed. Explicit same-key resend UI remains gated until the specified crash/restart evidence exists; absence/404/timeout never proves rollback. No broader Owner acceptance or merge is implied. Hosted CI at the final candidate is NOT RUN at this checkpoint. Frontend F02/F03 must pin the final B04 implementation before consuming it; B05/B06 are not selected by this prerequisite work.
