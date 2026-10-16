# P2 — Application pipeline, typed results and contracts

Status: Complete (2026-10-09). Depends on P1. Read `docs/CONTRACTS.md`, `docs/ARCHITECTURE.md`, `docs/TEST_STRATEGY.md`.

## Objective

Ensure all commands and queries can return standardized typed results to both Local Mode and future HTTP Mode. Application handlers must be transport-independent and reusable.

## Task list

- [x] **P2-T01 / Contracts:** stable ApiResponse<T>, ApiError, ApiErrorDetail, pagination and no-content response; enforce one-of success/error invariant through factories; JSON case contract and serializer settings.
- [x] **P2-T02 / MediatR wiring:** configure requests, handlers, pipeline ordering; validate MediatR package/license and net48 compatibility without changing approved pins silently.
- [x] **P2-T03 / FluentValidation:** async validators and generic ValidationBehavior returning typed ErrorOr<T>; no `dynamic`; field errors reflect exact JSON property paths including nested arrays.
- [x] **P2-T04 / Error translation:** ErrorOr<T> → transport-neutral ApiResponse<T>; central mapping of Validation / NotFound / Conflict / Unauthorized / Forbidden; unknown errors do not leak internal exception.
- [x] **P2-T05 / Desktop interfaces:** define feature API client shapes for catalog, receipts, stock query and identity; introduce LocalApiClient proof using a fake/minimal handler, no SQL app feature.
- [x] **P2-T06 / Contract tests:** unit tests for validation, mixed error sets, 400/401/403/404/409 semantics as metadata, JSON golden snapshots, cancellation/exception behavior.
- [x] **P2-T07 / Dependency tests:** Server.Domain/Application no ASP.NET/WinForms references, Desktop.Application only contracts, mapper no IResult.

## Exit evidence

Demonstrate one test-only command returning success and validation failure via LocalApiClient as ApiResponse. Tests prove no ad-hoc DTOs or ErrorOr escape to ViewModels. Contracts are fit for P5 and ready for HTTP serialization in P7.

Evidence: `docs/P2_VERIFICATION.md`.

## Avoid

HTTP host implementation in P2, raw exceptions in JSON, direct SQL in an adapter, redundant validation at ViewModel as security control.
