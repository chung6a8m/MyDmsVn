# UIN-T07 — native tab close and workspace close admission

Date: 2026-10-23. Scope: T07's contribution to UIN-A08. T13 retains package Login, attended and dated Owner disposition; this record does not establish production release acceptance.

## Candidate and ownership

| Slice | Exact revision | Behavior |
|---|---|---|
| UI library | `433eefaa68148c7769a80551ffee3881adbc1f0a` | Optional per-page native close affordance. Pointer hit before selection/focus, Ctrl+W, and accessible Shift+F10/context command emit a request for the original `TabPage`; the owner retains collection and disposal control. Reviewed additive API fingerprint `265d4448582740117ee95df2845ae23f701f6167f7c65bf4d22ec1ba5064240b`. |
| BSG integration pin | `ac4425eeb886a6654a7c1d9ea08a23e5f3a3afb0` | Pins the UI revision; no BSG adapter or SourceGrid change. SourceGrid remains `f4e457b43582bf01892f50bdc74aa480531e5944`. |
| App source and tests | `68d04eca8ce998557f71df292aaa3821ba4a98db` | Shell owns Save/Discard/Cancel, pending-request deduplication, session/page identity checks and forced cleanup. Four catalog VMs track accepted baselines and revisions. Catalog, Goods and Sales Save-close return explicit write outcomes and preserve pending/native buffers and newer edits. |

The app's Release net48/x86 executable built from this source has SHA-256 `8D63115AB983D3122C16D92CE4F14EFCB59791A917DC5DCFCC1534DC5E845952`. Debug net48/x86 test DLL SHA-256: GUI `16973B27AC6B188F1D96D7A782DC8A41755561B79F801DD6C131B01F85503FE4`, Session `5B71DE1F962D795FB2FBC012C23B829852485A02BA5904142FB695C85D76A35D`. The executable is a local build, not a packaged Login or release candidate.

## Verification

Interactive Windows NT 10.0.26300.0, SDK 8.0.426, VSTest 17.11.1. App tests run net48/x86; UI and BSG tests cover net48/x86 and net8.0-windows/x64. Tests use actual ignored TRX, serialized STA, finite waits and WinForms exception capture. Counts are selected/discovered/executed rows with zero skips.

| Scope | Local TRX | Result |
|---|---|---|
| UI tab behavior, render logic and intentional public API gate | `TestResults/uin-t07-ui-final-net48/uin-t07-ui-final-net48.trx`, `TestResults/uin-t07-ui-final-net8/uin-t07-ui-final-net8.trx` | 51/51 PASS on each TFM at native100 |
| BSG full suite against final UI pin | `TestResults/uin-t07-bsg-net48/uin-t07-bsg-net48.trx`, `TestResults/uin-t07-bsg-net8/uin-t07-bsg-net8.trx` | 256/256 PASS on each TFM |
| Catalog and Sales session contracts | `TestResults/uin-t07-session-final/uin-t07-session-final.trx` | 78/78 PASS |
| Shell, close coordinator, Catalog and Goods/Sales control/VM scope | `TestResults/uin-t07-app-gui-current/uin-t07-app-gui-current.trx` | 156/156 PASS at measured native100; includes Light/Dark close of nonselected and last tabs |
| Final shell lifetime regression after disposing the cancellation registration | `TestResults/uin-t07-shell-lifetime-final/uin-t07-shell-lifetime-final.trx` | 25/25 PASS at measured native100 |
| App Release net48/x86 build | `dotnet build src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -c Release -p:Platform=x86 --no-restore` | PASS, 0 warnings/errors |
| Light/Dark native125 on exact candidate | Pending interactive display switch | NOT RUN |

The independent PerMonitorV2 observer recorded `MONITOR_SCALE=100`, `MONITOR_DPI=96`, `WINDOW_DPI=96` for both Light/Dark shell rows. A preliminary command declared 125 while the physical monitor was 100; the observer rejected both rows, so they are not native125 evidence. The corrected native100 run passed. The first BSG restore attempts inherited root central package management and failed; `--no-restore` against the established pinned assets ran the actual full suites above.

Behavior cases include unchanged and dirty Catalog editors, raw text before New, duplicate/rejected/canceled/unknown Save, confirmed write with list-refresh failure and later clean close, newer edits during Save, pending receipt inline/date/lookup admission, busy/recovery refusal, delayed prompt plus session loss/reopened page, duplicate requests, last-tab close and single disposal. Sales pending numeric input was observed through native per-character messages: valid text commits before Save; invalid text remains raw and dispatches no update. A confirmed write with failed refresh keeps the current close request open and presents a saved/refresh warning; a later explicit clean close does not resend the write. Forced sign-out closes pages without prompting or saving.

## Boundary

T07 source and focused native100 behavior are verified on the recorded candidate. Native125 Light/Dark remains open until independently measured on this candidate. No SQL owner or cleanup was needed because this task changes no persistence. Docking/layout persistence and package/Owner acceptance are outside T07.
