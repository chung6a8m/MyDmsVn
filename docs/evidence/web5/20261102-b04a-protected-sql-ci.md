# WEB5 B04-A — protected hosted SQL baseline

**2026-11-02: implementation in progress; hosted RED and two GREEN runs NOT RUN. B04-A is not yet PASS. B04-B remains PLANNED / NOT RUN; B04 is incomplete.** [Owning plan](../../plans/20261102-001-web5-operational-safety-backend.md#5-b04--protected-sql-ci-split-into-b04-a--b04-b-web5-04) · [runbook/trust and cleanup design](../../development/WEB5_SQL_CI_BASELINE.md).

## Candidate and scope

Backend worktree branch `codex/web5-b04a-sql-ci-foundation`, starting clean main `fc36c879fdd2d46ee77d136bdb6f9e70f7191b64`. Existing WEB4 test source is separately pinned to that same SHA. B01 PR88 is unmerged and not imported. No production code, migration, permission keys, dependency lock or canonical48-blob corpus changes. No frontend, CEX, B02, IIS or production work.

## Local control validation

- PowerShell7 evidence guard:13 focused checks passed. A deliberate wrong-value RED recognition case first failed (expected250 incorrectly matched25); the matcher was narrowed to complete numeric lines, then all13 passed. This tests the guard, not SQL behavior.
- Windows PowerShell5.1 parsed all three scripts without syntax errors.
- Local Inventory discovery only:7 expanded cases from the exact baseline. No local SQL bodies executed for B04-A.
- Hosted runner inventory initially showed0 self-hosted runners and no configured environments. Selected hosted ephemeral private LocalDB, avoiding persistent/shared SQL credentials.

## Hosted evidence

Environment configured and read back: custom deployment policy ID65189351, tags web5-sql-baseline-* only,0secrets. actionlint1.7.7 passed. Independent review identified interrupted-check quarantine loss; persisted Checking/Completing phases now close that boundary. Actual hosted runs remain pending. Do not infer a SQL PASS from source counts, pure guard tests or this document.
