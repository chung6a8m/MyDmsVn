# P7 — ASP.NET Core 8 HTTP API and adapter parity

Status: Not started. Depends on P6 (including P3.1 provisioning/registry and P4.1 reusable Login UX). Read ADR 0001, `docs/CONTRACTS.md`, `docs/SECURITY.md`.

## Test execution for not-yet-started P7 (Codex policy, 2026-10-15)

Apply [Codex Test Execution Policy](../CODEX_TEST_EXECUTION_POLICY.md) to **P7-T01–T08**, preserving HTTP/Local contract and security acceptance:

- **L1:** prefer focused Application/DTO, status/JSON mapping, token/session and `HttpApiClient` tests while editing. No SQL for purely in-memory mapping.
- **L2:** exercise ASP.NET Core endpoint/adapter integration and approved real SQL for permissions, identity, transactions, migration compatibility and concurrency as applicable. Choose focused `FastSQL`, `MigrationSchema`, `SecurityProvisioning` or `SqlConcurrency` by actual test responsibility, not by speed. Include negative 401/403, revoked permission, bearer/session expiry and Local-versus-HTTP parity cases.
- **L3:** after final behavior edits, run relevant Release builds and full regressions across supported TFMs, including **unfiltered `net48` and `net8.0` SQL** with mandatory inventory/TRX, approval, watchdog, ownership and zero required skips. No single-pass strict SQL PR runner exists; filtered categories or plain `dotnet test` alone are not equivalent. If a mandatory gate is unavailable, report `Blocked/Not verified`.
- **L4:** provide plan-specific remote package/API security, SQL credential exclusion and compatibility evidence separately. PR Test Execution Reports must show actual commands/results and any manual or environmental blockers; this text does not claim that the future P7 host is implemented.

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

Rewriting UI, duplicating use-case logic in controllers, moving direct SQL to HttpApiClient, transmitting Domain entities, relying on UI-only permission guards. Reuse P3.1 registry and deployment sync for future remote modules; keep P4.1 Login presentation, but implement bearer token lifecycle only in P7 HTTP infrastructure.
