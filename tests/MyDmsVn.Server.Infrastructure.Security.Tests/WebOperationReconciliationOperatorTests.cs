using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using MyDmsVn.WebOperation.Operator;
using Xunit;
using Scenario = MyDmsVn.Server.Infrastructure.IntegrationTests.WebOperationJournalTests.Scenario;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebOperationReconciliationOperatorTests
{
    private static readonly Guid Operation = Guid.Parse("c23e4567-e89b-42d3-a456-426614174000");
    private static readonly Guid Epoch = Guid.Parse("a23e4567-e89b-12d3-a456-426614174000");
    private static readonly byte[] Fingerprint = Enumerable.Repeat((byte)7, 32).ToArray();

    [Fact]
    public async Task Reconciliation_help_and_invalid_inputs_never_open_sql()
    {
        using var output = new StringWriter();
        using var errors = new StringWriter();
        Assert.Equal(0, await OperatorReconciliationApplication.RunAsync(new[] { "reconcile-committed", "--help" },
            _ => throw new Exception("SQL reached"), output, errors, CancellationToken.None));
        Assert.Contains("reconcile-committed", output.ToString());
        Assert.DoesNotContain("--confirm", output.ToString());
        Assert.True(ReconcileCommittedOptions.TryParse(Arguments(), out var parsed, out _));
        Assert.Equal(32, parsed!.FingerprintBytes().Length);
        foreach (var extra in new[] { "--confirm", "--execute", "--response", "--kind", "--target", "--actor" })
            Assert.False(ReconcileCommittedOptions.TryParse(Arguments().Concat(new[] { extra, "1" }).ToArray(), out _, out _));
        foreach (var change in new[] { ("--actor", "01"), ("--database", "master"), ("--case", "../bad"),
            ("--case", "CON"), ("--operation", Epoch.ToString("D")), ("--fingerprint", new string('G', 64)),
            ("--timeout-seconds", "26"), ("--current-epoch", Guid.Empty.ToString("D")) })
        {
            var args = Arguments(); args[Array.IndexOf(args, change.Item1) + 1] = change.Item2;
            Assert.False(ReconcileCommittedOptions.TryParse(args, out _, out _));
            Assert.Equal(2, await OperatorReconciliationApplication.RunAsync(args, _ => throw new Exception("SQL reached"), output, errors, CancellationToken.None));
        }
    }

    [Theory]
    [InlineData(WebOperationReconciliationOutcome.Committed, "BUSINESS_COMMITTED_RESULT_UNAVAILABLE", 0)]
    [InlineData(WebOperationReconciliationOutcome.Superseded, "BUSINESS_COMMITTED_RESULT_UNAVAILABLE_SUPERSEDED", 0)]
    [InlineData(WebOperationReconciliationOutcome.Unknown, "UNKNOWN/ESCALATE", 8)]
    public async Task Output_is_bounded_and_never_returns_business_projection(WebOperationReconciliationOutcome outcome, string label, int code)
    {
        using var output = new StringWriter(); using var errors = new StringWriter();
        Assert.Equal(code, await OperatorReconciliationApplication.RunAsync(Arguments(), _ => new Client(outcome), output, errors, CancellationToken.None));
        Assert.Equal("status=" + label + Environment.NewLine, output.ToString() + errors);
        foreach (var type in new[] { typeof(OperatorReconciliationResult), typeof(WebOperationBusinessReconciliation) })
            Assert.All(type.GetProperties(), property => Assert.True(property.PropertyType.IsEnum || property.PropertyType == typeof(bool) || property.PropertyType == typeof(int)));
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Six_baseline_kinds_preserve_bytes_and_only_current_posts_have_positive_proof()
    {
        for (byte kind = 1; kind <= 6; kind++)
        {
            var selected = kind;
            await WebOperationResolutionScenario.Owned(async s =>
            {
                await Seed(s, posted: selected == 3 || selected == 6);
                await Journal(s, selected, Operation, 1, expired: true);
                var result = await Check(s);
                Assert.Equal(selected == 3 || selected == 6 ? WebOperationReconciliationOutcome.Committed : WebOperationReconciliationOutcome.Unknown, result.Outcome);
            });
        }
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Four_draft_baselines_require_complete_lineage_ending_in_valid_post()
    {
        foreach (byte kind in new byte[] { 1, 2, 4, 5 })
            await WebOperationResolutionScenario.Owned(async s =>
            {
                await Seed(s);
                await Journal(s, kind, Operation, 1, expired: true);
                if (kind == 1 || kind == 4) await Journal(s, (byte)(kind + 1), Guid.NewGuid(), 2);
                await Journal(s, kind <= 3 ? (byte)3 : (byte)6, Guid.NewGuid(), 3);
                Assert.Equal(WebOperationReconciliationOutcome.Superseded, (await Check(s)).Outcome);
            });
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Superseded_reconciliation_requires_same_owned_monotonic_committed_lineage()
    {
        foreach (var variant in new[] { "actor", "epoch", "equal", "earlier", "module", "target", "reuse", "cancelled", "missing", "update-only", "post-terminal", "intermediate-actor" })
            await WebOperationResolutionScenario.Owned(async s =>
            {
                await Seed(s);
                await Journal(s, variant == "post-terminal" ? (byte)3 : (byte)1, Operation, 1, expired: true);
                if (variant == "intermediate-actor") await Journal(s, 2, Guid.NewGuid(), 2, actor: s.Actor + 1);
                if (variant == "reuse") await Journal(s, 1, Guid.NewGuid(), 2);
                if (variant == "cancelled")
                {
                    await s.Observer.ExecuteAsync(@"DECLARE @n datetime2(7)='20250103';
INSERT dbo.WebOperationTerminalAudit VALUES(@actor,@op,@epoch,2,41,@hash,NEWID(),'case-negative','NOT_EXECUTED','case-evidence',USER_NAME(),ORIGINAL_LOGIN(),@n);
INSERT dbo.WebOperationJournal VALUES(@actor,@op,@epoch,1,1,2,41,@hash,NULL,'CANCELLED',NULL,@n,@n,NULL);",
                        new { actor = s.Actor, op = Guid.NewGuid(), epoch = s.Epoch, hash = Fingerprint });
                }
                if (variant != "missing") await Journal(s, variant == "module" ? (byte)6 : variant == "update-only" || variant == "post-terminal" ? (byte)2 : (byte)3,
                    Guid.NewGuid(), variant == "equal" ? 1 : variant == "earlier" ? 0 : 4,
                    actor: variant == "actor" ? s.Actor + 1 : (int?)null, epoch: variant == "epoch" ? Guid.NewGuid() : (Guid?)null,
                    target: variant == "target" ? 42 : 41);
                Assert.Equal(WebOperationReconciliationOutcome.Unknown, (await Check(s)).Outcome);
            });
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Physical_contradictions_never_produce_positive_proof()
    {
        foreach (byte kind in new byte[] { 3, 6 })
            foreach (var variant in new[] { "header", "line", "status", "quantity", "value", "cost", "audit", "balance", "valuation-missing", "extra-ledger" })
                await WebOperationResolutionScenario.Owned(async s =>
                {
                    await Seed(s, variant: variant);
                    await Journal(s, kind, Operation, 1, expired: true);
                    if (variant == "header") await s.Observer.ExecuteAsync(kind == 3
                        ? "DELETE dbo.SalesReceiptLines; DELETE dbo.SalesReceipts;" : "DELETE dbo.GoodsReceiptLines; DELETE dbo.GoodsReceipts;");
                    if (variant == "line") await s.Observer.ExecuteAsync(kind == 3 ? "DELETE dbo.SalesReceiptLines WHERE [LineNo]=2;" : "DELETE dbo.GoodsReceiptLines;");
                    if (variant == "status") await s.Observer.ExecuteAsync(kind == 3
                        ? "UPDATE dbo.SalesReceipts SET Status='Draft',PostedAtUtc=NULL,PostedByUserId=NULL;"
                        : "UPDATE dbo.GoodsReceipts SET Status='Draft',PostedAtUtc=NULL,PostedByUserId=NULL;");
                    if (variant == "quantity") await s.Observer.ExecuteAsync(kind == 3 ? "UPDATE dbo.SalesReceiptLines SET Qty=Qty+1;" : "UPDATE dbo.GoodsReceiptLines SET Quantity=Quantity+1;");
                    if (variant == "audit") await s.Observer.ExecuteAsync(kind == 3 ? "UPDATE dbo.SalesReceipts SET PostedAtUtc=DATEADD(second,1,PostedAtUtc);" : "UPDATE dbo.GoodsReceipts SET PostedAtUtc=DATEADD(second,1,PostedAtUtc);");
                    if (variant == "cost") await s.Observer.ExecuteAsync(kind == 3 ? "UPDATE dbo.SalesReceiptLines SET CostAmount=CostAmount+1;" : "UPDATE dbo.GoodsReceiptLines SET UnitCost=UnitCost+1;");
                    Assert.Equal(WebOperationReconciliationOutcome.Unknown, (await Check(s)).Outcome);
                });
    }

    [SqlServerTheory(SqlTestCategory.SecurityProvisioning)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public Task Sales_revenue_amount_or_price_contradiction_refuses_current_and_superseded_proof(int baselineKind, bool changePrice) =>
        WebOperationResolutionScenario.Owned(async s =>
        {
            await Seed(s);
            await Journal(s, (byte)baselineKind, Operation, 1, expired: true);
            if (baselineKind == 1) await Journal(s, 2, Guid.NewGuid(), 2);
            if (baselineKind != 3) await Journal(s, 3, Guid.NewGuid(), 3);
            Assert.Equal(baselineKind == 3 ? WebOperationReconciliationOutcome.Committed : WebOperationReconciliationOutcome.Superseded,
                (await Check(s)).Outcome);

            // Each independent arrangement changes exactly one normal-line revenue field.
            // Ledger, valuation, source quantity and COGS remain the original coherent values.
            await s.Observer.ExecuteAsync(changePrice
                ? "UPDATE dbo.SalesReceiptLines SET UnitPrice=UnitPrice+1 WHERE [LineNo]=1;"
                : "UPDATE dbo.SalesReceiptLines SET LineAmount=LineAmount+1 WHERE [LineNo]=1;");
            Assert.Equal(WebOperationReconciliationOutcome.Unknown, (await Check(s)).Outcome);
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Sales_revenue_uses_exact_half_up_rounding_and_preserves_gift_zero() => WebOperationResolutionScenario.Owned(async s =>
    {
        await Seed(s, fractional: true);
        // 0.0001 * 1.5000 = 0.00015, rounded at four places to 0.0002.
        // The independent gift row keeps nonzero UnitPrice=3 and zero revenue.
        await s.Observer.ExecuteAsync("UPDATE dbo.SalesReceiptLines SET UnitPrice=1.5000,LineAmount=0.0002 WHERE [LineNo]=1;");
        await Journal(s, 3, Operation, 1, expired: true);
        Assert.Equal(WebOperationReconciliationOutcome.Committed, (await Check(s)).Outcome);
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Identity_retention_and_missing_row_are_fail_closed() => WebOperationResolutionScenario.Owned(async s =>
    {
        await Seed(s);
        Assert.Equal(WebOperationReconciliationOutcome.Unknown, (await Check(s)).Outcome);
        await Journal(s, 3, Operation, 1, expired: false, retained: true);
        Assert.Equal(WebOperationReconciliationOutcome.Unknown, (await Check(s)).Outcome);
        Assert.Equal(WebOperationReconciliationOutcome.Unknown, (await Check(s, actor: s.Actor + 1)).Outcome);
        Assert.Equal(WebOperationReconciliationOutcome.Unknown, (await Check(s, epoch: Guid.NewGuid())).Outcome);
        Assert.Equal(WebOperationReconciliationOutcome.Unknown, (await Check(s, fingerprint: new byte[32])).Outcome);
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Evidence_delivery_failure_cannot_report_a_positive_case() => WebOperationResolutionScenario.Owned(async s =>
    {
        await Seed(s); await Journal(s, 3, Operation, 1, expired: true);
        foreach (var failAt in new[] { "attempt", "outcome", "backup", "none" })
        {
            var before = await Snapshot(s);
            Assert.True(ReconcileCommittedOptions.TryParse(Arguments(s), out var options, out _));
            var result = await new SqlOperatorReconciliationClient(new TestConnections(s), new Evidence(failAt)).ReconcileAsync(options!, s.Stop.Token);
            Assert.Equal(failAt == "none" ? WebOperationReconciliationOutcome.Committed : WebOperationReconciliationOutcome.Unknown, result.Outcome);
            Assert.Equal(before, await Snapshot(s));
        }
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Fractional_cumulative_cost_and_full_depletion_use_exact_frozen_allocation()
    {
        foreach (var full in new[] { false, true })
            await WebOperationResolutionScenario.Owned(async s =>
            {
                await Seed(s, fractional: true, fullDepletion: full);
                await Journal(s, 3, Operation, 1, expired: true);
                Assert.Equal(WebOperationReconciliationOutcome.Committed, (await Check(s)).Outcome);
                // This source-only change is legal SQL but contradicts frozen value/allocation.
                await s.Observer.ExecuteAsync("UPDATE dbo.SalesReceiptLines SET CostAmount=CostAmount+0.0001 WHERE [LineNo]=1;");
                Assert.Equal(WebOperationReconciliationOutcome.Unknown, (await Check(s)).Outcome);
            });
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Canonical_expired_later_post_is_committed_lineage_but_future_or_padded_tombstone_is_unknown()
    {
        foreach (var variant in new[] { "canonical", "future", "padded" })
            await WebOperationResolutionScenario.Owned(async s =>
            {
                await Seed(s); await Journal(s, 1, Operation, 1, expired: true);
                await s.Observer.ExecuteAsync(@"DECLARE @n datetime2(7)=CASE WHEN @future=1 THEN SYSUTCDATETIME() ELSE CONVERT(datetime2(7),'20250103') END;
INSERT dbo.WebOperationJournal VALUES(@actor,NEWID(),@epoch,1,1,3,41,@hash,41,@state,NULL,@n,@n,DATEADD(day,7,@n));",
                    new { actor = s.Actor, epoch = s.Epoch, hash = Fingerprint, future = variant == "future", state = variant == "padded" ? "EXPIRED " : "EXPIRED" });
                Assert.Equal(variant == "canonical" ? WebOperationReconciliationOutcome.Superseded : WebOperationReconciliationOutcome.Unknown, (await Check(s)).Outcome);
            });
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Real_evidence_contains_only_bounded_case_and_readback_metadata() => WebOperationResolutionScenario.Owned(async s =>
    {
        await Seed(s); await Journal(s, 3, Operation, 1, expired: true);
        using var roots = new EvidenceRoots();
        var store = new MaintenanceEvidenceStore(new MaintenanceEvidencePolicy(roots.Primary, roots.Backup, TimeSpan.FromDays(90)));
        Assert.True(ReconcileCommittedOptions.TryParse(Arguments(s), out var options, out _));
        var before = await Snapshot(s);
        Assert.Equal(WebOperationReconciliationOutcome.Committed,
            (await new SqlOperatorReconciliationClient(new TestConnections(s), store).ReconcileAsync(options!, s.Stop.Token)).Outcome);
        Assert.Equal(before, await Snapshot(s));
        foreach (var root in new[] { roots.Primary, roots.Backup })
        {
            var files = Directory.GetFiles(Path.Combine(root, "case-r4")); Assert.Equal(3, files.Length);
            var text = string.Join("\n", files.Select(File.ReadAllText));
            Assert.Contains("BusinessCommittedResultUnavailable", text);
            Assert.Contains("Delivered", text);
            foreach (var secret in new[] { "Private source", "ResultBytes", "RowVersion", "SaleLineId", "ReceiptLineId", s.Database.ConnectionString })
                Assert.DoesNotContain(secret, text);
        }
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Reconciliation_takes_inventory_barrier_before_journal_range_to_avoid_writer_lock_inversion() => WebOperationResolutionScenario.Owned(async s =>
    {
        await Seed(s); await Journal(s, 3, Operation, 1, expired: true);
        var writer = await s.Transaction();
        await writer.Sql.ExecuteAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'MyDmsVn.InventoryValuation.v1',@LockMode='Shared',@LockOwner='Transaction',@LockTimeout=1000; IF @r<0 THROW 51400,'Writer gate unavailable.',1;", transaction: writer.Tx);
        var reader = await s.Transaction();
        var writerId = await writer.Sql.QuerySingleAsync<int>("SELECT @@SPID;", transaction: writer.Tx);
        var readerId = await reader.Sql.QuerySingleAsync<int>("SELECT @@SPID;", transaction: reader.Tx);
        var observation = s.Track(new SqlWebOperationReconciliationMaintenance(reader).ReconcileAsync(s.Actor, s.Epoch, Operation, Fingerprint, s.Stop.Token));
        await s.Blocked(readerId, writerId);
        // A normal writer holds the shared gate until its final same-transaction journal insert.
        // This unrelated target must be able to finish while reconciliation waits for the gate.
        await writer.Sql.ExecuteAsync(@"DECLARE @n datetime2(7)=SYSUTCDATETIME();
INSERT dbo.WebOperationJournal VALUES(@actor,NEWID(),@epoch,1,1,1,NULL,@hash,42,'COMMITTED',0x7b7d,@n,@n,DATEADD(day,7,@n));",
            new { actor = s.Actor, epoch = s.Epoch, hash = Fingerprint }, writer.Tx, commandTimeout: 5);
        writer.Tx.Commit();
        Assert.Equal(WebOperationReconciliationOutcome.Committed, (await observation).Outcome);
        reader.Tx.Rollback();
        Assert.Equal(WebOperationReconciliationOutcome.Committed, (await Check(s)).Outcome);
    });

    private static async Task<WebOperationBusinessReconciliation> Check(Scenario s, int? actor = null, Guid? epoch = null, byte[]? fingerprint = null)
    {
        var before = await Snapshot(s);
        var transaction = await s.Transaction();
        WebOperationBusinessReconciliation result;
        try { result = await new SqlWebOperationReconciliationMaintenance(transaction).ReconcileAsync(actor ?? s.Actor, epoch ?? s.Epoch, Operation, fingerprint ?? Fingerprint, s.Stop.Token); }
        finally { transaction.Tx.Rollback(); }
        Assert.Equal(before, await Snapshot(s));
        return result;
    }

    private static async Task<string> Snapshot(Scenario s)
    {
        var values = new System.Collections.Generic.List<string>();
        foreach (var table in new[] { "SalesReceipts ORDER BY SaleId", "SalesReceiptLines ORDER BY SaleLineId", "GoodsReceipts ORDER BY ReceiptId", "GoodsReceiptLines ORDER BY ReceiptLineId", "StockLedger ORDER BY LedgerId", "StockValuationEntries ORDER BY LedgerId", "StockBalances ORDER BY WarehouseId,ProductId", "WebOperationJournal ORDER BY ActorUserId,OperationId", "WebOperationTerminalAudit ORDER BY ActorUserId,OperationId", "WebDeploymentState ORDER BY Id" })
            values.Add(await s.Observer.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo." + table + " FOR JSON PATH,INCLUDE_NULL_VALUES);"));
        return string.Join("|", values);
    }

    private static Task Journal(Scenario s, byte kind, Guid operation, int day, bool expired = false, int? actor = null, Guid? epoch = null, long target = 41, bool retained = false) => s.Observer.ExecuteAsync(@"
DECLARE @n datetime2(7)=CASE WHEN @retained=1 THEN SYSUTCDATETIME() ELSE DATEADD(day,@day,CONVERT(datetime2(7),'20250101')) END;
INSERT dbo.WebOperationJournal VALUES(@actor,@op,@epoch,1,1,@kind,CASE WHEN @kind IN(1,4) THEN NULL ELSE @target END,@hash,@target,
CASE WHEN @expired=1 THEN 'EXPIRED' ELSE 'COMMITTED' END,CASE WHEN @expired=1 THEN NULL ELSE 0x7b7d END,@n,@n,DATEADD(day,7,@n));",
        new { actor = actor ?? s.Actor, op = operation, epoch = epoch ?? s.Epoch, kind, target, hash = Fingerprint, day, expired, retained });

    private static Task Seed(Scenario s, bool posted = true, string variant = "", bool fractional = false, bool fullDepletion = false) => s.Observer.ExecuteAsync(@"
INSERT dbo.Products(Code,Name,Unit) VALUES(N'R4',N'Private source',N'ea'); DECLARE @p int=SCOPE_IDENTITY();
INSERT dbo.Warehouses(Code,Name) VALUES(N'R4',N'Warehouse'); DECLARE @w int=SCOPE_IDENTITY();
INSERT dbo.Employees(Code,Name) VALUES(N'R4',N'Employee'); DECLARE @e int=SCOPE_IDENTITY();
INSERT dbo.Customers(Code,Name) VALUES(N'R4',N'Customer'); DECLARE @c int=SCOPE_IDENTITY();
DECLARE @t datetime2(7)='20250101';
SET IDENTITY_INSERT dbo.GoodsReceipts ON;
INSERT dbo.GoodsReceipts(ReceiptId,ReceiptNo,ReceiptDate,WarehouseId,EmployeeId,Status,CreatedByUserId,PostedAtUtc,PostedByUserId)
VALUES(41,N'R4-R','20250101',@w,@e,CASE WHEN @posted=1 THEN 'Posted' ELSE 'Draft' END,@actor,CASE WHEN @posted=1 THEN @t END,CASE WHEN @posted=1 THEN @actor END);
SET IDENTITY_INSERT dbo.GoodsReceipts OFF;
INSERT dbo.GoodsReceiptLines(ReceiptId,[LineNo],ProductId,Quantity,UnitCost,CreatedByUserId) VALUES(41,1,@p,@rq,@rp,@actor); DECLARE @rl bigint=SCOPE_IDENTITY();
SET IDENTITY_INSERT dbo.SalesReceipts ON;
INSERT dbo.SalesReceipts(SaleId,SaleNo,SaleDate,WarehouseId,EmployeeId,CustomerId,Status,CreatedByUserId,PostedAtUtc,PostedByUserId)
VALUES(41,N'R4-S','20250101',@w,@e,@c,CASE WHEN @posted=1 THEN 'Posted' ELSE 'Draft' END,@actor,CASE WHEN @posted=1 THEN @t END,CASE WHEN @posted=1 THEN @actor END);
SET IDENTITY_INSERT dbo.SalesReceipts OFF;
INSERT dbo.SalesReceiptLines(SaleId,[LineNo],ProductId,Qty,Unit,IsGift,LineAmount,UnitPrice,PostedUnitCost,CostAmount,CreatedByUserId)
VALUES(41,1,@p,@sq1,N'ea',0,ROUND(@sq1*3,4),3,CASE WHEN @posted=1 THEN @uc END,CASE WHEN @posted=1 THEN @sc1 END,@actor);
DECLARE @sl1 bigint=SCOPE_IDENTITY();
INSERT dbo.SalesReceiptLines(SaleId,[LineNo],ProductId,Qty,Unit,IsGift,LineAmount,UnitPrice,PostedUnitCost,CostAmount,CreatedByUserId)
VALUES(41,2,@p,@sq2,N'ea',1,0,3,CASE WHEN @posted=1 THEN @uc END,CASE WHEN @posted=1 THEN @sc2 END,@actor);
DECLARE @sl2 bigint=SCOPE_IDENTITY();
IF @posted=1 BEGIN
INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId) VALUES(@w,@p,'GoodsReceipt',41,@rl,@rq,@t,@actor);
DECLARE @l bigint=SCOPE_IDENTITY();
IF @variant<>'valuation-missing' INSERT dbo.StockValuationEntries VALUES(@l,CASE WHEN @variant='value' THEN @rv+1 ELSE @rv END);
INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId) VALUES(@w,@p,'SalesReceipt',41,@sl1,-@sq1,@t,@actor);
SET @l=SCOPE_IDENTITY(); INSERT dbo.StockValuationEntries VALUES(@l,-@sc1);
INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId) VALUES(@w,@p,'SalesReceipt',41,@sl2,-@sq2,@t,@actor);
SET @l=SCOPE_IDENTITY(); INSERT dbo.StockValuationEntries VALUES(@l,-@sc2);
INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@w,@p,CASE WHEN @variant='balance' THEN @bq+1 ELSE @bq END,@bv);
IF @variant='extra-ledger' BEGIN
INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId) VALUES(@w,@p,'GoodsReceipt',41,999,1,@t,@actor);
SET @l=SCOPE_IDENTITY(); INSERT dbo.StockValuationEntries VALUES(@l,0); END
END;", new { actor = s.Actor, posted, variant, rq = fractional ? .0003m : 10m, rp = fractional ? 3333.3333m : 2m,
        rv = fractional ? 1m : 20m, sq1 = fractional ? .0001m : 3m, sq2 = fractional ? fullDepletion ? .0002m : .0001m : 2m,
        uc = fractional ? 3333.33333333m : 2m, sc1 = fractional ? fullDepletion ? .3333m : .3334m : 6m,
        sc2 = fractional ? fullDepletion ? .6667m : .3333m : 4m, bq = fractional ? fullDepletion ? 0m : .0001m : 5m,
        bv = fractional ? fullDepletion ? 0m : .3333m : 10m });

    private static string[] Arguments(Scenario? s = null) => new[] { "reconcile-committed", "--environment", "owned-test", "--database", s == null ? "MyDmsVn_Test_Reconcile" : new SqlConnectionStringBuilder(s.Database.ConnectionString).InitialCatalog,
        "--current-epoch", (s?.Epoch ?? Epoch).ToString("D"), "--stored-epoch", (s?.Epoch ?? Epoch).ToString("D"), "--actor", (s?.Actor ?? 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
        "--operation", Operation.ToString("D"), "--fingerprint", string.Concat(Fingerprint.Select(x => x.ToString("x2"))), "--correlation", "d23e4567-e89b-42d3-a456-426614174000", "--case", "case-r4", "--timeout-seconds", "15" };

    private sealed class Client(WebOperationReconciliationOutcome outcome) : IOperatorReconciliationClient
    {
        public Task<OperatorReconciliationResult> ReconcileAsync(ReconcileCommittedOptions options, CancellationToken token) => Task.FromResult(new OperatorReconciliationResult(outcome));
    }
    private sealed class TestConnections(Scenario scenario) : IMaintenanceSqlConnectionFactory
    {
        public async Task<SqlConnection> OpenAsync(IOperatorTargetOptions options, CancellationToken token)
        { var connection = new SqlConnection(scenario.Database.ConnectionString); await connection.OpenAsync(token); return connection; }
    }
    private sealed class Evidence(string failure) : IMaintenanceEvidenceStore
    {
        public Task<MaintenanceEvidenceReceipt> WriteAttemptAsync(MaintenanceEvidenceRecord record, CancellationToken token) => Reply("attempt");
        public Task<MaintenanceEvidenceReceipt> WriteOutcomeAsync(MaintenanceEvidenceRecord record, CancellationToken token) => Reply("outcome");
        public Task<MaintenanceEvidenceReceipt> FinalizeAndBackupAsync(string id, CancellationToken token) => Reply("backup");
        private Task<MaintenanceEvidenceReceipt> Reply(string phase) => failure == phase ? throw new IOException("private failure") : Task.FromResult(new MaintenanceEvidenceReceipt("case-r4", new[] { new string('a', 64) }, phase == "backup" ? MaintenanceEvidenceDelivery.Delivered : MaintenanceEvidenceDelivery.PrimaryOnly));
        public Task<MaintenanceEvidencePurge> PurgeExpiredAsync(string a, string b, string c, string d, string e, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class EvidenceRoots : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "b03-reconcile-" + Guid.NewGuid().ToString("N"));
        public string Primary => Path.Combine(_root, "primary");
        public string Backup => Path.Combine(_root, "backup");
        public EvidenceRoots()
        {
            Directory.CreateDirectory(_root);
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            foreach (var path in new[] { Primary, Backup })
            {
                var acl = new System.Security.AccessControl.DirectorySecurity();
                acl.SetOwner(identity.User!); acl.SetAccessRuleProtection(true, false);
                acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(identity.User!, System.Security.AccessControl.FileSystemRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));
                acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid, null), System.Security.AccessControl.FileSystemRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));
                Directory.CreateDirectory(path, acl);
            }
        }
        public void Dispose() => Directory.Delete(_root, true);
    }
}
