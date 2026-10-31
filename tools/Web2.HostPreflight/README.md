# WEB2-01 isolated net48 dependency probe

This standalone test project exercises the candidate in [ADR 0012](../../docs/adr/0012-web2-net48-host-preflight.md). It has no product project references and is deliberately absent from shipped solutions and the eleven active test projects. It starts only an in-memory Katana test server; it never opens SQL, starts IIS or launches a browser. Exact direct ranges and packages.lock.json own this candidate without changing repository central pins.

Run from repository root on Windows with .NET Framework 4.8 targeting/runtime support and the SDK selected by global.json. Execute sequentially and stop after a nonzero exit:

```powershell
$project = 'tools/Web2.HostPreflight/Web2.HostPreflight.csproj'
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Preflight locked restore failed.' }
dotnet build $project -c Release -f net48 --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Preflight build failed.' }
dotnet test $project -c Release -f net48 --no-restore --no-build `
  --logger 'trx;LogFileName=host-preflight-final.trx' `
  --results-directory TestResults/web2-01-preflight
if ($LASTEXITCODE -ne 0) { throw 'Preflight tests failed.' }
```

Expected current scope: 24 executed rows (14 dependency/JWT/cookie cases plus 10 wire numeric vectors), no SQL skips. Inspect actual TRX; discovery is not execution. JWT keys and GUIDs are synthetic in-memory test values, never printed. The ten JWT negatives cover wrong key/algorithm/issuer/audience, expiry, future nbf, missing exp, unknown/missing kid and unsigned tokens. They do not implement required sub/sid/jti/iat semantics, account/session persistence or permission validation.

The generated `bin/Release/net48/Web2.HostPreflight.dll.config` contains this probe's binding redirects. Inspect and regenerate redirects when creating the actual IIS Web.config; do not deploy the testhost configuration. SystemWebCookieManager tests use the System.Web object model in memory, not the IIS response pipeline. CookieOptions omits Max-Age; the probe deliberately records that limitation. The selected host-owned cookie writer still needs implementation and IIS/browser proof in WEB2-04.

Numeric vectors are copied from the single canonical `docs/contracts/web2-v1/numeric-vectors.json`, not duplicated here. Tests parse exact strings into Int64/Decimal under invariant culture, format at the declared scale and reserialize as JSON strings. They establish CLR/serializer capacity, not full endpoint/parser/business acceptance.

Initial exploratory results are preserved locally under TestResults/web2-01-preflight: two 14-row runs each had 11 pass / 3 fail because the test builder mutated the validation key's kid and combined append/delete in one synthetic response before looking up its first cookie. Corrected independent fixtures passed 14/14; the final corpus-inclusive scope is 24/24. No product fix was required. Full results/environment belong to WEB2 evidence, not a new test framework.
