# P7 — ASP.NET Core 8 HTTP API and adapter parity

Status: Not started. Depends on P6. Read ADR 0001, `docs/CONTRACTS.md`, `docs/SECURITY.md`.

## Objective

Introduce independently hosted ASP.NET Core 8 HTTP APIs and an HttpApiClient for the existing WinForms contract, reusing Application handlers and infrastructure.

## Task list

- [ ] **P7-T01 / Host:** add `MyDmsVn.Server.Api` targeting `net8.0` and composition root; register MediatR, validation, authorization, RepoDb/Dapper and SQL.
- [ ] **P7-T02 / Endpoints:** products, warehouses, employees, customers, receipts, inventory balances/card and identity based on existing request/response contracts.
- [ ] **P7-T03 / Security:** Bearer-based authentication, HTTPS, 401/403, role/direct permission enforcement in Application, session expiry and revocation policy.
- [ ] **P7-T04 / Error/status mapping:** convert application ErrorOr via transport-neutral mapper to ApiResponse envelope, then correct HTTP code; never serialize ErrorOr/raw SQL exception.
- [ ] **P7-T05 / OpenAPI:** publish stable schemas, documented pagination/filter/order, decimal/UTC representation and error examples.
- [ ] **P7-T06 / HttpApiClient:** use HttpClient, cancellation, token renewal/session-expiry UX, bounded network error mapping; no references to Server.Infrastructure.
- [ ] **P7-T07 / Shared adapter parity suite:** same DTOs/golden JSON fixtures, validation codes/fields, permission failures, draft/post/stock semantics against Local and HTTP modes.
- [ ] **P7-T08 / Deployment:** SQL credentials only on server; desktop remote deployment has no DB connection settings or SQL libraries; log correlation over HTTP and remote upgrade/version guidance.

## Exit evidence

P5 user scenario passes with both LocalApiClient and HttpApiClient; authorization/exception cases yield deterministic HTTP statuses/JSON; SQL mutation cannot be invoked by direct desktop DB access in remote package; relevant security/contract integration tests pass.

## Avoid

Rewriting UI, duplicating use-case logic in controllers, moving direct SQL to HttpApiClient, transmitting Domain entities, relying on UI-only permission guards.
