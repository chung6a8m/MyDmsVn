using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Testing.Sql;
using MyDmsVn.WebOperation.Operator;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebOperationOperatorDecisionTests
{
    private const string Epoch = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
    private const string Operation = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
    private const string Decision = "dddddddd-dddd-4ddd-8ddd-dddddddddddd";
    private const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Confirmation = "record-not-executed|environment=owned-test|database=MyDmsVn_Test_Operator|epoch=" + Epoch +
        "|actor=42|operation=" + Operation + "|kind=1|target=NONE|fingerprint=" + Fingerprint +
        "|decision=" + Decision + "|case=CASE-42|reason=ORIGINAL_NOT_EXECUTED|evidence=case://CASE-42/evidence-1";

    [Theory]
    [InlineData("record-not-executed", "--help")]
    public void Decision_help_is_an_explicit_sql_free_parse_result(params string[] args)
    {
        Assert.True(RecordNotExecutedOptions.TryParse(args, out var options, out var error));
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.True(options!.HelpRequested);
    }

    [Fact]
    public void Exact_dry_run_arguments_are_strictly_parsed()
    {
        Assert.True(RecordNotExecutedOptions.TryParse(DryRun(), out var options, out var error));
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.True(options!.DryRun);
        Assert.False(options.Execute);
        Assert.Equal(WebOperationKind.SalesCreate, options.Kind);
        Assert.Null(options.RequestTargetId);
        Assert.Equal(Guid.Parse(Decision), options.DecisionId);
        Assert.Equal(Confirmation, options.ConfirmationText);
    }

    [Fact]
    public void Exact_execute_confirmation_is_bound_to_every_decision_field()
    {
        Assert.True(RecordNotExecutedOptions.TryParse(Execute(), out var options, out var error));
        Assert.Null(error);
        Assert.True(options!.Execute);
        Assert.False(options.DryRun);
        Assert.Equal(Confirmation, options.ConfirmationText);

        foreach (var mutation in ConfirmationMutations())
        {
            var args = Replace(Execute(), mutation.Name, mutation.Value);
            Assert.False(RecordNotExecutedOptions.TryParse(args, out _, out var mutationError));
            Assert.Contains("confirmation", mutationError!, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public void Invalid_or_ambiguous_decision_arguments_fail_before_sql(string[] args)
    {
        Assert.False(RecordNotExecutedOptions.TryParse(args, out var options, out var error));
        Assert.Null(options);
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.DoesNotContain(Fingerprint, error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_confirmation_does_not_construct_a_SQL_client()
    {
        var args = Replace(Execute(), "--confirm", "wrong");
        var exit = await OperatorDecisionApplication.RunAsync(args,
            _ => throw new InvalidOperationException("client must not be constructed"),
            new StringWriter(), new StringWriter(), CancellationToken.None);
        Assert.Equal((int)OperatorExitCode.InvalidArguments, exit);
    }

    [Fact]
    public async Task Decision_help_does_not_construct_a_SQL_client()
    {
        var stdout = new StringWriter();
        var exit = await OperatorDecisionApplication.RunAsync(new[] { "record-not-executed", "--help" },
            _ => throw new InvalidOperationException("client must not be constructed"), stdout, new StringWriter(), CancellationToken.None);
        Assert.Equal((int)OperatorExitCode.Completed, exit);
        Assert.Contains("record-not-executed --dry-run", stdout.ToString());
    }

    [Fact]
    public async Task Dry_run_reports_only_bounded_indeterminate_preview()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var client = new FakeDecisionClient(OperatorDecisionResult.Preview(Inspection("INDETERMINATE")));
        var exit = await OperatorDecisionApplication.RunAsync(DryRun(), _ => client, stdout, stderr, CancellationToken.None);
        Assert.Equal((int)OperatorExitCode.Preview, exit);
        Assert.Equal(1, client.DryRunCalls);
        Assert.Equal(0, client.ExecuteCalls);
        Assert.Contains("status=PREVIEW", stdout.ToString());
        Assert.Contains("durableDisposition=INDETERMINATE", stdout.ToString());
        Assert.Contains("notProofOfNotExecuted=true", stdout.ToString());
        Assert.DoesNotContain(Fingerprint, stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact]
    public async Task Execute_reports_success_only_for_verified_cancelled_decision()
    {
        var stdout = new StringWriter();
        var client = new FakeDecisionClient(OperatorDecisionResult.Completed(Inspection("CANCELLED")));
        var exit = await OperatorDecisionApplication.RunAsync(Execute(), _ => client, stdout, new StringWriter(), CancellationToken.None);
        Assert.Equal((int)OperatorExitCode.Completed, exit);
        Assert.Equal(0, client.DryRunCalls);
        Assert.Equal(1, client.ExecuteCalls);
        Assert.Contains("status=CANCELLED", stdout.ToString());
        Assert.Contains("postCommitVerification=matched", stdout.ToString());
        Assert.DoesNotContain(Fingerprint, stdout.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ambiguous_commit_is_unknown_and_never_retried()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var client = new FakeDecisionClient(OperatorDecisionResult.Unknown());
        var exit = await OperatorDecisionApplication.RunAsync(Execute(), _ => client, stdout, stderr, CancellationToken.None);
        Assert.Equal((int)OperatorExitCode.Unknown, exit);
        Assert.Equal(1, client.ExecuteCalls);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Contains("UNKNOWN/ESCALATE", stderr.ToString());
        Assert.Contains("inspect", stderr.ToString());
        Assert.DoesNotContain(Fingerprint, stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Auth.Forbidden", 3)]
    [InlineData("Deployment.EpochMismatch", 3)]
    [InlineData("Idempotency.FingerprintMismatch", 3)]
    [InlineData("Operation.ResolutionConflict", 3)]
    [InlineData("Operation.FenceUnavailable", 6)]
    [InlineData("Operation.Unavailable", 5)]
    public async Task Decision_failures_are_sanitized(string code, int expectedExit)
    {
        var stderr = new StringWriter();
        var exit = await OperatorDecisionApplication.RunAsync(DryRun(),
            _ => new FakeDecisionClient(new WebOperationException(code)), new StringWriter(), stderr, CancellationToken.None);
        Assert.Equal(expectedExit, exit);
        Assert.DoesNotContain(Fingerprint, stderr.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-payload", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Timeout_cancellation_and_environment_failure_are_distinct_and_sanitized()
    {
        Assert.Equal((int)OperatorExitCode.Timeout, await OperatorDecisionApplication.RunAsync(DryRun(),
            _ => new FakeDecisionClient(new TimeoutException("private-payload")), new StringWriter(), new StringWriter(), CancellationToken.None));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Equal((int)OperatorExitCode.Cancelled, await OperatorDecisionApplication.RunAsync(DryRun(),
            _ => new FakeDecisionClient(waitForCancellation: true), new StringWriter(), new StringWriter(), cancelled.Token));
        Assert.Equal((int)OperatorExitCode.EnvironmentFailure, await OperatorDecisionApplication.RunAsync(DryRun(),
            _ => new FakeDecisionClient(new InvalidOperationException("Server=secret;Password=private-payload")),
            new StringWriter(), new StringWriter(), CancellationToken.None));
    }

    [Fact]
    public void Existing_inspect_exit_codes_remain_unchanged()
    {
        Assert.Equal(0, (int)OperatorExitCode.Completed);
        Assert.Equal(2, (int)OperatorExitCode.InvalidArguments);
        Assert.Equal(3, (int)OperatorExitCode.Denied);
        Assert.Equal(4, (int)OperatorExitCode.Indeterminate);
        Assert.Equal(5, (int)OperatorExitCode.EnvironmentFailure);
        Assert.Equal(6, (int)OperatorExitCode.Timeout);
        Assert.Equal(130, (int)OperatorExitCode.Cancelled);
        Assert.Equal(7, (int)OperatorExitCode.Preview);
        Assert.Equal(8, (int)OperatorExitCode.Unknown);
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Guarded_dry_run_is_byte_equal_and_execute_commits_then_independently_verifies() =>
        WebOperationResolutionScenario.Owned(async s =>
        {
            await s.Observer.ExecuteAsync("CREATE USER Web6DecisionOperator WITHOUT LOGIN; ALTER ROLE MyDmsVnWebOperationOperatorRole ADD MEMBER Web6DecisionOperator;");
            var operation = WebOperationResolutionScenario.Operation(s, WebOperationKind.SalesCreate);
            var dryOptions = Parse(DryRunArguments(s, operation));
            var factory = new ImpersonatingDecisionConnectionFactory(s.Database.ConnectionString, "Web6DecisionOperator");
            var client = new SqlOperatorDecisionClient(factory);
            var before = await SqlSnapshot(s.Observer);

            var preview = await client.DryRunAsync(dryOptions, s.Stop.Token);

            Assert.Equal(OperatorDecisionOutcome.Preview, preview.Outcome);
            Assert.Equal("INDETERMINATE", preview.Inspection!.Disposition);
            Assert.Equal(before, await SqlSnapshot(s.Observer));

            var executeOptions = Parse(ExecuteArguments(s, operation));
            var completed = await client.ExecuteAsync(executeOptions, s.Stop.Token);

            Assert.Equal(1, await s.Observer.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@operation AND Disposition='CANCELLED';",
                new { actor = s.Actor, operation = operation.OperationId }));
            Assert.Equal(1, await s.Observer.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit WHERE ActorUserId=@actor AND OperationId=@operation AND DecisionId=@decision AND CaseId='CASE-42' AND ReasonCode='ORIGINAL_NOT_EXECUTED' AND EvidenceReference='case://CASE-42/evidence-1' AND OperatorDatabaseUser='Web6DecisionOperator';",
                new { actor = s.Actor, operation = operation.OperationId, decision = executeOptions.DecisionId }));
            Assert.Equal(OperatorDecisionOutcome.Completed, completed.Outcome);
            Assert.Equal("CANCELLED", completed.Inspection!.Disposition);
            Assert.Equal(executeOptions.DecisionId, completed.Inspection.Terminal!.DecisionId);
            Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SalesReceipts;"));
            Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.GoodsReceipts;"));
            Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger;"));
            Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockValuationEntries;"));
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Existing_disposition_wrong_target_and_wrong_authority_fail_closed_without_DML() =>
        WebOperationResolutionScenario.Owned(async s =>
        {
            await s.Observer.ExecuteAsync(@"CREATE USER Web6DecisionOperator WITHOUT LOGIN;
ALTER ROLE MyDmsVnWebOperationOperatorRole ADD MEMBER Web6DecisionOperator;
CREATE USER Web6DecisionDenied WITHOUT LOGIN;");
            var operation = WebOperationResolutionScenario.Operation(s, WebOperationKind.SalesCreate);
            await s.Observer.ExecuteAsync("DECLARE @n datetime2(7)=SYSUTCDATETIME(); INSERT dbo.WebOperationJournal VALUES(@actor,@id,@epoch,1,1,1,NULL,@hash,41,'COMMITTED',0x7B7D,@n,@n,DATEADD(day,7,@n));",
                new { actor = s.Actor, id = operation.OperationId, epoch = s.Epoch, hash = operation.Fingerprint });
            var options = Parse(DryRunArguments(s, operation));
            var before = await SqlSnapshot(s.Observer);

            await Assert.ThrowsAsync<WebOperationException>(() => new SqlOperatorDecisionClient(
                new ImpersonatingDecisionConnectionFactory(s.Database.ConnectionString, "Web6DecisionOperator"))
                .DryRunAsync(options, s.Stop.Token));
            await Assert.ThrowsAsync<WebOperationException>(() => new SqlOperatorDecisionClient(
                new ImpersonatingDecisionConnectionFactory(s.Database.ConnectionString, "Web6DecisionDenied"))
                .DryRunAsync(options, s.Stop.Token));

            var wrongTarget = Parse(DryRunArguments(s, operation, database: "AnotherOwnedDatabase"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => new SqlOperatorDecisionClient(
                new OperatorSqlConnectionFactory(s.Database.ConnectionString, "Web6DecisionOperator", "Web6DecisionOperator"))
                .DryRunAsync(wrongTarget, s.Stop.Token));
            Assert.Equal(before, await SqlSnapshot(s.Observer));
        });

    public static IEnumerable<object[]> InvalidArguments()
    {
        yield return new object[] { new[] { "record-not-executed" } };
        yield return new object[] { Append(DryRun(), "--execute") };
        yield return new object[] { Append(DryRun(), "--actor", "42") };
        yield return new object[] { Append(DryRun(), "--unknown", "value") };
        yield return new object[] { Remove(DryRun(), "--database") };
        yield return new object[] { Replace(DryRun(), "--actor", "01") };
        yield return new object[] { Replace(DryRun(), "--epoch", Guid.Empty.ToString("D")) };
        yield return new object[] { Replace(DryRun(), "--operation", "11111111-1111-1111-1111-111111111111") };
        yield return new object[] { Replace(DryRun(), "--decision", "11111111-1111-1111-1111-111111111111") };
        yield return new object[] { Replace(DryRun(), "--kind", "0") };
        yield return new object[] { Replace(DryRun(), "--kind", "7") };
        yield return new object[] { Replace(DryRun(), "--target", "1") };
        yield return new object[] { Replace(WithKindAndTarget(DryRun(), "2", "NONE"), "--target", "NONE") };
        yield return new object[] { Replace(DryRun(), "--fingerprint", "abc") };
        yield return new object[] { Replace(DryRun(), "--case", "bad case") };
        yield return new object[] { Replace(DryRun(), "--reason", "bad/reason") };
        yield return new object[] { Replace(DryRun(), "--evidence", "bad evidence") };
        yield return new object[] { Replace(DryRun(), "--timeout-seconds", "26") };
        yield return new object[] { Append(DryRun(), "--confirm", Confirmation) };
        yield return new object[] { Remove(Execute(), "--confirm") };
    }

    private static IEnumerable<(string Name, string Value)> ConfirmationMutations()
    {
        yield return ("--environment", "other-test");
        yield return ("--database", "MyDmsVn_Test_Other");
        yield return ("--epoch", "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");
        yield return ("--actor", "43");
        yield return ("--operation", "cccccccc-cccc-4ccc-8ccc-cccccccccccc");
        yield return ("--kind", "4");
        yield return ("--fingerprint", new string('a', 64));
        yield return ("--decision", "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");
        yield return ("--case", "CASE-43");
        yield return ("--reason", "OTHER_REASON");
        yield return ("--evidence", "case://CASE-42/evidence-2");
    }

    private static string[] DryRun() => new[]
    {
        "record-not-executed", "--dry-run", "--environment", "owned-test", "--database", "MyDmsVn_Test_Operator",
        "--epoch", Epoch, "--actor", "42", "--operation", Operation, "--kind", "1", "--target", "NONE",
        "--fingerprint", Fingerprint, "--case", "CASE-42", "--reason", "ORIGINAL_NOT_EXECUTED",
        "--evidence", "case://CASE-42/evidence-1", "--decision", Decision, "--timeout-seconds", "15"
    };

    private static string[] Execute() => Append(DryRun().WithReplaced("--dry-run", "--execute"), "--confirm", Confirmation);

    private static RecordNotExecutedOptions Parse(string[] args)
    {
        Assert.True(RecordNotExecutedOptions.TryParse(args, out var options, out var error), error);
        return options!;
    }

    private static string[] DryRunArguments(WebOperationJournalTests.Scenario s, WebOperationContext operation, string? database = null)
    {
        var args = DryRun();
        args = Replace(args, "--database", database ?? s.Database.DatabaseName);
        args = Replace(args, "--epoch", s.Epoch.ToString("D"));
        args = Replace(args, "--actor", s.Actor.ToString());
        args = Replace(args, "--operation", operation.OperationId.ToString("D"));
        args = Replace(args, "--kind", ((byte)operation.Kind).ToString());
        args = Replace(args, "--target", operation.RequestTargetId?.ToString() ?? "NONE");
        return Replace(args, "--fingerprint", Hex(operation.Fingerprint));
    }

    private static string[] ExecuteArguments(WebOperationJournalTests.Scenario s, WebOperationContext operation)
    {
        var dry = Parse(DryRunArguments(s, operation));
        return Append(DryRunArguments(s, operation).WithReplaced("--dry-run", "--execute"), "--confirm", dry.ConfirmationText);
    }

    private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();

    private static async Task<string> SqlSnapshot(SqlConnection sql)
    {
        var values = new List<string>();
        foreach (var source in new[]
        {
            "WebOperationJournal ORDER BY ActorUserId,OperationId", "WebOperationTerminalAudit ORDER BY ActorUserId,OperationId",
            "SalesReceipts ORDER BY SaleId", "SalesReceiptLines ORDER BY SaleLineId", "GoodsReceipts ORDER BY ReceiptId",
            "GoodsReceiptLines ORDER BY ReceiptLineId", "StockBalances ORDER BY WarehouseId,ProductId",
            "StockLedger ORDER BY LedgerId", "StockValuationEntries ORDER BY LedgerId"
        }) values.Add(await sql.QuerySingleAsync<string>("SELECT COALESCE((SELECT * FROM dbo." + source + " FOR JSON PATH,INCLUDE_NULL_VALUES),N'[]');"));
        return string.Join("|", values);
    }

    private static string[] WithKindAndTarget(string[] source, string kind, string target) =>
        Replace(Replace(source, "--kind", kind), "--target", target);

    private static string[] Replace(string[] source, string name, string value)
    {
        var result = (string[])source.Clone();
        var index = Array.IndexOf(result, name);
        Assert.True(index >= 0 && index + 1 < result.Length, "Option not found: " + name);
        result[index + 1] = value;
        return result;
    }

    private static string[] Append(string[] source, params string[] values)
    {
        var result = new string[source.Length + values.Length];
        Array.Copy(source, result, source.Length);
        Array.Copy(values, 0, result, source.Length, values.Length);
        return result;
    }

    private static string[] Remove(string[] source, string name)
    {
        var result = new List<string>(source);
        var index = result.IndexOf(name);
        result.RemoveRange(index, name == "--dry-run" || name == "--execute" ? 1 : 2);
        return result.ToArray();
    }

    private static WebOperationInspection Inspection(string disposition) => new WebOperationInspection(
        Guid.Parse(Epoch), 42, Guid.Parse(Operation), disposition == "INDETERMINATE" ? null : WebOperationKind.SalesCreate,
        null, disposition, false, false, false, disposition == "CANCELLED"
            ? new WebOperationTerminalDecision(Guid.Parse(Decision), "CASE-42", DateTime.UtcNow) : null);

    private sealed class FakeDecisionClient : IOperatorDecisionClient
    {
        private readonly OperatorDecisionResult? _result;
        private readonly Exception? _error;
        private readonly bool _waitForCancellation;
        public FakeDecisionClient(OperatorDecisionResult result) { _result = result; }
        public FakeDecisionClient(Exception error) { _error = error; }
        public FakeDecisionClient(bool waitForCancellation) { _waitForCancellation = waitForCancellation; }
        public int DryRunCalls { get; private set; }
        public int ExecuteCalls { get; private set; }
        public async Task<OperatorDecisionResult> DryRunAsync(RecordNotExecutedOptions options, CancellationToken token)
        {
            DryRunCalls++;
            return await Complete(token);
        }
        public async Task<OperatorDecisionResult> ExecuteAsync(RecordNotExecutedOptions options, CancellationToken token)
        {
            ExecuteCalls++;
            return await Complete(token);
        }
        private async Task<OperatorDecisionResult> Complete(CancellationToken token)
        {
            if (_waitForCancellation) await Task.Delay(Timeout.Infinite, token);
            if (_error != null) throw _error;
            return _result!;
        }
    }

    private sealed class ImpersonatingDecisionConnectionFactory : IOperatorSqlConnectionFactory
    {
        private readonly string _connectionString;
        private readonly string _databaseUser;
        public ImpersonatingDecisionConnectionFactory(string connectionString, string databaseUser)
        {
            _connectionString = connectionString; _databaseUser = databaseUser;
        }
        public async Task<SqlConnection> OpenAsync(IOperatorTargetOptions options, CancellationToken cancellationToken)
        {
            var connection = new SqlConnection(new SqlConnectionStringBuilder(_connectionString) { Pooling = false }.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = "EXECUTE AS USER=N'" + _databaseUser + "';";
            await command.ExecuteNonQueryAsync(cancellationToken);
            return connection;
        }
    }
}

internal static class OperatorDecisionTestArrayExtensions
{
    public static string[] WithReplaced(this string[] source, string oldValue, string newValue)
    {
        var result = (string[])source.Clone();
        result[Array.IndexOf(result, oldValue)] = newValue;
        return result;
    }
}
