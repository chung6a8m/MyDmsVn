# P6-T03/P6-T04 verification record

Date: 2026-10-13 (Asia/Bangkok). Scope: detailed P6-T03 structured diagnostics/security audit and P6-T04 reproducible publish-directory artifacts.

## Implemented

- Serilog JSON rolling file sinks with host/runtime/version, operation, correlation and safe error code enrichment in both desktop composition roots.
- Non-NoOp structured security audit sink; login outcomes and Goods Receipt Post denied/success paths; success audit runs after durable commit.
- Post-commit audit sink failure reports `BusinessOperationCommitted=true` without converting a committed Post into a rollback error.
- Contract correlation ID, Local request isolation/cleanup, UI support ID and centralized nested-exception redaction.
- Sequential release script, locked restore, separate desktop/admin packages, secret/admin-artifact rejection and SHA-256 manifest.
- Diagnostics/troubleshooting and packaging/install/upgrade/uninstall runbooks.

## Automated evidence

- Baseline Release build: `dotnet build MyDmsVn.sln -c Release -m:1 --nologo` — Pass, 0 warnings/errors.
- Baseline tests ran sequentially. Architecture 5/5; Application 120/120 on each TFM; Desktop 186/186 on each TFM after one pre-existing net48 flaky test passed both isolated and full-suite rerun; SQL integration 161/161 on each TFM.
- TDD focused tests: correlation contract 1/1; Local correlation 1/1; Post audit ordering/deny/post-commit failure 3/3; production diagnostics/redaction/runtime failure/desktop reporting 5/5; release manifest/rejection 3/3; UI support ID/redaction 2/2.
- Final Release rebuild: `dotnet build MyDmsVn.sln -c Release --no-restore -m:1 -t:Rebuild --nologo` — Pass, 0 warnings/errors.
- Final review-fix verification ran sequentially, never with both TFMs active. Release rebuild passed with 0 warnings/errors; Architecture 5/5; Application 125/125 on each TFM; Desktop 197/197 on both `net48` and `net8.0-windows`; SQL integration 161/161 on each TFM.
- Earlier `net48` verification exposed order-dependent instability in two pre-existing async UI tests, as recorded in this PR history. The final review-fix run completed the full `net48` suite without splitting or retrying.
- Review regression fixtures verify complete Authorization/JWT and quoted-secret redaction, runtime audit file failure after successful initialization, degraded health/failure reporting without changing a committed result, and redacted desktop exception diagnostics with a UI support correlation ID.
- An actual pre-review `Publish-LocalRelease.ps1 -Version 0.6.0-p6t04` run passed and produced net48 x86, net8 x64 FDD and separate admin migrator directories. The final script additionally refuses dirty source trees and non-empty output directories; the generated `release-manifest.json` is the authoritative per-file SHA-256 evidence for each clean candidate run.

## Environment and blocked gates

- Windows `10.0.26200.0`, x64; .NET SDK `8.0.425`.
- Artifact publish/content/hash checks passed on the developer workstation.
- Clean VM install, prior-version upgrade, repair/uninstall, limited-user execution, full-disk behavior, signature trust and packaged interactive P5 walkthrough were not run. They remain Blocked/Not run, not Pass.
- P6-T06 SQL support/backup-recovery evidence and P6-T08 owner signing/release acceptance remain separate roadmap gates.
