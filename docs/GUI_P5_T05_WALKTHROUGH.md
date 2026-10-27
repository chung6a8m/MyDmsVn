# GUI-P5-T05 — Attended Inventory walkthrough

> Historical evidence record. Original checkpoints, failures, exceptions and task handoffs retain their dated scope. Current work is selected through the [execution index](plans/README.md). This record does not schedule new work or supply acceptance for another candidate.

**Status: COMPLETE / OWNER ACCEPTED —44PASS,1PARTIAL with explicit accepted Down exception. REMAIN-P5-GUI-02 PASS within signed scope.** Prepared2026-10-19, Asia/Bangkok, Codex; task base cdfa4b7. T04 accepted at cdfa4b7 is preserved. This worksheet records actual attended observations; existing automated results are separate.

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
| S-04 | Actual multi-page Balance/Card data when declared, Next/Previous and filter change page1; fixture rows distinguished from attended posts. | PASS — Owner explicitly confirmed consolidated actual observations; scoped artifacts/oracles and qualifications below |
| P-01 | Ordinary Read+Post without Write: read saved Draft, server rejects Save without Write, Post authorized with correct actor and single stock effect. | PASS — Owner confirmed Save denial then Post success; restricted stock/audit oracle |
| P-02 | Read+Write without Post: Draft write succeeds, Post unavailable/denied; no posted mutation. | PASS — Owner confirmed Save success then server Post denial; Draft/token/stock preserved |
| P-03 | Post without Read cannot load/post an unseen receipt; no leaked detail or mutation. | PASS — Owner explicitly confirmed consolidated actual observations; scoped artifacts/oracles and qualifications below |
| P-04 | Ordinary role Post grant plus direct Post Deny after enabled UI admission; server rejection/input preserved/no mutation, actual assignment snapshot. | PASS — Owner explicitly confirmed consolidated actual observations; scoped artifacts/oracles and qualifications below |
| P-05 | Restricted Stock permissions: Balance/Card deny safely and clear rows/totals; no previous actor data. | PASS — Owner explicitly confirmed consolidated actual observations; scoped artifacts/oracles and qualifications below |
| P-06 | Disabled user's actual Login denied safely; no admitted application session. | PASS — Owner confirmed actual Login error/still Login; SQL Active=false/nonprotected and inventory unchanged |
| P-07 | Actor disabled after actual Login: subsequent server operation fails closed/no stock or receipt mutation. | PASS — Owner explicitly confirmed consolidated actual observations; scoped artifacts/oracles and qualifications below |
| C-01 | Stale Save rowversion loses to controlled accepted winner; safe conflict retains loser input and winner unchanged. | PASS — Owner confirmed safe conflict/loser input retained; restricted oracle preserves controlled SQL winner and stock |
| C-02 | Stale Post version after winner Draft update is rejected; no ledger/balance and no overwrite. | PASS — Owner confirmed safe conflict/no retry; restricted oracle preserves Draft winner and original-only stock |
| K-01 | Catalog rename/active change while Receipt/Stock views exist; fresh/historical names and active-only new choices match shipped contracts. | PASS — Owner explicitly confirmed consolidated actual observations; scoped artifacts/oracles and qualifications below |
| L-01 | Real logout/other account/restart clears former actor data and reads current persisted Draft/Posted state. | PASS — Owner explicitly confirmed consolidated actual observations; scoped artifacts/oracles and qualifications below |
| F-01 | Actual SQL Post timeout observed; safe uncertain outcome and mutation lock; independently establish persisted state after worker drain. | PASS — Owner explicitly confirmed consolidated actual observations; scoped artifacts/oracles and qualifications below |
| F-02 | Exact fault removed, explicit read reload resolves status; only then justified single retry, one final stock effect; restart/schema preflight usable. | PASS — Owner explicitly confirmed consolidated actual observations; scoped artifacts/oracles and qualifications below |
| O-01 | Independent full-key ledger/balance reconciliation, receipt-line document identity and no denied/failed mutation. | PASS — final restricted oracle and15-table unchanged comparison;0full-key/line/document mismatches |
| O-02 | Sanitized screenshot/hash/actor/environment ledger; normal GUI drain and positively owned final DB/principal cleanup/exact census. | PASS — final133-file verification, inspected artifacts, chapter4exit0/CleanupVerified,169/169Dropped/exactcensus0 |

## Supplemental UX ledger (Owner-approved scope)

| Case | Action / required evidence |100% Dark|125% Light|125% Dark|
|---|---|---|---|---|
| UX-R | Receipt keyboard/focus/errors, windowed/maximized layout and Posted/busy command states. | PASS — Owner confirmed100Dark observations; scope/qualifications below | PASS — Owner confirmed125% observations; documented qualifications retained | PASS — Owner confirmed125% observations; documented qualifications retained |
| UX-B | Balance keyboard/filter/Refresh/error/totals/grid/paging bounds and resize. | PASS — Owner confirmed100Dark observations; scope/qualifications below | PASS — Owner confirmed125% observations; documented qualifications retained | PASS — Owner confirmed125% observations; documented qualifications retained |
| UX-S | Stock Card keyboard/date filters/Refresh/errors/totals/grid/paging bounds and resize. | PASS — Owner confirmed100Dark observations; scope/qualifications below | PASS — Owner confirmed125% observations; documented qualifications retained | PASS — Owner confirmed125% observations; documented qualifications retained |
| UX-P | Product popup F4/filter/arrows/Enter/reopen/Esc, focus/work-area bounds. | PASS — Owner confirmed100Dark observations; scope/qualifications below | PASS — Owner confirmed125% observations; documented qualifications retained | PASS — Owner confirmed125% observations; documented qualifications retained |
| UX-W | Warehouse popup F4/filter/arrows/Enter/reopen/Esc, focus/work-area bounds. | PASS — Owner confirmed100Dark observations; scope/qualifications below | PASS — Owner confirmed125% observations; documented qualifications retained | PARTIAL — Owner confirmed observed behavior; Down NOT TESTED, Enter/F4/Esc/filter observed |
| UX-E | Employee popup F4/filter/arrows/Enter/reopen/Esc, focus/work-area bounds. | PASS — Owner confirmed100Dark observations; scope/qualifications below | PASS — Owner confirmed125% observations; documented qualifications retained | PASS — Owner confirmed125% observations; documented qualifications retained |

