# GUI-P5-T05 — Attended Inventory walkthrough

**Status: IN PROGRESS / NOT RUN. REMAIN-P5-GUI-02 OPEN.** Prepared2026-10-19, Asia/Bangkok, Codex; task base cdfa4b7. T04 accepted at cdfa4b7 is preserved. This worksheet records actual attended observations; existing automated results are separate.

## Candidate and admission

Reuse frozen product/test candidate d5207b2ce1eae09e9317625b3d7d486c994f09fe, package1.0.0-guip5-t04-c2, only after installed133-file manifest verification. No product/test change is currently proposed. Exact candidate hashes remain in the [T04 worksheet](GUI_P5_T04_WALKTHROUGH.md#candidate2-freeze--automatedattended-acceptance-pending); recheck before launch rather than inferring unchanged bytes. The shared task branch is codex/gui-p5-t00 in its existing isolated worktree; source/test changes, if required, need focused red/green verification and a new explicit candidate.

Approved topology: Windows11 interactive x64, net48/x86,1920x1080,100/125%, Light/Dark; existing designated private non-production SQL instance, fresh positively owned GUI-P5 database/current001–009, separate provisioning/migration/runtime/oracle identities. Prior actual build26200.9457, runtime4.8.09221/Release533509 and SQL2017Developer14.0.2130.4 are historical metadata until freshly verified. SQL2022 is only the target for new provisioning, not the observed version. Runtime must remain limited MyDmsVnRuntimeRole/no db_owner/DDL. No recovery-drill database sharing.

**Verified preflight (2026-10-19):** Owner confirmed baseline-plus-UX scope and desktop100%/1920x1080. Independent x86 native probe verified primary scale100/HRESULT0; CIM physical1920x1080. Fresh exact census:166/166prior GUI ledgers Dropped, latest database/principals/process0. Installed133/133Desktop files match manifest; manifestSHA256 EDB0FE41DAB59FE7F4DEE7F2673815017C056C744E70E9D2E896D50075146101, frozen testDLL DD0BBBFC176D60F778CC3C5BDC7BD433636E0DA8AFC2C68CB803D98752CA41F7. Masked-password handoff succeeded on retry; fresh SQL/actor admission verified below. Actual human Login/attended case results remain NOT RUN until confirmed.

## Execution rules

- Owner-approved matrix (2026-10-19, chat): full functional100%Light baseline plus supplemental Receipt/Balance/Card keyboard/errors/resize and three Receipt popup UX at100%Dark/125%Light/125%Dark. Functional repetitions outside baseline are N/A by that decision, not PASS. No150% obligation.
- Human chooses a temporary test-only password in two masked fields; bounded3-minute owned prompt/ACL named-pipe memory transfer. No password in source/file/chat/stdout/command line. Human performs actual Login; no test-session injection. Ordinary actors are not protected owner/superuser.
- Reuse existing GuiOwnedProcess.RunAttendedAsync: each app chapter max30minutes, normal close before deadline, verified tree/pipe drain. Setup command admission55minutes; start another positively owned cohort if needed, with provenance recorded. No budget expansion or unsupervised executable launch.
- Explicit test actors: operator (all needed Catalog/Inventory keys), post-only with Read but no Write, writer without Post, ordinary Post role grant with a controlled direct Deny, Post without Read, restricted receipt reader without Stock rights, disabled actor, and actor disabled after real Login. Oracle records actual Active/role/direct assignments/protected binding before interpreting outcomes.
- Baseline creates fresh synthetic T05 Product/Warehouse/Employee through GUI; setup-only actors and later conflict/page fixtures never count as GUI-create evidence. Use separate receipt notes for normal Post, permissions, conflict and fault cases. Initial baseline quantities2.5000@4 then edit3.1250@4 (12.50); exact values recorded at execution.
- Rowversion conflicts use a second real application client when practical; otherwise a clearly identified owned migration-side winner is recorded as fixture manipulation, never a second GUI client. Snapshot accepted winner and stale loser independently. Rejected Save/Post must not replace the winner or add stock.
- Timeout/uncertain Post uses the existing owned45-second WAITFOR trigger technique, exact ownership/object-definition check, finite SQL timeout and drained runtime request. Preserve safe error/input, verify mutation lock, independently read persisted status/ledger/balance, remove only the exact owned trigger, explicitly reload, and only then permit one justified retry. Never equate timeout with rollback or blindly retry. Record whether actual outcome committed or rolled back.
- Independent restricted read-only oracle compares receipt/line state, posted actor/time, rowversions and per-line stock document identity. Reconcile grouped ledger sum versus balances using a full outer join, including missing/extra keys. Raw identifiers/SQL connections/logs stay ignored; public screenshots must be inspected application-only synthetic data.
- On uncertainty stop, retain/quarantine and report. Before drop verify exact instance/database-ID/create-date/random marker, principal-ID/SID/create-date and drained GUI/workers/connections. Never prefix-drop. Preserve original failure with any cleanup failure.

## Functional baseline ledger

PASS requires the named actual observation plus any independent oracle and Owner confirmation. NOT RUN does not mean defect. Source anchors: GoodsReceiptControl, GoodsReceiptEditorViewModel, StockBalanceControl, StockCardControl, InventoryPermissionDefinitions and the production Inventory handlers; tests PackagedGuiAcceptanceTests, ReceiptAcceptanceTests and InventoryAcceptanceTests remain independent automated evidence.

| Case | Action / required evidence |100% Light|
|---|---|---|
| A-01 | Actual ordinary Login, masked password, correct actor/session. | NOT RUN |
| A-02 | GUI create/read fresh T05 Product/Warehouse/Employee; independent fields/active identity. | NOT RUN |
| R-01 | New Draft, three lookups, quantity/cost/note; Save and independently verify aggregate. | NOT RUN |
| R-02 | Reload, edit quantity/note, Save/reload; same receipt ID, new opaque version, correct total. | NOT RUN |
| R-03 | Required/empty-lines/zero-quantity and available numeric boundaries; safe errors, retained input, no unintended persisted change. Do not require the UI to accept a numeric value its control rejects. | NOT RUN |
| R-04 | Dirty unsaved editor blocks navigation/New/Refresh as shipped, preserving input; explicit discard via normal close/restart when needed. | NOT RUN |
| R-05 | Post saved current Draft; Posted actor/time/status, one ledger movement per line and balance atomically match oracle. | NOT RUN |
| R-06 | Posted editor immutable; duplicate Post blocked/rejected with no extra ledger/balance increment. Distinguish disabled button from a server request. | NOT RUN |
| S-01 | Balance filters and visible totals agree with independent stock sum; no mixed-product grand-quantity claim. | NOT RUN |
| S-02 | Card warehouse/product/date filters, order/document identity/visible delta agree with oracle. | NOT RUN |
| S-03 | Empty/invalid filter, safe error and explicit Refresh recovery without stale rows/totals. | NOT RUN |
| S-04 | Actual multi-page Balance/Card data when declared, Next/Previous and filter change page1; fixture rows distinguished from attended posts. | NOT RUN |
| P-01 | Ordinary Read+Post without Write: read saved Draft, cannot edit/save, Post authorized with correct actor and single stock effect. | NOT RUN |
| P-02 | Read+Write without Post: Draft write succeeds, Post unavailable/denied; no posted mutation. | NOT RUN |
| P-03 | Post without Read cannot load/post an unseen receipt; no leaked detail or mutation. | NOT RUN |
| P-04 | Ordinary role Post grant plus direct Post Deny after enabled UI admission; server rejection/input preserved/no mutation, actual assignment snapshot. | NOT RUN |
| P-05 | Restricted Stock permissions: Balance/Card deny safely and clear rows/totals; no previous actor data. | NOT RUN |
| P-06 | Disabled user's actual Login denied safely; no admitted application session. | NOT RUN |
| P-07 | Actor disabled after actual Login: subsequent server operation fails closed/no stock or receipt mutation. | NOT RUN |
| C-01 | Stale Save rowversion loses to controlled accepted winner; safe conflict retains loser input and winner unchanged. | NOT RUN |
| C-02 | Stale Post version after winner Draft update is rejected; no ledger/balance and no overwrite. | NOT RUN |
| K-01 | Catalog rename/active change while Receipt/Stock views exist; fresh/historical names and active-only new choices match shipped contracts. | NOT RUN |
| L-01 | Real logout/other account/restart clears former actor data and reads current persisted Draft/Posted state. | NOT RUN |
| F-01 | Actual SQL Post timeout observed; safe uncertain outcome and mutation lock; independently establish persisted state after worker drain. | NOT RUN |
| F-02 | Exact fault removed, explicit read reload resolves status; only then justified single retry, one final stock effect; restart/schema preflight usable. | NOT RUN |
| O-01 | Independent full-key ledger/balance reconciliation, receipt-line document identity and no denied/failed mutation. | NOT RUN |
| O-02 | Sanitized screenshot/hash/actor/environment ledger; normal GUI drain and positively owned final DB/principal cleanup/exact census. | NOT RUN |

## Supplemental UX ledger (Owner-approved scope)

| Case |100% Dark|125% Light|125% Dark|
|---|---|---|---|
| UX-R | Receipt keyboard/focus/errors, windowed/maximized layout and Posted/busy command states. | NOT RUN | NOT RUN | NOT RUN |
| UX-B | Balance keyboard/filter/Refresh/error/totals/grid/paging bounds and resize. | NOT RUN | NOT RUN | NOT RUN |
| UX-S | Stock Card keyboard/date filters/Refresh/errors/totals/grid/paging bounds and resize. | NOT RUN | NOT RUN | NOT RUN |
| UX-P | Product popup F4/filter/arrows/Enter/reopen/Esc, focus/work-area bounds. | NOT RUN | NOT RUN | NOT RUN |
| UX-W | Warehouse popup F4/filter/arrows/Enter/reopen/Esc, focus/work-area bounds. | NOT RUN | NOT RUN | NOT RUN |
| UX-E | Employee popup F4/filter/arrows/Enter/reopen/Esc, focus/work-area bounds. | NOT RUN | NOT RUN | NOT RUN |

## Session / artifacts / decision

T05 selected/executed attended0/0; no automated invocation added. Package, SQL, scale, actor setup, actual observations, oracles, resources and timestamps will be recorded per cohort. Existing T03/T04 tests are not T05 attended passes. Final Owner sign-off PENDING; T05/REMAIN-P5-GUI-02 and T06/overallP5/P6 remain OPEN.

### Setup attempt1 — FAIL, safely disposed

At21:59 the first fresh T05 allocation reached actor seeding but SQL rejected a join between the default-collation table variable and production NormalizedUsername/NormalizedRoleName columns (SQL_Latin1_General_CP1_CI_AS versus Latin1_General_100_CI_AI). Desktop never launched; no attended case ran. Existing helper finally disposed the database/all3principals; exact census0 and167/167GUI ledgers Dropped. This is an attended setup-script defect, not an Inventory coverage/product failure.

Source002_Identity.sql explicitly declares both normalized identity columns Latin1_General_100_CI_AI. The ignored setup script now applies that exact collation at the two table-variable joins; parser verification PASS. A second fresh bounded masked-password setup is preparing; runtime SQL retry is not yet verified. The failure and its raw sanitized summary remain preserved; no PASS is inferred from the syntax check.

### Setup retry / baseline chapter1 admitted

Second masked handoff completed; corrected actor seeding reached Ready22:01:23. Existing helper verified schema001–009,3separate principals and limited runtime role. SQL actual14.0.2130.4/Developer Edition64-bit. Fresh restricted oracle baseline-admission verifies8ordinary actors,7active/1disabled,0protected-binding matches;54explicit role assignments with no initial direct grants/denies. Operator/disabled/revoked each13keys, deny5Inventory keys, post-only4Read/Post/Stock keys, writer4Read/Write/Stock keys, no-read onlyPost, restricted onlyReceiptRead. No T05 catalogs/receipts/lines/ledger/balances; independent full-key reconciliation0mismatches.

Chapter1 launched22:01:54, explicit30-minute cap/deadline22:31:54. Actual installed Login window identified; human Login/masking/T05Operator/Light confirmation pending. Desktop child environment excludes test/master/migration/probe credentials. Allocation168 is positively owned/admitted; earlier167ledgers are Dropped. GUI is live and resources deliberately retained under this bounded owner; final cleanup is not yet claimed. The successful SQL setup does not turn any attended ledger case into PASS.
