# SAL-T06 — reviewed Local/session/ViewModels checkpoint

Date: 2026-10-22. Development `codex/sal-direct-sales`; planning `v1a-pro`. Source/test commit `5fbd6f5c3022ccb05c770648e4967747d73602fa`, direct parent/review BASE `b1683af8deb1dc61ea720a45f73bf7a3f562a2ad`, product predecessor `f9149a6a7ca35d26163fac0745101774246891a7`. Planning baseline remains `35ba2e8a3f37fb07095701eb94f4b56f7941c491`. T06 is COMPLETE in its bounded Local/API/VM scope; [execution index](../../plans/README.md) records T07 IN PROGRESS, T08 NOT STARTED. Native UI/frozen package/Login/attended/Owner and operational release acceptance remain open. This development checkpoint is not deployable.

## Approved candidate and source boundary

Candidate1 had three Important findings despite passing its selected tests. Candidate2 closes R1–R3 and the approved Sales session-binding refinement; same combined reviewer gave final APPROVED with no remaining task Critical/Important/Minor finding. Final review SHA256 `DC005EB50CD9A19DC3457CBDF4FE1D4DAA5A2EAA3EA2C5B779BE1C7FBF5B5F6E`.

Exact16-path FILEBYTE manifest SHA256 `9A96B3673BB0D7F3626B632070C873F23B27577475851BF394C0B9E75B01C2F1`; root verified committed blobs equal approved bytes after CRLF normalization. Source includes feature API, list/editor/lineVM, preview/buffer interfaces, Local API/preview adapters and DI/dispatcher, provider-free Application preview facade, Session Local/editor/list/review/test-support and Application preview tests. Candidate1→2 changes eight paths (seven existing plus new review regression file); eight original paths retain bytes. Vendor stays `2bd7874f7e80b22ef181763749c2a2938ab7f16e`. No schema/package/project-reference/permission-key/vendor change.

All five Sales Local operations preserve typed responses/errors and captured actor/generation. Sales-only final fencing maps stale success/error to correlated Unauthorized/Auth.SessionChanged without Data, preserves current genuine Unauthorized before its own invalidation and fences inactive/unavailable branches. Ordinary SendAsync/default clients retain prior semantics.

Each VM captures its original session version once and binds its API once through ForSession. Real LocalDesktopSession.TryCaptureActor supplies a locked immutable actor/generation pair; dispatcher compares the real generation with the expectation before account/permission/handler admission. Expected generation never constructs an actor. Replacement before capture rejects; replacement after capture cannot substitute a replacement actor and existing current/final fences remain authoritative. Rebinding to another generation rejects. This is owner-bound protected-service admission, not zero API method attempts or a UI-thread serialization guarantee. Deterministic continuation regressions show attempted Get2/admittedGet1 and attemptedList3/admittedList2; all five actual bound Local operations reject replacement before protected services.

List VM separates pending/applied filters and publishes Items/selection/cursors atomically before notifications. First/Refresh/filter/PageSize reset cursors; ordinary failures retain the applied view, authorization loss scrubs it, stale/canceled/session/operation callbacks cannot publish. Empty later pages permit one bounded first-page recovery per criteria generation; no manufactured cursor.

Editor/line VMs retain stable independent row objects/Guid keys across duplicate new/persisted lines, Save/reorder/reload. Manual price/gifts/exact revenue preview share Desktop interface→Local adapter→provider-free Application facade→existing Domain math. Product change clears stale Unit/display while preserving manual price; server Save owns persisted Unit. Posted cost/profit remains authoritative detail data. Preview is not a copied arithmetic implementation or native grid proof.

Common session/Read/status/busy/recovery guards apply to command use. Edit/Save requires Write; Post independently requires Post, allowing Read+Post/noWrite on a clean saved Draft while edits/Save stay denied. Buffer admission precedes dirty/validation decisions; frozen work dispatch occurs in queued admission with ownership rechecks. Actual native editors/raw text/lookup buffers and Back control wiring remain T07.

Unknown Create retains draft/rows/recovery lock with no duplicate retry; explicit reviewed-ID Get resolves it. Ambiguous/conflicted Post never replays; failed/mismatched reload stays locked. Known Post applies status/newVersion before detail and cannot downgrade to Draft. Denied reload clears protected public presentation but keeps private original SaleId/document/session recovery context; explicit restored Read can fetch only that same ID. Successful authoritative detail alone restores costs/unlocks. Arbitrary Load/New/Create resolution cannot bypass Post recovery. Session replacement/disposal clears private ownership and cancels/invalidates old rows/work.

