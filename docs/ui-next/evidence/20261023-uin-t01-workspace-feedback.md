# UIN-T01 — originating workspace feedback

Date: 2026-10-23. Scope: UIN-T01 / UIN-A02, app-only `net48` Desktop.Application and WinForms. This records focused hosted automation, not packaged Login, attended acceptance or production readiness.

## Candidate and ownership

Roadmap/design baseline: UI-NEXT `7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639`. Actual execution parent: `0da4d975a8391910c2be27be42ea493ecafce179` (merged PR #44); T00 predecessor `7b7135eccd5f83fde1c751d82713cc582a2e33ec`. T01 source/test candidate is identified by the implementation commit containing these changes and the file hashes recorded below after review repairs. No package was built for this scope.

Vendor pins remain unchanged: BSG `2bd7874f7e80b22ef181763749c2a2938ab7f16e`, UI `2be9a4d778fda7016207b75470020be6126f2f3f`, SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`. No SQL, schema, business posting or vendor source change is selected.

`WorkspaceFeedbackScope` owns a fresh opaque workspace ID, authenticated-session generation, document generation and independent per-key Query/Write operation generations. Sinks capture before awaiting; publication and dispatched host work both recheck freshness. Document change, close/disposal and session teardown invalidate old publications. A list query cannot supersede a write outcome. A reopened module uses a different workspace ID.

The legacy `IDesktopNotificationService` remains available globally. Catalog composition supplies a workspace adapter; Load/Select/Save/SetActive capture at the ViewModel boundary, including message-triggered background reloads. Product Save and Activate/Deactivate success are emitted from authoritative current responses before list refresh. Catalog scopes also compare the live session version during publication and host dispatch, so a worker-thread session change rejects pending feedback before UI cleanup. Stale editor success cannot send a CatalogChanged message or reload the replacement document. Existing field metadata and validation behavior remain intact.

The shell hosts each actual control in `WorkspaceFeedbackHost`, unwraps Content for activation, and disposes content/scope with its page. Persistent Alert and bounded local Toast reuse the pinned UI. Inactive actionable feedback adds a tab attention marker without selecting or focusing it. Acknowledgment clears the marker, while persistent error/recovery remains. Toast policy is latest inactive success for at most five minutes; three visible and five pending; five seconds visible time; coalescing refreshes occurrence/time. Information retains Info semantics. Authoritative same-operation success clears resolved actionable feedback; explicit captured `ClearActionable()` handles successful recovery. Ordinary queries and failed recovery do not clear unresolved state.

Sales captures Save/Post sinks at command admission. Unknown Create/Post outcome continues to be authoritative editor state and retains the mutation lock. Validated recovery explicitly clears its resolved Alert; editor permission denial scrubs protected document and feedback. No automatic Post replay was added.

## Test evidence

Environment: interactive Windows, .NET Framework 4.8, repository-selected SDK; Session tests Release, GUI tests Debug. GUI checks use serialized `StaTest` / `WinFormsTestGuard`, finite waits and deterministic control disposal. These are hosted control checks; no independent native125, theme-matrix, package or attended claim is made.

Original failures remain local in TRX:

| Run | Selected/executed | Outcome and repair |
|---|---:|---|
| Scope RED | 3 | 1 PASS / 2 FAIL: old query and reopened/document contexts were accepted by the initial fence stub |
| Scope GREEN | 3 | 3 PASS / 0 FAIL / 0 SKIP after identity/generation checks |
| Catalog feedback RED | 2 | 0 PASS / 2 FAIL: no captured query or authoritative Save feedback |
| Catalog + scope GREEN | 40 | 40 PASS / 0 FAIL / 0 SKIP after ViewModel capture and authoritative success |
| First host attempt | 4 | 2 PASS / 2 FAIL: vendor rejects zero AnimationDuration; repaired by using supported default |
| Host freshness mutation RED | 1 | 1 FAIL when dispatch freshness recheck was temporarily removed; source restored |
| Host/shell/catalog before final review | 37 | 37 PASS / 0 FAIL / 0 SKIP |
| Pre-review focused Session candidate | 41 | 41 PASS / 0 FAIL / 0 SKIP |
| Pre-review host/shell/catalog candidate | 38 | 38 PASS / 0 FAIL / 0 SKIP |

Review repairs were verified separately:

| Run | Executed | Outcome |
|---|---:|---|
| Host review RED: retry / explicit clear / coalesced timer | 13 | 10 PASS / 3 FAIL, then same host selector GREEN 13 PASS |
| Independent Query error versus Save success RED | 1 | 1 FAIL, then full host GREEN 14 PASS |
| Older recovery clear versus newer Query error RED | 1 | 1 FAIL, then full explicit-x86 host GREEN 15 PASS |
| Busy rejected duplicate Save RED | 1 | 1 FAIL, then focused Session GREEN 43 PASS |
| Final handoff Session before Sales/review repair | 44 | 44 PASS / 0 FAIL / 0 SKIP: 38 Catalog rows plus 6 scope rows, including both A/B completion orders |
| Final handoff GUI before Sales/review repair, explicit x86 | 45 | 45 PASS / 0 FAIL / 0 SKIP: 15 host rows plus 30 shell/catalog rows |
| Sales pilot Session | 26 | 26 PASS / 0 FAIL / 0 SKIP: editor mutation/recovery feedback |
| Sales pilot GUI, explicit x86 | 34 | 34 PASS / 0 FAIL / 0 SKIP: editor denial, unknown Post recovery and admission |
| Integrated focused Session after final review repairs | 72 | 72 PASS / 0 FAIL / 0 SKIP: Catalog, scope and Sales editor selectors; includes live-session fence and Activate/Deactivate result-before-refresh regression |
| T00/T01 integrated Session | 76 | 76 PASS / 0 FAIL / 0 SKIP: preceding 72 plus four DesktopViewModelBase rows affected by shared catalog error behavior |
| Integrated focused GUI after final review repairs, explicit x86 | 69 | 69 PASS / 0 FAIL / 0 SKIP: host, shell, Catalog and Sales selectors; overlapping earlier selectors are counted once |

Final TRX inspection confirms nonempty discovery and execution. The final GUI command rebuilt the repaired app and host with explicit x86 build and test-host targets. The integrated final selected scope is 76 Session plus 69 GUI invocations, all PASS with zero skips; these are two distinct project runs. Review fixes preserve independent failures: Success clears only same-key/same-kind actionable state; explicit recovery clear cannot remove an Alert with a newer generation. Coalesced Toast refreshes message, semantic variant, occurrence title and the full five-second timer through the pinned public AutoHide setter.

Final focused commands:

```powershell
dotnet test tests/MyDmsVn.Desktop.Session.Tests -c Release -f net48 --no-restore --filter 'FullyQualifiedName~CatalogViewModelTests|FullyQualifiedName~WorkspaceFeedbackScopeTests' --logger 'trx;LogFileName=t01-catalog-scope-handoff.trx' --results-directory TestResults/uin-t01-catalog-scope-handoff
dotnet test tests/MyDmsVn.Desktop.Tests -c Debug -f net48 --no-restore -p:PlatformTarget=x86 --filter 'FullyQualifiedName~WorkspaceFeedbackHostTests|FullyQualifiedName~FoundationShellFormTests|FullyQualifiedName~CatalogControlTests' --logger 'trx;LogFileName=t01-host-shell-catalog-handoff.trx' --results-directory TestResults/uin-t01-host-shell-catalog-handoff -- RunConfiguration.TargetPlatform=x86
dotnet test tests/MyDmsVn.Desktop.Session.Tests -c Release -f net48 --no-restore --filter 'FullyQualifiedName~CatalogViewModelTests|FullyQualifiedName~WorkspaceFeedbackScopeTests|FullyQualifiedName~SalesReceiptEditorViewModelTests' --logger 'trx;LogFileName=uin-t01-all-session-final.trx' --results-directory TestResults/uin-t01-all-session-final
dotnet test tests/MyDmsVn.Desktop.Session.Tests -c Release -f net48 --no-restore --filter 'FullyQualifiedName~CatalogViewModelTests|FullyQualifiedName~WorkspaceFeedbackScopeTests|FullyQualifiedName~SalesReceiptEditorViewModelTests|FullyQualifiedName~DesktopViewModelBaseTests' --logger 'trx;LogFileName=uin-t00-t01-integrated-session.trx' --results-directory TestResults/uin-t00-t01-integrated-session
dotnet test tests/MyDmsVn.Desktop.Tests -c Debug -f net48 --no-restore -p:PlatformTarget=x86 --filter 'FullyQualifiedName~WorkspaceFeedbackHostTests|FullyQualifiedName~FoundationShellFormTests|FullyQualifiedName~CatalogControlTests|FullyQualifiedName~SalesReceiptControlTests|FullyQualifiedName~SalesReceiptAdmissionTests' --logger 'trx;LogFileName=uin-t01-all-gui-final.trx' --results-directory TestResults/uin-t01-all-gui-final -- RunConfiguration.TargetPlatform=x86
```

Raw TRX remains local under `TestResults/uin-t01-*` and `tests/MyDmsVn.Desktop.Tests/TestResults/uin-t01-*`; sanitized counts are published here. No connection strings, SQL identifiers or screenshots are included. No SQL allocation occurred. GUI controls, timers, toasts and scopes were disposed; no unresolved resource was reported by the selected cases.

## Remaining boundary

T02 owns the complete field/cell invalid-state visual pilot. Later tasks retain their own close/busy/theme/adoption and final package/Owner gates. This evidence neither reopens nor completes archived SAL/native125/attended/release work. Focused UIN-A02 application/session/host behavior is verified by the selected cases; the A02 row remains PARTIAL because Light/Dark native100/native125 composition was not run. Package, actual Login and Owner checks remain separate.

## Final candidate file identity

SHA-256 of saved working-tree source/test bytes at final handoff (no package):

| File | SHA-256 |
|---|---|
| `src/MyDmsVn.Desktop.Application/WorkspaceFeedbackScope.cs` | `ed00e2b4036e6767a418feba34252cc8c74610484917fff2e8e6eb14d8b50f5a` |
| `src/MyDmsVn.Desktop.Application/CatalogViewModel.cs` | `8421f1b42652f9d11eb99794772f45ca9f74388e3082b31cc049342bd0d66409` |
| `src/MyDmsVn.Desktop.Application/SalesReceiptEditorViewModel.cs` | `e33f1015554ce69e044b71275463b6db12cc1aa790b3971e23af7fd50dc9da61` |
| `src/MyDmsVn.Desktop.WinForms/WorkspaceFeedbackHost.cs` | `4f4d9d20c591e7c866f5b03452fcc28bee2c901a85e957f68d89f477f89a5126` |
| `src/MyDmsVn.Desktop.WinForms/CatalogControl.cs` | `cf5ba63c87d235bc89e08230e4c09062f45d4925ba20701fd1d814becf9286bb` |
| `src/MyDmsVn.Desktop.WinForms/DesktopServiceCollectionExtensions.cs` | `880735041bd54df1288c4e20eaa829d462be1db610b1619534e5a22971820f96` |
| `src/MyDmsVn.Desktop.WinForms/FoundationShellForm.cs` | `bb27dcd0ab7e113c0ebdff448c4c3d97a9992360cf22e4a0f749ac249ae992ab` |
| `src/MyDmsVn.Desktop.WinForms/SalesReceiptControl.cs` | `eac3621e7c09c47a696751c2d565e22a6252186369960616f05773eafdfbb49a` |
| `tests/MyDmsVn.Desktop.Session.Tests/WorkspaceFeedbackScopeTests.cs` | `e0b45e281347dbbe1044b27b951dcca6c52c09b5cf70ae14dd7d4ba02ee19db6` |
| `tests/MyDmsVn.Desktop.Session.Tests/CatalogViewModelTests.cs` | `d8d57d5a702cf33220c27ea380f86d84c7a7cfd350156a4288474b33372d1539` |
| `tests/MyDmsVn.Desktop.Session.Tests/SalesReceiptEditorViewModelTests.cs` | `8491d906e622100b59e283fc4375da5d6213973be737bab182da9f93daad30d4` |
| `tests/MyDmsVn.Desktop.Tests/WorkspaceFeedbackHostTests.cs` | `42e68ef701ce5ff81f9200e87db462390960ca8005d03ef38ccb5f0ea960e6d2` |
| `tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs` | `337264c60c3aecb20cca0545425c76b3e8b92eeaf24c225a42b9e27ddd44b6c8` |
| `tests/MyDmsVn.Desktop.Tests/SalesReceiptControlTests.cs` | `44000b528e08990b270639abe8d730215a4e407646ac7786ace558de82e6e4bd` |
