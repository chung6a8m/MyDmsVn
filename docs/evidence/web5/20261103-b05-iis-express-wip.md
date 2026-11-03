# WEB5 B05 — IIS Express work in progress (2026-11-03)

**Status: IN PROGRESS / NOT ACCEPTED.** The Owner selected B05 as the F04 prerequisite and, on 2026-11-03, accepted an exception for full IIS in this work. The actual host used below is IIS Express, not W3SVC/WAS staging. Full IIS remains **NOT RUN**; this exception is not production approval. B06/F05, same-key resend, production and the independently active CEX roadmap remain separate.

## Candidate and observations

- Backend base `68cf49e5c16811f2777d271808488d70ba7b66cb`, with uncommitted B05 fixture changes in progress. A final candidate/tree and paired frontend SHA and asset hashes are not yet frozen. Do not transfer these observations to a later tree without rerunning the affected gates.
- Two opt-in `Published_React_WEB5_compatible_recovery_preserves_journal_and_physical_stock` wrapper invocations completed against IIS Express HTTPS and a fresh owned SQL fixture: Chromium **1/1 wrapper PASS** (`web5-negative-chromium-rerun.trx`) and Edge **1/1 wrapper PASS** (`web5-negative-edge.trx`), 0 failed/skipped. These are .NET wrappers, not browser test counts. Their original TRX does not retain a machine-readable browser count; the later numeric reporter and post-cleanup independent SQL query were added after these runs and have not yet been established by those results.
- The fixture exercises the six committed commands, protected v2 committed readback and one Sales.Create durable negative with deliberately late original. The negative uses test-owner maintenance; the IIS runtime receives no operator authority. The physical oracle compares source, journal, stock, ledger and valuation and requires the late request not to alter the captured business state. It does not establish all six negative cases or full IIS controls.
- The first Chromium negative run failed the physical oracle because its orphan query included a valid CANCELLED tombstone with null result. The query was narrowed to COMMITTED and terminal disposition remains separately checked; the Chromium rerun and Edge wrapper above passed. The failed run is retained as `web5-negative-chromium.trx` in local TestResults and is not a PASS.
- The SQL-free `WebRecoveryPilotResultTests` suite executed **35/35 PASS**, zero failures/skips, after the attended opt-in and lease hardening. The attended selector without explicit opt-in executed **0**, skipped **1**. These do not prove an attended session or live SQL cleanup.

## Outstanding and limits

A fresh run is needed for the current source with machine-readable browser counts, independent post-cleanup query, exact artifact hashes and reviewed candidate. Prior runs used owned cleanup but did not contain the later independent check. Attended Edge/IME/Narrator is in progress and has no acceptance result here. Compatibility old/new cached assets, representative latency and load, operational audit/limiter/proxy, alert handling, backup/restore and measured RPO/RTO remain NOT RUN. Owner-defined thresholds and paired release disposition are outstanding. Do not claim full IIS, staging or production PASS from this record.

The test-only attended procedure is [here](../../development/WEB5_IIS_EXPRESS_ATTENDED.md). No credentials, raw browser traces or SQL connection information belong in evidence.