## Session / artifacts / decision

T05 evidence ledger currently44/45 PASS (27/27 baseline,17/18 supplemental); the remaining Warehouse125Dark cell is PARTIAL solely because Down was not tested. All actual125% observations are Owner-confirmed; no automated invocation added. Package, SQL, scale, actor setup, actual observations, oracles, resources and timestamps will be recorded per cohort. Existing T03/T04 tests are not T05 attended passes. Final Owner sign-off received2026-10-20 against5ed7bc6, including the explicit Down exception; T05 COMPLETE / REMAIN-P5-GUI-02 PASS within signed scope. T06/overallP5/P6 remain OPEN.

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

### Direct Post Deny — executed, Owner confirmation pending

Chapter3 actual human Login displayed T05 Direct Deny with clean workspace. GUI loaded T05-FIX-DENY Draft, NoteT05 direct deny/token2027, one1@4line/Total4; Post enabled before assignment change. Existing exact-owned maintenance DenyPost control validated ordinary active/nonprotected actor and role Post grant, then inserted only direct Inventory.GoodsReceipts.Post Deny. Restricted direct-deny-assigned and direct-deny-rejected snapshots verify RoleGrantPost plus DirectGrant0. One actual Post from the still-enabled UI returned The operation is not permitted.; Note/Draft/token2027 retained, total2ledger/onebalance3.375 and0reconciliation/posted-line/document mismatches. No blind retry or extra mutation. P-04 pending Owner observation confirmation; no product/test changes. Sign out then human p5.t05.no-read Login requested.

- [direct-deny-before-100light.jpg](assets/gui-p5-t05/direct-deny-before-100light.jpg), SHA256 5D11040D27628F153D0EE244216F1F427A0E92764463EAC8EFE7AD4AC21AA9A9.
- [direct-deny-rejected-100light.jpg](assets/gui-p5-t05/direct-deny-rejected-100light.jpg), SHA256 90C3DD5BA38EF8C109A38A2ADAC2ABB87AADA41A9989C15589CC232112AE0654.

### Overnight resume —2026-10-20

Owner reported seeing T05 Post Without Read. On resume at10:29+07, actual prior supervisor state is Disposed: chapter3 normal exit0/CleanupVerified at2026-10-19T23:10:13+07, helper disposed23:10:14. Fresh exact census confirms168/168GUI ledgers Dropped, latest database/principal/process counts all0. Therefore the reported actor label alone does not close P-03; no inaccessible old database is reused. Prior15/45confirmed passes remain valid against their captured candidate/cohort. P-04 direct-Deny human observation confirmation remains pending. Fresh bounded password handoff/cohort preparation started for remaining cases only; setup-only replacement references/Drafts must not be relabeled GUI-created evidence. T05/T06/P5/P6 remain OPEN.

### Continuation cohort admission —2026-10-20

Fresh masked handoff completed and supervisor Ready10:30:27+07. Reverified100% native x86 scale/HRESULT0 and physical1920x1080. Existing helper created a new disposable database/current001–009 with separated identities on SQL2017Developer14.0.2130.4; ordinary8actors/63role grants after explicit lookup prerequisite setup. Ownership-checked maintenance seeded three clearly named T05 Continuation catalog references and7separate T05-FIX-* Drafts/lines. These are setup-only replacement fixtures, not GUI creation evidence and not the disposed original GR0000000001. Restricted continuation-admission confirms0ledger/balance,0reconciliation mismatches; denied operations in this cohort must preserve this fresh baseline. Launch requested for actual p5.t05.no-read Login. No new attended PASS or product/test change. Prior15confirmed passes retain their original cohort provenance; final cleanup and sign-off pending.

### Post without Read — continuation cohort, confirmation pending

Owner confirmed actual Login label T05 Post Without Read. Native shell has clean workspace; opening Goods Receipts then actual Refresh each rejects The operation is not permitted. Grid has no receipt rows; Warehouse/Employee/Note/version/lines empty, Post disabled. No detail from a previous actor is displayed. This is blocked GUI admission, not a submitted server Post against an unseen ID. Restricted continuation-no-read-denied oracle independently sees the7setup Drafts/7lines,0ledger/0balances and0full-key/line/document mismatches. Actual actor has only Inventory.GoodsReceipts.Post, no Read/Write/Stock/Catalog grants or direct assignments. P-03 awaits Owner observation confirmation;15/45confirmed count retained. Sign out performed; human p5.t05.restricted Login requested. Current bounded chapter1 launched10:31:55/deadline11:01:55, supervisor58659 remains active; command admission ends11:25:27. Old168ledgers Dropped, current169 allocation retained with final disposal pending.

- [no-read-denied-100light-continuation.jpg](assets/gui-p5-t05/no-read-denied-100light-continuation.jpg), SHA256 76BED9113231402D2B4ED8987FB47CBD062383D34F63E6430FB82CB875610E59.

