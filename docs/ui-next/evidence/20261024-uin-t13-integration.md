# UIN-T13 — final UI Next integration and handoff

Date: 2026-10-24. Acceptance: UIN-A14 and reconciliation of UIN-A01–A13. Status: **IN PROGRESS**; package smoke has failures and native125/attended Owner review are pending. This record concerns UI Next only. Archived SAL acceptance and production release remain separate.

## Frozen identities and scope

The application and package source is `98d84c32e7de6cfebad38c5900450f8b90e6859c` (T12 source plus the initial T13 package probe). Subsequent test-only commits are `842f2c76a3c9eef88cd545518f49ebae867ff0fe` and `f3ce391` (diagnostics); no product or vendor source changed. BSG pin `a2c2cb749ac5656a56b1cfebfc7718b889d95152`, UI pin `910c19493b36d1db2fb0b92b7fc436f263ec564d`, SourceGrid pin `f4e457b43582bf01892f50bdc74aa480531e5944`. The clean schema10 x86/net48 package was produced by `deployment/release/Publish-LocalRelease.ps1` at `.local/t13/package` with manifest SHA-256 `FDE9285123C229AE8FCA4B35F8D426FABF6E3B3531C4EF31098D59CA6E626714` and Desktop executable SHA-256 `3036A78F3618C4E11F8C07AD45AEBB002D05D6BF18260ACBF4FDB9677DC8E5CB`. Package and test driver revisions are recorded separately.

T00–T12 retain their linked, bounded task evidence in the [roadmap](../20261023-active-roadmap.md). T13 adds a real package Product duplicate-Code assertion: the duplicate remains a draft and displays the server's field error. The initial package probe expected the mock text `Code already exists.`; the actual server returns `A product with this code already exists.`. That test expectation was corrected without a product change. The current diagnostic probe captures UIA state at Goods failures and gives the controlled timeout path time to report its own result.

## Current automated observations

Interactive Windows, .NET Framework 4.8, x86 package and testhost for package probes, SDK 8.0.426. The designated non-production SQL test configuration has effective `master` and no attach/user-instance option. Every selected package row uses the existing individually allocated database, runtime identity separation, worker drain and ownership-checked disposal. No SQL configuration, raw identifiers, credentials or raw TRX are published here.

| Scope | Local TRX | Result |
|---|---|---|
| Desktop.Session complete project | `TestResults/t13-session-final/t13-session-final.trx` | 216/216 PASS, no skip |
| Server Application Catalog and API response mapping | `TestResults/t13-server-app/t13-server-app.trx` | 41/41 PASS, no skip |
| Affected Catalog, shell, Goods/Sales, date, numeric, close/busy, feedback native GUI at measured 100% | `TestResults/t13-native100/t13-native100.trx` | 305/305 PASS, no skip; 28 PerMonitorV2 observations at monitor 96 DPI |
| Product duplicate-Code and catalog package flow, Light/Dark native100 | `TestResults/t13-package-product-100-fix/t13-package-product-100-fix.trx` | 2/2 PASS, no skip |
| Actual Login, Sales list/audit/uncertain Post, Goods audit/uncertain Post package flows, Light/Dark native100 | `TestResults/t13-package-flows-100/t13-package-flows-100.trx` | 7/12 PASS, 5 FAIL, no skip |

The first package invocation declared 125% while the physical monitor measured 96 DPI/100%; its two rows failed before Login and are excluded from native125 evidence. The first Product run at native100 failed 2/2 because the probe sought a mock error sentence; the corrected real-server selector passed 2/2. The package flow failures remain open: Goods uncertain Post timed out before the `uncertain` signal in both themes despite an observed owned SQL WAITFOR; Goods audit Light did not find the duplicate-Post message and Dark reached its finite process timeout; Sales audit Dark failed at a post-state inline cell query while Light passed. The harness reported verified process cleanup and no combined SQL-disposal error. There are no surviving GUI/testhost processes after the run. A targeted diagnostic rerun is required before classifying product versus probe failures.

## Pending handoff

Native125 Light/Dark on the same application package/source, targeted package diagnostic and repair runs, attended UI Next keyboard/error/contrast/close/busy observations, and a dated bounded Owner disposition remain pending. UIN-A14 is not PASS. Prior T00–T12 task-level evidence does not substitute for these final gates. No archived task or release acceptance is reopened.
