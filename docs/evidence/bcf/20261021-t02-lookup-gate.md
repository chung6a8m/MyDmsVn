# BCF-T02 — native lookup gate execution

**Status: COMPLETE / lookup gate PASS on the frozen candidate.** All four automated contexts pass (688/688), with independent native100/125 Light/Dark observations for all three controls. The human reports all three stable in all four contexts; both owned attended package sessions pass and clean up. BCF-A01–A13 are reconciled for this input scope. T03 is now eligible to start, but visual implementation, full BCF Owner acceptance and release acceptance are not granted here.

[Plan](../../plans/20261021-001-business-compact-lookup-and-visual-parity.md) · [Acceptance ledger](../../BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) · [T01 repair evidence](20261021-t01-lookup-binding.md)

## Candidate and execution boundary

- T02 starts after documentation checkpoint `94196ce`; product repair is `0089ef5` plus `58c8aa2`. Runtime and vendor trees are unchanged by T02.
- Test/observer and attended-package preparation: `67fff29866024a995da3ff83218bed69033dfcc1`.
- The package was built by the existing `deployment/release/Publish-LocalRelease.ps1`, version `0.0.0-bcf-t02`, into ignored `artifacts/release/bcf-t02-candidate`. Its manifest records source `67fff29866024a995da3ff83218bed69033dfcc1`, `sourceTreeDirty=false`, Desktop/admin net48 win-x86 packages and schema support 001–009. This local candidate is not a release approval.
- Pins remain BootstrapSourceGrid `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f`, Bootstrap5WinFormUI `aba102e33c48937fd92468791c287afda0a59e77`, SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`.
- Environment: interactive Windows NT 10.0.26200.0, SDK 8.0.425, VSTest 17.11.1; net48/x86 target. GUI commands are serialized. Only existing GUI-owned process and SQL fixture mechanisms are used.

| Frozen artifact | SHA-256 |
|---|---|
| Package release-manifest.json | `2738CDC2A8511EE544D5E28CB4C71C007BB9641D75E426F767B9737089B819B6` |
| Package MyDmsVn.Desktop.App.exe | `DC670BD55B28A6D6AAF72D227C51299F745F6D95CCD331DF30AA7147977E9B68` |
| Package MyDmsVn.Desktop.WinForms.dll | `557844EFDBA275954F2370ACF40E1B2CE6CA1A3A46E4EBFAA81D05FD39138E02` |
| Test DLL built at 67fff29 for the attended launch | `091B2F599773F1C69F6DDFCD8EF79797298BBA90E6740D1077A4E97B736888DA` |
| Test-host MyDmsVn.Desktop.WinForms.dll (separate default-version build) | `2625D737F388BDDAA0645D00AE1047837FFEFCA509256BF351FB03AD06C54411` |

Product package and test build are distinct artifacts; package bytes are validated against its frozen manifest. Later documentation does not alter those artifacts. Raw TRX, resource identities, runtime configuration, barriers and binaries remain local/ignored.

## Native environment and theme admission

`LookupGateEnvironment` is a class fixture for the existing typing/binding classes. With no declaration it preserves ordinary hosted regression use. With `MYDMSVN_BCF_EXPECTED_SCALE=100|125` and `MYDMSVN_BCF_THEME=Light|Dark`, it requires both valid declarations and checks the actual theme in every lookup case. The receipt fixture sets the requested theme before creating controls and restores the preceding theme after owned work drains and controls are disposed.

The existing `GuiInlineEditorProbe` has a lookup-only measurement mode. A separate net48/x86 process establishes PerMonitorV2 awareness, verifies that the target HWND belongs to the declared testhost PID, measures its actual monitor effective DPI, native client and physical window bounds, reports window DPI separately, and rejects a scale mismatch. The existing `GuiOwnedProcess` verifies tree/pipe cleanup. This is observation of the existing unaware/virtualized product-style host on an actual monitor; it does not pretend its `DeviceDpi=96` is the monitor's scale or apply simulated scaling.

The first phase measured **96 DPI / native100**. A premature development declaration of native125 was correctly rejected. After the human changed Windows Display Scale, the second phase independently measured **120 DPI / native125**, while the target's virtualized window DPI remains 96. The old OS profile AppliedDPI value is not authoritative. These tests do not change Windows Display Scale or synthesize scaling.

## Development trials (retained separately)

| Local TestResults path | Actual observation | Credit |
|---|---|---|
| bcf-t02-context-red/context.trx | Three intended failures: Dark declared, actual Light; clean guard/drain | Demonstrates missing theme admission |
| bcf-t02-context-green/context.trx | Theme now Dark, three failures because declared125 differs from measured100; observer cleanup verified | Correct environment rejection; not product RED or native125 evidence |
| bcf-t02-context-native100/context.trx | Three same-identity rows PASS in measured native100/Dark | Focused environment preflight |
| bcf-t02-native100-dark/matrix.trx | 172/172 PASS; 107 lookup rows each record Dark, measured native100 and verified observer cleanup | Development matrix; compiled before the final attended-probe two-theme observation condition; final frozen matrix remains to be reconciled |

The selected scope is the existing 15 typing rows, 92 binding rows and 65 affected control/inline/accessibility/Session-expiry anchors. Exact future discovery/execution is reconciled at each final context; these development totals are not substitute gate evidence. No full SQL suite is required for lookup binding.

## Frozen automated matrix

All final matrix invocations reuse the verified test output at `67fff29` with `--no-build --no-restore`, `-p:PlatformTarget=x86` and `-- RunConfiguration.TargetPlatform=x86`. Discovery selects 172 named rows; sorted discovery/TRX name comparison is empty for each completed context. Each context includes 107 lookup rows (28 filter, 40 Warehouse header, 39 Employee header), with 107 independent scale/theme records and 107 clean guard/drain records. The remaining 65 are affected regression anchors, some with their own theme-specific cases.

| Context | Executed / passed | Lookup observer / clean guard records | Start → finish (UTC+07) | Local TRX SHA-256 |
|---|---|---|---|---|
| native100 Light | 172 / 172; zero skip/fail/abort/timeout | 107 / 107 | 12:24:54.0832054 → 12:26:02.9144734 | `41D6D29C7C323F743E59EA109AD56E6ADA57F1E5FC29EE42F0454B553DC47E95` |
| native100 Dark | 172 / 172; zero skip/fail/abort/timeout | 107 / 107 | 12:26:03.5990115 → 12:27:14.7515859 | `4909A35C2A4E1BAC57B9EBFFFA6E3FA9AEC7345AD45E5D6D112718A1EEBA0A8B` |
| native125 Light | 172 / 172; zero skip/fail/abort/timeout | 107 / 107 | 12:28:21.2644816 → 12:29:48.4767564 | `CA124880D59A38B5FC3DE882FA83198708B110178FD07EDD8CC15345B38A5772` |
| native125 Dark | 172 / 172; zero skip/fail/abort/timeout | 107 / 107 | 12:29:49.1826748 → 12:31:16.6097681 | `092EDBCCAD61050E31C646D89E4F7C083E28A5CE0C73232899C0F8983FBB4043` |

Local results use `TestResults/bcf-t02-native{scale}-{theme}-frozen/matrix.trx`; discovery stdout is local TEMP `bcf-t02-frozen-discovery.log`. The actual focused selector is:

```powershell
$bcfFilter = 'FullyQualifiedName~GoodsReceiptLookupTypingTests|FullyQualifiedName~GoodsReceiptLookupBindingTests|FullyQualifiedName~GoodsReceiptControlTests|FullyQualifiedName~GoodsReceiptInlineEditorTests|FullyQualifiedName~InventoryAccessibilityAcceptanceTests|FullyQualifiedName~LoginSessionAcceptanceTests.Session_expiry_clears_private_workspace'
$env:MYDMSVN_BCF_EXPECTED_SCALE = '100' # Declare the actual independently measured context.
$env:MYDMSVN_BCF_THEME = 'Light' # Repeat separately in Dark.
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 `
  --no-build --no-restore -p:PlatformTarget=x86 --filter $bcfFilter `
  --logger 'trx;LogFileName=matrix.trx' --results-directory TestResults/bcf-t02-native100-light-frozen `
  -- RunConfiguration.TargetPlatform=x86
```

