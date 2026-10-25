# Security Administration — Local and SQL integration evidence

This record covers the post-gate SEC-T06–SEC-T08 candidate. The [Security plan](../../plans/20261025-002-security-administration-ui-lab-first.md) owns task state and the Owner decision. The Owner approved the SQL-free UI Lab source/test `8804cc181193c96c2cbb0a4626c68faf933627f9` on 2026-10-26, with the incomplete attended matrix retained as a limitation.

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

## SEC-T07 — focused SQL integration, 2026-10-26

Source/test `c09feb51ca079ce20b2b45d4fe0ad50c5e0b4908` adds four `SecurityWorkspaceSqlIntegrationTests` against the real session-bound Local port and application SQL fixture. The fourth test was added after the initial three-case SQL run. The effective test connection was verified as explicitly configured, nonproduction, `master`, without AttachDbFilename or User Instance; the value and proprietary identifiers are not recorded. The existing APP reusable fixture ran serially with owned allocation, reset/seed/verify and successful fixture drop. No schema, procedure, transport or vendor pin changed.

| Verification | Actual result | Local raw TRX |
|---|---|---|
| Four new Local/SQL methods: serial no-op and revision chain, partial conflict after committed first item, membership revision invalidating a draft, validation envelope without write | 4 discovered/executed, 4 PASS, 0 failed/skipped/not-executed; fixture drop succeeded | `TestResults/sec-t07-final-new/sec-t07-final-new.trx` |
| Final selected set: four new methods plus five existing Security SQL methods specified by plan section 4 | 9 discovered/executed, 9 PASS, 0 failed/skipped/not-executed; fixture drop succeeded | `TestResults/sec-t07-final-focused/sec-t07-final-focused.trx` |
| Three directly relevant existing import preview/apply, actor recheck and inactive/stale cases | 3 discovered/executed, 3 PASS, 0 failed/skipped/not-executed; fixture drop succeeded | `TestResults/sec-t07-import-sql/sec-t07-import-sql.trx` |
| `SecurityAdministrationTests` application authorization and validation selector | 5 discovered/executed, 5 PASS, 0 failed/skipped/not-executed | `TestResults/sec-t07-application/sec-t07-application.trx` |

The serial Save confirmed a no-op first key at revision 0, committed the second at revision 1, and matched final grants and one actor-linked audit. In the interleaved Save, the first key committed, a competing Local command committed at revision 2, the next key conflicted and the third was never attempted; two actor-linked audit events and database grants show that the first transaction was retained. A membership mutation advanced both user and role revisions, created membership and audit, then rejected the old role draft without a permission write. Existing selected SQL cases cover direct Deny precedence, inactive/protected targets, audit-failure rollback for one command, concurrency, and import revision/digest rechecks. The new Local invalid-key response was `BadRequest` / aggregate `ValidationError`, with `PredicateValidator` detail on `permissionKeyValue`; it made no assignment or audit write. The initial test expected the handler code `Security.PermissionKeyInvalid` and failed, establishing the actual validator-before-handler normalization; the corrected assertion passed. No lost-response commit conclusion is inferred from a UI value.

## SEC-T08 — bounded integration handoff, 2026-10-26

Final test-source candidate `a0a906c50ad4795295ea9b4689b50e7d4727a2c9` adds a mounted candidate shell test: clean close disposes the first `SecurityWorkspaceControl`, reopening creates a distinct control, and sign-out disposes the second and prevents another open. `TestResults/sec-t08-shell/sec-t08-shell.trx` reports 2 discovered/executed, 2 PASS. The earlier SEC-T06 full SQL-free Ui.Tests run was 196/196 at physical100; SEC-T05's exact approved SQL-free candidate was 196/196 at physical100 and physical125. Post-gate changes bind Local ports and shell close admission; no candidate rendering/layout change was made. Candidate lifetime tests and bound Local late-response tests cover protected-data clearing; the real SQL envelope test above establishes the error shape consumed by existing candidate error presentation tests. This is a focused integration recheck, not a new attended Lab review.

SEC-UI-01–08 remain tied to the approved candidate source and the [SEC-T05 review record](20261025-security-ui-lab.md); that record traces SEC-L01–SEC-L12 and keeps the complete attended matrix and narrower keyboard/review gaps open. The Owner's High Contrast exception applies to SEC-T04 only. Real Login, installer/package, full product and production-release acceptance were not exercised. Raw TRX and test SQL resources remain local and ignored.

## PR #51 review round 6 correction, 2026-10-26

Source/test `75fd28e9b9ea1781718c06bf94261ae0ff67ecb8` addresses R6-F01 and R6-F02. A confirmed role-grant or direct-user Save followed by a Forbidden required read now scrubs the affected target and editor content while retaining the non-sensitive committed/refresh-failed outcome and recovery close block. The same lifetime fence covers a confirmed profile mutation with a clean editor. Recovery requires a later authorized read and explicit review; no mutation is replayed. The role/direct regression was observed RED 2/2 before correction and GREEN 2/2 afterward; the final selected lifetime/modal selector passed **7/7** in local `TestResults/security-r6-final-focused/security-r6-final-focused.trx`.

Create/Edit and password dialogs now present a sanitized server Validation/Conflict result inside the modal, focus the named field for validation, clear password input and disable resubmission while recovery is required. A visible **Tải lại để đối chiếu** action reloads and resolves a conflict without resending the command. The Create BadRequest/Conflict modal regression was observed RED 2/2 before correction, then passed; the final selector includes it and password BadRequest/Conflict cases. Raw server messages and validation-detail text are not displayed. The primary UI specification now labels the removed legacy files as retired baseline and links current candidate source.

On this exact source, the full SQL-free Debug/net48 Ui.Tests project at measured physical100 with `MYDMSVN_UIN_EXPECTED_SCALE=100` passed **201 discovered/executed, 201 PASS, 0 failed/skipped/not-executed**, local `TestResults/security-r6-final-native100/security-r6-final-native100.trx`. Selected Desktop shell/candidate tests passed **27/27** in `TestResults/security-r6-desktop-shell/security-r6-desktop-shell.trx`; Desktop.App Release/net48 build passed with 0 warnings/errors. Physical125 has not yet been rerun for this revision. Earlier SEC-T07 SQL outcomes belong to the preceding source; this correction changes client recovery and modal presentation, not the SQL contract.
