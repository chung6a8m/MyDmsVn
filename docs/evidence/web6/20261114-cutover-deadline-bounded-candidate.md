# WEB6 cutover deadline bounded candidate — 2026-11-14

**Source and non-live verification.** Candidate `web6-20261114-3408cf4b` remains `FailedRetained` and was left untouched; earlier retained and pre-allocation candidates remain unchanged. The current source has no `CandidateVerified` result. Fresh live execution requires final cross-repository review, protected CI and separate explicit Owner authorization. [Approved design](../../superpowers/specs/2026-11-13-web6-cutover-deadline-hierarchy-design.md) · [Implementation plan](../../superpowers/plans/2026-11-13-web6-cutover-deadline-hierarchy.md) · [Runbook](../../development/WEB6_CANDIDATE_RUNBOOK.md#cutover-deadline-and-reset-protocol).

## Source identity

| Checkout or projection | Git commit | Git tree |
| --- | --- | --- |
| Backend reviewed base | `21a7188eaf24cc139c875865568cb23b647c57aa` | `e3265b1da017397aa8f81a69c54f8cf27bdff17e` |
| Initial Task 6 backend | `c99f426d98853dbd3f9a48ff6a769bba615ef8e9` | `460a3f51439ed7c3f5fbdb7a1d3fcae775b615d8` |
| Backend paired provenance correction | `6dddc17eca0bfc7970a81fa21bd3c14dc15a6682` | `c8e6a81578811bfb05573c60d626be6e08a47783` |
| Backend verified source and tests, before this documentation delta | Paired Git process cleanup correction | `dcdc8edda5a630fec191b5766ba78cd76d3490ba` |
| Frontend exact reviewed pin | `eafcc40a13c17dbfe3c546b76d6dbdd1bf360193` | `0ecffdc18a06685e4790c53e9494ae9f340abe19` |
| Frontend reload-restore reviewed pin | `78e24ef00738f0b1d09ff466991802a736c67fec` | `72a8e252f164c131fb72595cfda52c1432c2027c` |
| Frontend exact-wire reviewed pin | `fa032ece3d47af029d5095ae39b21fb43d0f1afc` | `e0cb0950f78d072e73850392b6bf496e574775ae` |
| Frontend current-recovery-region reviewed pin | `e5e083ffd1bf8f70ce49e2e9bad81661e9c6e669` | `928b9878a8b408244433e0eb3ee89130a46f4fd6` |

The only production bootstrap change in this correction is the `ReviewedFrontendSha` pin. The paired-source test additionally binds the exact-wire helper source and evidence. The vendor gitlink remains `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf`.

The real `BootstrapPreflight.PrepareAsync` path ran with synthetic Git/build/SQL-master toolchain inputs and disposable protected files. It accepted `e5e083ffd1bf8f70ce49e2e9bad81661e9c6e669` as `PIN_MATCHED`. It blocked the superseded reviewed pin `fa032ece3d47af029d5095ae39b21fb43d0f1afc` and every earlier row as `BLOCKED_REVIEWED_FRONTEND_PIN`. Only the admitted receipt opened a selector plan in the pin matrix.

The paired test verifies the configured frontend checkout's commit/tree with replacement lookup disabled and Git config isolated. It independently hashes each blob's original bytes as a Git object before copying four tracked TypeScript sources into the temporary prepared root. Besides deadline/control and exact-wire contracts, it verifies that revoked and stale recovery select `Gửi lại đúng thao tác` only inside the exact current `Đối soát thao tác` region. The paired test executes no frontend code or compiler. Separate frontend unit and browser runs provide behavior coverage. This fixture uses no real host, database or browser.

Paired Git starts suspended and enters a Windows job before resuming. On timeout or cancellation, the runner requests termination of that exact job and waits for root exit, zero active job processes and both stdout/stderr reader tasks before it releases their resources. Disposable child tests cover timeout, cancellation, delayed output on both streams, successful exit and start failure. A separate controlled test holds each reader open to verify that completion waits for both. If Windows permanently cannot prove exit or EOF, the paired test remains pending and retains ownership.

## Verification

Windows 10.0.26300, .NET SDK 8.0.428, VSTest 17.11.2, net48 Release, Node 22.14.0, npm 11.6.0, Vitest 5.0.5 and Playwright package 1.64.1.

| Selection | Discovered / executed | Passed / failed / skipped | SHA-256 |
| --- | ---: | ---: | --- |
| Fresh Prepare pin RED, TRX | 23 / 23 | 21 / 2 / 0 | `a8698acbc3b97cf3d167f415a6f00da2e7434f8d15cd55839417171064149cd5` |
| Fresh Prepare pin GREEN, TRX | 23 / 23 | 23 / 0 / 0 | `d5b59abcfd62a3e963da32b70c2ece8c1df6c8b00d4c81716a2214ed536dffe3` |
| Review correction RED, TRX | 10 / 10 | 0 / 10 / 0 | `8ded5eb0a7e7cb477dd29013725fd514f0a83930630d72c9aae5728e76c2d8a3` |
| Review correction focused GREEN, TRX | 11 / 11 | 11 / 0 / 0 | `e8f6b79f5a68c9016afbbd9bfc1aad9c35a51487bce3d6703e8e667bf05a02fe` |
| Process cleanup RED, TRX | 2 / 2 | 0 / 2 / 0 | `6673f3c0211833f368e20777538eb021bd592800d1a3a596613b8c2e224126c4` |
| Process cleanup focused GREEN, TRX | 16 / 16 | 16 / 0 / 0 | `daae348ee7ca236583e2eba344141819d2e9aba7cfca8c3a1a528a237038b47e` |
| Final integrated backend after review, TRX | 729 / 729 | 729 / 0 / 0 | `cdde1ef8eb99db7d6cb1d8e9f08c895794691a2e795d9faf7de49edafc96cd0c` |
| Exact-wire pin RED, TRX | 29 / 29 | 26 / 3 / 0 | `f8b37f7ef9056833a30e1549aeecb02668ef095ba1320ce94cbcaef825b32420` |
| Exact-wire pin focused GREEN, TRX | 29 / 29 | 29 / 0 / 0 | `597e70b368e137eb36abb3ecf2c7146b8f8fc3bc53e65100103ee4d2f8267715` |
| Exact-wire integrated backend, TRX | 739 / 739 | 739 / 0 / 0 | `b89f96398ebf41c03a912ffb0f08eb97b568adc78bc9cb28cd9057f9c24b3b6a` |
| Current-region pin RED, TRX | 30 / 30 | 27 / 3 / 0 | `e3670c9b0679205a1043778097d6a776debc3c2a88ca084b6c32e38daa7db646` |
| Current-region pin post-review GREEN, TRX | 30 / 30 | 30 / 0 / 0 | `c53db459385e8036faec2e407ee4d18684f42ef824985fa4e42b9f98e6a9e5af` |
| Current-region post-review integrated backend, TRX | 740 / 740 | 740 / 0 / 0 | `b56f255d23a0eac56e9eac5cd163dc146fbfb5ceacb4ecdaa0a162d7a0c71cd2` |
| Frontend typecheck log at reload-restore SHA `78e24ef…` | — | exit 0, zero diagnostics | `6a56be18103da1ff832d9eb9529790860e2aa12dbceef7417852378e41ef748d` |
| Frontend full unit log at reload-restore SHA `78e24ef…`, 90 files | 1,791 / 1,791 | 1,791 / 0 / 0 | `c2dfc50dab3f105307de361e2658de8428f3bec1826dede1d75f644f0e6971ef` |

The exact-wire pin RED failed only the inverse rows for the new and previously admitted SHA plus paired-source admission. The `447e39f…` row already blocked before the pin change. Review regression checks cover a replacement marker in a disposable repository, byte-hash mismatch, local/worktree configuration refusal and the absence of frontend toolchain execution. The exact-wire integrated backend run includes the four-source paired test and selector-plan assertions for all 28 fresh Prepare rows.

```powershell
$project = 'tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj'
dotnet test $project -c Release -f net48 --filter 'FullyQualifiedName~FreshPrepareRequiresTheReviewedArtifactCommandPin'

# For the paired source test, point to the exact reviewed checkout.
$env:MYDMSVN_WEB6_PAIRED_SOURCE_ROOT = '<reviewed frontend checkout>'
$focused = '(FullyQualifiedName~Paired_git_|FullyQualifiedName~Paired_source_|FullyQualifiedName~Prepared_frontend_cutover_matches_backend_wire_and_deadline_hierarchy)'
dotnet test $project -c Release -f net48 --no-build --no-restore --filter $focused --logger 'trx;LogFileName=round2-verified-focused.trx'
$filter = '(FullyQualifiedName~Web6CandidateBootstrap|FullyQualifiedName~Web6CandidateBackendSourceTests|FullyQualifiedName~Web6CandidateCutoverTests|FullyQualifiedName~Web6Cutover|FullyQualifiedName~Web6CandidatePairBudgetTests|FullyQualifiedName~WebRecoveryPilotControlTests|FullyQualifiedName~Web5IisExpressHostAdmissionTests|FullyQualifiedName~Web5HttpServiceStateTests)&FullyQualifiedName!~WebHostIisExpressTests.&FullyQualifiedName!~Deadline_terminates_the_owned_process_tree&FullyQualifiedName!~Live_IIS_Express_queue_is_exclusive&Category!=FastSQL&Category!=MigrationSchema&Category!=SecurityProvisioning&Category!=SqlConcurrency&Category!=Web5OwnedHostAdmission&Category!=Web6CandidateRecovery'
dotnet test $project -c Release -f net48 --no-build --no-restore --list-tests --filter $filter
dotnet test $project -c Release -f net48 --no-build --no-restore --filter $filter --logger 'trx;LogFileName=round2-verified-integrated.trx'

# From the reviewed frontend checkout.
npm.cmd run typecheck
npm.cmd run test:unit
```

Each project below passed `dotnet restore <project> --locked-mode` and `dotnet build <project> -c Release -f net48 --no-restore` with zero build warnings and errors. Security.Tests was restored and rebuilt with its project graph after the review correction. These hashes identify local build outputs and their lockfiles.

| Project | Build output SHA-256 | `packages.lock.json` SHA-256 |
| --- | --- | --- |
| `MyDmsVn.Server.Application` | `8c25b70ef8d67666047621bcf69f04ccdcc4e2eeeedab2bd773a22239154630c` | `852443fc48326cb50ee280d576865dc1ebd32adac691431e2fbf9fe545a4d8e2` |
| `MyDmsVn.Server.HttpHost` | `de8b3a3776bada2a834d1969ddaf23cade6118f10d96539a7897023b2637b23d` | `e415809ffd9971e46383ed770cc8a3f2186d4b64e378aa8ef69a9a9a40d16f36` |
| `MyDmsVn.Web6.CandidateBootstrap` | `92b4fb464952f11d96d13652a17af4ce2523eed8c3638e32fb480767ff3ff163` | `5dbe9c432c1ce36f9545a6e28a4390579cd81135408517aeac83acbd1878c5da` |
| `MyDmsVn.Server.Infrastructure.Security.Tests` | `7167ed91830a0e01a85ec13bb037e53ee71bef0ade96c8c8d82bd6945d539948` | `d3c948ec7736bfbd29ca86aadb8e11420fcb64c47d6805d2c7df17270544f9c4` |

Frontend `package-lock.json` SHA-256 is `283593a2d944b2031ce57c8c8a59f18755e51b25ab6767571ef3ffdd341f55e9`. The installed Playwright worker used by the unit source assertion has SHA-256 `b7ae55d578fb3a2a6bd4d9acc0abf93146ae1d9062330bbc0e3f82acbff751b8`. The candidate checks use existing locked dependencies. Both worktrees passed `git diff --check`.

## Final-review correction for early natural child exit

Backend parent `52b1ea4b0ed78dfc8d15c784a88eb9ae947d0739` called the running-process check during identity capture. A child could exit after a successful direct launch and be rejected before its output was read. This correction captures the retained launch handle, PID and start time. Before terminating a running child, the adapter verifies the same handle, PID, start time and executable path. The child lifecycle joins proven exit and both output readers before exposing the exit code to the existing service-state validator.

The regression gates the real adapter on a disposable local `cmd.exe` exit before capture. Exit-code rows 0 and 7 reproduce the original failure and verify both streams and exit-code handling after the correction. A live disposable child checks that a different handle or image path prevents termination. Controlled stdout and stderr cases separately verify that the lifecycle retains ownership until each reader completes. These fixtures use no HTTP.sys, SQL, IIS, browser, or retained resources.

The locked Security.Tests Release/net48 build passed with zero warnings or errors. The frontend remained at `96842a6329191713282b3212e5420872eb0f3a41`. The integrated selection above was rerun with that checkout configured, and its exact-source paired test passed.

| Selection | Discovered / executed | Passed / failed / skipped | TRX SHA-256 |
| --- | ---: | ---: | --- |
| Early-exit RED | 2 / 2 | 0 / 2 / 0 | `213caf29c83fc1781701842d9d843fbc24cb36d8d9cbd83f1194f832b2a276cf` |
| Focused final | 73 / 73 | 73 / 0 / 0 | `1d64ba8d74b78e863d67e250caa884b7261ef2e1d178fc569a67045db2dcc30e` |
| Integrated final | 732 / 732 | 732 / 0 / 0 | `dea6cdd9f2b575efa01ea349f75e786b0585082f0dfaaeddfd56e5bb4b478a99` |

The corrected Security.Tests assembly SHA-256 is `fb7c3d78b00e5e1a6be91f7b8044987a5e005dd86f913db0d62e161076d81520`. The 729-case result above records the prior backend source.

## PR review correction for connector cancellation and CI timeout

Frontend PR #38 review identified two P2 findings at `96842a6329191713282b3212e5420872eb0f3a41`: the 58.1-second virtual sequential observer case retained Vitest's five-second real timeout, and the cutover absolute timer settled the request without cancelling an in-progress named-pipe connector. The corrected frontend is `726b10351bdbe0028696f9fcfa0bbb52e8a84d28`, tree `2a6173d788d78f9f9c3e30818aced6cb95095684`.

The transport now passes an owned `AbortSignal` to the connector and aborts it when the request settles. The default connector destroys an in-progress socket, cancels its retry pause and checks cancellation before every further `ENOENT` attempt. It still never retries after connection or for a non-`ENOENT` failure. Regression tests prove that an absent pipe cannot produce a later connection after the absolute deadline and that a pending socket is destroyed. The long virtual observer keeps the same sequential 29-second context plus 29-second eligibility delays and exact selector deadline clamp, but uses a one-second controlled polling step and a 15-second test timeout so CI cost is bounded without shortening the simulated HTTP path.

Frontend verification on the corrected commit passed typecheck, production build, 90 unit files / 1,794 tests, 286 Chromium stub/browser tests and 2 production-artifact isolation tests. These are local non-live checks; they do not establish native WEB6 acceptance.

Backend pin tests first reproduced the expected inverse failure: the new SHA was blocked, the superseded SHA was admitted and the paired source test failed admission. After changing only `PreparedCandidateReceipt.ReviewedFrontendSha`, the exact pin matrix and paired source selection passed 25/25. The locked Release/net48 build passed with zero warnings/errors. The bounded integrated pure selection passed 733/733 with zero failed/skipped and included the paired exact-source row.

| PR review selection | Passed / failed / skipped | TRX SHA-256 |
| --- | ---: | --- |
| Exact pin matrix plus paired source | 25 / 0 / 0 | `bbcd1324cab556cd6e703e9aad4432f53a1531a8d58a801ec31106022e5e1432` |
| Integrated pure backend | 733 / 0 / 0 | `b96a1ffec58aa06a7224e4c0c64a60f2c9fe863f1e70a9b423ead0db919741d8` |

No fresh live candidate, allocation, SQL approval, IIS process or browser cutover was created. Attempt21 and all other retained resources were not accessed.

## Fresh-attempt product lookup commit correction

The next fresh-attempt source pair adds only the reviewed frontend cutover harness correction from PR #39 on top of PR #38. Frontend `eafcc40a13c17dbfe3c546b76d6dbdd1bf360193`, tree `0ecffdc18a06685e4790c53e9494ae9f340abe19`, waits for exactly one `PILOT-P · ...` option, clicks that option and commits the grid editor with `Tab`. This replaces the unguarded immediate `ArrowDown`/`Enter` sequence that left the retained attempt's product cell unselected and blocked Save before any Sales mutation request. The frontend regression was observed RED against the old sequence, then GREEN 40/40; local HTTP unit 1,119/1,119, full unit/component 1,795/1,795, typecheck and production build passed. Both GitHub frontend workflows for PR #39 passed, including Chromium regression and production-artifact isolation.

Backend pin verification first reproduced the exact inverse RED: the new frontend SHA was `BLOCKED_REVIEWED_FRONTEND_PIN` and the former `726b103...` SHA remained `PIN_MATCHED`; 23 other rows passed. Changing only `PreparedCandidateReceipt.ReviewedFrontendSha` made the 25-row pin matrix pass and made `726b103...` a negative case. The paired source test independently verified the clean frontend commit/tree and its tracked cutover sources. Release/net48 build completed with zero warnings and errors.

| Fresh-attempt correction selection | Passed / failed / skipped | TRX SHA-256 |
| --- | ---: | --- |
| Exact pin matrix plus paired source | 26 / 0 / 0 | `0b5d7a7693e8d53c4ace43eea9dc533e6b59fd7eae2b8e582022a144f40a819e` |
| Integrated pure backend | 736 / 0 / 0 | `7220a2926b3b5f2a7bca13f090bd66ec339f510ee77896e6576afe311c3cbc12` |

The resulting local Release artifacts were `MyDmsVn.Web6.CandidateBootstrap.exe` SHA-256 `7fd7c373ceab633cb571e246c37d1abaf026a2aa3f1f0014c9810549484edb06` and `MyDmsVn.Server.Infrastructure.Security.Tests.dll` SHA-256 `70e7b413bda3e269467ca1df8609d09bc5aeb3e367577efc4cf0102e4c27c19b`. These are local source-verification artifacts, not candidate publish hashes.

No Owner authority, fresh candidate root, SQL approval/allocation, IIS process or live browser selector was created by this correction checkpoint. Attempt21 and every other retained resource remain untouched. Fresh live preparation still requires this exact backend correction to be committed, reviewed and CI-verified, followed by a new exact-scope Owner authorization.

## Fresh-attempt protected-draft reload correction

Frontend PR #40 adds only the reviewed cutover harness correction on top of PR #39. Frontend `76b125d74769b96be1f007803954b5b2489b10c3`, tree `025e13fcda200973c705c8eabe827c4b926dc274`, restores the unique protected local draft after a workspace reload before opening `Đối soát thao tác`. The retained fresh attempt had reached the authenticated workspace with `Bản nháp trên thiết bị` visible, so waiting directly for the unresolved-operation action could not reopen recovery. The correction reuses the existing manual-resend restore path and closes the dialog before the next cutover control action.

The frontend regression was observed RED at 1 failed / 40 passed, then GREEN at 41/41. Local HTTP unit tests passed 1,120/1,120, the full frontend unit/component suite passed 1,796/1,796, and typecheck plus production build succeeded. Both GitHub frontend workflows for PR #40 passed, including Chromium regression and production-artifact isolation.

Backend pin verification reproduced the inverse RED before changing production code: the new frontend SHA was `BLOCKED_REVIEWED_FRONTEND_PIN`, the superseded `eafcc40...` SHA remained `PIN_MATCHED`, and the paired source test was blocked. After changing only `PreparedCandidateReceipt.ReviewedFrontendSha`, all 26 pin rows passed, with `76b125d...` admitted and `eafcc40...` rejected, and the paired source test independently verified the clean frontend commit/tree and tracked cutover sources.

| Protected-draft reload selection | Passed / failed / skipped | TRX SHA-256 |
| --- | ---: | --- |
| Exact pin matrix | 26 / 0 / 0 | `771c6ce287caae7381c5a6d1b277c6a21a7a8c6e61afd6fa4937a2e777835ee8` |
| Paired exact source | 1 / 0 / 0 | `f69757a807667b50d61b2ff8bab694ecb8826abfe5b09848f90216440e5dda36` |
| Integrated pure backend | 737 / 0 / 0 | `8717bd4d5826c0145828aa326bf29cd57a45241cedce1cfaac58da5eb3425581` |

The Release/net48 Security.Tests project graph built with zero warnings and errors. Resulting local verification artifacts were `MyDmsVn.Web6.CandidateBootstrap.exe` SHA-256 `8132dc4dbe1a3858e6db62e1fdd54ff8bb0494621823d1bb59a3e7515fbb9108` and `MyDmsVn.Server.Infrastructure.Security.Tests.dll` SHA-256 `47272120fd6d1ca115fe0b97f7beee76c345040fa888a22ed07b2f7d247859de`. These are local source-verification artifacts, not candidate publish hashes.

No Owner authority, candidate root, protected SQL approval, allocation, IIS process or live browser selector was created in this checkpoint. The previous failed attempt and all retained resources remain untouched. A fresh live chain requires this backend correction to be committed, reviewed and CI-verified, followed by a new exact-scope Owner authorization for the resulting backend SHA paired with frontend `76b125d...`.

## Boundary-observer CI determinism and final pin correction

Frontend PR #40 commit `78e24ef00738f0b1d09ff466991802a736c67fec`, tree `72a8e252f164c131fb72595cfda52c1432c2027c`, retains the protected-draft reload correction and makes its fake-clock boundary-observer regression deterministic under the protected 90-worker unit runner. The test adapter polls presentation every five virtual seconds and advances nested async work one pending timer at a time; runtime request, selector and controller deadlines are unchanged. Protected frontend run `43570399192` passed typecheck, production build, unit/component tests, Chromium frontend regression and production-artifact isolation on the exact commit.

Backend tests first reproduced the inverse RED: the new SHA was blocked, the superseded `76b125d...` SHA was admitted, and the paired-source test was blocked (3 failed, 25 passed). After changing only `PreparedCandidateReceipt.ReviewedFrontendSha`, the exact pin matrix plus paired source passed 28/28, including an explicit negative row for `76b125d...`. The bounded integrated pure selection passed 738/738 with zero failed/skipped, and the Release/net48 Security.Tests project graph built with zero warnings and errors.

| Final pin selection | Passed / failed / skipped | TRX SHA-256 |
| --- | ---: | --- |
| Exact pin matrix plus paired source | 28 / 0 / 0 | `4c650ae89a236325e9874107094acd7f8d73bbde5127c97c5c7949ab46979282` |
| Integrated pure backend | 738 / 0 / 0 | `ac4e699c6fd42e922f4838a0669f70e59b972d07217034b041cf55986b54d63a` |

The resulting local Release artifacts were `MyDmsVn.Web6.CandidateBootstrap.exe` SHA-256 `3a5884a0737aa8af1bad16907749760d60553642edd1298fad0cb600b7f8778d` and `MyDmsVn.Server.Infrastructure.Security.Tests.dll` SHA-256 `4c7d54546afcf4735bdc760b4123c6027693fe0aa4d9b531da1ece8068e2d407`. These identify local verification outputs, not live candidate publish artifacts.

No fresh candidate root, SQL approval/allocation, IIS process, browser cutover or retained-resource access occurred in this source correction. A new live attempt remains blocked until this backend change is independently reviewed, committed, pushed, protected-CI verified, and then explicitly authorized for its resulting exact backend SHA paired with frontend `78e24ef...`.

## Current-recovery-region locator and pin correction

Fresh candidate `web6-20261114-3408cf4b` used backend `a7c8bac5e6d97fa40b8ee9dba5976b9a5ca3e672` / tree `9cb4d2b09b66ce101f188ce4b17a24ab001e7bb8` and frontend `fa032ece3d47af029d5095ae39b21fb43d0f1afc` / tree `e0cb0950f78d072e73850392b6bf496e574775ae`. Authorize, Prepare and Run each ran exactly once. The candidate reached `PairPassed`, with Chromium and Edge each passing 1/1 and native cleanup accepted, then became `FailedRetained` when the third Chromium cutover case found two same-named resend buttons and its unscoped strict locator refused the click. Workload/recovery and Edge cutover were NOT RUN. Cutover resources remain retained with deletion authority false. No post-failure SQL query, cleanup, retry or retained-candidate access occurred.

Frontend `e5e083ffd1bf8f70ce49e2e9bad81661e9c6e669`, tree `928b9878a8b408244433e0eb3ee89130a46f4fd6`, changes only the pilot helper/spec, regression test and evidence. It locates the exact `Gửi lại đúng thao tác` button inside the current `Đối soát thao tác` region for both revoked and stale checks. Focused unit passed 43/43, full unit passed 1,798/1,798, typecheck/build passed, Chromium E2E passed 286/286 and production isolation passed 2/2. Protected push run `43590748324` and rerun pull-request run `43590752462` both terminal-PASS. An independent review found no issue.

Backend pin tests first reproduced the inverse RED: the new frontend SHA was blocked, `fa032...` remained `PIN_MATCHED`, and paired-source Prepare was blocked (3 failed, 27 passed). Changing only `PreparedCandidateReceipt.ReviewedFrontendSha` made all 30 rows pass. Review strengthened the paired-source contract to extract the exact helper and `restartRevokeRestore` bodies and bind the chained region/button locator, both guarded clicks, zero confirmation, one write, `DISPATCHED`, protected state and no-cutover-mutation assertions. The bounded post-review integrated pure selection passed 740/740 with zero failed/skipped, and the Release/net48 Security.Tests project graph built with zero warnings and errors.

| Current-region correction selection | Passed / failed / skipped | TRX SHA-256 |
| --- | ---: | --- |
| Exact pin matrix plus paired source RED | 27 / 3 / 0 | `e3670c9b0679205a1043778097d6a776debc3c2a88ca084b6c32e38daa7db646` |
| Exact pin matrix plus paired source post-review GREEN | 30 / 0 / 0 | `c53db459385e8036faec2e407ee4d18684f42ef824985fa4e42b9f98e6a9e5af` |
| Post-review integrated pure backend | 740 / 0 / 0 | `b56f255d23a0eac56e9eac5cd163dc146fbfb5ceacb4ecdaa0a162d7a0c71cd2` |

The resulting local Release artifacts were `MyDmsVn.Web6.CandidateBootstrap.exe` SHA-256 `bc0b4277ad216257bb0568479da523ebb4c721644a42955e569230d4b7052fce` and `MyDmsVn.Server.Infrastructure.Security.Tests.dll` SHA-256 `af20a008810d9d9b2b3f38ca54f95d8d0a12e5b07de41c4c6c2aada19b267c8c`. These identify local source-verification outputs, not candidate publish artifacts. No Owner authority, new candidate root, SQL approval/allocation, IIS process or live browser selector was created by this correction. A fresh attempt still requires the resulting backend commit to pass independent review and protected CI, then a new explicit Owner authorization for its exact pairing with frontend `e5e083f...`.

## Acceptance boundary

Real SQL, IIS Express, full IIS, Chromium/Edge cutover, native process/provider deadline containment, workload, recovery and Narrator acceptance were **NOT RUN** in this checkpoint. The tests exercised disposable filesystem/Git, fake toolchain and synthetic deadline/controller fixtures. No live approval or candidate was created. Ordinary resend is **OFF**, staging is **NOT ACCEPTED** and production remains **NO-GO**.
