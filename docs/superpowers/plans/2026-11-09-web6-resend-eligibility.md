# WEB6 Resend Eligibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the default-off `web6-resend-eligibility-v1` backend contract with a server-signed pre-dispatch receipt and authoritative read-only eligibility decision.

**Architecture:** Add a fail-closed v3 HTTP boundary without changing frozen v1/v2 mutations or payloads. A domain-separated JWS receipt proves the server observed the immutable original identity before dispatch; a dedicated SQL read repository rechecks current actor/session/epoch/original grant and protected journal state before the controller returns `ALLOW`, `DENY`, or `INDETERMINATE`.

**Tech Stack:** C# / net48, ASP.NET Web API 2 + OWIN, System.IdentityModel.Tokens.Jwt 8.23.0, Newtonsoft.Json 13.0.4, native SqlClient repositories, xUnit 2.9.3.

**Spec:** `docs/superpowers/specs/2026-11-09-web6-resend-eligibility-design.md`

## Global Constraints

- Keep `/api/v1` WEB4 and `/api/v2` WEB5 contracts and their frozen corpora byte-identical.
- `MYDMSVN_WEB6_RESEND_ELIGIBILITY` defaults to disabled; enabled value is exactly `receipt-v1` and requires exact `web5` mode plus a valid candidate ID.
- Receipt lifetime is exactly 604800 seconds from server time; browser time is never authoritative.
- No business mutation route accepts or requires the receipt; no privileged resend mutation endpoint is added.
- No automatic mutation retry on 401, timeout, 5xx, reload, timer, service worker or refresh.
- A valid receipt plus current authority can only produce an advisory eligibility result; `SqlWebOperationJournal` remains the final one-effect fence.
- No SQL schema/migration, broad grant, runtime operator privilege, historical quarantine access or cleanup.
- Feature and frontend resend UI remain OFF after this implementation pending frontend F02/F03 and explicit nonproduction approval.
- All production code is written only after its focused test has failed for the expected missing behavior.

## Review Focus

- A correctly signed receipt presented by a different actor/candidate/epoch/operation must fail without disclosing journal state; Task 2 and Task 5 pin each binding.
- A valid but expired receipt must return `DENY/RECEIPT_EXPIRED`, while malformed or wrongly signed input must remain HTTP 400; Task 2 and Task 5 distinguish these cases.
- An absent journal row must return `ALLOW` only after current Write/Post authority is checked for the caller-supplied allowlisted kind; Task 3 pins the absent-row authorization path.
- A terminal-looking journal row with incomplete matched audit must return `INDETERMINATE/TERMINAL_AUDIT_INCOMPLETE`, not `NOT_EXECUTED`; Task 3 and Task 5 pin this distinction.
- The new GET routes must cause zero journal, terminal-audit, source, stock, ledger or valuation mutation; Task 6 verifies this with an owned SQL fixture.

---

### Task 1: Fail-closed host configuration

**Files:**
- Modify: `src/MyDmsVn.Server.HttpHost/WebHostOptions.cs`
- Modify: `tests/MyDmsVn.Server.HttpHost.Tests/ConfigurationTests.cs`

**Interfaces:**
- Produces: `WebHostOptions.Web6ResendEligibilityEnabled : bool`
- Produces: `WebHostOptions.Web6CandidateId : string?`
- Extends constructor with optional `bool web6ResendEligibilityEnabled = false, string? web6CandidateId = null`

- [ ] **Step 1: Write failing configuration tests**

