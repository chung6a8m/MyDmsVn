# HTTP Typed JSON Boundary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace raw JSON handling in the Sales controller path with named HTTP request/response DTOs and a reusable strict typed boundary without changing wire or operation-fingerprint behavior.

**Architecture:** Keep `StrictWireJson` as the only strict parser. Return a `StrictJsonRequest<T>` containing both a validated typed DTO and the original strict `JObject`; map DTOs to Application requests explicitly and retain the original tree exclusively for WEB4 fingerprint canonicalization. Serialize named response DTOs through the unchanged `HttpJson`/`ApiJson` envelope.

**Tech Stack:** C# 12, .NET Framework 4.8, ASP.NET Web API 2, OWIN/Katana, Newtonsoft.Json, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-16-http-typed-json-boundary-design.md`

## Global Constraints

- Work from `main@00aca21b56df46befa166ee64bce0f6dbb36be06`; target `net48` only.
- No package/runtime/Application/Domain/SQL/frontend/canonical-fixture change.
- Keep `StrictWireJson` grammar, UTF-8, duplicate-key and depth semantics; do not add a second parser.
- Keep WEB4 canonicalization and SHA-256 based on the preserved strict source tree, never DTO reserialization.
- Keep all current field paths, envelopes, null omission, authorization and no-mutation-retry behavior.

## Review Focus

- A valid typed Create/Update/Post request must reach the exact existing Application request values, including Int64 and decimal extremes.
- Duplicate/unknown/missing fields, malformed UTF-8/JSON and wrong token types must fail before mediator dispatch with the existing field path.
- Nullable note, rowversion, dates, pagination/cursor and query duplication must keep their current semantics.
- Named detail/list/post response DTOs must match golden JSON with lossless Int64/decimal and UTC formatting.
- Recorded WEB4 fingerprint vectors and same-key operation tests must remain unchanged after the controller stops handling `JObject`.

---

### Task 1: Shared strict typed request boundary

**Files:**
- Create: `src/MyDmsVn.Server.HttpHost/StrictJsonRequest.cs`
- Modify: `src/MyDmsVn.Server.HttpHost/SalesWireInput.cs`
- Test: `tests/MyDmsVn.Server.HttpHost.Tests/SalesWireEndpointTests.cs`

**Interfaces:**
- Produces: `StrictJsonRequest<T>`, `StrictJsonRequest.Read<T>(HttpRequestMessage, Func<JObject,T>)`, and compatibility access to the preserved source tree.
- Consumes: existing `StrictWireJson.Read(byte[])` and `WireInputException`.

- [ ] Add a failing test proving the shared boundary returns a typed value and preserved strict source, while duplicate/unknown/malformed input still fails before dispatch.
- [ ] Run the focused test and confirm RED because the typed boundary does not exist.
- [ ] Implement the smallest shared reader by moving only content-type/charset/body admission from `SalesWireInput.Body`; retain a compatibility method for Inventory.
- [ ] Run focused strict request and Inventory/Sales boundary tests; expect PASS.
- [ ] Commit the task.

### Task 2: Named Sales request DTOs and mapping

**Files:**
- Create: `src/MyDmsVn.Server.HttpHost/SalesHttpRequests.cs`
- Modify: `src/MyDmsVn.Server.HttpHost/SalesWireInput.cs`
- Modify: `src/MyDmsVn.Server.HttpHost/SalesControllers.cs`
- Test: `tests/MyDmsVn.Server.HttpHost.Tests/SalesWireEndpointTests.cs`

**Interfaces:**
- Consumes: `StrictJsonRequest<T>` from Task 1.
- Produces: named Create/Update/Post/List/Get HTTP DTOs plus explicit mapping to existing Application request/query objects.

- [ ] Add failing reflection/behavior tests requiring named DTO results and a controller mutation path with no `JObject` local/parameter dependency.
- [ ] Run focused tests and confirm RED for missing DTO/boundary APIs.
- [ ] Implement exact field-set/token/lexical validation into named DTO decoders and explicit Application mapping; keep query validation typed.
- [ ] Update Sales controllers to use typed results and pass only preserved source to operation context.
- [ ] Run all Sales endpoint tests; expect golden, validation, authorization and no-retry tests PASS.
- [ ] Commit the task.

### Task 3: Named Sales response DTOs

**Files:**
- Replace internals in: `src/MyDmsVn.Server.HttpHost/SalesWireOutput.cs`
- Test: `tests/MyDmsVn.Server.HttpHost.Tests/SalesWireEndpointTests.cs`

**Interfaces:**
- Consumes: existing Application `SalesReceiptDto`, `SalesReceiptListPage`, `PostSalesReceiptResponse`.
- Produces: named Sales detail/line/list-row/page/post acknowledgement HTTP response DTOs serialized by unchanged `HttpJson`.

- [ ] Add failing tests asserting output mapper return types are named HTTP DTOs and all existing golden payloads remain byte-semantically equal.
- [ ] Run focused tests and confirm RED because mappers currently return anonymous objects.
- [ ] Implement named response DTOs with explicit Json.NET property names only where needed to preserve lower-camel wire names and current null omission.
- [ ] Run Sales response/golden/error tests; expect PASS.
- [ ] Commit the task.

### Task 4: Fingerprint compatibility, documentation and full verification

**Files:**
- Modify: `src/MyDmsVn.Server.HttpHost/WebOperationWireInput.cs` only if needed to consume the preserved boundary without changing canonicalization.
- Modify: `tests/MyDmsVn.Server.HttpHost.Tests/WebOperationWireTests.cs`
- Create: `docs/development/HTTP_TYPED_JSON_BOUNDARY.md`
- Create: `docs/evidence/web6/20261116-http-typed-json-boundary.md`

**Interfaces:**
- Consumes: strict source retained by Tasks 1–2 and existing WEB4 canonical fixtures.
- Produces: independent recorded fingerprint regression values, architecture/onboarding documentation, Before/After table and candidate evidence.

- [ ] Add/strengthen a failing regression test that compares the typed request's preserved source fingerprint against pinned baseline canonical/SHA-256 vectors and detects DTO-reserialization substitution.
- [ ] Run the focused operation test and confirm RED for the new compatibility assertion.
- [ ] Make the minimal integration change needed while leaving `Canonicalize` and SHA-256 semantics intact.
- [ ] Run focused Sales, WebOperation, Inventory/shared-boundary and Pipeline tests; expect PASS.
- [ ] Write the architecture and evidence documents with baseline/candidate SHA, environment, exact commands/counts, Before/After responsibilities and NOT RUN limits.
- [ ] Run `dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --no-restore` with TRX; expect all discovered tests pass with zero skipped.
- [ ] Run `dotnet build src/MyDmsVn.Server.HttpHost/MyDmsVn.Server.HttpHost.csproj -c Release -f net48 --no-restore`; expect exit 0.
- [ ] Commit the task, request whole-branch review, fix any Critical/Important finding with RED→GREEN, then rerun the full suite.
