# WEB5 attended IIS Express fixture — working procedure

**Work in progress, test-only.** This selector supplies a temporary nonproduction host for an attended operator; it does not itself establish IME, accessibility or production acceptance. Full IIS is NOT RUN. Review [current B05 evidence](../evidence/web5/20261103-b05-iis-express-wip.md) and the paired frontend task before use. Coordinate with the backend supervisor: port 44321 and the SQL owner are exclusive. Do not run an automated pilot concurrently.

## Preparation

Use the same explicit nonproduction master, a trusted existing localhost:44321 HTTPS binding, a newly published host directory with the reviewed production frontend assets, and the locked net48 test build. See the historical [WEB4 fixture procedure](WEB4_RECOVERY_PILOT_RUNBOOK.md#preparation-and-execution) for publish prerequisites; WEB5 uses `MigrateWeb5`, exact13 and `web5` mode, not that procedure's WEB4 selector. Preserve the original WEB4 record.

Set `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING`, `MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY`, `MYDMSVN_WEB_TEST_TLS_THUMBPRINT`, `MYDMSVN_WEB_ATTENDED_ENABLE=1`, and `MYDMSVN_WEB_ATTENDED_DIRECTORY` for the process. The last value is an **existing absolute private parent directory**, owned by the current Windows user, ACL inheritance disabled, with no allowed principals except that user; it and its ancestors must not be reparse points. Prepare it outside served assets and source control. Do not print or commit the connection value. The fixture refuses a missing opt-in or an unsafe directory.

Run the single selector in a supervised terminal:

```powershell
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter FullyQualifiedName~WEB5_attended_local_fixture --logger 'trx;LogFileName=web5-attended.trx' --results-directory TestResults/web5-attended
```

The selector starts a temporary restricted SQL-backed IIS Express host and creates an unpredictable `lease-*` child in the private parent. Open its `operator.txt` **locally** for the ephemeral ordinary test actor and URL; never paste the password, token or SQL settings into chat, logs, screenshots or evidence. The lease lasts at most 30 minutes. Record the exact candidate, named operator, Windows/Edge and input/Narrator configuration. The operator performs the applicable steps in the [frontend acceptance matrix](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/development/WEB5_WEB_ACCEPTANCE.md), recording actual outcomes separately. The fixture does not pre-create a genuine UNKNOWN recovery state; do not claim that scenario was observed without an explicit safe setup.

When finished, create an empty file named `done` inside that same private `lease-*` directory. The fixture deletes credentials, exits IIS Express, drains the owned login and disposes its SQL database, then checks exact resource absence on a separate connection. A timeout or failed check is a failure, not an acceptance. Wait for the process to exit and inspect its TRX; do not delete SQL objects by prefix. Confirm private files and owned resources are absent. The current fixture has not proved worker cleanup if the supervising test process is forcibly killed; retain and investigate any uncertain resources.

Do not record an attended PASS merely because the fixture ran: the human observation, devices, individual scenario outcomes, sanitized evidence and unresolved limitations must be recorded for the exact candidate. This procedure does not approve production, same-key resend or full IIS.
