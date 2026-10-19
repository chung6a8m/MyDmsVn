# GUI-P5 coverage matrix and T00 baseline

**Task:** GUI-P5-T00, [acceptance plan](plans/20261019-001-full-winforms-gui-p5-acceptance.md). **Recorded:** 2026-10-19 (Asia/Bangkok), Codex; operator sign-off pending. **Source:** `f275183c8b39dc2b712dc37be7f0f4ac71396357` on `v1a`, isolated implementation branch `codex/gui-p5-t00`. Documentation changes do not change the executable candidate.

**Status: inventory published; topology approval OPEN / packaged acceptance BLOCKED.** The source inventory and focused baseline below are evidence only for their stated scope. T01–T06, both attended P5 IDs, REL G1–G5 and production readiness remain OPEN. No package was built or installed in T00; no package hash or real-login result is available.

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
| SQL | Test connection setting present in process and User scope; value not printed or used | No connection, SQL version/edition, topology/principal validation or SQL test execution |
| Display/package | Theme/DPI and x86 packaged/testhost matrix NOT TESTED | Focused rendering tests do not establish a DPI matrix; x64 runner banner does not prove x86 execution |
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

The existing automated column identifies concrete evidence anchors, not exhaustive acceptance of every behavior in its row. Every gap has an executable project/task owner. T06 reconciles the complete final inventory and TRX again.

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
| F1 Errors/async after close/disposal/ThreadException/DataError/fatal hang | D `WinFormsTestInfrastructureTests` (all eight Appendix A methods); D `ProductionDiagnosticsTests.Redactor_removes_credentials_and_nested_sql_exception_details`; D `CatalogControlTests.Disposing_control_disposes_view_model_and_prevents_delayed_work`; D `GoodsReceiptEditorViewModelTests.Dispose_ignores_a_late_detail_refresh_response` | T01: regression for close/async/reporting + bounded noncooperative/fatal owned subprocess, kill/drain descendants, stop after cleanup failure. `StaTest` cancels then waits 5s; it cannot kill hung STA | T03 startup/SQL failure, unexpected modal + bounded teardown; T04/T05 safe error UX | No DB reset until all workers/connections drain; timeout/uncertain ownership quarantines allocation |
| F2 Packaging/composition/schema/x86 | D `ReleasePackagingTests.Manifest_only_mode_hashes_active_desktop_and_separate_admin_package`, `Packages_without_runtime_schema_policy_are_rejected`, two rows of `Desktop_packages_reject_admin_tools_and_embedded_secrets`; D `DesktopCompositionTests.WinForms_composition_resolves_goods_receipt_factory_and_shell`; D `DesktopHostRunnerTests.Smoke_mode_creates_and_renders_login_without_bypassing_authentication` | T01: fix unbounded PowerShell child wait/drain in `ReleasePackagingTests`; T03: actual published x86 Login-first host; manifest fixtures/smoke form creation do not establish real authentication | T03 frozen clean publish + manifest/hash; REL owns clean install/recovery and support matrix | Migrations/Security owners supply exact schema001–009 and runtime principal preflight; no prefix auto-upgrade |
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

**Approval status: OPEN.** The configured SQL test setting's presence does not identify an approved packaged E2E topology, grant permission to reuse an application DB or certify runtime least privilege. T00 requested the owner/operator's Windows/DPI, non-production SQL instance, runtime principal and approver selection. Until recorded, no SQL allocation/provisioning or real package launch is authorized by this document. This is the remaining T00 exit prerequisite; no approved topology is invented. [T07 resource evidence](L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke) records a scenario-specific temporary-login exception consumed by cleanup; it is not standing authorization to create another server principal for GUI-P5.

| Proposal for owner review | Required evidence / current status |
|---|---|
| Interactive Windows 11 x64 image, .NET Framework 4.8, published desktop x86; net48 x86 test/probe configuration | Candidate only; observed developer build above is not a supported release image. REL owns final OS support decisions |
| 96/144/192 DPI (100/150/200%), light/dark, keyboard navigation; one GUI process/suite at a time | Proposed GUI matrix; all combinations NOT TESTED as an acceptance matrix |
| One designated non-production SQL Server instance, effective test connection `master`; one fresh positively owned GUI E2E DB per scenario group | Exact instance/edition/version/SQL auth/TLS policy and approving operator pending. No LocalDB/default instance substitution; no production/backup/recovery DB |
| Separate setup/maintenance, least-privilege runtime and independent read-only oracle identities | Actual principal mapping/ACL/effective grants pending; runtime must never inherit test create/drop or maintenance rights |
| Production schemas/scripts001–009, exact current journal + protected superuser binding, deliberately seeded ordinary accounts | Setup/admin migrator first, then provisioning and bootstrap; no bootstrap/DbUp in desktop; test-specific ownership metadata retained separately from release schema assertions |