### Restricted Stock permissions — executed, confirmation pending

Actual human Login label T05 Restricted Reader confirmed. Ordinary actor has only Inventory.GoodsReceipts.Read, no Stock/Catalog/Write/Post grants. GUI Receipt list reads all7setup Drafts; Catalog lookup initialization emits separate safe denial, so do not claim every related request succeeded. Actual Balance opening returns The operation is not permitted., empty grid, visible total dash. Card initial0/0 requests valid selection without data; after actual WarehouseID1/ProductID1 entries, server request returns The operation is not permitted., empty grid/delta0. No previous-actor rows/totals. Restricted continuation-restricted-stock-denied snapshot retains7Drafts/7lines/0ledger/0balances,0reconciliation/line-document mismatches. P-05 awaits human observation confirmation; accepted count remains15/45. Actual Sign out executed, human p5.t05.disabled Login rejection requested. No authentication automation or product/test change.

- [restricted-receipt-list-100light.jpg](assets/gui-p5-t05/restricted-receipt-list-100light.jpg), SHA256 5A08925FB7AF2DB01707101DD458D0478614D9C47CCD0184189E3972B03838A6.
- [restricted-balance-denied-100light.jpg](assets/gui-p5-t05/restricted-balance-denied-100light.jpg), SHA256 5615F7DB530C5986E91FC7A8A659598DD79040C6E13801752D693142623D825E.
- [restricted-card-denied-100light.jpg](assets/gui-p5-t05/restricted-card-denied-100light.jpg), SHA256 FAB5AB00484EB7DF0D6160A8DC330C56C4D809E4EC724E54239A96F91CB4A5A0.

### Disabled Login — Owner confirmed

Owner performed actual p5.t05.disabled Login using the test password and explicitly confirmed an error with application remaining at Login. Fresh native window inventory returns only MyDmsVn — Đăng nhập, no admitted shell. Authentication controls were not automated or captured. Restricted continuation-disabled-login-denied verifies IsActive=false/nonprotected for disabled actor, existing role grants,7Drafts/0ledger/0balance/0reconciliation mismatches. P-06 PASS; total16/45confirmed observations. Exact error wording was not supplied, so no literal Login-message assertion is made. Actor p5.t05.revoked independently remains Active=true at this snapshot; human Login requested before controlled disable. P-03/P-04/P-05 observation confirmations remain pending. Current chapter1deadline11:01:55/finalcleanup pending.

### Disable after actual Login — executed, confirmation pending

Owner confirmed actual T05 Revoke After Login. While Active, GUI loaded T05-FIX-REVOKED Draft, NoteT05 revoked/token2021/one1@4line, Post enabled. Exact-owned maintenance DisableRevoked control changed only the expected active ordinary nonprotected actor toInactive. Restricted continuation-revoked-disabled verifies that state. One actual Post from the still-open shell then removed that shell and returned to Login; snapshot against old shell reported no screenshot target, fresh window inventory identified only MyDmsVn — Đăng nhập. Do not interpret this as an application crash or claim an unobserved inline error. Restricted continuation-revoked-post-rejected retains targetDraft/Note/token2021,7Drafts/0ledger/0balance/0reconciliation mismatches. Authentication UI was not automated/captured. P-07 human observation confirmation pending. Operator actual Login requested for remaining baseline. P-06 remains confirmed PASS; accepted total16/45; pending P-03/P-04/P-05/P-07 not promoted.

- [revoked-before-disable-100light.jpg](assets/gui-p5-t05/revoked-before-disable-100light.jpg), SHA256 D19E7A7FC7530D25010B29F95436F5C0FF82B5ABE59DF9BE89FC63250DA0816C.

### Actual Post timeout / recovery — confirmation and restart pending

On unchanged packaged Operator session, exact-owned migration installed a dedicated45-second WAITFOR trigger restricted to T05-FIX-FAULT Draft-to-Posted. Fault ownership record captures objectID/create-date/exactdefinition/random object marker; database and captured runtime/maintenance identities revalidated. Before Post runtime requests0. One actual Post entered SQL: admin observer with verified VIEW SERVER STATE recorded exact owned runtimeRequests1/WAITFOR1. Busy controls/lines disabled. After SQL timeout actual GUI says Posting outcome unknown. Refresh to reload the receipt before making further changes. An unexpected error occurred. Safe Support correlation only; Note retained, editor/Save/Post/New/lines locked and Refresh available.

After runtimeRequests0/WAITFOR0, restricted continuation-post-timeout-drained established actual rollback: targetDraft/token2024, NoteT05 uncertain,0ledger/0balance/mismatches. Only then fault removal verified exact objectID/create-date/definition/marker and0runtime requests. Actual Refresh explicitly reloaded that Draft and unlocked Post. One justified retry succeeded: continuation-post-retry-success targetPosted/token2031/PostedByP5.T05.OPERATOR, exactly1movement1 and1balance1,0full-key/line/doc mismatches. Actual editor/Save/Post/Add/Remove disabled. No blind retry; global old notification qualification remains. F-01 awaits Owner observation confirmation; F-02 additionally awaits normal restart/schema-preflight/readback. Fault recorded Removed=true; no fault remains installed.

### Paging fixture and actual Balance/Card navigation — confirmation pending