Native100 observed host geometry includes physical window `202,71,1516,889`, native client `1500×850`. Native125 includes physical window `13,0,1895,1105`, native client `1875×1056`, logical client `1500×845`, virtualized work area `1536×816`; the host window can extend past that work area. These are actual measurements, not manually scaled screenshots or visual-fit acceptance. Lookup text/caret/focus and ownership are asserted independently; later visual/viewport obligations remain T03–T06 work.

## Attended package session

The user explicitly agreed to participate in manual typing and Display Scale changes. The opt-in `Bcf_attended_lookup_package_session_keeps_human_input_separate_from_automation` case uses the existing designated non-production `GuiSqlEnvironment`: a fresh owned database, distinct admin/runtime identities, seeded catalog data, real packaged startup/Login and known committed header selections. Whole-string setup is labeled setup, not typing proof.

After setup, the bounded existing package probe sends no query keys. The human tests filter/Warehouse/Employee with separate `g`, `u`, `i`, slow/fast typing, middle/range correction, Escape and explicit keyboard selection, first in Light and then Dark. On controller-requested runtime coordination, the probe reads synthetic lookup raw text, `EM_GETSEL`, focus and theme metadata. It must observe both theme states before ending. Coordination markers close a session; they are **not approval files or proof that typing passed**. Human reports are recorded independently.

The owned process budget is 22 minutes (20-minute human predicate budget within it). Positive process-tree/output drain precedes independent SQL assertions that GoodsReceipts, GoodsReceiptLines, StockLedger and StockBalances remain empty, followed by identity-verified database/principal teardown. No Save/Post is requested. Test sessions do not expose generated credentials to the human or logs. Cleanup uncertainty blocks further GUI work.

