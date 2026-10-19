# GUI-P5-T05 — Attended Inventory walkthrough

**Status: IN PROGRESS —15/45 attended observations PASS; remaining baseline and supplemental UX pending. REMAIN-P5-GUI-02 OPEN.** Prepared2026-10-19, Asia/Bangkok, Codex; task base cdfa4b7. T04 accepted at cdfa4b7 is preserved. This worksheet records actual attended observations; existing automated results are separate.

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
| R-02 | Reload, edit quantity/note, Save/reload; same receipt ID, new opaque version, correct total. | PASS — Owner reopened full T05 baseline edited/Total12.50; same Draft1line3.125@4 and advanced versions, restricted oracle |
| R-03 | Required/empty-lines/zero-quantity and available numeric boundaries; safe errors, retained input, no unintended persisted change. Do not require the UI to accept a numeric value its control rejects. | PASS — Owner confirmed Quantity0 rejection/recovery and empty New/Save rejection; inspected errors, original-only Draft oracle, no stock |
| R-04 | Dirty unsaved editor blocks Refresh as shipped and preserves input; additional New/selection observations must be recorded separately. | PASS — Owner explicitly confirmed pre-Save Refresh blocked and retained edited input; no broader New/selection repetition claimed |
| R-05 | Post saved current Draft; Posted actor/time/status, one ledger movement per line and balance atomically match oracle. | PASS — Owner confirmed whole Post/Stock group; native artifacts and restricted oracles detailed below |
| R-06 | Posted editor immutable; duplicate Post blocked/rejected with no extra ledger/balance increment. Distinguish disabled button from a server request. | PASS — Owner confirmed whole Post/Stock group; native artifacts and restricted oracles detailed below |
| S-01 | Balance filters and visible totals agree with independent stock sum; no mixed-product grand-quantity claim. | PASS — Owner confirmed whole Post/Stock group; native artifacts and restricted oracles detailed below |
| S-02 | Card warehouse/product/date filters, order/document identity/visible delta agree with oracle. | PASS — Owner confirmed whole Post/Stock group; native artifacts and restricted oracles detailed below |
| S-03 | Empty/invalid filter, safe error and explicit Refresh recovery without stale rows/totals. | PASS — Owner confirmed whole Post/Stock group; native artifacts and restricted oracles detailed below |
| S-04 | Actual multi-page Balance/Card data when declared, Next/Previous and filter change page1; fixture rows distinguished from attended posts. | NOT RUN |
| P-01 | Ordinary Read+Post without Write: read saved Draft, server rejects Save without Write, Post authorized with correct actor and single stock effect. | PASS — Owner confirmed Save denial then Post success; restricted stock/audit oracle |
| P-02 | Read+Write without Post: Draft write succeeds, Post unavailable/denied; no posted mutation. | PASS — Owner confirmed Save success then server Post denial; Draft/token/stock preserved |
| P-03 | Post without Read cannot load/post an unseen receipt; no leaked detail or mutation. | NOT RUN |
| P-04 | Ordinary role Post grant plus direct Post Deny after enabled UI admission; server rejection/input preserved/no mutation, actual assignment snapshot. | NOT RUN |
| P-05 | Restricted Stock permissions: Balance/Card deny safely and clear rows/totals; no previous actor data. | NOT RUN |
| P-06 | Disabled user's actual Login denied safely; no admitted application session. | NOT RUN |
| P-07 | Actor disabled after actual Login: subsequent server operation fails closed/no stock or receipt mutation. | NOT RUN |
| C-01 | Stale Save rowversion loses to controlled accepted winner; safe conflict retains loser input and winner unchanged. | PASS — Owner confirmed safe conflict/loser input retained; restricted oracle preserves controlled SQL winner and stock |
| C-02 | Stale Post version after winner Draft update is rejected; no ledger/balance and no overwrite. | PASS — Owner confirmed safe conflict/no retry; restricted oracle preserves Draft winner and original-only stock |
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

T05 attended ledger currently15/45 Owner-confirmed PASS (15/27 baseline,0/18 supplemental); no automated invocation added. Package, SQL, scale, actor setup, actual observations, oracles, resources and timestamps will be recorded per cohort. Existing T03/T04 tests are not T05 attended passes. Final Owner sign-off PENDING; T05/REMAIN-P5-GUI-02 and T06/overallP5/P6 remain OPEN.

### Setup attempt1 — FAIL, safely disposed

