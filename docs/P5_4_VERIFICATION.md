# P5.4 verification — Inventory read/UI

**Date:** 2026-10-12  
**Scope:** Stock Balance and Stock Card read paths, Local adapter, shared desktop ViewModels, WinForms controls, shell navigation, and the P5 end-to-end automated slice.

## Implemented boundaries

- Application queries validate filters and require `Inventory.Balances.Read` or `Inventory.StockCard.Read` before dispatching to the read service.
- Dapper projects readable warehouse/product identity, active flags, quantities and receipt document identity without exposing persistence types.
- `IInventoryApiClient` is query-only. There is no desktop contract for directly mutating balances or ledger rows.
- Stock Balance supports warehouse/product/search filters and deterministic total-count paging. Stock Card supports warehouse/product and UTC date filters with deterministic `PostedAtUtc, LedgerId` ordering.
- Shared ViewModels provide loading, empty, validation/authorization/error states, totals, cancellation and request-generation stale-result protection. Only free-text search is debounced; selection changes and Refresh execute immediately.
- Both WinForms hosts reuse the same controls and shell navigation for “Tồn kho” and “Thẻ kho”.

## Automated evidence

The verification suite covers:

- contract JSON/read shapes and query-only client surface;
- Application permission checks, validation and handler forwarding;
- isolated SQL filtering, paging, ordering, inactive-history projections and document identity;
- Local adapter dispatch and error mapping;
- deterministic debounce, immediate filters/refresh, stale-result suppression and disposal;
- STA control rendering, totals and non-modal error behavior;
- shell workspace open/reuse and desktop DI composition;
- the complete automated business slice from receipt Post through balance/card reads, duplicate-Post rejection, deactivation failure and successful single Post after reactivation.

Final sequential verification results:

- `dotnet restore MyDmsVn.sln --locked-mode`: succeeded; all projects up to date.
- `dotnet build MyDmsVn.sln -c Release --no-restore -p:BuildInParallel=false`: succeeded with 0 warnings and 0 errors.
- `net48`: Application 120/120, SQL integration 97/97, Desktop 177/177.
- `net8.0` / `net8.0-windows`: Application 120/120, SQL integration 97/97, Desktop 177/177, Architecture 5/5.
- Both desktop hosts returned success from their Release `--smoke-test` entry point.

The two TFM groups were tested serially; no `net48` and `net8.0` test runs overlapped.

## Native GUI smoke and remaining blocker

The `net48` and `net8.0-windows` desktop executables start and render the shell with the new “Tồn kho” and “Thẻ kho” navigation entries without a modal error. Both hosts currently start with status “Not signed in”, and feature navigation is disabled because P6/P7 session/login bootstrap is not implemented. No security bypass or synthetic production session was added solely for smoke testing.

Therefore the authenticated manual walkthrough (catalog creation → Draft → Post → balance/card inspection → duplicate Post) remains open. Automated coverage is complete, but it is not represented as manual evidence.
