# WEB5 backend evidence

**2026-11-02: B01 DONE (bounded), merged PR #88; B02 DONE / VERIFIED NONPRODUCTION (bounded); B04-A VERIFIED NONPRODUCTION (bounded); B04-B DONE / VERIFIED NONPRODUCTION (bounded); B04 A+B DONE (bounded).** [B04-A evidence](20261102-b04a-protected-sql-ci.md) records actual protected RED +two103-case GREEN SQL runs and owned cleanup. Other task results are separately owned; B01 evidence is integrated through [PR #88](https://github.com/chung6a8m/MyDmsVn/pull/88), merge `b320c10acc0ddb2e7cf4b87c153bdf51f25c4bf0`. [B02 bounded verification](20261102-b02-terminal-resolution.md) and [B04-B hosted candidate proof](20261102-b04b-candidate-sql.md) record current results. Protected hosted run `41432523016` verified 167/167 at exact source `2d72700294227ef69f1a7a61a2969a6bfe2c58be`; final-head CI/merge disposition is recorded separately in PR #91. B03 CLI/rehearsal and B05/F04 full IIS remain NOT RUN; no production approval. The following activation/review notes are historical docs-only records, not current task status.

**WEB5 roadmap review amendments (docs-only, 2026-11-02):** six review findings (P1-01/P1-02 and P2-01–04) addressed in the owning plan, protocol, acceptance and Codex handoff. New gates B5-NEG-08, B5-COMP-01/02, B5-MIG-01/02 conditional, B5-CI-04/05, B5-OPS-06/07, B5-IIS-07/08 and B5-REL-04/05 are all **PLANNED / NOT RUN**. No WEB5 runtime/SQL/CI/attended evidence is claimed.

**Round-2 P1-01 docs clarification (2026-11-02):** Strict cutover must preserve protected **read-only** status for pre-cutover WEB4 schema-v1 UNKNOWN/COMMITTED intents without permitting legacy mutations; B5-COMP-03/04 added, B02 exit revised for ADR-selected additive OR strict-with-recovery. Backend implementation/SQL readback testing remains PLANNED / NOT RUN.

**Review vòng 3 — docs-only correction (2026-11-02):** recovery permission follows stored original operation kind (Create/Update Write; Post Post), **not ordinary Read**; Read denied/original Write or Post granted remains an authorized positive result. B02 common B5-COMP-01/02 vs strict-only B5-COMP-03/04 is now explicit; additive records strict-only N/A with ADR rationale, not PASS or test skip. The Markdown acceptance table is continuous. All WEB5 gates remain PLANNED / NOT RUN.

| Owner | Current state | Evidence |
|---|---|---|
| B01 process-death/Commit | DONE (bounded), merged PR #88 | [Actual proof](20261102-b01-crash-commit.md) |
| B02 terminal negative | DONE / VERIFIED NONPRODUCTION (bounded); exact-candidate local and hosted proof | [B02 candidate](20261102-b02-terminal-resolution.md); [contract](../../contracts/web5-resolution-v1/README.md) |
| B03 operator tooling/rehearsal | PLANNED | `YYYYMMDD-b03-operator-drill.md` |
| B04 SQL CI | A+B DONE / VERIFIED NONPRODUCTION (bounded); B hosted 167/167 PASS | [B04-A baseline](20261102-b04a-protected-sql-ci.md); [B04-B candidate](20261102-b04b-candidate-sql.md) |
| B05 full IIS and SQL/browser | PLANNED | `YYYYMMDD-b05-full-iis-staging.md` |
| B06 release dossier | PLANNED | `YYYYMMDD-b06-release-decision.md` |

[Owning plan](../../plans/20261102-001-web5-operational-safety-backend.md) · [protocol](../../design/WEB5_OPERATION_SAFETY_PROTOCOL.md) · [acceptance](../../development/WEB5_BACKEND_ACCEPTANCE.md) · [Codex handoff](../../development/WEB5_CODEX_HANDOFF.md) · [WEB4 historical evidence](../web4/README.md). Record original source SHA, command, runner, actual TRX counts, SQL oracle, cleanup and limits per candidate; never infer a current PASS from archived WEB4.
