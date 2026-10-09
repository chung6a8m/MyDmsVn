# P5.1 Master Data UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete P5.1-T03, P5.1-T04, and the automatable P5.1 quality gate for Product, Warehouse, Employee, and Customer catalogs.

**Architecture:** Keep all asynchronous catalog behavior in `Desktop.Application`: a testable delay-backed debouncer, UI dispatcher abstraction, typed process-local change messages, reusable catalog list/edit ViewModels, and active-only lookup ViewModels. `Desktop.WinForms` owns only controls, binding, UI-thread dispatch, and lifetime forwarding. Both hosts register the same desktop services and consume the existing `IxxxApiClient` contracts; no server or transport types cross the desktop boundary.

**Tech Stack:** C#; `net48`, `net8.0`, and `net8.0-windows`; CommunityToolkit.Mvvm 8.4.2; Microsoft DI; WinForms; xUnit.

**Spec:** `docs/plans/20261008-001-sales-inventory-foundation.md` (P5.1-T03, P5.1-T04, and P5.1 gate)

## Global Constraints

- Preserve the existing `net48`, `net8.0`, and `net8.0-windows` targets; do not use APIs unavailable on .NET Framework 4.8.
- Desktop UI and ViewModels depend only on desktop abstractions and Contracts, never directly on Server.Infrastructure.
- Use a shared per-host `WeakReferenceMessenger`; messages are desktop-local and are published only after a successful committed API mutation.
- Default text-input debounce is exactly 300 ms and configurable per ViewModel.
- Every asynchronous reload uses cancellation plus a monotonically increasing generation so a late stale response cannot overwrite newer state.
- New lookup selections contain active rows only; an existing inactive selection keeps its ID and historical label and is visibly unavailable.
- WinForms tests create and dispose controls on an STA thread and must never show modal error dialogs.

## Review Focus

- An API that ignores cancellation and completes an old search last must not replace the current result; Task 1 and Task 3 include late-response tests.
- Disposal during a pending delay or API call must prevent notifications and UI-bound state changes; Tasks 1 through 4 include disposal tests.
- A failed Create, Update, or SetActive response must publish zero catalog-change messages; Task 3 covers every mutation kind.
- Bursts of relevant messages must coalesce while unrelated catalog kinds are ignored; Task 2 covers two recipients, burst coalescing, and disposal.
- Reopening or reactivating a screen must fetch authoritative data even if process-local messages were missed; Tasks 2 and 4 cover activation refresh.

---

### Task 1: Testable debounce and UI dispatch infrastructure

**Files:**
- Create: `src/MyDmsVn.Desktop.Application/AsyncDelay.cs`
- Create: `src/MyDmsVn.Desktop.Application/DebouncedAsyncAction.cs`
- Create: `src/MyDmsVn.Desktop.Application/UiDispatcher.cs`
- Create: `tests/MyDmsVn.Desktop.Tests/DebouncedAsyncActionTests.cs`
- Create: `tests/MyDmsVn.Desktop.Tests/UiDispatcherTests.cs`

**Interfaces:**
- Produces: `IAsyncDelay.DelayAsync(TimeSpan, CancellationToken)`, `DebouncedAsyncAction.Schedule(Func<CancellationToken, Task>)`, `DebouncedAsyncAction.Cancel()`, `IUiDispatcher.InvokeAsync(Action, CancellationToken)`, and deterministic disposal.
- Consumers: Tasks 2 and 3 use these types for search and message-triggered reloads.

- [ ] **Step 1: Write failing tests for final-call debounce, superseded cancellation, immediate cancellation, and no callback after disposal.**

  Use a controllable fake `IAsyncDelay`; assert rapid schedules execute only the final operation after the fake releases it, and disposing before release executes nothing.

- [ ] **Step 2: Run the focused tests and verify RED because the async primitives do not exist.**

  Run: `dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj --no-restore --filter "FullyQualifiedName~DebouncedAsyncActionTests|FullyQualifiedName~UiDispatcherTests"`

  Expected: FAIL at compile with missing `IAsyncDelay`, `DebouncedAsyncAction`, or `IUiDispatcher`.

- [ ] **Step 3: Implement the minimal primitives.**

  `SystemAsyncDelay` wraps `Task.Delay`; `DebouncedAsyncAction` owns one cancellation source and invalidates scheduled work on replace/cancel/dispose; `SynchronizationContextUiDispatcher` posts to its captured context and completes a returned Task without executing after cancellation.

- [ ] **Step 4: Run the focused tests and verify GREEN on both desktop test TFMs.**

  Expected: all selected tests pass for `net48` and `net8.0-windows`.

