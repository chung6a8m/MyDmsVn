# ADR 0006 — Historical task gates and module projects

**Status:** Execution framework removed and policy superseded by [ADR 0010](0010-respawn-fixture-reuse-and-test-policy.md) / [TEST_POLICY](../TEST_POLICY.md), 2026-10-18.
**Original date:** 2026-10-16.

The original proposal combined task-level selection, mandatory per-PR Final envelopes and extraction of modular test projects. The tooling and modularization were implemented historically. The owner subsequently requested complete removal of `tests/test-gates.v1.json`, SqlTestHarness, SQL runner and runner tests, including dependent wrappers and framework-only tests.

The modular Application, Architecture, Desktop/session and six SQL test owners remain. They run directly through `dotnet test` or Test Explorer; the local IntegrationTests helper owns disposable database creation and guarded cleanup. This historical ADR does not require restoring Task/Final tooling, fingerprint registries, approval JSON, global leases or a full per-PR envelope.

New work follows affected-scope and named-milestone rules in TEST_POLICY. RESP adds fixture reuse inside existing test projects, followed by L2DB. The unimplemented manifest-v2 redesign is cancelled by the framework removal, not a deferred prerequisite.

The [original ADR](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/adr/0006-task-scoped-test-execution-and-module-projects.md) and [History](../HISTORY.md) preserve rationale and evidence without reviving the old policy or removed framework.
