# WEB5 backend evidence

**2026-11-02: B01 IMPLEMENTED / VERIFIED NONPRODUCTION (bounded); Owner acceptance/merge pending. B02–B06 PLANNED / NOT RUN.** [B01 actual evidence](20261102-b01-crash-commit.md) owns the nonproduction crash/Commit results and limits. Documentation-only activation and the historical review entries below did not themselves execute tests or approve production.

**WEB5 roadmap review amendments (docs-only, 2026-11-02):** six review findings (P1-01/P1-02 and P2-01–04) addressed in the owning plan, protocol, acceptance and Codex handoff. New gates B5-NEG-08, B5-COMP-01/02, B5-MIG-01/02 conditional, B5-CI-04/05, B5-OPS-06/07, B5-IIS-07/08 and B5-REL-04/05 are all **PLANNED / NOT RUN**. No WEB5 runtime/SQL/CI/attended evidence is claimed.

**Round-2 P1-01 docs clarification (2026-11-02):** Strict cutover must preserve protected **read-only** status for pre-cutover WEB4 schema-v1 UNKNOWN/COMMITTED intents without permitting legacy mutations; B5-COMP-03/04 added, B02 exit revised for ADR-selected additive OR strict-with-recovery. Backend implementation/SQL readback testing remains PLANNED / NOT RUN.

**Review vòng 3 — docs-only correction (2026-11-02):** recovery permission follows stored original operation kind (Create/Update Write; Post Post), **not ordinary Read**; Read denied/original Write or Post granted remains an authorized positive result. B02 common B5-COMP-01/02 vs strict-only B5-COMP-03/04 is now explicit; additive records strict-only N/A with ADR rationale, not PASS or test skip. The Markdown acceptance table is continuous. At that docs-only checkpoint all WEB5 gates remained PLANNED / NOT RUN.

| Owner | Current state | Future evidence file |
|---|---|---|
| B01 process-death/Commit | VERIFIED NONPRODUCTION (bounded); Owner pending | [20261102-b01-crash-commit.md](20261102-b01-crash-commit.md) |
| B02 terminal negative | PLANNED | `YYYYMMDD-b02-tombstone-protocol.md` |
| B03 operator tooling/rehearsal | PLANNED | `YYYYMMDD-b03-operator-drill.md` |
| B04 SQL CI | PLANNED | `YYYYMMDD-b04-protected-sql-ci.md` |
| B05 full IIS and SQL/browser | PLANNED | `YYYYMMDD-b05-full-iis-staging.md` |
| B06 release dossier | PLANNED | `YYYYMMDD-b06-release-decision.md` |

[Owning plan](../../plans/20261102-001-web5-operational-safety-backend.md) · [protocol](../../design/WEB5_OPERATION_SAFETY_PROTOCOL.md) · [acceptance](../../development/WEB5_BACKEND_ACCEPTANCE.md) · [Codex handoff](../../development/WEB5_CODEX_HANDOFF.md) · [WEB4 historical evidence](../web4/README.md). Record original source SHA, command, runner, actual TRX counts, SQL oracle, cleanup and limits per candidate; never infer a current PASS from archived WEB4.
