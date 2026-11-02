# WEB5 B01 — Source checkpoint and process-crash proof design

**2026-11-02 — IMPLEMENTED; bounded nonproduction proof recorded in [B01 evidence](../evidence/web5/20261102-b01-crash-commit.md).** This document records the inspected source and the selected test-only proof design, not test results. Baseline: backend [`fc36c879fdd2d46ee77d136bdb6f9e70f7191b64`](https://github.com/chung6a8m/MyDmsVn/commit/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64). Actual candidate, discovered/executed counts, observed crash outcomes and cleanup belong in dated [WEB5 evidence](../evidence/web5/README.md).

[B01 owning task](../plans/20261102-001-web5-operational-safety-backend.md#2-b01--real-process-crashcommit-evidence-web5-01) · [owning safety protocol](WEB5_OPERATION_SAFETY_PROTOCOL.md#2-crash-classification-and-exactly-once-observational-rule) · [B5-CR-01–07 acceptance](../development/WEB5_BACKEND_ACCEPTANCE.md#traceable-mandatory-gate-ids) · [test policy](../TEST_POLICY.md).

The frozen `web4-v1` contract and current production runtime remain unchanged. B01 does not implement B02 terminal negative, enable browser resend, authorize full IIS or approve production. References below describe the pinned baseline; later line movement is not a change to the evidence pin.

## 1. Six real command paths

HTTP controllers construct the existing immutable context with `WebOperationWireInput.CreateContext`: original key, deployment epoch, kind, target and canonical SHA-256. All six commands use `WireApiController.Operation`, including Receipt through the shared base controller. See [SalesControllers.cs:25](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.HttpHost/SalesControllers.cs#L25), [InventoryControllers.cs:31](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.HttpHost/InventoryControllers.cs#L31) and [WebOperationWireInput.cs:18](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.HttpHost/WebOperationWireInput.cs#L18).

MediatR authorization binds the authenticated actor before the handler ([AuthorizationBehavior.cs:65](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.Application/Security/AuthorizationBehavior.cs#L65)). This initial check is not the transaction fence: journal admission rechecks current authority after acquiring the operation lock.

| Command / handler source | Begin / admission | Business operation | Original result / journal / Commit |
|---|---|---|---|
| Sales.Create — [SalesReceiptFeatures.cs:153](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.Application/Sales/SalesReceiptFeatures.cs#L153) | UoW create 160; Begin 161; ReplayAsync 162 | Reference validation 165; InsertDraftAsync 179 | Original Write DTO 180; CompleteAsync 181; Commit 182 |
| Sales.Update — [SalesReceiptFeatures.cs:211](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.Application/Sales/SalesReceiptFeatures.cs#L211) | UoW create 219; Begin 220; ReplayAsync 221 | Header lock 224; expected version 227; ReplaceDraftAsync 254 | Original Write DTO 255; CompleteAsync 256; Commit 257 |
| Sales.Post — [PostSalesReceipt.cs:49](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.Application/Sales/PostSalesReceipt.cs#L49) | UoW create 51; Begin 52; ReplayAsync 53 | Posting gate 56; header 57; snapshot 62; balances 73; mark 100; inventory 102 | Original Post result **with version** 106; CompleteAsync 107; Commit 108; external audit 109 |
| Receipt.Create — [GoodsReceiptFeatures.cs:258](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.Application/Inventory/GoodsReceiptFeatures.cs#L258) | UoW create 270; Begin 271; ReplayAsync 272 | Reference validation 275; InsertDraftAsync 288 | Original Write DTO 289; CompleteAsync 290; Commit 291 |
| Receipt.Update — [GoodsReceiptFeatures.cs:343](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.Application/Inventory/GoodsReceiptFeatures.cs#L343) | UoW create 361; Begin 362; ReplayAsync 363 | Header lock 367; expected version 380; ReplaceDraftAsync 413 | Original Write DTO 414; CompleteAsync 415; Commit 416 |
| Receipt.Post — [GoodsReceiptFeatures.cs:421](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.Application/Inventory/GoodsReceiptFeatures.cs#L421) | UoW create 442; Begin 445; ReplayAsync 446 | Posting gate 449; header 450; snapshot 469; balances 478; mark 499; inventory 510 | Original Post result **without version** 513; CompleteAsync 514; Commit 515; external audit 516 |

`ReplayAsync` calls the real journal `AdmitAsync`; a retained positive result is decoded and its original document identity checked before returning. Each handler commits the read/replay transaction and returns without executing business repositories or repeating Post success audit. A new operation encodes its result and inserts the journal in the same UoW before Commit. See [WebOperationContext.cs:93](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.Application/Operations/WebOperationContext.cs#L93).

## 2. Connection, transaction and lock ownership

`SqlUnitOfWorkFactory.CreateAsync` opens one connection; `SqlUnitOfWork` owns that connection and its current transaction. `BeginTransaction` calls the provider once. All registered business and journal repositories receive that same `ISqlExecutionContext`. `Commit` synchronously invokes the provider transaction's `Commit()` at line 113, then marks the UoW committed and disposes the transaction. If Commit throws, the caller has no reliable success/rollback classification; best-effort disposal is not authoritative rollback proof. Normal early-return paths dispose the active transaction, but process death cannot rely on managed `Dispose` running. See [SqlUnitOfWorkFactory.cs:41](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.Infrastructure/Persistence/SqlUnitOfWorkFactory.cs#L41) and [Commit/cleanup:109](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.Infrastructure/Persistence/SqlUnitOfWorkFactory.cs#L109).

The actual transaction ordering is:

```text
HTTP authentication / wire validation / initial application grant check
  |
  v
open UoW connection -> BeginTransaction
  |
  v
current actor check
  -> WebDeploymentState HOLDLOCK; exact epoch
  -> exclusive actor + operation-ID application lock
  -> Users HOLDLOCK -> WebAuthSessions HOLDLOCK
  -> current original Write/Post permission HOLDLOCK reads
  -> owned journal lookup (identity, expiry, original bytes)
  |
  +-- existing valid COMMITTED -> decode original result -> Commit -> response
  |
  +-- new operation
        -> Post only: valuation shared gate
        -> business reference/header/line locks
        -> Post only: sorted stock key locks, ledger/value/balance changes
        -> Create/Update only: source draft/line changes
        -> encode original command result
        -> INSERT COMMITTED journal in SAME transaction
        -> [BEFORE COMMIT barrier: all changes still uncommitted]
        -> synchronous provider Commit [uncertain race window]
        -> [AFTER COMMIT barrier: before handler return / HTTP response]
        -> Post external audit -> HTTP response
```

The Post-specific order is **operation fence/authority -> valuation gate -> header -> snapshot/references -> sorted balances -> business writes -> journal -> Commit**. Create/Update do not acquire the valuation gate or change inventory.

- [SqlWebOperationJournal.cs:30](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationJournal.cs#L30): one admitted intent per transaction; current actor, fence, session and current grant precede journal read/replay and business execution.
- [FenceAsync:109](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationJournal.cs#L109): epoch row uses `HOLDLOCK`; application-lock resource is `MyDmsVn.WebOperation:{actor}:{operation:N}`, `Exclusive`, `LockOwner='Transaction'`, `LockTimeout=10000`. A negative acquisition result raises `Operation.Unavailable`; it does not retry. Epoch is checked and held separately, not encoded in the lock name.
- [Authority:85](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationJournal.cs#L85): Users precedes session; permission rows/ranges and role membership use `HOLDLOCK`, including absent direct deny. Revocation after admission must wait for transaction completion; revocation before admission or while waiting on the key must be observed by the subsequent authority check. Permission derives from operation kind: Create/Update Write, Post Post.
- [InventoryValuationCoordinator.cs:31](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.Infrastructure/Inventory/InventoryValuationCoordinator.cs#L31): Post takes the shared `MyDmsVn.InventoryValuation.v1` gate with transaction ownership and 15000 ms lock timeout. Stock keys are sorted and locked with `UPDLOCK,HOLDLOCK` using `PK_StockBalances`; movements cannot start before all required keys are locked.
- [SqlRead.cs:14](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.Infrastructure/Persistence/SqlRead.cs#L14), [InventorySql.cs](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.Infrastructure/Inventory/InventorySql.cs) and [LinqToDbSessionFactory.cs:24](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/src/MyDmsVn.Server.Infrastructure/Persistence/LinqToDbSessionFactory.cs#L24): native/LINQ commands use 30-second command timeouts. LINQ borrows the exact real SqlConnection/SqlTransaction without owning or independently committing them. These command/lock limits do not establish a separate cancellable bound for synchronous Commit; the external process supervisor supplies the overall bound.

The same GUID under a different legitimately authenticated actor is a different journal scope; it is intentionally independent, not globally rejected. Cross-actor tests must reject a foreign result or mismatched session/actor, rather than misstate the key model.

## 3. Selected test-only host and barriers

The supervisor launches a **separate Windows test executable** containing an OWIN TestServer configured with the existing HTTP pipeline, real handlers, real SQL persistence and schema12 fixture. Killing this owned OS process terminates the actual process holding the mutation connection; replacing a TestServer inside the test runner is insufficient. This is process/HTTP/SQL proof, not an IIS/TLS/browser deployment claim.

The implemented test-only `IUnitOfWorkFactory` / `IUnitOfWork` decorator is installed only in the child DI graph; all business repositories and transaction operations delegate to the real UoW. Non-`none` crash phases admit only the six canonical keyed Sales/Receipt Create/Update/Post routes. The selected operation ID, deployment epoch, kind and target must match, and the real journal's `CompleteAsync` must successfully finish with that exact operation context on **this UoW** before its Commit can claim the one-shot barrier. The journal decorator records completion only after awaiting the production journal; it cannot turn an exception, positive replay, context/status read or unrelated transaction into mutation-completion evidence. `phase=none` permits ordinary protected recovery without a crash barrier. See [ControlChannel.cs](../../tests/MyDmsVn.WebOperation.CrashHost/ControlChannel.cs) and [CommitBarrier.cs](../../tests/MyDmsVn.WebOperation.CrashHost/CommitBarrier.cs).

No production option, environment-controlled runtime hook, controller kill route or shipping dependency is added. Existing test-only DI decoration precedent is [WebOperationCommandTests.cs:168](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/WebOperationCommandTests.cs#L168). These source-level controls have been reviewed; their candidate runtime verification is still **NOT RUN/pending**, not PASS.

Do not replace SqlConnection/SqlTransaction with generic wrappers: production native adapters cast them and LINQ verifies exact connection ownership. The UoW decorator preserves those underlying objects. It observes the before/after boundary around the original `_inner.Commit()` call rather than implementing a substitute SQL transaction.

| Observation | What the child/supervisor can establish | Required interpretation |
|---|---|---|
| Request not released | Supervisor has not released that original request to the child HTTP execution | A delayed original can still arrive later; absence is not terminal negative |
| Before Commit | Decorator reached Commit after real business work and journal completion, and is blocked before calling inner Commit | Kill the owned process; independent SQL must find no partial business/journal effects |
| Commit ready -> release -> commit-enter race | Supervisor releases the barrier; child reports entry adjacent to the synchronous inner Commit call; supervisor races process termination | A marker immediately before invocation does **not** prove SQL Server was physically inside COMMIT. Record observed markers/order and classify fresh SQL truth, accepting committed or rolled-back outcome |
| After Commit | Inner Commit returned successfully; decorator blocks before returning to the handler | Kill before HTTP acknowledgement; original committed bytes and exactly one business effect must survive restart |
| Restart/status | Fresh child/service lifetime and current authenticated protected status | Return exact retained original result or nonaffirmative status; never synthesize rollback from 404/timeout |

The commit-race case is deliberately nondeterministic. Do not force it to alternate outcomes or classify a missing after-Commit marker as rollback. Evidence must distinguish ready/release/entry/returned markers, externally observed process exit and the independent SQL result. If timing never establishes the intended race exposure, record that limitation; do not rename a deterministic pre-Commit kill as proven in-server Commit loss.

Delayed-original/replay workers must own independent connections/process execution. Observe actual SQL blocking against the expected owned holder where possible, then release or kill the holder under a finite budget. A launched Task or elapsed sleep is not proof that the second worker reached the same fence. Exercise exact same frozen intent, changed key-bound inputs, current authority and expiry without adding a product retry path.

## 4. Independent oracle and original results

After owned process termination/drain, the test uses a fresh, nonpooled supervisor SQL connection to the exact owned fixture database. The bounded quiescence observer checks database sessions, active requests, user transactions and sessions attributable to the recorded owned child PIDs. Before outcome classification it excludes only itself and the one known retained arrangement/admin session; final teardown closes that admin scope and permits only the fresh observer. This check must also detect a transaction still recovering without a normal active request. Source: [WebOperationCrashSqlTests.cs](../../tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationCrashSqlTests.cs); candidate execution remains **NOT RUN/pending**.

Once quiescence is established, the observer opens an explicit transaction, verifies the exact deployment epoch with `HOLDLOCK`, and acquires the same `MyDmsVn.WebOperation:{actor}:{operation:N}` exclusive application lock with `LockOwner='Transaction'`. The observation has finite connection, command, lock and overall budgets. Commit releases this proof lock **before** fresh result classification or explicit replay; it is not held across replay and is not a new terminal disposition. No owned writer may remain while the lock is released and the physical oracle is read.

Use committed reads, never `NOLOCK`. An ordinary assertion, read-only oracle query or test-body failure still FAILS the case, including an ordinary oracle query timeout; successful later cleanup cannot convert failed classification into PASS. Safe cleanup may resolve worker uncertainty only through the complete independent proof below. Any failed or timed-out quiescence, epoch/key fence or cleanup proof is terminal: poison further test admission and quarantine the allocation before any drop. That poison is never cleared by a later successful observation. Do not infer rollback from the child exit code, HTTP timeout, first absent row or successful database deletion.

Compare the exact operation with its pre-dispatch baseline:

- Journal: actor/key/epoch/kind/target/fingerprint, contract/codec, disposition, result document ID and original bytes; either absent together with unchanged business rows, or one matching COMMITTED outcome together with the complete business effect. Absence is only a test observation after controlled quiescence, never a client NOT_EXECUTED certificate.
- Create/Update: source header and ordered server line IDs, original version, raw business values and durable actor columns. Preserve Sales retained line IDs versus Receipt replacement identities; compare results to the relevant command-time state, not a later edited document.
- Post: source Posted state/version and actor, all ledger/source-line links, valuation companion rows, balances and moving-average cost/COGS. Include repeated Sales product/gift rows and separate warehouse keys; aggregate total counts alone cannot prove correctness. Sales Post includes its original version; Receipt Post has none.
- Explicit identical replay after restart: original command result must match the retained result and physical state must not change. Protected status works with original Write/Post granted even when ordinary Read is denied; revoked original authority denies.

A rolled-back Create can consume identity/sequence values. Gaps alone are not a partial business effect and must not make the rollback oracle fail. The existing HTTP test `State()` includes `sys.sequences.current_value` ([WebOperationHttpSqlTests.cs:638](https://github.com/chung6a8m/MyDmsVn/blob/fc36c879fdd2d46ee77d136bdb6f9e70f7191b64/tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationHttpSqlTests.cs#L638)); do not reuse that whole snapshot as pre-Commit crash equality. Keep source/journal/ledger/value comparisons separate from allocation counters.

**Actor audit caveat:** CreatedBy/UpdatedBy/PostedBy and ledger PostedBy are durable business columns in the same transaction. The external `ISecurityAuditWriter` Post-success call runs **after** Commit (Sales line 109; Receipt line 516). A crash at the after-Commit barrier can prevent that external notification, and replay deliberately does not repeat it. Verify durable actor provenance independently and report external-audit delivery as a limitation; do not assert that external audit is atomic with business Commit or alter runtime auditing within this test task.

## 5. Process/control ownership and cleanup

Reuse the existing Windows `GuiOwnedProcess` Job Object containment pattern for the dedicated test child: start suspended, assign the owned job before resume, do not permit breakaway, and kill/drain only that owned tree. The supervisor must retain the exact process/job ownership and bounded exit/pipe-drain observations; never locate or kill a shared IIS worker, test runner or process by a generic name.

The implemented control channel uses a fresh current-user-only named pipe. Before writing configuration or enabling the writer's automatic flush, the supervisor compares kernel `GetNamedPipeClientProcessId` with the exact Job-owned root PID supplied by `GuiOwnedProcess`'s launch callback. A mismatch withholds SQL connection/token configuration and closes the owned child. The subsequent ready frame's PID must match that same root; a PID merely declared by the peer is not sufficient. See [WebOperationCrashProcess.cs](../../tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationCrashProcess.cs) and [GuiOwnedProcess.cs](../../tests/MyDmsVn.Desktop.Tests/GuiOwnedProcess.cs).

Both supervisor and child read frames incrementally, checking the remaining size **before** each buffer append. Each JSON frame is capped at `2 * 1024 * 1024` decoded characters (the implementation's unit is characters, not a promise of a 2 MiB UTF-8 byte limit). Strict parsing rejects duplicate properties, excess depth and trailing JSON; malformed/truncated, oversized or out-of-order frames and finite I/O timeouts fail closed. Parser diagnostics do not echo raw protocol contents. Keep SQL connection strings, tokens and raw business/result data out of command lines, stdout, tracked files and sanitized evidence. Test control does not become an HTTP route in the production pipeline. Source review of these controls is not an executed IPC acceptance result.

Cleanup/drop is permitted only after **all** owned child trees and competing workers have drained, the HTTP scope has disposed successfully, a fresh final SQL observer has proved no owned sessions/requests/user transactions remain and acquired/released the exact epoch/key transaction fence, and the existing fixture has revalidated the allocation token/SID/database ownership. All these conditions remain mandatory after an assertion, ordinary oracle-query timeout or other body failure; the case remains FAIL even when cleanup is safe. A failed or timed-out quiescence/fence/cleanup proof, uncertain tree drain or ownership mismatch permanently poisons the run and retains the allocation in quarantine for exact-ID review; later success cannot authorize its drop. Never bulk-drop by prefix or claim clean teardown from a best-effort finally block. Do not restore a general SQL runner or broaden runtime SQL grants.

## 6. Existing coverage and new proof boundary

At the inspected baseline, `WebOperationJournalTests` proves transaction-owned key waits, post-wait authority, held revocation/epoch locks, timeout/cancellation and terminal retention using live SQL. `WebOperationReadTests` covers all six original permissions and status waiting. `WebOperationHttpSqlTests` covers six-command original readback, duplicate requests, current authority and physical inventory; its lines 21 and 213–215 explicitly exclude OS-crash proof. `WebOperationCommandTests` covers original outcomes and pre-Commit codec/journal exceptions. These are useful regression owners, not substitutes for a killed child.

B01 adds the externally supervised process-death evidence required by B5-CR-01–07. Record actual named test discovery/execution and each observed crash window at the candidate; do not turn this design's six-command table into an assumed passing case count. Run SQL owners sequentially under the approved nonproduction policy, keep raw sensitive diagnostics outside Git, and publish sanitized source/child artifact hashes, classification and owned cleanup in the separate task evidence. Until those executions and review occur, the implementation and this design confer **no crash/Commit PASS**.
