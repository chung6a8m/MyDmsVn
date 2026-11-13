# WEB6 cutover deadline bounded candidate — 2026-11-14

**Source and non-live verification.** Attempt21 remains `FailedRetained` and was left untouched. The current candidate has no `CandidateVerified` result. Fresh live execution requires final cross-repository review and separate explicit Owner authorization. [Approved design](../../superpowers/specs/2026-11-13-web6-cutover-deadline-hierarchy-design.md) · [Implementation plan](../../superpowers/plans/2026-11-13-web6-cutover-deadline-hierarchy.md) · [Runbook](../../development/WEB6_CANDIDATE_RUNBOOK.md#cutover-deadline-and-reset-protocol).

## Source identity

| Checkout or projection | Git commit | Git tree |
| --- | --- | --- |
| Backend reviewed base | `21a7188eaf24cc139c875865568cb23b647c57aa` | `e3265b1da017397aa8f81a69c54f8cf27bdff17e` |
| Backend verified source and tests, before this documentation delta | Task 6 changes staged over the reviewed base | `e2c3ecd4b67a0e7595000b068a2cb5faf05f8d6c` |
| Frontend exact reviewed pin | `96842a6329191713282b3212e5420872eb0f3a41` | `a8095060d0c578d8556a58c5301e11df4b4aff97` |

The commit containing this record adds the documented protocol and evidence to the verified backend source/test tree. The only backend execution change in this checkpoint is the `ReviewedFrontendSha` pin. The vendor gitlink remains `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf`.

The real `BootstrapPreflight.PrepareAsync` path ran with synthetic Git/build/SQL-master toolchain inputs and disposable protected files. It accepted `96842a6329191713282b3212e5420872eb0f3a41` as `PIN_MATCHED`. It blocked the prior pin `66b935488d00ee78ec3ffbd183f474da06aa3404` and immediate frontend base `447e39f03b2b22398d30324c8cec992fc93ce83c` as `BLOCKED_REVIEWED_FRONTEND_PIN`. Only the admitted receipt opened a selector plan in the pin matrix.

The paired test verifies the configured frontend checkout's commit/tree and reads the three tracked TypeScript blobs from that commit. The temporary Prepare fixture copies those blobs under the prepared frontend root. The test executes the pure TypeScript deadline helper and frame builder there and validates the resulting frames with the backend parser. It checks 11 action caps across 12 frames, including both `revokeGrantOff` forms, the strict allowance field, the 5,000-ms response reserve, the three scenario/reset budgets, one reset hook and the pack/controller/aggregate/selector hierarchy. Frontend units separately exercise cumulative accounting, transport timing and the cleanup hook. This fixture uses no real host, database or browser.

## Verification

Windows 10.0.26300, .NET SDK 8.0.428, VSTest 17.11.2, net48 Release, Node 22.14.0, npm 11.6.0, Vitest 5.0.5 and Playwright package 1.64.1.

| Selection | Discovered / executed | Passed / failed / skipped | SHA-256 |
| --- | ---: | ---: | --- |
| Fresh Prepare pin RED, TRX | 23 / 23 | 21 / 2 / 0 | `a8698acbc3b97cf3d167f415a6f00da2e7434f8d15cd55839417171064149cd5` |
| Fresh Prepare pin GREEN, TRX | 23 / 23 | 23 / 0 / 0 | `d5b59abcfd62a3e963da32b70c2ece8c1df6c8b00d4c81716a2214ed536dffe3` |
| Final integrated backend, TRX | 714 / 714 | 714 / 0 / 0 | `57202e4547345e9850a1dcf857477c067702032ae6cbca8ce65eb15eddd1f8c9` |
| Frontend typecheck log | — | exit 0, zero diagnostics | `6a56be18103da1ff832d9eb9529790860e2aa12dbceef7417852378e41ef748d` |
| Frontend full unit log, 90 files | 1,791 / 1,791 | 1,791 / 0 / 0 | `c2dfc50dab3f105307de361e2658de8428f3bec1826dede1d75f644f0e6971ef` |

RED failed only the inverse rows for the new and previously admitted SHA. The `447e39f…` row already blocked before the pin change. The final integrated backend run includes the exact-source paired test and the strengthened selector-plan assertions for all 23 fresh Prepare rows.

```powershell
$project = 'tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj'
dotnet test $project -c Release -f net48 --filter 'FullyQualifiedName~FreshPrepareRequiresTheReviewedArtifactCommandPin'

# For the final integrated run, point to the exact reviewed checkout with locked npm dependencies installed.
$env:MYDMSVN_WEB6_PAIRED_SOURCE_ROOT = '<reviewed frontend checkout>'
$filter = '(FullyQualifiedName~Web6CandidateBootstrap|FullyQualifiedName~Web6CandidateBackendSourceTests|FullyQualifiedName~Web6CandidateCutoverTests|FullyQualifiedName~Web6Cutover|FullyQualifiedName~Web6CandidatePairBudgetTests|FullyQualifiedName~WebRecoveryPilotControlTests|FullyQualifiedName~Web5IisExpressHostAdmissionTests|FullyQualifiedName~Web5HttpServiceStateTests)&FullyQualifiedName!~WebHostIisExpressTests.&FullyQualifiedName!~Deadline_terminates_the_owned_process_tree&FullyQualifiedName!~Live_IIS_Express_queue_is_exclusive&Category!=FastSQL&Category!=MigrationSchema&Category!=SecurityProvisioning&Category!=SqlConcurrency&Category!=Web5OwnedHostAdmission&Category!=Web6CandidateRecovery'
dotnet test $project -c Release -f net48 --no-build --no-restore --list-tests --filter $filter
dotnet test $project -c Release -f net48 --no-build --no-restore --filter $filter --logger 'trx;LogFileName=backend-integrated.trx'

# From the reviewed frontend checkout.
npm.cmd run typecheck
npm.cmd run test:unit
```

Each project below passed `dotnet restore <project> --locked-mode` and `dotnet build <project> -c Release -f net48 --no-restore` with zero build warnings and errors. These hashes identify local build outputs and their lockfiles.

| Project | Build output SHA-256 | `packages.lock.json` SHA-256 |
| --- | --- | --- |
| `MyDmsVn.Server.Application` | `8c25b70ef8d67666047621bcf69f04ccdcc4e2eeeedab2bd773a22239154630c` | `852443fc48326cb50ee280d576865dc1ebd32adac691431e2fbf9fe545a4d8e2` |
| `MyDmsVn.Server.HttpHost` | `de8b3a3776bada2a834d1969ddaf23cade6118f10d96539a7897023b2637b23d` | `e415809ffd9971e46383ed770cc8a3f2186d4b64e378aa8ef69a9a9a40d16f36` |
| `MyDmsVn.Web6.CandidateBootstrap` | `44c33f4d489793efca30c690c4687f814213f9a4d0b88d27de758b4c6b5a9d75` | `5dbe9c432c1ce36f9545a6e28a4390579cd81135408517aeac83acbd1878c5da` |
| `MyDmsVn.Server.Infrastructure.Security.Tests` | `205a100253b6687a4ab7d84cce5508fdc8b950208586241347d85e3f6a7b65f0` | `d3c948ec7736bfbd29ca86aadb8e11420fcb64c47d6805d2c7df17270544f9c4` |

Frontend `package-lock.json` SHA-256 is `283593a2d944b2031ce57c8c8a59f18755e51b25ab6767571ef3ffdd341f55e9`. The installed Playwright worker used by the unit source assertion has SHA-256 `b7ae55d578fb3a2a6bd4d9acc0abf93146ae1d9062330bbc0e3f82acbff751b8`. The candidate checks use existing locked dependencies. Both worktrees passed `git diff --check`.

## Acceptance boundary

Real SQL, IIS Express, full IIS, Chromium/Edge cutover, native process/provider deadline containment, workload, recovery and Narrator acceptance were **NOT RUN** in this checkpoint. The tests exercised disposable filesystem/Git, fake toolchain and synthetic deadline/controller fixtures. No live approval or candidate was created. Ordinary resend is **OFF**, staging is **NOT ACCEPTED** and production remains **NO-GO**.