| Native context | Light human report | Dark human report | Session/SQL teardown |
|---|---|---|---|
| 100 | User: “Cả ba ô ổn ở 100% Light” | User: “Cả ba ô ổn ở 100% Dark” | 1/1 attended test PASS; process/output drained; independent no-inventory assertions PASS; database and all principals dropped after verified drain |
| 125 | User: “Cả ba ô ổn ở 125% Light” | User: “Cả ba ô ổn ở 125% Dark” | 1/1 attended test PASS; process/output drained; independent no-inventory assertions PASS; database and all principals dropped after verified drain |

At native100 the separate human reports cover all three controls under the instructed per-key/slow/fast/middle/range/Escape/selection smoke. Both theme snapshots were acknowledged. Light's final Employee snapshot reads `gui`, range `3+0`, focus false. Dark's final snapshot reads committed `GUI-EMP - UIA Employee Updated`, range `0+0`, focus true; it does **not** prove pending `gui` at that final instant. The snapshots therefore supplement, rather than replace, the human reports and deterministic per-key assertions. The test's PASS establishes session/SQL safety and observed theme states, not an automated verdict on every human keystroke.

Native100 attended TRX: `TestResults/bcf-t02-attended100/attended.trx`, start `12:19:07.3556031`, finish `12:24:24.5795912` (UTC+07), SHA-256 `583C4558ACE565E0D0261D3B8038806AAB25C32D4125A640FF86CCB9545FDECC`. One owned allocation/resource record reached `DatabaseAndAllPrincipalsDroppedAfterVerifiedDrain`; raw allocation/identity data remains ignored/local.

Native125 attended TRX: `TestResults/bcf-t02-attended125/attended.trx`, start `12:31:33.9415305`, finish `12:34:55.2372195` (UTC+07), SHA-256 `31E0116D9E9ECAC803631A75C83469FE9DC9CA8F0E2CB46D272DC4BD9F754492`. Both snapshots record their requested themes. Employee's final values in both snapshots are the committed label, range `0+30`, focus false, rather than pending `gui`; human reports provide the attended typing observation, and the separate matrix supplies exact native per-key assertions. Across the two attended sessions, **two** positively owned resource records reached `DatabaseAndAllPrincipalsDroppedAfterVerifiedDrain`; both coordination directories were removed by exact-file cleanup. No unresolved application/probe process or SQL resource remains.

## BCF-A01–A13 reconciliation

| Obligations | Candidate evidence | Disposition |
|---|---|---|
| A01–A03 native first key/order/caret/range | Original 15 T00 typing rows pass in each of four contexts; original failing baseline retained separately | PASS |
| A04–A05 pending/committed isolation and late cancellation | Binding/model/list-request assertions, shared source and ignored-cancellation rows in each context | PASS |
| A06–A08 popup/selection/cancel/navigation/history | Native keys, paste, owned popup/outside messages, deactivation and historical active-only filter/header cases; human keyboard selection in every context | PASS within recorded message-input limits |
| A09–A11 navigation/locks/document/session boundaries | Rejected/admitted dirty/query boundaries, stale callbacks, Posted/busy/denied/recovery and session/disposal rows; exact Save/Post and filter admission assertions | PASS |
| A12 accessibility/inline Product | 15 inline and 14 accessibility anchors plus affected receipt/Session-expiry anchors in each context | PASS |
| A13 hard gate | Four reconciled 172-row matrices, four separate all-three human reports, actual package/Login on both native scales, clean guard/observer/process/SQL teardown | PASS |

Owned mouse messages establish native control behavior, not physical cursor hit-testing across the desktop. The human smoke uses real keyboard input and result selection; no claim is made that every automated mouse case was separately attended. Final snapshots taken after returning to chat may show focus loss/committed labels; they are not retroactively labeled per-key recordings. These limits do not waive any matrix row, and no historical GUI-P5 exception is imported.

## Review and remaining gate

One independent read-only review of `94196ce..67fff29` found no Critical/Important issues. One Minor is deferred: the observer has a 15-second subprocess budget while fixture operation pumping has a 5-second boundary plus a 5-second cleanup boundary. Slow UIA can therefore cause a false failure or poison admission before the observer supervisor finishes; it cannot cause a false PASS. A timeout remains a failed/blocked case, never waived. No separate reviewer rerun is planned without a new material finding.

Rulings: use independently measured owned-monitor DPI rather than registry/in-process inference; keep automation and human reports separate; use the plan's focused affected selection rather than a full project/SQL run; retain the isolated branch/worktree without integration. Cost of the focused scope: unrelated integration behavior remains outside T02. This closes only the lookup phase gate; visual/full BCF acceptance and operational release prerequisites remain unaccepted until their own required evidence exists. T03–T06 remain unimplemented at this checkpoint.