- [ ] **Step 5: Commit.**

  `git commit -m "feat: add desktop debounce infrastructure"`

### Task 2: Typed catalog messages and reusable lookup state

**Files:**
- Create: `src/MyDmsVn.Desktop.Application/CatalogChangedMessage.cs`
- Create: `src/MyDmsVn.Desktop.Application/CatalogLookupViewModel.cs`
- Modify: `src/MyDmsVn.Desktop.Infrastructure.Local/DependencyInjection.cs`
- Create: `tests/MyDmsVn.Desktop.Tests/CatalogLookupViewModelTests.cs`
- Modify: `tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs`

**Interfaces:**
- Consumes: Task 1 debounce/dispatcher; existing `CatalogLookupDto` and `IProductApiClient`/`IWarehouseApiClient`/`IEmployeeApiClient`/`ICustomerApiClient`.
- Produces: `CatalogKind`, `CatalogChangeOperation`, `CatalogChangedMessage`, `CatalogLookupOption`, `ICatalogLookupSource`, and `CatalogLookupViewModel` with `ActivateAsync`, `SetSearch`, `RefreshAsync`, `SetSelection`, and `Dispose`.

- [ ] **Step 1: Write failing tests for one shared messenger instance, relevant-only refresh, burst coalescing, two recipients, authoritative activation refresh, disposal, stale-response discard, and inactive historical selection preservation.**

  Assert a deactivated selected ID remains represented with its historical label and `IsAvailableForNewSelection == false`, while inactive rows returned unexpectedly by lookup cannot become new selections.

- [ ] **Step 2: Run the focused tests and verify RED because the message and lookup types do not exist.**

  Run: `dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj --no-restore --filter "FullyQualifiedName~CatalogLookupViewModelTests|FullyQualifiedName~DesktopCompositionTests"`

- [ ] **Step 3: Implement typed messages, per-catalog lookup adapters, lookup state, and `IMessenger` singleton registration backed by `WeakReferenceMessenger`.**

  Messenger callbacks schedule through `IUiDispatcher`; relevant bursts use the Task 1 debouncer; `ActivateAsync` and explicit refresh bypass the debounce.

- [ ] **Step 4: Run the focused tests and verify GREEN on both TFMs.**

- [ ] **Step 5: Commit.**

  `git commit -m "feat: add catalog change lookup refresh"`

### Task 3: Four catalog list/edit ViewModels

**Files:**
- Create: `src/MyDmsVn.Desktop.Application/CatalogViewModel.cs`
- Create: `src/MyDmsVn.Desktop.Application/ProductCatalogViewModel.cs`
- Create: `src/MyDmsVn.Desktop.Application/WarehouseCatalogViewModel.cs`
- Create: `src/MyDmsVn.Desktop.Application/EmployeeCatalogViewModel.cs`
- Create: `src/MyDmsVn.Desktop.Application/CustomerCatalogViewModel.cs`
- Create: `tests/MyDmsVn.Desktop.Tests/CatalogViewModelTests.cs`

**Interfaces:**
- Consumes: Task 1 debounce/dispatcher, Task 2 messages/messenger, existing feature API clients and contract DTOs.
- Produces: bindable list/edit state and `ActivateAsync`, `SetSearch`, `RefreshAsync`, `MoveToPageAsync`, `BeginCreate`, `SelectAsync`, `SaveAsync`, `SetActiveAsync`, `CancelCurrentOperation`, and `Dispose` for each catalog.

- [ ] **Step 1: Write failing deterministic tests for the shared list behavior.**

  Cover 300 ms default debounce, rapid typing issuing one final request, page reset to 1, immediate Refresh, cancellation plus generation stale-response protection, loading/empty/error state, and no state updates after disposal.

- [ ] **Step 2: Run the focused list tests and verify RED because catalog ViewModels do not exist.**

- [ ] **Step 3: Implement minimal shared paging/search state and the four typed API adapters.**

  Keep request construction in the concrete catalog ViewModels; the reusable base owns state transitions and generation checks without referring to WinForms.

- [ ] **Step 4: Run the list tests and verify GREEN.**

- [ ] **Step 5: Write failing mutation tests for all four catalogs.**

  Assert Create/Update/SetActive success refreshes displayed data and publishes exactly one message containing the correct kind, entity ID, and operation; validation/auth/conflict/exception/cancellation publishes none and maps field errors through `DesktopViewModelBase`.

- [ ] **Step 6: Run mutation tests and verify RED for the missing mutation behavior.**

- [ ] **Step 7: Implement typed editors and success-only publication.**

  Save requests remain catalog-specific; no Domain, MediatR, or infrastructure type enters the ViewModels.

