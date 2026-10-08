# Test harness

P0 tests require Windows, the .NET 8 SDK selected by `global.json`, and the .NET Framework 4.8 targeting pack/runtime. Run from the repository root:

```powershell
dotnet restore MyDmsVn.sln --locked-mode
dotnet build MyDmsVn.sln -c Release --no-restore
dotnet test MyDmsVn.sln -c Release --no-build --no-restore
```

WinForms tests create and dispose controls on an STA thread through `StaTest`. Each action receives a cancellation token; a timeout cancels the action and waits for cleanup before returning. Actions that cannot cooperate with cancellation, including fatal-process scenarios, must run in a bounded subprocess.

`WinFormsTestGuard` captures `Application.ThreadException` and `DataGridView.DataError`, subscribes to `AppDomain.UnhandledException` as last-chance diagnostics, and enables the Windows thread error mode that suppresses critical-error and general-protection-fault dialogs. Unconsumed captured exceptions fail the test when the guard is disposed; a test that intentionally raises an expected exception must consume it with `DrainExceptions()` and assert it. An `AppDomain.UnhandledException` notification cannot prevent process termination, so tests must catch/await worker failures before they become unhandled and use subprocess isolation when intentionally testing fatal behavior.

P0 contains no SQL integration tests, database connection fallback, provisioning, reset, drop, or migration action. P1 must require the explicit `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` environment variable and an isolated disposable test database before adding any destructive test-only setup.
