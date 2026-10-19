# GUI-P5-T04 — Attended Catalog/Lookup walkthrough

**Status: PREPARED / ATTENDED EXECUTION NOT RUN. `REMAIN-P5-GUI-01` remains OPEN.**

**Plan:** [GUI-P5-T04](plans/20261019-001-full-winforms-gui-p5-acceptance.md#gui-p5-t04--remain-p5-gui-01-attended-cataloglookup-sign-off). **Prepared:** 2026-10-19 (Asia/Bangkok), Codex; documentation base `f33a959`. This is an operator worksheet for actual installed Login-first use. [T03 automated results](GUI_P5_VERIFICATION.md#final-frozen-candidate--gui-p5-t03) remain separate and are not counted as attended passes.

## Candidate and preparation evidence

| Item | Verified preparation / limit |
|---|---|
| Executable/package source | `02eff225d41ff564769bedbb0ca4420d27233a67`, clean T03 candidate3, version `1.0.0-guip5-t03`, schema001–009 |
| Manifest SHA-256 | `4C28486221AFB81243F94DDBCC1A8482BAAE43E6E92BFD6ABAA4E11096BD9F51` |
| Desktop EXE SHA-256 | `92D400CD583689D82C99B85EDBB7D4B969E1B68804BC938AA3B6F8C9B91C38D6` |
| WinForms DLL SHA-256 | `A6D1531867B26E51B18137277EBD2445FBD9BCD00D1BAD16878FF6C7DE2548AB` |
| Private per-user installation | `%LocalAppData%/MyDmsVn/Acceptance/1.0.0-guip5-t03-02eff22/desktop-net48-win-x86`; copied all133 Desktop manifest files and verified every length/hash. No admin migrator copied. Installation root has protected DACL for current account, SYSTEM and Administrators |
| Installation boundary | No existing directory overwritten; no shared `%ProgramData%` configuration, Start menu or desktop shortcut changed. Runtime has not started. This is a private versioned-directory test install on the developer machine, not P6 clean-machine/machine-wide/limited-user certification |
| Actual Windows | Windows11 Home Single Language25H2, build26200.9457, x64, interactive session38; not a broader OS support claim |
| Installed v4 runtime | Registry Version4.8.09221, Release533509. Target remains net48/x86; no claim of an isolated4.8-only image |
| Actual display at preparation | Native32-bit metadata: primary scale100%, HRESULT0; active physical1920×1080. Re-measure for each attended session; prior T03 ended at125%, which is not current-state evidence |
| SQL | Designated owner-managed non-production instance approved in the topology/session. Actual T04 connection/edition/schema/principals **NOT TESTED**; no T04 database allocated. T03's SQL2017 Developer14.0.2130.4 is historical, not a T04 runtime observation |
| Human participation | Availability requested; attendee/tester identity, session times, observations and sign-off **pending** |
| Screenshots and oracles | No T04 screenshots or SQL results yet; every walkthrough cell below is **NOT RUN** |

The read-only package verification parsed `release-manifest.json`, selected `desktop-net48-win-x86`, then compared each installed file's byte length and `Get-FileHash -Algorithm SHA256` against the manifest. Current display metadata came from the existing ignored x86 scale probe and CIM/registry; no UI workflow or xUnit invocation is implied. No product/test source or dependency was changed.

## Session procedure and ownership

1. Confirm the human attendee is ready and identify the operator/tester. Start with actual100% Light; then100% Dark,125% Light and125% Dark. Record exact dates/times and actual scale before each. A case may be completed in a later session; unobserved cells remain NOT RUN. Change DPI only after the owned application is closed/drained.150% is outside the approved mandatory matrix.
2. Before first Login, use the existing `GuiSqlEnvironment` in the existing Desktop test owner to allocate a fresh positively owned `MyDmsVn_GuiP5_<GUID>` on the designated instance. Run the packaged admin migrator/bootstrap separately, schema001–009, current limited runtime role and separate read-only oracle. Verify instance/database-ID/create-date/marker and all principal ID/SID/create-date identities. Keep sensitive identities only in ignored local evidence. Do not reuse an old T03 or recovery database.
3. Configure only the installed Desktop's child environment with runtime application-database connection and deliberate protected username. Exclude master provisioning/migration/test/probe credentials and password variables from Desktop. Do not overwrite shared workstation configuration or store secrets in package/source/TRX/screenshots/command lines. Human enters the real application Login through protected input; no test-session injection or smoke bypass.
4. Reuse `GuiOwnedProcess` and its verified job/process-tree cleanup. Its current per-launch maximum is5minutes; split the walkthrough into finite observed chapters and close normally before the deadline, reopening the actual installed application as needed. Keep SQL allocation ownership live only in the supervised setup session. Stop immediately after process/worker/ownership uncertainty; quarantine instead of guessing cleanup. Do not extend budgets silently or start an unbounded Desktop from the EXE/shortcut.
5. Prepare explicit disposable data: at least26 page-probe rows per catalog (actual catalog page size25), distinct prefixes from the operator-created rows, plus three fresh Product/Warehouse/Employee references for an unposted Draft. Seeded page probes do not count as attended UI create/edit evidence. No Post or Stock mutation is needed for T04.
6. Deliberately provision ordinary application actors for all four catalogs: one with Read/Write role grants, one with Read role grants plus direct Write Deny, and one lacking Read. Use only registered Active `Catalog.{Products,Warehouses,Employees,Customers}.{Read,Write}` keys. Record the controlled assignments through the independent oracle. Protected superuser is not an ordinary-Deny test actor. Security setup uses the separate administrative channel, not privileged client bootstrap.
7. Capture only the test application's safe screen content: Login masking (no typed secret exposed), catalog create/edit/validation/active/permission states, paging/search, held receipt lookups, theme and restart. Inspect screenshots for SQL identifiers/connections, credentials, unrelated desktop content and personal data before publishing. Keep raw images local/ignored; sanitized artifacts need relative evidence references, hashes, timestamps, case IDs, scale/theme and attendee observation. Do not mark a screenshot placeholder PASS.
8. After each chapter close and verify the exact owned process tree and pipes drained. Before final DB teardown, finish/dispose all setup/oracle/fault workers and connections, verify schema/runtime separation and no unexpected inventory mutation, then use the existing ownership-checked cleanup. Report allocated/dropped/quarantined counts and final exact-identity SQL/process census. Never drop by prefix. Preserve any original failure alongside cleanup failure.

The session can be operated by the human or by Codex with the human observing. Record which; agent-only observation is not attended sign-off. The attendee must explicitly confirm the observed steps. A general instruction to execute T04 is not a fabricated signature.

## Instructions behind the case ledger

For each catalog use a unique code per configuration, for example `T04-P-L100`, `T04-W-D100`, `T04-E-L125`, `T04-C-D125`; use synthetic names/contact details only. Preserve the selected code and any entered update while checking failures. Source anchors are [CatalogControl](../src/MyDmsVn.Desktop.WinForms/CatalogControl.cs), [CatalogViewModel](../src/MyDmsVn.Desktop.Application/CatalogViewModel.cs), [permission keys](../src/MyDmsVn.Server.Application/Catalog/CatalogPermissionDefinitions.cs), [Product validation](../src/MyDmsVn.Server.Application/Catalog/ProductFeatures.cs), [other validation](../src/MyDmsVn.Server.Application/Catalog/OtherCatalogRequests.cs) and [lookup binding](../src/MyDmsVn.Desktop.WinForms/GoodsReceiptControl.cs).

- Product inputs: Code, Name, Unit; Warehouse: Code, Name, Address; Employee: Code, Name, Phone and optional UserId; Customer: Code, Name, Address, Phone, TaxCode. Use valid synthetic values first, then leave required Code/Name/Unit empty as applicable. Confirm client/server errors are safe and no unintended row persists.
- CRUD here means the shipped create/read/update/active-state workflow. There is no physical Delete action to invent. Deactivate/reactivate must persist after reselect/reopen.
- Search uses300ms debounce and25-row pages. Search change resets to page1. Refresh cancels pending debounce and reloads the current query/page; **do not require Refresh alone to reset page1**, because the current source does not promise that.
- Rapid typing final-result correctness, cancellation and a demonstrated late result are different observations. For the stale-result/cancel row, arrange a bounded owned-database delayed read with exact ownership verification and drained worker cleanup; record that the pending/late condition actually happened. A fast unobserved race or the T02 fake-client test cannot be recorded as an attended late-result pass. No instance-wide shutdown/session kill is authorized.
- Cancel cancels pending list/editor/write work; it is not a promised editor-reset/revert action. Do not retry an ambiguous write until the oracle establishes persisted state. New explicitly clears the editor; Enter on a non-editing result row opens its existing selection path. Check Tab/Shift+Tab order, visible focus, Enter/Space actions, resizing, labels/errors and absence of clipping in every declared theme/scale.
- For lookup cases keep the original Receipt tab/control alive while visiting catalogs. Confirm Product/Warehouse/Employee name changes return through the same process, including offscreen editors. Existing inactive selections stay historical; a fresh Draft cannot commit an inactive new reference. Reactivate and verify availability again, then close/reopen and restart to fetch fresh state. Do not infer cross-process messenger delivery.

## Per-step attended results

`NOT RUN` means no attended execution. Replace a cell only with PASS, FAIL, BLOCKED or NOT RUN plus an observation/evidence reference. A PASS needs actual UI observation and any named SQL check; automated T03 references stay in the separate section below. All four mandatory configurations must be resolved before closure.

| Case | Exact action and expected evidence |100% Light|100% Dark|125% Light|125% Dark|
|---|---|---|---|---|---|
| C-P01 | Products: New/create using the listed fields; Save, reselect/read and oracle-confirm exactly the operator-created row. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-P02 | Products: Edit fields, Save, navigate away/back and reopen; latest values persist without changing the business identity. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-P03 | Products: Blank required fields and duplicate Code; safe field/server errors, entered values retained as designed, no unintended insert/update. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-P04 | Products: Deactivate, reselect/read inactive row, reactivate and reopen; oracle confirms both state transitions. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-P05 | Products: Rapid A→B filter; demonstrate pending/canceled/late read, press Cancel and finish at B; stale A cannot replace B; no modal or leaked busy state. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-P06 | Products: With26+ page rows go Next/Previous, then change search (page1); press Refresh during debounce; final query/count/page stay consistent. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-P07 | Products: Navigate via keyboard, select result with Enter, edit/save via focused controls, resize and inspect focus/labels/errors/grid in this theme/scale. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-P08 | Products: Ordinary Read/Write actor succeeds; Read+direct Write Deny rejects write/active actions; no-Read actor cannot read; oracle confirms assignments and no denied mutation. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-W01 | Warehouses: New/create using the listed fields; Save, reselect/read and oracle-confirm exactly the operator-created row. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-W02 | Warehouses: Edit fields, Save, navigate away/back and reopen; latest values persist without changing the business identity. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-W03 | Warehouses: Blank required fields and duplicate Code; safe field/server errors, entered values retained as designed, no unintended insert/update. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-W04 | Warehouses: Deactivate, reselect/read inactive row, reactivate and reopen; oracle confirms both state transitions. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-W05 | Warehouses: Rapid A→B filter; demonstrate pending/canceled/late read, press Cancel and finish at B; stale A cannot replace B; no modal or leaked busy state. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-W06 | Warehouses: With26+ page rows go Next/Previous, then change search (page1); press Refresh during debounce; final query/count/page stay consistent. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-W07 | Warehouses: Navigate via keyboard, select result with Enter, edit/save via focused controls, resize and inspect focus/labels/errors/grid in this theme/scale. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-W08 | Warehouses: Ordinary Read/Write actor succeeds; Read+direct Write Deny rejects write/active actions; no-Read actor cannot read; oracle confirms assignments and no denied mutation. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-E01 | Employees: New/create using the listed fields; Save, reselect/read and oracle-confirm exactly the operator-created row. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-E02 | Employees: Edit fields, Save, navigate away/back and reopen; latest values persist without changing the business identity. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-E03 | Employees: Blank required fields and duplicate Code; safe field/server errors, entered values retained as designed, no unintended insert/update. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-E04 | Employees: Deactivate, reselect/read inactive row, reactivate and reopen; oracle confirms both state transitions. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-E05 | Employees: Rapid A→B filter; demonstrate pending/canceled/late read, press Cancel and finish at B; stale A cannot replace B; no modal or leaked busy state. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-E06 | Employees: With26+ page rows go Next/Previous, then change search (page1); press Refresh during debounce; final query/count/page stay consistent. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-E07 | Employees: Navigate via keyboard, select result with Enter, edit/save via focused controls, resize and inspect focus/labels/errors/grid in this theme/scale. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-E08 | Employees: Ordinary Read/Write actor succeeds; Read+direct Write Deny rejects write/active actions; no-Read actor cannot read; oracle confirms assignments and no denied mutation. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-C01 | Customers: New/create using the listed fields; Save, reselect/read and oracle-confirm exactly the operator-created row. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-C02 | Customers: Edit fields, Save, navigate away/back and reopen; latest values persist without changing the business identity. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-C03 | Customers: Blank required fields and duplicate Code; safe field/server errors, entered values retained as designed, no unintended insert/update. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-C04 | Customers: Deactivate, reselect/read inactive row, reactivate and reopen; oracle confirms both state transitions. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-C05 | Customers: Rapid A→B filter; demonstrate pending/canceled/late read, press Cancel and finish at B; stale A cannot replace B; no modal or leaked busy state. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-C06 | Customers: With26+ page rows go Next/Previous, then change search (page1); press Refresh during debounce; final query/count/page stay consistent. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-C07 | Customers: Navigate via keyboard, select result with Enter, edit/save via focused controls, resize and inspect focus/labels/errors/grid in this theme/scale. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| C-C08 | Customers: Ordinary Read/Write actor succeeds; Read+direct Write Deny rejects write/active actions; no-Read actor cannot read; oracle confirms assignments and no denied mutation. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| K-P01 | Product: keep original Receipt alive/offscreen; rename in catalog, return and observe fresh same-process display/options. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| K-P02 | Product: deactivate selected reference; original saved Draft retains historical selection; read-only oracle confirms reference and Draft unchanged. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| K-P03 | Product: fresh Draft/new choice rejects inactive reference; pending text cannot silently become a committed historical ID; no unintended saved mutation. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| K-P04 | Product: reactivate and select actively; close/reopen Receipt and restart actual installed Desktop through real Login; fresh names/state persist. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| K-W01 | Warehouse: keep original Receipt alive/offscreen; rename in catalog, return and observe fresh same-process display/options. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| K-W02 | Warehouse: deactivate selected reference; original saved Draft retains historical selection; read-only oracle confirms reference and Draft unchanged. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| K-W03 | Warehouse: fresh Draft/new choice rejects inactive reference; pending text cannot silently become a committed historical ID; no unintended saved mutation. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| K-W04 | Warehouse: reactivate and select actively; close/reopen Receipt and restart actual installed Desktop through real Login; fresh names/state persist. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| K-E01 | Employee: keep original Receipt alive/offscreen; rename in catalog, return and observe fresh same-process display/options. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| K-E02 | Employee: deactivate selected reference; original saved Draft retains historical selection; read-only oracle confirms reference and Draft unchanged. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| K-E03 | Employee: fresh Draft/new choice rejects inactive reference; pending text cannot silently become a committed historical ID; no unintended saved mutation. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| K-E04 | Employee: reactivate and select actively; close/reopen Receipt and restart actual installed Desktop through real Login; fresh names/state persist. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| U-01 | Actual installed Login first: keyboard/focus, password masking, successful protected/ordinary authentication and correct session; record operator and actor. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| U-02 | Read actual application theme action, verify visible Light/Dark rendering and actual scale/resolution for this session; inspect all relevant tabs and errors. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| U-03 | Observe a bounded safe request failure and recovery/Refresh; no raw SQL/connection/secret, no unexpected modal or unintended write; drain any owned fault worker. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| U-04 | Sign out/switch actor and reopen navigation; previous actor editor/results cannot populate the next session; actual restart fetches fresh data. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |
| U-05 | Capture sanitized screenshots and oracle results; close/drain owned GUI, finish workers/connections and record exact ownership cleanup/census. | NOT RUN | NOT RUN | NOT RUN | NOT RUN |

## Session and artifact ledger

| Session/configuration | Start/end +07:00 | Human attendee / operator | Actual actor/permissions | Screenshot/SQL references | Result |
|---|---|---|---|---|---|
|100% Light|Pending|Pending|Pending|None|NOT RUN|
|100% Dark|Pending|Pending|Pending|None|NOT RUN|
|125% Light|Pending|Pending|Pending|None|NOT RUN|
|125% Dark|Pending|Pending|Pending|None|NOT RUN|

Allocation/teardown: **0 T04 databases allocated,0 T04 principals allocated,0 T04 GUI launches** at preparation. No T04 resource was deleted or quarantined. Existing T03 resources remain governed by their own evidence; do not add their152 historical ledgers to a T04 count. No T04 screenshot, oracle or cleanup result exists yet.

## Separate automated references

T02 covers deterministic control/debounce/cancellation/stale/focus/error boundaries; T03 covers real packaged Login, four catalogs and same-process lookup/history/inactive/new-choice/restart with independent SQL. Final T03 source02eff22:100%312/312 PASS;125%311 PASS/1 FAIL in the full run plus the exact focused Light lookup row1/1 PASS after requesting an exclusive desktop. The owner confirmed window switching during the failed full run; this qualification is preserved. These results are not T04 attended executions or a T06 full-matrix pass.

## Closure and handoff

**Current decision: OPEN — attended execution and sign-off pending.** Installation/hash preparation is PASS only for the observed133-file private copy; application execution, actual T04 SQL setup, all196 case/configuration cells, sanitized screenshots, oracles, cleanup and human sign-off are NOT RUN. Missing attendance is an execution prerequisite, not a coverage-inventory defect.

To close: resolve all mandatory case/configuration cells with dated observations, verified artifacts/environment/actors, independent no-mutation checks where specified, sanitized screenshots and positive final cleanup; investigate failures and rerun affected scope on the same or explicitly re-frozen candidate. Record the attendee's exact acceptance scope and outstanding issues. Then, and only then, check T04 and `REMAIN-P5-GUI-01` PASS. T05/T06, the other attended P5 ID and REL/P6 remain OPEN.

**Human sign-off:** PENDING. **Tester/operator:** PENDING for execution (Codex prepared this worksheet). **Date/time:** PENDING. No signature is inferred from preparation or automated results.
