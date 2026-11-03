# WEB6 B01 evidence — source and safety policy

**Date:** 2026-11-03 (Asia/Bangkok). **Checkpoint:** SOURCE/POLICY DOCUMENTED; OWNER TECHNICAL REVIEW REQUIRED. **Runtime:** NOT CHANGED / resend OFF.

## Scope and revisions

- Backend source: `c7782429e105688e916fe2a0d41bb58fc0a1cb4d`.
- Paired frontend source inspected: `a011230fc9d5057ab76321251ad0a292e18aecbc` (frontend PR #32 merged).
- Deliverable: [B01 source and safety policy](../../development/WEB6_B01_SOURCE_SAFETY_POLICY.md).
- Only B01/F01 source, policy, threat/state/test mapping was reviewed. B02–B06/F02–F05 were not implemented or executed.

## Source facts verified

| Check | Result |
|---|---|
| WEB4 corpus | 48 tracked files; tree `fcab4a1e7505b150a132ce2899002930e9f2a6ee` |
| WEB5 corpus | 41 tracked files including manifest; manifest SHA-256 `fef5f1f18d3a556bf35d5c9ed6bc38af3eeb0c1a89dd5f9d125d7fb190a133b2` |
| Schema13 | source constant and accepted evidence agree on `D0A86EA0CACAB3D68F1188A1166227DFA5DE452572D1719CD55A3FA3C16F02C9` |
| Journal | actor/key/epoch fence, current session/grant, same-transaction COMMITTED result, seven-day result retention |
| Status/resolution | 404/`NOT_OBSERVED` returns no rollback proof; v2 context explicitly reports `sameKeyResendEnabled: false` |
| SQL authority | runtime and operator/maintenance paths remain separated; no grant change |

## Verification performed

This is Markdown-only work under `docs/TEST_POLICY.md`. No runtime, SQL, browser, or hosted tests were required or claimed. Verification consisted of:

- current-HEAD source and test inventory using `rg`;
- direct inspection of journal/read/resolution/maintenance/controllers and all six command handlers;
- Git tree/file counts and SHA-256 checks for frozen corpora;
- schema fingerprint source/evidence comparison;
- Markdown diff, local link-target, UTF-8/LF, and repository-status checks recorded on the delivery commit.

Historical WEB5 counts remain historical and were not rerun or relabeled WEB6 PASS.

Commands used for the final documentation gate:

```powershell
git diff --check
git rev-parse HEAD:docs/contracts/web4-v1
(git ls-files docs/contracts/web4-v1 | Measure-Object).Count
(Get-FileHash -Algorithm SHA256 docs/contracts/web5-resolution-v1/manifest.json).Hash
rg -n "AdmitAsync|CompleteAsync|ReadStatusAsync|ReadResolutionAsync|CompactAsync|sameKeyResendEnabled" src tests
```

Executed runtime tests: **0**. Browser/SQL/IIS cases: **NOT RUN**, appropriate for the Markdown-only B01 checkpoint. The final delivery commit also runs the repository-status, link-target, UTF-8/no-BOM/LF and `git diff --check` gates.

## Established conditions

- Manual resend is a separate action from safe GET and from a new intent after durable `NOT_EXECUTED`.
- The original operation ID, method, route, target, body, fingerprint, expected version, actor and epoch remain frozen.
- `UNKNOWN` and `NOT_OBSERVED` do not prove rollback. A same-key resend can be the first business execution.
- Current source cannot provide a trustworthy resend window; existing contracts must remain frozen.
- Two-tab coordination is local-browser IDB work for F02; backend journal/fence is the cross-device one-effect authority.
- No automatic mutation retry, runtime SQL elevation, historical-quarantine cleanup, or WEB5 archive edit occurred.

## Owner decisions / blockers

Owner review is required for the proposed additive `web6-resend-eligibility-v1`, the authoritative age source, the initial seven-day eligibility ceiling, and the one-claim policy. The recommendation is a server-issued receipt bound to actor/epoch/key/fingerprint before original dispatch. Until that choice and B04 proof, `NOT_OBSERVED` remains `INDETERMINATE`, resend stays OFF, and B01 is not an implementation approval.

**Acceptance state:** B6-SRC-01/B6-SRC-02 evidence is prepared for review, not Owner-accepted. P0/P1 review status is not claimed before PR review completes.
