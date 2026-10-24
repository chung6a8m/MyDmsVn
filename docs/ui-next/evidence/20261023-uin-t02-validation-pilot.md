# UIN-T02 — Product and Sales validation pilot

Date: 2026-10-23. Scope: UIN-T02 / UIN-A01 and UIN-A03, app-only `net48` Desktop.Application and WinForms. The execution parent is `a12ff92188496f960471f6da0eaaa0b40d37ff1c`, after T00/T01. This document identifies the final source/test candidate by its implementation commit and the saved-file hashes below. No package was built.

Vendor pins are unchanged: BootstrapSourceGrid `2bd7874f7e80b22ef181763749c2a2938ab7f16e`, Bootstrap5WinFormUI `2be9a4d778fda7016207b75470020be6126f2f3f`, SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`. No SQL, schema, business posting, transport DTO or vendor source was changed.

## Binding and ownership

`FieldValidationBinder` subscribes to the existing `INotifyDataErrorInfo` source and binds each Product field key to its existing editor. For example, `Bind("code", codeEditor, state => codeEditor.ValidationState = state, message => ...)` projects the current Code error into invalid state, field help and `ErrorProvider`. The Catalog summary lists all errors, including unbound fields, and its button navigates to the first bound error. The attempted Code text remains in the editor. Binding disposal removes the subscription and clears presentation.

Sales keeps validation in an app-local `InlineValidationStore` addressed by document generation, line object reference and field key. The VM maps `Request.Lines[i]` errors against the exact Save/Post submitted-row snapshot, preserving duplicate Product and zero-ID line identity. Indexed out-of-range or unknown fields remain in the summary without being attached to an unrelated row. Local parse and server issues are source-separated. Active editors project invalid border, accessible description and provider from the same current store; inactive cells/rows have indicators and accessible summaries. Enter navigation explicitly focuses the first error. Cancellation and resolved local input clear local presentation; editing one field does not claim that unrelated server or row-level errors are resolved. Document replacement, removal and session invalidation fence old errors. Raw edit buffers and uncertain-Post recovery remain owned by the existing adapter/VM.

## Tests and measured environment

Interactive Windows, .NET Framework 4.8, x86 GUI build/testhost, serialized STA and `WinFormsTestGuard`; Session project uses Release net48. Final TRX XML counters were inspected for selected, executed, pass, fail and not-executed rows.

| Run | Selected / executed | Outcome |
|---|---:|---|
| Sales VM server-issue RED | 2 / 2 | Two intended assertion failures on absent mapped issues; `TestResults/uin-t02-server-issues-red/uin-t02-server-issues-red.trx` |
| Sales active-editor RED | 2 / 2 | Two intended invalid-state assertion failures (`None` before projection) |
| Product/Catalog focused GUI | 30 / 30 | PASS, 0 skipped; `tests/MyDmsVn.Desktop.Tests/TestResults/uin-t02-product-final2.trx` |
| Sales focused GUI after review repair | 109 / 109 | PASS, 0 skipped; `TestResults/uin-t02-sales-gui-review-final/uin-t02-sales-gui-review-final.trx` |
| Final integrated Session, after file-format normalization | 73 / 73 | PASS, 0 failed, 0 skipped; `TestResults/uin-t02-session-candidate/uin-t02-session-candidate.trx` |
| Final integrated Product/Sales GUI, after file-format normalization | 139 / 139 | PASS, 0 failed, 0 skipped; `TestResults/uin-t02-gui-candidate/uin-t02-gui-candidate.trx` |

The integrated Session selector covered `SalesReceiptEditorViewModelTests`, `CatalogViewModelTests` and `DesktopViewModelBaseTests`. The integrated GUI selector covered `FieldValidationBinderTests`, `CatalogControlTests`, `CatalogAccessibilityAcceptanceTests`, `InlineValidationPresentationTests`, `SalesReceiptGridTests`, `SalesReceiptControlTests`, `SalesReceiptAccessibilityTests` and `SalesReceiptLookupNativeInputTests`. The final 73 and 139 runs are distinct projects; earlier overlapping runs are not added to them.

```powershell
dotnet test tests/MyDmsVn.Desktop.Session.Tests -c Release -f net48 --no-restore --filter 'FullyQualifiedName~SalesReceiptEditorViewModelTests|FullyQualifiedName~CatalogViewModelTests|FullyQualifiedName~DesktopViewModelBaseTests' --logger 'trx;LogFileName=uin-t02-session-candidate.trx' --results-directory TestResults/uin-t02-session-candidate
dotnet test tests/MyDmsVn.Desktop.Tests -c Debug -f net48 --no-restore -p:PlatformTarget=x86 --filter 'FullyQualifiedName~FieldValidationBinderTests|FullyQualifiedName~CatalogControlTests|FullyQualifiedName~CatalogAccessibilityAcceptanceTests|FullyQualifiedName~InlineValidationPresentationTests|FullyQualifiedName~SalesReceiptGridTests|FullyQualifiedName~SalesReceiptControlTests|FullyQualifiedName~SalesReceiptAccessibilityTests|FullyQualifiedName~SalesReceiptLookupNativeInputTests' --logger 'trx;LogFileName=uin-t02-gui-candidate.trx' --results-directory TestResults/uin-t02-gui-candidate -- RunConfiguration.TargetPlatform=x86
```

Product Light/Dark hosted cases measured `DeviceDpi=96`, client `940×700`, inspector width `340`. Sales Light/Dark hosted cases measured `DeviceDpi=96`, form client `944×700`, grid `912×330`; the last column remained within grid width. These are native100 observations only. Native125, package/Login and attended Owner observations were **NOT RUN**. A01/A03 therefore remain PARTIAL even though the selected source/hosted cases pass. No SQL allocation occurred. GUI controls and sessions were disposed by the selected tests; raw TRX remains local and is not committed.

## Candidate file identity

SHA-256 of final saved source/test bytes; no package hash exists.

| File | SHA-256 |
|---|---|
| `src/MyDmsVn.Desktop.Application/SalesReceiptEditorViewModel.cs` | `b402588b53abf7c057ce151719fca4fbefa0110df1331f17c3cf741cc8ff1fd3` |
| `src/MyDmsVn.Desktop.Application/SalesReceiptLineViewModel.cs` | `349f2947ed8fdd1c896a81accd558a49a90f7f1380404d4c339c29b218ed68d1` |
| `src/MyDmsVn.Desktop.Application/SalesReceiptValidationIssue.cs` | `dedad6bcd9795eb991c14ddaddddd3787dde6a3385d8381942735913aabd3c86` |
| `src/MyDmsVn.Desktop.WinForms/FieldValidationBinder.cs` | `875a85f3dba89ab2b952ad919b1c76f639524a651f4cc1fa0ac9d246c00493c2` |
| `src/MyDmsVn.Desktop.WinForms/InlineValidationStore.cs` | `2e7034b870566bb50c0704e0eaa4075c453da746db34348b103c239beed42bf7` |
| `src/MyDmsVn.Desktop.WinForms/CatalogControl.cs` | `308ec3af97dbeb27cb583125fea9a710613d292c68d64aa10c6e41a83f63b509` |
| `src/MyDmsVn.Desktop.WinForms/SalesReceiptControl.cs` | `46797173d9d0699c85398fcee85e903fb072a2fe651f524c867f929e91e43cde` |
| `src/MyDmsVn.Desktop.WinForms/SalesReceiptLinesGrid.cs` | `1961c8b8299a5cb486d88c712541d96b658171cd79f11cf7a2165b1652b0690c` |
| `src/MyDmsVn.Desktop.WinForms/SalesReceiptLinesGridAdapter.cs` | `0dc56833ced019e6848c866d70622ad7ed7249183ccca760f3bf7d5ca6f0162a` |
| `tests/MyDmsVn.Desktop.Session.Tests/SalesReceiptEditorViewModelTests.cs` | `972418e88e5e1299e79e486bc93a0cf6bb94e64e7cf2c39866eb3a41bd1c1c33` |
| `tests/MyDmsVn.Desktop.Tests/CatalogControlTests.cs` | `01365cd81762139f12af2a0044bc8d35dd760febe5fc96663698788b0f74289c` |
| `tests/MyDmsVn.Desktop.Tests/FieldValidationBinderTests.cs` | `9e57992dd36b512ddc96e06b0fa849b605679bbf20ced42da17be81400178d07` |
| `tests/MyDmsVn.Desktop.Tests/InlineValidationPresentationTests.cs` | `6e9f13b2846dea407c5c04d9680bfdb726f0ae3353618a04cd93df016422e42d` |
| `tests/MyDmsVn.Desktop.Tests/SalesReceiptGridTests.cs` | `1f4295e5cd0b195f7a78dcaf875e3d2bd9ee3b424e7d0ada0c27af7319839d8c` |

`git diff --check` and BOM/CRLF checks are part of the final handoff. The archive's SAL/native125/attended/release gaps are not reclassified by this pilot.