At21:59 the first fresh T05 allocation reached actor seeding but SQL rejected a join between the default-collation table variable and production NormalizedUsername/NormalizedRoleName columns (SQL_Latin1_General_CP1_CI_AS versus Latin1_General_100_CI_AI). Desktop never launched; no attended case ran. Existing helper finally disposed the database/all3principals; exact census0 and167/167GUI ledgers Dropped. This is an attended setup-script defect, not an Inventory coverage/product failure.

Source002_Identity.sql explicitly declares both normalized identity columns Latin1_General_100_CI_AI. The ignored setup script now applies that exact collation at the two table-variable joins; parser verification PASS. A second fresh bounded masked-password setup is preparing; runtime SQL retry is not yet verified. The failure and its raw sanitized summary remain preserved; no PASS is inferred from the syntax check.

### Setup retry / baseline chapter1 admitted

Second masked handoff completed; corrected actor seeding reached Ready22:01:23. Existing helper verified schema001–009,3separate principals and limited runtime role. SQL actual14.0.2130.4/Developer Edition64-bit. Fresh restricted oracle baseline-admission verifies8ordinary actors,7active/1disabled,0protected-binding matches;54explicit role assignments with no initial direct grants/denies. Operator/disabled/revoked each13keys, deny5Inventory keys, post-only4Read/Post/Stock keys, writer4Read/Write/Stock keys, no-read onlyPost, restricted onlyReceiptRead. No T05 catalogs/receipts/lines/ledger/balances; independent full-key reconciliation0mismatches.

Chapter1 launched22:01:54, explicit30-minute cap/deadline22:31:54. Actual installed Login window identified; human Login/masking/T05Operator/Light confirmation pending. Desktop child environment excludes test/master/migration/probe credentials. Allocation168 is positively owned/admitted; earlier167ledgers are Dropped. GUI is live and resources deliberately retained under this bounded owner; final cleanup is not yet claimed. The successful SQL setup does not turn any attended ledger case into PASS.

Owner confirmed actual p5.t05.operator Login/T05Operator/Light with masked password and no Login error. A-01 PASS; remaining44applicable observations NOT RUN. Human creating/reading three T05 catalog rows through actual GUI; A-02 is pending confirmation and independent oracle.

### Catalog creation/readback —22:06,100% Light

Owner confirmed GUI create and reopen of Product T05-P-001/T05 Product/pcs, Warehouse T05-W-001/T05 Warehouse/Synthetic T05 and Employee T05-E-001/T05 Employee/0000000000/blankUserId. Restricted ownership-verified catalog-created snapshot matches all fields, all3Active, Employee.HasUserBinding=false;0receipts/lines/ledger/balances. Actual Employee editor/grid and Light theme inspected; A-02 PASS. [Employee readback](assets/gui-p5-t05/employee-readback-100light.jpg), SHA256 99CCEB0679DF43452F72D3FDE3AAC0F8F48E437674303200EFBA2D9A9EF4526A. Baseline2/27PASS; supplemental0/18, no Post yet.

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

### Quantity0 rejected without mutation —22:15,100% Light

Owner explicitly confirmed reopening the complete T05 baseline edited Note/Total12.50, then Quantity0/Save rejected and input retained. R-02 PASS for observed reopen/edit/persist identity and advanced versions. Current actual UI quantity/grid0, Total0, safe The request is invalid., inline error and Post disabled were inspected. Restricted zero-quantity-rejected oracle keeps original Draft/token2021, NoteT05 baseline edited, one3.1250@4line and0stock: no failed-write mutation. R-03 numeric lower-bound subcase observed, but full R-03 remains NOT RUN until the requested empty New/Save validation and recovery evidence. Baseline5/27PASS,0/18supplemental.

Owner instructed restore3.1250/Save, then New/Save fully blank and retain error screen, without Post. No unsaved reference choice is requested, so the existing clean blank editor should remain navigable; do not bypass a dirty guard if a different state is observed.

Quantity0 error artifact: [quantity-zero-rejected-100light.jpg](assets/gui-p5-t05/quantity-zero-rejected-100light.jpg), SHA256 A849D71C6E1FF8E93FBBFD21F081A29F3DF6B9121638DC075626EAD32EA76DC3. It was captured after fresh native observation and Owner's confirmation; all content is synthetic app-only. Current R-03 empty-New validation/recovery observation remains pending; no blind Save/Post retry requested.

### Empty Draft validation / recovery —22:17,100% Light

Owner restored3.1250/Save successfully, then New/Save with all fields empty rejected. Actual UI no Warehouse/Employee selection, blankNote/version,0lines, required icons, The request is invalid., disabled Post inspected. Restricted empty-draft-rejected oracle retains exactly the original GR0000000001 Draft, NoteT05 baseline edited, one3.1250@4line, token2022 after the successful restore Save,0stock. No extra Draft was created by the blank Save. R-03 PASS for required/empty-lines and numeric minimum0 rejection, retained input and observed recovery; no manual claim for inaccessible out-of-range values. Existing automated precision/range cases stay separate.

