# GUI-P5 coverage matrix and T00 baseline

**Task:** GUI-P5-T00, [acceptance plan](plans/20261019-001-full-winforms-gui-p5-acceptance.md). **Recorded:** 2026-10-19 (Asia/Bangkok), Codex; Project Owner approved the Minimal Disposable Test Topology and T00 scope in this chat on the same date. **Executable source:** `f275183c8b39dc2b712dc37be7f0f4ac71396357` on `v1a`, isolated implementation branch `codex/gui-p5-t00`; initial inventory/evidence commit `7cfefd4`. Documentation changes do not change the executable candidate.

**Status: GUI-P5-T00 COMPLETE — reviewed inventory, owner-approved topology and explicit execution prerequisites. Packaged execution readiness remains BLOCKED / NOT TESTED as detailed below.** T00 completes reconciliation/planning, not environment certification. The source inventory and focused baseline below are evidence only for their stated scope. Current T00–T04 task scope is COMPLETE; T04/REMAIN-P5-GUI-01 has explicit Owner sign-off in the [attended worksheet](GUI_P5_T04_WALKTHROUGH.md). T05/T06, REMAIN-P5-GUI-02, overallP5, REL G1–G5 and production readiness remain OPEN. T01/T02 completion evidence is recorded separately below and in the verification document. Current T03 automation evidence is in Appendix D; historical T00 BLOCKED/NOT TESTED entries are not relabeled as runtime passes. No package was built or installed in T00; no package hash or real-login result is available.

## 1. How to use this ledger

`D` means `tests/MyDmsVn.Desktop.Tests`; `S` means `tests/MyDmsVn.Desktop.Session.Tests`. Test references use exact `Class.Method` names; append them to the project's namespace for a `FullyQualifiedName` filter. Appendix A links every Desktop test method to source. A method existing in source is **existing automated**, not a passing execution. **New automated** means required work, with no test yet implemented by T00. **Packaged E2E**, **attended** and **backend oracle** are separate obligations and do not substitute for control/VM tests.

All listed test projects and shipped hosts target `net48`. Desktop tests contain both control and non-control tests, including 17 editor ViewModel methods; do not label all 139 invocations real GUI workflows. No new test project, dependency, production selector or runtime behavior is introduced here. The authoritative execution policy is [TEST_POLICY](TEST_POLICY.md); the [strategy](TEST_STRATEGY.md) and [test commands](../tests/README.md) remain applicable.

## 2. Observed baseline and executable commands