Add cases proving: default disabled/null; `receipt-v1` is valid only with exact `web5` mode and candidate matching `\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z`; enabled-without-candidate, enabled-outside-web5, invalid candidate, unknown enable value, and candidate-while-disabled all fail closed.

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --filter 'FullyQualifiedName~ConfigurationTests' --logger 'trx;LogFileName=web6-config-red.trx' --results-directory TestResults/web6-resend
```

Expected: FAIL because the WEB6 options/properties and environment parsing do not exist.

- [ ] **Step 3: Implement minimal option validation and environment parsing**

Parse optional `MYDMSVN_WEB6_RESEND_ELIGIBILITY` as absent/`disabled` or `receipt-v1`; parse `MYDMSVN_WEB6_CANDIDATE_ID` only under the selected rules. Preserve source compatibility for existing constructor calls.

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run the Step 2 command with `web6-config-green.trx`; expected PASS, zero skipped.

- [ ] **Step 5: Commit**

```powershell
git add src/MyDmsVn.Server.HttpHost/WebHostOptions.cs tests/MyDmsVn.Server.HttpHost.Tests/ConfigurationTests.cs
git commit -m "feat(web6): gate resend eligibility configuration"
```

### Task 2: Immutable identity parsing and signed receipt codec

**Files:**
- Create: `src/MyDmsVn.Server.HttpHost/Web6ResendReceipt.cs`
- Modify: `src/MyDmsVn.Server.HttpHost/WebOperationWireInput.cs`
- Create: `tests/MyDmsVn.Server.HttpHost.Tests/Web6ResendReceiptTests.cs`

**Interfaces:**
- Produces: `internal sealed class Web6ResendIdentity`
- Produces: `internal sealed class Web6ResendReceiptCodec`
- Produces: `IssuedWeb6ResendReceipt Issue(Web6ResendIdentity identity)`
- Produces: `ValidatedWeb6ResendReceipt Validate(string compactReceipt, Web6ResendIdentity expected)`
- Consumes: `WebHostOptions`, `IUtcClock`, `WebOperationWireInput.KindName/Method/RouteScope`

- [ ] **Step 1: Write failing identity and receipt tests**

Test all six operation kinds and Create-vs-target rules; exact header/payload members; canonical GUID/decimal/lowercase fingerprint; exact `iat=nbf`, `exp=iat+604800`; active and previous root keys; key removal; derived-key domain separation from access JWTs; candidate/actor/epoch/key/fingerprint/kind/target mismatch; extra/duplicate claims; non-ASCII/over-4096 receipt; exact expiry boundary.

- [ ] **Step 2: Run the focused tests and verify RED**

```powershell
dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --filter 'FullyQualifiedName~Web6ResendReceiptTests' --logger 'trx;LogFileName=web6-receipt-red.trx' --results-directory TestResults/web6-resend
```

Expected: FAIL because `Web6ResendReceiptCodec` and strict WEB6 identity parsing are absent.

- [ ] **Step 3: Implement minimal identity parser and receipt codec**

Use `HMACSHA256(rootKey, UTF8("MyDmsVn.Web6.ResendEligibility.SigningKey.v1"))` as the HS256 JWS key. Freeze `typ=MYDMSVN-WEB6-RESEND-RECEIPT`, audience suffix `:web6-resend-eligibility-v1`, exact claims and 4096-character bound from the spec. Parse request kind and optional target through new strict `WebOperationWireInput` helpers; never accept caller-supplied method/route.

- [ ] **Step 4: Run focused and affected auth tests and verify GREEN**

```powershell
dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --filter 'FullyQualifiedName~Web6ResendReceiptTests|FullyQualifiedName~AuthEndpointTests' --logger 'trx;LogFileName=web6-receipt-green.trx' --results-directory TestResults/web6-resend
```

Expected: PASS, zero skipped; receipt cannot validate as an access token and access token cannot validate as a receipt.

- [ ] **Step 5: Commit**

```powershell
git add src/MyDmsVn.Server.HttpHost/Web6ResendReceipt.cs src/MyDmsVn.Server.HttpHost/WebOperationWireInput.cs tests/MyDmsVn.Server.HttpHost.Tests/Web6ResendReceiptTests.cs
git commit -m "feat(web6): sign immutable resend receipts"
```

### Task 3: Current-authority eligibility repository

**Files:**
- Create: `src/MyDmsVn.Server.Application/Operations/IWebOperationResendEligibilityRepository.cs`
- Create: `src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationResendEligibilityRepository.cs`
- Modify: `src/MyDmsVn.Server.Infrastructure/DependencyInjection.cs`
- Modify: `src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationResolutionRepository.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationResendEligibilityTests.cs`

**Interfaces:**
- Produces: `Task AuthorizeReceiptAsync(int actorUserId, Guid sessionId, Guid deploymentEpoch, WebOperationKind kind, CancellationToken token)`
- Produces: `Task<WebOperationResolution?> ReadEligibilityAsync(int actorUserId, Guid sessionId, Guid deploymentEpoch, Guid operationId, WebOperationKind expectedKind, long? expectedTargetId, byte[] expectedFingerprint, CancellationToken token)`
- Consumes: explicit caller-owned transaction, current actor accessor, existing session/epoch/grant checks, existing resolution semantics.

- [ ] **Step 1: Write failing owned-SQL repository tests**

Using the existing `WebOperationResolutionScenario.Owned` lifecycle, add cases for all six kind-to-Write/Post grants; ordinary Read denied but original grant allowed; revoked grant/session; wrong epoch/fingerprint/kind/target; absent owned row after current authorization; COMMITTED retained, unavailable, expired and complete terminal-negative states; incomplete terminal audit returns `INDETERMINATE`; other actor remains indistinguishable; no row/table mutation from either method.

- [ ] **Step 2: Run the focused tests and verify RED**

After locked restore and only with the repository's approved owned nonproduction SQL environment:

```powershell
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --filter 'FullyQualifiedName~WebOperationResendEligibilityTests' --logger 'trx;LogFileName=web6-eligibility-repository-red.trx' --results-directory TestResults/web6-resend
```

Expected: FAIL because the repository interface/implementation is absent. If owned SQL admission is unavailable, record BLOCKED rather than replacing it with mocks.

- [ ] **Step 3: Implement the minimal read-only repository**

Keep the epoch → Users/session → permission → actor/key fence ordering. For an absent row, authorize using the caller-supplied allowlisted kind before returning null. For an existing row, require the stored kind/target/fingerprint/epoch to equal the receipt-bound identity, then delegate to/refactor the current resolution logic so terminal audit semantics are not duplicated.

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run the Step 2 command with `web6-eligibility-repository-green.trx`; expected all discovered cases PASS, zero skipped/retry, owned cleanup verified.

- [ ] **Step 5: Commit**

```powershell
git add src/MyDmsVn.Server.Application/Operations/IWebOperationResendEligibilityRepository.cs src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationResendEligibilityRepository.cs src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationResolutionRepository.cs src/MyDmsVn.Server.Infrastructure/DependencyInjection.cs tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationResendEligibilityTests.cs
git commit -m "feat(web6): authorize resend eligibility reads"
```

### Task 4: Candidate context and receipt issuance endpoints

**Files:**
- Create: `src/MyDmsVn.Server.HttpHost/Web6ResendEligibilityController.cs`
- Create: `src/MyDmsVn.Server.HttpHost/Web6ResendEligibilityWire.cs`
- Create: `tests/MyDmsVn.Server.HttpHost.Tests/Web6ResendEligibilityEndpointTests.cs`
- Create: `docs/contracts/web6-resend-eligibility-v1/README.md`
- Create: `docs/contracts/web6-resend-eligibility-v1/context.json`
- Create: six receipt JSON fixtures under `docs/contracts/web6-resend-eligibility-v1/`

**Interfaces:**
- Produces: `GET /api/v3/operations/resend-context`
- Produces: `GET /api/v3/operations/{operationId}/resend-receipt`
- Consumes: Task 1 options, Task 2 codec/identity, Task 3 `AuthorizeReceiptAsync`.

- [ ] **Step 1: Write failing endpoint and corpus tests**

Test exact JSON/property order against additive fixtures for context and all six receipt identities; default OFF and old modes return indistinguishable 404; malformed path/body/header is 400 before storage; unauthenticated/session/grant/epoch failures retain existing mappings; responses are no-store/no-cache and contain no business payload or secret.

- [ ] **Step 2: Run the focused tests and verify RED**

```powershell
dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --filter 'FullyQualifiedName~Web6ResendEligibilityEndpointTests' --logger 'trx;LogFileName=web6-receipt-endpoint-red.trx' --results-directory TestResults/web6-resend
```

Expected: FAIL because v3 routes/wire/corpus do not exist.

- [ ] **Step 3: Implement context and receipt endpoints**

Follow the existing controller `Read`/correlation/error pattern, exact v3 path validation and caller-owned UoW transaction. Serialize only the spec fields while the current authority transaction is held; create the compact receipt only after repository admission succeeds.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the Step 2 command with `web6-receipt-endpoint-green.trx`; expected PASS and exact corpus equality.

- [ ] **Step 5: Commit**

```powershell
git add src/MyDmsVn.Server.HttpHost/Web6ResendEligibilityController.cs src/MyDmsVn.Server.HttpHost/Web6ResendEligibilityWire.cs tests/MyDmsVn.Server.HttpHost.Tests/Web6ResendEligibilityEndpointTests.cs docs/contracts/web6-resend-eligibility-v1
git commit -m "feat(web6): issue pre-dispatch resend receipts"
```

### Task 5: Eligibility decision endpoint

**Files:**
- Modify: `src/MyDmsVn.Server.HttpHost/Web6ResendEligibilityController.cs`
- Modify: `src/MyDmsVn.Server.HttpHost/Web6ResendEligibilityWire.cs`
- Modify: `tests/MyDmsVn.Server.HttpHost.Tests/Web6ResendEligibilityEndpointTests.cs`
- Create: eligibility JSON fixtures under `docs/contracts/web6-resend-eligibility-v1/`

**Interfaces:**
- Produces: `GET /api/v3/operations/{operationId}/resend-eligibility`
- Consumes: `X-MyDmsVn-Resend-Receipt`, Task 2 validation, Task 3 `ReadEligibilityAsync`.

- [ ] **Step 1: Write failing decision tests**

Pin exact envelopes for `ALLOW/ORIGINAL_NOT_OBSERVED_RECEIPT_VALID`, each DENY reason, and `INDETERMINATE/TERMINAL_AUDIT_INCOMPLETE`. Assert valid-expired receipt returns HTTP 200 DENY; missing/duplicate/malformed/wrong-signature receipt returns 400; auth/grant/identity conflicts use 401/403/409; SQL/unavailable uses 503; candidate mismatch reveals no protected state; every success includes signed expiry and excludes outcome/terminal metadata.

- [ ] **Step 2: Run the focused tests and verify RED**

Run the Task 4 focused command with `web6-eligibility-endpoint-red.trx`; expected FAIL because the eligibility action and mappings are absent.

- [ ] **Step 3: Implement minimal decision mapping**

Validate strict input and receipt before storage; authorize/read under one explicit transaction; map null resolution only to ALLOW with a valid current receipt; map known journal states exactly; never convert exceptions/absence without receipt into a success decision.

- [ ] **Step 4: Run focused and frozen-wire regression tests**

```powershell
dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --filter 'FullyQualifiedName~Web6ResendEligibility|FullyQualifiedName~WebOperationResolution|FullyQualifiedName~WebOperationWire|FullyQualifiedName~WebOperationStatusEndpointTests' --logger 'trx;LogFileName=web6-eligibility-green.trx' --results-directory TestResults/web6-resend
```

Expected: PASS, zero skipped; v1/v2 corpus tests unchanged.

- [ ] **Step 5: Commit**

```powershell
git add src/MyDmsVn.Server.HttpHost/Web6ResendEligibilityController.cs src/MyDmsVn.Server.HttpHost/Web6ResendEligibilityWire.cs tests/MyDmsVn.Server.HttpHost.Tests/Web6ResendEligibilityEndpointTests.cs docs/contracts/web6-resend-eligibility-v1
git commit -m "feat(web6): expose guarded resend eligibility"
```

### Task 6: Cross-layer regression, documentation and evidence

**Files:**
- Modify: `src/MyDmsVn.Server.HttpHost/README.md`
- Modify: `docs/development/WEB6_B01_SOURCE_SAFETY_POLICY.md`
- Modify: `docs/development/WEB6_BACKEND_ACCEPTANCE.md`
- Modify: `docs/evidence/web6/20261103-b04-same-key-sql-replay.md`
- Modify: `docs/evidence/web6/README.md`
- Create: `docs/evidence/web6/20261109-b04-resend-eligibility.md`

**Interfaces:**
- Consumes: all prior tasks and exact test/TRX counts.
- Produces: accurate B6-RPL-05 implementation evidence while retaining resend OFF and later frontend/B05 gates.

- [ ] **Step 1: Restore locked affected projects and run the complete SQL-free HTTP project**

```powershell
$http='tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj'
dotnet restore $http --locked-mode
dotnet test $http -c Release -f net48 --no-restore --logger 'trx;LogFileName=web6-http-complete.trx' --results-directory TestResults/web6-resend
```

Expected: nonzero discovery, all PASS, zero failed/skipped; record exact counts rather than copying historical totals.

- [ ] **Step 2: Run the affected owned-SQL selection sequentially**

```powershell
$security='tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj'
dotnet restore $security --locked-mode
dotnet test $security -c Release -f net48 --no-restore --filter 'FullyQualifiedName~WebOperationResendEligibilityTests|FullyQualifiedName~WebOperationResolutionTests|FullyQualifiedName~WebOperationReadTests' --logger 'trx;LogFileName=web6-eligibility-sql.trx' --results-directory TestResults/web6-resend
```

Expected: nonzero discovery, all PASS, zero failed/skipped/retry, verified owned cleanup and unchanged journal/business counts. If approved SQL admission is unavailable, record NOT RUN/BLOCKED and do not claim B6-RPL-05 verified.

- [ ] **Step 3: Verify frozen corpora and repository hygiene**

Run `git diff --check`, recount/hash WEB4 and WEB5 fixture trees using their existing evidence commands, verify no existing corpus diff, inspect `git status --short`, and confirm no SQL allocation/quarantine action outside the new owned tests.

- [ ] **Step 4: Update live docs and new evidence from actual results**

Document exact source SHA, environment, commands, discovery/execution/pass/fail/skip/retry, TRX hashes, feature/candidate configuration, SQL ownership/cleanup and limitations. Mark B6-RPL-05 implemented/verified only to the level actually run. Keep frontend resend OFF, B05/F04 NOT RUN, staging NOT ACCEPTED and production NO-GO.

- [ ] **Step 5: Commit**

```powershell
git add src/MyDmsVn.Server.HttpHost/README.md docs/development/WEB6_B01_SOURCE_SAFETY_POLICY.md docs/development/WEB6_BACKEND_ACCEPTANCE.md docs/evidence/web6
git commit -m "docs(web6): record resend eligibility evidence"
```

- [ ] **Step 6: Request independent code review and address findings with RED→GREEN tests**

Review security boundaries, public wire compatibility, fail-closed errors, no-auto-retry, SQL authorization ordering and evidence accuracy. Any behavioral correction starts with a focused failing regression test and repeats the applicable verification before the final commit.

