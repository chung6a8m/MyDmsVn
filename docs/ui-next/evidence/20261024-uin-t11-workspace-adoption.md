# UIN-T11 — existing workspace adoption

Date: 2026-10-24. Scope: UIN-A12 and nullable Sales filter portion of UIN-A07. This is bounded source and GUI evidence. Package Login, dated Owner review and release disposition remain UIN-T13.

## Candidate and behavior

App source/test revision `e0dbf0c` from baseline `e1f682c5b150042e11c821a098fac0c2701839ce`; BootstrapSourceGrid pin `a2c2cb749ac5656a56b1cfebfc7718b889d95152`; Bootstrap5WinFormUI pin `910c19493b36d1db2fb0b92b7fc436f263ec564d`; SourceGrid pin `f4e457b43582bf01892f50bdc74aa480531e5944`. No vendor source or pin changed.

| Workspace | T11 disposition |
|---|---|
| Product, Warehouse, Employee, Customer | One `CatalogControl` implementation already composes local feedback, field validation, busy host, close admission and semantic inspector for all four factory variants. T11 retains that shared composition and its existing focused regressions. |
| Sales Receipt | From/To now use nullable `BootstrapDateInput`. Explicit clear commits null; invalid or reversed range rejects Apply without a list query; unrelated state projection preserves pending text and caret. Sign-out clears sensitive pending text. Existing required Sale Date, seven columns, list pagination, Save/Post admission and recovery remain. Operation status uses a semantic label. |
| Goods Receipt | Factory supplies one session-bound feedback scope to list, editor and lookup ViewModels; admitted query/write operations capture their own scope before starting. The shell hosts that scope. Errors display in the owning tab with sanitized Support ID. Existing required Receipt Date, inline grid, close/save outcome and recovery remain. Status labels use theme roles. |
| Stock Balance and Stock Card | Each factory-created control owns a session-bound feedback scope and query notifications; query failures appear locally and hidden-tab attention remains with that instance. Session replacement and scope disposal reject late callbacks. Semantic status labels report errors. |

Native exceptions: Stock Card retains `DateTimePicker` for UTC seconds and inclusive displayed-second upper bound. Stock views retain a native query-only `ProgressBar`; it does not gate commands. Receipt grids retain the pinned SourceGrid adapters. Native layout containers remain where no repeated theme behavior calls for a semantic surface. See [control examples](../20261023-design-and-control-policy.md#t11-adoption-examples-and-retained-native-controls).

## Verification

Interactive Windows, net48/x86 app candidate, SDK 8.0.426 and VSTest 17.11.1. No SQL connection, schema, package Login or production probe was used. Actual TRX rows below have zero skips.

| Scope | Local TRX | Outcome |
|---|---|---|
| Affected GUI Catalog/Sales/Goods/Stock/shell, including Light/Dark typed filter input, hidden A/B workspace behavior and session replacement at native100 | `TestResults/t11-gt-source-commit-native100/t11-gt-source-commit-native100.trx` | 141/141 PASS; `PerMonitorV2`, monitor 96 DPI / 100%. |
| Same affected GUI selection on the same source/test revision at native125 | `TestResults/t11-gt-source-commit-native125/t11-gt-source-commit-native125.trx` | 141/141 PASS; 14 `PerMonitorV2` observations measured monitor 120 DPI / 125%, zero 100% observations. The Windows host reported `WINDOW_DPI=96`; this is recorded separately from physical monitor DPI. |
| Focused feedback, inventory, Sales list and Catalog Session tests | `TestResults/t11-st-final/t11-st-final.trx` | 77/77 PASS. |
| Bounded Goods, Stock and Sales DI/shell smoke without SQL/Login | `TestResults/t11-host-source-commit/t11-host-source-commit.trx` | 3/3 PASS. |
| Release app x86 build | `dotnet build src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -c Release -p:Platform=x86 --no-restore -v:q` | PASS, zero warnings/errors. |

An earlier T11 run declared expected125 while the physical display still measured 96 DPI/100%; its two scale assertions failed as designed. It does not count as native125 evidence. Its remaining rows are not substituted for a correctly measured run.

Release x86 executable SHA-256: `EB02DCCB55546F8F4F5987D1557F9CD1F00315BD52D8AE41FBFD4DB553DBBA56`. Native100 GUI test DLL SHA-256: `DCBD07FD7086ED18BA650232ECF57478249125B3086F16A5B8711016AC0BA91E`; native125 GUI test DLL SHA-256: `7C01A5090FB8EB15FC1F7AC18CF76983B4B07443DE67BAA5C431DE573DDA0323`. Both builds use app source/test revision `e0dbf0c`; no source or vendor pin changed between DPI runs.

## Boundary

T11 changes UI presentation/admission only. It does not alter Catalog fields, receipt business rules, seven Sales columns, stock queries, pagination, permissions, SQL schema or public transport DTOs. T12 staged paste and T13 packaged/Owner acceptance remain separate.
