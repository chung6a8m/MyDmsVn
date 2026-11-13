# WEB6 B05b — paired candidate, cutover, workload and recovery evidence

**Latest disposition (2026-11-13): attempt16 reached PairPassed (Chromium and Edge native ACCEPTED), then FailedRetained during cutover epoch seed.** Both pair cleanups were accepted. Cutover allocations remain retained. Workload/recovery NOT RUN, CandidateVerified absent, Task 6 OPEN.

## Task 6 attempt16 browser pair PASS and cutover epoch seed failure

The Owner authorized one fresh nonproduction attempt for candidate `web6-20261113-8d732d8c`. The exact backend source `4644b8e9416c2e34d8b7a57e40cdd62e35b052be` / tree `9a4b4a8aef63577a9e347d0c50c7d49a5c398ebc` and frontend source `5f355e4ed3d0ca0778e195b940ecbed67b410dc0` / tree `47dc71f900a4977b953f6850f943fde12d64e632` were prepared in clean detached worktrees. The frontend donor HEAD `935b03ed80d7b3e5f2c958e6e37f1a2b474bae61` was used as the documentation base.

The previous private tool path was absent. An authorized copy-only replacement from installed Node 22.14.0 and npm 11.6.0 matched the reviewed whole-tree digest `c55ed76c2333bcb335b2327f639195464c2ed7685b329da812f02c85bed29529` and npm subtree digest `e254ea5bde41caff094a51ea37c52114e5075d4ea4d5c1864944676c4620eb58`. All 2,337 files and 572 directories passed identity, no-link and current-user-only protected ACL checks. Four read-only Edge observations over 30.210 seconds matched identity/version/hash. A bounded read-only query verified approved effective master. The Windows PowerShell/net48 helper issued a new protected `web6-cutover` SQL approval, expiring `2026-11-13T18:46:15Z`, and ran authorize once, exit 0.

Prepare ran once 10:46:16.4414507Z–10:49:29.0417247Z, exit 0 / `PIN_MATCHED`. The independent check verified ordinary resend OFF, the exact candidate marker, all 10 dist files equal to the complete published wwwroot, and zero live allocations. Run began 0.830 seconds later and ended at 10:54:21.8703337Z, exit 5 / `FailedRetained`. The browser pair used the reviewed 35/50/65-second boundary, 65/35-second one-effect, 170-second mismatch and 32/39/41/44-minute time limits.

| Selector | Discovered / executed / passed / failed / skipped | Outcome |
| --- | --- | --- |
| Chromium pair | 1 / 1 / 1 / 0 / 0 | PASS / native ACCEPTED |
| Edge pair | 1 / 1 / 1 / 0 / 0 | PASS / native ACCEPTED |
| Cutover | 1 / 1 / 0 / 1 / 0 | FAIL / current allocation epoch seed, before host/browser launch |
| Workload | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |
| Recovery | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |

Cutover accepted the parent-supplied `PairPassed` digest and the separate artifact/source scopes. It completed the guarded allocation sequence and confirmed the current, old and restored databases and their three runtime logins. During the current allocation seed, `WebHostIisExpressTests.Cutover.cs:159` threw `System.IO.IOException: Fresh current allocation epoch was not initialized.` because the epoch update's `ExecuteAsync` result failed the one-row check. Cleanup threw `Incomplete bootstrap cutover retains every allocation after bounded drain.` and the TRX contains both errors in an aggregate. The terminal receipt reports `processTreeDrained:true` and `requiresDispositionProof:true`.

The admitted provisioning script starts with `SET NOCOUNT ON`, leaves it enabled and runs on the same open connection as the epoch update. Migration 012 inserts the singleton epoch row. The return-value check is therefore consistent with a suppressed affected-row count. The TRX does not capture the returned integer or the row's persisted state. No cutover deployment, IIS/browser launch or lifecycle pack was reached.

| Receipt or artifact | SHA-256 |
| --- | --- |
| Release/net48 bootstrap | `0193187fe87b84ed4a130bc825babccfe0ac436022bdc8ffcb1b2b9bb606ceaf` |
| New SQL approval | `58e81d6bbd6fb13574e10e546bf1e8eca70df696a3383ee99e95f51d9fdb0db1` |
| Owner authority / request | `87ec530139f263593c099305a0b52570d32fe01af757f367bbcba5b249a35092` / `ebe5b1b895d422f185e96ae7ec4866a8590e7d03c21640104149a870736d25a7` |
| Prepared / PairPassed | `ba23575e22b600768d1b375e446635f8143eb243d7e3f285a0fbc38e30a9402d` / `18983f570ab895626134cd1cb65b78b363d8d39ae296cc985fea00419163d82c` |
| FailedRetained | `0442269c4fac79e7b9aa8efaf951b2976abc48683e685d56f174764e9d4ac78a` |
| Ordinary dist / manifest | `5c7908157609e24760dcab5da26b170f5194f1918f2343f80aa164aab0ef59ab` / `796d86094b6cfd29efa1d99aac49bd9fd10bb80c73b71d0164d853e74485ef53` |
| Candidate dist / manifest | `6ffbaee981996ace95efd728fb4c477359f954e74f216b4c9981a833b14dcd49` / `250e9a7517bf1a671fa88e4241d0faeea711f3ea64ea2a104884f467318f8410` |
| Current / old backend publish | `d8e1210460c4418a0048ca1809739c8e38b40525daa9536fd87255f83afab592` / `20892378d68b08c4aba65520a8f1f724278b3b033072909199f0361fadfffa79` |
| Archived frontend dist / cutover build authority | `da573ea101e95b9a98850cc1b29270d89a24e6f4d54a8c2044949cfdb9289882` / `9e41389161bc17149dddc2649a5e7855b2bdd715bc2b7395bbfb0527f60daa0e` |
| TLS certificate / selector DLL | `47985af0a6345e7c8df22aacbea36cf7cb12d924eba42704cb6997745470f647` / `b9373ef49b67d0b5b4d2f595c8c073ad6214ca3a1d5eccf0742bcf86a2c0a181` |

| Selector | TRX SHA-256 | Native / envelope SHA-256 | Cleanup inventory SHA-256 |
| --- | --- | --- | --- |
| pair-chromium | `cc02228c4e8470954761ff83b02ca8485ea9e7bde56a71ec027d8eed02458918` | `b9b410e754c90484535e40f75bccea43206096c3f5b8232be5fa4ce3c3f74df7` / `36899ba1c5d664138c51edae2b276bfd714943fc9fbcc8253635d7b9977e5591` | `414421d85683f06703948de256bf03510952af59b934b785bc273791efc72613` |
| pair-edge | `6607f10a9884db38d7325f1847aeac8da0840ca9b6111df8c6e57988c292cb26` | `a3334e0f84cce6f7b7de8e11f5dbb6c4c89c8791203a37eb7d075a5bf326c8a3` / `95a1159e658aa011d523a94d91951f7e4def58708a4aecc7a44af3434ddb236e` | `f61be89b94ed34dd70e912973d0e4ad3531d11eaeecd9cfa3a482d0fd737da7e` |
| cutover | `3cc8706d5a6f42c7479fbc625be8649a19694f53e9bf2cb9fe5bd4e117d205a3` | Absent | Absent |

| Cutover allocation | Intent SHA-256 | Confirmation SHA-256 |
| --- | --- | --- |
| current database | `e47120f2bd1c96181afa83e0ff58dce8b48fdb8983ddefc860c2deaa95cec95f` | `bc528310d0febac741b7ac05404afafa7ef230a2a4c13c9144760fab444fc47f` |
| current login | `b5de89db0c17a1a74efb0c294757da43b60ffc3ae9b1e9a5c693212409e7269e` | `c913a156b72e454e41b05a68d004cec4454709b1e2812a9534c5d79c669a0031` |
| old database | `eeabf417730311c1ff9371be9fd6e38e6265d4024401ffaf4711aab8f89d0eaa` | `476745724c2e33815038b7d94076fa6fe4c1d3e48b84e2a8c6d967eae87ed163` |
| old login | `e7c5ef80cbbb467d51a93bc11ac7ee54a8676951ca25d87f9a9263f28c0270af` | `f565c1d410bfa286ddf7cb76c7a6b5d00f18ecf81f61eba05efe2b59197e0058` |
| restored database | `571fd0b377b10fcec0b723b04c198d1a05873c526f0e9d4f8abe1395e835689d` | `c21cc6716deebe6936e1658cde8ebbd739b252d7e32e112f33e30c4399e7f035` |
| restored login | `bf11779e38109d06e87622feac6c0f94e8d39dc054cd3e9926800b63e4df9ce2` | `a7f0d34f59cd4e792e4b6acce11713785eee0a68474dad2bb2d9d77feee90311` |

Both pair receipts certify accepted cleanup and bind their exact native result and inventory. Each inventory records one database, one login, three processes, three queues and one deleted runtime directory. Both runtime directories were observed absent. Cutover retained three owned databases, three runtime logins and three SQL control roots with the six intent/confirmation pairs above. Each confirmation references the exact intent hash. These records carry `deletionAuthority:false`. Cutover has no cleanup inventory, native acceptance or Task4 receipt. The new tool layout, prepared artifacts, approvals and private evidence remain retained. There was no retry, post-failure SQL query, manual cleanup, external termination or access to earlier protected attempt records or resources.

Workload has no sample floor or percentile evidence. Recovery has no observed RPO/RTO. Final independent disposition and `CandidateVerified` are absent. Task 6 and B05/F04 remain open. Ordinary resend stays OFF. Full IIS and unperformed Narrator remain NOT RUN. Staging remains NOT ACCEPTED and production NO-GO.

