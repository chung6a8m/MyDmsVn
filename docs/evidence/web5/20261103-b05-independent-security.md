# WEB5 B05 — independent direct-host security verification (2026-11-03)

**Status: VERIFIED SQL-FREE ON SOURCE CANDIDATE / B05 STILL IN PROGRESS AND NOT ACCEPTED.** This record covers only independent direct-host contract tests that do not require an admitted IIS Express listener. It does not claim IIS Express/full-IIS execution, host ownership, old/new cutover, workload, SQL oracle, audit destination/retention, multiworker behavior, service recovery, staging acceptance or production approval.

## Candidate and environment

- Base: merged backend `main` `6f51dfffb38a58ecd380efe61dd2fbc89647bc58` (PR #92).
- Test candidate: `e1804769c3406d871c034dd19b855d96bb1d88be`, tree `4e6b18a505d62665147f03caf1d32eb063cadd79`.
- Test file SHA-256: `PipelineTests.cs` `03e4587219a0242c2070983cdae0f873067aab8899a78baf6ec1396864189592`; `SpaAssetsTests.cs` `4b19c1ed803bf44d57878a4c8b936bdf439d045d6abba13d5a7b7c593d7d8bf5`.
- Windows reported `Microsoft Windows NT 10.0.26300.0`; `Get-ComputerInfo` reported Windows 10 Home Single Language, version 2009, build 26300. .NET SDK `8.0.427`; tests target `net48` Release.
- No SQL connection, browser, IIS/IIS Express listener, attended session, proxy, external endpoint or quarantined resource was used.

## Scope and results

The candidate adds behavior-level regression coverage only; production source is unchanged. The API pipeline now has explicit cases for RFC `Forwarded`, `X-Forwarded-For` and `X-Forwarded-Proto`. The SPA asset boundary separately verifies rejection of wrong scheme, wrong host, RFC `Forwarded` and `X-Forwarded-For`, and pins HSTS on HTML. These tests exercise real OWIN pipeline/assets code through `Microsoft.Owin.Testing`; they do not emulate an owned IIS Express request queue.

| Run | Result |
|---|---|
| Starting-main focused baseline: `AccessLifetimeTests`, `AuditTests`, `AuthEndpointTests`, `PipelineTests`, `SpaAssetsTests` | **139/139 PASS**, 0 failed/skipped: mode/lifetime 17, audit sink 1, JWT/cookie/CSRF/Origin/limiter/auth audit 62, pipeline 52, SPA assets 7 |
| Candidate focused pipeline + SPA | **65/65 PASS**, 0 failed/skipped: pipeline 54, SPA assets 11 |
| Deliberate mutation check: temporarily remove exact RFC `Forwarded` rejection from API and SPA production branches | Expected **RED**: 2 failed, 13 passed. Both RFC `Forwarded` cases observed 200 instead of required 400. Mutation was not committed; production files were restored and verified clean before the final run. |
| Complete `MyDmsVn.Server.HttpHost.Tests` project on candidate | **554/554 PASS**, 0 failed/skipped |

Commands:

~~~powershell
dotnet restore tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj --locked-mode

dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --no-restore `
  --filter "FullyQualifiedName~PipelineTests|FullyQualifiedName~SpaAssetsTests|FullyQualifiedName~AuthEndpointTests|FullyQualifiedName~AccessLifetimeTests|FullyQualifiedName~AuditTests"

dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --no-restore
~~~

The local TRX files remain under task-specific `TestResults/web5-b05-*` directories and are not committed because they contain detailed machine/test metadata.

## Gate disposition

- The direct-host SQL-free boundary verifies actual source behavior for JWT/cookie/CSRF/Origin, CSP/HSTS, exact operation-mode admission, auth audit and per-worker limiter cases in the test host. It also demonstrates that both RFC and `X-Forwarded-*` authority are rejected rather than trusted in the no-proxy topology.
- Trusted reverse-proxy deployment/topology testing is `NOT_APPLICABLE` because the selected topology has no reverse proxy. This does not turn the direct-host security controls above into N/A.
- Full IIS and unperformed Narrator steps remain `OWNER_ACCEPTED_EXCEPTION / NOT RUN`; none was executed here.
- Owned IIS Express host admission is still unproven. Workload and old/new cutover remain `BLOCKED / NOT RUN`; PID 4, a foreign listener, a socket snapshot or an unverified application challenge is not accepted as ownership proof.
- The two historically quarantined database/private-manifest allocations were not queried, adopted or removed. Cleanup remains `BLOCKED` pending exact ownership, stable quiescence and valid authority.
- No SQL oracle, audit retention/destination rehearsal, application backup/recovery, service RPO/RTO, B03 operator rehearsal or B06 release decision was produced. B05 remains `IN PROGRESS / NOT ACCEPTED`; staging is not accepted and production remains `NO-GO / DEFERRED`.
- Same-key resend remains disabled. No WEB4 contract, SQL operation fence or UNKNOWN protection changed.

Independent P0/P1/P2 review and delivery-PR exact-head CI are recorded separately before merge; this local result does not predict hosted CI.