Exact-owned maintenance prepared26synthetic Posted paging receipts T05-PAGE-001..026 dated2001-01-01 seconds1..26, each2lines: baseProduct1 and one separate PAGE-P product, quantity1each. These are setup-only SQL fixtures, not attended GUI Posts. Existing actual timeout-retry movement remains distinct. Atomic fixture reconciles53ledger/27balance keys, baseproduct quantity27, each26otherproducts quantity1. Restricted continuation-paging-stock-seeded confirms33receipts/59lines,53ledger/27balances,0full-key/line/doc mismatches.

Actual Balance page1 lists PAGE-P-001..025; Next page2 lists PAGE-P-026 and baseT05 quantity27; Previous returnspage1. Next then Search PAGE-P-026 frompage2 yieldspage1of1/onequantity1row; mixed-product total stays dash. Card1/1 lists25chronological paging movements/delta25page1; Next page2 shows T05-PAGE-026 followed by actual T05-FIX-FAULT/delta2, Previouspage1. Next then Product2 filter resetspage1of1/onlyT05-PAGE-001/delta1. No additional writes. S-04 awaits Owner confirmation. Accepted count remains16/45; unconfirmed observations are not PASS.

- [post-fault-busy-100light.jpg](assets/gui-p5-t05/post-fault-busy-100light.jpg), SHA256 F2CAD33A2D038174A591E1ABF358B16ADB6C6C375046D234CE6E170BB8C11133.
- [post-timeout-outcome-locked-100light.jpg](assets/gui-p5-t05/post-timeout-outcome-locked-100light.jpg), SHA256 51CC8F80D662782C1E463220138B5A29F1EE1A4B7BDA373730E7803E69E53D8A.
- [post-timeout-explicit-reload-100light.jpg](assets/gui-p5-t05/post-timeout-explicit-reload-100light.jpg), SHA256 7EE8BE3E98B27D06C4AD6FA462DF0AF2E656FFDEA1C4B944A92C540E0C512390.
- [post-timeout-single-retry-posted-100light.jpg](assets/gui-p5-t05/post-timeout-single-retry-posted-100light.jpg), SHA256 485705868D657E2AA7D2FF82857A7053EBB653BB77700491733EDB5C5628F638.
- [balance-page1-100light.jpg](assets/gui-p5-t05/balance-page1-100light.jpg), SHA256 A8F9F9ADB5B226AE4FB965B1F16512F4B097D01D2BC6564C405E70E84BA1A9EC.
- [balance-page2-100light.jpg](assets/gui-p5-t05/balance-page2-100light.jpg), SHA256 411EAB919CEFA376B5D6FBEA01C266C849DEEAD26E504FCBF59AE314D6D451E9.
- [balance-search-reset-page1-100light.jpg](assets/gui-p5-t05/balance-search-reset-page1-100light.jpg), SHA256 1E49639F8056D2D2DF777B12BE5EA0CA27679F223C10D53EC2898DC372DD4D3A.
- [card-page1-100light.jpg](assets/gui-p5-t05/card-page1-100light.jpg), SHA256 D64F91F5791A1D78C22BFF172C271F1677721C72B0EAE448532AEBC5782E7660.
- [card-page2-100light.jpg](assets/gui-p5-t05/card-page2-100light.jpg), SHA256 C2CFCB6C6EE44CE021F154925ECEF0FE21B0A24CE9B64F1BA62AC45B72B5ADE1.
- [card-filter-reset-page1-100light.jpg](assets/gui-p5-t05/card-filter-reset-page1-100light.jpg), SHA256 183BEC0E893E29380E4410BB42556F9BB2330755A251568097B033C992ED5051.

### Catalog changes while Inventory views exist — confirmation pending

With original Posted Receipt and Stock tabs alive, exact-owned maintenance changed only the3Continuation reference names to endChanged, then deactivated them. These are controlled fixture changes, not GUI Catalog-edit repetitions. Fresh Card1/1query reads newProduct/Warehouse names; after deactivate+Refresh, actual native row values include(inactive), retain27movements/stockhistory. Receipt explicitRefresh rehydrates all3historical names despiteInactive, samePosted/token2031/lineidentity. New unsaved Draft Warehouse/Employee F4popups each0/0; Product exactT05-P-001 query0/0 despite26otherActive pagingproducts. After exact-owned Reactivate, fresh exactqueries each1/1 and actualEnterselectsall3Changed references. F4alone initially reused cachedemptyProductresults afterActive; freshfilterquery refreshed them, no claim of unsolicitedlivecacheinvalidation. NoSave/Post on temporaryDraft.

Restricted continuation-inactive-history/continuation-reactivated-history/before-restart preserve33receipts/59lines,53ledger/27balances,0full-key/line/doc mismatches; onlynamedcatalogfields/Active flags changed. K-01 awaits Ownerconfirmation. Chapter1 normalclose/discard10:53:10/exit0/CleanupVerified before11:01:55cap. Newchapter2restart requested to verify schema-preflight and readofactualPostedrecoveryreceipt; F-02/L-01 pending. Accepted16/45unchanged while humanconfirmationspending.

