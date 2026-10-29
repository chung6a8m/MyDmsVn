# WinFormsKit input/popup consumer acceptance

**Date:** 2026-10-29. The Owner selected this consumer follow-up after approving the toolkit's canonical visuals on [WinFormsKit PR #3](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/pull/3). This work belongs to MyDmsVn and does not reopen Business Compact exceptions or historical SQL/release gates.

## Baseline and candidate

- App base: `main@7e4dc2ae8ca2d5087d14206ad5fbb1bdb085fea5`; old WinFormsKit gitlink: `a5dd3cec6daba71943549afac628e13727c00da5`.
- Exact toolkit candidate to pin: `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf`. Its production source execution revision is `c060415141cfa3bf1867593326741edad2de0ae9`; later commits only change the capture helper and documentation. SourceGrid stays at `f4e457b43582bf01892f50bdc74aa480531e5944`.
- The app targets `net48`; toolkit retains `net48;net8.0-windows`. Keep source `ProjectReference`, assembly/public identities and package configuration.

## Tasks

1. Pin the exact toolkit gitlink; verify recursive checkout, project graph and a candidate net48 build.
2. Run focused UI/native tests for `SecurityPermissionCatalogView`, one/two-row filters, focus/copy/selection/theme, Sales and GoodsReceipt Lookup/popup/inline editors, and relevant product-host parity. Add a consumer regression where the current tests lack a behavior oracle.
3. Inspect the interactive host at physical 100%/125%, Light/Dark, with Security catalog and an app Lookup popup. Record actual scale separately from `DeviceDpi`; distinguish screenshots, native observations and automated tests.
4. Record a candidate-specific report with discovered/executed/pass/fail/skip, commands, artifacts and limitations. Prepare a narrow consumer PR without business, SQL or release graph changes.

## Acceptance boundary

This consumer scope needs an exact pin/build, nonempty focused GUI tests, physical 100%/125% host observations and an honest report. The Owner's toolkit visual approval does not by itself approve the product host. If a product visual decision is needed, present its exact candidate and evidence to the Owner. SQL, actual Login, package/release, physical 150%/200% and the toolkit pairwise matrix remain separate scopes.

**Outcome, 2026-10-29:** The [candidate evidence](../evidence/ui-labs/20261029-input-popup-consumer-acceptance.md) records code/test/pin `95916e3`, app evidence candidate `003db0a`, toolkit pin `433e5b5`, focused Windows/native and physical 100%/125% results, and the local comparison board. The Owner replied “Consumer acceptance: Tôi approve.” for this presented app candidate. This plan is complete within the scope above; its listed external gates remain separate.
