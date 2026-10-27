# Independent Sales and Security UI Labs

**Status:** selected implementation design, 2026-10-27; runtime changes are **NOT IMPLEMENTED** by the documentation PR. The sole task/status owner is the [Business Compact and independent UI Labs roadmap](../plans/20261027-001-business-compact-and-independent-ui-labs.md). Its native visual gates start **NOT APPROVED**.

**Inspected baseline:** [`main@8de4451573e8a9a033d7e5a0b07e5f22ef3018b9`](https://github.com/chung6a8m/MyDmsVn/commit/8de4451573e8a9a033d7e5a0b07e5f22ef3018b9). This design complements the durable [Business Compact policy](BUSINESS_COMPACT.md), [Security UI specification](SECURITY_ADMINISTRATION_UI.md), [current Lab runbook](../development/UI_LAB.md) and [Security review guide](../development/SECURITY_UI_LAB_REVIEW.md).

## 1. Outcome and boundary

Opening Security UI Lab must immediately create a Security workspace, with Security scenarios and review tools. Opening Sales UI Lab must do the equivalent for Sales. Neither startup, Reset nor session replacement may construct the other module's workspace or fixture as an intermediate step.

Each Lab must reproduce the product's presentation environment: the same theme construction and switching, typography, DPI behavior and measured workspace content bounds. The Lab continues to host the real production controls and view-models with deterministic fake ports. It remains SQL-free and does not compose Local, Server, DbMigrator, real Login or application configuration.

**Chosen scope:** two executable launchers in the existing repository and `MyDmsVn.UiLab.sln`, shared neutral host infrastructure, and independently owned module presentation/runtime graphs. Keep the production WinForms assembly, scenario assembly and UI test assembly shared initially. This provides independent launch and use; it does not promise independent compilation of all SAL and SEC source.

No new generic UI framework, toolkit copy, business API, permission rule, SQL contract or legacy Security factory cutover is selected. The production factory already uses `SecurityWorkspaceControl`; the roadmap improves the presentation of that existing component and verifies its composition in the product host.

## 2. Observed source and required changes

| Baseline fact | Design consequence |
|---|---|
| [`Program.Main`](https://github.com/chung6a8m/MyDmsVn/blob/8de4451573e8a9a033d7e5a0b07e5f22ef3018b9/samples/MyDmsVn.Desktop.UiLab/Program.cs) runs one `UiLabForm`; its [project](../../samples/MyDmsVn.Desktop.UiLab/MyDmsVn.Desktop.UiLab.csproj) is a net48 `WinExe`. | Add two explicit entry points; retain the existing assembly as reusable Lab code. |
| [`UiLabForm`](https://github.com/chung6a8m/MyDmsVn/blob/8de4451573e8a9a033d7e5a0b07e5f22ef3018b9/samples/MyDmsVn.Desktop.UiLab/UiLabForm.cs) initially selects Sales, contains both graphs and switches branches when the module selector changes. | Remove the module-switching path from the final design. Each module form owns only its own scenario, workspace and tools. |
| The Lab theme button uses `BootstrapTheme.CreateDefault`; [`FoundationShellForm`](../../src/MyDmsVn.Desktop.WinForms/FoundationShellForm.cs) uses `BusinessCompactAppearance.CreateTheme`. | Share the product's app-owned theme construction and bootstrap; changing only control colors is insufficient. |
| [`BusinessCompactAppearance.Apply`](../../src/MyDmsVn.Desktop.WinForms/BusinessCompactAppearance.cs) styles from the current theme; it does not install the product theme. | Initialize the theme before constructing the workspace and its dialogs, then use the same switching path. |
| Lab toolbar/journal consume space inside its outer form; product shell size is not workspace client size. | Measure the actual product workspace rectangle and reproduce that viewport independently of Lab diagnostics. |
| [`ScenarioInfrastructure`](https://github.com/chung6a8m/MyDmsVn/blob/8de4451573e8a9a033d7e5a0b07e5f22ef3018b9/samples/MyDmsVn.Desktop.UiLab.Scenarios/ScenarioInfrastructure.cs) includes both neutral utilities and Sales-specific types. | Separate neutral and module-specific source ownership without requiring new scenario assemblies. |
| [`SecurityReviewScenarioTests`](../../tests/MyDmsVn.Desktop.Ui.Tests/SecurityReviewScenarioTests.cs) waits for Sales before switching to Security. | Change the relevant test setup to direct Security startup; a test-name filter alone does not fix this dependency. |

The baseline already has production Security components, native tests and historical review evidence. This work must preserve their useful behavior and exact acceptance limits. New design documentation is not evidence that a new candidate renders correctly.

## 3. Project and ownership design

The following project changes and names are **implementation targets**, not files/APIs available at the inspected baseline:

| Target | Responsibility | Dependency boundary |
|---|---|---|
| `samples/MyDmsVn.Desktop.UiLab.Sales/` | Thin net48 `WinExe`: STA startup, shared bootstrap, launch Sales form. | References shared Lab assembly; no Security startup or selector. |
| `samples/MyDmsVn.Desktop.UiLab.Security/` | Thin net48 `WinExe`: STA startup, shared bootstrap, launch Security form. | References shared Lab assembly; no Sales startup or selector. |
| Existing `samples/MyDmsVn.Desktop.UiLab/` converted to a class library, retaining assembly name | Neutral host/lifecycle/presentation helpers plus module-owned forms under separate `Sales/` and `Security/` source areas. | References production WinForms and Scenarios as today. Common host code does not branch on SAL/SEC or store either module's graph. |
| Existing `samples/MyDmsVn.Desktop.UiLab.Scenarios/` | Deterministic fixture and fake-port implementations grouped into `Common/`, `Sales/`, `Security/`. | Remains free of WinForms, Local and SQL composition. |
| Existing `tests/MyDmsVn.Desktop.Ui.Tests/` | Common, SAL and SEC tests grouped by ownership, with direct module-host setup where needed. | Retains linked STA/native helpers and one serialized test assembly. |
| Existing `MyDmsVn.UiLab.sln` | Lists both launchers, reusable Lab code, Scenarios and UI tests. | No second solution or production project split is required. |

Keep launcher APIs small. A public entry/factory facade in the Lab assembly may expose the two launch choices while module forms and test hooks remain internal. Exact type/method names are chosen and recorded during implementation; this specification does not assert an existing `UiLabBootstrap` or `BusinessCompactPreset` API.

Retaining `MyDmsVn.Desktop.UiLab` as the reusable assembly preserves the existing production [`InternalsVisibleTo`](../../src/MyDmsVn.Desktop.WinForms/Properties/AssemblyInfo.cs) boundary. Its [test friend access](../../samples/MyDmsVn.Desktop.UiLab/Properties/AssemblyInfo.cs) can continue to serve `MyDmsVn.Desktop.Ui.Tests`. Do not make production internals public merely to let thin launchers start a form. Any changed friend/reference boundary must be explicit in the implementation diff.

Neutral host code owns the viewport, diagnostics placement, reset admission and the common lifecycle sequence. The module provides a narrow lifecycle adapter and owns creation, pending-operation cancellation/drain and cleanup of its own graph. Avoid duplicating the shared reset state machine in two forms or introducing a general plugin registry for two fixed launchers.

Sales owns its receipt, lookup race, held Reload, access variants, Save-error tools and scenario IDs. Security owns user/role/catalog fixtures, read/mutation holds, import preview, revision/conflict tools and Security scenario IDs. A tool must operate only on the currently owned module graph and its dialogs.

`ScenarioJournal`, `ScenarioGate<T>`, task tracking, fake session and generic errors can remain shared. `SalesScenarioId` and `ScenarioPreviewCalculator` belong with Sales source. Existing canonical Sales permission keys inside a Security catalog fixture are test data, not a Sales runtime dependency; keep the real keys needed to exercise Security behavior.

## 4. Presentation parity contract

### Theme and typography

Both launchers and the product host must use one app-owned Business Compact initialization path built on the pinned WinFormsKit theme manager. Reuse `BusinessCompactAppearance.CreateTheme` or an explicitly extracted shared equivalent; do not create a second manager or copy its palette into the Lab.

Initialize the theme before creating controls. Theme changes use the same factory and preserve supported theme state, including the existing reduced-motion setting. Apply the same typography/scaling ownership to workspaces and separately created dialogs. `Apply` alone is insufficient if the current theme is the toolkit default.

Dark is the primary visual reference; Light must retain the same structure, spacing, action hierarchy, focus and semantic readability. Each Lab runs as its own process, so one Lab's theme selection must not require or mutate the other Lab's session.

The exact toolkit baseline remains `vendor/MyDmsVn.WinFormsKit@c232a4c09dc0386a5bd8bb484a5c991acae827d5`, with nested SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`. Any proven dependency gap follows the separate upstream change/pin process; splitting launchers does not select an upgrade.

### Product content bounds

The product currently has normal outer shell size `1200 × 760` and minimum `900 × 600`. These are not the dimensions to assign blindly to the Lab workspace. During implementation, record the actual content-client rectangle after shell chrome, navigation and DPI scaling for the reviewed product state.

Give each Lab an explicit viewport reproducing that rectangle. Keep the Lab toolbar, journal and optional review tools outside it, using a separate diagnostics area or adjusting the outer Lab host without changing the target viewport. Showing or hiding tools must not silently change the reviewed viewport. If the monitor cannot fit the requested viewport and tools, report the constraint and arrange the tools separately; do not shrink the product text or claim a resized screenshot proves the target geometry.

Review normal and minimum product geometry at native Windows 100% and 125% scale. Evidence records outer form size, workspace client size, effective DPI, theme and text scaling. Logical design dimensions and device-pixel captures must be identified separately. Programmatic image enlargement is not native-DPI evidence.

Use the product's existing scaling behavior and shared typography. Do not increase the product minimum size, reduce font sizes or add workspace-wide horizontal scrolling merely to fit Lab controls. Local vertical scrolling and the existing approved responsive behavior remain available.

### Native behavior

The Lab hosts production controls, not a parallel visual recreation. Preserve keyboard order, native text/IME/caret behavior, focus, validation, disabled/busy behavior, accessible names and session invalidation. Review theme switching while representative controls and a target-named account dialog exist; no dialog may retain a mismatched default theme.

Use the [Business Compact control policy](BUSINESS_COMPACT.md) when selecting toolkit controls or documenting native exceptions. Lab chrome must be readable and compact, but tool-only controls must stay visually separate from the product viewport so they are not mistaken for product actions.

## 5. Runtime and lifecycle requirements

| ID | Required behavior |
|---|---|
| LAB-R01 | Sales launcher starts directly in Sales; Security launcher starts directly in Security. Neither constructs the other graph during startup, reset or session replacement. |
| LAB-R02 | Scenario picker and developer tools contain only the current module's actions. The final user flow has no SAL/SEC module-switch step. |
| LAB-R03 | Each reset closes admission, cancels the owned lifetime and pending gates, drains with finite bounds on an active WinForms message loop, disposes the old control/fixture, then constructs the replacement. |
| LAB-R04 | Drain/cleanup timeout poisons the host; no replacement graph is constructed and further reset cannot hide an unresolved operation. Preserve existing SAL and SEC failure semantics. |
| LAB-R05 | Close admission retains pending editor state/focus when rejected. Admitted close, sign-out/session replacement and disposal invalidate late callbacks. |
| LAB-R06 | Held mutation/import release controls bind to the originating Security graph and its owned modal. They must not attach to an unrelated window or survive graph replacement. |
| LAB-R07 | Both modules use the same production theme/bootstrap, typography and measured viewport contract; tool visibility does not alter the claimed review geometry. |
| LAB-R08 | Opening either Lab requires no SQL connection, real Login or private application configuration. Fake ports remain outside Desktop.App composition. |

The existing reset implementation has module-specific cancellation details. Preserve those details while extracting the common sequence; do not assume that disposing a fixture proves every task or control has drained. Hold/ignore-cancellation cases must retain bounded failure behavior and late-result rejection.

Each module owns its session and graph generation. Sharing neutral host code does not introduce shared mutable scenario/session state between running executables. Busy visuals remain presentation; they do not replace command authorization, editability or uncertain-outcome recovery.

## 6. Test organization and verification

Keep one `MyDmsVn.Desktop.Ui.Tests` assembly initially. Its [`AssemblyInfo`](../../tests/MyDmsVn.Desktop.Ui.Tests/AssemblyInfo.cs) disables test parallelization, and its [project](../../tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj) links the existing STA, guard, subprocess and native typing helpers. Preserve those boundaries; separate launcher processes do not authorize concurrent native GUI tests.

Change tests that actually construct the mixed `UiLabForm` to construct the correct module host directly. Existing Security control tests that already create `SecurityWorkspaceControl` directly need no wholesale migration. Preserve behavioral assertions instead of rewriting tests only to mirror the new class names.

Add or adapt focused proof of direct startup and module-only tools, reset/drain/poison, close/session ownership and presentation parity. Verify that Security startup creates no Sales fixture/control, and the reverse. Include affected shared-host Sales behavior; a SEC-only test run cannot establish safety of shared lifecycle changes.

Current source-verified class groups provide a starting point, subject to discovery after extraction:

| Scope | Baseline selector expression |
|---|---|
| Security | `FullyQualifiedName~Security` |
| Sales Lab behavior | `FullyQualifiedName~SalesScenarioBehaviorTests` OR `FullyQualifiedName~UiLabResetTests` OR `FullyQualifiedName~UiLabToolbarReviewTests` |
| Common native-driver ownership | `FullyQualifiedName~NativeDriverIndependenceTests` |

`FullyQualifiedName~Sales` alone misses the two SAL classes named `UiLabResetTests` and `UiLabToolbarReviewTests`. If implementation introduces module traits or renames classes, update selectors and verify actual nonempty discovery. Do not claim a filter fixes a test body that still starts Sales before Security.

The following illustrates a **future** focused run after the host changes have been implemented and built on interactive Windows; it is not a result of this documentation PR:

```powershell
$uiTests = 'tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj'
dotnet test $uiTests -c Debug -f net48 --no-restore `
  --list-tests --filter 'FullyQualifiedName~Security'
if ($LASTEXITCODE -ne 0) { throw 'Security test discovery failed.' }
# Inspect discovered cases before running; an empty selection is not acceptance.
dotnet test $uiTests -c Debug -f net48 --no-restore `
  --filter 'FullyQualifiedName~Security' --logger 'trx;LogFileName=ui-security.trx'
if ($LASTEXITCODE -ne 0) { throw 'Security UI tests failed.' }
```

Use the sole [TEST_POLICY](../TEST_POLICY.md), [TEST_STRATEGY](../TEST_STRATEGY.md) and [tests runbook](../../tests/README.md). Record exact candidate/test hashes, environment, discovered/executed rows and actual TRX outcomes. No old full-suite, SQL gate or removed test harness is inherited by this design.

## 7. Implementation sequence and launch instructions

The active roadmap owns dependency order and task state. Its intended sequence is: component/skill preparation (`UIL-T00`), shared appearance bootstrap (`UIL-T01`), independent launchers (`UIL-T02`), native Security pilot (`UIL-T03`) and Owner pilot approval (`UIL-T04`), complete Security presentation (`UIL-T05`), final native approval (`UIL-T06`), SQL-free product-host parity plus affected Sales regression (`UIL-T07`), then handoff (`UIL-T08`).

`UIL-T02` must change project types/references, solution entries, launcher programs, module forms, scenario source ownership and relevant tests together. It also updates current run instructions and output names; a documentation-only project rename is insufficient. Retire the mixed module selector from active startup and test paths, then remove obsolete mixed-host code once no active reference needs it.

After implementation, Visual Studio offers `MyDmsVn.Desktop.UiLab.Sales` and `MyDmsVn.Desktop.UiLab.Security` as explicit startup projects. Launching Security with F5 must not require first opening Sales. Existing runbook commands remain the authority for the currently implemented host until that task updates them.

These are **proposed future build commands**, not commands supported by the baseline checkout:

```powershell
dotnet restore MyDmsVn.UiLab.sln --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'UiLab locked restore failed.' }
dotnet build samples/MyDmsVn.Desktop.UiLab.Security/MyDmsVn.Desktop.UiLab.Security.csproj `
  -c Debug -f net48 --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Security Lab build failed.' }
dotnet build samples/MyDmsVn.Desktop.UiLab.Sales/MyDmsVn.Desktop.UiLab.Sales.csproj `
  -c Debug -f net48 --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Sales Lab build failed.' }
```

Read each successful build's actual executable path and bitness; do not assume an output folder. Any lock-file/project updates required by the extraction belong to the implementation task and must preserve dependency pins.

## 8. Acceptance and evidence

| Acceptance area | Required evidence |
|---|---|
| Independent launch | Both executable identities and direct module startup; no transient opposite-module fixture/control; correct scenario/tool set through reset and session replacement. |
| Lifecycle | Focused reset/drain/poison, close, stale completion and modal-owner results for affected paths, with finite waits and cleanup recorded. |
| Presentation parity | Same candidate in product host and module Lab, measured client bounds, common theme/bootstrap and typography, native 100%/125% captures at normal/minimum geometry in Dark/Light. |
| Security pilot | Role-permissions composition and one account dialog, control mapping/native-exception reasons, native editing/focus/theme checks and dated Owner decision for the exact candidate. |
| Completed Security candidate | Reviewed Users, Roles, Catalog and affected dialogs/states, remaining defects explicitly recorded, and separate dated Owner approval before the selected product-host integration check. |
| Handoff | Updated executable/run/test guidance, current project/API names, focused results, evidence limits and no claim of SQL/Login/package/release acceptance. |

Documentation approval selects this design and its roadmap. It does not approve native visuals. Historical SEC-T05 approval remains valid only for its recorded candidate and scope; it does not approve this later Business Compact revision. Mockup SVGs, isolated control screenshots and stretched captures cannot establish product-context parity.

The Security review guide supplies repeatable exercises and evidence fields. The active plan records current gate state; this specification remains the durable architecture/acceptance contract rather than a second task ledger. If a later task identifies a genuine service, SQL or toolkit API gap, record it and select the needed scope explicitly instead of expanding this presentation roadmap silently.