[Empty Draft rejection](assets/gui-p5-t05/empty-draft-rejected-100light.jpg), SHA256 ABB2B9CBB5EC20CDC595A844AEFE0128DEDDFC006F6354691D9461B6D2C8D626. Baseline6/27PASS;0/18supplemental. Owner now instructed to reopen only the original receipt, verify Total12.50 and Post once; after definite Posted, inspect readonly editor/Save/Post and duplicate prevention. R-05/R-06 pending actual results/oracle; no timeout/fault installed.

### Unsaved new-editor navigation guard / normal discard —22:25

Owner reported unable to open the original after blank validation. Fresh native state differed from the earlier fully blank screenshot: Warehouse/Employee now selected, emptyNote/version and0lines; actual workflow message Save the current draft before leaving it. The transition that selected those references was not independently observed; no cause is attributed. Source CanAbandonEditor blocks selection while IsDirty. The instruction to open directly was corrected for the actual dirty state, preserving this observation rather than bypassing the guard or saving an extra receipt.

Codex normally closed without Save22:25:22, chapter1 exit0/CleanupVerified before22:31:54 cap. Before/after-discard exact restricted oracles retain only original Draft/token2022/one3.125@4/NoteT05 baseline edited and0stock. [Unsaved navigation guard](assets/gui-p5-t05/unsaved-new-navigation-guard-100light.jpg), SHA256 B9223C6178E819BC590EB6EFA2075A5BF223BD79C9F9704BC02326D5D694CEBC. Same positively owned database retained for chapter2; human real Login requested, no Post has occurred. This is the existing guard already qualified at T04, not evidence of a new product fix or a second saved Draft.

### Chapter2 actual Post / Stock group — human confirmation pending

Chapter2 launched22:25:37 (deadline22:55:37); Owner completed actual ordinary Login and explicitly confirmed ready to observe Codex operating Post/Stock. Codex opened Goods Receipts, selected original grid row and Enter; complete NoteT05 baseline edited, token2022,3.1250@4/Total12.50 reloaded. One actual Post button click succeeded at15:28:13.1832705UTC (22:28:13+07). Fresh native state/screenshot Posted, disabled date/Warehouse/Employee/Note/Product/Quantity/cost/Save/Post/Add/Remove. A mouse click on the visibly disabled Post button caused no action; this is UI duplicate prevention, not a server duplicate request. Restricted snapshots baseline-posted/duplicate-post-ui-blocked preserve token2023, exactly1GoodsReceipt movement3.1250 with matching original line/warehouse/product/actor/time and one balance3.1250/token2024. PostedBy=P5.T05.OPERATOR; no orphan/mismatched movement or balance key.

Balance initially shows the one stock row but no mixed-product total (dash with ProductID0). Native ProductID1 then WarehouseID1 immediately filter the row and Visible total3.1250. Warehouse999 returns no rows/total0/No stock balances; restore1 and explicit Refresh returns the correct row/total/Ready. Card warehouse1/product1 with unchecked date boxes shows original document GR0000000001, TypeGoodsReceipt, PostedUTC15:28:13Z, delta3.1250. Enabling FromUTC22:30:06 (after Post) produces no rows/delta0. The attempted native DatePicker ValuePattern change while unchecked did not change the actual date, and is not counted as an applied date action. Actual keyboard Right selected year, Up changed From to2027; enabling To2026 then produced safe The request is invalid., cleared rows/delta0 and a synthetic Support correlation ID. Unchecking From then To and explicit Refresh recovers the original row/delta3.1250/operation statusReady. Global status retains the prior validation notification; no claim it clears on success. One-row ordering is observed only; multi-page/order evidence remains S-04.

Final post-stock-group-final restricted snapshot keeps1Posted receipt/1line/1ledger/1balance,0full-key reconciliation mismatches,0invalid posted-line/orphan movements. No further receipt/stock write occurred during these filter/error/recovery probes. R-05/R-06/S-01/S-02/S-03 are executed with artifacts/oracles but table cells remain NOT RUN until Owner confirms this attended group. No permission/conflict/fault or supplemental context is inferred.

