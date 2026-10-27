# UIN-T03 — App-local inline edit sessions

Date: 2026-10-23. Task: UIN-T03 / UIN-A04. Implementation commit: `e86164382985182b04061f78a7f3b6aadc9eb1eb`, based on verified T00–T02 commit `a5b6374c7c8943372b076645a7db1fbce5ca6d5e`. Scope is the Goods Receipt and Sales WinForms adapters and their `net48` GUI tests. No package was built.

Vendor pins were unchanged: BootstrapSourceGrid `2bd7874f7e80b22ef181763749c2a2938ab7f16e`, Bootstrap5WinFormUI `2be9a4d778fda7016207b75470020be6126f2f3f`, SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`. No SQL, schema, transport DTO, Desktop.Application, vendor or posting code changed.

## Behavior and ownership

`InlineEditSession<TLine>` owns a fresh target for each Begin, including `InlineValidationAddress` (document generation, line reference and field), original model/raw snapshots, pending raw text and the separately validated candidate. A conversion attempt revokes its preceding proof. The helper accepts only its current target, passes the stored candidate to admission/application and consumes eligibility before a model callback can reenter. An address whose line identity differs from the target is refused.

Both adapters retain native SourceGrid Begin/EndEdit and their own numeric parsing, Product selection rules, model setters and command admission. Callback guards check the controller's line/column/generation and the native grid, position, cell and editor before consulting the helper. The native accepted value must equal the validated candidate. Cancel invalidates the target before native cancellation and projects the **current** model; it does not restore the Begin snapshot. Sales continues to keep local and server validation issues separate in `InlineValidationStore`; the helper does not clear an unrelated issue. Save/Post/Back policy, seven Sales columns, gift/manual price behavior, lookup query versus selection, deferred structural refresh and uncertain-Post recovery remain in their existing owners.

## Selected verification

Interactive Windows `10.0.26300.0` on 64-bit OS, .NET SDK `8.0.426`, Release `net48`, explicit x86 build/testhost. The Light/Dark native tests logged `windowDpi=96` and `actualTestBitness=32`. Tests use serialized STA, `WinFormsTestGuard`, finite waits and disposal. `dotnet restore tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj --locked-mode` succeeded before the selected runs. Local TRX files are retained under `TestResults/` and are not committed.

| Run | Discovered / executed | Result |
|---|---:|---|
| Pre-change four GUI classes, `TestResults/uin-t03-baseline/uin-t03-baseline.trx` | 111 / 111 | PASS; 0 failed, 0 skipped |
| Helper, `TestResults/uin-t03-helper/uin-t03-helper.trx` | 4 / 4 | PASS; 0 failed, 0 skipped |
| Final five-class candidate, `TestResults/uin-t03-candidate/uin-t03-candidate.trx` | 117 / 117 | PASS; 0 failed, 0 skipped, 43 s |

The final TRX has 16 `GoodsReceiptInlineEditorTests`, 4 `InlineEditSessionTests`, 9 `SalesReceiptGridTests`, 24 `SalesReceiptControlTests` and 64 `SalesReceiptLookupNativeInputTests` rows. The two added adapter tests verify A/B native value mismatch, failed revalidation, unrelated callback, wrong-position callback and independent model projection without stealing proof or changing the active model; helper cases verify fresh target, line identity and reentrant exact-once use. Existing selected cases cover duplicate/zero-ID lines, current-model cancel, invalid raw/caret preservation, Product query-only navigation, Save/Post/Back admission, R4 feedback clearing and Light/Dark native Tab/Escape behavior. The initial expanded run had 115 pass and two test-harness reflection failures (`GetField` omitted `NonPublic`); after correcting those tests, the final candidate passed. No product failure was inferred from that harness error.

```powershell
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --filter 'FullyQualifiedName~GoodsReceiptInlineEditorTests|FullyQualifiedName~SalesReceiptGridTests|FullyQualifiedName~SalesReceiptControlTests|FullyQualifiedName~SalesReceiptLookupNativeInputTests|FullyQualifiedName~InlineEditSessionTests' --logger 'trx;LogFileName=uin-t03-candidate.trx' --results-directory TestResults/uin-t03-candidate --blame-hang --blame-hang-timeout 5m -- RunConfiguration.TargetPlatform=x86
```

## Candidate identity and limits

SHA-256 of saved bytes at the implementation commit:

| File | SHA-256 |
|---|---|
| `src/MyDmsVn.Desktop.WinForms/InlineEditSession.cs` | `2d011ef604a4659505d2fea40edefdddbde85eaa0961fe09092b8a82971d871d` |
| `src/MyDmsVn.Desktop.WinForms/GoodsReceiptLinesGridAdapter.cs` | `437dea11f72f9af8d9271c1955036da03e3a4ddf539080ba5ac4fc0f6f6e45df` |
| `src/MyDmsVn.Desktop.WinForms/SalesReceiptLinesGridAdapter.cs` | `efc83539db5ca1804feb6af2baab59765a6c34b57c3102826e0d4703c32af826` |
| `tests/MyDmsVn.Desktop.Tests/InlineEditSessionTests.cs` | `2eaf6fb83f22d9851f16a4bf5fa2327f62983656df7ab14088a688d043e6502a` |
| `tests/MyDmsVn.Desktop.Tests/GoodsReceiptInlineEditorTests.Sessions.cs` | `4a6a217fa3d72e9948073d40eecf2168b50354afe70a4aa5bdac2b3d288918e6` |
| `tests/MyDmsVn.Desktop.Tests/SalesReceiptGridTests.cs` | `838005b24461f09eb10a3d8df9258cb7a40b26ed5e13524e985d2b6c4aac8807` |
| Built `MyDmsVn.Desktop.WinForms.dll` | `87a888aafd85b407cc5bf34ed987b749615dd14e9ca23a6c650ec99a533b7f3a` |
| Built `MyDmsVn.Desktop.Tests.dll` | `8b99666cc9b0f11d0594af25393d64b91059fdfbfb105c781ba979632c3ba703` |

UIN-A04 is verified for T03's current receipt adapters at native100 in this selected scope. T05 will consume this mechanism with new numeric controls and must bring its own A04/A06 evidence. This run does not prove a package/actual Login, native125 on this T03 candidate, attended Owner acceptance, full SAL integration or production readiness. No SQL allocation occurred. `git diff --check` and C# BOM/CRLF checks passed; Markdown uses UTF-8 without BOM and CRLF in the working tree.
