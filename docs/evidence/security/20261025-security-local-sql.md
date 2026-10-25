# Security Administration — Local and SQL integration evidence

This record covers the post-gate SEC-T06/SEC-T07 candidate. The [Security plan](../../plans/20261025-002-security-administration-ui-lab-first.md) owns task state and the Owner decision. The Owner approved the SQL-free UI Lab source/test `8804cc181193c96c2cbb0a4626c68faf933627f9` on 2026-10-26, with the incomplete attended matrix retained as a limitation.

## SEC-T06 — session-bound Local cutover, 2026-10-26

| Item | Observed candidate |
|---|---|
| Source/test revision | `c2d3acac92f22d10e34362171a5c438912225919` (production cutover source introduced at `13d97b7`) |
| Toolkit pin | `vendor/MyDmsVn.WinFormsKit@c232a4c09dc0386a5bd8bb484a5c991acae827d5`; no vendor or schema change |
| Environment | Interactive Windows x64, .NET Framework 4.8 target, .NET SDK 8.0.426 build tool, physical monitor scale 100% for native UI run |
| Changed path | Production `SecurityAdministrationControlFactory.Create()` captures one session version and constructs `SecurityWorkspaceControl` with bound administration/catalog ports and the same view-model owner. The coordinator retains that bound administration instance. Both Local ports compare the captured actor/generation inside `SendForCurrentSessionAsync` before account/permission/handler admission and fence late responses. Unbound consumers retain their existing `SendAsync` path. |
| Shell/lifecycle | The Security tab uses normal workspace close admission via the candidate's `IWorkspaceCloseParticipant`; activation, sign-out disposal and feedback host remain in the shell. Legacy control/view-model and their duplicate tests were retired; their shared factory/activation interfaces moved to `SecurityWorkspaceContracts.cs`. |
| Release graph | Locked restore and `dotnet build src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -c Release -f net48 --no-restore`: PASS, 0 warnings/errors. The Desktop.App graph contains no UI Lab scenario/sample project reference. |

### Test obligation mapping

| Retired legacy owner | Current source-verified owner |
|---|---|
| `SecurityAdministrationViewModelTests`: catalog projection, direct decisions, conflict, protected identity, lifecycle, Local authorization | `SecurityPermissionProjectionTests`, `SecurityPaneStateTests`, `SecurityWorkspaceLifetimeTests`, `LocalSecurityAdministrationApiClientTests` |
| `PermissionImportViewModelTests`: exact preview JSON, selected target and revision, invalidation | `SecurityPermissionImportTests` (coordinator and mounted control) |
| `SecurityControlAcceptanceTests`: user/role commands, membership revisions, read-only catalog, import, errors, theme, focus and late results | `SecurityWorkspaceControlTests`, `SecurityWorkspaceLifetimeTests`, `SecurityPermissionImportTests`, `SecurityNativeAndResetTests` |
| `SecurityAdministrationFormTests`: STA creation/disposal, DataError guard, shell sign-out | Migrated in place to `SecurityWorkspaceControl`; `FoundationShellFormTests.Clean_security_tab_uses_normal_workspace_close_lifecycle` covers close/reopen admission |

The factory type test and shell close test were observed RED against the old production path and GREEN after cutover. The port binding reflection test was RED before `ForSession` existed and GREEN after implementation. The bound Local tests exercise replacement/sign-out before admission, replacement during account validation, admitted late security/catalog reads, late old Unauthorized, same/different-version rebinding, serial item boundary, request field/digest and permission-key parity. They assert `401 Auth.SessionChanged`, no admission where applicable, no old data reaching the replacement session and no expiry of that session. A fenced old mutation does not prove rollback.

### Executed verification

| Command/selector | Actual result | Local raw TRX |
|---|---|---|
| `dotnet test tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj -c Debug -f net48 --no-restore --filter FullyQualifiedName~LocalSecurityAdministrationApiClientTests` | 8 discovered/executed, 8 PASS, 0 failed/skipped/not-executed | `TestResults/sec-t06-parity/sec-t06-parity.trx` |
| Session project selected `DesktopSessionProjectContractTests`, Local Security and catalog client tests | 8 discovered/executed, 8 PASS, 0 failed/skipped/not-executed (before the final three additional Local cases) | `TestResults/sec-t06-retire-session/sec-t06-retire-session.trx` |
| Desktop project selected `SecurityAdministrationFormTests`, `FoundationShellFormTests`, factory composition | 28 discovered/executed, 28 PASS, 0 failed/skipped/not-executed | `TestResults/sec-t06-retire-desktop/sec-t06-retire-desktop.trx` |
| Full SQL-free `MyDmsVn.Desktop.Ui.Tests` Debug/net48 with `MYDMSVN_UIN_EXPECTED_SCALE=100` | 196 discovered/executed, 196 PASS, 0 failed/skipped/not-executed; run after legacy source removal, before four final Session-only test additions | `TestResults/sec-t06-final-ui-native100/sec-t06-final-ui-native100.trx` |

No SQL command was run for SEC-T06. This Local cutover does not establish real Login, package, SQL or product acceptance.

## SEC-T07 — focused SQL integration

Pending execution on the post-gate Local candidate.