# P6 — Local v1 production hardening

Status: Not started. Depends on P5 functional acceptance. Read `docs/PRD.md`, `docs/SECURITY.md` and `docs/TEST_STRATEGY.md`.

## Objective

Turn the completed Local Mode vertical slice into a supportable, installable dual-target Windows application with controlled schema upgrades.

## Task list

- [ ] **P6-T01 / Configuration:** validate deployment config, connection strings, secret distribution and least-privilege SQL logins; no production secrets in source; sensitive data redacted from logs.
- [ ] **P6-T02 / Migration deployment:** run DbMigrator separately via installation/admin path with DB backups and schema version checks; test first install + upgrade + repeat.
- [ ] **P6-T03 / Logging and diagnostics:** Serilog structured logs, correlation ID, application error codes, privacy-safe auditing and troubleshooting docs.
- [ ] **P6-T04 / Packaging:** define net48 and net8.0-windows installer/publish settings, runtime prerequisites, architecture x86/x64 and dependency packaging; updater choice only if needed.
- [ ] **P6-T05 / Functional smoke:** launch each host, log in, catalog CRUD, Draft receipt, Post, stock balances/card, permissions, application restart; confirm no unintended dialogs.
- [ ] **P6-T06 / Operational safeguards:** SQL backups, failed deploy recovery, database compatibility matrix, downtime/rollback plan, connection failure behavior.
- [ ] **P6-T07 / Nonfunctional regression:** safe concurrency, canceled operations, large grid paging, slow SQL handling, error-message localization and validation UX.
- [ ] **P6-T08 / Release review:** known limits (2-tier trust/security, no sales-order/outbound stock, net8 lifecycle), verified test matrix, signed artifacts if deployment policy requires.

## Exit evidence

Documented reproducible install/upgrade/recovery and passing Windows smoke tests for both desktop hosts. SQL credentials scoped to necessary actions and migrator isolated from clients.

## Avoid

Claiming v1 as a trusted independent middle tier, shipping DB owner credentials with every client, launching migrations from every login, skipping SQL rollback drills.
