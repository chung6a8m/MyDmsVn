# Security UI Lab review evidence — 2026-10-25

This record contains observed SEC-T05 evidence for the [Security plan](../../plans/20261025-002-security-administration-ui-lab-first.md). The plan alone owns task state and the Owner decision. Review remains incomplete; this record does not admit Local or SQL integration.

## Candidate and environment

| Field | Observed value |
|---|---|
| Source and test SHA | `b4968072bff74145571a0ca63f7d97a2e3e1c566` on `codex/security-ui-lab` |
| Vendor gitlinks | WinFormsKit `c232a4c09dc0386a5bd8bb484a5c991acae827d5`; SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944` |
| Toolchain | Debug/net48; SDK 8.0.426; VSTest 17.11.1 x64; Windows 11 Home Single Language 10.0.26300, 64-bit OS/test process |
| Isolation | Evaluated ProjectReference closure: Lab 8 projects, Ui.Tests 9, neither with Local/Server/DbMigrator/SQL fixture. Desktop.App closure 12 projects, none from samples/tests. The production Security factory still constructs the legacy control. |

Each Reset restores fixed fake identities: U-ACTIVE=101/rev11 (roles 201+203, direct Deny Sales.Receipts.Post, direct Allow Inventory.Stock.Read, Inherit Sales.Receipts.Read); U-INACTIVE=102/rev12 (member of 201); U-PROTECTED=103/rev13. R-ACTIVE=201/rev21 (members 101+102 and Read/Write grants); R-OTHER=202/rev22; R-INACTIVE=203/rev23 (shared Read plus inactive-only Inventory.Stock.Audit). Catalog digest `security-fixture-v1` has seven items across Sales, Inventory and Legacy, including Deprecated Legacy.Deprecated and unassigned Active Inventory.Stock.Export. These fake IDs do not prove a runtime superuser binding.

## Automation and physical measurement

Locked restore, Debug/net48 Lab build and the whole Ui.Tests project passed. Final command: `dotnet test tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj -c Debug -f net48 --no-restore --logger 'trx;LogFileName=security-t05-review-fixes.trx' --results-directory TestResults/security-t05-review-fixes`. The ignored local TRX reports **179 discovered/executed, 179 passed, zero failed/skipped/not-executed**. This includes fixture aliases, review controls, partial-save status, geometry and poisoned Reset regressions.

Four native-search Theory rows measured physical monitor scale **100%**, Light/Dark at client 900×600 and 1200×760. Monitor effective DPI and window DPI were both reported as 96, separately from physical scale. At 900×600, list/detail bounds were 219×498 / 645×564; at 1200×760, 258×658 / 906×724. Native assertions cover raw text per key, caret/range, focus and committed owner. Partial-save recovery action bounds passed at both client sizes and themes (4/4). A previous source revision passed physical125, but that is not native125 evidence for `b496807`.

Original failures remain in ignored local TRX: a 125%-expected run measured actual 100% (161 pass / four deliberate scale-guard failures); a 171-case run had one old journal substring assertion failure after response rows were added (170 pass); a new partial-status test failed against the generic Conflict banner. Focused repairs and the final 179/179 run supersede those checks without erasing the failures. No raw TRX or SQL identifier is committed.

## Running candidate observations

All committed captures show only synthetic fixture identities and were visually checked. The review-tools panel was closed for normal geometry captures.

| Guide | Direct observation and limit |
|---|---|
| SEC-L01 | On `b496807` at physical100, Light user and Dark role screens showed three top tabs, `Vai trò (2)` and `Thành viên (2)`, no duplicate header chips/counts and no visible horizontal scrolling. [Light user](images/sec-l01-light-native100.png), [Dark role](images/sec-l01-dark-role-native100.png). Earlier live inspection navigated from read-only role members to user memberships; repeat on the final revision. |
| SEC-L02 | Earlier live pass: Users Forbidden while Roles/Catalog stayed Ready; explicit Success restored Users. A held Users read scrubbed old identity; release to successful-empty showed an empty state and effective `Chưa xác định` with reason. [Pre-final diagnostic](images/sec-l02-users-empty-dark-native100.png). Final-revision host test repeats Forbidden→Success→held-empty; full source-by-source attended matrix remains open. |
| SEC-L05 | On `b496807` at physical100, Sales.Receipts.Post explained direct Deny; role Read/Write and direct Inventory Allow were separate. [Dark direct-Deny capture](images/sec-l05-direct-deny-dark-native100.png). Inactive/protected and unavailable-source variants remain for attended review. |
| SEC-L08 | An earlier live three-key Save showed Post success revision 21→22, Read Conflict at revision 22, and no Write call. The original banner hid partial success; [original finding](images/sec-l08-partial-conflict-before-fix-native100.png) is retained. A corrected pre-final pass showed `Đã lưu 1; không lưu 1; chưa gửi 1 — xung đột` with compare actions [capture](images/sec-l08-partial-conflict-fixed-light-native100.png). Final-revision model/control tests cover counts, partial Forbidden visibility, no automatic continuation and geometry. Full uncertain/reload/diff walkthrough remains open. |
| SEC-L03/04/06/07/09/10/11/12 | Automated behavior is mapped in plan section 1A. Complete attended UI actions, response order and native keyboard traversal are not recorded here. Selecting an SEC_Lxx ID alone does not execute that scenario. |

The Lab-only review panel arms one-shot Users/Roles/Catalog Success, Empty, Forbidden, Failure and held reads; mutation Success, Validation, Forbidden, Conflict, held success/failure and uncertain completion; and explicit release. The sanitized journal shows operation, target, key, tri-state Decision, expected/returned revisions, response outcome and order without passwords. A two-Reload→Reset timeout test preserves the visible failure and Release action until the old task drains. The panel has no live knob to change catalog digest or target revision between import Preview and Apply; those stale paths are tested directly through fakes.

## Gate boundary

The exact `b496807` candidate still needs physical125 Light/Dark observations and sanitized captures, the complete attended SEC-L01–SEC-L12 action matrix with gate-release order, and an explicit dated Owner approve/reject statement tied to this revision and declared exceptions. High Contrast is NOT RUN / OWNER-WAIVED for SEC-T04 only. SEC-T06–SEC-T08 remain blocked until the plan records an approved SEC-T05 decision.
