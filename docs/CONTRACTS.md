# Application / Desktop API contracts

## Purpose

All WinForms ViewModels talk to feature-specific `IxxxApiClient` abstractions returning the same transport-neutral result, whether the implementation invokes Server.Application directly (v1) or calls ASP.NET Core HTTP (v2). Public DTOs are contracts, not Domain entities.

## Proposed result envelope

```csharp
public sealed class ApiResponse<T>
{
    public T Data { get; set; }
    public ApiError Error { get; set; }
    // Factory methods enforce exactly one of Data or Error as a meaningful result.
}

public sealed class ApiError
{
    public string Code { get; set; }
    public string Message { get; set; }
    public IReadOnlyCollection<ApiErrorDetail> Details { get; set; }
}

public sealed class ApiErrorDetail
{
    public string Code { get; set; }
    public string Message { get; set; }
    public string Field { get; set; }
}
```

This illustrates the *wire shape*, not compilable final implementation; enable nullable annotations appropriately for both target frameworks. Success can carry `Data = null` only for an explicitly documented no-content operation; prefer a typed `UnitResponse`. Reject ambiguous both-set/none-set states through factories and tests.

JSON property names: `data`, `error`, `code`, `message`, `details`, `field`. Specify one serializer naming policy and verify it with golden fixtures. No serialized ErrorOr, SQL errors, stack traces, transactions or MediatR internals.

## Converting Application results

```text
ValidationBehavior / Application handler -> ErrorOr<T>
                                     |
                       transport-neutral mapper
                                     |
                               ApiResponse<T>
                               /            \
                       Local result       HTTP JSON
                                              +
                                      HTTP status code
```

- The **transport-neutral** ErrorOr-to-ApiResponse mapping should be implemented in an application composition/adapter library without ASP.NET dependency.
- `Server.Api` wraps the mapped response with HTTP status and headers.
- Application handlers never construct `IResult`, `Results.Ok`, `HttpResponse` or `JsonResult`.
- Avoid `dynamic` for pipeline result conversion; use a strongly typed generic conversion strategy compatible with ErrorOr version pinned in the solution.
- Validate handler exceptions as unexpected failures, and log them without exposing details to users.

## Error taxonomy

| Situation | ApiError.Code | HTTP status (v2) |
|---|---|---|
| Request invalid | `ValidationError` | 400 |
| Unauthenticated | `Auth.Unauthorized` | 401 |
| Not permitted | `Auth.Forbidden` | 403 |
| Entity missing | Feature-qualified code, e.g. `Product.NotFound` | 404 |
| Business conflict / invalid transition | e.g. `GoodsReceipt.AlreadyPosted` | 409 |
| Unique key conflict | e.g. `Product.CodeAlreadyExists` | 409 |
| Unhandled failure | `InternalError` | 500 |

Validation detail example: `{ "code": "Validation.Required", "message": "Quantity is required.", "field": "lines[0].quantity" }`. Field paths **must** match JSON naming and array-index conventions. Do not just lowercase the first character of nested C# paths.

For multiple failures, choose a deterministic aggregate code/status policy (all validation => ValidationError; mixed types => mapped dominant safe status) and retain details. Never select a status only from arbitrary enumeration order.

## Desktop client APIs

Recommended initial interfaces:
- `IProductApiClient`, `IWarehouseApiClient`, `IEmployeeApiClient`, `ICustomerApiClient`.
- `IGoodsReceiptApiClient`: `CreateDraftAsync`, `UpdateDraftAsync`, `PostAsync`, `GetAsync`, `ListAsync`.
- `IInventoryApiClient`: `GetStockBalancesAsync`, `GetStockCardAsync`.
- `IIdentityApiClient`: `LoginAsync` / `GetCurrentUserAsync`, after auth contract is defined.

Each call is async and accepts `CancellationToken`. Commands have DTO request and typed response; queries return paginated DTOs with total count and deterministic sort where appropriate.

Example command-level behavior:
- `PostGoodsReceiptRequest` identifies the receipt and may carry concurrency token; it does **not** send a raw UnitOfWork or EF/RepoDb entity.
- `PostGoodsReceiptResponse` returns receipt identifier, status, posting timestamp; retry of an already posted receipt returns a documented conflict (or a stable idempotent success if an idempotency contract is added later). In either case, **no second stock posting** occurs.
- Identifiers and date/time formats are consistent in Local/HTTP tests; transport changes cannot alter business semantics.

## Auth and HTTP-specific behavior (P7)

Use `Authorization: Bearer` for HTTP endpoint calls; Session/token lifecycle belongs to desktop HTTP infrastructure, not ViewModels. Security rules live on Application use cases and HTTP host authentication. HTTP response codes are meaningful, despite carrying a common JSON envelope.

## Compatibility tests

Create golden JSON fixtures for:
- success and validation failure with field detail,
- 401/403/404/409,
- pagination,
- decimal quantities and UTC timestamps,
- all planned P5 command DTOs.

Run the **same behavior/response assertions** against LocalApiClient and HttpApiClient when P7 is added. Any contract breaking change requires versioning / ADR.
