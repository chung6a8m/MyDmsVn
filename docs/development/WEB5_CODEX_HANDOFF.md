# Codex handoff — WEB5 backend

**Selected 2026-11-02; B01–B06 PLANNED / NOT RUN.** Read [AGENTS](../../AGENTS.md), [AI_CONTEXT](../../AI_CONTEXT.md), [plan selector](../plans/README.md), [owning plan](../plans/20261102-001-web5-operational-safety-backend.md), [safety design](../design/WEB5_OPERATION_SAFETY_PROTOCOL.md), [acceptance](WEB5_BACKEND_ACCEPTANCE.md), [TEST_POLICY](../TEST_POLICY.md), [WEB4 archived reference](WEB4_OPERATOR_HANDOFF.md), actual tests/lockfiles and latest `main`.

## Task order and independent PRs

| Branch suggestion | Deliver only | Dependencies |
|---|---|---|
| `codex/web5-b01-crash-proof` | Test-only process supervisor, failpoints, real SQL oracle for six commands | First; no product resend |
| `codex/web5-b02-terminal-negative` | Reviewed protocol/ADR, exact negative tombstone and tested versioned operator read | B01 real process crash PASS |
| `codex/web5-b03-operator-tools` | Separate CLI, audit/retention/rotation/backup rehearsal | B02 contract |
| `codex/web5-b04-sql-ci` | Protected isolated SQL CI and cleanup/failure guards | Selected SQL owners stable; may overlap B03 |
| `codex/web5-b05-full-iis` | Real IIS stage, limiter/audit/proxy hardening and paired Web F04 pilot | B01–B04 meaningful gates |
| `codex/web5-b06-release-handoff` | Evidence freeze, dated review and Owner decision dossier | Paired B05/F04 |

A task begins in an isolated worktree from verified current `main`, never the docs PR's stale SHA. Pin companion ReactWebUI source and `web4-v1` 48-blob corpus. Preserve exact12 web4, legacy-web11/exact11, Desktop exact10, SQL business handlers and independent CEX. Never add a browser SQL admin endpoint, manual journal DELETE, automatic retry or status404-derived unlock. A changed wire/schema requires a newly versioned additive contract and compatibility/permission tests.

## Copy-ready Codex task request

~~~text
Implement only Roadmap 05 backend Bxx, using docs/plans/20261102-001-web5-operational-safety-backend.md,
docs/design/WEB5_OPERATION_SAFETY_PROTOCOL.md, docs/development/WEB5_BACKEND_ACCEPTANCE.md,
docs/TEST_POLICY.md and actual current main source/tests.
Start isolated branch; map existing source/SQL lock-order/fixtures and publish exact change plan.
Create meaningful RED behavior/crash evidence before minimal changes when possible.
Do not use a test-only injection seam in published runtime. Preserve authoritative
operation journal same business transaction, frozen contract, exact mode/schema and grants.
For operator negative, absence/404/timeout is not rollback proof; only an audited
terminal tombstone under same per-key fence can certify NOT_EXECUTED.
Keep product same-key resend OFF until B01 and frontend separate gated acceptance.
Run focused actual SQL-free/SQL/child/IIS gates only in authorized owned environment.
Record exact source/test/runner hashes, discovered/executed PASS/FAIL/SKIP,
independent physical SQL oracle, cleanup, earlier failures and residual NOT RUN.
Request independent review of P0/P1/P2, open narrow PR to main; do not release production.
~~~

**Task PR requirements:** chosen Bxx, dependent Fxx/source pins, changed files and invariants, actual tests run, finite clean-up, sanitized logs, independent review, exact CI head, blocked/unsafe cases, follow-up sequencing. Apply existing repository Markdown/C# encoding policies. No fake SQL master, no global old test runner/test-gates, no broad unguarded SQL tests. Historical WEB4 evidence cannot be reused as current PASS. Documentation-only activation needs links/encoding/diff checks; it runs zero product tests.
