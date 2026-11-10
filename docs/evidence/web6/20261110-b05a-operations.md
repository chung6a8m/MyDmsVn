# WEB6 B05a — operations hardening evidence

**Disposition (2026-11-10): IMPLEMENTED / LOCALLY VERIFIED NONPRODUCTION; live host evidence BLOCKED / NOT RUN.** This record covers the audit, limiter and host-safety implementation at backend source `9598c75d60370bc73efaa14525deb42af3f0e8d2`, tree `889cc4c5acf70713f3f05fae1fa950016d3cffc3`. It does not accept staging, enable same-key resend or approve production.

## Implemented boundary

- The runtime audit path is fail-closed at admission, writes through a protected selected destination and exposes sticky degraded health after delivery failure. It does not log bearer/refresh credentials, SQL connection settings or business payloads.
- Candidate admission seals source/binary/config/launch and filesystem identity before requests. Host identity, queue/controller/worker ownership and cleanup absence remain mandatory before a live result can be accepted.
- The existing limiter remains a **single verified worker** boundary. The implementation characterizes concurrency and sink outage, but makes no multiworker or shared atomic limiter claim.
- Ordinary runtime mode and manual same-key resend remain OFF. No automatic mutation retry was introduced.

## Serial verification on the Task 7 checkout

Each restore and test command completed before the next began on Windows NT `10.0.26300.0` with .NET SDK `8.0.427` targeting Release/net48. The enabled live selectors were also run one at a time; none allocated SQL, IIS Express or browser resources.

| Selection | Result | Evidence |
|---|---:|---|
| Application Release/net48 complete | 363 passed, 0 failed, 0 skipped | locked restore PASS; `TestResults/task-7-application/task-7-application.trx` |
| HttpHost Release/net48 complete | 635 passed, 0 failed, 0 skipped | locked restore PASS; `TestResults/task-7-httphost/task-7-httphost.trx` |
| Security pure affected selection | 253 passed, 0 failed, 1 skipped | locked restore PASS; the one skip is the intentionally disabled live recovery row; `TestResults/task-7-security-pure/task-7-security-pure.trx` |

These are local implementation gates, not published-IIS/SQL acceptance. The complete Security project is not reported green: an earlier Task 2 run retained five unchanged WEB5 CRLF fixture failures reproduced on the clean baseline (630 passed, 5 failed, 218 skipped at that run).

## Failure and retained-resource register

- A Task 3 SQL-enabled full run reached an existing authentication SQL test that expected HTTP 200 and received 500, then hung for more than ten minutes. Task-owned processes were interrupted. Result: **ABORTED / NOT PASS**.
- Task 6 round 2 accidentally overlapped two full Security suites. Their destructive SQL/HTTP.sys fixtures interfered through sessions/locks, poison-fence state and HTTP.sys snapshots; both were stopped. Result: **NOT PASS**. Later focused success does not erase these runs.
- Task 6 round 4 first exposed a real-SQL permission-preflight defect: 26 passed / 2 failed. Cleanup correctly refused destructive commands and two fresh task-owned allocations/evidence roots remain retained untouched. Corrected fresh selections passed, but the retained allocations were not adopted or removed.
- Historical B03 quarantines, retained probes and the two earlier WEB5 SQL allocations remain outside this task's cleanup authority and were untouched.

## Acceptance mapping

`B6-HOST-02` and `B6-HOST-03` have implementation and local pure-test evidence only. `B6-HOST-01` still lacks a published owned IIS Express/SQL run, so all three live host gates remain **BLOCKED / NOT RUN**. Full IIS and Narrator are NOT RUN. Reverse-proxy topology is NOT_APPLICABLE only to the selected direct same-origin host; forged forwarded-header rejection remains applicable. Multiworker operation is NOT ACCEPTED.

Staging is **NOT ACCEPTED** and production remains **NO-GO**.
