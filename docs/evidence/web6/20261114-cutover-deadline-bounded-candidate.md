# WEB6 cutover deadline bounded candidate — 2026-11-14

**Source and non-live verification.** Attempt21 remains `FailedRetained` and was left untouched. The current candidate has no `CandidateVerified` result. Fresh live execution requires final cross-repository review and separate explicit Owner authorization. [Approved design](../../superpowers/specs/2026-11-13-web6-cutover-deadline-hierarchy-design.md) · [Implementation plan](../../superpowers/plans/2026-11-13-web6-cutover-deadline-hierarchy.md) · [Runbook](../../development/WEB6_CANDIDATE_RUNBOOK.md#cutover-deadline-and-reset-protocol).

## Source identity

| Checkout or projection | Git commit | Git tree |
| --- | --- | --- |
| Backend reviewed base | `21a7188eaf24cc139c875865568cb23b647c57aa` | `e3265b1da017397aa8f81a69c54f8cf27bdff17e` |
| Initial Task 6 backend | `c99f426d98853dbd3f9a48ff6a769bba615ef8e9` | `460a3f51439ed7c3f5fbdb7a1d3fcae775b615d8` |
| Backend paired provenance correction | `6dddc17eca0bfc7970a81fa21bd3c14dc15a6682` | `c8e6a81578811bfb05573c60d626be6e08a47783` |
| Backend verified source and tests, before this documentation delta | Paired Git process cleanup correction | `dcdc8edda5a630fec191b5766ba78cd76d3490ba` |
| Frontend exact reviewed pin | `726b10351bdbe0028696f9fcfa0bbb52e8a84d28` | `2a6173d788d78f9f9c3e30818aced6cb95095684` |

The only change to bootstrap execution in Task 6 is the `ReviewedFrontendSha` pin. The review correction changes the paired source test and evidence. The vendor gitlink remains `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf`.

The real `BootstrapPreflight.PrepareAsync` path ran with synthetic Git/build/SQL-master toolchain inputs and disposable protected files. It accepted `726b10351bdbe0028696f9fcfa0bbb52e8a84d28` as `PIN_MATCHED`. It blocked the superseded reviewed pin `96842a6329191713282b3212e5420872eb0f3a41`, the prior pin `66b935488d00ee78ec3ffbd183f474da06aa3404` and immediate frontend base `447e39f03b2b22398d30324c8cec992fc93ce83c` as `BLOCKED_REVIEWED_FRONTEND_PIN`. Only the admitted receipt opened a selector plan in the pin matrix.

The paired test verifies the configured frontend checkout's commit/tree with replacement lookup disabled and Git config isolated. It independently hashes each blob's original bytes as a Git object before copying the three tracked TypeScript sources into the temporary prepared root. The test inspects those sources as text and validates synthetic frames against the real backend parser. It checks 11 action caps, both `revokeGrantOff` forms, the strict allowance field, the 5,000-ms response reserve, the three scenario/reset budgets, one reset hook and the pack/controller/aggregate/selector hierarchy. The paired test executes no frontend code or compiler. The separate full frontend unit run provides behavior coverage for cumulative accounting, framing, transport timing and the cleanup hook. This fixture uses no real host, database or browser.

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
| Frontend typecheck log at the reviewed SHA | — | exit 0, zero diagnostics | `6a56be18103da1ff832d9eb9529790860e2aa12dbceef7417852378e41ef748d` |
| Frontend full unit log at the reviewed SHA, 90 files | 1,791 / 1,791 | 1,791 / 0 / 0 | `c2dfc50dab3f105307de361e2658de8428f3bec1826dede1d75f644f0e6971ef` |

The pin RED failed only the inverse rows for the new and previously admitted SHA. The `447e39f…` row already blocked before the pin change. Review regression checks cover a replacement marker in a disposable repository, byte-hash mismatch, local/worktree configuration refusal and the absence of frontend toolchain execution. The final integrated backend run includes the exact-source paired test and the selector-plan assertions for all 23 fresh Prepare rows.

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

## Acceptance boundary

Real SQL, IIS Express, full IIS, Chromium/Edge cutover, native process/provider deadline containment, workload, recovery and Narrator acceptance were **NOT RUN** in this checkpoint. The tests exercised disposable filesystem/Git, fake toolchain and synthetic deadline/controller fixtures. No live approval or candidate was created. Ordinary resend is **OFF**, staging is **NOT ACCEPTED** and production remains **NO-GO**.
