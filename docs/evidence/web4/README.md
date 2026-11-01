# WEB4 backend evidence — prospective, not test results

**Selected:** 2026-11-01. **All backend B01–B06 implementation gates: PLANNED / NOT RUN at documentation activation.** [Owning plan](../../plans/20261101-003-web4-reliability-server.md), [backend acceptance IDs](../../development/WEB4_BACKEND_ACCEPTANCE.md), [Codex handoff](../../development/WEB4_CODEX_HANDOFF.md). This scaffold asserts no CI, SQL, IIS, browser or product runtime PASS.

For each later task produce a new dated evidence file in this directory with exact backend/frontend source and fixture/corpus SHA, selected API/SQL test owner, exact invocation and actual executed PASS/FAIL/SKIP rows, errors/corrections, sanitized result/cleanup, remaining gates and explicit Owner decision. Distinguish host unit vs SQL integration vs published HTTPS browser vs .NET wrapper vs attended manual vs docs check. No test reports with secrets, credentials, token/cookie, HAR/trace or unrestricted DB identifying information.

Existing WEB2/WEB3 evidence remains immutable and candidate-specific. WEB4 source must be verified independently. Frontend F tasks/evidence live in ReactWebUI. CEX WinForms acceptance and full IIS/production remain independent.


## B01 — DONE (bounded contract)

[2026-11-01 B01 evidence](20261101-b01-contract.md) records source/canonical corpus validation and the frontend pin/signoff gate. Runtime/SQL/CI/browser are not implied.

## B02 — Windows/net48 CI

[B02 execution evidence](20261101-b02-windows-ci.md): selected by Owner; DONE (bounded): final executable candidate hosted push/PR PASS, Owner accepted; PR #81 merged 2026-11-01. B01 acceptance and old evidence remain unchanged.

## B03 — schema12 and transaction-owned journal

[B03 execution evidence](20261101-b03-schema-journal.md): VERIFIED CANDIDATE through [draft PR82](https://github.com/chung6a8m/MyDmsVn/pull/82); Owner acceptance/merge pending. Source,253 selected local rows (SQL and SQL-free), Local compatibility and hosted415/415 checks are recorded separately. B01/B02 historical evidence remains unchanged.