| Item | Observed T00 evidence | Limit |
|---|---|---|
| Isolation | Managed worktree `gui-p5-t00/CleanArchitecture`, branch `codex/gui-p5-t00`, base commit above; original checkout untouched | Worktree is retained for follow-up |
| OS/tooling | Windows `10.0.26200.0`, OS/shell x64, interactive API true, session 38; SDK `8.0.425`; VSTest `17.11.1 (x64)` | Developer host observation; no supported-OS or clean-VM certification |
| Dependencies | Locked restore succeeded after recursive submodule initialization | Initial NU1004 was caused by absent pinned UI project, not a stale lock; no lock regeneration |
| Vendor pins | BootstrapSourceGrid `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f`; Bootstrap5WinFormUI `aba102e33c48937fd92468791c287afda0a59e77`; SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944` | Pins unchanged |
| Desktop source | 137 Facts + 1 Theory method with 2 InlineData rows = 138 methods / 139 invocations | Count rederived for this revision, independently of historical T07 |
| Desktop discovery | 139 rows from current Release/net48 build and `--list-tests` | Discovery executes no tests |
| Selected/executed | 10 Facts: `LoginFormTests` (2), `WinFormsTestInfrastructureTests` (8) | Focused baseline, ordinary testhost; no actual packaged Login |
| Actual TRX | 10 executed, 10 passed, 0 failed/error/timeout/aborted/notExecuted | 129 discovered invocations unselected / NOT RUN, not skipped |
| Session project | Source read for behavior mapping; 0 selected/executed in T00 | No Session project pass claimed |
| SQL | Test setting present and identical in process/User scope; parsed without opening SQL: effective catalog `master`, no AttachDBFilename/UserInstance | Endpoint/credentials withheld; no connection, SQL version/edition, instance-ownership/principal validation or SQL test execution |
| Display/package | Follow-up metadata: primary display 1920×1080 at 100%; application DPI/theme matrix and x86 packaged/testhost execution NOT TESTED | Desktop OS theme settings are Dark; they do not establish application theme. Focused rendering tests do not establish a DPI matrix; x64 runner banner does not prove x86 execution |
| Resources | Focused test command completed; no packaged app, SQL DB or SQL principal allocated | No subprocess/DB cleanup claim for T01/T03; raw TRX/logs remain local |

Commands run sequentially from the isolated repository root:

```powershell
git submodule update --init --recursive
dotnet restore tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore --list-tests
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore --no-build --filter 'FullyQualifiedName~WinFormsTestInfrastructureTests|FullyQualifiedName~LoginFormTests' --logger 'trx;LogFileName=t00-baseline.trx' --results-directory TestResults/gui-p5-t00
```

Local evidence: `TestResults/gui-p5-t00/t00-baseline.trx`; restore/discovery/baseline logs in the operator's temporary directory (`gui-p5-t00-*.log`). Raw artifacts are not committed. Every method in the two selected classes in Appendix A passed; all other appendix methods are NOT RUN, including both packaging Theory rows. There were no new automated cases in T00.

## 3. Behavior-to-test obligations

The following table preserves the T00 obligations. Appendix C reconciles every T02 control/VM gap with implemented named tests and current execution evidence. Packaged, attended and backend obligations stay separate. T06 reconciles the complete final inventory and TRX again.

| ID / behavior and implementation | Existing automated (exact anchors) | New automated / uncovered control path | Packaged E2E + attended | Backend oracle / owner |
|---|---|---|---|---|
| L1 Login masking, keyboard, theme; `LoginForm`, `LoginViewModel` | D `LoginFormTests.Login_form_is_compact_keyboard_accessible_and_masks_password`, `Login_form_renders_after_theme_change_and_disposes_without_ui_exceptions`; S `LoginViewModelTests.Pending_login_sets_busy_and_rejects_duplicate_submit`, `Invalid_credentials_use_safe_vietnamese_message_and_clear_password`, `Empty_fields_are_rejected_without_calling_the_backend` | T02: actual control failed/disabled/restricted login, Enter/Escape/focus and closing during delayed authentication; VM tests alone do not prove control binding | T03: Login-first real credentials; T04/T05: operator keyboard/error observations | Security `IdentityStoreIntegrationTests.User_and_permission_stores_read_current_sql_state_and_replace_hash_atomically`; T03 independent account/permission state |
| L2 Session/logout/relogin/idle/prior-user completion; `DesktopHostRunner`, `FoundationShellForm`, `LocalDesktopSession` | D `FoundationShellFormTests.Sign_out_and_session_expiry_clear_the_session_with_explicit_status`, `Changing_authenticated_user_closes_the_previous_users_workspace`; D `LocalIdentityApiClientTests.Late_current_user_validation_from_user_a_cannot_sign_out_user_b`; S `DesktopSessionExpiryTests.Idle_boundary_expires_session_and_background_dispatch_does_not_reset_activity` | T02: real controls after expiry/relogin and late async completion across every workspace; no current real-package idle proof | T03: logout, different user, default idle expiry and restart; T04/T05 repeat relevant steps | Active account + captured actor enforced in Application; T03 read-only account/no-mutation assertions |
| C1 Products CRUD/active/validation/layout; `CatalogControl`, `ProductCatalogViewModel` | D `CatalogControlTests.Save_button_forwards_editor_values_and_shows_field_errors_without_modal_dialogs`, `Catalog_control_renders_active_state_and_keeps_compact_keyboard_layout`; S `CatalogViewModelTests.Product_create_update_and_set_active_publish_one_typed_message_after_each_success` | T02: real-control successful create/update/deactivate/reactivate, denied mutation and errors; existing control validation uses Product fake only | T03 all actions through package; T04 Catalog/Lookup sign-off | Catalog SQL `CatalogBackendIntegrationTests.Catalog_backend_crud_queries_audit_and_conflicts_use_real_sql_boundaries`, `Explicitly_denied_catalog_write_leaves_business_tables_unchanged`; T03 independently query values/active flags and denied no-mutation |
| C2 Warehouses, Employees, Customers CRUD/active and entity-specific fields; catalog factories | S `CatalogViewModelTests.Every_catalog_create_publishes_its_own_kind_and_entity_id`, `Warehouse_and_employee_status_changes_publish_typed_messages`, `Employee_user_id_rejects_invalid_text_and_blank_explicitly_unlinks`; D `CatalogControlTests.Shell_reuses_catalog_tab_and_disposes_catalog_controls_on_sign_out` | T02: named real-control CRUD/status/field-validation/permission cases per entity; shared Product grid tests do not prove all four adapters/editors | T03 four catalog flows, T04 attended all four | Catalog SQL owner for constraints/nullable linkage/Unicode/uniqueness; T03 independent persisted state |
| C3 Search/debounce/stale response/Refresh/paging/reopen; `CatalogViewModel` | S `CatalogViewModelTests.Rapid_search_uses_300ms_debounce_issues_one_query_and_resets_page`, `Refresh_is_immediate_and_preserves_the_current_page`, `Late_old_list_response_cannot_overwrite_newer_items`; D `CatalogControlTests.Catalog_control_pages_beyond_the_first_twenty_five_rows`, `Reopening_existing_catalog_tab_refreshes_authoritative_data_without_new_control` | T02: control-driven rapid search/cancellation/Refresh and stale completion per shared control; preserve Refresh page contract below | T03 bounded state-based filters; T04 keyboard/page checks | Catalog SQL owner supplies authoritative deterministic paging/filtering |
| K1 Same-process changed lookups/active-only options/historical selection/reopen; `CatalogChangedMessage`, `CatalogLookupViewModel`, receipt lookups | D `GoodsReceiptControlTests.Open_catalog_controls_refresh_receipt_lookups_without_losing_selected_references`, `Receipt_line_keeps_a_historical_product_selected_outside_the_active_page`; S `CatalogLookupViewModelTests.Relevant_message_bursts_refresh_two_recipients_and_disposal_stops_one`, `Inactive_historical_selection_remains_visible_but_inactive_results_are_not_new_options`, `Selected_active_item_becomes_unavailable_after_deactivation_message` | T02: explicit Product/Warehouse/Employee deactivate/reactivate + open receipt/reopen control assertions; no cross-process messenger promise | T03 same running host + restart freshness; T04 full Catalog/Lookup sequence | Inventory `GoodsReceiptQueryIntegrationTests.Get_returns_complete_aggregate_version_and_inactive_historical_labels`; Catalog SQL lookup predicates |
| R1 Draft/new/save/reload/edit/dirty/precision/concurrency; receipt control/editor/list VMs | D `GoodsReceiptControlTests.Draft_commands_update_lines_and_validation_errors_stay_inline`, `Dirty_editor_blocks_selection_new_and_refresh_without_losing_input`, `Editing_a_line_after_save_updates_the_reloaded_line`; D `GoodsReceiptEditorViewModelTests.Update_reuses_opaque_version_and_conflict_preserves_user_input`, `Successful_create_keeps_identity_when_follow_up_refresh_fails`; S `GoodsReceiptListViewModelTests.Late_old_list_and_get_responses_cannot_replace_newer_state` | T02: stale-rowversion error rendered by actual control, invalid input boundaries and account switch while saving; preserve entered data on rejected conflict | T03 independently persisted Draft/save/reload/edit and two-client stale snapshot; T05 attended | Inventory `GoodsReceiptCommandIntegrationTests.Create_and_update_draft_use_server_numbers_atomic_lines_and_advancing_versions`, `Failed_update_rolls_back_header_version_and_all_line_replacement`, `Concurrent_updates_with_one_expected_version_allow_exactly_one_commit` |
| R2 Post/duplicate/uncertain result/reconciliation | D `GoodsReceiptControlTests.Draft_post_button_posts_current_version_and_locks_the_editor`; D `GoodsReceiptEditorViewModelTests.Post_uses_persisted_identity_and_version_then_refreshes_the_posted_receipt`, `Concurrent_save_requests_issue_only_one_create_command` | T02: duplicate Post, Post-specific busy/error/uncertain outcome controls; concurrent-save fake is not duplicate-Post proof. Reload state before any mutating retry | T03 real Post/duplicate/SQL timeout and uncertain outcome; T05 attended full inventory sequence | Inventory `GoodsReceiptCommandIntegrationTests.Post_commits_status_ledger_and_balances_once`, `Concurrent_posts_for_same_receipt_commit_exactly_once`, `Failure_after_ledger_insert_rolls_back_status_ledger_and_balance`, `Reconciliation_reports_no_transient_difference_while_receipts_post_concurrently`; T03 independent raw SQL receipt/ledger/balance oracle |
| R3 Read/Write/Post-only, direct Deny/revoked/disabled/no mutation | D `GoodsReceiptControlTests.Forbidden_refresh_clears_visible_receipt_data`, `Reactivation_after_access_revocation_clears_visible_receipt_data`, `Write_only_create_clears_sensitive_detail_and_locks_save_after_commit`; D `PermissionActionBinderTests.Delayed_permission_result_cannot_reenable_an_action_after_sign_out` | T02: control matrix for Read/Write/Post combinations, direct Deny, disabled account and pending completion; hiding buttons is insufficient | T03 SQL-backed deliberate grants/deny/disabled state; T05 per-actor steps | Inventory `GoodsReceiptCommandIntegrationTests.Post_only_user_cannot_read_or_write_and_leaves_the_aggregate_unchanged`, `Stale_or_denied_post_leaves_status_ledger_and_balances_unchanged`, `Inactive_reference_and_explicit_write_deny_leave_receipt_tables_empty` |
| I1 Balance/Card filters/order/page/date/zero/empty/history/busy/errors | D `InventoryControlTests.Balance_control_renders_history_totals_and_runs_explicit_filters_immediately`, `Stock_card_control_filters_without_debounce_and_reports_errors_without_dialogs`, `Stock_card_control_applies_dates_chosen_before_ids_in_first_query`; S `InventoryViewModelTests.Balance_does_not_total_quantities_without_a_single_product_filter`, `Stock_card_selection_date_and_refresh_are_immediate_and_discard_late_responses` | T02: real controls page/order/busy/revocation/stale callbacks and historical references; verify date boundaries independent of display labels | T03 package balance/card + SQL oracle; T05 totals and movement order | Inventory `InventoryQueryIntegrationTests.Balance_and_stock_card_queries_filter_page_sort_and_keep_inactive_history`, `Stock_card_query_preserves_datetime2_boundaries`; independent ledger aggregation/reconciliation |
| S1 Security administration, protected superuser/roles/direct decisions/import revisions | D `SecurityAdministrationFormTests.Administration_control_loads_and_disposes_on_serialized_sta_without_modal_errors`, `Security_workspace_is_session_scoped_and_closes_on_sign_out`; S `SecurityAdministrationViewModelTests.Bound_protected_account_cannot_be_edited_from_the_view_model`, `Revision_conflict_is_reported_and_refreshes_the_current_assignment_without_retrying`; S `PermissionImportViewModelTests.Selection_change_invalidates_preview_before_apply` | T02: real-control protected-target/read/manage permissions, import Preview/Apply stale-revision/error binding, session revocation; two existing control tests only load/dispose/session-scope | T03 deliberate identities + stale preview/direct Deny; T04/T05 relevant permission flows | Security `SecurityAdministrationSqlTests.Permission_import_rejects_stale_catalog_revision_conflicts_and_audit_failure`, `Permission_import_rechecks_actor_permission_after_preview`, `Administration_is_revisioned_audited_and_preserves_direct_decision_precedence`, `Missing_targets_and_bound_superuser_lifecycle_changes_are_rejected` |
| F1 Errors/async after close/disposal/ThreadException/DataError/fatal hang | D `WinFormsTestInfrastructureTests` (all eight Appendix A methods); D `ProductionDiagnosticsTests.Redactor_removes_credentials_and_nested_sql_exception_details`; D `CatalogControlTests.Disposing_control_disposes_view_model_and_prevents_delayed_work`; D `GoodsReceiptEditorViewModelTests.Dispose_ignores_a_late_detail_refresh_response` | T01 COMPLETE: close/async/reporting + owned noncooperative/modal/fatal subprocess, verified descendant/pipe drain, fail-stop admission on uncertain cleanup ([evidence](GUI_P5_VERIFICATION.md)). T02 owns remaining control-specific disposal/callback gaps. `StaTest` cancels then waits 5s; it cannot kill hung STA | T03 startup/SQL failure, unexpected modal + bounded teardown; T04/T05 safe error UX | No DB reset until all workers/connections drain; timeout/uncertain ownership quarantines allocation |
| F2 Packaging/composition/schema/x86 | D `ReleasePackagingTests.Manifest_only_mode_hashes_active_desktop_and_separate_admin_package`, `Packages_without_runtime_schema_policy_are_rejected`, two rows of `Desktop_packages_reject_admin_tools_and_embedded_secrets`; D `DesktopCompositionTests.WinForms_composition_resolves_goods_receipt_factory_and_shell`; D `DesktopHostRunnerTests.Smoke_mode_creates_and_renders_login_without_bypassing_authentication` | T01 COMPLETE: packaging now uses bounded job-owned PowerShell/tree/pipe drain ([evidence](GUI_P5_VERIFICATION.md)); T03: actual published x86 Login-first host; manifest fixtures/smoke form creation do not establish real authentication | T03 frozen clean publish + manifest/hash; REL owns clean install/recovery and support matrix | Migrations/Security owners supply exact schema001–009 and runtime principal preflight; no prefix auto-upgrade |
| U1 Theme/accessibility/keyboard/DPI | D `UiDependencyCompatibilityTests.Bootstrap_source_grid_tracks_runtime_theme_changes`; D `FoundationShellFormTests.ToggleTheme_switches_between_light_and_dark`; D `CatalogControlTests.Enter_selects_the_active_catalog_row`; Login tests above | T02: keyboard/focus/tab/read-only/error semantics for all workspaces and stable accessible selectors; T03 runtime UIA tree/pattern checks | T03 DPI/theme automation; T04/T05 attended clipping/readability/focus. Visual usability remains attended, mandatory automation stays mandatory | No backend substitute for visual/focus behavior; T06 reconciles each declared environment |

### Source/plan reconciliation

- Catalog `SetSearchAsync` resets `PageNumber` to 1. `RefreshAsync` cancels pending debounce/message work and reloads **the current page**; the named Session test explicitly asserts this. The plan's shorthand “Refresh/page reset” is interpreted as immediate Refresh plus reset on changed search/filter, not a requirement to change Catalog Refresh semantics. Any different owner decision must be recorded before a T02 behavior change.
- Same-process `WeakReferenceMessenger` is a singleton in Local DI; open receipt lookup updates are not cross-process notifications. Reopen/restart must fetch authoritative state.
- Product-specific control tests and shared factories do not prove every entity's real-control CRUD or permission matrix. Likewise, fake Posting proves UI forwarding/state only; SQL concurrency/atomicity remains in the Inventory SQL owner.
- Backend owners above are source anchors only in T00. No historical 581-pass count, former 139 NOT RUN count or old packaged smoke is counted as a current execution.

## 4. Production composition and authorization prerequisites

Inspect [Program](../src/MyDmsVn.Desktop.App/Program.cs), [WinForms registrations](../src/MyDmsVn.Desktop.WinForms/DesktopServiceCollectionExtensions.cs), [Local registrations](../src/MyDmsVn.Desktop.Infrastructure.Local/DependencyInjection.cs) and [publishing script](../deployment/release/Publish-LocalRelease.ps1) before changing tests. Startup runs read-only database/principal/schema/superuser preflight before constructing Login. Production registers Server.Application/Infrastructure, SQL persistence, Local adapter, production diagnostics and UI-context dispatcher. The session/messenger are process singletons; forms/VMs/factories are transient and host scopes are disposed. T03 must use this path, without injected authenticated session or `--smoke-test`.

Permission definitions are code-owned: [Catalog](../src/MyDmsVn.Server.Application/Catalog/CatalogPermissionDefinitions.cs), [Inventory](../src/MyDmsVn.Server.Application/Inventory/InventoryPermissionDefinitions.cs), [Security composition](../src/MyDmsVn.Server.Application/Security/PermissionRegistryComposition.cs) and [key aliases](../src/MyDmsVn.Server.Application/Security/PermissionKeys.cs).

| Scenario family | Exact keys / actor setup to test |
|---|---|
| Each of Products/Warehouses/Employees/Customers | `Catalog.<entity>.Read`, `Catalog.<entity>.Write`: Read only, Read+Write, absent grant and direct Write Deny over role grant |
| Receipts | `Inventory.GoodsReceipts.Read`, `.Write`, `.Post`: separate Read/Write/Post combinations, explicit Deny and revoked/disabled account |
| Stock | `Inventory.Balances.Read`, `Inventory.StockCard.Read`: separately granted/denied |
| Administration | `Security.Users.Manage`, `Security.Roles.Manage`, `Security.Permissions.Read`: explicit deliberate grants and unauthorized no-mutation |
| Configured superuser | Matching configured normalized username + protected DB binding + active account; unknown/deprecated key denies before bypass. Administrator role name grants no implicit rights |

Preserve hash/salt/algorithm-aware verification. Do not seed automatic Administrator grants, synchronize permission keys or bootstrap from the client. SQL checks must inspect current permissions, not infer authorization from visible buttons.

## 5. Native UI Automation approach and selector gaps

**Proposed approach:** Windows-provided UI Automation via .NET Framework `UIAutomationClient`/`UIAutomationTypes`, in the existing Desktop test owner, with an explicitly x86 net48 driver/probe where needed. No external automation library is adopted in T00, so no external-library compatibility claim/spike is made. T03 must first prove cross-process access to the actual x86 package and supported patterns on the declared Windows image. Do not claim the current CUA tool or a framework name alone proves product automability.

Select the owned process/window first, then scope by stable accessible name/ID, control type and parent workspace. Use `InvokePattern`, `ValuePattern` where available, selection/expand patterns for lookups and grid/table patterns only when actually exposed. Bound each condition/event wait and whole process tree in T01/T03; observe enabled/busy/state changes rather than global sleeps. Masked credentials must never appear in snapshots/logs. No screen coordinates, generated ordinal selectors, reflection into production session, or direct-DB business mutations as an automation shortcut.

| Surface | Current source selector anchors | Gap / T03 prerequisite |
|---|---|---|
| Login | Window text `MyDmsVn — Đăng nhập`; buttons `Đăng nhập`, `Thoát`; password masking and TabIndex | Username/password lack explicit `Name`/`AccessibleName`. Public C# properties are test hooks, not AutomationIds. Add stable accessible semantics and verify the actual runtime tree |
| Catalog | `AccessibleName = "Search catalog"`; editor names from field labels (`Code`, `Name`, entity fields); button text | Repeated labels across tabs require workspace scope; SourceGrid is custom and exposed row/cell patterns are unverified |
| Goods Receipt | `Search goods receipts`, `Filter by status`, `Receipt date`, `Note`, `Quantity`, `Unit cost`; lookup factory sets `AccessibleName` | Prove product/warehouse/employee lookup and editable grid selection/value patterns; identify validation/status and Post state without row ordinals |
| Stock Balance/Card | Refresh/Previous/Next button text; numeric ID/date filters and DataGridView | Filters have no explicit accessible names in constructors; distinguish warehouse/product and date fields. Verify DataGridView rows and column labels at runtime |
| Security | `Temporary password`, `Role name`, `Filter permission catalog`, generated labeled editors, `PermissionExplanation` name | Preview/apply, grids, role/user selection and protected-target explanation need runtime selectors/pattern assertions |
| Shell/startup | Document `Name = key`, navigation captions; startup failures may use MessageBox before Login | Control.Name does not prove AutomationId; discover actual tree, handle only owned dialogs, fail on unexpected modal windows |

If SourceGrid does not expose sufficient patterns, T03 must document the exact failure and add appropriate accessibility support within existing controls, with T02 tests. Do not silently fall back to coordinate automation or mark mandatory E2E as manual PASS. UIA selection and compatibility are pending validation, not a completed packaged spike.

## 6. Disposable topology, credentials and cleanup contract

**Approval: Project Owner, 2026-10-19, explicit chat decision “GUI-P5-T00 — Minimal Disposable Test Topology”.** The owner approved the configuration, identity separation, secure credential delivery/cleanup boundaries and T00 scope below. An owner-managed SQL instance is approved for testing; a shared SQL Server additionally requires its DBA/instance owner's confirmation. The current configured endpoint's private/shared ownership has not been established. Existing credential presence is not proof of that condition, suitability or runtime least privilege. No SQL connection or provisioning was attempted in T00. The earlier [T07 resource exception](L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke) was consumed by its cleanup; GUI-P5 authorization comes from this new decision, not that old run.

| Approved Minimal Disposable Test Topology | Actual evidence / execution state |
|---|---|
| Windows 11 x64 interactive desktop, .NET Framework 4.8 requirement, application `net48`/x86 | **Observed:** CIM reports Windows 11 Home Single Language 25H2, version `10.0.26200`, build/UBR `26200.9457`; OS x64, interactive true, session 38. Installed v4 runtime registry Version `4.8.09221`, Release `533509`; prior net48 build/focused tests succeeded. An exact 4.8-only image and packaged x86 execution remain **NOT TESTED**. No broader OS support/clean-VM claim |
| Baseline primary display 1920×1080, 100% (96 DPI); supplementary 125% (120 DPI); application Light/Dark; one GUI suite/process at a time | **PASS, metadata only:** primary physical dimensions 1920×1080, monitor scale 100%, native scale API result 0. **NOT TESTED:** 125% and application Light/Dark scenario matrix. 150% is outside the current mandatory matrix; 200% is not added. No workstation display/theme settings were changed |
| Prefer suitable existing non-production SQL Server; SQL Server 2022 Developer is target only if new provisioning is needed | **BLOCKED for SQL E2E admission:** actual edition/version, suitability and private/shared ownership not verified. Existing test connection parses to `master` without attachment/user-instance options. No server contacted or new SQL instance installed; shared-instance use needs DBA/instance-owner confirmation |
| Dedicated application database `MyDmsVn_GuiP5_<GUID>` with scripts001–009; separate from production and recovery drills | **NOT PROVISIONED:** no GUI-P5 database/schema/journal/binding created. Existing SQL helper name guard accepts only `MyDmsVn_Test_<32-hex-GUID>`; it does not yet allocate/admit the approved GUI prefix. T03 prerequisite below preserves positive ownership checks |
| Distinct test database provisioning, migration/maintenance and runtime identities; Desktop uses only runtime in its application test DB | **NOT PROVISIONED / NOT TESTED:** principal names, effective grants and credential ACLs not established. Harness `master` connection serves infrastructure only. Runtime must receive only reviewed `MyDmsVnRuntimeRole` object/column/procedure rights, no `db_owner`, DDL or out-of-design administration. Independent oracle reads use separately reviewed access, without broadening runtime grants |
| Ownership proof, actual process/connection/worker drain, identity/marker revalidation before deletion; uncertain ownership retained/reported | **Procedure pinned below; destructive execution NOT TESTED in T00.** No DB/principal/process allocation to delete. Prefix matching alone never authorizes cleanup |

Follow-up observations used read-only CIM/registry/native display metadata: `Win32_OperatingSystem`, Windows CurrentVersion build/UBR, installed Framework v4 Full Release/Version, primary `GetSystemMetrics` under a temporary per-monitor-aware thread context and `GetScaleFactorForMonitor`. The thread context was restored. SQL configuration was parsed locally with `SqlConnectionStringBuilder.set_ConnectionString`; values were never printed or persisted and no SQL connection was opened. Source/project/script checks establish `net48` and publish `-p:PlatformTarget=x86`, not runtime package verification. Test counts in section 2 are the existing T00 run; no additional test invocation is implied by metadata checks.

### Remaining provisioning steps before T03 execution

These are explicit prerequisites, not failed coverage-inventory criteria. T00 does not require their execution; actual results must be recorded by the owning task.

1. **Owner/operator, before SQL admission:** identify the exact designated non-production instance privately, confirm owner-managed versus shared; obtain additional DBA/instance-owner confirmation for shared SQL. Then perform bounded read-only server edition/version/authentication/TLS checks and assess the existing instance. If unsuitable or absent, provision SQL Server 2022 Developer on approved resources; no automatic installation or fallback is specified.
2. **T03, existing Desktop test owner with affected SQL-helper coverage under TEST_POLICY:** implement a narrowly scoped allocator/guard for `MyDmsVn_GuiP5_<GUID>` retaining the existing instance/database-ID/create-time/random-marker proof and bounded lifecycle. Review allocation and disposal together. The current `SqlTestDatabaseName` and `SqlTestDatabase.CreateAsync` cannot be treated as already supporting this prefix. Do not merely rename a DB, weaken a prefix guard, accept arbitrary names, or change the accepted RESP fixture behavior. No new generic runner/framework is needed.
3. **Setup operator + T03:** create the positively owned database and distinct provisioning/migration/runtime identities through the approved administration channel. Apply current packaged migration001–009, unchanged principal script, deliberate protected-superuser bootstrap and test account/permission setup. Verify runtime effective grants, including extra EXECUTE, under the actual principal; give Desktop no provisioning/migration/test-harness credential. Record principal ownership and secure per-run credential delivery/ACLs without secrets. No real credentials have been generated or stored by T00.
4. **T01 then T03:** deliver bounded process-tree ownership/termination/drain first; publish the actual clean x86 package, validate UIA selectors/patterns and independent SQL oracles. Prepare per-child runtime configuration and read-only oracle access with finite timeouts. Capture hashes, schema/binding/principal checks and readiness before Login/E2E; do not use startup smoke as a real-login substitute.
5. **T02/T03/T06 with operator:** record actual 100% baseline and 125% supplementary runs, Light/Dark, resolution, OS build/runtime/bitness per run. Scaling/theme scenarios remain NOT TESTED until executed. Do not change a shared desktop's display settings as part of T00.

Use [configuration/security runbook](CONFIGURATION_AND_SQL_SECURITY.md), [security administration runbook](SECURITY_ADMINISTRATION_RUNBOOK.md), [schema runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) and [principal provisioning script](../deployment/sql/Provision-MyDmsVnPrincipals.sql). The runtime uses reviewed object/column grants and only the three named security procedures, no fixed broad roles, DDL, journal/binding mutation or blanket EXECUTE. Audit effective grants separately: a successful current startup validator does not check every extra EXECUTE grant. An independent oracle reads business/security assertions under a separately reviewed identity; do not broaden desktop permissions to obtain test evidence.

Credentials stay outside source/package/TRX/screenshots. Prefer a deliberately assigned Windows identity. If SQL Authentication is selected, use operator-managed ACL-protected runtime/migration files separately; the existing loader does not implement DPAPI. Never pass passwords on command lines or print environment values. Do not overwrite the workstation's shared `%ProgramData%` runtime configuration for a test; T03 must use a scoped child environment/explicit protected file for its owned allocation. Preserve the caller's prior environment. Bind `Security__SuperuserUsername` deliberately and provide passwords through protected input only.

Required T01/T03 resource lifecycle:

1. Record designated instance and generated `MyDmsVn_GuiP5_<GUID>` allocation identity (`database_id`, creation time, random `MyDmsVn.TestOwnership` marker), preserving the existing owned SQL helper's verification semantics through the T03 prefix prerequisite above; never identify ownership by name prefix alone. Keep sensitive values local, publish sanitized counts/status.
2. Setup schema/limited principals/accounts under the separate admin identity, then capture clean candidate/manifest/SHA-256, Windows/runtime/bitness/DPI/theme and controlled actor permissions. Do not run Respawn on operational or recovery databases; real packaged scenarios require their own explicitly owned lifecycle.
3. Start the actual package as an owned bounded child/tree. Track any helpers, timers, queries/readers/UoWs, independent oracle connections and setup resources. T01 supplies timeout/termination/drain self-tests before T03 depends on unattended execution.
4. Close Login/shell and drain all actual child/descendant processes and SQL operations before cleanup. A timed-out wait is not proof that SQL stopped. Preserve both initial and cleanup failures.
5. Revalidate instance + database ID/creation time/marker before dropping only that allocation; remove only positively owned temporary principals after all users/connections drain. On ambiguous ownership, leaked process or timeout, stop the suite, retain/quarantine and report for operator review. No prefix bulk-drop, unrelated pool clearing, automatic adoption or silent fresh retry.
6. Publish sanitized results per scenario (selected/discovered/executed/pass/fail/skip/block/timeout), actor state, independent SQL assertion, package/revision/hash, cleanup outcome, tester/date and remaining risks. No mandatory skipped case closes P5/P6.

## 7. Handoff and acceptance status

| Deliverable | T00 state / next owner |
|---|---|
| Source/DI/permission/packaging/selector reconciliation | Published here; complete Desktop method/row inventory in Appendix A |
| Current discovery + smallest safe baseline | 139 discovered; selected 10 / passed 10; remaining 129 NOT RUN |
| Reviewed matrix | Independent Codex review of initial inventory 2026-10-19: no important findings; 138 source anchors, named methods and 10/10 TRX checked. Owner subsequently supplied explicit Minimal Disposable Test Topology approval. Independent follow-up review on 2026-10-19 found no important issues: T00 criteria/status, approved scope, metadata versus runtime evidence and T03 prefix prerequisite are consistent |
| Approved topology/limited-identity design | COMPLETE for T00: Project Owner decision pinned in section 6, including shared-instance approval condition, secure credential boundaries and cleanup. Actual instance/principals/provisioning and packaged execution remain BLOCKED / NOT TESTED as itemized there |
| T01 | COMPLETE: [bounded reliability evidence](GUI_P5_VERIFICATION.md); at the T01 revision Desktop153/153 and Session108/108 on x86. T02 closure follows; T03–T06 remain OPEN |
| T02 | COMPLETE: Appendix C maps 59 new methods / 129 rows; current Desktop282/282 and Session108/108 passed net48/x86. Independent SQL/package/attended gates remain OPEN |
| T03 | Validate native UIA selectors/patterns/x86, actual Login and SQL oracles on approved topology |
| T04/T05/T06 | Attended P5 + frozen unfiltered GUI/Session and cleanup evidence, all OPEN |

### T00 acceptance reconciliation

| T00 criterion | Evidence / disposition |
|---|---|
| Read current GUI/VM/source, Facts/Theory rows, permissions, DI/package/selectors; list every Desktop method and separate run states | Sections 2–5 and Appendix A; 138 methods / 139 current discovered invocations, 10/10 focused TRX and 129 NOT RUN. **COMPLETE** |
| Reviewed coverage matrix with exact executable owners/gaps, proposed Windows/DPI/SQL and native automation approach | Sections 3–5; source-linked methods, task owners, selector limitations; no external library adopted, hence external-library spike condition not triggered. Actual native packaged UIA remains T03. **COMPLETE** |
| Pin approved disposable topology, least-privilege identity design, secure credential handling/cleanup and supported/not-tested distinction | Owner decision and section 6 pin all requested boundaries; verified metadata and unprovisioned components have separate outcomes and executable prerequisite owners. **COMPLETE for T00**, without provisioning/runtime PASS |

**T00 exit met:** reviewed coverage matrix, owner-approved Minimal Disposable Test Topology and explicit prerequisites are recorded. The owner's follow-up explicitly allows unprovisioned infrastructure to be recorded as remaining steps rather than a coverage-inventory failure. T00 completion does not certify environment readiness, actual least privilege, packaged E2E, either P5 walkthrough or P6 release support. At the T00 handoff the next task was T01. T01 is now complete in its [bounded reliability scope](GUI_P5_VERIFICATION.md); next is T02, with T03 provisioning still subject to section 6 conditions.

## Appendix A. Frozen T00 Desktop test method inventory

Inventory below is derived from checked-in `[Fact]`/`[Theory]` declarations, reconciled with actual VSTest discovery. All are in namespace `MyDmsVn.Desktop.Tests`. `Fact` = one invocation; only `ReleasePackagingTests.Desktop_packages_reject_admin_tools_and_embedded_secrets` is a Theory (two rows). Its row labels below identify the input class without reproducing the dummy connection string. Helpers without test attributes are excluded. A method marked `PASS` was selected in the focused T00 TRX; `NOT RUN` means unselected, not skipped.

### CatalogControlTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Catalog_control_renders_active_state_and_keeps_compact_keyboard_layout`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L18) | Fact / 1 | NOT RUN |
| [`Catalog_control_pages_beyond_the_first_twenty_five_rows`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L52) | Fact / 1 | NOT RUN |
| [`Canceled_activation_does_not_start_a_catalog_query`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L84) | Fact / 1 | NOT RUN |
| [`Save_button_forwards_editor_values_and_shows_field_errors_without_modal_dialogs`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L105) | Fact / 1 | NOT RUN |
| [`Editor_fields_are_disabled_while_a_save_is_in_flight`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L144) | Fact / 1 | NOT RUN |
| [`Editor_fields_are_disabled_while_a_row_selection_is_in_flight`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L175) | Fact / 1 | NOT RUN |
| [`Enter_selects_the_active_catalog_row`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L212) | Fact / 1 | NOT RUN |
| [`All_catalog_list_cells_are_read_only`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L239) | Fact / 1 | NOT RUN |
| [`Enter_uses_the_immutable_row_id_instead_of_the_visible_cell_value`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L271) | Fact / 1 | NOT RUN |
| [`Enter_does_not_select_a_row_while_the_active_cell_is_editing`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L300) | Fact / 1 | NOT RUN |
| [`Background_view_model_updates_are_marshaled_to_the_control_thread`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L336) | Fact / 1 | NOT RUN |
| [`Disposing_control_disposes_view_model_and_prevents_delayed_work`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L362) | Fact / 1 | NOT RUN |
| [`Shell_reuses_catalog_tab_and_disposes_catalog_controls_on_sign_out`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L380) | Fact / 1 | NOT RUN |
| [`Reopening_existing_catalog_tab_refreshes_authoritative_data_without_new_control`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L412) | Fact / 1 | NOT RUN |

### DesktopCompositionTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Local_desktop_services_use_one_shared_weak_reference_messenger`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L26) | Fact / 1 | NOT RUN |
| [`Local_desktop_services_resolve_api_without_database_configuration`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L43) | Fact / 1 | NOT RUN |
| [`Local_api_returns_validation_failure_as_api_response`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L65) | Fact / 1 | NOT RUN |
| [`Local_api_honors_pre_canceled_request`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L91) | Fact / 1 | NOT RUN |
| [`Local_api_maps_handler_exceptions_and_reports_diagnostics`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L115) | Fact / 1 | NOT RUN |
| [`Local_catalog_clients_are_registered_and_map_application_errors_to_api_responses`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L144) | Fact / 1 | NOT RUN |
| [`Local_catalog_validation_fields_are_relative_to_public_request_dtos`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L169) | Fact / 1 | NOT RUN |
| [`WinForms_composition_resolves_goods_receipt_factory_and_shell`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L205) | Fact / 1 | NOT RUN |
| [`WinForms_composition_resolves_inventory_factory_and_controls`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L229) | Fact / 1 | NOT RUN |
| [`In_flight_catalog_mutation_keeps_authorized_actor_after_local_sign_out`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L254) | Fact / 1 | NOT RUN |
| [`Stale_login_error_keeps_request_correlation_and_cleans_up_scope`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L299) | Fact / 1 | NOT RUN |

### DesktopHostRunnerTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Smoke_mode_creates_and_renders_login_without_bypassing_authentication`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/DesktopHostRunnerTests.cs#L11) | Fact / 1 | NOT RUN |
| [`Production_smoke_resolves_login_inside_a_disposed_scope`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/DesktopHostRunnerTests.cs#L50) | Fact / 1 | NOT RUN |

### FoundationShellFormTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Shell_exposes_navigation_workspace_and_session_status`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L16) | Fact / 1 | NOT RUN |
| [`OpenWorkspace_reuses_existing_document_for_the_same_key`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L34) | Fact / 1 | NOT RUN |
| [`Inventory_navigation_opens_and_reuses_balance_and_stock_card_workspaces`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L64) | Fact / 1 | NOT RUN |
| [`ToggleTheme_switches_between_light_and_dark`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L108) | Fact / 1 | NOT RUN |
| [`Shell_reflects_shared_session_and_notification_changes`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L136) | Fact / 1 | NOT RUN |
| [`Sign_out_and_session_expiry_clear_the_session_with_explicit_status`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L163) | Fact / 1 | NOT RUN |
| [`Changing_authenticated_user_closes_the_previous_users_workspace`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L216) | Fact / 1 | NOT RUN |
| [`OpenWorkspace_rejects_content_after_sign_out`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L246) | Fact / 1 | NOT RUN |
| [`OpenWorkspace_rejects_a_stale_request_after_another_user_signs_in`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L277) | Fact / 1 | NOT RUN |
| [`Shell_binding_tracks_async_command_busy_and_ready_state`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L309) | Fact / 1 | NOT RUN |
| [`Session_and_notification_updates_are_marshaled_to_the_shell_thread`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L335) | Fact / 1 | NOT RUN |
| [`Session_change_before_handle_creation_is_reconciled_when_handle_is_created`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L384) | Fact / 1 | NOT RUN |
| [`Ready_view_model_state_before_handle_creation_is_reconciled`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L420) | Fact / 1 | NOT RUN |
| [`Busy_view_model_state_before_handle_creation_is_reconciled`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L448) | Fact / 1 | NOT RUN |
| [`Notification_before_handle_creation_is_reconciled`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L481) | Fact / 1 | NOT RUN |
| [`Goods_receipt_navigation_reuses_one_workspace_and_sign_out_disposes_it`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L510) | Fact / 1 | NOT RUN |

### GoodsReceiptControlTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Activation_and_selection_render_master_detail_lookups_total_and_read_only_state`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L17) | Fact / 1 | NOT RUN |
| [`Draft_post_button_posts_current_version_and_locks_the_editor`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L51) | Fact / 1 | NOT RUN |
| [`Draft_commands_update_lines_and_validation_errors_stay_inline`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L85) | Fact / 1 | NOT RUN |
| [`Draft_decimal_values_round_trip_at_the_contract_precision_and_range`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L115) | Fact / 1 | NOT RUN |
| [`In_flight_activation_disables_commands_until_the_list_finishes`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L150) | Fact / 1 | NOT RUN |
| [`Operation_errors_render_in_the_non_modal_status_area`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L176) | Fact / 1 | NOT RUN |
| [`Paging_and_explicit_filters_refresh_immediately`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L198) | Fact / 1 | NOT RUN |
| [`Lookup_text_searches_the_server_side_catalog_sources`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L229) | Fact / 1 | NOT RUN |
| [`Receipt_line_keeps_a_historical_product_selected_outside_the_active_page`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L257) | Fact / 1 | NOT RUN |
| [`Failed_selection_does_not_reload_the_previous_receipt_into_a_new_editor`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L282) | Fact / 1 | NOT RUN |
| [`Dirty_editor_blocks_selection_new_and_refresh_without_losing_input`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L309) | Fact / 1 | NOT RUN |
| [`Forbidden_refresh_clears_visible_receipt_data`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L341) | Fact / 1 | NOT RUN |
| [`Editing_a_line_after_save_updates_the_reloaded_line`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L370) | Fact / 1 | NOT RUN |
| [`Canceled_post_save_refresh_still_refreshes_list_and_reports_saved`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L395) | Fact / 1 | NOT RUN |
| [`Editing_a_line_after_refresh_updates_the_reloaded_line`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L437) | Fact / 1 | NOT RUN |
| [`Detail_inputs_stay_disabled_during_the_entire_refresh`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L462) | Fact / 1 | NOT RUN |
| [`Reactivation_after_access_revocation_clears_visible_receipt_data`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L493) | Fact / 1 | NOT RUN |
| [`Write_only_create_clears_sensitive_detail_and_locks_save_after_commit`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L520) | Fact / 1 | NOT RUN |
| [`Repeated_activation_refreshes_and_disposal_owns_both_view_models`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L564) | Fact / 1 | NOT RUN |
| [`Open_catalog_controls_refresh_receipt_lookups_without_losing_selected_references`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L585) | Fact / 1 | NOT RUN |

### GoodsReceiptEditorViewModelTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Create_sends_current_lines_then_refreshes_the_persisted_aggregate`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L17) | Fact / 1 | NOT RUN |
| [`Post_uses_persisted_identity_and_version_then_refreshes_the_posted_receipt`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L47) | Fact / 1 | NOT RUN |
| [`Update_reuses_opaque_version_and_conflict_preserves_user_input`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L78) | Fact / 1 | NOT RUN |
| [`Concurrent_save_requests_issue_only_one_create_command`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L103) | Fact / 1 | NOT RUN |
| [`Successful_create_keeps_identity_when_follow_up_refresh_fails`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L121) | Fact / 1 | NOT RUN |
| [`Immediate_validation_blocks_missing_fields_and_posted_receipts_are_read_only`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L149) | Fact / 1 | NOT RUN |
| [`Dirty_state_tracks_user_edits_and_resets_after_load_or_save`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L168) | Fact / 1 | NOT RUN |
| [`Lookup_metadata_changes_do_not_dirty_a_posted_receipt`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L190) | Fact / 1 | NOT RUN |
| [`Throwing_create_is_reported_inline_without_faulting_save`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L203) | Fact / 1 | NOT RUN |
| [`Throwing_update_is_reported_inline_without_faulting_save`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L224) | Fact / 1 | NOT RUN |
| [`Throwing_follow_up_get_preserves_successful_create_identity`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L246) | Fact / 1 | NOT RUN |
| [`Throwing_detail_refresh_is_reported_inline_without_replacing_the_editor`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L269) | Fact / 1 | NOT RUN |
| [`Delayed_refresh_applies_collection_mutations_on_the_owning_sta_thread`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L290) | Fact / 1 | NOT RUN |
| [`Cancellation_after_create_response_reports_saved_without_refreshing_details`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L330) | Fact / 1 | NOT RUN |
| [`Boundary_line_amount_overflow_is_safe_and_blocks_save`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L364) | Fact / 1 | NOT RUN |
| [`Aggregate_overflow_across_safe_lines_is_safe_and_blocks_save`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L383) | Fact / 1 | NOT RUN |
| [`Dispose_ignores_a_late_detail_refresh_response`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L407) | Fact / 1 | NOT RUN |

### InventoryControlTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Balance_control_shows_empty_state_after_first_successful_zero_row_query`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/InventoryControlTests.cs#L15) | Fact / 1 | NOT RUN |
| [`Stock_card_control_shows_empty_state_after_first_successful_zero_row_query`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/InventoryControlTests.cs#L39) | Fact / 1 | NOT RUN |
| [`Balance_control_renders_history_totals_and_runs_explicit_filters_immediately`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/InventoryControlTests.cs#L64) | Fact / 1 | NOT RUN |
| [`Stock_card_control_filters_without_debounce_and_reports_errors_without_dialogs`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/InventoryControlTests.cs#L104) | Fact / 1 | NOT RUN |
| [`Stock_card_control_applies_dates_chosen_before_ids_in_first_query`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/InventoryControlTests.cs#L148) | Fact / 1 | NOT RUN |

### LocalIdentityApiClientTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Successful_login_updates_the_shared_local_session_and_current_user_query`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L23) | Fact / 1 | NOT RUN |
| [`Signing_out_clears_the_server_current_user_for_subsequent_requests`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L44) | Fact / 1 | NOT RUN |
| [`Login_while_authenticated_is_rejected_without_replacing_the_current_user`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L64) | Fact / 1 | NOT RUN |
| [`Login_result_does_not_create_a_session_when_sql_account_validation_rejects_it`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L87) | Fact / 1 | NOT RUN |
| [`Disabled_account_revalidation_clears_the_desktop_session`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L112) | Fact / 1 | NOT RUN |
| [`Sql_validation_failure_is_not_reported_as_bad_credentials`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L139) | Fact / 1 | NOT RUN |
| [`Sql_failure_during_credential_lookup_is_reported_as_database_unavailable`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L160) | Fact / 1 | NOT RUN |
| [`Late_account_validation_from_user_a_cannot_expire_user_b_session`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L182) | Fact / 1 | NOT RUN |
| [`Late_current_user_validation_from_user_a_cannot_sign_out_user_b`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L219) | Fact / 1 | NOT RUN |
| [`Request_waiting_for_account_validation_is_rejected_after_session_switch`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L255) | Fact / 1 | NOT RUN |
| [`Late_validation_from_old_login_attempt_cannot_clear_new_session`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L296) | Fact / 1 | NOT RUN |
| [`Signing_out_invalidates_a_login_that_is_still_in_flight`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L329) | Fact / 1 | NOT RUN |
| [`Concurrent_login_attempts_allow_only_the_latest_attempt_to_publish_session`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L360) | Fact / 1 | NOT RUN |
| [`Parallel_local_requests_keep_the_actor_captured_for_each_session`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L398) | Fact / 1 | NOT RUN |
| [`Canceled_login_cannot_establish_a_session_when_the_handler_ignores_cancellation`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L440) | Fact / 1 | NOT RUN |
| [`Canceled_login_cannot_establish_a_session_when_account_validation_ignores_cancellation`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L469) | Fact / 1 | NOT RUN |
| [`Canceled_login_cleans_up_when_account_validation_observes_cancellation`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L500) | Fact / 1 | NOT RUN |
| [`Explicit_logout_then_pending_login_invalidates_the_old_principals_desktop_requests`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L530) | Fact / 1 | NOT RUN |

### LoginFormTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Login_form_is_compact_keyboard_accessible_and_masks_password`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LoginFormTests.cs#L18) | Fact / 1 | PASS |
| [`Login_form_renders_after_theme_change_and_disposes_without_ui_exceptions`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/LoginFormTests.cs#L49) | Fact / 1 | PASS |

### MasterDetailPrototypeControlTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`SetRows_projects_master_rows_into_the_source_grid`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/MasterDetailPrototypeControlTests.cs#L12) | Fact / 1 | NOT RUN |
| [`Lookup_and_field_errors_follow_reusable_contracts`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/MasterDetailPrototypeControlTests.cs#L34) | Fact / 1 | NOT RUN |
| [`Editor_panel_keeps_a_usable_width_after_the_control_is_resized`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/MasterDetailPrototypeControlTests.cs#L65) | Fact / 1 | NOT RUN |

### PermissionActionBinderTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Action_stays_disabled_until_permission_is_granted`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/PermissionActionBinderTests.cs#L14) | Fact / 1 | NOT RUN |
| [`Action_is_disabled_when_permission_is_denied_or_cannot_be_verified`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/PermissionActionBinderTests.cs#L40) | Fact / 1 | NOT RUN |
| [`Delayed_permission_result_cannot_reenable_an_action_after_sign_out`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/PermissionActionBinderTests.cs#L75) | Fact / 1 | NOT RUN |
| [`Queued_permission_grant_cannot_reenable_an_action_after_sign_out`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/PermissionActionBinderTests.cs#L102) | Fact / 1 | NOT RUN |
| [`Queued_permission_grant_cannot_reenable_an_action_after_binder_disposal`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/PermissionActionBinderTests.cs#L130) | Fact / 1 | NOT RUN |
| [`Permission_update_without_a_control_handle_is_marshaled_to_the_ui_thread`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/PermissionActionBinderTests.cs#L165) | Fact / 1 | NOT RUN |
| [`Previous_sessions_permission_result_cannot_override_a_new_grant`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/PermissionActionBinderTests.cs#L205) | Fact / 1 | NOT RUN |
| [`Older_permission_result_cannot_override_the_latest_same_session_result`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/PermissionActionBinderTests.cs#L239) | Fact / 1 | NOT RUN |

### ProductionDiagnosticsTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Production_registration_writes_structured_audit_with_request_correlation`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/ProductionDiagnosticsTests.cs#L19) | Fact / 1 | NOT RUN |
| [`Redactor_removes_credentials_and_nested_sql_exception_details`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/ProductionDiagnosticsTests.cs#L65) | Fact / 1 | NOT RUN |
| [`Unwritable_log_location_uses_non_modal_fallback_sink`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/ProductionDiagnosticsTests.cs#L87) | Fact / 1 | NOT RUN |
| [`Runtime_audit_file_failure_degrades_health_and_reports_committed_state`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/ProductionDiagnosticsTests.cs#L122) | Fact / 1 | NOT RUN |
| [`Retention_cleanup_failure_does_not_disable_subsequent_audit_writes`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/ProductionDiagnosticsTests.cs#L167) | Fact / 1 | NOT RUN |
| [`Sharing_violation_reports_failure_and_recovers_on_the_next_audit_write`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/ProductionDiagnosticsTests.cs#L210) | Fact / 1 | NOT RUN |
| [`Desktop_exception_reporter_writes_redacted_diagnostic_with_support_id`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/ProductionDiagnosticsTests.cs#L264) | Fact / 1 | NOT RUN |

### ReleasePackagingTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Manifest_only_mode_hashes_active_desktop_and_separate_admin_package`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/ReleasePackagingTests.cs#L14) | Fact / 1 | NOT RUN |
| [`Packages_without_runtime_schema_policy_are_rejected`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/ReleasePackagingTests.cs#L49) | Fact / 1 | NOT RUN |
| [`Desktop_packages_reject_admin_tools_and_embedded_secrets`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/ReleasePackagingTests.cs#L67) | Theory / 2 | NOT RUN |

### SecurityAdministrationFormTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Administration_control_loads_and_disposes_on_serialized_sta_without_modal_errors`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/SecurityAdministrationFormTests.cs#L19) | Fact / 1 | NOT RUN |
| [`Security_workspace_is_session_scoped_and_closes_on_sign_out`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/SecurityAdministrationFormTests.cs#L60) | Fact / 1 | NOT RUN |

### UiDependencyCompatibilityTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Bootstrap_source_grid_tracks_runtime_theme_changes`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/UiDependencyCompatibilityTests.cs#L11) | Fact / 1 | NOT RUN |
| [`Bootstrap_source_grid_constructs_and_renders_without_host_startup_state`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/UiDependencyCompatibilityTests.cs#L39) | Fact / 1 | NOT RUN |

### WinFormsTestInfrastructureTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`StaTest_runs_action_on_sta_thread`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/WinFormsTestInfrastructureTests.cs#L12) | Fact / 1 | PASS |
| [`StaTest_cancels_timed_out_action_and_waits_for_cleanup`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/WinFormsTestInfrastructureTests.cs#L24) | Fact / 1 | PASS |
| [`StaTest_rethrows_action_failure_on_test_thread`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/WinFormsTestInfrastructureTests.cs#L47) | Fact / 1 | PASS |
| [`WinFormsTestGuard_captures_thread_exception`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/WinFormsTestInfrastructureTests.cs#L58) | Fact / 1 | PASS |
| [`WinFormsTestGuard_fails_when_captured_exception_is_not_consumed`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/WinFormsTestInfrastructureTests.cs#L77) | Fact / 1 | PASS |
| [`WinFormsTestGuard_suppresses_error_dialogs_on_current_sta_thread`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/WinFormsTestInfrastructureTests.cs#L96) | Fact / 1 | PASS |
| [`WinFormsTestGuard_disposal_does_not_disable_another_sta_thread_guard`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/WinFormsTestInfrastructureTests.cs#L110) | Fact / 1 | PASS |
| [`WinFormsTestGuard_captures_grid_data_error_without_throwing`](https://github.com/chung6a8m/MyDmsVn/blob/f275183c8b39dc2b712dc37be7f0f4ac71396357/tests/MyDmsVn.Desktop.Tests/WinFormsTestInfrastructureTests.cs#L150) | Fact / 1 | PASS |

Theory row reconciliation: admin executable payload (1) and embedded-configuration payload (1), both discovered / NOT RUN. Total: **138 methods / 139 invocations**, matching VSTest discovery.


## Appendix B. T01 current-source additions and outcomes

Candidate `32ea40c`: **149 methods / 153 discovered and executed invocations**, all PASS, including all 139 T00 invocations. This is an unfiltered T01 regression, not T06/final P5 acceptance. Appendix A retains frozen T00 names/outcomes and now links to that exact source commit. The 11 new methods below supply 14 invocations; [verification](GUI_P5_VERIFICATION.md) records commands, TRX, cleanup, development failures and review.

| New method / source | Type / rows | T01 outcome |
|---|---|---|
| [`GuiOwnedProcessTests.Large_stdout_and_stderr_are_drained_without_deadlock_and_retention_is_bounded`](../tests/MyDmsVn.Desktop.Tests/GuiOwnedProcessTests.cs#L18) | Fact / 1 | PASS |
| [`GuiOwnedProcessTests.Timeout_terminates_and_drains_owned_GUI_and_descendants`](../tests/MyDmsVn.Desktop.Tests/GuiOwnedProcessTests.cs#L31) | Theory / 3 | PASS |
| [`GuiOwnedProcessTests.Cancellation_after_start_drains_the_owned_tree_before_returning`](../tests/MyDmsVn.Desktop.Tests/GuiOwnedProcessTests.cs#L45) | Fact / 1 | PASS |
| [`GuiOwnedProcessTests.Parent_exit_does_not_leave_a_descendant_holding_redirected_pipes`](../tests/MyDmsVn.Desktop.Tests/GuiOwnedProcessTests.cs#L68) | Fact / 1 | PASS |
| [`GuiOwnedProcessTests.Fatal_child_failure_returns_exit_and_diagnostics_without_poisoning_testhost`](../tests/MyDmsVn.Desktop.Tests/GuiOwnedProcessTests.cs#L78) | Fact / 1 | PASS |
| [`GuiOwnedProcessTests.Pre_canceled_launch_creates_no_child`](../tests/MyDmsVn.Desktop.Tests/GuiOwnedProcessTests.cs#L89) | Fact / 1 | PASS |
| [`GuiOwnedProcessTests.Pipe_read_failure_keeps_already_captured_failure_diagnostics`](../tests/MyDmsVn.Desktop.Tests/GuiOwnedProcessTests.cs#L100) | Fact / 1 | PASS |
| [`LoginFormTests.Closed_login_ignores_late_success_or_failure_without_notifications_or_dialogs`](../tests/MyDmsVn.Desktop.Tests/LoginFormTests.cs#L130) | Theory / 2 | PASS |
| [`LoginFormTests.Login_error_reporter_failure_keeps_safe_inline_error_without_modal_dialog`](../tests/MyDmsVn.Desktop.Tests/LoginFormTests.cs#L168) | Fact / 1 | PASS |
| [`LoginFormTests.Background_login_completion_publishes_state_on_the_form_owner_thread`](../tests/MyDmsVn.Desktop.Tests/LoginFormTests.cs#L199) | Fact / 1 | PASS |
| [`WinFormsTestInfrastructureTests.StaTest_timeout_preserves_failure_raised_during_cooperative_cleanup`](../tests/MyDmsVn.Desktop.Tests/WinFormsTestInfrastructureTests.cs#L64) | Fact / 1 | PASS |

## Appendix C — GUI-P5-T02 deterministic control closure

**Date/tester:** 2026-10-19, Codex. **Executable revision:** 3a0621c65a2b584d2dfbe99b01febe915769bb44. T00 section 3 is the original gap inventory; this appendix reconciles every T02 obligation against current named tests. See [T02 verification](GUI_P5_VERIFICATION.md#gui-p5-t02--deterministic-control-and-viewmodel-coverage) for actual results and failed development trials.

D = Desktop.Tests; S = Desktop.Session.Tests. The new seven acceptance classes add **59 methods / 129 invocations**: 31 Facts plus 28 Theories with 98 rows. Existing T01 methods remain present: current Desktop **208 methods / 282 invocations** (177 Facts, 31 Theories / 105 rows). Pure Session behavior reuses its existing **108 invocations**; no test project, framework or dependency was added.

The tables establish deterministic control/VM contracts only. Injected responses prove command forwarding, disabled/busy state, safe inline errors and no UI retry/publication after denial or cancellation. They do not prove actual SQL authorization, disabled-account enforcement, transaction atomicity, authentication or a persisted business result. Section 3's independent backend oracles and T03–T06 obligations remain mandatory.

| ID | Exact current anchors | T02 interpretation / remaining control issue |
|---|---|---|
| L1 | D `LoginSessionAcceptanceTests.Login_validation_focuses_the_first_invalid_field_without_dispatch`; `Login_inputs_have_distinct_accessible_names_and_password_visibility_toggle`; `Escape_requests_cancel_and_closed_login_suppresses_a_late_authentication_result`; `Rejected_login_keeps_form_open_with_safe_inline_error_and_retry_focus`; `Enter_submits_once_while_busy_and_success_closes_the_login_form`; `Login_tab_order_focus_and_layout_are_usable_in_each_theme`; D `LoginFormTests.Closed_login_ignores_late_success_or_failure_without_notifications_or_dialogs`; S `LoginViewModelTests.Disabled_session_response_uses_explicit_vietnamese_reauthentication_message` | New controls prove empty/error/Enter/Escape/busy/retry/accessible fields. The modeless control test verifies Escape requests DialogResult.Cancel, then closes/drains the form and discards the late result; actual modal host lifecycle remains T03; rejection Theory now includes Auth.Unauthorized along with InvalidCredentials, SessionChanged and Database.Timeout. These are response-binding checks; actual restricted/disabled-account authentication remains T03. |
| L2 | D `LoginSessionAcceptanceTests.Closed_shell_drains_late_initialization_without_affecting_the_next_user`; `Session_expiry_clears_private_workspace_and_relogin_rejects_the_old_workspace_version`; D `FoundationShellFormTests.Sign_out_and_session_expiry_clear_the_session_with_explicit_status`; `Changing_authenticated_user_closes_the_previous_users_workspace`; `Goods_receipt_navigation_reuses_one_workspace_and_sign_out_disposes_it`; D `CatalogControlTests.Shell_reuses_catalog_tab_and_disposes_catalog_controls_on_sign_out`; D `SecurityAdministrationFormTests.Security_workspace_is_session_scoped_and_closes_on_sign_out`; S `DesktopSessionExpiryTests.Idle_boundary_expires_session_and_background_dispatch_does_not_reset_activity`; D `LocalIdentityApiClientTests.Late_current_user_validation_from_user_a_cannot_sign_out_user_b` | Shell tests establish disposal/version fencing, complemented by domain-specific late-disposal anchors below. ReceiptAcceptanceTests.Session_switch_disposes_pending_receipt_save_and_old_completion_cannot_reach_next_users_workspace adds four actual Receipt/shell rows (direct user switch or sign-out, late Save success/failure); default real-package idle duration remains T03. |
| C1 | D `CatalogAcceptanceTests.Catalog_buttons_create_update_deactivate_and_reactivate_all_entity_fields` (Product row); `Entity_field_validation_is_inline_preserves_input_and_clears_for_new_editor` (unit row); `Rejected_mutations_keep_visible_rows_and_report_non_modal_error` (Product x3 statuses); D `CatalogControlTests.Catalog_control_renders_active_state_and_keeps_compact_keyboard_layout`; S `CatalogViewModelTests.Product_create_update_and_set_active_publish_one_typed_message_after_each_success` | Real buttons and Enter selection forward all Product fields, display saved rows and both active states; rejection leaves displayed data/editor input intact. No fake establishes server permission enforcement. |
| C2 | D same three `CatalogAcceptanceTests` methods (Warehouse/Employee/Customer rows); `Employee_invalid_user_id_blocks_API_then_blank_explicitly_unlinks`; S `CatalogViewModelTests.Every_catalog_create_publishes_its_own_kind_and_entity_id`; `Warehouse_and_employee_status_changes_publish_typed_messages`; `Employee_user_id_rejects_invalid_text_and_blank_explicitly_unlinks` | All four actual adapters/editors covered, including address/phone/taxCode and valid/invalid/blank employee UserId. Server field validation is injected response presentation, not independent validation proof. |
| C3 | D `CatalogAcceptanceTests.Paging_and_immediate_refresh_keep_page_but_changed_search_resets_it`; `Rapid_control_search_queries_only_latest_text_after_controlled_debounce`; `Search_cancels_inflight_list_and_late_old_response_cannot_replace_grid`; `Disposal_cancels_pending_control_search_and_discards_late_list_completion`; D `CatalogControlTests.Reopening_existing_catalog_tab_refreshes_authoritative_data_without_new_control`; S `CatalogViewModelTests.Search_intent_invalidates_an_inflight_refresh_before_debounce_elapses`; `Explicit_refresh_cancels_a_pending_message_reload_instead_of_querying_twice` and `Search_supersedes_a_message_reload_callback_already_queued_for_ui_dispatch` | Immediate Refresh retains current page, changed search resets to1. Controlled delay proves 300ms contract and canceled superseded intents without wall-clock sleep. No behavior change to Refresh policy. |
| K1 | D `GoodsReceiptControlTests.Open_catalog_controls_refresh_receipt_lookups_without_losing_selected_references`; `Receipt_line_keeps_a_historical_product_selected_outside_the_active_page`; D `ReceiptAcceptanceTests.Reopened_receipt_fetches_fresh_active_options_for_all_three_lookup_kinds`; S `CatalogLookupViewModelTests.Relevant_message_bursts_refresh_two_recipients_and_disposal_stops_one`; `Inactive_historical_selection_remains_visible_but_inactive_results_are_not_new_options`; `Selected_active_item_becomes_unavailable_after_deactivation_message` | Existing real catalog Deactivate/Activate buttons for Product/Warehouse/Employee refresh the already-open receipt while retaining selected historical IDs. New reopen test changes sources without messenger and proves fresh active-only lists. Same-process scope only. |
| R1 | D `ReceiptAcceptanceTests.Rowversion_conflict_is_inline_and_keeps_dirty_header_line_and_opaque_version`; D `GoodsReceiptControlTests.Draft_commands_update_lines_and_validation_errors_stay_inline`; `Draft_decimal_values_round_trip_at_the_contract_precision_and_range`; `Dirty_editor_blocks_selection_new_and_refresh_without_losing_input`; `Editing_a_line_after_save_updates_the_reloaded_line`; D `GoodsReceiptEditorViewModelTests.Boundary_line_amount_overflow_is_safe_and_blocks_save`; `Aggregate_overflow_across_safe_lines_is_safe_and_blocks_save`; S `GoodsReceiptListViewModelTests.Late_old_list_and_get_responses_cannot_replace_newer_state` | Conflict keeps note/line values/opaque ExpectedVersion and performs no blind refresh. Existing control checks supply create/edit/validation/dirty paths; arithmetic overflow pure paths reused. D `ReceiptAcceptanceTests.Session_switch_disposes_pending_receipt_save_and_old_completion_cannot_reach_next_users_workspace` covers pending Save, shell disposal/cancellation and no next-user UI/notification contamination (four rows). |
| R2 | D `ReceiptAcceptanceTests.Pending_post_disables_every_mutating_control_and_repeated_clicks_issue_one_command`; `Uncertain_post_requires_read_reload_before_another_mutating_command`; `Disposal_cancels_pending_post_and_late_success_cannot_publish_or_refresh`; D `GoodsReceiptControlTests.Draft_post_button_posts_current_version_and_locks_the_editor`; D `GoodsReceiptEditorViewModelTests.Post_uses_persisted_identity_and_version_then_refreshes_the_posted_receipt` | Post expected-version forwarding, busy duplicate suppression, Posted editor lock and uncertain response both committed/not-committed x exception/API-failure are explicit. Failed outcome reload leaves mutation locked; successful read reload resolves state. SQL exactly-once/reconciliation remains backend/T03. |
| R3 | D `ReceiptAcceptanceTests.Rejected_save_or_post_renders_authorization_error_without_success_or_followup_reads`; `Post_only_actor_with_denied_read_cannot_post_an_unloaded_receipt`; D `GoodsReceiptControlTests.Forbidden_refresh_clears_visible_receipt_data`; `Reactivation_after_access_revocation_clears_visible_receipt_data`; `Write_only_create_clears_sensitive_detail_and_locks_save_after_commit`; D `LoginSessionAcceptanceTests.Canceled_permission_check_cannot_enable_an_action_when_client_ignores_cancellation`; `Disposed_action_ignores_delayed_permission_grant`; D `PermissionActionBinderTests.Delayed_permission_result_cannot_reenable_an_action_after_sign_out` | Save/Post negative rows cover Forbidden/UserInactive/SessionExpired; existing Write-only/Read-denied paths reused. Direct Deny vs grant is not encoded as a trusted UI-side authorization evaluator and must remain server/T03 proof. |
| I1 | D `InventoryAcceptanceTests.Paging_renders_response_order_and_inactive_history_and_filter_change_returns_to_first_page`; `Pending_query_disables_grid_refresh_and_paging_then_recovers_after_completion`; `Revoked_or_expired_read_clears_rows_totals_and_paging_and_can_refresh_after_access_returns`; `Late_success_from_canceled_filter_cannot_repopulate_inventory_after_newer_denial`; `Disposed_inventory_control_cancels_query_and_ignores_late_rows_and_notifications`; `Validation_error_is_inline_clears_stale_rows_and_explicit_refresh_recovers`; D `InventoryControlTests.Stock_card_control_applies_dates_chosen_before_ids_in_first_query`; `Balance_control_shows_empty_state_after_first_successful_zero_row_query`; `Stock_card_control_shows_empty_state_after_first_successful_zero_row_query`; S `InventoryViewModelTests.Balance_does_not_total_quantities_without_a_single_product_filter`; `Stock_card_selection_date_and_refresh_are_immediate_and_discard_late_responses` | Both Balance/Card controls cover response order/history/page/reset/busy/errors/revocation/stale/disposal. Exact SQL datetime2 boundary semantics remain backend oracle. |
| S1 | D `SecurityControlAcceptanceTests.Protected_bound_user_disables_edit_membership_and_import_without_dispatch`; `User_selection_and_direct_decision_buttons_dispatch_selected_identity_and_revision`; `Role_selection_limits_catalog_to_active_permissions_and_dispatches_grant_or_remove`; `User_membership_checkbox_uses_both_assignment_revisions_once`; `Revision_conflict_renders_safe_error_refreshes_rows_and_does_not_retry`; `Forbidden_reads_disable_only_the_affected_management_tabs_and_show_server_error`; `New_user_required_field_validation_renders_without_a_request_or_modal`; `Session_revocation_discards_delayed_load_and_clears_control_rows`; `Import_button_previews_exact_json_confirms_additions_then_applies_preview_revision`; `Declined_import_confirmation_does_not_apply_any_assignment`; `Import_revision_conflict_clears_preview_refreshes_and_renders_error_without_retry`; `Late_import_preview_after_revocation_or_disposal_cannot_confirm_or_apply`; `Changing_selected_target_while_import_preview_is_pending_cannot_confirm_the_old_target`; `User_management_buttons_forward_editor_values_and_selected_revision`; `Create_user_button_forwards_new_editor_and_clears_temporary_password`; `Role_management_buttons_forward_selected_role_revision`; `New_role_button_clears_selected_identity_before_create`; `Deprecated_permission_is_visible_for_user_but_cannot_be_assigned`; S `SecurityAdministrationViewModelTests.Delayed_mutation_from_previous_session_cannot_update_or_submit_after_relogin`; `Superuser_status_is_not_inferred_from_role_name`; S `PermissionImportViewModelTests.Selection_change_invalidates_preview_before_apply` | Also D `SecurityControlAcceptanceTests.Moving_between_columns_of_the_same_user_keeps_editor_and_preview` and `Changing_target_mode_while_user_import_preview_is_pending_cannot_confirm_old_user`. Controls cover target/revision/permission display/mutations/import/confirmation/staleness/protected-user/session-revocation. SQL protected binding, audit and Deny precedence remain backend/T03. |
| F1 | D `CatalogAcceptanceTests.Disposal_cancels_pending_control_search_and_discards_late_list_completion`; D `ReceiptAcceptanceTests.Disposal_cancels_pending_post_and_late_success_cannot_publish_or_refresh`; D `InventoryAcceptanceTests.Disposed_inventory_control_cancels_query_and_ignores_late_rows_and_notifications`; D `SecurityControlAcceptanceTests.Disposed_control_discards_delayed_background_load_and_unsubscribes_session`; D `LoginFormTests.Closed_login_ignores_late_success_or_failure_without_notifications_or_dialogs`; `Login_error_reporter_failure_keeps_safe_inline_error_without_modal_dialog`; D `LoginSessionAcceptanceTests.Closed_shell_drains_late_initialization_without_affecting_the_next_user`; existing D `GuiOwnedProcessTests.Timeout_terminates_and_drains_owned_GUI_and_descendants`; `Fatal_child_failure_returns_exit_and_diagnostics_without_poisoning_testhost`; D `WinFormsTestInfrastructureTests.WinFormsTestGuard_captures_thread_exception`; `WinFormsTestGuard_captures_grid_data_error_without_throwing`; D `ProductionDiagnosticsTests.Redactor_removes_credentials_and_nested_sql_exception_details` | Reuse T01 subprocess/guard proof plus T02 domain disposal/failure paths. StaTest is cooperative; these tests do not give it the ability to kill a noncooperative STA. |
| F2 | D `ReleasePackagingTests.Manifest_only_mode_hashes_active_desktop_and_separate_admin_package`; `Packages_without_runtime_schema_policy_are_rejected`; `Desktop_packages_reject_admin_tools_and_embedded_secrets`; D `DesktopCompositionTests.WinForms_composition_resolves_goods_receipt_factory_and_shell`; `WinForms_composition_resolves_inventory_factory_and_controls`; D `DesktopHostRunnerTests.Smoke_mode_creates_and_renders_login_without_bypassing_authentication` | Existing regression anchors only. T02 has no real-package Login-first E2E, x86 deployment or schema acceptance claim; T03/REL retain those gates. |
| U1 | D `CatalogAccessibilityAcceptanceTests.Catalog_entity_fields_render_inside_editor_and_are_reachable_by_native_tab_navigation`; D `InventoryAccessibilityAcceptanceTests.Stock_filters_expose_distinct_meaningful_names_to_accessibility_clients`; `Stock_filter_tab_focus_and_readonly_rows_survive_theme_render_and_layout`; `Receipt_supports_enter_selection_filter_and_editor_tab_focus_and_theme_render`; `Posted_receipt_disables_mutation_and_excludes_editors_from_focus_in_each_theme`; D `SecurityControlAcceptanceTests.Security_tabs_keep_read_only_grids_accessible_inputs_and_keyboard_focus_at_supported_width`; D `LoginSessionAcceptanceTests.Login_tab_order_focus_and_layout_are_usable_in_each_theme`; D `CatalogControlTests.All_catalog_list_cells_are_read_only`; D `FoundationShellFormTests.ToggleTheme_switches_between_light_and_dark` | Real native tab/focus/layout/render for all four catalog kinds x2 themes plus Receipt/Stock/Security/Login. Search/field selector semantics covered at control-property level; runtime UIA patterns and DPI matrix remain T03. Catalog wrapper Focus() false was a test assumption: BootstrapTextBox.OnEnter moves focus to its native TextBox; ContainsFocus is the correct observable. |

### Explicit visual and runtime limitations

Native SelectNextControl/ContainsFocus checks, property-level accessibility names, editor bounds and Light/Dark bitmap rendering ran at the current 100% desktop. They establish reachable controls, read-only focus behavior and structural layout. Human readability, visual contrast, real OS file/confirmation dialog usability, assistive-technology behavior, actual packaged modal Login/logout/restart, supported 125% DPI and native UIA patterns still require T03–T06. These are bounded task ownership/attended visual limitations, not a waiver of mandatory automation. No shared desktop settings were changed and no 150% mandatory gate was invented.

### Complete T02 additions

Each class is in `tests/MyDmsVn.Desktop.Tests/<Class>.cs`; names below are exact FullyQualifiedName suffixes. Theory arguments and synthetic values remain in those source files; final TRX reconciles every row, including all 153 earlier invocations.

| Class / named method | Type | Rows |
|---|---|---:|
| `CatalogAcceptanceTests.Catalog_buttons_create_update_deactivate_and_reactivate_all_entity_fields` | Theory | 4 |
| `CatalogAcceptanceTests.Entity_field_validation_is_inline_preserves_input_and_clears_for_new_editor` | Theory | 4 |
| `CatalogAcceptanceTests.Rejected_mutations_keep_visible_rows_and_report_non_modal_error` | Theory | 12 |
| `CatalogAcceptanceTests.Employee_invalid_user_id_blocks_API_then_blank_explicitly_unlinks` | Theory | 4 |
| `CatalogAcceptanceTests.Paging_and_immediate_refresh_keep_page_but_changed_search_resets_it` | Theory | 4 |
| `CatalogAcceptanceTests.Rapid_control_search_queries_only_latest_text_after_controlled_debounce` | Fact | 1 |
| `CatalogAcceptanceTests.Search_cancels_inflight_list_and_late_old_response_cannot_replace_grid` | Fact | 1 |
| `CatalogAcceptanceTests.Disposal_cancels_pending_control_search_and_discards_late_list_completion` | Fact | 1 |
| `CatalogAccessibilityAcceptanceTests.Catalog_entity_fields_render_inside_editor_and_are_reachable_by_native_tab_navigation` | Theory | 8 |
| `InventoryAcceptanceTests.Paging_renders_response_order_and_inactive_history_and_filter_change_returns_to_first_page` | Theory | 2 |
| `InventoryAcceptanceTests.Pending_query_disables_grid_refresh_and_paging_then_recovers_after_completion` | Theory | 2 |
| `InventoryAcceptanceTests.Revoked_or_expired_read_clears_rows_totals_and_paging_and_can_refresh_after_access_returns` | Theory | 4 |
| `InventoryAcceptanceTests.Late_success_from_canceled_filter_cannot_repopulate_inventory_after_newer_denial` | Theory | 2 |
| `InventoryAcceptanceTests.Disposed_inventory_control_cancels_query_and_ignores_late_rows_and_notifications` | Theory | 2 |
| `InventoryAcceptanceTests.Validation_error_is_inline_clears_stale_rows_and_explicit_refresh_recovers` | Theory | 2 |
| `InventoryAccessibilityAcceptanceTests.Stock_filters_expose_distinct_meaningful_names_to_accessibility_clients` | Theory | 2 |
| `InventoryAccessibilityAcceptanceTests.Stock_filter_tab_focus_and_readonly_rows_survive_theme_render_and_layout` | Theory | 4 |
| `InventoryAccessibilityAcceptanceTests.Receipt_supports_enter_selection_filter_and_editor_tab_focus_and_theme_render` | Theory | 2 |
| `InventoryAccessibilityAcceptanceTests.Posted_receipt_disables_mutation_and_excludes_editors_from_focus_in_each_theme` | Theory | 2 |
| `LoginSessionAcceptanceTests.Login_validation_focuses_the_first_invalid_field_without_dispatch` | Fact | 1 |
| `LoginSessionAcceptanceTests.Login_inputs_have_distinct_accessible_names_and_password_visibility_toggle` | Fact | 1 |
| `LoginSessionAcceptanceTests.Escape_requests_cancel_and_closed_login_suppresses_a_late_authentication_result` | Fact | 1 |
| `LoginSessionAcceptanceTests.Rejected_login_keeps_form_open_with_safe_inline_error_and_retry_focus` | Theory | 4 |
| `LoginSessionAcceptanceTests.Enter_submits_once_while_busy_and_success_closes_the_login_form` | Fact | 1 |
| `LoginSessionAcceptanceTests.Login_tab_order_focus_and_layout_are_usable_in_each_theme` | Theory | 2 |
| `LoginSessionAcceptanceTests.Closed_shell_drains_late_initialization_without_affecting_the_next_user` | Theory | 2 |
| `LoginSessionAcceptanceTests.Session_expiry_clears_private_workspace_and_relogin_rejects_the_old_workspace_version` | Fact | 1 |
| `LoginSessionAcceptanceTests.Canceled_permission_check_cannot_enable_an_action_when_client_ignores_cancellation` | Fact | 1 |
| `LoginSessionAcceptanceTests.Disposed_action_ignores_delayed_permission_grant` | Fact | 1 |
| `ReceiptAcceptanceTests.Rowversion_conflict_is_inline_and_keeps_dirty_header_line_and_opaque_version` | Fact | 1 |
| `ReceiptAcceptanceTests.Pending_post_disables_every_mutating_control_and_repeated_clicks_issue_one_command` | Fact | 1 |
| `ReceiptAcceptanceTests.Uncertain_post_requires_read_reload_before_another_mutating_command` | Theory | 4 |
| `ReceiptAcceptanceTests.Rejected_save_or_post_renders_authorization_error_without_success_or_followup_reads` | Theory | 6 |
| `ReceiptAcceptanceTests.Post_only_actor_with_denied_read_cannot_post_an_unloaded_receipt` | Fact | 1 |
| `ReceiptAcceptanceTests.Disposal_cancels_pending_post_and_late_success_cannot_publish_or_refresh` | Fact | 1 |
| `ReceiptAcceptanceTests.Reopened_receipt_fetches_fresh_active_options_for_all_three_lookup_kinds` | Fact | 1 |
| `ReceiptAcceptanceTests.Session_switch_disposes_pending_receipt_save_and_old_completion_cannot_reach_next_users_workspace` | Theory | 4 |
| `SecurityControlAcceptanceTests.Protected_bound_user_disables_edit_membership_and_import_without_dispatch` | Fact | 1 |
| `SecurityControlAcceptanceTests.User_selection_and_direct_decision_buttons_dispatch_selected_identity_and_revision` | Theory | 3 |
| `SecurityControlAcceptanceTests.Role_selection_limits_catalog_to_active_permissions_and_dispatches_grant_or_remove` | Theory | 2 |
| `SecurityControlAcceptanceTests.User_membership_checkbox_uses_both_assignment_revisions_once` | Fact | 1 |
| `SecurityControlAcceptanceTests.Revision_conflict_renders_safe_error_refreshes_rows_and_does_not_retry` | Fact | 1 |
| `SecurityControlAcceptanceTests.Forbidden_reads_disable_only_the_affected_management_tabs_and_show_server_error` | Fact | 1 |
| `SecurityControlAcceptanceTests.New_user_required_field_validation_renders_without_a_request_or_modal` | Fact | 1 |
| `SecurityControlAcceptanceTests.Session_revocation_discards_delayed_load_and_clears_control_rows` | Fact | 1 |
| `SecurityControlAcceptanceTests.Disposed_control_discards_delayed_background_load_and_unsubscribes_session` | Fact | 1 |
| `SecurityControlAcceptanceTests.Security_tabs_keep_read_only_grids_accessible_inputs_and_keyboard_focus_at_supported_width` | Theory | 2 |
| `SecurityControlAcceptanceTests.Import_button_previews_exact_json_confirms_additions_then_applies_preview_revision` | Fact | 1 |
| `SecurityControlAcceptanceTests.Declined_import_confirmation_does_not_apply_any_assignment` | Fact | 1 |
| `SecurityControlAcceptanceTests.Import_revision_conflict_clears_preview_refreshes_and_renders_error_without_retry` | Fact | 1 |
| `SecurityControlAcceptanceTests.Late_import_preview_after_revocation_or_disposal_cannot_confirm_or_apply` | Theory | 2 |
| `SecurityControlAcceptanceTests.Changing_selected_target_while_import_preview_is_pending_cannot_confirm_the_old_target` | Fact | 1 |
| `SecurityControlAcceptanceTests.User_management_buttons_forward_editor_values_and_selected_revision` | Theory | 4 |
| `SecurityControlAcceptanceTests.Create_user_button_forwards_new_editor_and_clears_temporary_password` | Fact | 1 |
| `SecurityControlAcceptanceTests.Role_management_buttons_forward_selected_role_revision` | Theory | 3 |
| `SecurityControlAcceptanceTests.New_role_button_clears_selected_identity_before_create` | Fact | 1 |
| `SecurityControlAcceptanceTests.Deprecated_permission_is_visible_for_user_but_cannot_be_assigned` | Fact | 1 |
| `SecurityControlAcceptanceTests.Moving_between_columns_of_the_same_user_keeps_editor_and_preview` | Fact | 1 |
| `SecurityControlAcceptanceTests.Changing_target_mode_while_user_import_preview_is_pending_cannot_confirm_old_user` | Fact | 1 |

## Appendix D — T03 packaged real-login automation (COMPLETE in task scope)

**Frozen executable/package/test source:** `02eff225d41ff564769bedbb0ca4420d27233a67`, clean `gui-p5-t03-candidate3`; full hashes, topology, commands and oracles are in [T03 verification](GUI_P5_VERIFICATION.md#final-frozen-candidate--gui-p5-t03). This appendix updates current automation readiness; the T00 inventory above remains historical evidence. The owner confirmed that the designated non-production SQL instance is privately managed and approved for T03. Actual SQL2017 Developer14.0.2130.4 was provisioned with separate per-case GUI databases and migration/runtime/oracle principals; current schema001–009, restricted runtime grants, ownership and positive cleanup passed.

| Obligation | Named packaged methods (`PackagedGuiAcceptanceTests`) | Actual100% Light/Dark | Actual125% Light/Dark |
|---|---|---|---|
| L1 / real authentication and session lifecycle | `Actual_packaged_login_uses_native_uia_and_authenticates_against_the_owned_database` | 2/2 PASS | 2/2 PASS |
| C1 / all four catalog create/edit/active actions | `Catalog_rows_support_semantic_uia_selection_and_persist_create_edit_active_changes` | 2/2 PASS | 2/2 PASS |
| G1 / Draft, persisted reload/edit/Post, balance/card and duplicate Post | `Packaged_receipt_draft_reload_edit_post_and_stock_reconcile_with_readonly_sql` | 2/2 PASS | 2/2 PASS |
| G1 / concurrent stale write and input preservation | `Two_actual_clients_reject_stale_receipt_save_and_preserve_losing_input` | 2/2 PASS | 2/2 PASS |
| Security / disabled Login and server-side direct Deny after enabled UI | `Disabled_login_and_server_direct_deny_reject_a_previously_enabled_write_without_mutation` | 2/2 PASS | 2/2 PASS |
| K1 / same-process changed lookup, inactive history/new choice and restart | `Same_process_lookup_messages_preserve_history_reject_inactive_new_choices_and_reopen_fresh` | 2/2 PASS | 2/2 PASS across full Dark + focused Light; full Light failure retained |
| G1 / real SQL command timeout, uncertain Post, explicit reload and checked retry | `Real_sql_post_timeout_locks_mutation_until_explicit_reload_and_checked_retry` | 2/2 PASS | 2/2 PASS |
| G1 / invalid fields and no inventory mutation | `Invalid_receipt_fields_preserve_input_and_create_no_inventory` | 2/2 PASS | 2/2 PASS |
| SQL identity/schema/permissions/ownership | `Disposable_gui_topology_separates_provisioning_migration_runtime_and_readonly_oracle` | 1/1 PASS (no theme) | 1/1 PASS (no theme) |
| L1 / unavailable SQL safe startup and process drain | `Unavailable_sql_startup_is_safe_owned_and_drains_under_native_uia` | 1/1 PASS (default Login theme) | 1/1 PASS (default Login theme) |

The19 added Desktop methods contribute30 invocations:10 packaged methods/18 rows, plus these9 methods/12 control/infrastructure rows:

| Added method | Rows |
|---|---:|
| `CatalogAccessibilityAcceptanceTests.Accessible_open_row_uses_the_existing_selection_path_and_rejects_stale_rows` | 4 |
| `GoodsReceiptControlTests.Explicit_new_discards_uncommitted_lookup_text_in_an_unsaved_empty_draft` | 1 |
| `InventoryAccessibilityAcceptanceTests.Receipt_and_line_accessible_actions_use_existing_selection_and_readonly_columns` | 1 |
| `InventoryAccessibilityAcceptanceTests.Numeric_accessibility_updates_line_with_validation_and_disabled_guards` | 1 |
| `InventoryAccessibilityAcceptanceTests.Lookup_accessibility_distinguishes_committed_selection_from_pending_text` | 1 |
| `GuiOwnedProcessTests.Normal_tree_exit_allows_a_short_bounded_natural_drain` | 1 |
| `GuiOwnedProcessTests.Infrastructure_probe_does_not_inherit_SQL_credentials` | 1 |
| `GuiOwnedProcessTests.Poisoned_gui_session_blocks_SQL_allocation_before_configuration_or_database_work` | 1 |
| `InventoryAccessibilityAcceptanceTests.Lookup_F4_keeps_results_open_when_typing_has_already_opened_them` | 1 |

Unfiltered100% final Desktop source/discovery/TRX reconcile **227 methods /312 invocations /312 PASS /0 failed/skipped**, including all18 packaged rows and all282 prior T02 display names. Actual effective monitorDPI96/scale100/windowDPI96 and32-bit testhost are recorded in the TRX. Session108/108, Architecture12/12 and the six sequential SQL projects306/306 passed. The17 GUI allocations from this100% run drained/dropped with exact ownership; read-only follow-up found zero captured databases/principals and zero owned GUI processes remaining. Raw evidence remains ignored, with no credentials/raw SQL IDs/screenshots committed.

**T03 COMPLETE in task scope.** Final125% full execution was311 PASS/1 FAIL/0 skipped; the exact failed Light lookup row passed1/1 on the same frozen binaries after requesting an exclusive desktop. The owner confirmed window switching during the full run; popup closure on deactivation is consistent with that failure, without an event-level causal trace. The312 unique names reconcile exactly across configurations, with all312 ultimately passing automatically; this is not an unfiltered125%312/312 pass. Full125% plus focused rerun recorded63 observations of monitorDPI120/scale125/windowDPI96. The35 final GUI allocations all have complete ownership/principal snapshots and verified cleanup; all152 local GUI ledgers are dropped, with exact-name SQL census and owned-process census zero. Package/test hashes are unchanged. Failed trials remain separately recorded, and future runs require an exclusive foreground desktop. T04/T05 attended walkthroughs, T06 frozen acceptance and REL/P6 gates remain OPEN.