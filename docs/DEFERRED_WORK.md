# Deferred work — outside the active acceptance queue

**Updated:** 2026-10-19. This page contains work that remains outside the approved near-term implementation/acceptance queue. **P5 interactive/full GUI and P6 operational/release requirements are no longer parked here**: they are still OPEN, but now explicitly planned under [GUI-P5](plans/20261019-001-full-winforms-gui-p5-acceptance.md) and [REL/P6](plans/20261019-002-local-v1-p6-release-acceptance.md). Moving requirements into a plan is **not** their acceptance or a release waiver.

Historical original scope remains in the [P5/P6 remainder](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/20261016-004-remaining-p5-acceptance-and-p6-hardening.md) and [P6 operations source](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/archive/20261008-007-phase-6-local-hardening.md). Current plan IDs preserve `REMAIN-P5-GUI-01/02`, `REMAIN-P6-OPS-01/T05–T08` and G1–G5; older TESTGATE/dual-host/P3.1 command instructions are superseded. [Execution index](plans/README.md) decides order.

## 1. P7 — HTTP/remote client (DEFERRED / NOT AUTHORIZED)

- [ ] Select a supported server/runtime matrix and expressly authorize AppCore/.NET 8 reactivation or a separately reviewed modern runtime decision. The currently parked product graph/lock file and an installed .NET 8 SDK do not establish API readiness.
- [ ] Introduce a trusted Server.Api host with endpoints for current catalogs, Goods Receipts, stock inquiries and identity; reuse accepted Server.Application handlers/contracts and current LINQ/native persistence. Do **not** copy the old RepoDb/Dapper composition instructions from historical P7.
- [ ] Define HTTPS/Bearer auth, expiration/revocation, 401/403 and enforced Application permissions; no Desktop SQL credential in the HTTP client.
- [ ] Maintain safe ErrorOr/ApiResponse mapping and OpenAPI for error codes, filters, paging, decimals and UTC contracts.
- [ ] Add an HttpApiClient with bounded cancellation/network handling and explicit remote-session UX. Run Local/HTTP parity against identical DTO/JSON/permission/validation/draft/Post/stock cases before claiming remote compatibility.
- [ ] Provide separate server deployment, versioning, logs/correlation, migration compatibility, operator runbook and full security/acceptance tests.

[Original P7 design](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/20261008-008-phase-7-remote-api.md) contains historical provider/runtime assumptions and is **not** executable authority.

## 2. Modern product target — net8/AppCore (DEFERRED)

- [ ] Follow [REACTIVATE_NET8](REACTIVATE_NET8.md) only after explicit owner authorization. The net48 product and all ten active tests remain the current release scope; parked AppCore/net8.0-windows is NOT SHIPPED and cannot inherit GUI/release PASS from net48.
- [ ] Re-evaluate target runtime lifecycle, build/package assets, lock graphs, new DAL compatibility, DI, tests and signed/installed host support if reactivation is selected.

## 3. Future product modules (DEFERRED)

Outbound sales/invoices, returns/reversal, pricing, valuation/accounting/tax, transfers/reservations, lots/serials, offline sync, multi-tenancy and other non-MVP workflows require separate approved scope. Customers currently remain a master-data catalog, not an invoicing module.

## 4. Cancelled/superseded tooling — NOT deferred prerequisites

The owner **removed** `tests/test-gates.v1.json`, `SqlTestHarness`, standalone SQL runner and runner tests, related wrappers, legacy execution policies and manifests in October 2026. The unimplemented STAB-T03/T04 manifest-v2/fingerprint tooling is **CANCELLED**, not a prerequisite to GUI-P5, REL or P7. [Original stabilization proposal](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/20261017-001-testgate-stabilization.md) and [History](HISTORY.md) retain provenance.

Keep RESP fixture lifecycle inside existing SQL test owners, use direct `dotnet test` and local positively owned database cleanup. A bounded GUI-owned subprocess for fatal modal/hang probes is not permission to recreate the old global runner.