- [card-renamed-references-100light.jpg](assets/gui-p5-t05/card-renamed-references-100light.jpg), SHA256 D140BD215C566664E4340C93233A558BD15D917906A30127EA36B9C98CD3A4EB.
- [card-inactive-history-100light.jpg](assets/gui-p5-t05/card-inactive-history-100light.jpg), SHA256 3901A6B571F6EE2E50C9285668D2CC05F151BB05A5C9456F7542668CABDC2BA8.
- [receipt-inactive-historical-references-100light.jpg](assets/gui-p5-t05/receipt-inactive-historical-references-100light.jpg), SHA256 D32B043CDB8755741FA2A398FFF3C9BB63B67FE61427308BBB0A4D2F2BAEB6F5.
- [warehouse-inactive-new-choice-empty-100light.jpg](assets/gui-p5-t05/warehouse-inactive-new-choice-empty-100light.jpg), SHA256 507B487058F50D742770064FEFFDBAC351534AE4E1C5BCEB6AF15E76C9A038A5.
- [employee-inactive-new-choice-empty-100light.jpg](assets/gui-p5-t05/employee-inactive-new-choice-empty-100light.jpg), SHA256 589668FDADD57610A7053F5CC72946C5B435036AE694B62D8D67A5719BFD11B8.
- [product-inactive-new-choice-empty-100light.jpg](assets/gui-p5-t05/product-inactive-new-choice-empty-100light.jpg), SHA256 3135AE70BE32F6C984A19AE83A8098B5F47974D7DB47B67A7249D9C88ECF8147.
- [reactivated-three-new-selections-100light.jpg](assets/gui-p5-t05/reactivated-three-new-selections-100light.jpg), SHA256 87424B1BF2966D5EFE2C00878DFA985D991F36EA53CD8CC6392C2A57752C8E19.

### Restart / persisted recovery readback — confirmation pending

Chapter2 launched10:53:20+07 (30minute deadline11:23:20). Actual runtime-only startup schema preflight admitted Login; Owner confirmed T05Operator after real Login. Fresh shell workspace empty, no former actor/editor state. GUI reopened T05-FIX-FAULT: Posted/token2031, NoteT05 uncertain, threeChanged reference names, same1@4line/Total4; editor/Save/Post readonly. Actual BalanceProduct1query displays onequantity27.0000row/VisibleTotal27.0000 with ChangedWarehouse/Productnames. Restricted continuation-after-restart-readback matches33receipts/59lines,53ledger/27balancekeys/basequantity27,0full-key/line/document mismatches; recoveredPostedactorP5.T05.OPERATOR/token2031 unchanged. No new writes or fault reinstallation. F-02/L-01 execution complete pending human observations; finalcohortcleanup remains O-02pending. A consolidated humanquestion requests confirmation for P-03/P-04/P-05/P-07/F-01/F-02/S-04/K-01/L-01; accepted count stays16/45 untilreply. Supplemental18UXcells stillNOTRUN.

- [restart-recovery-posted-readonly-100light.jpg](assets/gui-p5-t05/restart-recovery-posted-readonly-100light.jpg), SHA256 CBDD4F8B4F27B271CDBFA8CF3D19E8EDB3ABB049475DE895934A12FBF5295712.
- [restart-balance27-readback-100light.jpg](assets/gui-p5-t05/restart-balance27-readback-100light.jpg), SHA256 3950B9C9CB41C3C929D6BE87B5CC87EC62A586D2099CCD9A880DC79F24B7CAFE.

### Consolidated baseline observations accepted —2026-10-20

Owner explicitly confirmed directly observing DirectDeny, NoRead, RestrictedStock, revocation, timeout/retry, paging, catalogchanges and restart. P-03/P-04/P-05/P-07/F-01/F-02/S-04/K-01/L-01 nowPASS within their documented scope/fixture/cache/UI qualifications. Baseline25/27PASS; O-01finalindependentreconciliation/O-02finalcleanup pending. Supplemental0/18; total25/45. This is observation acceptance, not finalT05sign-off or overallP5/P6acceptance. Continueactual100%DarkUX on unchangedcandidate.

###100% Dark supplemental UX — executed, confirmation pending

Actual theme toggle/100%desktop; shellDark and nativeReceipteditors/gridsDark inspected, Stockcontrolbody retains current lighter rendering. No claim of universalDark recoloring. Balancewindowed/maximizedlayout, nonexistentProduct999empty/total0, restoredProduct1/Refreshquantity27; SearchTabmovescarettoWarehouse andShiftTabback. StockUIA focused_element sometimes reports priorgridcell while visiblecaret moves ineditors; observationsusefreshrenders rather than stalefocusmetadata. Card1/1datecheckbox/keyboardyearchangeFrom2027>To2026safevalidation/emptydelta0; uncheckboth+Refreshrestores25rowspage1/delta25; windowed/maximizedbounds/controlsinspected, WarehouseTabProduct/ShiftTabback. Globaloldernotificationretained, samequalifiedbehavior.

ReceiptPostedreadonly/windowedthenmaximized, NewSaveemptyrequiredicons/saferequestinvalid/disabledPost; nosavednewreceipt. ActualProductQuantityTab/ShiftTabfocusreturnsverified. ThreepopupsexactT05codesquery1/1,Down/Enterselect,F4reopen,Escclose; popupboundswithinworkareaandselectionfocusreturns. Longnamestruncatewithactualdisplay/accessibilityqualified, notfulltextalwaysvisible. Warehouse set_value tool timedout; freshnativequeryverifiedinputand1/1resultbeforecontinuing,noactionblindlyresent. This is a tooltimingincident, noapplicationhangclaim. NoSave/PostafterUXselection.

Normalclose/discard11:08:24/exit0/CleanupVerified. ExactruntimeDMVrequests0/WAITFOR0; restricted100dark-drainedretains33receipts59lines53ledger27balancekeys/base27,0full-key/line/docmismatches. Current169DBretainedforDPItransition, finaldisposalnotclaimed. All6DarkUXcellsawaitOwnerconfirmation; accepted25/45unchanged. Human125%/1920x1080switchrequestedonlyafterdrain; freshnativeproberequiredbeforelaunch.

