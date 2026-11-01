# Retired directions and scope boundaries
> **WEB2 boundary update, 2026-11-01:** The expressly selected net48 HTTP/JWT/Sales parallel pilot [completed with Owner-accepted full-IIS exception](archive/20261101-web2-http-jwt-sales/README.md); it is no longer an active implementation queue. The original UI-NEXT HTTP exclusion in this historical table still applies only to UI-NEXT, not the implemented WEB2 host. **Full IIS NOT RUN, no production rollout approved.** The [current plan selector](plans/README.md) retains the independent CEX WinForms selection.


**New WEB3 selection 2026-11-01:** the [WEB3 HTTP/Inventory server companion](plans/20261101-002-web3-goods-receipt-inventory-http.md) is active at **PLANNED / docs-only** stage alongside the independently active CEX WinForms workstream. Existing net48 WEB2 host is implemented and retained; WEB3 will extend that host. Historical UI-NEXT exclusion of new HTTP work **does not** prohibit separately selected WEB3. Neither WEB3 activation nor WEB2 IIS Express evidence approves full IIS/production, operational audit/limiter policy, durable draft or idempotent operation journal.

**Routing updated:** 2026-10-24; the prior scope record used merged PR #43 baseline `7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639`. This page remains non-executable. Use the [execution index](plans/README.md) for current work; there are no tasks to execute from this page.

## Active scope is recorded elsewhere

UI-NEXT's workspace feedback, validation, tab close/busy and theme work remains recorded in its [historical reference index](ui-next/README.md). The [execution index](plans/README.md) selects current scope. The [archive boundary](archive/20261023-pre-ui-next/README.md) retires prior queues and excludes their unfinished work, including SAL native125/attended/Owner acceptance and complete integration repeats. Exclusion does not complete or waive those gaps. Current Sales/schema10 and other business contracts remain implemented source behavior.

[SAL-T08 evidence](evidence/sal/20261021-t08-integrated-acceptance.md) retains product/package `f5039cb`, separate GUI test `4697744`, candidate-specific runnable automation PASS and its OPEN acceptance boundary. BCF/BCI/GUI-P5/L2DB/RESP retain their recorded signed scopes and exceptions. The broader operational rollout, support and recovery requirements below remain unscheduled and cannot be inferred from feature tests.

## Release prerequisites

Production release acceptance has not been granted. The retired REL/P6 plan was unimplemented; its removal does not turn pending evidence into PASS or authorize production deployment.

The following remain facts limiting release claims, without an execution schedule: supported Windows/SQL and legacy-credential decisions; clean-machine installation and compatible package/schema behavior; least-privilege runtime and separate administration; backup/restore and failure recovery; diagnostics/audit behavior; distribution/signing/support decisions; candidate-specific full release evidence and named sign-off. Current [packaging](LOCAL_RELEASE_PACKAGING.md), [SQL configuration](CONFIGURATION_AND_SQL_SECURITY.md) and [schema runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) remain technical references.

GUI-P5 is complete only in its signed scope, including the Warehouse 125% Dark Down NOT TESTED exception. BCI UI acceptance is complete in its signed scope; BCF acceptance covers its recorded lookup/presentation obligations and explicit exceptions, not these unscheduled production prerequisites. A future release initiative requires a new owner-selected plan based on the then-current source; the deleted REL tasks are not implicitly queued.

## Out-of-scope directions

| Direction | Current boundary |
|---|---|
| P7 / HTTP / remote adapter (historical UI-NEXT limit) | Excluded from original UI-NEXT only; WEB2 separately implemented scoped net48 HTTP/JWT Sales pilot and WEB3 is now selected to extend it to GoodsReceipt/Inventory. Full remote parity beyond selected features and production acceptance remain out of scope. |
| AppCore / modern runtime | Parked, not shipped; all active product/tests remain net48. The unimplemented reactivation procedure was deleted; no automatic target/lock/package restoration is authorized |
| Business expansion beyond implemented contracts | Orders/POS, payments, debt, returns/reversal, pricing engine, tax/fiscal invoicing, FIFO/periodic/revaluation, transfers, lots/serials, sync and multi-tenancy remain excluded; direct sales and moving-average valuation are implemented source contracts with archived incomplete acceptance |
| Unapproved receipt fields | Supplier/tax/discount/lot and other fields beyond the explicit SAL seven-column/header contract remain excluded; Goods Receipt keeps its business fields |
| Distribution tooling | No MSI, auto-updater or new migration framework is scheduled by UI-NEXT |

This table records exclusions, not a backlog or checklist. Any future selection needs a fresh design and evidence boundary.

## RELEASE-MANIFEST-01 — observed schema metadata discrepancy

This historical metadata defect is CLOSED in its narrow scope: the publisher reads the shipped runtime schema policy, and the clean-commit `d8013030` package reconciliation records schema 9 / scripts 001–009. [Accepted repair evidence](L2DB_VERIFICATION.md#clean-commit-package-reconciliation--release-manifest-01-closed) retains the original trial and artifact provenance. This heading preserves older evidence links; it introduces no task or renewed blocker. Correct metadata alone does not satisfy the release prerequisites above.

## Removed tooling remains removed

The former `tests/test-gates.v1.json`, SqlTestHarness, standalone SQL runner/runner tests, approval-file flow and related command wrappers remain removed. STAB-T03/T04 manifest-v2/fingerprint work remains cancelled. Respawn ownership stays inside existing SQL test owners; bounded GUI-owned processes do not recreate a global runner or authorize cleanup by database-name prefix.

[History](HISTORY.md#2026-10-20--business-compact-roadmap-reset) preserves immutable links to the retired plans and earlier decisions. Historical commands are not current execution authority. Use the [current execution index](plans/README.md) and the sole [TEST_POLICY](TEST_POLICY.md).