Use [configuration/security runbook](CONFIGURATION_AND_SQL_SECURITY.md), [security administration runbook](SECURITY_ADMINISTRATION_RUNBOOK.md), [schema runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) and [principal provisioning script](../deployment/sql/Provision-MyDmsVnPrincipals.sql). The runtime uses reviewed object/column grants and only the three named security procedures, no fixed broad roles, DDL, journal/binding mutation or blanket EXECUTE. Audit effective grants separately: a successful current startup validator does not check every extra EXECUTE grant. An independent oracle reads business/security assertions under a separately reviewed identity; do not broaden desktop permissions to obtain test evidence.

Credentials stay outside source/package/TRX/screenshots. Prefer a deliberately assigned Windows identity. If SQL Authentication is selected, use operator-managed ACL-protected runtime/migration files separately; the existing loader does not implement DPAPI. Never pass passwords on command lines or print environment values. Do not overwrite the workstation's shared `%ProgramData%` runtime configuration for a test; T03 must use a scoped child environment/explicit protected file for its owned allocation. Preserve the caller's prior environment. Bind `Security__SuperuserUsername` deliberately and provide passwords through protected input only.

Required T01/T03 resource lifecycle:

1. Record designated instance and generated allocation identity (`database_id`, creation time, random `MyDmsVn.TestOwnership` marker) using the existing owned SQL helper boundary; never identify ownership by name prefix alone. Keep sensitive values local, publish sanitized counts/status.
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
| Reviewed matrix | Independent Codex review 2026-10-19: no important findings; 138 source anchors, named methods and 10/10 TRX checked. Owner acceptance pending |
| Approved executable topology/limited identities | OPEN; owner/operator must select actual resources, credential delivery and approver |
| T01 | Bound packaging child wait and fatal/hang cleanup before unattended package tests |
| T02 | Fill named control/VM gaps; retain independent backend semantics |
| T03 | Validate native UIA selectors/patterns/x86, actual Login and SQL oracles on approved topology |
| T04/T05/T06 | Attended P5 + frozen unfiltered GUI/Session and cleanup evidence, all OPEN |

The plan's T00 topology checkbox stays open until an approved actual topology is recorded; this matrix is reviewable preparatory work, not a complete T00/P5/release sign-off.

## Appendix A. Complete Desktop test method inventory

Inventory below is derived from checked-in `[Fact]`/`[Theory]` declarations, reconciled with actual VSTest discovery. All are in namespace `MyDmsVn.Desktop.Tests`. `Fact` = one invocation; only `ReleasePackagingTests.Desktop_packages_reject_admin_tools_and_embedded_secrets` is a Theory (two rows). Its row labels below identify the input class without reproducing the dummy connection string. Helpers without test attributes are excluded. A method marked `PASS` was selected in the focused T00 TRX; `NOT RUN` means unselected, not skipped.

### CatalogControlTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Catalog_control_renders_active_state_and_keeps_compact_keyboard_layout`](../tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L18) | Fact / 1 | NOT RUN |
| [`Catalog_control_pages_beyond_the_first_twenty_five_rows`](../tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L52) | Fact / 1 | NOT RUN |
| [`Canceled_activation_does_not_start_a_catalog_query`](../tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L84) | Fact / 1 | NOT RUN |
| [`Save_button_forwards_editor_values_and_shows_field_errors_without_modal_dialogs`](../tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L105) | Fact / 1 | NOT RUN |
| [`Editor_fields_are_disabled_while_a_save_is_in_flight`](../tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L144) | Fact / 1 | NOT RUN |
| [`Editor_fields_are_disabled_while_a_row_selection_is_in_flight`](../tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L175) | Fact / 1 | NOT RUN |
| [`Enter_selects_the_active_catalog_row`](../tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L212) | Fact / 1 | NOT RUN |
| [`All_catalog_list_cells_are_read_only`](../tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L239) | Fact / 1 | NOT RUN |
| [`Enter_uses_the_immutable_row_id_instead_of_the_visible_cell_value`](../tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L271) | Fact / 1 | NOT RUN |
| [`Enter_does_not_select_a_row_while_the_active_cell_is_editing`](../tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L300) | Fact / 1 | NOT RUN |
| [`Background_view_model_updates_are_marshaled_to_the_control_thread`](../tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L336) | Fact / 1 | NOT RUN |
| [`Disposing_control_disposes_view_model_and_prevents_delayed_work`](../tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L362) | Fact / 1 | NOT RUN |
| [`Shell_reuses_catalog_tab_and_disposes_catalog_controls_on_sign_out`](../tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L380) | Fact / 1 | NOT RUN |
| [`Reopening_existing_catalog_tab_refreshes_authoritative_data_without_new_control`](../tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs#L412) | Fact / 1 | NOT RUN |

### DesktopCompositionTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Local_desktop_services_use_one_shared_weak_reference_messenger`](../tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L26) | Fact / 1 | NOT RUN |
| [`Local_desktop_services_resolve_api_without_database_configuration`](../tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L43) | Fact / 1 | NOT RUN |
| [`Local_api_returns_validation_failure_as_api_response`](../tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L65) | Fact / 1 | NOT RUN |
| [`Local_api_honors_pre_canceled_request`](../tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L91) | Fact / 1 | NOT RUN |
| [`Local_api_maps_handler_exceptions_and_reports_diagnostics`](../tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L115) | Fact / 1 | NOT RUN |
| [`Local_catalog_clients_are_registered_and_map_application_errors_to_api_responses`](../tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L144) | Fact / 1 | NOT RUN |
| [`Local_catalog_validation_fields_are_relative_to_public_request_dtos`](../tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L169) | Fact / 1 | NOT RUN |
| [`WinForms_composition_resolves_goods_receipt_factory_and_shell`](../tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L205) | Fact / 1 | NOT RUN |
| [`WinForms_composition_resolves_inventory_factory_and_controls`](../tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L229) | Fact / 1 | NOT RUN |
| [`In_flight_catalog_mutation_keeps_authorized_actor_after_local_sign_out`](../tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L254) | Fact / 1 | NOT RUN |
| [`Stale_login_error_keeps_request_correlation_and_cleans_up_scope`](../tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs#L299) | Fact / 1 | NOT RUN |

### DesktopHostRunnerTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Smoke_mode_creates_and_renders_login_without_bypassing_authentication`](../tests/MyDmsVn.Desktop.Tests/DesktopHostRunnerTests.cs#L11) | Fact / 1 | NOT RUN |
| [`Production_smoke_resolves_login_inside_a_disposed_scope`](../tests/MyDmsVn.Desktop.Tests/DesktopHostRunnerTests.cs#L50) | Fact / 1 | NOT RUN |

### FoundationShellFormTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Shell_exposes_navigation_workspace_and_session_status`](../tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L16) | Fact / 1 | NOT RUN |
| [`OpenWorkspace_reuses_existing_document_for_the_same_key`](../tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L34) | Fact / 1 | NOT RUN |
| [`Inventory_navigation_opens_and_reuses_balance_and_stock_card_workspaces`](../tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L64) | Fact / 1 | NOT RUN |
| [`ToggleTheme_switches_between_light_and_dark`](../tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L108) | Fact / 1 | NOT RUN |
| [`Shell_reflects_shared_session_and_notification_changes`](../tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L136) | Fact / 1 | NOT RUN |
| [`Sign_out_and_session_expiry_clear_the_session_with_explicit_status`](../tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L163) | Fact / 1 | NOT RUN |
| [`Changing_authenticated_user_closes_the_previous_users_workspace`](../tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L216) | Fact / 1 | NOT RUN |
| [`OpenWorkspace_rejects_content_after_sign_out`](../tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L246) | Fact / 1 | NOT RUN |
| [`OpenWorkspace_rejects_a_stale_request_after_another_user_signs_in`](../tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L277) | Fact / 1 | NOT RUN |
| [`Shell_binding_tracks_async_command_busy_and_ready_state`](../tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L309) | Fact / 1 | NOT RUN |
| [`Session_and_notification_updates_are_marshaled_to_the_shell_thread`](../tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L335) | Fact / 1 | NOT RUN |
| [`Session_change_before_handle_creation_is_reconciled_when_handle_is_created`](../tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L384) | Fact / 1 | NOT RUN |
| [`Ready_view_model_state_before_handle_creation_is_reconciled`](../tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L420) | Fact / 1 | NOT RUN |
| [`Busy_view_model_state_before_handle_creation_is_reconciled`](../tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L448) | Fact / 1 | NOT RUN |
| [`Notification_before_handle_creation_is_reconciled`](../tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L481) | Fact / 1 | NOT RUN |
| [`Goods_receipt_navigation_reuses_one_workspace_and_sign_out_disposes_it`](../tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs#L510) | Fact / 1 | NOT RUN |

### GoodsReceiptControlTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Activation_and_selection_render_master_detail_lookups_total_and_read_only_state`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L17) | Fact / 1 | NOT RUN |
| [`Draft_post_button_posts_current_version_and_locks_the_editor`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L51) | Fact / 1 | NOT RUN |
| [`Draft_commands_update_lines_and_validation_errors_stay_inline`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L85) | Fact / 1 | NOT RUN |
| [`Draft_decimal_values_round_trip_at_the_contract_precision_and_range`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L115) | Fact / 1 | NOT RUN |
| [`In_flight_activation_disables_commands_until_the_list_finishes`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L150) | Fact / 1 | NOT RUN |
| [`Operation_errors_render_in_the_non_modal_status_area`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L176) | Fact / 1 | NOT RUN |
| [`Paging_and_explicit_filters_refresh_immediately`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L198) | Fact / 1 | NOT RUN |
| [`Lookup_text_searches_the_server_side_catalog_sources`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L229) | Fact / 1 | NOT RUN |
| [`Receipt_line_keeps_a_historical_product_selected_outside_the_active_page`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L257) | Fact / 1 | NOT RUN |
| [`Failed_selection_does_not_reload_the_previous_receipt_into_a_new_editor`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L282) | Fact / 1 | NOT RUN |
| [`Dirty_editor_blocks_selection_new_and_refresh_without_losing_input`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L309) | Fact / 1 | NOT RUN |
| [`Forbidden_refresh_clears_visible_receipt_data`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L341) | Fact / 1 | NOT RUN |
| [`Editing_a_line_after_save_updates_the_reloaded_line`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L370) | Fact / 1 | NOT RUN |
| [`Canceled_post_save_refresh_still_refreshes_list_and_reports_saved`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L395) | Fact / 1 | NOT RUN |
| [`Editing_a_line_after_refresh_updates_the_reloaded_line`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L437) | Fact / 1 | NOT RUN |
| [`Detail_inputs_stay_disabled_during_the_entire_refresh`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L462) | Fact / 1 | NOT RUN |
| [`Reactivation_after_access_revocation_clears_visible_receipt_data`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L493) | Fact / 1 | NOT RUN |
| [`Write_only_create_clears_sensitive_detail_and_locks_save_after_commit`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L520) | Fact / 1 | NOT RUN |
| [`Repeated_activation_refreshes_and_disposal_owns_both_view_models`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L564) | Fact / 1 | NOT RUN |
| [`Open_catalog_controls_refresh_receipt_lookups_without_losing_selected_references`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs#L585) | Fact / 1 | NOT RUN |

### GoodsReceiptEditorViewModelTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Create_sends_current_lines_then_refreshes_the_persisted_aggregate`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L17) | Fact / 1 | NOT RUN |
| [`Post_uses_persisted_identity_and_version_then_refreshes_the_posted_receipt`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L47) | Fact / 1 | NOT RUN |
| [`Update_reuses_opaque_version_and_conflict_preserves_user_input`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L78) | Fact / 1 | NOT RUN |
| [`Concurrent_save_requests_issue_only_one_create_command`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L103) | Fact / 1 | NOT RUN |
| [`Successful_create_keeps_identity_when_follow_up_refresh_fails`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L121) | Fact / 1 | NOT RUN |
| [`Immediate_validation_blocks_missing_fields_and_posted_receipts_are_read_only`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L149) | Fact / 1 | NOT RUN |
| [`Dirty_state_tracks_user_edits_and_resets_after_load_or_save`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L168) | Fact / 1 | NOT RUN |
| [`Lookup_metadata_changes_do_not_dirty_a_posted_receipt`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L190) | Fact / 1 | NOT RUN |
| [`Throwing_create_is_reported_inline_without_faulting_save`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L203) | Fact / 1 | NOT RUN |
| [`Throwing_update_is_reported_inline_without_faulting_save`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L224) | Fact / 1 | NOT RUN |
| [`Throwing_follow_up_get_preserves_successful_create_identity`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L246) | Fact / 1 | NOT RUN |
| [`Throwing_detail_refresh_is_reported_inline_without_replacing_the_editor`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L269) | Fact / 1 | NOT RUN |
| [`Delayed_refresh_applies_collection_mutations_on_the_owning_sta_thread`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L290) | Fact / 1 | NOT RUN |
| [`Cancellation_after_create_response_reports_saved_without_refreshing_details`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L330) | Fact / 1 | NOT RUN |
| [`Boundary_line_amount_overflow_is_safe_and_blocks_save`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L364) | Fact / 1 | NOT RUN |
| [`Aggregate_overflow_across_safe_lines_is_safe_and_blocks_save`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L383) | Fact / 1 | NOT RUN |
| [`Dispose_ignores_a_late_detail_refresh_response`](../tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs#L407) | Fact / 1 | NOT RUN |

### InventoryControlTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Balance_control_shows_empty_state_after_first_successful_zero_row_query`](../tests/MyDmsVn.Desktop.Tests/InventoryControlTests.cs#L15) | Fact / 1 | NOT RUN |
| [`Stock_card_control_shows_empty_state_after_first_successful_zero_row_query`](../tests/MyDmsVn.Desktop.Tests/InventoryControlTests.cs#L39) | Fact / 1 | NOT RUN |
| [`Balance_control_renders_history_totals_and_runs_explicit_filters_immediately`](../tests/MyDmsVn.Desktop.Tests/InventoryControlTests.cs#L64) | Fact / 1 | NOT RUN |
| [`Stock_card_control_filters_without_debounce_and_reports_errors_without_dialogs`](../tests/MyDmsVn.Desktop.Tests/InventoryControlTests.cs#L104) | Fact / 1 | NOT RUN |
| [`Stock_card_control_applies_dates_chosen_before_ids_in_first_query`](../tests/MyDmsVn.Desktop.Tests/InventoryControlTests.cs#L148) | Fact / 1 | NOT RUN |

### LocalIdentityApiClientTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Successful_login_updates_the_shared_local_session_and_current_user_query`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L23) | Fact / 1 | NOT RUN |
| [`Signing_out_clears_the_server_current_user_for_subsequent_requests`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L44) | Fact / 1 | NOT RUN |
| [`Login_while_authenticated_is_rejected_without_replacing_the_current_user`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L64) | Fact / 1 | NOT RUN |
| [`Login_result_does_not_create_a_session_when_sql_account_validation_rejects_it`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L87) | Fact / 1 | NOT RUN |
| [`Disabled_account_revalidation_clears_the_desktop_session`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L112) | Fact / 1 | NOT RUN |
| [`Sql_validation_failure_is_not_reported_as_bad_credentials`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L139) | Fact / 1 | NOT RUN |
| [`Sql_failure_during_credential_lookup_is_reported_as_database_unavailable`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L160) | Fact / 1 | NOT RUN |
| [`Late_account_validation_from_user_a_cannot_expire_user_b_session`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L182) | Fact / 1 | NOT RUN |
| [`Late_current_user_validation_from_user_a_cannot_sign_out_user_b`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L219) | Fact / 1 | NOT RUN |
| [`Request_waiting_for_account_validation_is_rejected_after_session_switch`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L255) | Fact / 1 | NOT RUN |
| [`Late_validation_from_old_login_attempt_cannot_clear_new_session`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L296) | Fact / 1 | NOT RUN |
| [`Signing_out_invalidates_a_login_that_is_still_in_flight`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L329) | Fact / 1 | NOT RUN |
| [`Concurrent_login_attempts_allow_only_the_latest_attempt_to_publish_session`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L360) | Fact / 1 | NOT RUN |
| [`Parallel_local_requests_keep_the_actor_captured_for_each_session`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L398) | Fact / 1 | NOT RUN |
| [`Canceled_login_cannot_establish_a_session_when_the_handler_ignores_cancellation`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L440) | Fact / 1 | NOT RUN |
| [`Canceled_login_cannot_establish_a_session_when_account_validation_ignores_cancellation`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L469) | Fact / 1 | NOT RUN |
| [`Canceled_login_cleans_up_when_account_validation_observes_cancellation`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L500) | Fact / 1 | NOT RUN |
| [`Explicit_logout_then_pending_login_invalidates_the_old_principals_desktop_requests`](../tests/MyDmsVn.Desktop.Tests/LocalIdentityApiClientTests.cs#L530) | Fact / 1 | NOT RUN |

### LoginFormTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Login_form_is_compact_keyboard_accessible_and_masks_password`](../tests/MyDmsVn.Desktop.Tests/LoginFormTests.cs#L18) | Fact / 1 | PASS |
| [`Login_form_renders_after_theme_change_and_disposes_without_ui_exceptions`](../tests/MyDmsVn.Desktop.Tests/LoginFormTests.cs#L49) | Fact / 1 | PASS |

### MasterDetailPrototypeControlTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`SetRows_projects_master_rows_into_the_source_grid`](../tests/MyDmsVn.Desktop.Tests/MasterDetailPrototypeControlTests.cs#L12) | Fact / 1 | NOT RUN |
| [`Lookup_and_field_errors_follow_reusable_contracts`](../tests/MyDmsVn.Desktop.Tests/MasterDetailPrototypeControlTests.cs#L34) | Fact / 1 | NOT RUN |
| [`Editor_panel_keeps_a_usable_width_after_the_control_is_resized`](../tests/MyDmsVn.Desktop.Tests/MasterDetailPrototypeControlTests.cs#L65) | Fact / 1 | NOT RUN |

### PermissionActionBinderTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Action_stays_disabled_until_permission_is_granted`](../tests/MyDmsVn.Desktop.Tests/PermissionActionBinderTests.cs#L14) | Fact / 1 | NOT RUN |
| [`Action_is_disabled_when_permission_is_denied_or_cannot_be_verified`](../tests/MyDmsVn.Desktop.Tests/PermissionActionBinderTests.cs#L40) | Fact / 1 | NOT RUN |
| [`Delayed_permission_result_cannot_reenable_an_action_after_sign_out`](../tests/MyDmsVn.Desktop.Tests/PermissionActionBinderTests.cs#L75) | Fact / 1 | NOT RUN |
| [`Queued_permission_grant_cannot_reenable_an_action_after_sign_out`](../tests/MyDmsVn.Desktop.Tests/PermissionActionBinderTests.cs#L102) | Fact / 1 | NOT RUN |
| [`Queued_permission_grant_cannot_reenable_an_action_after_binder_disposal`](../tests/MyDmsVn.Desktop.Tests/PermissionActionBinderTests.cs#L130) | Fact / 1 | NOT RUN |
| [`Permission_update_without_a_control_handle_is_marshaled_to_the_ui_thread`](../tests/MyDmsVn.Desktop.Tests/PermissionActionBinderTests.cs#L165) | Fact / 1 | NOT RUN |
| [`Previous_sessions_permission_result_cannot_override_a_new_grant`](../tests/MyDmsVn.Desktop.Tests/PermissionActionBinderTests.cs#L205) | Fact / 1 | NOT RUN |
| [`Older_permission_result_cannot_override_the_latest_same_session_result`](../tests/MyDmsVn.Desktop.Tests/PermissionActionBinderTests.cs#L239) | Fact / 1 | NOT RUN |

### ProductionDiagnosticsTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Production_registration_writes_structured_audit_with_request_correlation`](../tests/MyDmsVn.Desktop.Tests/ProductionDiagnosticsTests.cs#L19) | Fact / 1 | NOT RUN |
| [`Redactor_removes_credentials_and_nested_sql_exception_details`](../tests/MyDmsVn.Desktop.Tests/ProductionDiagnosticsTests.cs#L65) | Fact / 1 | NOT RUN |
| [`Unwritable_log_location_uses_non_modal_fallback_sink`](../tests/MyDmsVn.Desktop.Tests/ProductionDiagnosticsTests.cs#L87) | Fact / 1 | NOT RUN |
| [`Runtime_audit_file_failure_degrades_health_and_reports_committed_state`](../tests/MyDmsVn.Desktop.Tests/ProductionDiagnosticsTests.cs#L122) | Fact / 1 | NOT RUN |
| [`Retention_cleanup_failure_does_not_disable_subsequent_audit_writes`](../tests/MyDmsVn.Desktop.Tests/ProductionDiagnosticsTests.cs#L167) | Fact / 1 | NOT RUN |
| [`Sharing_violation_reports_failure_and_recovers_on_the_next_audit_write`](../tests/MyDmsVn.Desktop.Tests/ProductionDiagnosticsTests.cs#L210) | Fact / 1 | NOT RUN |
| [`Desktop_exception_reporter_writes_redacted_diagnostic_with_support_id`](../tests/MyDmsVn.Desktop.Tests/ProductionDiagnosticsTests.cs#L264) | Fact / 1 | NOT RUN |

### ReleasePackagingTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Manifest_only_mode_hashes_active_desktop_and_separate_admin_package`](../tests/MyDmsVn.Desktop.Tests/ReleasePackagingTests.cs#L14) | Fact / 1 | NOT RUN |
| [`Packages_without_runtime_schema_policy_are_rejected`](../tests/MyDmsVn.Desktop.Tests/ReleasePackagingTests.cs#L49) | Fact / 1 | NOT RUN |
| [`Desktop_packages_reject_admin_tools_and_embedded_secrets`](../tests/MyDmsVn.Desktop.Tests/ReleasePackagingTests.cs#L67) | Theory / 2 | NOT RUN |

### SecurityAdministrationFormTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Administration_control_loads_and_disposes_on_serialized_sta_without_modal_errors`](../tests/MyDmsVn.Desktop.Tests/SecurityAdministrationFormTests.cs#L19) | Fact / 1 | NOT RUN |
| [`Security_workspace_is_session_scoped_and_closes_on_sign_out`](../tests/MyDmsVn.Desktop.Tests/SecurityAdministrationFormTests.cs#L60) | Fact / 1 | NOT RUN |

### UiDependencyCompatibilityTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`Bootstrap_source_grid_tracks_runtime_theme_changes`](../tests/MyDmsVn.Desktop.Tests/UiDependencyCompatibilityTests.cs#L11) | Fact / 1 | NOT RUN |
| [`Bootstrap_source_grid_constructs_and_renders_without_host_startup_state`](../tests/MyDmsVn.Desktop.Tests/UiDependencyCompatibilityTests.cs#L39) | Fact / 1 | NOT RUN |

### WinFormsTestInfrastructureTests

| Method / source | Type / rows | T00 outcome |
|---|---|---|
| [`StaTest_runs_action_on_sta_thread`](../tests/MyDmsVn.Desktop.Tests/WinFormsTestInfrastructureTests.cs#L12) | Fact / 1 | PASS |
| [`StaTest_cancels_timed_out_action_and_waits_for_cleanup`](../tests/MyDmsVn.Desktop.Tests/WinFormsTestInfrastructureTests.cs#L24) | Fact / 1 | PASS |
| [`StaTest_rethrows_action_failure_on_test_thread`](../tests/MyDmsVn.Desktop.Tests/WinFormsTestInfrastructureTests.cs#L47) | Fact / 1 | PASS |
| [`WinFormsTestGuard_captures_thread_exception`](../tests/MyDmsVn.Desktop.Tests/WinFormsTestInfrastructureTests.cs#L58) | Fact / 1 | PASS |
| [`WinFormsTestGuard_fails_when_captured_exception_is_not_consumed`](../tests/MyDmsVn.Desktop.Tests/WinFormsTestInfrastructureTests.cs#L77) | Fact / 1 | PASS |
| [`WinFormsTestGuard_suppresses_error_dialogs_on_current_sta_thread`](../tests/MyDmsVn.Desktop.Tests/WinFormsTestInfrastructureTests.cs#L96) | Fact / 1 | PASS |
| [`WinFormsTestGuard_disposal_does_not_disable_another_sta_thread_guard`](../tests/MyDmsVn.Desktop.Tests/WinFormsTestInfrastructureTests.cs#L110) | Fact / 1 | PASS |
| [`WinFormsTestGuard_captures_grid_data_error_without_throwing`](../tests/MyDmsVn.Desktop.Tests/WinFormsTestInfrastructureTests.cs#L150) | Fact / 1 | PASS |

Theory row reconciliation: admin executable payload (1) and embedded-configuration payload (1), both discovered / NOT RUN. Total: **138 methods / 139 invocations**, matching VSTest discovery.
