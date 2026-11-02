# Codex handoff — WEB5 backend

**Selected 2026-11-02; B01–B06 PLANNED / NOT RUN.** Read [AGENTS](../../AGENTS.md), [AI_CONTEXT](../../AI_CONTEXT.md), [plan selector](../plans/README.md), [owning plan](../plans/20261102-001-web5-operational-safety-backend.md), [safety design](../design/WEB5_OPERATION_SAFETY_PROTOCOL.md), [acceptance](WEB5_BACKEND_ACCEPTANCE.md), [TEST_POLICY](../TEST_POLICY.md), [WEB4 archived reference](WEB4_OPERATOR_HANDOFF.md), actual tests/lockfiles and latest `main`.

## Task order and independent PRs

| Branch suggestion | Deliver only | Dependencies |
|---|---|---|
| `codex/web5-b01-crash-proof` | Test-only process supervisor, failpoints, real SQL oracle for six commands | In parallel with B04-A; no product resend |
| `codex/web5-b04a-sql-ci-foundation` | Isolated protected SQL lane, existing owners, intentional RED and owned cleanup | May parallel B01; PASS before B02 runtime |
| `codex/web5-b02-terminal-negative` | B02 ADR, exact12→13 if necessary, additive vs strict old-client protocol, six-command durable negative and versioned read | B01 AND B04-A proven, B04-B required before merge |
| `codex/web5-b04b-sql-ci-terminal` | New B02 six-command race/migration/compatibility selectors and exact-candidate CI | Pass before B02 acceptance; B04 not DONE until A+B |
| `codex/web5-b03-operator-tools` | Operator CLI, positive COMMITTED/result unavailable ticket reconciliation, audited retention/rotation/backup rehearsal | B02 and B04-B accepted |
| `codex/web5-b04-sql-ci` | Protected isolated SQL CI and cleanup/failure guards | Selected SQL owners stable; may overlap B03 |
| `codex/web5-b05-full-iis` | Real IIS, old/new cached-tab matrix, limiter/audit/proxy, p95/p99+RPO/RTO baseline, Web F04 pilot | B01–B04 (including B04-A/B) PASS |
| `codex/web5-b06-release-handoff` | Positive/negative case closure and numeric threshold-vs-measured Owner decision dossier | Paired B05/F04 |

A task begins in an isolated worktree from verified current `main`, never the docs PR's stale SHA. B01 and B04-A may be implemented in parallel; B02 executable changes require B01+B04-A. The protected B04-B SQL lane must PASS at B02 HEAD before B02 acceptance; B04 as a whole is only DONE when A+B pass. Pin companion ReactWebUI source and `web4-v1` 48-blob corpus. Preserve exact12 web4, legacy-web11/exact11, Desktop exact10, SQL business handlers and independent CEX. Never add a browser SQL admin endpoint, manual journal DELETE, automatic retry or status404-derived unlock. A changed wire/schema requires a newly versioned additive contract and compatibility/permission tests.

## Copy-ready Codex task request

~~~text
Implement only Roadmap 05 backend Bxx, using docs/plans/20261102-001-web5-operational-safety-backend.md,
docs/design/WEB5_OPERATION_SAFETY_PROTOCOL.md, docs/development/WEB5_BACKEND_ACCEPTANCE.md,
docs/TEST_POLICY.md and actual current main source/tests.
Start isolated branch; map existing source/SQL lock-order/fixtures and publish exact change plan.
Create meaningful RED behavior/crash evidence before minimal changes when possible.
Prepare protected SQL CI B04-A before B02 changes; run B04-B on exact B02 candidate before acceptance.
B02 ADR selects schema12 reuse or explicit exact12->13 path, and additive WEB4-safe old clients
or strict server-side per-write/status version fence. Prove all six commands x two lock orders.
Do not use a test-only injection seam in published runtime. Preserve authoritative
operation journal same business transaction, frozen contract, exact mode/schema and grants.
For operator negative, absence/404/timeout is not rollback proof; only an audited
terminal tombstone under same per-key fence can certify NOT_EXECUTED.
Keep product same-key resend OFF until B01 and frontend separate gated acceptance.
Add COMMITTED-but-original-result-expired positive case closure WITHOUT edit unlock,
an old/new cached-client IIS matrix, and measured RPO/RTO/p95/p99 Owner-approved release gates.
Run focused actual SQL-free/SQL/child/IIS gates only in authorized owned environment.
Record exact source/test/runner hashes, discovered/executed PASS/FAIL/SKIP,
independent physical SQL oracle, cleanup, earlier failures and residual NOT RUN.
Request independent review of P0/P1/P2, open narrow PR to main; do not release production.
~~~

**Task PR requirements:** chosen Bxx, dependent Fxx/source pins, changed files and invariants, actual tests run, finite clean-up, sanitized logs, independent review, exact CI head, blocked/unsafe cases, follow-up sequencing. Apply existing repository Markdown/C# encoding policies. No fake SQL master, no global old test runner/test-gates, no broad unguarded SQL tests. Historical WEB4 evidence cannot be reused as current PASS. Documentation-only activation needs links/encoding/diff checks; it runs zero product tests.
