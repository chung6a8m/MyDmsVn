# UIN-T00 — explicit duplicate Code field errors

Date: 2026-10-23. Boundary: T00 application/session contract verified; A01 native invalid presentation remains T02 / NOT RUN. This evidence does not certify SQL, package, Login, SAL acceptance or production readiness.

## Candidate and environment

Base application/source/test revision: `0da4d975a8391910c2be27be42ea493ecafce179`. The implementation is the bounded T00 commit containing this ledger. Exact tested source/test blob identities below distinguish it from the concurrently developed T01 files. No vendor changes: BSG `2bd7874f7e80b22ef181763749c2a2938ab7f16e`, nested UI `2be9a4d778fda7016207b75470020be6126f2f3f`, SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`.

Windows `10.0.26300.0`, SDK `8.0.426`, Release `net48`, VSTest `17.11.1` x64; pure application/session tests. No SQL allocation or package was required or produced. Product runtime remains Windows x86/net48; this is not native GUI evidence.

## Contract and compatibility

Only the existing catalog `*.DuplicateCode` source errors gain metadata `Field = "Code"`. Their Conflict status, existing codes and messages are unchanged. Example mapped detail: `{ "code": "Product.DuplicateCode", "message": "A product with this code already exists.", "field": "code" }`. Create/update for Product, Warehouse, Employee and Customer do not commit on this conflict and retain the attempted request. Mapper casing/root stripping is unchanged; unknown explicit fields are preserved as unknown, absent fields stay unbound, and authorization precedence suppresses all private detail. Employee user-link conflict remains unbound. No generic conflict-to-Code inference was introduced.

Catalog Save errors and unexpected failures now check the originating editor generation again inside UI dispatch. New-document/record replacement and disposal invalidate old errors. Editing a changed property clears its matching JSON field error through the shared catalog base, while unchanged assignment, unrelated fields and the operation summary remain intact. Existing editor admission and cancellation paths remain authoritative. Session teardown is represented by workspace disposal; T01 owns authenticated workspace scope routing.

## Execution

Commands used ordinary `dotnet test <project> -c Release -f net48 --filter <selector> --logger 'trx;LogFileName=<name>' --results-directory C:/MyData/2026/MyDmsVn/t00-results`.

- AT project: `tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj`; RED selector `FullyQualifiedName~Duplicate_code_create_and_update`; GREEN/final selector `FullyQualifiedName~CatalogApplicationTests|FullyQualifiedName~ApiResponseMapperTests`.
- ST project: `tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj`; RED selector `FullyQualifiedName~Delayed_save_error|FullyQualifiedName~Editing_code_clears`; GREEN/final selector `FullyQualifiedName~CatalogViewModelTests|FullyQualifiedName~DesktopViewModelBaseTests`.

Each selected row was discovered and executed; zero skips. RED is expected failure evidence, not acceptance. The initial AT draft run had missing update-handler query dependencies and a null dereference assertion; the corrected RED below failed all eight rows specifically at missing metadata. One ST build attempt was blocked by concurrently drafted T01 tests calling an unimplemented API; those tests were removed from the project before the meaningful T00 RED. An AT build attempt exposed an editing encoding error, corrected before GREEN; original Vietnamese text and BOM/CRLF were restored. No build/discovery failure is counted as RED proof.

| TRX (local only) | Discovered | Executed | Passed | Failed |
|---|---:|---:|---:|---:|
| t00-at-red-valid.trx | 8 | 8 | 0 | 8 |
| t00-at-green.trx | 41 | 41 | 41 | 0 |
| t00-at-final.trx | 41 | 41 | 41 | 0 |
| t00-st-red.trx | 5 | 5 | 0 | 5 |
| t00-st-green.trx | 34 | 34 | 34 | 0 |
| t00-st-final.trx | 38 | 38 | 38 | 0 |

AT final includes eight create/update duplicate rows, mapper casing/unbound/authorization coverage and existing catalog contracts. ST RED contains four controlled delayed failures (API/unexpected × new document/selection) and one relevant-field correction case. ST final adds four queued-dispatch cases (API/unexpected × new document/disposal), existing disposal/cancellation/selection regressions and base error handling. The final selected scope is **79/79 passed**, zero failed/skipped. Intermediate runs are recorded separately and are not added to that total.

Raw TRX stays outside source control. Final test DLL SHA256: AT `4A769ABF5F00B9F62AAC324AF79CBC9A69FE01DA65375599E7D9F1C9947C0964`; ST `FBFB8CA845B5DB48016F1A8987871B161B228539492070EBAFF4B41AC4DA00AE`. ST hash describes the final T00 invocation before subsequent T01 rebuilding.

## Exact tested T00 files

SHA256 hashes are of working-tree bytes (C# UTF-8 BOM/CRLF); Git normalizes line endings.

| File | SHA256 |
|---|---|
| `src/MyDmsVn.Server.Application/Catalog/CatalogErrors.cs` | `30ff464f53a8c86af06090c790558eb3cff7bb5f201d2fb65c9f209cd6468e6e` |
| `src/MyDmsVn.Server.Application/Catalog/ProductFeatures.cs` | `2cc9c77319664d3ebe7fcc08bb855a702c4068c025b235bc70faaa8697036bbd` |
| `src/MyDmsVn.Server.Application/Catalog/OtherCatalogRequests.cs` | `298bcdd6309c3d7a3e2c8e3c5ea32fb67b57bafdf0e0cc190e6632a393ccfc6b` |
| `src/MyDmsVn.Server.Application/Catalog/OtherCatalogFeatureHandlers.cs` | `a42c259d81eb11f436d25c74ca6c33139f7878ef237e69f8508b08abe258ebc1` |
| `src/MyDmsVn.Desktop.Application/DesktopViewModelBase.cs` | `dac5415c6a85f4987e393ade12412466c802b51f27c99f1cdc75a9f6019fc052` |
| `src/MyDmsVn.Desktop.Application/CatalogViewModel.cs` | `1868681568fc526a5f25b78f9367ffa01c8013e4dcf40314b66ac8ed2c9a127a` |
| `tests/MyDmsVn.Server.Application.Tests/CatalogApplicationTests.cs` | `634b4a57afcb7bd4c04a20e291cd07996936cfa33ba1a5ec04714ae0357ab018` |
| `tests/MyDmsVn.Server.Application.Tests/ApiResponseMapperTests.cs` | `e6e28b7fb510ac027e5d442ed8fd9287a48e616d57de2217263770fc25a0b1ca` |
| `tests/MyDmsVn.Desktop.Session.Tests/CatalogViewModelTests.cs` | `e5d57393553583805554d2a583f51593d35f4db5875c1cb4259adc8606e87414` |

## Remaining boundary

No transport DTO/schema, database, permission, vendor or business workflow changed. T02 must prove visual invalid/readable targeting and native editor behavior. T01 must prove workspace/session feedback scope; this task proves catalog error freshness through existing editor lifetime/disposal. No broader test matrix or archived acceptance was inherited. The active roadmap status update is owned by the integrating T00/T01 handoff.