## Actual final evidence

Windows11 Home Single Language10.0.26200/build26200, SDK8.0.425, net48 Release, VSTest17.11.1 x64. Root verified exact discovery/test-name sets, frozen source and pre/post/live/raw-archive binaries; reviewer independently read final TRX counters/hashes. Counts are per invocation, not additive unique totals. All final scopes have zero nonpass outcomes.

| Evidence | Discovered / executed / PASS | TRX SHA256 | Test DLL SHA256 |
|---|---|---|---|
| Candidate2 complete Desktop.Session | 169 / 169 / 169 | 6490AC291B39BB679C6BB23108F8167A74FB88FB6EA87DEA1393057E2E17AE13 | 23415339942F6C04C35E36B13E80777D86785706795F31C88E7961D018BC9010 |
| Candidate2 complete Architecture | 13 / 13 / 13 | ABDF0691AD3931000307FFBB63E788696A5EC8F1BA7EC4CDF63006A723243AED | 7A381FEEF2538F4D13C72EAA60FFFF3BEFC22A3A4C77BDF04FFE286665FCA411 |
| Reused candidate1 focused Application preview/math — NOT rerun | 14 / 14 / 14 | 8DD1BC8FBC2EF4526C2551BFE4A91C6D55F08A171E49B19E9FD2D16789742EEE | 508B522B3B820DB8319060574C5346B5E362803A9C3FED889A65BDEBE2CD4A5D |

Session169 comprises20 LocalSales,24 editor,11 list,6 review regressions (61 Sales) plus108 existing. Candidate2 Session/Architecture builds show zero warnings/errors. Application14 is one preview facade Fact plus13 math rows, selector `FullyQualifiedName~SalesReceiptPreviewTests|FullyQualifiedName~SalesValuationMathTests`. Root verified its unchanged relevant Application/Domain/Contracts/project/preview-test graph and all31 DLLs equal candidate1 pre/post/archive/current bytes. **No new Application run** is asserted.

| Binary evidence | Raw DLL entries | Before-manifest file SHA256 |
|---|---:|---|
| Candidate2 Session pre/post/live/archive | 35 | A1641694E54B74B8367A0BD6C724293494F498EF94D192CADFD19BDA335C2FD0 |
| Candidate2 Architecture pre/post/live/archive | 64 | C38D94EAC68FBEF67556A696407DA9CDE23A4E236A24799FF2B3546DF1FA0F7A |
| Reused candidate1 Application pre/post/archive, unchanged graph | 31 | FD1B1291CB72564E3BCFFCBB2EBF2F7992AEEDB20F0A9780B77B98F891401449 |

These are full captured test-output DLL sets, not MyDmsVn-only counts or package manifests. Six fix-development runs each have35 matched pre/post/archive DLLs and source manifests; historical source manifests identify byte hashes without claiming every intermediate source body was separately archived. Complete candidate1 body archive and frozen candidate2 source are retained locally.

### Invocation boundary

Complete Session and Architecture were built/discovered, then executed with ordinary `dotnet test -c Release -f net48 --no-build --no-restore` and distinct TRX. Discovery used `--list-tests` for the same complete project scope. Projects are `tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj` and `tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj`. Existing candidate1 Application14 used the exact selector above in `tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj`; it was not rerun after repair. The exact full build invocation/restore flags were NOT CAPTURED in the artifacts available for this checkpoint; no flags are inferred from build output and no rerun was performed to fill this gap. The command forms above and actual scope/log identities are documented, not a reproduction of the missing full build CLI. Local build/discovery/log/source/binary archive and actual TRX records remain retained; raw proprietary artifacts are not committed.

This documentation checkpoint executes no build/test/SQL/GUI. Fake credential/account/permission services plus actual Local plumbing prove deterministic session boundaries, not actual Login/runtime SQL identity/native GUI/package/attended evidence. Root reports no process left running; no SQL resource/cleanup proof is invented from this task.

## Candidate1, original failures and repaired review boundaries

Candidate1 remains a distinct real complete Session157/157, Application14/14 and Architecture13/13 pass; it did not cover the three review findings. Candidate1 source manifest `1F457542A258A29D32A7B407C9C2E1A48E5321B9BCA0EE732B55AB4945F31E85`, Session TRX `5D24CDC0DFCBE2E8395A2D67181AE10E1C8CA46DCF7F2482B503FDC8AE3CBB1A`, Architecture TRX `F79BA626A6CCDD30D9BF3902BC8A3AD2067537BED4EFC171654ADB99628FCAEB`. Candidate2 full169 is a separate execution, not old157 plus focused12 relabeled.

