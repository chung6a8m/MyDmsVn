# WEB6 backend evidence — selected, awaiting execution

**Roadmap selected 2026-11-03; ALL WEB6 implementation and verification tasks PLANNED / NOT RUN.** This is a routing index only; a documentation PR creates no source, SQL, browser, CI or release acceptance.

[Owning plan](../../plans/20261103-001-web6-recovery-quality-backend.md) · [safety design](../../design/WEB6_RECOVERY_OPERATION_PROTOCOL.md) · [acceptance gates](../../development/WEB6_BACKEND_ACCEPTANCE.md) · [Codex handoff](../../development/WEB6_CODEX_HANDOFF.md) · [paired candidate runbook](../../development/WEB6_CANDIDATE_RUNBOOK.md) · [archived WEB5 evidence](../web5/20261103-web5-roadmap-closeout.md).

| Task | Planned evidence | Status |
|---|---|---|
| B01 | [Exact source/contract/risk/expiry eligibility checkpoint](20261103-b01-source-safety-policy.md) and Owner scope | READY FOR OWNER TECHNICAL REVIEW; runtime NOT CHANGED |
| B02a/B02b | [B02a isolated read-only operator CLI](20261103-b02a-read-only-operator-cli.md); B02b typed terminal command/races remain pending | B02a IMPLEMENTED / VERIFIED NONPRODUCTION for B6-OPS-01/02; B02b NOT IMPLEMENTED |
| B03 | Compaction/retention/positive-case/backup-restore/epoch drill, independent SQL oracle | PLANNED |
| B04 | Six commands × same-key/terminal/OS-death, proof of original result and one effect | PLANNED |
| B05a/B05b | Actual audit/limiter, owned IIS Express paired candidate, old/new cutover, load and service recovery | PLANNED |
| B06 | Exact frontend/backend paired release manifest, target-vs-actual and named Owner disposition | PLANNED |

**Inherited evidence is not a WEB6 result.** WEB5 B01 process-crash, B02 terminal resolution, B04 protected SQL CI, PR #95 host admission, frontend F03 browser and bounded F04 IIS Express are historical source-pinned successes; the new feature and paired candidate must be re-evaluated after changes. Current no-go: STAGING NOT ACCEPTED / PRODUCTION NO-GO / DEFERRED; resend disabled; full IIS/unperformed Narrator NOT RUN exceptions for bounded nonproduction, reverse-proxy topology N/A on direct host; two historical SQL allocations quarantined and outside any cleanup authority. CEX independent.
