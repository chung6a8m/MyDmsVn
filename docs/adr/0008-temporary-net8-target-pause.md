# ADR 0008 — Temporary .NET 8 product target pause

**Status:** Accepted and implemented, PR #32 (`8e310778`); current-state clarification 2026-10-18.
**Original date:** 2026-10-16.

## Decision

The active first-party product, SQL-test and WinForms target is **net48**. Keep Desktop.App as the shipped desktop and DbMigrator as a separate net48 administrator package. AppCore source remains parked outside solution/release; historical product net8/net8-windows cases are PARKED / NOT RUN.

The SQL runner and runner tests continue on tool-only net8.0, and the shared SQL harness stays netstandard2.0. Tool-host tests do not prove product runtime coverage. Preserve vendor source/targets and use the appropriate active host references; no vendor rewrite or SDK downgrade is implied.

The owner reaffirmed active net48 when approving L2DB option 3A and the v1a roadmap. Neither Respawn compatibility nor LINQ to DB adoption authorizes modern-target reactivation or a .NET 10 upgrade.

## Evidence and qualification

Runtime/build/package/GUI/restoration evidence is recorded in [History](../HISTORY.md). The original candidate-specific Stage B exception has ended. The owner waived the comparable-duration measurement at `09d00bfa` because no matched baseline existed; that is not a measured performance result or a waiver of future functional/ownership checks.

## Reactivation

Reactivation is [deferred](../DEFERRED_WORK.md). Follow [REACTIVATE_NET8](../REACTIVATE_NET8.md) after the owner activates that scope: restore the coherent project/reference/solution/package graph, dependencies and active case mappings, then obtain fresh evidence for every restored runtime. Preserve net48 unless the owner separately changes that requirement.

Current new-work testing follows [TEST_POLICY](../TEST_POLICY.md). This accepted pause does not require rerunning its historical acceptance envelope for unrelated documentation or feature changes.
