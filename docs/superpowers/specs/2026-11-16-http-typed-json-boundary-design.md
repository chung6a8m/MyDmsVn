# HTTP Typed JSON Boundary Design

## Intent

Refactor the Sales HTTP v1 boundary so controllers operate on named HTTP DTOs rather than `JObject`/anonymous projections, while preserving every existing WEB2/WEB3/WEB4/WEB6 wire, security and operation-journal behavior. The change is for maintainability and onboarding; it does not alter Application/Domain DTOs, business rules, SQL, runtime, packages or production enablement.

## Baseline and current responsibilities

Baseline is `main@00aca21b56df46befa166ee64bce0f6dbb36be06` (also `origin/main` after fetch on 2026-11-16). The SQL-free `MyDmsVn.Server.HttpHost.Tests` baseline is 636 passed, 0 failed, 0 skipped after locked restore.

| Responsibility | Before | After |
|---|---|---|
| Strict UTF-8, RFC 8259 grammar, depth and duplicate keys | `StrictWireJson` | Unchanged, behind one shared request boundary |
| Content-Type/charset/body admission | `SalesWireInput.Body` | Shared strict JSON request reader |
| Sales field/type/range validation | `SalesWireInput` over `JObject` | Sales HTTP DTO decoder/validator behind the boundary |
| Application mapping | `SalesWireInput` returns Application requests | Explicit typed Sales HTTP mapping |
| Controller request flow | Reads `JObject`, maps it, passes it to fingerprint | Receives validated typed request plus preserved canonical source |
| Response projection | Anonymous objects in `SalesWireOutput` | Named Sales HTTP response DTOs |
| Error mapping | `WireApiController`/`HttpJson` | Unchanged envelope/status/correlation semantics |
| Operation fingerprint | `WebOperationWireInput` canonicalizes strict `JObject` | Same canonical source and algorithm; never reserialize DTO |

## Chosen approach

Use a typed façade over the existing strict JSON tree. A reusable `StrictJsonRequest<T>` contains the validated HTTP DTO and the exact `JObject` produced by `StrictWireJson`. A shared reader owns transport admission and invokes one supplied decoder. Sales-specific decoders validate exact property sets, token types, lexical decimal/integer/date/version rules and create named request DTOs. Mapping from those DTOs to Application requests is explicit and contains no JSON traversal.

The controller flow becomes:

`HTTP request -> StrictWireJson -> StrictJsonRequest<Sales...RequestDto> -> Sales mapping -> Application -> named Sales response DTO -> HttpJson`

For mutations, the preserved strict `JObject` travels separately to `WebOperationWireInput`; canonicalization and SHA-256 remain byte-for-byte governed by the existing WEB4 algorithm. This is deliberately not Web API's default formatter/model binding because it cannot by itself guarantee the existing duplicate/unknown-property, numeric-token, lexical-scale and field-path behavior.

## Alternatives considered

1. Default Web API 2 model binding with Json.NET converters: less custom code, but it risks coercion and loses the original strict tree needed for proven fingerprint compatibility. Rejected.
2. Typed façade over `StrictWireJson` (selected): removes raw JSON from controller/mapping flow while retaining the one proven parser and canonical source.
3. Keep moving `JObject` logic into another helper: smallest diff, but controllers would still depend on untyped request state and it would not meet the stated maintainability goal. Rejected.

## Contract ownership

Named request DTOs are internal to `MyDmsVn.Server.HttpHost`; they use wire-shaped scalar types (for example decimal and Int64 lexemes initially remain strings until validation). Named response DTOs expose already formatted strings for Int64, decimal, date and UTC values. Local/Desktop DTOs and global `ApiJson` settings remain unchanged.

The shared boundary is reusable by Inventory/Auth later, but this PR migrates only Sales. Inventory may continue calling the compatibility body reader; no second parser is introduced.

## Invariants and non-goals

- Preserve `net48`, ASP.NET Web API 2, OWIN/Katana and Newtonsoft.Json versions.
- Preserve exact field names/casing, required/optional/null rules, omission behavior, status/error envelopes and correlation IDs.
- Preserve strict query parsing, request-size admission and authorization behavior.
- Preserve WEB4/WEB6 operation kind, route, target, actor/session/epoch/key, fingerprint, replay/conflict and UNKNOWN behavior; no mutation retry or resend enablement.
- Do not change Auth, Goods Receipt or Inventory contracts beyond compiling against shared compatibility APIs.
- Do not alter fixtures to accommodate output drift.
- No SQL, migration, grant, frontend, package or deployment changes.

## Verification

Use test-first regression coverage for the typed boundary/API shape, DTO-to-Application mapping, invalid input before handler, named response golden JSON, and recorded fingerprint vectors. Run focused Sales/operation tests during implementation and the complete SQL-free `MyDmsVn.Server.HttpHost.Tests` suite at completion. Any SQL/IIS/browser acceptance remains NOT RUN because this source-only refactor does not authorize or require those environments.