| Artifact | SHA-256 |
|---|---|
| [receipt-posted-locked-100light.jpg](assets/gui-p5-t05/receipt-posted-locked-100light.jpg) | E2241361AE4725B64B57A86C2F13F59DF62D93ECD414757F25F860C149AAF148 |
| [balance-filtered-100light.jpg](assets/gui-p5-t05/balance-filtered-100light.jpg) | CA1C5B501243B321191E9FBC2CECFF7B78F4EB2C7130B5E922BCFA713BD71813 |
| [balance-empty-100light.jpg](assets/gui-p5-t05/balance-empty-100light.jpg) | CA6A63E4D39B1B0A49F49B2789CF3FA037FA6FE30DEF19B0A648AA3040662164 |
| [balance-refresh-recovered-100light.jpg](assets/gui-p5-t05/balance-refresh-recovered-100light.jpg) | 589332C0011F37389F3252B43EA8AC3DE3E2B904DDE430E1047FFC70CDAE6CAD |
| [stock-card-movement-100light.jpg](assets/gui-p5-t05/stock-card-movement-100light.jpg) | CD63281768606BAF0FB0BE194B93C389CD6A1E32C24FB3945E67B772670614DF |
| [stock-card-date-empty-100light.jpg](assets/gui-p5-t05/stock-card-date-empty-100light.jpg) | 66E0BBF2304E342794BA5A7FB701FA0218B9F25E4107B2887255F212B7A33002 |
| [stock-card-invalid-date-100light.jpg](assets/gui-p5-t05/stock-card-invalid-date-100light.jpg) | 49508A418B41D82E2A7BF9CF6D2AB4D13D098622BE2C88BF011FC030B08867AA |
| [stock-card-refresh-recovered-100light.jpg](assets/gui-p5-t05/stock-card-refresh-recovered-100light.jpg) | DD0B1981BB1AC9B61EF018B4943E9BDB23B9E1BCFB448833EE09F7C96F8DCB3D |

### Permission actor lookup prerequisites

Before logging the permission actors in, source LookupProducts/Warehouses/Employees query authorization was checked: each requires its Catalog.*.Read key. The initial actor setup's Inventory-only post-only/writer/deny roles would not provide these lookup prerequisites. An exact ownership/principal-verified migration-side fixed control added only3CatalogRead keys to each of those3roles (9assignments), before those actors were used. No CatalogWrite, InventoryWrite or Post privilege was added by this correction. Restricted oracle permission-lookup-prerequisites now records63role assignments: post-only7, writer7, deny8; their original Inventory permission differences remain. There are still8ordinary actors/0protected matches. This is test setup correction, not a product permission change or an attended PASS. Inventory remains one original Posted/one ledger/one balance3.1250.

Owner explicitly confirmed all requested actual Post/Stock/empty/date-error/Refresh observations. R-05/R-06/S-01/S-02/S-03 PASS within their recorded limits; total11/45attended observations PASS. No new full-suite/permission/P6 acceptance is implied.

### Separate fixture Drafts and stale rowversion cases

Exact owned maintenance control prepared7separate schema-valid Drafts, with explicit synthetic T05-FIX-* numbers and one line each referencing the GUI-created T05 catalog rows. These are setup-only fixtures, not additional GUI create/Save passes. Post-only quantity0.25; writer/deny/revoked/stale-save/stale-post/fault quantities1, cost4. Original GR0000000001 remains the sole Posted/ledger/balance3.125. First preparation attempt had unquoted reserved column LineNo and failed before adding rows; restricted oracle confirmed only original state. Corrected [LineNo] batch succeeded atomically:8total receipts/8lines,7Drafts. No product/schema/permission contract changed.

C-02: actual GUI loaded T05-FIX-POST NoteT05 stale post/token2030. Controlled maintenance update changed persisted Note toT05 conflict winner post/token2032, not a second GUI client. Fresh native view still held oldNote/token, Post enabled. Actual Post rejected The goods receipt was changed by another operation.; GUI retained old snapshot. Restricted stale-post-rejected query confirms Draft/winnerNote/token2032 unchanged,1original ledger/balance3.125.

C-01: actual GUI loaded T05-FIX-SAVE/token2029, Codex edited Note toT05 stale loser input. Controlled maintenance update persisted T05 conflict winner save/token2033. Fresh GUI still held loserinput/token2029. Actual Save rejected the same safe conflict, preserved loserinput and did not report success; Post remains disabled while dirty. Restricted stale-save-rejected confirms accepted winnerNote/token2033 and original stock unchanged. No blind retry. Owner explicitly confirmed both stale Post and stale Save observations, safe conflicts, preserved loser input and no success/blind retry; C-01/C-02 PASS. Global Support GUIDs shown in app-only artifacts are synthetic request correlations, not SQL allocation identities.

