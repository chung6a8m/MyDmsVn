# HTTP typed JSON boundary refactor evidence — 2026-11-16

## Scope and candidate

- Owner request: Sales-only HTTP typed DTO/shared JSON boundary refactor; no contract or runtime behavior change.
- Baseline: `main` / `origin/main` `00aca21b56df46befa166ee64bce0f6dbb36be06` after fetch.
- Source candidate: `d1997dcaa3b9b40a9d17932a5d39d835a88afe90` on `codex/http-typed-json-boundary` (includes required C# BOM/CRLF normalization).
- Target: `net48`; ASP.NET Web API 2, OWIN/Katana and Newtonsoft.Json unchanged.
- Environment: Windows 11 Home Single Language, `Microsoft Windows NT 10.0.26300.0`, .NET SDK `8.0.428`, VSTest `17.11.2` x64.
- SQL, IIS/IIS Express, browser, frontend and production deployment: **NOT RUN / NOT IN SCOPE**. No release or resend-enable claim is made.

## Baseline

Initial `--no-restore` execution was blocked by missing `tests/MyDmsVn.Server.HttpHost.Tests/obj/project.assets.json` (`NETSDK1004`). The checkout was restored with locked dependencies; no package version changed.

```powershell
dotnet restore tests\MyDmsVn.Server.HttpHost.Tests\MyDmsVn.Server.HttpHost.Tests.csproj --locked-mode
dotnet test tests\MyDmsVn.Server.HttpHost.Tests\MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --no-restore --logger "trx;LogFileName=http-typed-json-baseline.trx"
```

Actual baseline: 1 assembly discovered, 636 executed, 636 passed, 0 failed, 0 skipped. TRX: `tests/MyDmsVn.Server.HttpHost.Tests/TestResults/http-typed-json-baseline.trx` (local evidence; not committed).

## TDD and focused verification

Each new behavior test was observed RED before production implementation:

| Test | RED reason | GREEN boundary |
|---|---|---|
| `Shared_strict_boundary_returns_typed_value_and_preserves_the_validated_source` | `StrictJsonRequest` did not exist | shared strict typed reader |
| `Sales_wire_decoder_returns_named_HTTP_DTOs_before_explicit_Application_mapping` | named request DTO/mapper types did not exist | Sales HTTP DTO + mapper |
| `Sales_output_mappers_return_named_HTTP_response_DTOs` | named response types did not exist | typed Sales response projections |
| `Typed_boundary_fingerprint_uses_preserved_strict_source_not_DTO_reserialization` | no typed operation-context overload | strict-source operation integration |

Focused commands/results:

- Sales + Inventory endpoint boundary selection: 199 passed, 0 failed, 0 skipped.
- `SalesWireEndpointTests`: 113 passed, 0 failed, 0 skipped after named responses.
- Sales/WebOperation/Inventory/Pipeline cross-module selection: 379 passed, 0 failed, 0 skipped.

The existing independent `fingerprint-vectors.json` canonical text and SHA-256 values were not changed. The new regression proves the typed path yields the pinned Sales Create SHA while DTO reserialization is rejected as a canonical wire input.

## Before/after responsibility evidence

| Item | Baseline | Candidate |
|---|---|---|
| Raw mutation bodies retained by `SalesReceiptsController` | Create/Update/Post each kept a `JObject` local | zero raw-body locals; typed `StrictJsonRequest<T>` only |
| Sales request ownership | decoder returned Application requests directly | HTTP DTO -> explicit mapper -> Application |
| Sales response ownership | anonymous objects | named HTTP response DTOs |
| Shared body boundary | Sales `Body` performed admission + parse | `StrictJsonRequest.Read<T>` owns admission + one strict parse |
| Fingerprint source | original strict `JObject` | same original strict `JObject` carried inside typed wrapper |
| Low-level JSON retained | strict grammar, token/lexical validation, canonical JSON | retained intentionally for exact compatibility |

## Final verification

```powershell
dotnet test tests\MyDmsVn.Server.HttpHost.Tests\MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --no-restore --logger "trx;LogFileName=http-typed-json-final.trx"
dotnet build src\MyDmsVn.Server.HttpHost\MyDmsVn.Server.HttpHost.csproj -c Release -f net48 --no-restore
```

- Full SQL-free HTTP host suite on the source candidate: 1 assembly discovered, 640 executed, 640 passed, 0 failed, 0 skipped; duration 10 seconds. TRX: `tests/MyDmsVn.Server.HttpHost.Tests/TestResults/http-typed-json-final.trx` (local evidence; not committed).
- HTTP host build: succeeded, 0 warnings, 0 errors.

Required interpretation remains: these are source/SQL-free HTTP host results only; SQL/IIS/browser categories above remain NOT RUN.

## Limits and findings

- The compatibility `JObject` operation overload remains for Inventory, which was explicitly excluded from bulk migration. Sales uses the typed overload.
- Default Web API formatter/model binding was not adopted because it does not independently prove duplicate/unknown property rejection, token/lexical exactness, stable field paths and fingerprint source equivalence.
- This refactor does not change WEB6 acceptance status, enable resend, authorize automatic mutation retries, approve staging/production, or modify historical evidence/quarantines.
