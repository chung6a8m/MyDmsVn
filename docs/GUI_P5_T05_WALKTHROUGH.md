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
| A-01 | Actual ordinary Login, masked password, correct actor/session. | PASS — Owner confirmed p5.t05.operator/T05Operator, Light and password masking |
| A-02 | GUI create/read fresh T05 Product/Warehouse/Employee; independent fields/active identity. | PASS — Owner created/read all3; inspected Employee image and restricted oracle catalog-created exact fields |
| R-01 | New Draft, three lookups, quantity/cost/note; Save and independently verify aggregate. | PASS — Owner Save confirmed; GUI/SQL same Draft1line2.5@4/Total10; actual Note05 baseline draft qualified below |
| R-02 | Reload, edit quantity/note, Save/reload; same receipt ID, new opaque version, correct total. | NOT RUN |
| R-03 | Required/empty-lines/zero-quantity and available numeric boundaries; safe errors, retained input, no unintended persisted change. Do not require the UI to accept a numeric value its control rejects. | NOT RUN |
| R-04 | Dirty unsaved editor blocks Refresh as shipped and preserves input; additional New/selection observations must be recorded separately. | PASS — Owner explicitly confirmed pre-Save Refresh blocked and retained edited input; no broader New/selection repetition claimed |
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

Owner confirmed actual p5.t05.operator Login/T05Operator/Light with masked password and no Login error. A-01 PASS; remaining44applicable observations NOT RUN. Human creating/reading three T05 catalog rows through actual GUI; A-02 is pending confirmation and independent oracle.

### Catalog creation/readback —22:06,100% Light

Owner confirmed GUI create and reopen of Product T05-P-001/T05 Product/pcs, Warehouse T05-W-001/T05 Warehouse/Synthetic T05 and Employee T05-E-001/T05 Employee/0000000000/blankUserId. Restricted ownership-verified catalog-created snapshot matches all fields, all3Active, Employee.HasUserBinding=false;0receipts/lines/ledger/balances. Actual Employee editor/grid and Light theme inspected; A-02 PASS. [Employee readback](assets/gui-p5-t05/employee-readback-100light.jpg), SHA25699CCEB0679DF43452F72D3FDE3AAC0F8F48E437674303200EFBA2D9A9EF4526A. Baseline2/27PASS; supplemental0/18, no Post yet.

Draft instructions issued: one T05-P-001 line2.5000×4.0000, T05-W-001/T05-E-001, Note T05 baseline draft, Save/Total10.00, no Post. R-01 awaits human confirmation, screenshot and oracle. Ignored scoped maintenance controls for later direct-Deny/revocation/rowversion-winner fixtures have syntax validation only and have not executed; every action revalidates exact instance/database ownership and captured maintenance principal. A controlled rowversion winner will be explicitly labeled SQL fixture manipulation, not a second GUI client.

### Initial saved Draft —22:09,100% Light

Owner confirmed Save completed. Restricted baseline-draft-saved oracle: GR0000000001 Draft, one T05-P-001 line2.5000@4.0000, T05-W-001/T05-E-001, rowversion token2019;0ledger/balance/reconciliation mismatches. Inspected actual controls/grid/status match Total10.00 and Goods receipt saved. Actual Note is05 baseline draft in both UI and SQL, differing from the requested synthetic label T05 baseline draft; the actual value is retained in evidence rather than silently corrected or attributed to a product fault. R-01 PASS for the observed aggregate persistence; the next edit explicitly changes Note toT05 baseline edited.

[Saved Draft](assets/gui-p5-t05/draft-saved-100light.jpg), SHA256 C909C3FFFFD0C04A6EBD35A4FDF8F3FC1932014AED0E51F3C56F0C69E25C016F. Baseline3/27PASS, supplemental0/18. Owner instructed to reopen, edit3.1250/NoteT05 baseline edited, attempt Refresh while dirty (must block/preserve), Save and reopen with Total12.50; R-02/R-04 pending human/oracle. No Post authorized yet.

### Edit/reload and Note pre/post-Save diagnosis —22:13,100% Light

Owner completed the requested reopen/edit3.1250/dirtyRefresh-block/Save/reopen sequence. Restricted baseline-edited-reloaded snapshot confirms original GR0000000001,3.1250@4.0000,Total12.50, token2020 (from2019), Draft and0stock. Actual Note again read05 baseline edited in both UI and SQL. The precise source of that requested-label discrepancy was not established; no product defect or user typo is inferred.

Codex explicitly set the real Note inner editor through its native ValuePattern toT05 baseline edited, then inspected both accessibility and screenshot before Save. Post was disabled while dirty. Codex clicked the actual Save; screenshot and independent note-explicit-before-after-save snapshot preserve the complete Note, token2021,Total12.50 and0stock. Thus this actual Save probe did not reproduce loss of the leadingT. It does not prove all typing/focus paths defect-free. No product/test bytes changed. Owner is now asked to reopen/check the complete Note before Quantity0 validation; R-02 final full-Note confirmation remains pending. R-04 PASS for the explicitly confirmed dirty Refresh path, without claiming unobserved New/selection repetitions.

| Artifact | SHA-256 |
|---|---|
| [note-before-save-100light.jpg](assets/gui-p5-t05/note-before-save-100light.jpg) | 0AAEE26986DCDCDB6D4DB80D0E923B974BC531251107ECD20663468250F289D9 |
| [draft-edited-note-verified-100light.jpg](assets/gui-p5-t05/draft-edited-note-verified-100light.jpg) | 0557FC653F0BD467A35506B9227ED35F969104AA2F0E1A0955CC7B48458FE89D |

Scope ruling: R-04 attended action is the requested dirty Refresh rejection/input retention. New/selection variants remain separately covered by existing control tests and are not relabeled as attended repetitions; T05 plan does not mandate every equivalent dirty-guard trigger.
