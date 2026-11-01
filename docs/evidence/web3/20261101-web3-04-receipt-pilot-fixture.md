# WEB3-04 — bounded Receipt pilot fixture preparation

**Owner authorization:** 2026-11-01, "Cho phép mở rộng fixture và chạy nghiệm thu thật", for the frontend WEB3-04 real acceptance dependency. **State:** fixture implemented and compiled; IIS Express/SQL/browser acceptance NOT RUN at this preparation checkpoint. The coordinator must review the concrete diff and provide a stable selected frontend build before exclusive live execution. This does not mark B04 or all B05 complete.

## Candidate and scope

Base `main`: `6e960787a9cd5a656a3a5cab0fe0bf0194a0f24c`, actual merge of PR #76. B02/B03 executable source still matches tested implementation `d2e7e3f3709a7e97cf91c7d4558c560303fa498d`. Existing B02/B03 documentation's premerge wording is historical; this inspection uses actual Git state. Branch: `codex/web3-04-receipt-pilot`.

Native worktree creation was attempted but rejected because this frontend chat's roots exclude the backend repository. The authorized isolated fallback is `C:/Users/pdchung/.codex/worktrees/web3-04-receipt-pilot/MyDmsVn`; backend main was not edited.

Only the existing `WebHostIisExpressTests` fixture changes. A separate Receipt selector shares its SQL allocation, migrations/provisioning, TLS/host, browser process supervision and owned cleanup. Default Sales-only selection remains separate. Receipt actors have explicit ordinary permissions, including Write/Post without Read. The added Product uses a separate stock key so existing Sales costs remain unchanged. No production/business source, schema, provisioner/grant algorithm, dependencies, CEX or historical WEB2 evidence changes.

The [new runbook](../../development/WEB3_RECEIPT_PILOT_RUNBOOK.md) owns mode/manifest/results and execution details. It documents separate receipt-key SQL proof, complete unique Posted set reconciliation and preservation of Sales source/COGS checks. This is not the ordered same-key B04 cross-module scenario.

## Preparation checks actually performed

Windows NT 10.0.26300.0, .NET SDK 8.0.427, Node v22.14.0, net48 Release; locked project restore succeeded. Commands from the isolated backend worktree:

```powershell
dotnet restore tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj --locked-mode
dotnet build tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --list-tests --filter FullyQualifiedName~WebHostIisExpressTests
```

Build succeeded with 0 warnings and 0 errors. Discovery found exactly three packaged-host cases: original System.Web smoke, original Sales browser selector and new `Published_React_Receipt_browser_uses_real_HTTPS_SQL_and_independent_receipt_stock`. Discovery executed **zero tests**. No new runtime PASS is claimed; the fixture's required real-SQL and browser validation remains pending.

Documentation checks: `git diff --check` passed; 20 local Markdown links across the three changed Markdown files resolved. All four changed files follow repository encoding/EOL rules (C# UTF-8 BOM/CRLF; Markdown UTF-8 without BOM/CRLF).

Read-only inspection found an existing `0.0.0.0:44321` TLS binding; no binding/certificate was installed or changed. The designated SQL connection exists in this process (value not printed); publish directory, TLS test thumbprint and browser-directory inputs were initially unset. No IIS, SQL allocation or browser was started. Build/discovery and documentation checks alone do not establish seed/oracle execution.

## Remaining acceptance and retained boundaries

Run the new selector once per Chromium/Edge against fresh positively owned SQL fixtures after coordinator review, using the stable Web production build and both real pilot spec files. Record each candidate, wrapper/browser counts, first failures/corrections, independent SQL reconciliation and verified cleanup in this new evidence record. A skipped, missing or empty browser selection is NOT RUN.

B04 Receipt→Sale→later Receipt valuation, Inventory read UI, all B05/F06 acceptance, attended IME/accessibility, full IIS/production and durable mutation recovery remain outside this bounded fixture checkpoint. Existing WEB2 and B02/B03 results remain their original candidate-specific evidence.
