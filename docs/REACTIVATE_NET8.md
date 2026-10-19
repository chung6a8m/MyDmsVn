# Reactivate modern product targets — deferred procedure

**Status:** DEFERRED / NOT AUTHORIZED by the current [GUI-P5 and REL/P6 roadmap](plans/README.md). Active production and all ten test projects remain net48. [ADR 0008](adr/0008-temporary-net8-target-pause.md) retains parked AppCore source. Completed RESP/L2DB and the new acceptance plans do **not** reactivate net8.

## Entry conditions

The owner first chooses the supported server/Desktop/runtime release scope, including whether .NET 8 remains appropriate at that future release date. Start from the accepted active-plan baseline and inventory the actual source, dependencies, tests and packages. An SDK version is not product runtime coverage.

## Restore a coherent graph

1. Restore selected modern targets across SharedKernel, Contracts, Domain, Application, Infrastructure, DbMigrator, Local adapter and applicable tests, retaining net48 unless separately changed by the owner.
2. Restore modern WinForms/Desktop targets and AppCore solution/composition/package entries only when included in the approved scope. Validate vendored UI references and native dependencies.
3. Regenerate the parked AppCore lock after the runtime decision: T06 deliberately leaves its historical RepoDb/Dapper nodes untouched while removing obsolete active central pins. It cannot be reused as a current locked graph. Review lock-file/dependency changes and verify locked restore/build on all activated targets. Use the accepted post-L2DB provider and Respawn fixture contract; do not copy retired RepoDb/modern Respawn snippets blindly.
4. Update actual active test project/TFM mappings, direct test-command framework selections and fixture ownership. Preserve historical parked case mappings without relabeling them as executed; account for new/moved/replaced Theory cases explicitly.
5. Keep test databases separated across testhost processes/TFMs; sequential project execution and GUI serialization still apply. Reset/profile behavior must work in each activated host.

## Verification and release

Use [TEST_POLICY](TEST_POLICY.md) with a dedicated runtime-migration acceptance matrix: every activated project builds, all affected contracts/Application/SQL cases execute, each shipped GUI host has guarded and packaged evidence, and release manifests match runtime/schema/package contents. Execute legacy and modern SQL/GUI targets sequentially unless a separately verified resource policy permits concurrency.

P7 also requires real HTTP/auth/adapter parity evidence. Missing targeting packs, dependency compatibility, SQL/GUI access, case identity, cleanup or package integrity remains BLOCKED. Until acceptance, do not advertise the modern host or API as supported.

If reactivation fails, revert its coherent matrix/reference/solution/package/manifest changes together. Historical dual-target evidence cannot make a partial reactivation pass.