- [ ] **Step 8: Run all `CatalogViewModelTests` and verify GREEN on both TFMs.**

- [ ] **Step 9: Commit.**

  `git commit -m "feat: add catalog desktop view models"`

### Task 4: Catalog WinForms screens and host composition

**Files:**
- Create: `src/MyDmsVn.Desktop.WinForms/CatalogControl.cs`
- Create: `src/MyDmsVn.Desktop.WinForms/DesktopServiceCollectionExtensions.cs`
- Modify: `src/MyDmsVn.Desktop.WinForms/FoundationShellForm.cs`
- Modify: `src/MyDmsVn.Desktop.App/Program.cs`
- Modify: `src/MyDmsVn.Desktop.AppCore/Program.cs`
- Create: `tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs`
- Modify: `tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs`
- Modify: `tests/MyDmsVn.Desktop.Tests/DesktopHostRunnerTests.cs`

**Interfaces:**
- Consumes: Task 3 ViewModels and existing Bootstrap/SourceGrid controls.
- Produces: keyboard-friendly catalog navigation and controls that bind state, forward search/create/edit/activate/deactivate/cancel/refresh input, marshal state updates to the owning UI thread, activate on open, and dispose ViewModels with their tab lifetime.

- [ ] **Step 1: Write failing STA tests for compact layout, field-error binding, keyboard/tab order, command forwarding, active/inactive display, background PropertyChanged marshaling, tab reuse, and disposal on close/sign-out.**

- [ ] **Step 2: Run the focused STA tests and verify RED because the catalog control/composition does not exist.**

  Run: `dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj --no-restore --filter "FullyQualifiedName~CatalogControlTests|FullyQualifiedName~FoundationShellFormTests|FullyQualifiedName~DesktopHostRunnerTests"`

- [ ] **Step 3: Implement the WinForms control, navigation wiring, and one shared desktop composition extension used identically by both hosts.**

  Views issue no API calls and publish no messages directly; every event forwards to a ViewModel method. Keep UI errors non-modal and captured by the existing test guard.

- [ ] **Step 4: Run the focused STA tests and both host smoke modes.**

  Run tests as in Step 2, then `dotnet run --project src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -f net48 --no-restore -- --smoke-test` and `dotnet run --project src/MyDmsVn.Desktop.AppCore/MyDmsVn.Desktop.AppCore.csproj -f net8.0-windows --no-restore -- --smoke-test`.

  Expected: all tests pass; both processes exit 0 without modal hangs.

- [ ] **Step 5: Commit.**

  `git commit -m "feat: add WinForms catalog screens"`

### Task 5: P5.1 quality gate and evidence

**Files:**
- Modify: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/CatalogBackendIntegrationTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/CatalogMappingIntegrationTests.cs`
- Create: `docs/P5_1_VERIFICATION.md`
- Modify: `docs/plans/20261008-001-sales-inventory-foundation.md`
- Modify: `docs/DEVELOPMENT_PLAN.md`

**Interfaces:**
- Consumes: all prior tasks and existing SQL Server disposable test database infrastructure.
- Produces: evidence for uniqueness/collation, Employee.UserId FK, inactive historical Get/List versus active-only Lookup, permission denial, deterministic desktop behavior, messenger lifetime, dual-target builds, and host smoke.

- [ ] **Step 1: Audit existing SQL tests and write only missing failing gate cases.**

  Add direct database/API assertions for case-insensitive unique codes on every catalog, Employee.UserId FK rejection, inactive row visibility through Get/List while Lookup excludes it, and denied writes leaving business tables unchanged.

- [ ] **Step 2: Run the focused SQL tests and verify any new tests fail for the intended uncovered behavior; if an assertion already passes, record it as existing evidence instead of adding a change-detector test.**

  Run: `dotnet test tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~Catalog"`

- [ ] **Step 3: Implement only production fixes exposed by genuine RED gate tests, using a separate RED→GREEN cycle for each finding.**

- [ ] **Step 4: Run full verification.**

  Run: `dotnet build MyDmsVn.sln --no-restore`; `dotnet test MyDmsVn.sln --no-build --no-restore`; both host smoke commands from Task 4.

  Expected: both target families build, all tests pass (SQL against the disposable test database only), and both smoke processes exit 0.

- [ ] **Step 5: Record exact commands/counts in `docs/P5_1_VERIFICATION.md` and update only acceptance checkboxes supported by the evidence.**

  Leave interactive manual-smoke criteria unchecked if no human-observed GUI session was performed; do not mark all P5 complete.

- [ ] **Step 6: Commit.**

  `git commit -m "test: complete P5.1 quality gate"`
