# WEB6 backend evidence — selected, work in independent review

**Roadmap selected 2026-11-03; overall acceptance remains incomplete and production remains NO-GO.** B01 is merged, while B02a PR #101 and B04 PR #100 are independent pending reviews. Local evidence is not Owner acceptance and is not protected hosted SQL CI.

[Owning plan](../../plans/20261103-001-web6-recovery-quality-backend.md) · [safety design](../../design/WEB6_RECOVERY_OPERATION_PROTOCOL.md) · [acceptance gates](../../development/WEB6_BACKEND_ACCEPTANCE.md) · [Codex handoff](../../development/WEB6_CODEX_HANDOFF.md) · [paired candidate runbook](../../development/WEB6_CANDIDATE_RUNBOOK.md) · [archived WEB5 evidence](../web5/20261103-web5-roadmap-closeout.md).

| Task | Planned evidence | Status |
|---|---|---|
| B01 | [Exact source/contract/risk/expiry eligibility checkpoint](20261103-b01-source-safety-policy.md) and Owner scope | PR #99 MERGED; source-policy checkpoint present; Owner policy decisions still pending; runtime NOT CHANGED |
| B02a/B02b | B02a read-only CLI is carried by independent PR #101; B02b typed terminal command/races remain pending | PR #101 PENDING with local nonproduction evidence there; B02b NOT IMPLEMENTED |
| B03 | Compaction/retention/positive-case/backup-restore/epoch drill, independent SQL oracle | PLANNED |
| B04 | [Six-command same-key/terminal/OS-death SQL proof](20261103-b04-same-key-sql-replay.md) and [quarantine register](20261103-b04-quarantine-register.md) | PR #100 PENDING; Round-2 local proof separates 8 WEB6 exact13 matrices from 5 unchanged WEB4 schema12 matrices; B6-RPL-02 protected exact-candidate SQL CI BLOCKED / NOT RUN; B6-RPL-05 PARTIAL / OWNER DECISION PENDING; resend OFF |
| B05a/B05b | Actual audit/limiter, owned IIS Express paired candidate, old/new cutover, load and service recovery | PLANNED |
| B06 | Exact frontend/backend paired release manifest, target-vs-actual and named Owner disposition | PLANNED |

**Inherited evidence is not a WEB6 result.** WEB5 B01 process-crash, B02 terminal resolution, B04 protected SQL CI, PR #95 host admission, frontend F03 browser and bounded F04 IIS Express are historical source-pinned successes; the new feature and paired candidate must be re-evaluated after changes. Current no-go: STAGING NOT ACCEPTED / PRODUCTION NO-GO / DEFERRED; resend disabled; full IIS/unperformed Narrator NOT RUN exceptions for bounded nonproduction, reverse-proxy topology N/A on direct host; two historical SQL allocations quarantined and outside any cleanup authority. CEX independent.
