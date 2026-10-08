# Architecture — MyDmsVn

Status: v1/v2 architecture baseline. Decision records: `docs/adr/`.

## 1. Design

- **Modular monolith**, organized by feature areas (Identity, Catalog, Inventory) and Clean Architecture layers.
- **v1 Local:** WinForms → `Desktop.Application` abstractions → LocalApiClient → Server.Application (MediatR) → Domain abstractions → Infrastructure → SQL Server.
- **v2 Remote:** same WinForms/ViewModels and Desktop.Application → HttpApiClient → HTTP/JSON → ASP.NET Core 8 Server.Api → same Server.Application → Infrastructure → SQL Server.

v1 executes server logic inside the desktop process and therefore is a **2-tier deployment**, not an independent trusted application server. A malicious client or SQL credentials can bypass app checks; limit SQL privileges/deployment exposure. v2 creates a separately enforceable service boundary.

```text
WinForms Views + ViewModels
        |
Desktop.Application (IProductApiClient, IGoodsReceiptApiClient, ...)
        |
     Adapter
     /     \
LocalApiClient    HttpApiClient (v2)
     |                  |
     |              HTTP JSON
     |                  |
Server.Application   Server.Api (.NET 8)
     |                  |
     +------<-----------+
     |
Server.Domain + Repository/Query abstractions
     |
Server.Infrastructure (RepoDb / Dapper / SQL / DbUp integration)
     |
SQL Server
```

## 2. Dependency rules

Allowed dependency direction (compile-time):
- Server.Domain: no dependency on Application, Infrastructure, HTTP, WinForms.
- Server.Application → Domain, shared primitives as needed. May use Contracts for DTOs only when justified; avoid putting domain entities in Contracts.
- Server.Infrastructure → Application and Domain interfaces.
- Desktop.Application → Contracts (interface/API contract definitions).
- Desktop.WinForms → Desktop.Application and Contracts; no direct SQL/Server.Infrastructure reference.
- Local adapter composition → Desktop.Application, Server.Application, Server.Infrastructure. The composition root is a host, not a reusable UI assembly.
- Server.Api → Server.Application and Infrastructure, and Contracts to format JSON.
- HTTP adapter → Desktop.Application and Contracts only.

Prevent a single Desktop.Infrastructure assembly from pulling SQL and server assemblies into Remote Mode. Use `Desktop.Infrastructure.Local` / `Desktop.Infrastructure.Http` projects or strictly isolated assemblies.

Cross-layer constructs forbidden: UI classes in Contracts; ADO.NET connections in DTOs; MediatR IRequest/ErrorOr in public JSON; HTTP IResult in Application handlers.

## 3. Modules

Initial bounded modules:
- **Identity:** users, roles, permissions, auth, employee-user linkage where required.
- **Catalog:** products, warehouses, employees, customers.
- **Inventory:** goods receipt, ledger, balance query.

Each feature keeps Command, Query, Validator, Handler and DTO mapping near each other. Avoid generic "service" layers that merely forward calls.

## 4. Request handling

```text
Client request DTO -> adapter -> Application Command/Query
                  -> Authentication/Authorization
                  -> FluentValidation pipeline
                  -> Handler (ErrorOr<T>)
                  -> transport-neutral result mapper (ApiResponse<T>)
                  -> Local return OR HTTP status + JSON
```

Rules:
- Domain/application never depend on ASP.NET Core.
- FluentValidation, authorization and logging behaviors have stable pipeline ordering; do not assume generic exceptions are a business error.
- For HTTP, map typed error codes to suitable HTTP statuses; Local Mode still uses the same `ApiResponse<T>` data/error envelope.
- Share a common matrix of contracts and error fixtures for Local/HTTP parity tests.

See `docs/CONTRACTS.md`.

## 5. Transaction ownership

Use **Explicit UnitOfWork (Option A)**. One instance owns one connection and optional transaction. It creates repositories and exposes a read-side query execution context as needed; all DB commands in the scope must receive the same transaction explicitly. Do not use static/AsyncLocal ambient transaction providers.

The goods receipt Post command owns transaction boundaries; infrastructure implementation uses SQL constraints and locking to keep posting idempotent and balances correct. See `docs/PERSISTENCE.md`.

## 6. Security

UserPermission override precedence: if a direct record exists, its Granted value decides; else union of role permissions; missing/unknown permission = deny. Enforce in Application regardless of client mode, plus auth and input controls at the API host in v2. Keep PasswordSalt for imported legacy credentials. See `docs/SECURITY.md`.

## 7. Client UI

- Thin WinForms View plus bindable CommunityToolkit.Mvvm ViewModel.
- One API interface per meaningful feature, async and CancellationToken-aware.
- Shared shell, compact workspace, lookups, details editing with BootstrapSourceGrid, busy states, async form commands, error-to-field mapping.
- Bootstrap5WinFormUI / BootstrapSourceGrid are optional source/project references depending on compatibility tests. Never silently substitute their APIs without checking actual versions.
- Multi-target UI library `net48;net8.0-windows`; separate hosts for Windows target frameworks.

## 8. Startup / composition

Each desktop host configures DI once (separate composition roots). Local mode registers Server Application + SQL Infrastructure + Local adapter, and requires DB config. V2 Remote host registers HTTP adapter + token/session management and must **not** load SQL Server credentials / server infrastructure.

DbUp runs through a controlled migrator/deployment step, not automatically from every desktop client login. DB schema version must be compatible with the deployed client/server.

## 9. Cross-cutting

Use correlation IDs, structured Serilog logging, UTC audit timestamps, cancellation, deterministic exceptions/failed results, careful connection disposal, and user-facing error localization at presentation layer rather than exposing SQL exceptions.

Architecture validation: tests or dependency checks fail if Domain/Application reference Infrastructure/UI/HTTP libraries. SQL integration tests validate the most dangerous invariants.

## 10. Architectural change process

A change affecting target frameworks, transport contracts, permission precedence, transaction model, identity migration or ledger invariants requires a new ADR and updated relevant docs before implementation. `project-idea.md` remains historical input.
