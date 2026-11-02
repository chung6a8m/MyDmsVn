# Codex handoff — WEB5 backend

**Selected 2026-11-02; B01 DONE (bounded), merged PR #88; B02 DONE / VERIFIED NONPRODUCTION (bounded); B04-A VERIFIED NONPRODUCTION (bounded); B04-B DONE / VERIFIED NONPRODUCTION (bounded); B04 A+B DONE (bounded).** [B04-A evidence](../evidence/web5/20261102-b04a-protected-sql-ci.md) · [SQL runbook](WEB5_SQL_CI_BASELINE.md). Other milestones require their own candidate evidence. Read [AGENTS](../../AGENTS.md), [AI_CONTEXT](../../AI_CONTEXT.md), [plan selector](../plans/README.md), [owning plan](../plans/20261102-001-web5-operational-safety-backend.md), [safety design](../design/WEB5_OPERATION_SAFETY_PROTOCOL.md), [acceptance](WEB5_BACKEND_ACCEPTANCE.md), [TEST_POLICY](../TEST_POLICY.md), [WEB4 archived reference](WEB4_OPERATOR_HANDOFF.md), actual tests/lockfiles and latest `main`.

**WEB5-01 agreement:** consume [ADR0015](../adr/0015-web5-additive-recovery-boundary.md). B01 PR #88 and B04-A PR #89 are merged. Owner subsequently selected B02/B04-B. The additive [resolution contract](../contracts/web5-resolution-v1/README.md),013 and operator inspection/recording are implemented. [B02 evidence](../evidence/web5/20261102-b02-terminal-resolution.md) and [B04-B hosted proof](../evidence/web5/20261102-b04b-candidate-sql.md) are current; [hosted candidate verification](WEB5_SQL_CANDIDATE.md) passed 167/167 in run `41432523016` on exact source `2d72700294227ef69f1a7a61a2969a6bfe2c58be`. Final-head CI and merge disposition remain in PR #91; paired frontend final-candidate acceptance remains separate. No same-key resend UI is authorized.

## Task order and independent PRs

| Branch suggestion | Deliver only | Dependencies |
|---|---|---|
| `codex/web5-b01-crash-proof` | Test-only process supervisor, failpoints, real SQL oracle for six commands | In parallel with B04-A; no product resend |
| `codex/web5-b04a-sql-ci-foundation` | Isolated protected SQL lane, existing owners, intentional RED and owned cleanup | VERIFIED bounded: RED +2x103SQL PASS; see evidence; B01 proof remains independent |
| `codex/web5-b02-terminal-resolution` | Implemented additive exact13, separate operator inspection/recording, versioned resolution and original Write/Post authorization | DONE / VERIFIED NONPRODUCTION bounded; local and exact-candidate hosted proof PASS; final-head CI/merge disposition in PR #91 |
| B04-B stacked with the B02 candidate | New terminal/principal/inspection/crash/migration owners and protected exact-candidate CI | Local 167/167 and hosted 167/167 PASS with clean owned teardown; B04 A+B DONE bounded |
| `codex/web5-b03-operator-tools` | Operator CLI, positive COMMITTED/result unavailable ticket reconciliation, audited retention/rotation/backup rehearsal | B02 and B04-B accepted |
| `codex/web5-b05-full-iis` | Real IIS, old/new cached-tab matrix, limiter/audit/proxy, p95/p99+RPO/RTO baseline, Web F04 pilot | B01–B04 (including B04-A/B) PASS |
| `codex/web5-b06-release-handoff` | Positive/negative case closure and numeric threshold-vs-measured Owner decision dossier | Paired B05/F04 |

**B04-A and B04-B are two checkpoints of the single owning task B04, not a third standalone `codex/web5-b04-sql-ci` execution PR.** B04-B must run new B02 tests on the exact combined trusted candidate before accepting the B02 PR.

A task begins in an isolated worktree from verified current `main`, never the docs PR's stale SHA. B01 and B04-A may be implemented in parallel; B02 executable changes require B01+B04-A. The protected B04-B SQL lane must PASS at B02 HEAD before B02 acceptance; B04 as a whole is only DONE when A+B pass. Pin companion ReactWebUI source and `web4-v1` 48-blob corpus. Preserve exact12 web4, legacy-web11/exact11, Desktop exact10, SQL business handlers and independent CEX. Never add a browser SQL admin endpoint, manual journal DELETE, automatic retry or status404-derived unlock. A changed wire/schema requires a newly versioned additive contract and compatibility/permission tests.

## Copy-ready Codex task request

~~~text
Implement only Roadmap 05 backend Bxx, using docs/plans/20261102-001-web5-operational-safety-backend.md,
docs/design/WEB5_OPERATION_SAFETY_PROTOCOL.md, docs/development/WEB5_BACKEND_ACCEPTANCE.md,
docs/TEST_POLICY.md and actual current main source/tests.
Start isolated branch; map existing source/SQL lock-order/fixtures and publish exact change plan.
Create meaningful RED behavior/crash evidence before minimal changes when possible.
Prepare protected SQL CI B04-A before B02 changes; run B04-B on exact B02 candidate before acceptance.
ADR0015 selects additive WEB4-safe old clients plus explicit exact12->13/web5 admission.
Consume the published web5-resolution-v1 contract; do not repeat completed B02 implementation.
For existing schema-v1 DISPATCHED/UNKNOWN preserve actor/epoch/key/fingerprint and original
COMMITTED outcomes. Additive preserves frozen v1 keyed writes/status; strict mutation denial
is conditional on a future reviewed strict policy, not the selected implementation.
B04-B tests common B5-COMP-01/02 under either ADR; strict-only B5-COMP-03/04 test v1 positive with Read denied but original Write/Post granted, NOT_OBSERVED, expiry, revoked original Write/Post, CANCELLED, stale epoch and Sales Post version/Receipt Post no version. In additive, strict-only B5-COMP-03/04 are NOT_APPLICABLE with evidence, not PASS or skipped mandatory tests.
Prove all six commands x two lock orders.
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

**Task PR requirements:** chosen Bxx, dependent Fxx/source pins, changed files and invariants, actual tests run, finite clean-up, sanitized logs, independent review, exact CI head, blocked/unsafe cases, follow-up sequencing. For strict B02 include independent pre-cutover WEB4 readback proof (B5-COMP-03/04) and explain permitted recovery-only route versus denied old mutation. For additive B02 report strict-only B5-COMP-03/04 as `NOT_APPLICABLE`, while common B5-COMP-01/02 and current Write/Post-auth positive/negative proof remain mandatory. Apply existing repository Markdown/C# encoding policies. No fake SQL master, no global old test runner/test-gates, no broad unguarded SQL tests. Historical WEB4 evidence cannot be reused as current PASS. Documentation-only activation needs links/encoding/diff checks; it runs zero product tests.
