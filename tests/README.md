# Test harness

P0 tests require Windows, the .NET 8 SDK selected by `global.json`, and the .NET Framework 4.8 targeting pack/runtime. Run from the repository root:

```powershell
dotnet restore MyDmsVn.sln --locked-mode
dotnet build MyDmsVn.sln -c Release --no-restore
dotnet test MyDmsVn.sln -c Release --no-build --no-restore
```

WinForms tests create and dispose controls on an STA thread through `StaTest`. `WinFormsTestGuard` captures `Application.ThreadException`, `AppDomain.UnhandledException`, and `DataGridView.DataError` so failures are asserted instead of opening default modal dialogs. UI work must keep finite timeouts and deterministic cleanup.

P0 contains no SQL integration tests, database connection fallback, provisioning, reset, drop, or migration action. P1 must require the explicit `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` environment variable and an isolated disposable test database before adding any destructive test-only setup.
