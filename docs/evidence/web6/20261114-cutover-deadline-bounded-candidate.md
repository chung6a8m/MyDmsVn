# WEB6 cutover deadline bounded candidate — 2026-11-14

**Source and non-live verification.** Attempt21 remains `FailedRetained` and was left untouched. The current candidate has no `CandidateVerified` result. Fresh live execution requires final cross-repository review and separate explicit Owner authorization. [Approved design](../../superpowers/specs/2026-11-13-web6-cutover-deadline-hierarchy-design.md) · [Implementation plan](../../superpowers/plans/2026-11-13-web6-cutover-deadline-hierarchy.md) · [Runbook](../../development/WEB6_CANDIDATE_RUNBOOK.md#cutover-deadline-and-reset-protocol).

## Source identity

| Checkout or projection | Git commit | Git tree |
| --- | --- | --- |
| Backend reviewed base | `21a7188eaf24cc139c875865568cb23b647c57aa` | `e3265b1da017397aa8f81a69c54f8cf27bdff17e` |
| Initial Task 6 backend | `c99f426d98853dbd3f9a48ff6a769bba615ef8e9` | `460a3f51439ed7c3f5fbdb7a1d3fcae775b615d8` |
| Backend verified source and tests, before this documentation delta | Review correction staged over initial Task 6 | `eaa6f3c30b4864cd8464c9459dcc87f16f746c1b` |
| Frontend exact reviewed pin | `96842a6329191713282b3212e5420872eb0f3a41` | `a8095060d0c578d8556a58c5301e11df4b4aff97` |

The only change to bootstrap execution in Task 6 is the `ReviewedFrontendSha` pin. The review correction changes the paired source test and evidence. The vendor gitlink remains `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf`.

The real `BootstrapPreflight.PrepareAsync` path ran with synthetic Git/build/SQL-master toolchain inputs and disposable protected files. It accepted `96842a6329191713282b3212e5420872eb0f3a41` as `PIN_MATCHED`. It blocked the prior pin `66b935488d00ee78ec3ffbd183f474da06aa3404` and immediate frontend base `447e39f03b2b22398d30324c8cec992fc93ce83c` as `BLOCKED_REVIEWED_FRONTEND_PIN`. Only the admitted receipt opened a selector plan in the pin matrix.

The paired test verifies the configured frontend checkout's commit/tree with replacement lookup disabled and Git config isolated. It independently hashes each blob's original bytes as a Git object before copying the three tracked TypeScript sources into the temporary prepared root. The test inspects those sources as text and validates synthetic frames against the real backend parser. It checks 11 action caps, both `revokeGrantOff` forms, the strict allowance field, the 5,000-ms response reserve, the three scenario/reset budgets, one reset hook and the pack/controller/aggregate/selector hierarchy. The paired test executes no frontend code or compiler. The separate full frontend unit run provides behavior coverage for cumulative accounting, framing, transport timing and the cleanup hook. This fixture uses no real host, database or browser.

## Verification

Windows 10.0.26300, .NET SDK 8.0.428, VSTest 17.11.2, net48 Release, Node 22.14.0, npm 11.6.0, Vitest 5.0.5 and Playwright package 1.64.1.

| Selection | Discovered / executed | Passed / failed / skipped | SHA-256 |
| --- | ---: | ---: | --- |
| Fresh Prepare pin RED, TRX | 23 / 23 | 21 / 2 / 0 | `a8698acbc3b97cf3d167f415a6f00da2e7434f8d15cd55839417171064149cd5` |
| Fresh Prepare pin GREEN, TRX | 23 / 23 | 23 / 0 / 0 | `d5b59abcfd62a3e963da32b70c2ece8c1df6c8b00d4c81716a2214ed536dffe3` |
| Review correction RED, TRX | 10 / 10 | 0 / 10 / 0 | `8ded5eb0a7e7cb477dd29013725fd514f0a83930630d72c9aae5728e76c2d8a3` |
| Review correction focused GREEN, TRX | 11 / 11 | 11 / 0 / 0 | `e8f6b79f5a68c9016afbbd9bfc1aad9c35a51487bce3d6703e8e667bf05a02fe` |
| Final integrated backend after review, TRX | 724 / 724 | 724 / 0 / 0 | `415b054fae0fe69986256846b97d9881224ef8f50f75632caeba9879a51724c0` |
| Frontend typecheck log at the reviewed SHA | — | exit 0, zero diagnostics | `6a56be18103da1ff832d9eb9529790860e2aa12dbceef7417852378e41ef748d` |
| Frontend full unit log at the reviewed SHA, 90 files | 1,791 / 1,791 | 1,791 / 0 / 0 | `c2dfc50dab3f105307de361e2658de8428f3bec1826dede1d75f644f0e6971ef` |

The pin RED failed only the inverse rows for the new and previously admitted SHA. The `447e39f…` row already blocked before the pin change. Review regression checks cover a replacement marker in a disposable repository, byte-hash mismatch, local/worktree configuration refusal and the absence of frontend toolchain execution. The final integrated backend run includes the exact-source paired test and the selector-plan assertions for all 23 fresh Prepare rows.

```powershell
$project = 'tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj'
dotnet test $project -c Release -f net48 --filter 'FullyQualifiedName~FreshPrepareRequiresTheReviewedArtifactCommandPin'

# For the paired source test, point to the exact reviewed checkout.
$env:MYDMSVN_WEB6_PAIRED_SOURCE_ROOT = '<reviewed frontend checkout>'
$focused = '(FullyQualifiedName~Paired_source_|FullyQualifiedName~Prepared_frontend_cutover_matches_backend_wire_and_deadline_hierarchy)'
dotnet test $project -c Release -f net48 --no-build --no-restore --filter $focused --logger 'trx;LogFileName=round1-final-focused.trx'
$filter = '(FullyQualifiedName~Web6CandidateBootstrap|FullyQualifiedName~Web6CandidateBackendSourceTests|FullyQualifiedName~Web6CandidateCutoverTests|FullyQualifiedName~Web6Cutover|FullyQualifiedName~Web6CandidatePairBudgetTests|FullyQualifiedName~WebRecoveryPilotControlTests|FullyQualifiedName~Web5IisExpressHostAdmissionTests|FullyQualifiedName~Web5HttpServiceStateTests)&FullyQualifiedName!~WebHostIisExpressTests.&FullyQualifiedName!~Deadline_terminates_the_owned_process_tree&FullyQualifiedName!~Live_IIS_Express_queue_is_exclusive&Category!=FastSQL&Category!=MigrationSchema&Category!=SecurityProvisioning&Category!=SqlConcurrency&Category!=Web5OwnedHostAdmission&Category!=Web6CandidateRecovery'
dotnet test $project -c Release -f net48 --no-build --no-restore --list-tests --filter $filter
dotnet test $project -c Release -f net48 --no-build --no-restore --filter $filter --logger 'trx;LogFileName=round1-integrated.trx'

# From the reviewed frontend checkout.
npm.cmd run typecheck
npm.cmd run test:unit
```

Each project below passed `dotnet restore <project> --locked-mode` and `dotnet build <project> -c Release -f net48 --no-restore` with zero build warnings and errors. Security.Tests was restored and rebuilt with its project graph after the review correction. These hashes identify local build outputs and their lockfiles.

| Project | Build output SHA-256 | `packages.lock.json` SHA-256 |
| --- | --- | --- |
| `MyDmsVn.Server.Application` | `8c25b70ef8d67666047621bcf69f04ccdcc4e2eeeedab2bd773a22239154630c` | `852443fc48326cb50ee280d576865dc1ebd32adac691431e2fbf9fe545a4d8e2` |
| `MyDmsVn.Server.HttpHost` | `de8b3a3776bada2a834d1969ddaf23cade6118f10d96539a7897023b2637b23d` | `e415809ffd9971e46383ed770cc8a3f2186d4b64e378aa8ef69a9a9a40d16f36` |
| `MyDmsVn.Web6.CandidateBootstrap` | `44c33f4d489793efca30c690c4687f814213f9a4d0b88d27de758b4c6b5a9d75` | `5dbe9c432c1ce36f9545a6e28a4390579cd81135408517aeac83acbd1878c5da` |
| `MyDmsVn.Server.Infrastructure.Security.Tests` | `d9d5ff9806b229746d9bba61c46b15b12d5de34faf04d01e176f022cc3d60a39` | `d3c948ec7736bfbd29ca86aadb8e11420fcb64c47d6805d2c7df17270544f9c4` |

Frontend `package-lock.json` SHA-256 is `283593a2d944b2031ce57c8c8a59f18755e51b25ab6767571ef3ffdd341f55e9`. The installed Playwright worker used by the unit source assertion has SHA-256 `b7ae55d578fb3a2a6bd4d9acc0abf93146ae1d9062330bbc0e3f82acbff751b8`. The candidate checks use existing locked dependencies. Both worktrees passed `git diff --check`.

## Acceptance boundary

Real SQL, IIS Express, full IIS, Chromium/Edge cutover, native process/provider deadline containment, workload, recovery and Narrator acceptance were **NOT RUN** in this checkpoint. The tests exercised disposable filesystem/Git, fake toolchain and synthetic deadline/controller fixtures. No live approval or candidate was created. Ordinary resend is **OFF**, staging is **NOT ACCEPTED** and production remains **NO-GO**.
