# SAL-T00 — exact arithmetic and Sales foundations

**Executed:** 2026-10-22. **Scope:** T00-owned pure arithmetic/contracts and permission registry portions of SAL-A01–A08/A20; complete acceptance rows remain open.

## Candidate and environment

- Source/test: `b5a2f3f059b301e26e01a83bd2a26490ac60dce9`, from implementation base `67b8099030df75c6106201db71157e7ec1cbc21c`; design baseline remains `35ba2e8a3f37fb07095701eb94f4b56f7941c491`.
- Windows 11 Home Single Language `10.0.26200`, Framework Release `533509`, SDK `8.0.425`, VSTest `17.11.1` x64, Release/net48.
- Vendor pins unchanged: BootstrapSourceGrid `2bd7874f7e80b22ef181763749c2a2938ab7f16e`, Bootstrap5WinFormUI `2be9a4d778fda7016207b75470020be6126f2f3f`, SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`.
- No package/provider upgrade. Domain adds the framework `System.Numerics` reference. No SQL, GUI, session or package execution.

## Implemented boundary

`InventoryValuationMath` uses exact BigInteger coefficients, checked storage bounds, midpoint-away rounding, full-depletion value consumption and cumulative LineNo allocation. Checked amount/quantity sums support later writers. Independent constant vectors cover paid/gift Q20/V300, residual Q3/V0.0002, final depletion, wide intermediates, precision/range errors and 200/201 issue lines.

Sales contracts define command allowlists, opaque versions, slim cursor page/detail shapes and property-scoped strict yyyy-MM-dd calendar JSON without changing global UTC audit handling. Draft detail cost/profit must be null; Posted detail requires costs and posting timestamp. Domain persistence shapes and separate Active Read/Write/Post definitions are present; no assignments are granted.

This does not implement Save reference/identity/Note/sequence validation, sales persistence or immutable database snapshots, cursor validation, inventory writer cutover, Local or UI behavior. These remain later task obligations. The calculator's 201-line rejection is not evidence for an unimplemented Save endpoint.

## Actual execution

| Invocation | Executed | PASS | FAIL | SKIP |
|---|---:|---:|---:|---:|
| Baseline affected Application | 48 | 48 | 0 | 0 |
| Baseline Architecture registry | 2 | 2 | 0 | 0 |
| Arithmetic RED, compilable API | 11 | 6 | 5 | 0 |
| Contract/default development run | 26 | 23 | 3 | 0 |
| Detail cost-state RED | 1 | 0 | 1 | 0 |
| Final affected Application | 75 | 75 | 0 | 0 |
| Final Architecture registry | 3 | 3 | 0 | 0 |

Initial arithmetic RED had four intended missing-product-calculation failures and one invalid fixture that legitimately overflowed the snapshot bound. The fixture was corrected from Q1 to Q100000; the original TRX is retained locally. Three contract failures exposed omitted JSON PageSize becoming0; the constructor now defaults omitted input to50. Detail RED proved missing Posted costs were accepted before the guard. A BOM-decoding compilation error was repaired and is not behavioral RED evidence. Earlier 74/3 and75-row pre-format passing runs remain separate invocations, not additional unique coverage.

Final discovery matched execution: math11 + contracts11 + Sales permissions5 + affected GoodsReceipt37 + existing registry4 + authorization7 =75 Application rows; Architecture registry3. Independent task review approved spec compliance and quality without findings and verified final TRX hashes. No complete-project or whole-SAL PASS is claimed.

## Commands and local evidence

Application locked restore succeeded. Architecture baseline restore succeeded without dependency changes. Commands below ran from the candidate checkout; discovery used the same filters with `--no-build --list-tests`.

```powershell
$appFilter = 'FullyQualifiedName~SalesValuationMathTests|FullyQualifiedName~SalesContractTests|FullyQualifiedName~SalesPermissionTests|FullyQualifiedName~PermissionRegistryTests|FullyQualifiedName~PermissionAuthorizationTests|FullyQualifiedName~GoodsReceiptApplicationTests'
dotnet test tests/MyDmsVn.Server.Application.Tests -c Release -f net48 --no-restore --filter $appFilter --logger 'trx;LogFileName=t00-focused-formatted.trx' --results-directory TestResults/sal-t00-verified
dotnet test tests/MyDmsVn.Architecture.Tests -c Release -f net48 --no-restore --filter FullyQualifiedName~PermissionRegistryCoverageTests --logger 'trx;LogFileName=t00-architecture-green.trx' --results-directory TestResults/sal-t00-verified
```

- Final Application TRX SHA-256: `f2359e10b37e7a2281a6ee0004d555805c1008d4067dd347a20ecd86e72f7e5d`.
- Final Architecture TRX SHA-256: `db886550bf66df44a9dcb4f18be583c1419ae8ccd54a3ab7133e2b32cacfd703`.
- Original failures/discovery/command log remain local; no raw TRX, connections or SQL identifiers are committed. Diff/encoding checks passed. No SQL/GUI resources were allocated in T00.

T01 is next in the [serial plan](../../archive/20261023-pre-ui-next/plans/20261021-002-direct-sales-and-moving-average-costing.md). [SAL acceptance](../../DIRECT_SALES_ACCEPTANCE.md) remains open for all required multi-task portions and frozen T08/Owner disposition. This development checkpoint is not deployable; operational release prerequisites remain UNACCEPTED / UNSCHEDULED.
