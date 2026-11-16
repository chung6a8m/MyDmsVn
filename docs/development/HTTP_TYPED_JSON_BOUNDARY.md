# HTTP typed JSON boundary

## Purpose and scope

The Sales HTTP v1 endpoints use named HTTP-specific DTOs without changing the transport-neutral Application contracts. The boundary remains compatible with the WEB2 wire contract and WEB4/WEB6 operation journal. Goods Receipt, Inventory and Auth are not migrated by this refactor.

## Request flow

```text
HTTP request
  -> OWIN transport and request-size admission
  -> StrictWireJson (strict UTF-8, RFC 8259 grammar, depth, duplicate keys)
  -> StrictJsonRequest<T> (typed value + preserved strict JObject source)
  -> SalesWireInput validation/decoder
  -> SalesHttpRequestMapper
  -> Application command/query via MediatR
  -> SalesWireOutput named response DTO
  -> HttpJson / ApiJson success or error envelope
```

`StrictJsonRequest<T>.Value` is the validated Sales HTTP DTO used for Application mapping. `StrictJsonRequest<T>.Source` is the original strict tree and is reserved for compatibility boundaries such as operation fingerprinting. Do not serialize `Value` back to JSON to compute a fingerprint: CLR decimal/DateTime types and DTO property layout are not the canonical WEB4 request representation.

## Ownership

| Component | Owns | Does not own |
|---|---|---|
| `Web2Pipeline` | HTTPS/origin/forwarded-header checks, authentication scope, maximum request bytes, correlation | Sales field rules |
| `StrictWireJson` | strict UTF-8, JSON grammar, maximum depth, duplicate property rejection | Sales property names/ranges |
| `StrictJsonRequest<T>` | content type/charset admission, pairing typed value with strict source | endpoint-specific validation |
| `SalesWireInput` | exact fields, token types, lexical numeric/date/version rules, query decoding and field paths | Application business rules |
| `SalesHttpRequestMapper` | HTTP DTO to existing Application request | JSON parsing or MediatR dispatch |
| `SalesControllers` | orchestration, cancellation, authorization-mediated dispatch and error mapping | raw field traversal or manual serialization |
| `SalesWireOutput` | named HTTP response projection and lossless wire formatting | Local/Desktop serialization |
| `WebOperationWireInput` | operation headers/path, canonical WEB4 intent and SHA-256 | DTO reserialization or mutation retry |

## Before and after

| Concern | Before | After |
|---|---|---|
| Sales mutation controller locals containing raw body trees | Three (`Create`, `Update`, `Post`) | Zero; each receives `StrictJsonRequest<T>` |
| Controller field access by string/token | Indirect through `SalesWireInput` after controller retained `JObject` | None in `SalesReceiptsController` |
| Request contract type | Application request returned directly from JSON decoder | Named HTTP DTO, then explicit mapper |
| Response contract type | Anonymous projections | Named detail/line/list/post HTTP DTOs |
| Content-type/charset + strict parse | Sales-specific `Body` method | Reusable `StrictJsonRequest.Read<T>` |
| Low-level tree handling retained | Parser, Sales decoder, fingerprint canonicalizer | Same three boundaries, because exact tokens/lexemes and canonical source are contract requirements |

The `WireApiController.Operation(JObject)` compatibility overload remains for the non-migrated Inventory controllers. Sales uses the typed overload. Removing the compatibility overload belongs to a separately authorized Inventory migration, not this PR.

## Extension rule

For a new module, define named HTTP request/response DTOs, decode one strict source tree exactly once, map explicitly to Application, and pass the same `StrictJsonRequest<T>` to operation infrastructure when the endpoint is journaled. Reuse `StrictWireJson`; never add a second manual parser or rely on default formatter coercion for strict wire fields.

## Preserved invariants

- Int64 IDs and decimals remain JSON strings; Catalog Int32 values remain JSON numbers.
- Required/optional/null rules, field paths, query strictness, UTC/date/version handling and null omission remain unchanged.
- Success/error envelopes, statuses, business codes, permissions and correlation IDs remain unchanged.
- Fingerprint canonicalization, SHA-256, operation key/session/epoch/route/target semantics, replay/conflict behavior and UNKNOWN recovery remain unchanged.
- HTTP mutations are not automatically retried; manual resend remains governed by existing WEB6 gates and is not enabled here.