- Local RED3=1PASS/2 intendedFAIL retained stale result/session-error failures. Initial Local/VM runs combined build/test: source prehash and postrun DLL/archive retained, no preexecution DLL manifest.
- VM RED27=5PASS/22FAIL includes21 missing behaviors plus one separately labeled incorrect arithmetic oracle. Correcting that expected constant is not product RED proof.
- Dev1 27=26PASS/1 observerFAIL assumed nonempty state during Dispose; source was not separately frozen, postrun DLL manifest retained.
- Dev2 first compiler failure used unsupported PermissionSnapshot argument; actual38=30PASS/8FAIL exposed incorrect SalesReceipts.* expected literals and account flags enabled before fake fixture Login. Correct Sales.Receipts.* fixture keys/after-login flags repaired tests without changing product keys or proving actual Login. Dev3 38PASS has matched hashes but no separate rawDLL archive before next build.
- Cancellation RED2FAIL deliberately ignored cancellation in transport/queuedUI; dev4 48PASS after callback fences and broader regressions.
- Unit RED1FAIL specifically retained old Unit when ProductId changed; manual price was not the failing assertion. Original TRX SHA256 `125EE737975FA41B2C57B35A1B470AABFDA035FBA0401E6CABFDA7CCC1C63B0C`, source manifest `C55BAF2DE5F3108C00970AE9859E8293E2A5E14028F2E59C1378D6060991BEDB`, matched pre/post DLL manifest `4630672FC1D10B6EF89AFCC24D459ACCB1DBAAA784D202339B1913D1A6DF2853`; raw archive copied before next build. Dev2/red-cancel/dev4/red-unit raw DLL archives remain retained; missing dev1/dev3/initial provenance is not reconstructed from final artifacts.

| Fix round1 invocation | Actual rows / PASS / intendedFAIL | TRX SHA256 |
|---|---|---|
| red13 (R1/R3) | 4 / 1 / 3 | E0EE1CA67DCC448FEB87E9269413EBC419C07B6E053125751C5B2076FB21C60E |
| red2 (owner admission) | 4 / 0 / 4 | 5CE636119A5701630FB3FF0203AC9EF4237EC529296673E6A8E618D1C69ADFAF |
| green1 | 1 / 1 / 0 | 477EB40E5C86B71C18F825B3A2AA7BCEB4EE6414648D4CA7A8968608956B0F2C |
| green3 | 3 / 3 / 0 | D9FC2140EC3F406BCC4BBB46221C092DDDA10155A2EFA4A98F1EE68D3275CB50 |
| green2 | 4 / 4 / 0 | 152DCD8F878AF2895D8DD878752A0D4C0B9DC0723E8D9E79E491B25C4F9617FF |
| scoped regression | 12 / 12 / 0 | 2E1B24D5927CD55F014CDBBA814D52970704475C58738CEA0E41BEA14F71DA69 |

No fix-round setup/compile/oracle failure is claimed as intended RED. Scoped source differs from final frozen candidate2 only by normalization/using order; final rebuild owns frozen execution proof. R1 independent Post, R2 protected-service owner admission and R3 private authorized recovery are closed by source review plus targeted tests, not retroactively covered by candidate1 passes.

## Bounded acceptance and remaining limits

A41–A43 are bounded deterministic Local/VM PASS for this candidate/date in the [ledger](../../DIRECT_SALES_ACCEPTANCE.md). A44 full T06/T07 remains open: only VM/buffer interface/recovery portion is verified. A20/A24/A32 gain bounded Local/session/uncertainty portions without automatic full multi-owner closure. Native raw buffers/keyboard/lookups/Back/accessibility/layout, T08 frozen SQL/UI/package/actual Login/attended and explicit Owner disposition remain open. SessionChanged/cancellation is not proof a mutation rolled back and never permits replay.

T00–T05 public evidence and T01 migration runbook remain immutable, including T04/T05 failure/provenance gaps. T01 R3 generic bootstrap diagnostics stays deferred Minor; exact9 SQL-login/trusted-maintenance, inherited collation, Local direct-SQL trust and mixed-client/deployment limits persist. Historical BCF/BCI/GUI-P5 acceptance does not certify Sales native obligations. Full SAL/Owner/package/operational release acceptance is not established.
