# ADR 0001 — Local v1 and HTTP v2 adapters

Status: **Accepted** · Date: 2026-10-08

## Context
The MVP must serve WinForms clients targeting .NET Framework 4.8 and .NET 8 while minimizing infrastructure complexity. Later versions must expose secure ASP.NET Core HTTP APIs without rewriting UI or Application business logic.

## Decision
Define feature-specific `IxxxApiClient` interfaces and DTO/ApiResponse contracts at Desktop.Application/Contracts boundary. **v1 LocalApiClient** dispatches into Server.Application in process, composed in desktop host. **v2 HttpApiClient** serializes the same semantics over HTTP; Server.Api invokes the same Application handlers.

In v2 no desktop SQL credentials or Server.Infrastructure assemblies are required. Views, ViewModels and client contracts stay unchanged except explicit additive/versioned contracts.

## Alternatives
A. UI directly references Server.Infrastructure — rejected: couples UI and makes transport migration expensive.
B. HTTP Server from day one — deferred by user goal/time-to-MVP.
C. Shared Service Locator / static database facade — rejected: hides dependencies.

## Consequences
v1 is not a trusted 3-tier server: SQL permissions and client distribution require extra operational protection. Dual adapter contract tests are mandatory in P7. SQL migrations must be independent from desktop app startup.

See `docs/ARCHITECTURE.md` and `docs/CONTRACTS.md`.