Environment: Windows 11 x64 build 26300, .NET SDK 8.0.428, SQL Server 14.0.2150.2, IIS Express 10.0.26013.1000, Chromium 156.0.8078.4, Edge 157.0.4341.46, Node 22.14.0 and npm 11.6.0. [Frontend evidence](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/codex/web6-f04-paired-cutover/docs/evidence/web6/20261110-f04b-measured-quality.md#task-6-attempt16-browser-pair-pass-and-cutover-epoch-seed-failure).

## Task 6 attempt15 browser pair PASS and cutover build-authority failure

The Owner authorized one fresh attempt after independently reviewed boundary/parent budget hierarchy. Candidate `web6-20261110-f1330b4a` used backend `08123da11f21f57be73cd24992a57d7bce9c6d61` / tree `64e22c22e283ccbcfa19969277458bbbcf266825` and frontend `5f355e4ed3d0ca0778e195b940ecbed67b410dc0` / tree `47dc71f900a4977b953f6850f943fde12d64e632`. Reviewed budgets: boundary 35/50/65s, one-effect dispatch 65s/response 35s, mismatch 170s, third test 32m, controller 39m, browser process 41m, pair gate 44m. No external termination occurred; selectors ran serially once.

Before new roots/approval, four ordinary read-only Edge observations at `14:23:50.4361114Z`–`14:24:20.5493631Z` (30.1132517 seconds) matched identity/version `157.0.4341.46`/SHA-256 `3e2c8557b5ca4d78868d3089346146ceab94f198ce3d58526ce8f6c8d4b4167c`. Full independent Node `22.14.0` / npm `11.6.0` identity/hash/current-owner protected ACL/no-link checks passed for all 2,337 files/572 directories, tree `c55ed76c2333bcb335b2327f639195464c2ed7685b329da812f02c85bed29529`. A bounded query reverified approved effective master/server. No custom lock/copy/updater change, installation or global mutation occurred.

Fresh roots, new four-field `web6-cutover` SQL approval, Owner authority and request used Windows PowerShell/net48 protected create-new publication. Exact current-user-only protected ACL/schema/digest/master binding passed; eight-hour expiry `2026-11-10T22:24:34Z`. No old protected payload/resource was inspected, copied, reused or adopted. Authorize once exited 0. Prepare once ran `14:24:50.0741073Z`–`14:27:36.1663528Z`, exit 0 `PIN_MATCHED`. Independent checks confirmed ordinary null candidate ID/OFF, exact candidate marker, ten-file full dist/wwwroot equality, clean detached pins and zero pre-run live allocation.

| Attempt15 record | SHA-256 |
|---|---|
| Release/net48 bootstrap | `cb1cccdaa8a15f16f69143416aa7b0d3ffba61cbcc58ab925fd0c5a64f961d8c` |
| New SQL approval | `f1a94713d3d433b174361735a321baa630a3d23af4c886693dd14ebcb94667b0` |
| Owner authority / request | `546a213df0be4fd44a120cddfbd10c2a6b05c1fcdfb510a5c67d643ccb039fca` / `26245a14fa06bb41a3ea61b8d26516743c1ce4c1c916353eb5e4075d3d638514` |
| Prepared / PairPassed | `6976f8a22906abb9213b4175c5cd8847b97a221d3921db3c3ef547cbc928e3b1` / `121305576939a4acba2872b9c43df7233ceac2a7fe28a7f736daaa7dc6c44d80` |
| FailedRetained terminal | `03e9cbc1f87284cac0f597752bff681c0e4079448212c9e94491139921fb3bff` |
| Ordinary dist / manifest | `5c7908157609e24760dcab5da26b170f5194f1918f2343f80aa164aab0ef59ab` / `796d86094b6cfd29efa1d99aac49bd9fd10bb80c73b71d0164d853e74485ef53` |
| Candidate dist / manifest | `53c88f584e218a6fcb5a30729bb716a1de0dff65abb86608c450a1bbf8bb8473` / `23c4ec5e30801aa4db0733d37d45dfb7157a1e9af7c47604b7229ac73c2e568a` |
| Current / old backend publish | `6402c9e96445d0eff484bd28da0d1b34fde8b81dbef682171db80b28f171ef73` / `05a64c2412b541f14b4c6a712f70a0cafdada1982d16bdf15dd28a7c5a93f7c5` |
| Cutover build authority | `2d4e75842aaeadd271dc3bee99e0cdd3b72664f8262c58f3a4de6f0146201732` |
| Selector DLL | `d2b30b0017f53e4eed8aa82ae8b51c839264a5593d52f90dce16aa40d1479547` |

Run began `14:27:36.9575335Z`, **0.791 seconds** after prepare, and ended `14:32:58.1174857Z`, exit 5 `FailedRetained`; run ID `run-8f075dd55517465bb8b305c1216680d9`. Both browser selectors passed with native `ACCEPTED` receipts and exact hash-bound envelopes. The state reached `PairPassed`, then stopped at cutover; no later selector ran.

| Required selector | Discovered / executed / passed / failed / skipped | Result |
|---|---:|---|
| Chromium pair | 1 / 1 / 1 / 0 / 0 | PASS / native ACCEPTED |
| Edge pair | 1 / 1 / 1 / 0 / 0 | PASS / native ACCEPTED |
| Cutover | 1 / 1 / 0 / 1 / 0 | FAIL / preallocation build-authority guard |
| Workload | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |
| Recovery | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |

Chromium selector ran `14:27:57.8968787Z`–`14:30:23.5935539Z` (TRX duration 134.450s), native acceptance `14:30:22.7879398Z`. Edge ran `14:30:27.1988064Z`–`14:32:44.9546728Z` (TRX duration 129.657s), native acceptance `14:32:42.6470948Z`. Cutover ran `14:32:48.5599954Z`–`14:32:58.0375909Z` (TRX duration 2.416s), child exit 1, `NonzeroExit`; terminal records `processTreeDrained:true` / `requiresDispositionProof:true`.

| Gate receipt | TRX SHA-256 | Native / envelope SHA-256 |
|---|---|---|
| pair-chromium | `1db773c0762c45e6a67799949bae32c595e034a5c709ff2505949fa664559e96` | `b903253c458803aa3092c3f48efc451482e80959f61ba86930c3acbe0c47425c` / `133b64d5d5efffa666303387df6d721b306ad5c2aa4017ee89f6c3ab3a2f9a83` |
| pair-edge | `7aa847c1486683a0dd5c69c17d9dc6e27c6074d3f4d59a6636d958b292ba3c64` | `d7fce4e76974bf6bf886648ccb0df60af437caf7a4b75b6cf979e46cacbe4541` / `f9a01578cce371b4d9b3e84b78384d3fe3c652950329060ea56b821d795764e2` |
| cutover | `aa44de3020c2cd17881f444251fb6f00c620c7cf158448e0c586160eed880eb1` | absent |

Cutover exception is exactly `System.IO.IOException : Cutover sealed build authority or published artifact drifted.` at `Web6CandidateManifest.cs:361` via `WebHostIisExpressTests.cs:631`. This guard runs before cutover SQL approval/allocation or host launch. No SqlException appears.

Read-only diagnosis remeasured current attempt artifacts with the native `Web6CandidateFileSet.MeasureTree` and compared the protected build authority. Both archive source/tree/clean checks and every artifact-to-sealed-authority comparison passed. The sole failing guard comparison is `actual.Frontend.ArtifactHash != actual.CurrentPublishedHash`: archived frontend dist `da573ea101e95b9a98850cc1b29270d89a24e6f4d54a8c2044949cfdb9289882` versus full current publish `6402c9e96445d0eff484bd28da0d1b34fde8b81dbef682171db80b28f171ef73`. Both still match their separately sealed expected hashes. This identifies an artifact-scope contract mismatch; no changed artifact bytes were observed. No authority/source patch, digest replacement, retry or post-failure SQL query occurred.

Both pair native receipts record `processDrained/httpQueuesAbsent/sqlQuiescent/resourcesVerified:true`. Each inventory contains one database, one login, one deletedDirectory, three process and three queue records. Pair cleanup was accepted by those receipts; both recorded runtime directories are absent and the current SQL control parent has zero children. Inventory `deletionAuthority:false` is non-authorizing evidence, not permission for additional cleanup. Cutover allocation receipt/root are absent; workload/recovery roots and final independent disposition are absent. Prepared worktrees/artifacts, new authorities and receipts remain retained. The overall run is `FailedRetained` after `PairPassed`; do not label the accepted pair cleanup as Quarantined. No manual cleanup, historical-resource inspection/adoption or ownership transfer occurred.

Environment: Windows 11 Home x64 build 26300, SDK `8.0.427`, SQL `14.0.2140.2`, IIS Express `10.0.26013.1000`, Chromium `156.0.8078.4`, Edge `157.0.4341.46`; Node/npm above. Workload sample-floor/percentile evidence, observed RPO/RTO, final independent disposition and CandidateVerified are absent. A reviewed cutover correction plus new explicit Owner authorization is required before another fresh candidate/root/approval chain. Ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO. Task 6 and B05/F04 remain incomplete.

## Task 6 attempt14 candidate-mismatch observation failure and quarantine

The Owner authorized one fresh attempt after independently reviewed one-effect response/commit correction. Candidate `web6-20261110-37528a15` used backend `a5f264dd5ca3108a3be50fd8bf60c5d82dcc01af` / tree `e2c243894c385c579f4596a262400fb7105f9842` and frontend `0dc8a2ff0d88218af9bc156298d971fd3e60e715` / tree `3c9cf852e12b3dc6c6cd01c4f25912284ba8a82d`. The revised helper must observe the successful completed response of the exact mutation Request before verifyOneEffect, with exactly one dispatch. Reviewed 50/65/35/170/480-second budgets remain.

Before new roots/approval, four read-only Edge observations at `13:36:37.0762219Z`–`13:37:07.2100765Z` (30.1338546 seconds) matched identity/version `157.0.4341.46`/SHA-256 `3e2c8557b5ca4d78868d3089346146ceab94f198ce3d58526ce8f6c8d4b4167c`. All 2,337 tool files/572 directories passed independent identity/hash/current-owner protected ACL/no-link checks: Node `22.14.0`, npm `11.6.0`, tree `c55ed76c2333bcb335b2327f639195464c2ed7685b329da812f02c85bed29529`. A bounded query independently reverified approved effective master/server. No custom lock/copy/updater change, installation or global mutation occurred.

Fresh roots, new four-field `web6-cutover` SQL approval, Owner authority and request used Windows PowerShell/net48 protected create-new publication. Exact current-user-only protected ACL/schema/digest/master binding passed; eight-hour expiry `2026-11-10T21:37:23Z`. No old protected payload/resource was inspected, copied, reused or adopted. Authorize once exited 0; prepare once ran `13:37:37.9584709Z`–`13:40:21.5773439Z`, exit 0 `PIN_MATCHED`. Independent checks confirmed ordinary null candidate ID/OFF, exact candidate marker, ten-file full dist/wwwroot equality, clean detached pins and zero pre-run live allocation.

| Attempt14 record | SHA-256 |
|---|---|
| Release/net48 bootstrap | `edb039e62c4ee9ead11612dc1daa56a356dce65bcfc748b10ab7f8f1a8e12e54` |
| New SQL approval | `2a3e98fc47c271a5f43d6d9a07078bebd8fa36fe094e814aba3221b123ac2b8e` |
| Owner authority / request | `2241e9ba65d65bac003e47fe0798e6c736b9498a5d15d054684c329cd7e65ef7` / `69a946658d76f123b8729118433532ab8c0d1c08637176f682cef14d73c99a3d` |
| Prepared / terminal receipt | `fac7d517d26fd5feca7049c6dc7b476c3d8072f92593ff4a2d9d4a7aadbe2d20` / `e36421c41c20a18a206badb6e08b1b0385b653e41500def7ed0e0b0da2fb32a1` |
| Ordinary dist / manifest | `5c7908157609e24760dcab5da26b170f5194f1918f2343f80aa164aab0ef59ab` / `796d86094b6cfd29efa1d99aac49bd9fd10bb80c73b71d0164d853e74485ef53` |
| Candidate dist / manifest | `8f2ae915068411067fb661a751c493efd25c6143581383c073210c72889f843c` / `2911851f2b1da038908429526679318ae610201ee89a4c20e678d808f46235cb` |
| Current / old backend publish | `d7ac1100f39bed99c5dd581fc73f0ed1d890aa428d1e2e03d38ecf771075759c` / `955a62da249f11813d77f88b395df6ff02e67fb86e8855edb1cbb0cae115a81f` |
| Cutover build authority | `b2531c9ed65a10b29ff26191bdcee510cd94629744677e982394e71ad5b47429` |
| Chromium wrapper TRX / selector DLL | `41c8d560f4c1c454ddde5921fe8f2edc11ab9e3e11dcc2e9da3683aa4d7522ee` / `8d97593b940324813ff8922138254563837651ddeccfcdc35c0575880a81673d` |

Run began `13:40:22.3475904Z`, **0.770 seconds** after prepare, and ended `13:42:26.5325218Z`, exit 5 `FailedRetained`; run ID `run-a67f00cc0d6c451baaab15f867d52fa0`. Sole Chromium selector: `13:40:42.1835679Z`–`13:42:26.5069515Z`, wrapper duration 91.287 seconds, child exit 1, `NonzeroExit`, `processTreeDrained:true`, `requiresDispositionProof:true`. No external termination occurred.

| Required selector | Discovered / executed / passed / failed / skipped | Result |
|---|---:|---|
| Chromium pair | 1 / 1 / 0 / 1 / 0 | FAIL / FailedRetained |
| Edge pair | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Cutover | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Workload | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |
| Recovery | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |

Nested Chromium selected three tests/one worker: **2 passed, 1 failed, 0 did not run**, displayed duration `54.5s`, plus one reporter error outside a test. Six-operation explicit consent and committed-response loss passed. The first case exercises the revised response/commit observer. The third case failed `WEB6 boundary=candidateMismatchOn action=prepare HTTP/auth/recovery contract` at `web6-manual-resend-live.spec.ts:263:79` / `web6-boundary-observer.ts:69:19`, after a 5,000-ms predicate window. Exact sanitized state and observer trace:

~~~json
{"contextStatus":null,"eligibilityStatus":null,"refreshStatus":null,"loginVisible":false,"recoveryVisible":true,"resendEnabled":false,"confirmationVisible":false,"additionalMutations":0}
{"phase":"poll","events":["attached","control-start","control-done","trigger-start","trigger-done","poll","request:trigger:context"],"omittedEvents":0}
~~~

The predicate requires context 200, no eligibility/refresh response, recovery visible and resend enabled for this boundary. A trigger context request was observed; no correlated response was observed before predicate failure. Null statuses do not establish no request, a server outcome or root cause. No additional mutation or confirmation was observed. The third-case 401/503, grant, two-tab exact-response/one-effect, IME and reload/offline tail were not reached. No one-effect failure diagnostic, socket-control failure diagnostic or SqlException appears. No source patch, wait/status relaxation, retry or post-failure SQL query occurred.

Candidate records are `Chromium=Failed` and `Cleanup=Quarantined`. One original SQL database/runtime login/control root, host-runtime/config/browser log, candidate manifest/audit/dispositions and prepared artifacts remain retained. Allocation receipt `c7e20bf7931b6a21466b00f2aa416e09df41e514efebdf825a805e9e4d32eec0` binds control digest `d2a8766a2a73f5cb59bfb7ccb38c2544a6422548a60ce693b7f069e96825d325`. Later live roots/native success envelopes are absent. Process drain is not accepted SQL/artifact cleanup or final independent disposition. No manual cleanup, historical-resource inspection/adoption or ownership transfer occurred.

Environment: Windows 11 Home x64 build 26300, SDK `8.0.427`, SQL `14.0.2140.2`, IIS Express `10.0.26013.1000`, Chromium `156.0.8078.4`, Edge `157.0.4341.46`; Node/npm above. Workload samples/percentiles, RPO/RTO, Task4 acceptance and CandidateVerified are absent. A reviewed boundary correction and new explicit Owner authorization are required before any fresh candidate/root/approval chain. Ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO. Task 6 and B05/F04 remain incomplete.

## Task 6 attempt13 committed-identity verification failure and quarantine

The Owner authorized one fresh attempt after independently reviewed eligibility-unavailable HTTP503 correction. Candidate `web6-20261110-85909668` used backend `9823bb4f78f81f5b725c23e94f75aa4e7a8a4702` / tree `c16b1d1c998c23088fe595caa1cf7e11a4bda9ee` and frontend `9a1a2ba17414fabadace5edfb16e85ade6ecffbc` / tree `9c7568e580e78076e39497f8a8a362a2f006b3c3`. Historical action `eligibility500` requires trigger-request-correlated HTTP503 / `Operation.Unavailable`, protected UNKNOWN/resend candidate and zero additional mutation. Reviewed 50/170/480-second budgets remain.

Before new roots/approval, four read-only Edge observations at `12:50:08.6896244Z`–`12:50:38.8196954Z` (30.130071 seconds) matched identity/version `157.0.4341.46`/SHA-256 `3e2c8557b5ca4d78868d3089346146ceab94f198ce3d58526ce8f6c8d4b4167c`. All 2,337 tool files/572 directories passed independent identity/hash/current-owner protected ACL/no-link checks: Node `22.14.0`, npm `11.6.0`, tree `c55ed76c2333bcb335b2327f639195464c2ed7685b329da812f02c85bed29529`. A bounded query independently reverified approved effective master/server. No custom lock/copy/updater change, installation or global mutation occurred.

Fresh roots, new four-field `web6-cutover` SQL approval, Owner authority and request used Windows PowerShell/net48 protected create-new publication. Exact current-user-only protected ACL/schema/digest/master binding passed; eight-hour expiry `2026-11-10T20:50:58Z`. No old protected payload/resource was inspected, copied, reused or adopted. Authorize once exited 0; prepare once ran `12:51:15.2878979Z`–`12:53:57.4554080Z`, exit 0 `PIN_MATCHED`. Independent checks confirmed ordinary null candidate ID/OFF, exact candidate marker, ten-file full dist/wwwroot equality, clean detached pins and zero pre-run live allocation.

| Attempt13 record | SHA-256 |
|---|---|
| Release/net48 bootstrap | `5982981a74a2570cb99a27d06513d41e6d0b98195bb5cabcd6321d88245146d1` |
| New SQL approval | `0e8429b16d4f179158c2d7ac6fd0f88ce61c6a0b2cb8ea8f36842f12f115505b` |
| Owner authority / request | `64bef34dc5b1264bab00093f100f0d09349a8d883d8bc194143c468264f5be4b` / `952eaf95b3187eed616df2d092cec329e45a655ca6ed70e5b8bf8d40f14adc59` |
| Prepared / terminal receipt | `7accfe43981c680123a05ed0e554be8163f2bf1965cfc8c19e7a9882eb6d5ca2` / `c9a5fd3c176ed318d52f502c4fbd605f3c008919a0ca3eb616d11a881ee349fe` |
| Ordinary dist / manifest | `5c7908157609e24760dcab5da26b170f5194f1918f2343f80aa164aab0ef59ab` / `796d86094b6cfd29efa1d99aac49bd9fd10bb80c73b71d0164d853e74485ef53` |
| Candidate dist / manifest | `94a6d7257a9fd9e40e701938a1cbdd569d1d79f06b3f82515ed8597d2d593945` / `9f146a4ac8834f0a1e5d4326b07004ee05becf7cf64b35cc6ebf36a8bbb5ac31` |
| Current / old backend publish | `117602c35e7f3aabefddaad62aa3d462624f018d58ed2531cdbe17c950b0524c` / `c450dc51091269914a5601aecf9453e834164ce3709c0eb9de0289387e420039` |
| Cutover build authority | `0aa224b30054d5a17fb5ed220659d164f2ce52d51edd62d48e20c453e3ad7772` |
| Chromium wrapper TRX / selector DLL | `bca8335624acd312e66ab44f0507d4be42f9fe99512c9285123c49b83a8ddf32` / `2ff0ea946b49fa145ed19489973a4db3f3107b66aa2368fb574a2f18b55c9896` |

Run began `12:53:58.2630867Z`, **0.808 seconds** after prepare, and ended `12:56:10.1054295Z`, exit 5 `FailedRetained`; run ID `run-a83c9de4089d4badaf5d5b8dc613f61f`. Sole Chromium selector: `12:54:18.2381468Z`–`12:56:10.0755858Z`, wrapper duration 99.526 seconds, child exit 1, `NonzeroExit`, `processTreeDrained:true`, `requiresDispositionProof:true`. No external termination occurred.

| Required selector | Discovered / executed / passed / failed / skipped | Result |
|---|---:|---|
| Chromium pair | 1 / 1 / 0 / 1 / 0 | FAIL / FailedRetained |
| Edge pair | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Cutover | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Workload | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |
| Recovery | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |

Nested Chromium selected three tests/one worker: **2 passed, 1 failed, 0 did not run**, displayed duration `1.1m`, plus one reporter error outside a test. Six-operation explicit consent and committed-response loss passed. The third case failed in candidate control with this exact allowlisted diagnostic, no optional code:

~~~json
{"action":"verifyOneEffect","terminal":"eof","phase":"response-header","elapsedMs":10,"category":"incomplete-response"}
~~~

The current wrapper TRX records `InvalidDataException` with `Candidate committed identity is missing or duplicated.` and the drain aggregate `Recovery fixture drain failed.`; no `SqlException` appears. For this action, `WebRecoveryPilotControl.cs:188–190` rejects a missing expected operation, a different operation kind, or journal count other than one before the business-effect oracle. Current evidence does not distinguish those three conditions. The EOF itself does not prove a timeout, a duplicate effect or absence of a committed operation.

Source flow reaches this `verifyOneEffect` after the boundary loop (including trigger-correlated HTTP503), grant checks, two visible consent dialogs, IME suppression and one counted consent dispatch across the two pages. Those earlier steps returning is a control-flow inference, not full native pair acceptance or proof of a completed mutation response/commit. The request counter alone does not establish commit completion. Later reload/offline tail did not complete. No assertion/status relaxation, source patch, retry or post-failure SQL query occurred.

Candidate records are `Chromium=Failed` and `Cleanup=Quarantined`. One original SQL database/runtime login/control root, host-runtime/config/browser log, candidate manifest/audit/dispositions and prepared artifacts remain retained. Allocation receipt `b376e6bf46e1e5e22a0989ef5dc529825e02ff4b95f919184b7312096aba30ae` binds control digest `1580e9d22eb75340f3836a71647744e209894920ddc2fe5c63a89ac6d0ef928e`. Later live roots/native success envelopes are absent. Process drain is not accepted SQL/artifact cleanup or final independent disposition. No manual cleanup, historical-resource inspection/adoption or ownership transfer occurred.

Environment: Windows 11 Home x64 build 26300, SDK `8.0.427`, SQL `14.0.2140.2`, IIS Express `10.0.26013.1000`, Chromium `156.0.8078.4`, Edge `157.0.4341.46`; Node/npm above. Workload samples/percentiles, RPO/RTO, Task4 acceptance and CandidateVerified are absent. A reviewed boundary correction and new explicit Owner authorization are required before any fresh candidate/root/approval chain. Ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO. Task 6 and B05/F04 remain incomplete.

## Task 6 attempt12 correlated eligibility status mismatch and quarantine

The Owner authorized one fresh attempt after independently reviewed request-correlated eligibility observer correction. Candidate `web6-20261110-3fff3d2f` used backend `0711d0b2fcd44b86fef48cc9dae5dadfc0a7089d` / tree `dc1fff8c5462c9a5aa4131b86f296429d75da192` and frontend `4a6541c419b1293a04c57d67bc6faaf1254ba391` / tree `296cae43cea74136b97fee38ffe4e08a20dddea2`. Only request-identity-correlated trigger responses can decide boundary PASS; control/pre-setup/unobserved traffic is excluded. Reviewed 50/170/480-second eligibility401/mismatch-header/third-case budgets remain.

Before new roots/approval, four read-only Edge observations at `12:20:15.6465496Z`–`12:20:45.7694443Z` (30.1228947 seconds) matched identity/version `157.0.4341.46`/SHA-256 `3e2c8557b5ca4d78868d3089346146ceab94f198ce3d58526ce8f6c8d4b4167c`. All 2,337 tool files/572 directories passed independent identity/hash/current-owner protected ACL/no-link checks: Node `22.14.0`, npm `11.6.0`, tree `c55ed76c2333bcb335b2327f639195464c2ed7685b329da812f02c85bed29529`. A bounded query independently reverified approved effective master/server. No custom lock/copy/updater change, installation or global mutation occurred.

Fresh roots, SQL approval, Owner authority and request used Windows PowerShell/net48 protected create-new publication. Exact four-field `web6-cutover` schema/current-user-only protected ACL/digest/master binding passed; eight-hour expiry `2026-11-10T20:21:00Z`. No old protected payload/resource was inspected, copied, reused or adopted. Authorize once exited 0; prepare once ran `12:21:15.6025874Z`–`12:23:53.3579636Z`, exit 0 `PIN_MATCHED`. Independent checks confirmed ordinary null candidate ID/OFF, exact candidate marker, ten-file full dist/wwwroot equality, clean detached pins and zero pre-run live allocation.

| Attempt12 record | SHA-256 |
|---|---|
| Release/net48 bootstrap | `4f584f889b01f440ef0147beb11573bc60e6e9c9419c240736a938c4345ce8d7` |
| New SQL approval | `98c2de524b96e82ff6dc32587b5c831a8e302742b281d4bb2e7de5cb3672ea22` |
| Owner authority / request | `b5b1c15c33ae6035179db224bca88a3e0be757baa01a9499e06ffb3ffc316828` / `a2207230ec0695bf9e611ed490233f74a0a3674851e1f1d40af3ab621943380d` |
| Prepared / terminal receipt | `c33ae71822620a90fa247c62505178293a24427486ad8df27b47c233f6a72bd4` / `806abca4f404fa70267ba2dd8ee88c9cad3dacd787eb4c21d4909c6bc3a5470c` |
| Ordinary dist / manifest | `5c7908157609e24760dcab5da26b170f5194f1918f2343f80aa164aab0ef59ab` / `796d86094b6cfd29efa1d99aac49bd9fd10bb80c73b71d0164d853e74485ef53` |
| Candidate dist / manifest | `fdd540c464c9e4a80a18a69319fc785db4251ff79d205944638d2052b2ed1dde` / `b1b1af29d29758d46bb3d0e0645f3442e4f065416d41508b3a6a9478d72f74f0` |
| Current / old backend publish | `31362a3290a13d528987e337cdc9412814b7c9e50f2ea0e83aa282345924d124` / `2cd5c7187fa454dc0e677e05939c4c3fffaea39475a4dda2fe6e06ea68bf5ccb` |
| Cutover build authority | `e97ce58197136444598298fa8501d947cbd523c9af68e5e489d9d752f402f0c1` |
| Chromium wrapper TRX / selector DLL | `e0a88f516653b24de29a3977c11af55f87f9859e9305f858e8c603b49ea074ab` / `ad4147f71849e64b7c7b05b897ec35f34e7ed61701ae9619b88cb56050d6dde6` |

Run began `12:23:54.1787260Z`, **0.821 seconds** after prepare, and ended `12:26:07.6925591Z`, exit 5 `FailedRetained`; run ID `run-e88847b49b814cfdb4ed5b5ca0a42c21`. Sole Chromium selector: `12:24:14.9109591Z`–`12:26:07.6643502Z`, wrapper duration 101.689 seconds, child exit 1, `NonzeroExit`, `processTreeDrained:true`, `requiresDispositionProof:true`. No external termination occurred.

| Required selector | Discovered / executed / passed / failed / skipped | Result |
|---|---:|---|
| Chromium pair | 1 / 1 / 0 / 1 / 0 | FAIL / FailedRetained |
| Edge pair | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Cutover | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Workload | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |
| Recovery | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |

Nested Chromium selected three tests/one worker: **2 passed, 1 failed, 0 did not run**, displayed duration `1.1m`, plus one reporter error outside a test. Six-operation explicit consent and committed-response loss passed. Third case failed `WEB6 boundary=eligibility500 action=prepare HTTP/auth/recovery contract` at spec `260:79`, observer `69:19`, after a 5,000-ms predicate window. Exact sanitized state and observer trace:

~~~json
{"contextStatus":200,"eligibilityStatus":503,"refreshStatus":null,"loginVisible":false,"recoveryVisible":true,"resendEnabled":true,"confirmationVisible":false,"additionalMutations":0}
{"phase":"poll","events":["attached","control-start","control-done","trigger-start","trigger-done","poll","request:trigger:context","response:trigger:context:200","request:trigger:eligibility","response:trigger:eligibility:503"],"omittedEvents":0}
~~~

The observed eligibility response is 503; checked-in predicate requires 500 for this named boundary. Both decision responses are correlated to trigger requests; no control/unobserved response supplied them. The trace shows no additional mutation or confirmation. Reaching this later serial step implies the prior 401 assertion/re-login/protected-draft restore flow completed; this is a control-flow inference, not full pair/native acceptance. Remaining two-tab/grant checks did not complete. Current evidence contains no socket-failure diagnostic or SQL oracle exception. No status broadening, source patch or live rerun occurred.

Candidate records are `Chromium=Failed`, `Cleanup=Quarantined`. One original SQL database/runtime login/control root, host-runtime/config/browser log, candidate manifest/audit/disposition and prepared artifacts remain retained. Allocation receipt `e5cdae3dc44af7007f70cf1e1f804855f8cb068a8c361de8812a3a04e73cd806` binds control digest `f8da4fff2b6ceee878934bd8d42c3890c30d65a8f6292795c2162d6757f03d3d`. Later live roots/native success envelopes are absent. Process drain is not accepted SQL/artifact cleanup or final independent disposition. No post-failure SQL query, manual cleanup, historical-resource inspection/adoption or retry occurred.

Environment: Windows 11 Home x64 build 26300, SDK `8.0.427`, SQL `14.0.2140.2`, IIS Express `10.0.26013.1000`, Chromium `156.0.8078.4`, Edge `157.0.4341.46`; Node/npm above. Workload samples/percentiles, RPO/RTO, Task4 acceptance and CandidateVerified are absent. A reviewed boundary correction and new explicit Owner authorization are required before a fresh attempt. Ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO. Task 6 and B05/F04 remain incomplete.

## Task 6 attempt11 named auth-boundary failure and retained quarantine

The Owner authorized one fresh attempt after independently reviewed eligibility/auth/recovery UI-boundary correction. Candidate `web6-20261110-dcc4ff39` used backend `87d756de1e2c8567b6ddfac052b418790d6454c5` / tree `abbed9668f1774e93d90fe90f93d228383e915db` and frontend `7ab16026ca2cf963ef378a843c609fa5a4b69cd0` / tree `39d1393a75115a24ba3f06b276f1c14c9fe0fb77`. The reviewed case names boundaries, expects 401 sign-out/clearing, explicitly reauthenticates/restores the owned protected draft, and then continues 500 without replay. Existing 170-second mismatch header/480-second third-case budgets remain; no command retry was added.

Before new roots/approval, four read-only Edge observations at `11:42:11.6115412Z`–`11:42:41.7509473Z` (30.1394061 seconds) matched identity/version `157.0.4341.46`/SHA-256 `3e2c8557b5ca4d78868d3089346146ceab94f198ce3d58526ce8f6c8d4b4167c`. All 2,337 tool files/572 directories passed independent identity/hash/current-owner protected ACL/no-link checks: Node `22.14.0`, npm `11.6.0`, tree `c55ed76c2333bcb335b2327f639195464c2ed7685b329da812f02c85bed29529`. A bounded query independently reverified approved effective master/server. No custom lock/copy/updater change, installation or global mutation occurred.

Fresh roots, independent SQL approval, Owner authority and request used Windows PowerShell/net48 protected create-new publication. Exact four-field `web6-cutover` schema/current-user-only protected ACL/digest/master binding passed; eight-hour expiry `2026-11-10T19:42:57Z`. No old protected payload/resource was inspected, copied, reused or adopted. Authorize once exited 0; prepare once ran `11:43:12.2027816Z`–`11:45:55.7717753Z`, exit 0 `PIN_MATCHED`. Independent checks confirmed ordinary null candidate ID/OFF, exact candidate marker, ten-file full dist/wwwroot equality, clean detached pins and zero pre-run live allocation.

| Attempt11 record | SHA-256 |
|---|---|
| Release/net48 bootstrap | `ad001abba4423149385fe9d04eb558beffe44ac5764a2338884110b7aa97bbbf` |
| New SQL approval | `3a86c91fcbe0fa1cafc22cce3873e45fc503a06f3e06417052e6e4983436bfb5` |
| Owner authority / request | `76baeebad28faad1ca6b82d32555a51aef0f0b1d0d2b7feaebf6b8b6697a7c48` / `bd3fdce3a2fe7372ada091923cd76a43d8849f2da4e2ceef781472bf7d6d8eae` |
| Prepared / terminal receipt | `333786b44c6c82331ae9531076bcfe458a4465610f607b2f04f755e279ad16d2` / `cf358502bfc840352d7a53537672c76592e2f518071b7a15bc44add0d82dcdab` |
| Ordinary dist / manifest | `5c7908157609e24760dcab5da26b170f5194f1918f2343f80aa164aab0ef59ab` / `796d86094b6cfd29efa1d99aac49bd9fd10bb80c73b71d0164d853e74485ef53` |
| Candidate dist / manifest | `a340c4194b9d1bc347a9ff6a3d4589bb0535f9a8f6443d42f39a24a1fc78b297` / `684884b8cc740e5cf557cf50ec2ab7e3d3f252d40403d2a46148eeff27e3e85c` |
| Current / old backend publish | `9e806f84c17dd6c322f901ae283d24f563838b61194dc75d69a51dce1bc1f83c` / `786ab7e4d8093f81eea8c75b4251cf903ec5003ea9b12558ba2633daf1112b2a` |
| Cutover build authority | `a062ee72419d1790801e2ad2532d43a40beda0e79424ba9a370406fac2c504ea` |
| Chromium wrapper TRX / selector DLL | `8d6695d3209cf493e7c6429fbbd404473828d691ab78c58ae611b1e7936798f0` / `743b6ee58d518cc6a315098cbf282705d45410287c5603a0154b29dbb8e967e4` |

Run began `11:45:56.6101557Z`, **0.838 seconds** after prepare, and ended `11:48:06.8382364Z`, exit 5 `FailedRetained`; run ID `run-f2137019c78b4ceaad564fc5c4fa8994`. Sole Chromium selector: `11:46:16.6046964Z`–`11:48:06.8113409Z`, wrapper duration 97.865 seconds, child exit 1, `NonzeroExit`, `processTreeDrained:true`, `requiresDispositionProof:true`. No external termination occurred.

| Required selector | Discovered / executed / passed / failed / skipped | Result |
|---|---:|---|
| Chromium pair | 1 / 1 / 0 / 1 / 0 | FAIL / FailedRetained |
| Edge pair | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Cutover | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Workload | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |
| Recovery | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |

Nested Chromium selected three tests/one worker: **2 passed, 1 failed, 0 did not run**, 59.5 seconds, plus one reporter error outside a test. Six-operation explicit consent and committed-response loss passed. Third case failed `WEB6 boundary=eligibility401 action=prepare HTTP/auth/recovery contract` at spec `274:97` inside step `247:5`; predicate timeout was 5,000 ms. Exact sanitized state:

~~~json
{"contextStatus":null,"eligibilityStatus":null,"refreshStatus":null,"loginVisible":false,"recoveryVisible":true,"resendEnabled":false,"confirmationVisible":false,"additionalMutations":0}
~~~

These nulls mean no matching response statuses were observed at the assertion, not that no request existed. The diagnostic shows the observed UI had not reached the required signed-out state; no further cause is established. The explicit re-login/restore/500 continuation and remaining two-tab checks did not complete. Current evidence contains no socket-failure diagnostic or SQL oracle exception. No timeout change, source patch or live rerun was made.

Candidate records are `Chromium=Failed`, `Cleanup=Quarantined`. One original SQL database/runtime login/control root, host-runtime/config/browser log, candidate manifest/audit/disposition and prepared artifacts remain retained. Allocation receipt `4681d31d43ef0cd8a55f8a0e9b9fd2e15a1dfd0bc23fe393ba2a2e529db443b5` binds control digest `edc435339b229602cc0005200468c0185ad555c0a67dc7e171e2428448e708f1`. Later live roots/native success envelopes are absent. Process drain is not accepted SQL/artifact cleanup or final independent disposition. No post-failure SQL query, manual cleanup, historical-resource inspection/adoption or retry occurred.

Environment: Windows 11 Home x64 build 26300, SDK `8.0.427`, SQL `14.0.2140.2`, IIS Express `10.0.26013.1000`, Chromium `156.0.8078.4`, Edge `157.0.4341.46`; Node/npm above. Workload samples/percentiles, RPO/RTO, Task4 acceptance and CandidateVerified are absent. A reviewed boundary correction and new explicit Owner authorization are required before a fresh attempt. Ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO. Task 6 and B05/F04 remain incomplete.

## Task 6 attempt10 boundary assertion failure and retained quarantine

The Owner authorized one fresh attempt after independent review of the mismatch response-budget correction. Candidate `web6-20261110-dd12de6d` used backend `aa59990db966c450378c1a5981190174258ef9c8` / tree `255f2fed1068173da75b3294b23387603be50b63` and frontend `b91aba84b8e42b8aac78bd113c732ad9c5bff715` / tree `5daba3195ca055246f751bd7919ccc970c29b800`. Reviewed mismatch header budget is 170,000 ms with an absolute deadline; body/ordinary calls remain 10,000 ms. Third browser case budget is 480,000 ms; no command retry was added.

Before new root/approval issuance, four read-only Edge observations at `11:07:42.1270872Z`–`11:08:12.2539956Z` (30.1269084 seconds) matched identity/version `157.0.4341.46`/SHA-256 `3e2c8557b5ca4d78868d3089346146ceab94f198ce3d58526ce8f6c8d4b4167c`. All 2,337 tool files/572 directories passed independent identity/hash/current-owner protected ACL/no-link verification: Node `22.14.0`, npm `11.6.0`, tree `c55ed76c2333bcb335b2327f639195464c2ed7685b329da812f02c85bed29529`. A bounded query independently reverified approved effective master/server. No custom lock/copy/updater change, installation or global mutation occurred.

Fresh roots, independent SQL approval, Owner authority and request were created using Windows PowerShell/net48 protected create-new publication. Exact four-field `web6-cutover` schema/current-user-only protected ACL/digest/master binding passed; eight-hour expiry `2026-11-10T19:08:26Z`. No old protected payload/resource was inspected, copied, reused or adopted. Authorize once exited 0; prepare once ran `11:08:44.7369553Z`–`11:11:19.8098887Z`, exit 0 `PIN_MATCHED`. Independent checks confirmed ordinary null candidate ID/OFF, exact candidate marker, ten-file full dist/wwwroot equality, clean detached pins and zero pre-run live allocation.

| Attempt10 record | SHA-256 |
|---|---|
| Release/net48 bootstrap | `301df9f68fb63affd36e9bcce938f24907a813c54741cee100f6e88a9b8b94cc` |
| New SQL approval | `08ae0028b84357c081317200497c5f069a266533e7fb26ee0847e65fa248426b` |
| Owner authority / request | `c4a739f3d4141a9e6d2e1281444b5d71cf9105be3f12e69f4ddafb3fe19374e7` / `e68129be02658e9707b983f1e5f8d919bd9a4434747bcd4f28c75d72d1e44068` |
| Prepared / terminal receipt | `b27cce07365b1e7f1a2885436f3e3a6acebb5faa0b1f22e692d6035371c33cd4` / `c1425096003bbde5610ebf1ce1c86736830b837bc875744b68c79459f71928fd` |
| Ordinary dist / manifest | `5c7908157609e24760dcab5da26b170f5194f1918f2343f80aa164aab0ef59ab` / `796d86094b6cfd29efa1d99aac49bd9fd10bb80c73b71d0164d853e74485ef53` |
| Candidate dist / manifest | `dc4e693818fdc5bd96a8923cd13c363179b4999dbbe6e2a71415a849c73241a9` / `f83f82b44ca0860cab915d62fa203761713192c1a7d0f71fddb7ec3ff470958b` |
| Current / old backend publish | `61728c3566a9e738ceb050763ed7c56eeb8e3d4fff8c454e02a8917691e4bf16` / `dbe262110a273e11a4ccf6ae36a140c0f1308649289890b77f95cd4c29f4d3cb` |
| Cutover build authority | `121b196a543e4c1fcb036e4115c2e9b57b927cccbe538a2bd2446b2aeed18168` |
| Chromium wrapper TRX / selector DLL | `145326507aef32df94e65575380aa81128f5c91eb39514a643e6bc7078c86bf5` / `906635750b87d8ba3fb009059333b3841d88a47c1a1faba8e3b7057e8a133b1a` |

Run began `11:11:20.4204078Z`, **0.611 seconds** after prepare, and ended `11:13:29.6614655Z`, exit 5 `FailedRetained`; run ID `run-9e1699490b2d4e9fb73018723c3decb0`. Sole Chromium selector: `11:11:40.2039657Z`–`11:13:29.6335178Z`, wrapper duration 97.371 seconds, child exit 1, `NonzeroExit`, `processTreeDrained:true`, `requiresDispositionProof:true`. No external termination occurred; the assertion failed within the reviewed test budget.

| Required selector | Discovered / executed / passed / failed / skipped | Result |
|---|---:|---|
| Chromium pair | 1 / 1 / 0 / 1 / 0 | FAIL / FailedRetained |
| Edge pair | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Cutover | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Workload | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |
| Recovery | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |

Nested Chromium selected three tests/one worker: **2 passed, 1 failed, 0 did not run**; displayed duration `1.1m`, plus one reporter error outside a test. The six-operation explicit-consent and committed-response-loss cases passed. The third two-tab/candidate/grant/HTTP case failed at spec line `238:73`: `expect(firstResend).toBeEnabled()`, timeout 5,000 ms, `element(s) not found`. The locator targets the exact resend button inside the recovery dialog. This follows a completed candidate-control call, button click and awaited operations response in the shared `candidateMismatchOn / eligibility401 / eligibility500` loop.

Current retained snapshot shows the login button/password control; recovery dialog/resend button are absent. Current audit has 11 entries, ending in `WebAuthRefresh / Authentication / Denied`. The log does not identify the loop iteration or response status, so neither is inferred. There is no socket-failure diagnostic or SQL oracle exception in current evidence. This assertion failure does not prove complete mismatch/boundary acceptance. No timeout change, source patch or live rerun was made.

Candidate records are `Chromium=Failed`, `Cleanup=Quarantined`. One original SQL database/runtime login/control root, host-runtime/config/browser log, candidate manifest/audit/disposition and prepared artifacts remain retained. Allocation receipt `bdbcc5d9c52b27396109f4e4214d9064d9c82a661accacbf03e7f515d144b398` binds control digest `0358586f5b68ceb8a893e092ef289cb51156eb9ff49757044306b8d417531942`. Later live roots/native success envelopes are absent. Process drain is not accepted SQL/artifact cleanup or final independent disposition. No post-failure SQL query, manual cleanup, historical-resource inspection/adoption or retry occurred.

Environment: Windows 11 Home x64 build 26300, SDK `8.0.427`, SQL `14.0.2140.2`, IIS Express `10.0.26013.1000`, Chromium `156.0.8078.4`, Edge `157.0.4341.46`; Node/npm above. Workload samples/percentiles, RPO/RTO, Task4 acceptance and CandidateVerified are absent. A reviewed boundary correction and new explicit Owner authorization are required before a fresh attempt. Ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO. Task 6 and B05/F04 remain incomplete.

## Task 6 attempt09 diagnosed control timeout and retained quarantine

The Owner authorized one fresh attempt after review of the sanitized socket diagnostic. Candidate `web6-20261110-69528e92` pinned backend `9665dfd6aafab3ccaca1aed3f928067b87e25373` / tree `c80f07727b1e1a51fb1a8de3e84b93b1e5d3c47d` and frontend `a8afa96dd81e3a282c6351fd6d1786d331a6479d` / tree `ad030e4fdc0ec777a90d94da283c51bf67c6d24f`. The diagnostic retains the existing timeout and command dispatch behavior, adding only allowlisted failure fields.

Before new root/approval issuance, four read-only Edge observations at `10:40:06.7687118Z`–`10:40:36.8784861Z` (30.1097743 seconds) matched identity/version `157.0.4341.46`/SHA-256 `3e2c8557b5ca4d78868d3089346146ceab94f198ce3d58526ce8f6c8d4b4167c`. Complete independent tool validation covered 2,337 files/572 directories, identity/hash/current-owner protected ACL/no links: Node `22.14.0`, npm `11.6.0`, tree `c55ed76c2333bcb335b2327f639195464c2ed7685b329da812f02c85bed29529`. A bounded query independently reverified the approved effective master/server. No custom lock/copy/updater change, installation or global mutation occurred.

Fresh roots, SQL approval, Owner authority and request were created using Windows PowerShell/net48 and the repository protected create-new writer. Exact four-field `web6-cutover` schema/current-user-only protected ACL/digest/master binding passed; eight-hour expiry `2026-11-10T18:40:52Z`. No old protected payload/resource was read, copied, reused or adopted. Authorize once exited 0; prepare once ran `10:41:10.1382081Z`–`10:43:51.3024088Z`, exit 0 `PIN_MATCHED`. Independent checks confirmed ordinary null candidate ID/OFF, exact candidate marker, ten-file full dist/wwwroot equality, clean detached pins and zero pre-run allocation/live roots.

| Attempt09 record | SHA-256 |
|---|---|
| Release/net48 bootstrap | `39a3b05e29943f1c7e6a6e0dd6356d6eece64fb6367f39cad82324fa222b1509` |
| New SQL approval | `62abee56442767a1a375111e795908ebd1dff0cbd47db09e7be5a3c665c24cf5` |
| Owner authority / request | `a7d1ec91ba563978cc477b90dd8b97508196462a41a08b685316883fa956ee2d` / `a6253f2964a52e2c682ad7da5af4206f9d8cd36b90236c8dce45b3c50c3fb8e5` |
| Prepared / terminal receipt | `34d18b61ba52767b5816673b297ac4042bf843c30d00b581ebb92a46a437f235` / `27d59e9907e7ef32272e2f7cbb8d2b4a3580deb4cf818353846ebfb454948b90` |
| Ordinary dist / manifest | `5c7908157609e24760dcab5da26b170f5194f1918f2343f80aa164aab0ef59ab` / `796d86094b6cfd29efa1d99aac49bd9fd10bb80c73b71d0164d853e74485ef53` |
| Candidate dist / manifest | `0e61d45cedd2b6d6197e48b010f9d0332965429915e50db951e2f43c999dd6ac` / `67e352483b8d28da387ed199b5de9f9cfeb37d78ff1fb2fd4e4c0d0725f7b17f` |
| Current / old backend publish | `e649fb97bf820a74747b0b9961ed0240d112eee4ebd920e307a5a8786e3fda7d` / `1f4a5fe5ced6050c759e3f2ebf2889bfafddc3bd3e810d3bd74dec906cf09295` |
| Cutover build authority | `b0a221fa55ee059e0748a7bc5de29c5d626c7ac061b3371b510052a796ac43e4` |
| Chromium wrapper TRX / selector DLL | `91c1f53b9e812574a63513254f8dea1d4fec5b503cd62d90c359c08174dd6350` / `1470765f9d565ddb72747c869bef70fdbbbc20d96f86169f8210e9ad642fb3b9` |

Run began `10:43:52.1153062Z`, **0.813 seconds** after prepare, and ended `10:45:44.4858991Z`, exit 5 `FailedRetained`; run ID `run-d14947dd0e4c4ec8bf778336a1df1683`. Sole Chromium selector: `10:44:11.9648976Z`–`10:45:44.4618867Z`, wrapper duration 80.721 seconds, child exit 1, `NonzeroExit`, `processTreeDrained:true`, `requiresDispositionProof:true`.

| Required selector | Discovered / executed / passed / failed / skipped | Result |
|---|---:|---|
| Chromium pair | 1 / 1 / 0 / 1 / 0 | FAIL / FailedRetained |
| Edge pair | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Cutover | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Workload | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |
| Recovery | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |

Nested Chromium selected three tests/one worker: **2 passed, 1 failed, 0 did not run**, 36.8 seconds; reporter also recorded one error outside a test. The six-operation explicit-consent and committed-response-loss cases passed. The third two-tab/candidate/grant/HTTP case failed with the exact sanitized diagnostic below; the optional code field was absent:

~~~json
{"action":"candidateMismatchOn","terminal":"timeout","phase":"response-header","elapsedMs":10000,"category":"socket-timeout"}
~~~

This establishes a client inactivity timeout in the response-header phase of `candidateMismatchOn`. It does not establish whether the backend restart failed or completed, and no further cause is inferred. Current TRX contains no SQL oracle exception. The source timeout remains ten seconds; no retry, timeout adjustment or retained-candidate patch was made.

Candidate records are `Chromium=Failed`, `Cleanup=Quarantined`. One SQL database/runtime login/control root, host-runtime/config/browser log, candidate manifest/audit/disposition and prepared artifacts remain retained. Allocation receipt `81645a06ade1bd81baf8b66d2716cc97f8c0781ce7c845960b4c10ea179a2b9c` binds control digest `23825fbebe10bc59b437762f5ae6922b058402304421cfcedf5b4d6de108e74a`. Later live roots and native success envelopes are absent. Process drain is not accepted SQL/artifact cleanup or final independent disposition. No post-failure SQL query, manual cleanup, source fix, historical-resource inspection/adoption or retry occurred.

Environment: Windows 11 Home x64 build 26300, SDK `8.0.427`, SQL `14.0.2140.2`, IIS Express `10.0.26013.1000`, Chromium `156.0.8078.4`, Edge `157.0.4341.46`; Node/npm above. Workload samples/percentiles, RPO/RTO, Task4 acceptance and CandidateVerified are absent. A reviewed control-boundary correction and new explicit Owner authorization are required before another fresh attempt. Ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO. Task 6 and B05/F04 remain incomplete.

## Task 6 attempt08 partial Chromium results and retained quarantine

The Owner authorized one fresh attempt after complete oracle identifier review. Candidate `web6-20261110-8599b4e1` pinned backend `6988a81af82e8b625b6a8806eccb1186653ffc51` / tree `19a6d0af541c263c554fee3f3b6bd193f87fee9c` and frontend source `13179f56a3381275c0652f6edaf29258f18927f2` / tree `f493e5911494a3eb4361397e3c65c72ae37b87e9`. Frontend evidence head `1477a3981ae50fe838897723811c068f9cdfbb15` did not retarget the executable source. The reviewed backend brackets physical `[LineNo]` in both line projections/order clauses and retains `SaleNo AS SaleNumber`.

Before any new root/approval, four read-only Edge observations at `09:59:57.9888668Z`–`10:00:28.2504380Z` (30.2615712 seconds) matched identity/version `157.0.4341.46`/SHA-256 `3e2c8557b5ca4d78868d3089346146ceab94f198ce3d58526ce8f6c8d4b4167c`. Complete independent Node/npm validation covered 2,337 files/572 directories, identity/hash/protected current-owner ACL/no links: Node `22.14.0`, npm `11.6.0`, tree `c55ed76c2333bcb335b2327f639195464c2ed7685b329da812f02c85bed29529`. A fresh bounded query reverified the approved effective master/server. No lock/copy/updater change, installation or global mutation was introduced.

Fresh roots, independent SQL approval, Owner authority and request were created using Windows PowerShell/net48 protected create-new publication. Exact four-field `web6-cutover` schema/current-user-only protected ACL/digest/effective-master binding passed; expiry `2026-11-10T18:01:27Z` was eight hours. Old protected payloads and SQL/runtime resources were not read, copied, reused or adopted. Authorize once exited 0. Prepare once at `10:01:49.5610960Z`–`10:05:50.7430489Z` exited 0, `PIN_MATCHED`; independent checks confirmed ordinary null candidate ID/OFF, exact candidate marker, complete ten-file dist/wwwroot equality, clean detached pins and zero pre-run live allocations.

| Attempt08 record | SHA-256 |
|---|---|
| Release/net48 bootstrap | `3c8702a1c341104b7409c210b13af61343f979445d3aa9a68dee5f0b60a955ba` |
| New SQL approval | `aaccc2178b5acdec8043f766a9b7c314bf4bbdeb9dbea1a60603eb52c67b140a` |
| Owner authority / request | `dccc32cc76360d57991215a62646eef3c865cce10acb7514eff5fec7be5728fb` / `e1d491c080de16e02da13b5784d47cdb5d621b8d5333e1df41fd20e0ca70b16a` |
| Prepared / terminal receipt | `cf9efc189f94b5312e1213ee8513166e6b808257e5dbb7dc103e7001c8d614df` / `59be45ab81cc586cc26fe9f7d5c27b537a34bc2d5cfb20b05fc53c5ed71a9a45` |
| Ordinary dist / manifest | `5c7908157609e24760dcab5da26b170f5194f1918f2343f80aa164aab0ef59ab` / `796d86094b6cfd29efa1d99aac49bd9fd10bb80c73b71d0164d853e74485ef53` |
| Candidate dist / manifest | `10e7b0f6a7fd3b67e177d1aad3cb1f0a440c915d8ab86ad1620a45ec089c1810` / `f0766d264a4f2dd2cb5eee559f82b6a04414cbdb3ef334f4f780b4ae485d311f` |
| Current / old backend publish | `d05d5d4a883682652224381afbcee2f9f9d3fd86c371838e4828de487967883e` / `18ea414773a3ac2a9583b67d3970cea6b1361eae3bc26908bf2646d9779a85f7` |
| Cutover build authority | `631f5c9cb62e26d815e91a351c913152b5b33883befcb5d095afc1d4ae2bea40` |
| Chromium wrapper TRX / selector DLL | `2fae55db35e77ede055a413fd65d01f2eb95d253a0280dde3c1594c329b65a54` / `0754980ddc8a5652ca2bcf3314d112a2bfb1f2cd214bf9fad9d49f85e13133f2` |

Run started `10:05:51.9539903Z`, **1.211 seconds** after prepare, and ended `10:08:20.6681532Z`, exit 5 `FailedRetained`. Run ID `run-dac0b55c86a44e1c951db0965352d8d2`. The sole Chromium selector ran `10:06:16.2627803Z`–`10:08:20.6094487Z`; wrapper test duration 110.094 seconds. Terminal disposition is `NonzeroExit`, child exit 1, `processTreeDrained:true`, `requiresDispositionProof:true`. No retry or extra selector invocation occurred.

| Required selector | Discovered / executed / passed / failed / skipped | Result |
|---|---:|---|
| Chromium pair | 1 / 1 / 0 / 1 / 0 | FAIL / FailedRetained |
| Edge pair | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Cutover | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Workload | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |
| Recovery | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |

Nested Chromium Playwright selected three tests using one worker: **2 passed, 1 failed, 0 did not run**, 50.5 seconds; reporter also recorded one error outside a test. The six-operation explicit-consent test and committed-response-loss test passed. The third, two-tab/candidate/grant/HTTP boundary test, failed with `Owned WEB6 candidate control failed` at `Socket.fail`, spec line 62. The current TRX contains no SQL oracle exception. This is partial nested evidence, not pair acceptance.

The observed callback handles timeout, socket error and EOF; the retained log does not identify which event or control action caused this failure. Source inspection shows the same helper has a 10-second socket inactivity timeout, while candidate-mismatch control can stop/restart IIS Express using 10/20/45-second bounded waits. A timeout during that switch is a diagnostic hypothesis only. Pilot trace/video/screenshots were disabled; no stronger event-level diagnosis or boundary PASS is claimed.

Candidate records are `Chromium=Failed`, `Cleanup=Quarantined`. One newly allocated SQL database/runtime login/control root, host-runtime configuration/browser log, candidate manifest/audit/disposition and prepared artifacts remain retained. Allocation receipt `c3249f609ab26ff64988fae96d814b4ef1f33363c0b117c9bc6c68c32f0429ea` binds original control digest `3648a339fd0beba2e5fe44eacf4cc4d5db3630183038e9687e7d3b7c0569a7b3`. Later live roots and native success envelopes are absent. Process drain does not establish accepted SQL/artifact cleanup or final independent disposition. Retained SQL contains test activity; no zero-effect claim is made. No post-failure SQL query, manual cleanup, source fix, historical-resource inspection/adoption or retry occurred.

Prepared environment: Windows 11 Home x64 build 26300, SDK `8.0.427`, SQL `14.0.2140.2`, IIS Express `10.0.26013.1000`, Chromium `156.0.8078.4`, Edge `157.0.4341.46`; Node/npm as above. Workload has no samples/percentiles and recovery has no RPO/RTO. No Task4 acceptance, final disposition or CandidateVerified exists. A future fresh attempt requires a reviewed control-boundary correction and new explicit Owner authorization. Ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO. Task 6 and B05/F04 remain incomplete.

## Task 6 attempt07 SQL syntax failure and retained quarantine

The Owner authorized one fresh attempt after review of backend `0e090643e2d9de01bcbd98ae5dcfd08174b29216` (tree `94f931671c9829e8ffd21de209cfcbc8e385863a`), which corrects the Sales header projection to `SaleNo AS SaleNumber`. Candidate `web6-20261110-dc006b02` used that exact backend and frontend source `13179f56a3381275c0652f6edaf29258f18927f2` / tree `f493e5911494a3eb4361397e3c65c72ae37b87e9`; frontend evidence head `e5d0e51` did not retarget the source pin.

Before any new private root/approval/authority, four read-only Edge observations (`09:32:31.2744022Z`–`09:33:01.3951521Z`, 30.12075 seconds) matched file identity, version `157.0.4341.46` and SHA-256 `3e2c8557b5ca4d78868d3089346146ceab94f198ce3d58526ce8f6c8d4b4167c`; metadata/file identity was unchanged during each read. Independently, the approved private Node/npm pointer passed all 2,337-file/572-directory identity/hash/ACL/no-link checks before root creation: Node `22.14.0`, npm `11.6.0`, tree `c55ed76c2333bcb335b2327f639195464c2ed7685b329da812f02c85bed29529`. No Edge lock/copy/updater change, installation or global mutation was introduced.

Fresh roots, independent SQL approval, Owner authority and request were then created with Windows PowerShell/net48 protected create-new publication. Exact four-field `web6-cutover` schema, current-user-only protected ACL, digest and fresh effective-master/server binding passed; expiry `2026-11-10T17:33:55Z` was eight hours. Previous protected payloads/SQL/runtime resources were not read, copied, reused or adopted. Authorize ran once, exit 0. Prepare ran once, `09:34:15.9965825Z`–`09:38:14.6366892Z`, exit 0, `PIN_MATCHED`; ordinary candidate ID null/OFF, exact candidate marker, ten-file full dist/wwwroot equality, clean detached pins and zero pre-run allocation/live roots were independently checked.

| Attempt07 record | SHA-256 |
|---|---|
| Release/net48 bootstrap | `8b63fc85bd24a99b4c542948f54952799dbf6f4fcd6a8b5a54a7e79a42889502` |
| New SQL approval | `2f1da5744b979adfa66865d33a014cb5aa1577be964db8d8ac487383dc4edeae` |
| Owner authority / request | `62a28aaa9d39b90874185c729dda8aea4a732bd49927278fe0adaff365f0bf9e` / `8599a3390581a7fdf67b935dd96fb52d657f02a4268bc2febe32df637bac1abd` |
| Prepared / terminal receipt | `33d4e18dad8fb7e41a61e8cd9cec3bf0d994a39e2cd1cac1e38db987fa6f4dcd` / `c879eb79433a040230027eeeda2c8596ab4ff33a9830a8390fe5d7d14e70851d` |
| Ordinary dist / manifest | `5c7908157609e24760dcab5da26b170f5194f1918f2343f80aa164aab0ef59ab` / `796d86094b6cfd29efa1d99aac49bd9fd10bb80c73b71d0164d853e74485ef53` |
| Candidate dist / manifest | `04b188b19d5a08da6a285462026d39afae37b3dea33b33f3a00b859ec126a49b` / `59401842df3caae60bcffa7c232c2efcb8b60d104f838a495965ecaee090fd69` |
| Current / old backend publish | `571d260988cafeddd970411118287c41eba7e9a432e823dcf6d97b2caf87dc2c` / `cb0c12e4006b80acee3b8126bcd043134da6aac0960a3e25b1dd113456c57deb` |
| Cutover build authority | `2030442e8580ef49470232a77e63bd069d53f4087ab70766cb8a7326cc9b630e` |
| Chromium wrapper TRX / selector DLL | `7588786a4256faa148610e2e622adb7be92f77ae6d8fae34fed0d2040dc0c909` / `cc57d16c9151920072943ecc4b6ea967212c78d6e93deb8307a264e76e7f9a23` |

Run started `09:38:15.8173560Z`, **1.181 seconds** after prepare ended, and finished `09:39:55.2807593Z`, exit 5 `FailedRetained`. Run ID `run-a12187dd1de340ca8bad488d980fcf46`. The sole exact Chromium selector documented below ran `09:38:39.8783361Z`–`09:39:55.2115509Z`; wrapper test duration 63.050 seconds. It reports `NonzeroExit`, `processTreeDrained:true`, `requiresDispositionProof:true`. No retry/resend or nonessential inter-stage pause occurred.

| Required selector | Discovered / executed / passed / failed / skipped | Result |
|---|---:|---|
| Chromium pair | 1 / 1 / 0 / 1 / 0 | FAIL / FailedRetained |
| Edge pair | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Cutover | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Workload | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |
| Recovery | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |

IIS Express and Chromium launched. Nested Playwright selected three tests using one worker: **0 passed, 1 failed, 2 did not run**. The first six-operation case reported `Owned WEB6 candidate control failed`; control drain surfaced `SqlException: Incorrect syntax near the keyword 'LineNo'`. The stack reaches `Web6CandidateOracle.VerifyResultProjection:313`, the Sales line query at line 314. Both that query and the Receipt line query at line 324 use unquoted `LineNo` in projection/order clauses; migration010 declares `[LineNo]`. The reviewed header-column correction is present, but this separate syntax defect remains unfixed. Full six-command/projection verification did not complete, and retained SQL may contain committed test effects.

Candidate records are `Chromium=Failed`, `Cleanup=Quarantined`. One newly allocated SQL database/runtime login/control root, host-runtime config/browser log, candidate manifest/audit/disposition and prepared artifacts remain retained. Allocation receipt `9948a31473b199e1c327afbeaca9f70b54f612145da51b7ebd88d92234921e6a` binds original control digest `c19a005cd8d23101bfc200f459bc40b39cbf30ade5cc7e16a43be5497113f1f4`. The harness reports process drain; no accepted SQL/artifact cleanup or final independent disposition exists. The agent performed no post-failure SQL query, manual cleanup, source fix, historical-resource inspection/adoption or retry.

Prepared environment: Windows 11 Home x64 build 26300, SDK `8.0.427`, SQL `14.0.2140.2`, IIS Express `10.0.26013.1000`, Chromium `156.0.8078.4`, Edge `157.0.4341.46`; Node/npm as above. Workload has no samples/percentiles; recovery has no RPO/RTO. No Task4 acceptance or CandidateVerified exists. A future attempt requires a reviewed oracle SQL correction and new explicit Owner authorization with fresh roots/SQL approval/Owner authority/request. Ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO. Task 6 and B05/F04 remain incomplete.

## Task 6 attempt06 Chromium failure and retained quarantine

The Owner explicitly authorized one fresh attempt after attempt05. Before creating any new root/authority/approval, four ordinary read-only Edge observations over 30.057 seconds (`08:33:17.3177855Z`–`08:33:47.3746218Z`) all matched version `157.0.4341.46`, SHA-256 `3e2c8557b5ca4d78868d3089346146ceab94f198ce3d58526ce8f6c8d4b4167c`, with stable version/length/last-write metadata during each read. No custom locking, copying, updater-policy change or global mutation occurred. This bounded stability check is not a guarantee against later external changes.

Candidate `web6-20261110-2f863589` used exact backend source `914b2196d41999f6f465ae076e4fcbe4ef524a87` / tree `62632251fb124f85769edde70fd7dd8e688f1051`, frontend source `13179f56a3381275c0652f6edaf29258f18927f2` / tree `f493e5911494a3eb4361397e3c65c72ae37b87e9`. Evidence heads `10b86e0` / `5fe3687` did not retarget those pins. A new private root, new independent four-field `web6-cutover` SQL approval, Owner authority and request were created using existing Windows PowerShell and the repository protected create-new writer. Exact current-user-only ACL/schema/digest/effective-master binding passed; expiry was `2026-11-10T16:34:31Z` (eight hours). Previous approval/request/receipt payloads and SQL/runtime resources were not read or reused. The independently supplied Node/npm tool pointer was revalidated across all 2,337 files/572 directories, hashes, identities, ACLs and no links.

| Attempt06 record | SHA-256 |
|---|---|
| Bootstrap executable | `47d303a4b30e2ccb63519ca90ae7bf95cbdbc6caade33867706f1566465c1e41` |
| New SQL approval | `6256959a2d4d463173b6dfed7ab74573d2af6a0fb379099509e345e200cf952b` |
| Owner authority / request | `422986379999fb8ac042b952f2ed32d2f756f8c29dd35e8ba375c87fd8049713` / `3b44282588b8bd74263f1c900a955d44a6a6e7067ae694995d44ee7b236e03c4` |
| Prepared receipt | `0b953a41bbbcf2e6be81e7b0f78ab92edf3c251f8b203a40f66ca6d9782b0850` |
| Ordinary dist / manifest | `5c7908157609e24760dcab5da26b170f5194f1918f2343f80aa164aab0ef59ab` / `796d86094b6cfd29efa1d99aac49bd9fd10bb80c73b71d0164d853e74485ef53` |
| Candidate dist / manifest | `ba2b3f9eeef21f37f84835d5d5044a7c68b5c5680a1eb3e44cd89d321bd5c088` / `74bc0bb59e0c8fd26694b66cc7ee238a075456cf62d46b7a51d138ff865b795e` |
| Current / old backend publish | `29c8a620f5443c6fecbfca9f099be28dfd98de533ac3453a84bdaa250cb30ba0` / `6e8279322526ff4bed4e69fe47baa7f76fced34d7407f4a6f7cb14bcc9421200` |
| Cutover build authority | `4a30b24ad724d4b35b7f7b31438c53e5ed284d18cd4098abc860f8e81d3a7900` |
| FailedRetained terminal receipt | `cdb76de3ec813e5106512f2872e46e5e85c1538117a4eb0de3599ee4a57bbe0b` |
| Chromium wrapper TRX / selector DLL | `fe88167ea57a72171888c96c5bb11ec304d70c8c494486956e710983f98a61c5` / `d278568550c28c53e3e80a439abff831b68fbe6fa8438782260060bcaf06a933` |

Authorize ran once, exit 0. Prepare ran once at `08:35:39.5417017Z`–`08:38:22.7524325Z`, exit 0, `PIN_MATCHED`. Independent checks confirmed ordinary `candidateId:null` (OFF), exact candidate marker, complete ten-file dist/wwwroot equality, clean detached source pins and zero allocation/live roots. Run started `08:38:23.6408738Z`, **0.888 seconds** after prepare ended, and finished `08:39:42.2459671Z`, exit 5. No nonessential pause, retry or resend was added between prepare and run.

| Required selector | Discovered / executed / passed / failed / skipped | Result |
|---|---:|---|
| Chromium pair | 1 / 1 / 0 / 1 / 0 | FAIL / FailedRetained |
| Edge pair | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Cutover | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Workload | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |
| Recovery | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |

Run ID `run-8668a7a9d988432e9518809741fd9639`; selector `FullyQualifiedName=MyDmsVn.Server.Infrastructure.IntegrationTests.WebHostIisExpressTests.Web6_paired_candidate_runs_six_manual_resends_in_real_browsers_and_SQL` ran `08:38:44.2042385Z`–`08:39:42.217657Z`, test duration 45.881 seconds. The terminal result is `NonzeroExit`, `processTreeDrained:true`, `requiresDispositionProof:true`. Nested Chromium Playwright selected three tests using one worker: **0 passed, 1 failed, 2 did not run**. These nested counts are distinct from the single .NET wrapper row.

The first browser case reported `Owned WEB6 candidate control failed`. The control task then surfaced `SqlException: Invalid column name 'SaleNumber'` during drain. Its stack reaches `Web6CandidateOracle.VerifyResultProjection` (query at line 308), `VerifyOneEffectAsync` and `WebRecoveryPilotControl.Serve/Stop`. The oracle selects `SaleNumber` directly from `dbo.SalesReceipts`; checked-in migration010 declares `SaleNo`, and `InventoryLinqToDbMapping` maps model `SaleNumber` to `SaleNo`. This source/schema mismatch is diagnosed, not fixed here. No complete six-command or SQL projection oracle PASS is claimed; retained SQL may contain committed test effects.

Candidate records say `Chromium=Failed`, `Cleanup=Quarantined`. One newly allocated SQL database/runtime login and private SQL control root, host-runtime config/browser log, candidate manifest/audit/disposition records and prepared artifacts are retained under private ownership evidence. Allocation receipt SHA-256 `54daa66fdfe8143a392147ebf9c9713e4ee6d0f843b027ebb9013128405fbf0d` binds control digest `d7c95d670b239c8a4400e3ed3be61f9f82b45ae4ba29d8fc62b99758b35ab799`. The harness performed process drain; failed fixture drain and quarantined disposition prevent SQL/artifact cleanup acceptance. The agent performed no manual cleanup, independent post-failure SQL query, resource adoption or run retry. Process-tree drain alone does not establish the required final SQL/queue/resource disposition.

Environment remained Windows 11 Home x64 build 26300, SDK `8.0.427`, Node `22.14.0`, npm `11.6.0`, SQL Server `14.0.2140.2`, IIS Express `10.0.26013.1000`, Chromium `156.0.8078.4`, Edge `157.0.4341.46`. No workload samples, recovery RPO/RTO, Task4 acceptance, final independent disposition or CandidateVerified exists. All earlier retained resources stay untouched. A future live attempt requires a reviewed source correction and new explicit Owner authorization with a fresh candidate/root/SQL approval chain. Ordinary resend OFF; full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED, production NO-GO. B05/F04 and Task 6 remain incomplete.

## Task 6 attempt05 prepared then BlockedNoAllocation

After the reviewed pre-allocation backend source-admission fix and new explicit Owner authorization, candidate `web6-20261110-300ef832` used new private roots, new SQL approval and fresh Owner authority/request. The backend source was `914b2196d41999f6f465ae076e4fcbe4ef524a87`, tree `62632251fb124f85769edde70fd7dd8e688f1051`. The controller distinguished frontend evidence head `cdae4f43cea5a06ea15b5b119622888dbc1faac6` from exact reviewed source `13179f56a3381275c0652f6edaf29258f18927f2`, tree `f493e5911494a3eb4361397e3c65c72ae37b87e9`; prepare created fresh detached worktrees at the latter pin. No pin guard was changed.

The new four-field SQL approval used a fresh read-only effective-master/server query, purpose `web6-cutover`, expiry `2026-11-10T16:14:46Z` (eight hours), and the repository's protected create-new writer. Exact current-user-only owner/ACL, schema, digest and server binding passed. No old approval/request/receipt payload or historical SQL/runtime resource was read or reused. Only the independently supplied existing private Node/npm pointer was reused: all 2,337 files and 572 directories passed identity/hash/ACL/no-link checks, with Node `22.14.0`, npm `11.6.0`, whole-tree SHA-256 `c55ed76c2333bcb335b2327f639195464c2ed7685b329da812f02c85bed29529`.

A preliminary issuance-only root (`issue05a`) is separately retained: using net48 `ProtectedJsonFile` under PowerShell 7 failed at `FileStream.GetAccessControl()`, after creating only a new approval file and diagnostic. Its task/SQL parents stayed empty; authorize/prepare/run were not called. The controller approved a new root (`issue05b`) using existing Windows PowerShell/.NET Framework. No install, browser change, global configuration mutation, old approval copy or source change was performed.

| Attempt05 record | SHA-256 |
|---|---|
| Release/net48 bootstrap executable | `47d303a4b30e2ccb63519ca90ae7bf95cbdbc6caade33867706f1566465c1e41` |
| New SQL approval | `b38ef13bf82f7bd1b8be56e3222f2ef9c58ee14c3d3ca532dc69329ed9acf0f2` |
| Owner authority / request | `ad94675c3729bad50016354618b4f1c169ca4aff36f950a7ee07843132a06050` / `538641396142353ae7d300602233a78cfa555bc6b0a896b6d04dfc6ff342bbac` |
| Prepared receipt | `25323e16d014875c8b8e8617749028c05a6d407153f4c7d98fbd184b57b1d042` |
| Ordinary dist / manifest | `5c7908157609e24760dcab5da26b170f5194f1918f2343f80aa164aab0ef59ab` / `796d86094b6cfd29efa1d99aac49bd9fd10bb80c73b71d0164d853e74485ef53` |
| Candidate dist / manifest | `bc8efce84e631b52fb4eace9bf42ee8f47414b1577b5cbf4f1926d6d55e190a9` / `ea0dfa8bb3b1385aae5ae8c96355b16277012bb5ea6722030bb0a873314224d2` |
| Current / old backend publish | `69a337871d09b214cfec188173b38b164ace51b570d3f0c666e0b340e7f5abc0` / `0d16ea13a2499b81d74189bd948344cc8562eeed241d3c1fd005452eaf2d0278` |
| Cutover build authority | `c2e3ba665e9fdb70c6f2a363cd83fe697d3f1d26c987b15dcc897798d97368e6` |
| BlockedNoAllocation terminal receipt | `5066422846b7aff633a0cac7093a564816745d689ab2e872d4ad94aacb991844` |

Authorize ran once, exit 0. Prepare ran once, `08:15:31.8016884Z`–`08:18:21.2920865Z`, exit 0, `PREPARED / PIN_MATCHED`. Independent checks confirmed ordinary `candidateId:null` (OFF), the exact candidate marker, all ten dist files equal to the complete published wwwroot, clean detached source pins, empty SQL control parent and absent live roots. The command forms remain `authorize`, `prepare`, then `run` as documented in the [runbook](../../development/WEB6_CANDIDATE_RUNBOOK.md#7-controlled-bootstrap-and-protected-sql-approval).

One run from `08:18:40.2239975Z` to `08:18:40.9365692Z` exited 4 with `BlockedNoAllocation`, run ID `run-823340899eb7478d851dd419bf713092`. Read-only diagnosis found all authority bindings and seven of eight tool digests still matching, but Edge had changed from prepared version `157.0.4341.42` / SHA-256 `d258e512aa24947cfbef0c0033d8c3cd56ecb40f7b2ad59802fa75c4e4f3324d` to observed `157.0.4341.46` / `3e2c8557b5ca4d78868d3089346146ceab94f198ce3d58526ce8f6c8d4b4167c`. `BootstrapRunExecution.PrepareAsync` rejects unequal pinned tool records before selector build/invocation. The agent did not change Edge; the external actor responsible was not established. No guard was bypassed and no run was retried.

| Required selector | Discovered / executed / passed / failed / skipped | Result |
|---|---:|---|
| Chromium pair | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Edge pair | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Cutover | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Workload | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |
| Recovery | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |

The terminal receipt has no selector/TRX and disposition `BlockedBeforeAllocation`. SQL control parent remains empty, all five live roots absent and evidence directory empty. Retained attempt05 resources are authority/request/diagnostics, five prepared worktrees, artifacts and prepared/run receipts; **no new SQL database/login, host-runtime, IIS process/queue or browser context was allocated**. No cleanup or resource-adoption action occurred. The earlier attempt04 SQL/database/login and host-runtime remain untouched FailedRetained resources.

Other observed prepared inputs: Windows 11 Home x64 build 26300, SDK `8.0.427`, SQL Server `14.0.2140.2`, IIS Express `10.0.26013.1000`, Chromium `156.0.8078.4`; certificate digest remains `47985af0a6345e7c8df22aacbea36cf7cb12d924eba42704cb6997745470f647`. Workload has no samples/percentiles and recovery has no RPO/RTO. No Task4 acceptance, final independent disposition or CandidateVerified exists. Any further attempt requires new explicit Owner authorization and a fresh candidate/root/approval chain against independently revalidated machine inputs; do not reopen this terminal attempt or alter its receipt. Ordinary resend OFF, full IIS/unperformed Narrator NOT RUN, staging NOT ACCEPTED and production NO-GO remain unchanged. No roadmap status advances.

### Attempt05 pure verification

At backend `914b2196` and frontend evidence head `cdae4f4`, fresh SQL-free checks passed: Application **363/363**, HttpHost **635/635**, and Security **240/240** (four bootstrap classes 227 plus `Web6CandidateBackendSourceTests` 13), zero failed/skipped with actual TRX totals matching execution. Direct Debug/net48 `dotnet test` used `--no-restore`; child shells removed both SQL connection/approval variables. The Security filter was the four-class filter recorded below plus `FullyQualifiedName~Web6CandidateBackendSourceTests`. Frontend typecheck passed; unit **81 files / 1,558 tests** passed, zero failures. TRX SHA-256: Application `4e71eb2833afc5e3c1e3fe04f653a6e970a603fcb2a06d8bf773af0c15244d18`, HttpHost `9a43c8214947cb37ea4b4342e1172e565c470971572a034063923e4be06e107e`, Security `9eb111438152b319fac6629e126d883bf848baecff362fd48c2f549025bf9fde`. These checks do not replace any missing live result.

## Task 6 attempt04 prepared then FailedRetained

After explicit Owner authorization to issue independent protected WEB6 SQL approval and review of the manifest-publication correction, candidate `web6-20261110-182d4285` used fresh attempt04 roots and fresh Owner authority/request. The earlier roots were not reused or altered. The Owner-approved SQL approval and private copy of existing Node/npm were reused only after exact ACL/schema/digest/expiry/effective-master and whole tool-tree identity/hash/ACL/link checks. The legacy SQL approval bytes and ACL remain unchanged.

| Candidate input or output | Exact identity |
|---|---|
| Backend source / tree | `18672300d642eef7944fbd540a42b3654783ba5b` / `f6a790770454112a27692220d359aef5e6e483c4` |
| Frontend source / tree | `13179f56a3381275c0652f6edaf29258f18927f2` / `f493e5911494a3eb4361397e3c65c72ae37b87e9` |
| Release/net48 bootstrap executable SHA-256 | `d845a2a937ff4b07fcf8202a4c2bb5d2b48570a7e5476652657f01a9eb225c24` |
| Owner authority / request SHA-256 | `3864549865977dbba9fb61084208ac37aae6712972ec062caaed191bfe6da449` / `2a6897681c29bfeb36a354ca83b84beeae5b3eaf7b24335b21a0a4d54f33e415` |
| Independent protected SQL approval SHA-256 | `faaf1fa43e63bb17eca3d13116e96312fbf21a8bc32fe9e8f4a8d0b91325724b` |
| Prepared receipt SHA-256 | `0a7376b0175c9e5fc1fa0fac254a9edeb2c3ca7b3293e83089ad4d9579e28c93` |
| Ordinary dist / manifest SHA-256 | `5c7908157609e24760dcab5da26b170f5194f1918f2343f80aa164aab0ef59ab` / `796d86094b6cfd29efa1d99aac49bd9fd10bb80c73b71d0164d853e74485ef53` |
| Candidate dist / manifest SHA-256 | `6efbddbe9391ade739710512e19423e6117e2183d99360f50641026dd0d7c9bf` / `bba31a3da72df6932133bd282dca9508330422bd13d896631a7a794dac4e71c5` |
| Current / old backend publish SHA-256 | `c7b2ba4ea6fbe186f97a834c0edb8d02d25fcdf216f07dd61216ba0b95b3491d` / `1edfb6b04bf9a10ca224f02d20a9e16028a44ccd5272ccd3bcc7ee7852441f1f` |
| Old frontend dist SHA-256 | `da573ea101e95b9a98850cc1b29270d89a24e6f4d54a8c2044949cfdb9289882` |
| Cutover build-authority SHA-256 | `af58f7c6d3187319c6524bf81c7a25514ec9d7a3b7d7f4d00c4856a9ffa28fdc` |

The archive pins remain backend `a3f3db16381a38bdf0b76e8ab36cb55bec02bbd2` and frontend `ea104582a5aa4bcdf0d04999296a30a3c43e450e`. Environment: Windows 11 Home x64 build 26300, SDK `8.0.427`, Node `22.14.0`, npm `11.6.0`, IIS Express `10.0.26013.1000`, SQL Server `14.0.2140.2`; prepared browser binaries Chromium `156.0.8078.4` and Edge `157.0.4341.42`. The 2,337-file copied tool tree digest is `c55ed76c2333bcb335b2327f639195464c2ed7685b329da812f02c85bed29529`; no installation or global configuration change occurred. Owner/SQL authority expiry is `2026-11-10T15:10:28Z`.

Authorize ran once, exit 0. Prepare ran once from `07:31:40.0910864Z` to `07:34:39.6501042Z`, exit 0, `PREPARED / PIN_MATCHED`. It verified clean detached source pins, ordinary marker `candidateId:null` (OFF), exact candidate marker, existing tool/browser/TLS pins and independent effective-master authority. All ten candidate dist files independently matched the complete published `wwwroot` set. SQL control parent and all live roots were empty/absent before run. This prepared result resolves the earlier preparation defects; it does not establish live acceptance.

```powershell
& $bootstrap authorize --input $ownerInput --output $ownerAuthority --acknowledge 'I AUTHORIZE WEB6 NONPRODUCTION DESTRUCTIVE DRILLS'
& $bootstrap prepare --request $request --authority $ownerAuthority --authority-sha256 $ownerAuthoritySha256
& $bootstrap run --prepared $prepared --prepared-sha256 $preparedSha256
```

Run was invoked exactly once, from `07:35:43.6601902Z` to `07:36:27.1863867Z`, exit 5. It restored/built the pinned Security selector, then invoked Chromium only. Actual exact row: `FullyQualifiedName=MyDmsVn.Server.Infrastructure.IntegrationTests.WebHostIisExpressTests.Web6_paired_candidate_runs_six_manual_resends_in_real_browsers_and_SQL`, via `dotnet test <pinned Security project> --configuration Release --framework net48 --no-build --no-restore --disable-build-servers -nr:false --filter <exact row> --logger 'trx;LogFileName=result.trx' --results-directory <new private results>`.

| Required invocation | Discovered / executed / passed / failed / skipped | Actual boundary |
|---|---:|---|
| Pair Chromium .NET selector | 1 / 1 / 0 / 1 / 0 | FAILED after SQL setup, before IIS/browser launch; native browser cases NOT RUN |
| Pair Edge | 0 / 0 / 0 / 0 / 0 | NOT RUN; stopped by first failure |
| Cutover | 0 / 0 / 0 / 0 / 0 | NOT RUN |
| Workload | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |
| Recovery | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED |

Root exception: `FileNotFoundException: CleanArchitecture.sln`, from `WebHostIisExpressTests.FindRootFile` at source line 527, called at line 196. The repository solution is `MyDmsVn.sln`. This lookup occurs after owned database allocation/migration, runtime-login creation and fixture seed, and after writing host configuration; it precedes candidate-manifest construction and IIS Express process launch. The aggregate failure was preserved. No application HTTP/browser scenario or independent business/journal oracle ran.

Terminal receipt is `FailedRetained`, SHA-256 `07b1fa91391a2177aa6dfd81fa54df928b0f19cd1fea12ce5e7e2376f859c8a7`, with `NonzeroExit`, `processTreeDrained=true`, `requiresDispositionProof=true`. Actual TRX SHA-256 is `54bdb2ebe299cb65d36101046a92508292748af5c63b2dc772ee13a8154f4eae`. Protected allocation evidence SHA-256 is `131ca868bcfd837c95483a68518d4ea733aeac15801496bda85d6dcd45e698b8`, bound to original SQL control SHA-256 `a00ffa11d966a592908d81b3909e36a81f634e957bbd89adcebb206d2210eb86`.

**Retained resources:** one owned database, one mapped runtime SQL login, one private SQL control root and the host-runtime directory/configuration, together with prepared worktrees/artifacts and all receipts/TRX. Read-only inspection of that exact allocation matched original server/database ID/creation tuple and ownership token, and observed one mapped SQL login plus retained host configuration. This is observation only, with no deletion authority. Fixture cleanup was refused after the failure; no manual cleanup, retry or receipt replacement occurred. Process-tree drain does not establish SQL/HTTP-queue/final disposition acceptance. Historical quarantines and attempts01–03 remain untouched.

No workload samples/percentiles, recovery RPO/RTO, accepted Task4 receipt, final independent disposition or CandidateVerified exists. A further live attempt requires **a reviewed source correction and new explicit Owner authorization**, then a new candidate/root/request; never patch or rerun the retained attempt. Ordinary artifact resend remains OFF. Full IIS and unperformed Narrator remain NOT RUN within their bounded exceptions; staging NOT ACCEPTED, production NO-GO. B05/F04 and Task 6 do not advance to complete.

### Fresh pure verification after attempt04

At backend `1867230` / frontend `13179f5`, the requested pure gates passed: Application **363/363**, HttpHost **635/635**, four bootstrap Security classes **227/227**, each with zero failed/skipped and matching actual TRX total/executed counts. Backend commands used the same direct Debug/net48 `dotnet test <project> --no-restore --logger ... --results-directory ...` form and exact four-class filter documented in the earlier checkpoint below; both SQL environment variables were removed only from the test children. Frontend `npm.cmd run typecheck` passed and `npm.cmd run test:unit` passed **81 files / 1,558 tests**, zero failures. No live selector was repeated.

TRX SHA-256: Application `c7a0a06507e1cadf6fcf6ae37997520b0bf59e42c5d1c93aa7c92b9233767520`; HttpHost `2634b9011cbd833b2b4476f8370e87bd3a426891d4e7eb89b24485701a00ff70`; bootstrap `a9f43a233a46d138fe6ee4ba2dc56137aa0e5e3ef3f865bf118e6ebba0e5600a`. These pure results do not replace the retained Chromium failure or establish SQL/host/browser acceptance. Raw TRX and private records remain outside committed evidence.

### Retained preparation attempts before attempt04

- Attempt01 `web6-20261110-25a1cf46`: protected legacy-SQL-approval refusal before task-root creation, detailed below. Owner later explicitly authorized issuing independent four-field protected SQL approval; the legacy file was not copied or repaired.
- Attempt02 `web6-20261110-e5cd8acd`: new independent SQL approval passed; prepare refused before task-root creation because Node's adjacent npm was `10.9.2`, while shell `npm.cmd` resolved installed npm `11.6.0` by prefix. A separately authorized task-local copy of existing Node/npm resolved this prerequisite in a fresh root, without install/global mutation.
- Attempt03 `web6-20261110-01a86302`: ordinary build succeeded, then manifest hard-link publication failed `EBUSY` while the protected parent-directory lease was held. A separate diagnostic output succeeded unheld and failed held with the same child environment. Five clean worktrees, ordinary artifact and diagnostic records remain retained. Reviewed backend `1867230` / frontend `13179f5` changed bootstrap to verified exact UTF-8 stdout plus backend protected create-new publication; attempt04 proved that corrected preparation path.
- A preliminary local issuance-script type-binding failure created only new private directories/error evidence, no approval/prepare/live invocation; those records were also retained.

## Task 6 controlled bootstrap attempt

**Attempt01 history; the latest disposition is attempt16 above.**

Owner approved the nonproduction bootstrap plan and destructive drill scope, including the separate authorization step. Candidate `web6-20261110-25a1cf46`, attempt `attempt01`, ran once at `2026-11-10T05:01:14Z`. The Owner authority covered HTTPS localhost ports 44321/44322, the pair/cutover/workload/recovery drills, new private task/SQL parents, the four exact source pins below and expiry `2026-11-10T13:01:13Z`. That authority is not a pilot-enable decision.

| Pin or local record | Exact identity |
|---|---|
| Backend source / tree | `9351942bd7c144f0f221b2cc4c78577c5f4d92f4` / `270455fa95a309fe705248665f8739d13c030c59` |
| Frontend source / tree | `5f7c0051497e8631925b68127ff556d2d066cd5c` / `158c5ec562b8bf66e7be3ea8db41cbae32b7e5c2` |
| Old backend / frontend pins | `a3f3db16381a38bdf0b76e8ab36cb55bec02bbd2` / `ea104582a5aa4bcdf0d04999296a30a3c43e450e` |
| Release/net48 bootstrap executable SHA-256 | `f56be7ad8b604f4c6e134b28407cec2f0c206748b13c4f7a000dae52dc856c61` |
| Owner authority SHA-256 | `628f0795fc78212697f293a08d45bd5ffd07b6531cc597504da083a05ff49085` |
| Strict request SHA-256 | `66fb71c14d5842173afdefe64b6c4f0a3d5d9eb7d449064927acb3e8fe6f7bfa` |
| Existing SQL approval SHA-256 | `bcc3ae3947a8913057252613b31e3d319f6876752c0d8a498ba8c209c0389fb5` |
| Private attempt summary SHA-256 | `d30cfc8cfb72d498585d511c78a188f5e55d73f6d30ea492f6ea9e0ef8281080` |

Both source worktrees were clean and detached. Windows 11 Home x64 build 26300, .NET SDK `8.0.427`, Node `22.14.0`, npm `11.6.0` and IIS Express `10.0.26013.1000` were observed. A read-only designated SQL identity check reached effective `master` on SQL Server `14.0.2140.2`; the legacy approval's nonproduction attestation, unexpired validity, server/machine/instance and stored fingerprint were checked. Its endpoint spelling differed from the connection's DataSource spelling and was not reinterpreted as approval. These observations did not authorize allocation.

The blocking fact is the legacy SQL approval's **unprotected inherited DACL**: current-user owner, four inherited ACEs, including an ACE permitting modification by another principal. Its legacy JSON schema also differs from the required WEB6 schema. The original bytes and ACL were rechecked unchanged. No copy, ACL repair, derived authority or replacement was created. A read-only diagnostic invoked the exact bootstrap authority/request/tool/certificate readers, then observed `IOException: Bootstrap file is outside the protected Owner ACL.` at `protected-sql-approval-read`; it did not invoke prepare again.

Commands, with private paths represented by named variables:

```powershell
dotnet build tools/MyDmsVn.Web6.CandidateBootstrap/MyDmsVn.Web6.CandidateBootstrap.csproj --configuration Release --framework net48 --no-restore --disable-build-servers -nr:false
& $bootstrap authorize --input $ownerInput --output $ownerAuthority --acknowledge 'I AUTHORIZE WEB6 NONPRODUCTION DESTRUCTIVE DRILLS'
& $bootstrap prepare --request $request --authority $ownerAuthority --authority-sha256 $ownerAuthoritySha256
```

Build: exit 0, 0 warnings/errors. Authorize: exit 0, `status=AUTHORIZED`. Prepare: exit 4, `status=BLOCKED_PREFLIGHT`. The task root did not exist afterward and the new SQL control parent had zero children. The prepare code opens protected SQL approval before worktree/artifact creation and before its SQL identity query. No `prepared.json`, detached candidate worktree, publish/dist artifact or live run receipt was produced. `run` was not invoked. The current attempt has no artifact hash or fresh ordinary-artifact build proof; ordinary resend remains OFF in source, and no candidate artifact was admitted.

| Required live invocation | Discovered / executed / passed / failed / skipped in this attempt | Disposition |
|---|---:|---|
| Pair Chromium | 0 / 0 / 0 / 0 / 0 | NOT RUN; blocked at prepare |
| Pair Edge | 0 / 0 / 0 / 0 / 0 | NOT RUN; blocked at prepare |
| Cutover | 0 / 0 / 0 / 0 / 0 | NOT RUN; blocked at prepare |
| Workload | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED; no class samples or percentiles |
| Recovery | 0 / 0 / 0 / 0 / 0 | NOT RUN / NOT MEASURED; no RPO/RTO |

There is no business/journal/ledger oracle, host queue admission or cleanup PASS for this attempt. No live SQL database/login, IIS worker/queue or browser context was allocated. Only the new private authority/request/diagnostic records and empty parent directories were retained. Historical quarantines and failed-attempt resources were untouched. The [runbook remediation contract](../../development/WEB6_CANDIDATE_RUNBOOK.md#7-controlled-bootstrap-and-protected-sql-approval) is required before a new attempt; an unprotected legacy approval cannot be promoted to authority.

### Task 6 pure verification at the source pins

All commands were fresh at the source pins above. Backend test children had both SQL connection/approval environment variables removed; these are pure tests, not SQL acceptance. Actual TRX total/executed/pass counts matched and no selected case skipped. Tests used Debug/net48; the CLI build above separately used Release/net48.

| Gate | Result |
|---|---|
| Application complete project | 363 / 363 passed, 0 failed/skipped |
| HttpHost complete project | 635 / 635 passed, 0 failed/skipped |
| Four bootstrap Security classes: Authority, Preflight, Runner, Orchestrator | 218 / 218 passed, 0 failed/skipped |
| Frontend `npm.cmd run typecheck` | PASS |
| Frontend `npm.cmd run test:unit` | 81 files / 1,552 tests passed, 0 failed |

Backend command form was `dotnet test <project> --no-restore --logger 'trx;LogFileName=<name>.trx' --results-directory <local-results>`. The exact Security filter was `FullyQualifiedName~Web6CandidateBootstrapOrchestratorTests|FullyQualifiedName~Web6CandidateBootstrapRunnerTests|FullyQualifiedName~Web6CandidateBootstrapAuthorityTests|FullyQualifiedName~Web6CandidateBootstrapPreflightTests`. Local TRX SHA-256: Application `711045ca60364e5129182672add8b207a51f5879ef76ced2a3a25256999c1447`; HttpHost `5147e7517894d8d168458fac7ef61e4ef4475b428cfedd7323de224a909d72ba`; bootstrap `2f8ef49ca416a8cbba3210569f09cb3447a8401aeeca4a0095fdd9520047b3ac`. Raw TRX and private records are not committed. The [frontend record](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/codex/web6-f04-paired-cutover/docs/evidence/web6/20261110-f04b-measured-quality.md) retains its earlier evidence and has a Task 6 addendum for the same blocked candidate. This branch link identifies the pending documentation update; no push or merge is claimed here.

No roadmap status advances. Full IIS and unperformed Narrator remain NOT RUN within their existing bounded exceptions; staging remains NOT ACCEPTED and production NO-GO. Task 6 cannot complete without PIN_MATCHED preparation, five passing live invocations, the required workload samples, measured recovery and CandidateVerified.

## Candidate provenance

**Earlier harness checkpoint, before the Task 6 source pins above.**

| Component | Reviewed identity |
|---|---|
| Backend implementation | source `9598c75d60370bc73efaa14525deb42af3f0e8d2`; tree `889cc4c5acf70713f3f05fae1fa950016d3cffc3` |
| Frontend implementation | source `9eb7d37e8fdb7ced5871ae9eb6be00f3e6e7163d`; tree `bd6cda2d6f13912101a357ad5f243fa4082e6140` |
| Frontend reviewed evidence | commit `4496a991991ea7c1001dee8ccac80192a3a27f16`; ordinary `dist` digest `b2422fdce8d0b8645a3f6213952ce2b4c6bff288c0c8a217ed655b70be93ce88` |

The reviewed frontend record reports npm locked install PASS with six advisories (two low, four moderate), typecheck PASS, unit 80 files / 1,517 tests passed, ordinary build 2,579 modules with a chunk-size warning, and production Playwright 2/2 passed with zero skip/retry. Its live pair/cutover is BLOCKED/NOT RUN, workload is NOT MEASURED and recovery is NOT RUN. Those frontend results do not substitute for a backend live pair.

## Exact enabled selectors

Selectors were executed sequentially against the Task 7 backend checkout. Each row is a test failure representing a deliberate fail-closed prerequisite block, not a skipped or silently disabled live test.

| Gate and exact row | Executed result | Blocking prerequisites | TRX |
|---|---:|---|---|
| Pair — `Web6_paired_candidate_runs_six_manual_resends_in_real_browsers_and_SQL` | 0 passed / 1 failed / 0 skipped | candidate root/id, browser directory, publish directory, TLS thumbprint and pilot project absent | `TestResults/task-7-paired-blocked/task-7-paired-blocked.trx` |
| Cutover — `Web6_pinned_cutover_runs_protected_IIS_and_browser_lifecycle` | 0 / 1 / 0 | candidate identity, current/old publish and archives, frontend dist, build authority path/hash, SQL control parent, current/restored epochs, browser directory and TLS thumbprint absent | `TestResults/task-7-cutover-blocked/task-7-cutover-blocked.trx` |
| Workload — `Web6_paired_candidate_runs_bounded_workload_profiles` | 0 / 1 / 0 | publish/TLS/browser/pilot inputs, candidate enable/root/id, workload root/cohort and reviewed Task 4 acceptance path/hash absent | `TestResults/task-7-workload-blocked/task-7-workload-blocked.trx` |
| Recovery — `Web6_candidate_recovery_rehearses_distinct_owned_restore_before_service_admission` | 0 / 1 / 0 | publish/browser/TLS inputs, candidate root/id, reviewed Task 4 acceptance path/hash and recovery root absent | `TestResults/task-7-recovery-blocked/task-7-recovery-blocked.trx` |

## Implemented harness

- Pairing binds protected backend manifest/result identity to the frontend source/tree/dist evidence and the actual browser bridge. Six same-key manual-resend command kinds remain behind explicit candidate admission.
- Cutover models independent current, old-compatible and restored adapters, protected lifecycle admission and compatibility oracles. It does not perform an unsafe schema13 downgrade.
- Workload supplies bounded per-class metrics and up to 1,000 attempts, retained PR #95 host re-observation and outer cleanup. No measurements were collected here.
- Recovery uses a real source-outage coordinator, Task 2/PR #95 admission, HTTP Sales create/update/post, full business oracle, pinned recovery root, scoped evidence lease and permission preflight. No service recovery was rehearsed here.

## Known failures, aborts and retained allocations

The Task 2 complete-Security five-failure baseline, Task 3 aborted SQL run, Task 6 round 2 concurrent-suite interference and Task 6 round 4 initial 26/2 permission-preflight failure remain recorded in [B05a operations evidence](20261110-b05a-operations.md#failure-and-retained-resource-register). The two fresh round-4 allocations/evidence roots, all historical B03 retained resources and the two historical WEB5 SQL allocations remain untouched. A clean later affected selection cannot erase those dispositions.

## Gate disposition

| Gate | Disposition |
|---|---|
| B6-HOST-01 | BLOCKED / NOT RUN — no admitted published owned host |
| B6-COMP-01 | BLOCKED / NOT RUN — harness implemented, no actual old/current/restored lifecycle |
| B6-PAIR-01 | BLOCKED / NOT RUN — no actual SQL/browser pair or physical oracle |
| B6-LOAD-01 | NOT RUN / NOT MEASURED — no percentile, throughput or capacity claim |
| B6-REC-01 | BLOCKED / NOT RUN — no service RPO/RTO or restored-business admission claim |

The B03 dependency waiver authorized bounded implementation only; it is not a live PASS. Ordinary resend remains OFF. Staging is **NOT ACCEPTED** and production remains **NO-GO**. B06 remains unperformed.
