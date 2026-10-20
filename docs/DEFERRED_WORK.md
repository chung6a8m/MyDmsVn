# Retired directions and scope boundaries

**Updated:** 2026-10-20. This page is a non-executable scope record. The owner retired all older unimplemented plans and selected [BCI](plans/20261020-001-business-compact-inline-goods-receipt.md) as the sole active roadmap. There are no tasks to execute from this page.

## Release prerequisites

Production release acceptance has not been granted. The retired REL/P6 plan was unimplemented; its removal does not turn pending evidence into PASS or authorize production deployment.

The following remain facts limiting release claims, without an execution schedule: supported Windows/SQL and legacy-credential decisions; clean-machine installation and compatible package/schema behavior; least-privilege runtime and separate administration; backup/restore and failure recovery; diagnostics/audit behavior; distribution/signing/support decisions; candidate-specific full release evidence and named sign-off. Current [packaging](LOCAL_RELEASE_PACKAGING.md), [SQL configuration](CONFIGURATION_AND_SQL_SECURITY.md) and [schema runbook](SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) remain technical references.

GUI-P5 is complete only in its signed scope, including the Warehouse 125% Dark Down NOT TESTED exception. A future BCI UI acceptance will likewise prove its defined UI behavior, not these unscheduled production prerequisites. A future release initiative requires a new owner-selected plan based on the then-current source; the deleted REL tasks are not implicitly queued.

## Out-of-scope directions

| Direction | Current boundary |
|---|---|
| P7 / HTTP / remote adapter | Not part of BCI; no new API host, remote security boundary or adapter parity claim |
| AppCore / modern runtime | Parked, not shipped; all active product/tests remain net48. The unimplemented reactivation procedure was deleted; no automatic target/lock/package restoration is authorized |
| New business modules | Sales/invoicing, returns, valuation/tax, transfers, lots/serials, sync and multi-tenancy are not added by a receipt presentation change |
| New receipt fields | Supplier/tax/discount/lot or other new business contracts require separate scope; BCI uses the existing receipt and line fields |
| Distribution tooling | No MSI, auto-updater or new migration framework is scheduled by BCI |

This table records exclusions, not a backlog or checklist. Any future selection needs a fresh design and evidence boundary.

## RELEASE-MANIFEST-01 — observed schema metadata discrepancy

This historical metadata defect is CLOSED in its narrow scope: the publisher reads the shipped runtime schema policy, and the clean-commit `d8013030` package reconciliation records schema 9 / scripts 001–009. [Accepted repair evidence](L2DB_VERIFICATION.md#clean-commit-package-reconciliation--release-manifest-01-closed) retains the original trial and artifact provenance. This heading preserves older evidence links; it introduces no task or renewed blocker. Correct metadata alone does not satisfy the release prerequisites above.

## Removed tooling remains removed

The former `tests/test-gates.v1.json`, SqlTestHarness, standalone SQL runner/runner tests, approval-file flow and related command wrappers remain removed. STAB-T03/T04 manifest-v2/fingerprint work remains cancelled. Respawn ownership stays inside existing SQL test owners; bounded GUI-owned processes do not recreate a global runner or authorize cleanup by database-name prefix.

[History](HISTORY.md#2026-10-20--business-compact-roadmap-reset) preserves immutable links to the retired plans and earlier decisions. Historical commands are not current execution authority. Use [the active index](plans/README.md) and the sole [TEST_POLICY](TEST_POLICY.md).