| Artifact | SHA-256 |
|---|---|
| [stale-post-before-100light.jpg](assets/gui-p5-t05/stale-post-before-100light.jpg) | 65F1E1F66B12B84A74D90E295E6AB965A6533E1B01ADD77BD7B11950B32383F8 |
| [stale-post-rejected-100light.jpg](assets/gui-p5-t05/stale-post-rejected-100light.jpg) | C78E8DE216D7B3DC6860084E4DAB5EC011832C63059DB8A441A163FBFD376980 |
| [stale-save-before-100light.jpg](assets/gui-p5-t05/stale-save-before-100light.jpg) | EB0818072C153C11324E2AD1C0EFA5659C0527B046A5579D8B25AA5E104B0AEC |
| [stale-save-rejected-100light.jpg](assets/gui-p5-t05/stale-save-rejected-100light.jpg) | 06ED0B75D1876B30ACE389D56ED56FFCC1CB62A3B0C79E17C3BCEA4113800B58 |

Checkpoint22:45:13/45observations PASS. Sign out discarded unsaved stale loser input; actual human Login as p5.t05.post-only requested for P-01. Chapter2 remains bounded until22:55:37; retained allocation168/final disposal pending. No fault installed or additional stock effect.

### Post-only permission observation — Owner confirmed

Human confirmed T05 Post Only after real Login. Actual GUI loaded setup-only T05-FIX-POSTONLY, NoteT05 post only, quantity0.25@4/Total1/token2025. Contrary to the initial worksheet expectation, editor/Save are enabled for Draft regardless of role: GoodsReceiptControl canEdit uses busy/status/access-denied/outcome-lock state, without permission prefetch. Actual Save with unchanged fields returns The operation is not permitted.; restricted post-only-save-denied snapshot preserves Draft/token2025 and original stock3.125/oneledger. This corrects an unsupported UI-lock expectation; it does not waive the server Write authorization requirement or claim edits are visually prevented.

One actual Post then succeeds despite absent Write: restricted post-only-posted snapshot records Posted/token2035, PostedByP5.T05.POST-ONLY, exactly one additional GoodsReceipt movement0.25, total2movements/onebalance3.375. Full-key reconciliation and posted-line/document invariants all0mismatches. Actual Posted editor/Save/Post/Add/Remove are disabled. Role assignment snapshot contains ReceiptRead/Post/StockRead and three CatalogRead prerequisites, no Write/protected binding. Owner explicitly confirmed this group with the subsequent Writer observation; P-01 PASS. No product/test byte change. Sign out performed; human p5.t05.writer Login requested.

- [post-only-draft-100light.jpg](assets/gui-p5-t05/post-only-draft-100light.jpg), SHA256 484C0D8E0EB5E8B81A4A1BD3273C9B33B9F8A10966369533BDE713322101F011.
- [post-only-save-denied-100light.jpg](assets/gui-p5-t05/post-only-save-denied-100light.jpg), SHA256 F52070241D569D032C2D575A2BB3F78A1A12B4B842FACB06A2963573FAE65049.
- [post-only-posted-100light.jpg](assets/gui-p5-t05/post-only-posted-100light.jpg), SHA256 F8D7F05C6E3C2DD7FF8461824971C1E6E0C93AAA2AF17352A8978A8A7A720E93.

### Writer without Post — Owner confirmed

Human Login confirmed by actual shell T05 Writer No Post, clean workspace with no prior actor editor. Actual T05-FIX-WRITER Draft loaded/token2026; native Note changed toT05 writer saved without Post, actual Save succeeded/token2038. Actual Post button was enabled after Save; one click returned The operation is not permitted., preserving Note/Draft/token2038. Restricted writer-saved and writer-post-denied snapshots preserve2ledger movements/onebalance3.375,0full-key or line/document mismatches. Owner explicitly confirmed both Post-only and Writer groups: P-01/P-02 PASS,15/45total. UI admission is not permission enforcement; no disabled-Post assertion for this role. No product change.

- [writer-saved-100light.jpg](assets/gui-p5-t05/writer-saved-100light.jpg), SHA256 33212C1C532CD84835CAB42C8E98711261A9F77717E712EF3BAD5A8A21CDAD7A.
- [writer-post-denied-100light.jpg](assets/gui-p5-t05/writer-post-denied-100light.jpg), SHA256 9947407750EF0B71A3190B90FE68269B518B34B03733B8BA1FC72D48852FCCFB.

Chapter2 closed normally22:51:15/exit0/CleanupVerified before22:55:37 cap. Chapter3 launch requested while root admission remains open, same positively owned allocation; final disposal pending.
