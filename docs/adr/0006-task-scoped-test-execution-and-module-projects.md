# ADR 0006 — Historical task gates and module projects

**Status:** Superseded as an execution-policy proposal by [ADR 0010](0010-respawn-fixture-reuse-and-test-policy.md) and [TEST_POLICY](../TEST_POLICY.md), 2026-10-18.
**Original date:** 2026-10-16.

The original proposal combined task-level test selection, mandatory per-PR Final envelopes and extraction of modular test projects. Task/Final tooling and modular projects were subsequently implemented. They remain source code; they are not tasks to restart.

The owner requested removal and replacement of the existing test policy. New work follows the affected-scope and milestone rules in TEST_POLICY. This historical ADR imposes no additional L1/L2/L3/L4 matrix, blanket full-PR gate or multi-pass benchmark.

Existing manifests and runner validation remain executable constraints until the owning RESP task adapts them. Do not bypass a failed source, approval, ownership or watchdog check. General fingerprint-v2/manifest splitting remains [deferred](../DEFERRED_WORK.md).

The [original ADR](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/adr/0006-task-scoped-test-execution-and-module-projects.md) and [History](../HISTORY.md) preserve rationale and evidence without reviving the old policy.