- [ux-balance-windowed-100dark.jpg](assets/gui-p5-t05/ux-balance-windowed-100dark.jpg), SHA256 D42A1D6689195B909155C542827A74969F1FF79E430C1A5933EC92848E574385.
- [ux-balance-recovered-100dark.jpg](assets/gui-p5-t05/ux-balance-recovered-100dark.jpg), SHA256 13FD3C9CA3048B98FB066CF756E2CA3304BC7EE1A1A9F1663E9077915CF30714.
- [ux-card-invalid-date-100dark.jpg](assets/gui-p5-t05/ux-card-invalid-date-100dark.jpg), SHA256 D5C69FC47CAB75AD3D00564156B5A3453EE2E3A34C26BD1153A27E97418DD6E0.
- [ux-card-recovered-100dark.jpg](assets/gui-p5-t05/ux-card-recovered-100dark.jpg), SHA256 D7F3156EDAE471716F0F4618C2049B9BE5E0D9EEBFF62CE35A30F45591C14E26.
- [ux-card-windowed-100dark.jpg](assets/gui-p5-t05/ux-card-windowed-100dark.jpg), SHA256 A9A7E429E776FEA3EAD07A840FDC73315EFE8BFBB1375CBE7841246C0626D9D7.
- [ux-receipt-posted-windowed-100dark.jpg](assets/gui-p5-t05/ux-receipt-posted-windowed-100dark.jpg), SHA256 79067B732CA17BC91D4D67281C7BDAD08468107AA8EF906530A6E69F59844BCA.
- [ux-receipt-required-100dark.jpg](assets/gui-p5-t05/ux-receipt-required-100dark.jpg), SHA256 2710F25D3276C56D82690B23EF95EECD0A36BFE1E0D2E5918CC541A0BA4CDFF5.
- [ux-warehouse-popup-100dark.jpg](assets/gui-p5-t05/ux-warehouse-popup-100dark.jpg), SHA256 D404FB6D47B501741B19ED16E4537D19416EEECA125949F54832A339A0BF56AA.
- [ux-employee-popup-100dark.jpg](assets/gui-p5-t05/ux-employee-popup-100dark.jpg), SHA256 EE8382D13B48BD4869FF10719F19D8BD04B23B55A4806B40731FBE92786B164A.
- [ux-product-popup-100dark.jpg](assets/gui-p5-t05/ux-product-popup-100dark.jpg), SHA256 1350E46BDEB9615FD4841260F69C7EDFCEE352744C2E2370B14A89F0BB446C64.

###125% admission —2026-10-20

After chapter2normalclose/drain, Owner switched WindowsScale125% keeping1920x1080. Fresh x86nativeprobe returnsPrimaryScale125/HRESULT0; CIMphysical1920x1080. SamepositivelyownedretainedcohortsupervisorReady; chapter3launchrequested11:10:37+07, existing30mincap and unchanged133-filemanifest/separationverification. HumanOperatorLogin/Light requested before actual125UX. No125PASSyet;100DarkOwnerconfirmationstillpending. Accepted25/45; T05/P5/P6OPEN.

Owner explicitly confirmed100%DarkUX as observed: all6supplemental100DarkcellsPASS, total31/45. Actual125%humanOperator/Light confirmed;125Light/DarkUXnotyetaccepted.

###125% Light UX — executed, confirmation pending

Owner confirmed actualOperator/Light after measured125%/1920x1080launch. Balancewindowed27keys/toolbarwrap andmaximizedfilter999empty/total0,Product1restoreRefreshquantity27; keyboardnavigation exercised. Card1/1rows/delta25, Fromcheckboxempty0, actualkeyboardyearUp2027withTo2026safeinvalid/clearsrowsdelta0, uncheckboth+Refreshcorrectrows/delta25; windowed/maximizedlayoutinspected. TemporaryOSsnappreviewduringrestorewasnotpublishedasevidence. StockUIA focusmetadatacaveatretained; datekeyboardverifiedbyactualvalue/screenshot.

ReceiptactualgridEnteropensPostedFIX-FAULT/token2031, readonlycontrols/windowed; maximizeNewSaveemptyrequiredvalidation. Each3popupsexactcode1/1,DownEnterselect,F4reopenEscclose,insideworkarea; ProductQuantityTab/ShiftTabverified. Longnamestruncate/currentwindowedlookuparrowrenderqualificationretained; nofulltextvisibilityclaim. Warehousetoolset_valueagainreportedtimeout; freshsnapshotshowedappliedcode/1result, noresend. NoSave/Postafterrequirednegativecase/selection; nolastingnewDraft.

Chapter3normalclose11:20:02/exit0/CleanupVerified, exactruntimeRequests0/WAITFOR0; restricted125light-drainedretains33receipts59lines53ledger27keys/base27/0mismatches. All6cellsawaitOwnerconfirmation. Chapter4launchrequested11:20:10before55minadmissionexpiry, sameownedDB; actualDarkLogin/humanconfirmationpending. Accepted31/45(25baseline+6Dark100UX),0accepted125cells. Finalcleanup/O01O02/sign-offpending.

