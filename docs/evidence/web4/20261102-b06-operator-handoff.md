# WEB4 B06 — operator handoff documentation candidate

Date: 2026-11-02, Asia/Bangkok. **IN_PROGRESS / review-ready documentation candidate; independent review complete, Owner acceptance pending.** Owner selected WEB4-06/F05 and its backend B06 documentation companion. [Owning plan](../../plans/20261101-003-web4-reliability-server.md#7-b06--runbook-handoff-and-closeout-planned) · [operator handoff](../../development/WEB4_OPERATOR_HANDOFF.md). No DONE/archival/merge or production approval is inferred.

## Baseline and scope

- Clean backend main baseline: `473c6bfc39ca545c945a2387f1c644ff361a2132`; B05 merge `9ef084a61c1dd0e9f59be049fb5fbb51870e9444`, reviewed B05 head `1d5ce74f0babf687423a3912af40a6fa713a690e` remain historical.
- Worktree: `C:/MyData/2026/MyDmsVn/CleanArchitecture-web4-06`, branch `codex/web4-06-operator-handoff-backend`. Native `create_worktree` rejected backend `sourceRoot` outside this frontend chat's workspace; Git fallback created the isolated checkout from the verified clean baseline. No existing checkout was modified.
- Frontend F05 base: `0e44c5b5cab363d1c24fb785bb5679e2e551d327`; paired accepted F04 merge `f7765f052a66242e978a38daa4c22221ea262411`. F05 owns its own candidate/evidence; this is not its acceptance.
- B04 runtime source: `bd871518fd162927d938babd5535a2ddf91c2ad5`; B01 corpus `1c603c6ac57e59a3f7b4bc6ca976b2d531c079aa`, manifest SHA256 `0acab190e7fe126d53c13c0510a1958365e5a6e1fda78a4321da78297e0b3f1a` unchanged. Documentation commit is identified by the submitted Git candidate; no runtime candidate is created.

Markdown-only changes add the source-backed operational handoff, update stale current host references to accepted B04/B05, and add selection/candidate notices to entry pages. The existing host README remains migration-command owner. Notices distinguish current selection from historical PLANNED text without marking B06 complete or archiving the active plan. Original task evidence, archived WEB1–03, CEX, runtime, test/SQL scripts, grants, dependency pins and CI are unchanged.

## Source review

Reviewed the actual `DatabaseMigrationRunner`, `Schema11WebMaintenanceValidator`/`Schema9MaintenanceValidator`, schema012, Web principal provisioner, host startup/options/README, `SqlWebOperationJournal`, `SqlWebOperationReadRepository`, `SqlWebOperationMaintenance`, operation controller/codec and focused journal tests against the existing B01/B04 contract and B05 runbook/evidence. Findings recorded in the handoff:

- Exact schema/mode/principal/frontend coordination remains required; host runtime has journal SELECT/INSERT and epoch SELECT only. Migration stays an explicit separate maintenance action.
- Epoch is persistent and stable across workers. Controlled replacement/rewind rotation must wait for transaction-held epoch fences; there is no shipped rotation CLI.
- Positive original authorized outcome is the supported client settlement path. Current grants, original target/fingerprint and epoch remain mandatory; current Get or SQL observations cannot be forged into the original response or editable version.
- The schema permits CANCELLED, but no implemented/verified terminal negative-resolution protocol exists. Missing status is UNKNOWN; same-key resend remains disabled pending crash/Commit proof.
- Seven-day logical expiry and permanent keys are implemented; physical compaction needs an explicitly constructed privileged API/caller/audit/transaction. No scheduler, deployed maintenance UI/CLI or production backup/retention system is supplied by B06.

## Verification scope and results

[TEST_POLICY](../../TEST_POLICY.md) and [test commands](../../../tests/README.md) require diff/link/source-reference/status/encoding checks for Markdown-only changes. Local documentation verification uses Windows PowerShell, Python UTF-8/path checks and Git; no local browser/runtime is started. Hosted SQL-free CI is recorded separately below. Actual checks (all exit0):

- `git diff --check`: no whitespace errors.
- `git diff --stat` and focused `git diff -- src/MyDmsVn.Server.HttpHost/README.md docs/plans/20261101-003-web4-reliability-server.md`: inspected migration ownership, candidate selection and preserved historical checkpoints.
- Inline Python using `git diff --name-only`, `git ls-files --others --exclude-standard`, strict `bytes.decode('utf-8')`, BOM/CR byte checks and Markdown link/heading-anchor resolution: **11 Markdown files**, **49 added local link/anchor references**, **0 errors**. One external URL was syntax/source-context checked only; no remote availability request or PASS claim.
- `git status --short` and changed-path review: only the nine current Markdown entry/runbook pages and the two new B06 documents; original evidence (except its current index), archives, CEX files, runtime/tests/SQL/grants/pins/CI unchanged. Git's inherited autocrlf warning was informational; actual file bytes were checked UTF-8 without BOM and LF. No global/repository Git configuration was changed.

Source-reference review matched migration admission, grant delta, seven-day/1MiB expiry, immutable trigger, epoch/key lock ordering, current authority, absent rotation/negative-unlock CLI and explicit maintenance compaction API to the files listed above. No new tool or runnable command is claimed from documentation alone.

**Local runtime counts for B06: 0 executed tests.** Local build/unit/integration, SQL allocation/migration/provisioning, IIS/IIS Express, browser, epoch rotation, compaction, backup/restore, process-crash/Commit and attended IME/Narrator: **NOT RUN**. The fresh hosted SQL-free builds/tests below are a separate evidence scope; no live SQL or operator rehearsal ran. Historical B03/B04/B05 passes are linked only, not repeated or relabelled B06 PASS. B4-OPS-01 is documentation coverage; B4-OPS-02 operator rehearsal is not newly verified.

## Independent review — 2026-11-02

Independent whole-milestone review examined backend `7be9ee3b45ac54bc3c7d642038fc77cf794ea944` and frontend `3823b39aaf6736eb41cd097e0130c0d8a3d31f7c` plus its compatibility anchors. Result: **no actionable P0/P1/P2 or minor findings**. The parent coordinator independently checked the backend candidate: **11 Markdown documents, 49 added local links including 4 anchors, 0 errors**. B06 local runtime remains **0 tests executed / NOT RUN**; subsequent hosted SQL-free CI is recorded below. This review is not Owner acceptance, merge or production approval.

The follow-up changes only review metadata and the direct frontend user-guide pin; the guide content at the pinned frontend SHA is the reviewed content. Final patch verification passed `git diff --check`, changed-path review and the inline Python check against the baseline: 11 Markdown files, 49 added local links including 4 anchors, 2 external URLs, 0 errors, UTF-8 without BOM/LF. The pinned frontend guide was verified with `git show` from the local frontend Git object; no remote availability is claimed. The follow-up diff changes only three Markdown files; no local runtime execution is added.

## Hosted CI and paired PR delivery — 2026-11-02

Backend [draft PR #85](https://github.com/chung6a8m/MyDmsVn/pull/85) is paired with frontend [draft PR #22](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/pull/22). [Hosted PR run 41377146596](https://github.com/chung6a8m/MyDmsVn/actions/runs/41377146596), job `125059754182` (`http-host`, Windows2022), **PASS** at PR head `9defa0fa9ae60ae618addece8949b1a46d439fc1`. The log checkout was PR merge ref `45bfe0ee6fa577b4905b510a0f68062c3d1503a0`. The coordinator fetched `refs/pull/85/merge` and verified both merge ref and PR head have tree `1440449c35dd0ddfb6252e71161b7836f59235ba` using `git rev-parse 'FETCH_HEAD^{tree}'` and `git rev-parse '9defa0fa9ae60ae618addece8949b1a46d439fc1^{tree}'`. The coordinator inspected the hosted logs and supplied these exact nonempty-complete execution summaries:

| Hosted net48 suite | Total | Executed | Passed | Failed | Not executed |
|---|---:|---:|---:|---:|---:|
| http-host | 506 | 506 | 506 | 0 | 0 |
| recovery-fixture | 62 | 62 | 62 | 0 | 0 |

The existing [workflow](../../../.github/workflows/backend-web.yml) uses locked restore, Release/net48 builds and the following commands. It explicitly clears `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING`; the Security project invocation selects only the four pure validator classes. No SQL owner, IIS fixture, browser, publish or deployment was executed by this workflow.

```powershell
dotnet restore tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj --locked-mode
dotnet build tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --no-restore
dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --no-build --no-restore --logger "trx;LogFileName=http-host.trx" --results-directory TestResults/backend-web
dotnet restore tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj --locked-mode
dotnet build tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore
$filter = 'FullyQualifiedName~WebRecoveryPilotResultTests|FullyQualifiedName~WebIntegratedPilotResultTests|FullyQualifiedName~WebReceiptPilotResultTests|FullyQualifiedName~WebFrontendPortBridgeTests'
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-build --no-restore --filter $filter --logger "trx;LogFileName=recovery-fixture.trx" --results-directory TestResults/backend-web
```

These hosted counts belong only to the exact head/ref/run above. This evidence-only follow-up does not claim a later-head CI result or add another test execution. The document was checked locally for whitespace, local links/anchors, UTF-8 without BOM/LF and evidence-only scope. PR delivery and hosted SQL-free PASS do not grant Owner acceptance, merge, B4-OPS-02 rehearsal or production approval.

## Disposition and retained limits

Candidate is review-ready after independent review; Owner disposition remains pending. Same-key resend, negative administrative unlock, production operator tooling/audit/retention deployment, backup/restore drill, distributed limiter/proxy policy, full IIS and production release remain unapproved or unverified. B06/F05 completion routing and archive remain conditional on actual Owner acceptance; no earlier WEB1–03 or independent CEX status changes.
