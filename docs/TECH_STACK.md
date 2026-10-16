# Technical stack and compatibility baseline

Status: decisions approved by project owner on 2026-10-08.

## Runtime lifecycle note (2026-10-16)

The NET8-PAUSE candidate actively declares `net48` for product, SQL and Desktop projects. Historical `net8.0` / `net8.0-windows` logical cases remain in the reviewed manifest as `PARKED / NOT RUN`; AppCore source is retained but excluded from the solution/release. The strict/focused runner remains tool-only `net8.0` with explicit framework selection. T05 runtime/build/package/GUI/restoration gates pass at `6b0ce337`; same-protocol duration comparison and merge remain open. Restore .NET 8 through [the reviewed procedure](REACTIVATE_NET8.md) before P7 Server.Api or modern desktop parity unless a separate runtime plan is approved.

## Runtimes

- Desktop legacy host: `net48` (WinForms / Windows, .NET Framework 4.8).
- Desktop modern host: parked AppCore source formerly `net8.0-windows`; not built or shipped during the pause.
- Application/Infrastructure libraries and shared WinForms: active `net48`; historical net8 targets are retained in the reactivation contract, not current binaries.
- ASP.NET Core Server.Api (P7): future `net8.0`, blocked on reviewed reactivation or an approved alternate framework plan.
- Windows + Visual Studio 2022, installed .NET 8 SDK and .NET Framework 4.8 developer targeting pack.

**No automatic migration to .NET 10.** .NET 8 support lifecycle is a documented operational risk to revisit separately; it is an explicit compatibility decision.

## Package baseline

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="BCrypt.Net-Next" Version="4.2.0" />
    <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.2" />
    <PackageVersion Include="Dapper" Version="2.1.79" />
    <PackageVersion Include="dbup-sqlserver" Version="7.2.0" />
    <PackageVersion Include="ErrorOr" Version="2.1.1" />
    <PackageVersion Include="FluentValidation" Version="11.12.0" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="11.12.0" />
    <PackageVersion Include="MediatR" Version="12.5.0" />
    <PackageVersion Include="MediatR.Contracts" Version="2.0.1" />
    <PackageVersion Include="Microsoft.Data.SqlClient" Version="7.0.2" />
    <PackageVersion Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="8.0.1" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.2" />
    <PackageVersion Include="MiniExcel" Version="1.45.0" />
    <PackageVersion Include="Newtonsoft.Json" Version="13.0.4" />
    <PackageVersion Include="Onova" Version="2.6.13" />
    <PackageVersion Include="RepoDb" Version="1.16.0" />
    <PackageVersion Include="RepoDb.SqlServer" Version="1.16.1" />
    <PackageVersion Include="RepoDb.SqlServer.BulkOperations" Version="1.16.2" />
    <PackageVersion Include="Serilog" Version="4.4.0" />
    <PackageVersion Include="Serilog.Extensions.Logging" Version="8.0.0" />
    <PackageVersion Include="Serilog.Sinks.File" Version="7.0.0" />
    <PackageVersion Include="System.Security.Cryptography.ProtectedData" Version="8.0.0" />
  </ItemGroup>
</Project>
```

These are **requested pins / baseline**, not a claim of successful restore on both TFMs. P0 must verify package restore, licenses and transitive compatibility. Only include a package in a project that uses it; no project should indiscriminately reference all packages.

- No blanket `NoWarn=NU1507`. If multiple NuGet sources cause NU1507, solve package-source mapping or document the exception.
- Use `packages.lock.json` (locked restore in CI once generated).
- Do not introduce EF Core as an additional ORM in P0–P5.
- For JSON, prefer a single specified serializer/configuration across Local/HTTP; Newtonsoft.Json can remain when actual compatibility requires it.
- Only bring MiniExcel and Onova into projects when import/export and updater features are approved.
- SourceGrid and Bootstrap UI projects must be inspected for target framework/API compatibility before adding references.
- C# syntax compatibility must be tested for the legacy TFM, including packages needing backported runtime/compiler support.

## Environment variables

Define test/database configuration using environment variables, e.g. `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING`. Never check in production connection strings. The test harness should **fail safely** or skip with explicit reason when SQL Server is not configured; never fallback to a production or developer database silently.

## Editing standards

Follow the repository's `.editorconfig` and `.gitattributes`, especially UTF-8/CRLF for C#/Markdown and LF for YAML/scripts/web files.

## Future runtime review

Reevaluate .NET 8 end-of-support and vendor package status before production release. A future migration is a separate user decision and ADR, not permission for Codex to change these pins unilaterally.