- [ux-balance-windowed-125light.jpg](assets/gui-p5-t05/ux-balance-windowed-125light.jpg), SHA256 F63E0638F233AFBFD2FBE2EF3F0B8CE59A2D30232C91B2FC582EA9BB6B7DE7AE.
- [ux-balance-recovered-125light.jpg](assets/gui-p5-t05/ux-balance-recovered-125light.jpg), SHA256 CB757000E5B8A8D71DE821AC06D7B6D44C50A247FA64536C038DB9409A3E1C7C.
- [ux-card-invalid-date-125light.jpg](assets/gui-p5-t05/ux-card-invalid-date-125light.jpg), SHA256 1CCFFAE0245C6799000DBE15E6BAE2D8A8039834AFEB1D83E44496D17C1E77E2.
- [ux-card-recovered-125light.jpg](assets/gui-p5-t05/ux-card-recovered-125light.jpg), SHA256 DFBE3CA33468D0F932A05FF76C3FC2E5376FC4F8420789C0CD3666CB4569C5B0.
- [ux-receipt-posted-windowed-125light.jpg](assets/gui-p5-t05/ux-receipt-posted-windowed-125light.jpg), SHA256 4EFA466E8632C29CD52FBC35CC202BE0E92A6248B65C3486C9A26B26552ACB1D.
- [ux-receipt-required-125light.jpg](assets/gui-p5-t05/ux-receipt-required-125light.jpg), SHA256 09ABBA68180E3AFC1BA3288007F2D498902274E098621687757A958A3776A6FC.
- [ux-warehouse-popup-125light.jpg](assets/gui-p5-t05/ux-warehouse-popup-125light.jpg), SHA256 EB16CEAB720F2251429C5640508204F20DA361C0CD033615843D0A0785C86CD3.
- [ux-employee-popup-125light.jpg](assets/gui-p5-t05/ux-employee-popup-125light.jpg), SHA256 9FF38C43E3C1518A82B8472E6754DFDAACE6BD118025A72842267F6F9D43BC1F.
- [ux-product-popup-125light.jpg](assets/gui-p5-t05/ux-product-popup-125light.jpg), SHA256 80A143C9683547D9C2659F412BABF02E440AAEA8572863804F38E6365BB971B1.

###125% Dark UX and final resource closure —2026-10-20

Owner confirmed actual T05Operator/Dark Login in chapter4 (launch11:20:13,30-minute cap11:50:13). Receipt loading/disabled controls and Posted token2031 readonly state inspected in normal window; maximized New/Saveempty produced safe invalid request/required markers and disabledPost. Each Warehouse/Employee/Product exactfilter returned1/1; Enterselected, F4reopened, Escclosed, selectionfocus returned. Down exercised Employee/Product; Warehouse selection and F4/Esc verified. ProductQuantityTab/ShiftTab exercised with visible caret/selection. Popupbounds remain withinworkarea; known long-name truncation retained. FirstWarehouse set_value reported tooltimeout; initial snapshot still looked blank, later native focus/query showed applied code/1result, no repeated filter submission. No producthang or all-text-visible claim.

Balance normal/maximized toolbar/grid/totals inspected; Product999 empties rows/total0, Product1 restores27.0000, TabfocusRefresh/Enterquery succeeds. Card1/1 shows25page1rows/delta25, keyboardFromyear2027 with checkedTo2026 safely rejects/clearsrowsdelta0; uncheckboth/explicitRefresh recovers25rows/delta25. Normal window wraps toolbar without hiding commands; Tab movesWarehouse→Product. Stockbody retains its existing lighter rendering withinDark shell; oldglobalnotification remains after successfulrefresh. Temporary OSsnapoverlay was not used as app evidence. NoSave/Post of temporaryUXDraft.

Final restricted snapshot final-ux-125dark verifies33receipts (27Posted/6Draft),59lines,53ledger/27balancekeys, baseProduct27.0000. Full-key mismatches0; InvalidPostedLineMovements0; UnexpectedLedgerMovements0. All15oracle tables are byte-equal after normalized JSON serialization to continuation-125light-drained: sixDraft versions/inputs, recoveredPosted token2031/NoteT05 uncertain/actorP5.T05.OPERATOR, catalogChanged/Active flags,63assignments and disabled/revokedInactive remain unchanged. The26setup-only pagingPostedreceipts/52movements remain clearly separate from oneactual continuationPost/1movement; originalGUI-created/Post permission/conflict evidence belongs to disposedcohort168. Exact faultrecord remainsRemoved; no reinstallation.

Chapter4 normalclose/discard11:33:51.954+07, exit0/CleanupVerified. Root admission had expired while waiting current boundedchild; final separation/reconciliation/disposal completed11:33:52.996, ATTENDED_SESSION_CLEANUP=PASS/rootexit0. Fresh exactinstance-aware census:169/169GUI allocationledgersDropped; exactlatest database0/principals0/ownedprocess0. All3T05 allocations/9principals safelydisposed (including initialsetupfailure); no prefixdrop or recoverydatabase sharing. O-01/O-02 PASS. Fresh installed133/133files match frozenmanifest EDB0FE41DAB59FE7F4DEE7F2673815017C056C744E70E9D2E896D50075146101. No new automatedinvocations or product/testbinarychanges.

Both125%UXgroups executed; explicit consolidated Ownerobservation confirmation requested and stillpending. Current33/45PASS=27baseline+6UX100Dark. FinalT05signature remainsPENDING; T05/REMAIN-P5-GUI-02/T06/overallP5/P6OPEN. Do not infer UXacceptance from Loginconfirmation.
- [ux-balance-recovered-125dark.jpg](assets/gui-p5-t05/ux-balance-recovered-125dark.jpg), SHA256 C9730A3F318AB021BF0F18CCF9898266B303EE3B0F0073700103D4E9558D6E53.
- [ux-balance-windowed-125dark.jpg](assets/gui-p5-t05/ux-balance-windowed-125dark.jpg), SHA256 E2DE62A30FEE01782274469B62CA0C29770983C161B994E4767F0FEA1AC2F360.
- [ux-card-invalid-date-125dark.jpg](assets/gui-p5-t05/ux-card-invalid-date-125dark.jpg), SHA256 42916B09BD9AC5EB18F6CD8EF98A37C449FDE60769229ADBFB39114266069B04.
- [ux-card-recovered-125dark.jpg](assets/gui-p5-t05/ux-card-recovered-125dark.jpg), SHA256 5856B5C5B5F77883691DF80ACEAF32D51390AED29832CA731E28375D89D613EA.
- [ux-card-windowed-125dark.jpg](assets/gui-p5-t05/ux-card-windowed-125dark.jpg), SHA256 D8E5678B90DB2C0CE3794D778F92BBC4821DF65A2FCEF69B250D735F8D423F03.
- [ux-employee-popup-125dark.jpg](assets/gui-p5-t05/ux-employee-popup-125dark.jpg), SHA256 B470556BE6D81034791F3D71C8EFBB2DAE7683FB023995A0B9C03F8746328A5C.
- [ux-product-popup-125dark.jpg](assets/gui-p5-t05/ux-product-popup-125dark.jpg), SHA256 B1025F058E5F9849F211657296C259DC7B5C54D788E30388C3AB4E7180DF5864.
- [ux-receipt-busy-windowed-125dark.jpg](assets/gui-p5-t05/ux-receipt-busy-windowed-125dark.jpg), SHA256 50DFEE2E49D197C3F4FC764FF46323559AF8B101D36FFA0B146892B3A0AA6F4B.
- [ux-receipt-posted-windowed-125dark.jpg](assets/gui-p5-t05/ux-receipt-posted-windowed-125dark.jpg), SHA256 8D13C9F46846DCBC868D87DFBECBE7E94D4F2150362A636CAC1C6F87901AD771.
- [ux-receipt-required-125dark.jpg](assets/gui-p5-t05/ux-receipt-required-125dark.jpg), SHA256 48C76FD0738BDBAF8495BFDB342095B3B102262CA2FA8E5950ED7E7695DCFD2D.
- [ux-warehouse-popup-125dark.jpg](assets/gui-p5-t05/ux-warehouse-popup-125dark.jpg), SHA256 3759CD7BA0233EC82AACACBF2C8E3735F9DB26B6371076F49AE7B7240F54E47C.


### Final independent review —2026-10-20

Fresh read-only reviewer checked the T05 branch scope cdfa4b7..8c0cf5d: no material candidate/cohort/oracle/cleanup finding. Verified80unique publicimages against their recorded hashes (inline/table references include duplicates),15-table finalsnapshot equivalence and chapter4/root lifecycle. Corrected stale current summary in GUI_P5_VERIFICATION and12executed-but-NOTRUN125cells. Warehouse125DarkDown remains NOT TESTED; no inference from anotherpopup or general Ownerconfirmation. Filter/Enter/F4/Esc were observed. This narrow observation limit is disclosed for Ownerdecision, not a productfailure or an automatedpass. No productfix/test rerun required for documentation corrections; no secondreview dispatched. T05 stillOPEN pending explicit125observations and finalOwneracceptance.

### Owner125% observations confirmed / proposed final acceptance —2026-10-20

Owner explicitly confirmed: “xác nhận hai lượt125% Light/Dark đúng như đã quan sát”. This accepts the observed behavior in both contexts; it does not turn the untested Warehouse125DarkDown substep into a test result. Updated canonical ledger:27baselinePASS +17supplementalPASS =44PASS;1supplementalPARTIAL with onlyDown NOT TESTED. No productfailure identified. Other observed keyboard paths in that cell (filter/Enter/F4/Esc/focus/bounds) are confirmed.

Concrete acceptance scope for Owner: installed net48/x86 frozen d5207b2 on actual Windows11build26200.9457, SQL2017Developer14.0.2130.4/schema001–009; full100Light functional baseline and100Dark/125Light/Dark supplemental UX; exact original/continuation fixture provenance and preserved failures/qualifications above. Final restricted reconciliation0mismatches, normal process drain, all3T05allocations/9principals disposed,169/169historicalGUIledgersDropped and exactlatest database/principal/process0. Review completed, no material runtime/evidence finding. No new automated pass, T06/fullGUI gate or P6 certification claimed.

Final Owner signature and explicit disposition of the single untestedDown substep remain pending. Proposed decision: accept T05 within the documented observations and accept that narrow observation limit; retain Down as NOT TESTED rather than relabeling itPASS. Only after that explicit decision may T05/REMAIN-P5-GUI-02 close. T06/overallP5/P6 remainOPEN.

### Final T05 Owner sign-off — 2026-10-20 11:45 +07:00

Project Owner explicitly accepted evidence commit5ed7bc6: “Nghiệm thu T05 theo biên bản, chấp nhận giới hạn Down đã ghi”. GUI-P5-T05 COMPLETE / REMAIN-P5-GUI-02 PASS within the documented, Owner-accepted scope. Outcome remains44PASS and1PARTIAL observation cell; the Warehouse125DarkDown substep remains NOT TESTED with an explicit Owner-accepted exception, never converted into a testPASS. The other observed paths and both125Light/Dark groups are confirmed.

Frozen d5207b2 candidate/cohort provenance,80publicimages, independent oracle/reconciliation and final owned cleanup remain as evidenced above. All3T05allocations/9principals disposed,169/169GUIledgersDropped, exactlatest database/principal/process0. This signature changes documentation only; no GUI/SQL/test execution or new automatedpass is claimed. T06, overallP5, REL/P6 G1–G5 and productionreadiness remainOPEN. Historical pending checkpoints above are superseded by this signed decision.

