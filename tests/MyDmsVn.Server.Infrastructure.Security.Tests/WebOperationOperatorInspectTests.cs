using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using MyDmsVn.WebOperation.Operator;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebOperationOperatorInspectTests
{
    private const string Epoch = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
    private const string Operation = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
    private const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData("--help")]
    [InlineData("inspect", "--help")]
    public void Help_is_an_explicit_sql_free_parse_result(params string[] args)
    {
        Assert.True(InspectOptions.TryParse(args, out var options, out var error));
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.True(options!.HelpRequested);
    }

    [Fact]
    public void Exact_inspect_arguments_are_strictly_parsed()
    {
        Assert.True(InspectOptions.TryParse(Valid(), out var options, out var error));
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.False(options!.HelpRequested);
        Assert.Equal("owned-test", options.EnvironmentName);
        Assert.Equal("MyDmsVn_Test_Operator", options.DatabaseName);
        Assert.Equal(Guid.Parse(Epoch), options.DeploymentEpoch);
        Assert.Equal(42, options.ActorUserId);
        Assert.Equal(Guid.Parse(Operation), options.OperationId);
        Assert.Equal(Fingerprint, options.FingerprintHex);
        Assert.Equal(15, options.TimeoutSeconds);
    }

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public void Invalid_or_ambiguous_arguments_fail_before_sql(string[] args)
    {
        Assert.False(InspectOptions.TryParse(args, out var options, out var error));
        Assert.Null(options);
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.DoesNotContain("Server=", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Exit_codes_are_stable_and_distinct()
    {
        Assert.Equal(0, (int)OperatorExitCode.Completed);
        Assert.Equal(2, (int)OperatorExitCode.InvalidArguments);
        Assert.Equal(3, (int)OperatorExitCode.Denied);
        Assert.Equal(4, (int)OperatorExitCode.Indeterminate);
        Assert.Equal(5, (int)OperatorExitCode.EnvironmentFailure);
        Assert.Equal(6, (int)OperatorExitCode.Timeout);
        Assert.Equal(130, (int)OperatorExitCode.Cancelled);
    }

    [Fact]
    public async Task Help_does_not_construct_a_SQL_client()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await OperatorInspectApplication.RunAsync(new[] { "--help" },
            _ => throw new InvalidOperationException("client must not be created"), stdout, stderr, CancellationToken.None);
        Assert.Equal((int)OperatorExitCode.Completed, exit);
        Assert.Contains("inspect --environment", stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Theory]
    [InlineData("COMMITTED", 0)]
    [InlineData("EXPIRED", 0)]
    [InlineData("CANCELLED", 0)]
    [InlineData("INDETERMINATE", 4)]
    public async Task Status_output_is_bounded_and_exit_code_is_stable(string disposition, int expectedExit)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var result = Inspection(disposition);
        var exit = await OperatorInspectApplication.RunAsync(Valid(), _ => new FakeClient(result), stdout, stderr, CancellationToken.None);
        Assert.Equal(expectedExit, exit);
        Assert.Contains("status=" + disposition, stdout.ToString());
        Assert.Contains("environmentLabel=owned-test", stdout.ToString());
        Assert.Contains("environmentLabelSource=operator-supplied", stdout.ToString());
        Assert.Contains("database=MyDmsVn_Test_Operator", stdout.ToString());
        Assert.DoesNotContain(Fingerprint, stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-payload", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Theory]
    [InlineData("Auth.Forbidden", 3)]
    [InlineData("Deployment.EpochMismatch", 3)]
    [InlineData("Idempotency.FingerprintMismatch", 3)]
    [InlineData("Operation.Unavailable", 5)]
    public async Task Operation_failures_are_sanitized(string code, int expectedExit)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await OperatorInspectApplication.RunAsync(Valid(), _ => new FakeClient(new WebOperationException(code)), stdout, stderr, CancellationToken.None);
        Assert.Equal(expectedExit, exit);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.DoesNotContain(Fingerprint, stderr.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-payload", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Failed_rollback_or_disposal_is_an_environment_failure_without_secret_output()
    {
        var stderr = new StringWriter();
        var exit = await OperatorInspectApplication.RunAsync(Valid(),
            _ => new FakeClient(new InvalidOperationException("Server=secret;Password=private-payload")),
            new StringWriter(), stderr, CancellationToken.None);
        Assert.Equal((int)OperatorExitCode.EnvironmentFailure, exit);
        Assert.DoesNotContain("Server=", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-payload", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Caller_cancellation_is_distinct_from_timeout()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var callerExit = await OperatorInspectApplication.RunAsync(Valid(), _ => new FakeClient(waitForCancellation: true),
            new StringWriter(), new StringWriter(), cancelled.Token);
        Assert.Equal((int)OperatorExitCode.Cancelled, callerExit);

        var timeoutArgs = Replace("--timeout-seconds", "1");
        var timeoutExit = await OperatorInspectApplication.RunAsync(timeoutArgs, _ => new FakeClient(waitForCancellation: true),
            new StringWriter(), new StringWriter(), CancellationToken.None);
        Assert.Equal((int)OperatorExitCode.Timeout, timeoutExit);
    }

    [Fact]
    public async Task Explicit_timeout_exception_is_reported_as_timeout()
    {
        var exit = await OperatorInspectApplication.RunAsync(Valid(),
            _ => new FakeClient(new TimeoutException("private SQL timeout")),
            new StringWriter(), new StringWriter(), CancellationToken.None);
        Assert.Equal((int)OperatorExitCode.Timeout, exit);
    }

    [Fact]
    public async Task Hard_process_deadline_invokes_termination_once()
    {
        var expirations = 0;
        using (new OperatorProcessDeadline(TimeSpan.FromMilliseconds(50), () => Interlocked.Increment(ref expirations)))
            await Task.Delay(250);
        Assert.Equal(1, expirations);
    }

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Actual_SQL_command_timeout_minus_two_maps_to_timeout_without_DML() => WebOperationResolutionScenario.Owned(async s =>
    {
        var operation = s.Operation(hash: Enumerable.Repeat((byte)9, 32).ToArray());
        var before = await SqlSnapshot(s.Observer);
        var exit = await OperatorInspectApplication.RunAsync(Arguments(s, operation, timeoutSeconds: 5),
            _ => new RawSqlTimeoutClient(s.Database.ConnectionString), new StringWriter(), new StringWriter(), s.Stop.Token);
        Assert.Equal((int)OperatorExitCode.Timeout, exit);
        Assert.Equal(before, await SqlSnapshot(s.Observer));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Restricted_operator_inspects_all_states_without_changing_SQL() => WebOperationResolutionScenario.Owned(async s =>
    {
        await s.Observer.ExecuteAsync("CREATE USER Web6CliOperator WITHOUT LOGIN; ALTER ROLE MyDmsVnWebOperationOperatorRole ADD MEMBER Web6CliOperator;");
        var committed = s.Operation(hash: Enumerable.Repeat((byte)1, 32).ToArray());
        var expired = s.Operation(hash: Enumerable.Repeat((byte)2, 32).ToArray());
        var cancelled = s.Operation(hash: Enumerable.Repeat((byte)3, 32).ToArray());
        var missing = s.Operation(hash: Enumerable.Repeat((byte)4, 32).ToArray());
        await s.Observer.ExecuteAsync(@"DECLARE @n datetime2(7)=SYSUTCDATETIME();
INSERT dbo.WebOperationJournal VALUES(@actor,@committed,@epoch,1,1,1,NULL,@committedHash,41,'COMMITTED',@bytes,@n,@n,DATEADD(day,7,@n));
SET @n=DATEADD(day,-8,SYSUTCDATETIME());
INSERT dbo.WebOperationJournal VALUES(@actor,@expired,@epoch,1,1,1,NULL,@expiredHash,42,'COMMITTED',@bytes,@n,@n,DATEADD(day,7,@n));",
            new { actor = s.Actor, committed = committed.OperationId, expired = expired.OperationId, epoch = s.Epoch,
                committedHash = committed.Fingerprint, expiredHash = expired.Fingerprint, bytes = Encoding.UTF8.GetBytes("{\"data\":{}}") });
        var terminal = await s.Transaction();
        await WebOperationResolutionScenario.Record(s, terminal, cancelled);
        terminal.Tx.Commit();
        var before = await SqlSnapshot(s.Observer);
        var factory = new ImpersonatingConnectionFactory(s.Database.ConnectionString, "Web6CliOperator");
        foreach (var item in new[]
        {
            (Operation: committed, Status: "COMMITTED", Exit: 0),
            (Operation: expired, Status: "EXPIRED", Exit: 0),
            (Operation: cancelled, Status: "CANCELLED", Exit: 0),
            (Operation: missing, Status: "INDETERMINATE", Exit: 4)
        })
        {
            var stdout = new StringWriter();
            var exit = await OperatorInspectApplication.RunAsync(Arguments(s, item.Operation),
                _ => new SqlOperatorInspectionClient(factory), stdout, new StringWriter(), s.Stop.Token);
            Assert.Equal(item.Exit, exit);
            Assert.Contains("status=" + item.Status, stdout.ToString());
        }
        Assert.Equal(before, await SqlSnapshot(s.Observer));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task SQL_target_and_authority_fail_closed_without_disclosure() => WebOperationResolutionScenario.Owned(async s =>
    {
        await s.Observer.ExecuteAsync(@"CREATE USER Web6CliOperator WITHOUT LOGIN;
ALTER ROLE MyDmsVnWebOperationOperatorRole ADD MEMBER Web6CliOperator;
CREATE USER Web6CliRuntime WITHOUT LOGIN;");
        var operation = s.Operation(hash: Enumerable.Repeat((byte)5, 32).ToArray());
        await s.Observer.ExecuteAsync("DECLARE @n datetime2(7)=SYSUTCDATETIME(); INSERT dbo.WebOperationJournal VALUES(@actor,@id,@epoch,1,1,1,NULL,@hash,41,'COMMITTED',@bytes,@n,@n,DATEADD(day,7,@n));",
            new { actor = s.Actor, id = operation.OperationId, epoch = s.Epoch, hash = operation.Fingerprint, bytes = Encoding.UTF8.GetBytes("{\"data\":{}}") });
        var before = await SqlSnapshot(s.Observer);

        Assert.Equal((int)OperatorExitCode.EnvironmentFailure, await RunSql(Arguments(s, operation, database: "AnotherOwnedDatabase"),
            new OperatorSqlConnectionFactory(s.Database.ConnectionString, "unused", "unused")));
        Assert.Equal((int)OperatorExitCode.Denied, await RunSql(Arguments(s, operation),
            new ImpersonatingConnectionFactory(s.Database.ConnectionString, "Web6CliRuntime")));
        Assert.Equal((int)OperatorExitCode.Denied, await RunSql(Arguments(s, operation, epoch: Guid.NewGuid()),
            new ImpersonatingConnectionFactory(s.Database.ConnectionString, "Web6CliOperator")));
        Assert.Equal((int)OperatorExitCode.Denied, await RunSql(Arguments(s, operation, fingerprint: new byte[32]),
            new ImpersonatingConnectionFactory(s.Database.ConnectionString, "Web6CliOperator")));
        Assert.Equal((int)OperatorExitCode.Indeterminate, await RunSql(Arguments(s, operation, actor: s.Actor + 100),
            new ImpersonatingConnectionFactory(s.Database.ConnectionString, "Web6CliOperator")));
        Assert.Equal(before, await SqlSnapshot(s.Observer));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Matching_but_elevated_SQL_principal_is_denied_before_inspection() => WebOperationResolutionScenario.Owned(async s =>
    {
        var user = await s.Observer.QuerySingleAsync<string>("SELECT USER_NAME();");
        var login = await s.Observer.QuerySingleAsync<string>("SELECT ORIGINAL_LOGIN();");
        var operation = s.Operation(hash: Enumerable.Repeat((byte)7, 32).ToArray());
        var before = await SqlSnapshot(s.Observer);
        var exit = await RunSql(Arguments(s, operation),
            new OperatorSqlConnectionFactory(s.Database.ConnectionString, user, login));
        Assert.Equal((int)OperatorExitCode.Denied, exit);
        Assert.Equal(before, await SqlSnapshot(s.Observer));
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task SQL_lock_timeout_is_bounded_and_read_only() => WebOperationResolutionScenario.Owned(async s =>
    {
        await s.Observer.ExecuteAsync("CREATE USER Web6CliOperator WITHOUT LOGIN; ALTER ROLE MyDmsVnWebOperationOperatorRole ADD MEMBER Web6CliOperator;");
        var operation = s.Operation(hash: Enumerable.Repeat((byte)6, 32).ToArray());
        var before = await SqlSnapshot(s.Observer);
        var holder = await s.Transaction();
        var resource = "MyDmsVn.WebOperation:" + s.Actor + ":" + operation.OperationId.ToString("N");
        Assert.True(await holder.Sql.QuerySingleAsync<int>(new SqlTestCommand(
            "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=0; SELECT @r;",
            new { resource }, transaction: holder.Tx, cancellationToken: s.Stop.Token)) >= 0);
        var args = Arguments(s, operation, timeoutSeconds: 1);
        var exit = await RunSql(args, new ImpersonatingConnectionFactory(s.Database.ConnectionString, "Web6CliOperator"));
        Assert.Equal((int)OperatorExitCode.Timeout, exit);
        holder.Tx.Rollback();
        Assert.Equal(before, await SqlSnapshot(s.Observer));
    });

    [Fact]
    public async Task Unavailable_SQL_endpoint_returns_environment_failure_without_secret_output()
    {
        var args = Valid();
        var connection = "Data Source=127.0.0.1,1;Initial Catalog=MyDmsVn_Test_Operator;Integrated Security=True;Encrypt=False;Pooling=False;Connect Timeout=1";
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = await OperatorInspectApplication.RunAsync(args,
            _ => new SqlOperatorInspectionClient(new OperatorSqlConnectionFactory(connection, "Web6CliOperator", "Web6CliOperator")), stdout, stderr, CancellationToken.None);
        Assert.Equal((int)OperatorExitCode.EnvironmentFailure, exit);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.DoesNotContain("Data Source", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Release_executable_uses_a_real_restricted_login_and_never_changes_SQL() => WebOperationResolutionScenario.Owned(async s =>
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "MyDmsVn.WebOperation.Operator.exe");
        Assert.True(File.Exists(executable), "The referenced Release CLI executable must be present in the test output.");
        var login = new OwnedOperatorLogin(s.Database.ConnectionString);
        Exception? failure = null;
        try
        {
            await login.CreateAsync();
            await s.Observer.ExecuteAsync("CREATE USER [" + login.Name + "] FOR LOGIN [" + login.Name + "]; " +
                "ALTER ROLE MyDmsVnWebOperationOperatorRole ADD MEMBER [" + login.Name + "]; ");
            var operation = s.Operation(hash: Enumerable.Repeat((byte)8, 32).ToArray());
            await s.Observer.ExecuteAsync("DECLARE @n datetime2(7)=SYSUTCDATETIME(); INSERT dbo.WebOperationJournal VALUES(@actor,@id,@epoch,1,1,1,NULL,@hash,41,'COMMITTED',@bytes,@n,@n,DATEADD(day,7,@n));",
                new { actor = s.Actor, id = operation.OperationId, epoch = s.Epoch, hash = operation.Fingerprint,
                    bytes = Encoding.UTF8.GetBytes("{\"data\":{\"secret\":\"private-payload\"}}") });
            var before = await SqlSnapshot(s.Observer);
            var arguments = Arguments(s, operation, timeoutSeconds: 5);

            var allowed = await RunExecutable(executable, arguments, login.ConnectionString(s.Database.DatabaseName), login.Name, login.Name);
            Assert.Equal((int)OperatorExitCode.Completed, allowed.ExitCode);
            Assert.Contains("status=COMMITTED", allowed.Stdout);
            Assert.Contains("environmentLabelSource=operator-supplied", allowed.Stdout);
            Assert.True(login.IsSecretFree(allowed.Stdout + allowed.Stderr));
            Assert.DoesNotContain("private-payload", allowed.Stdout + allowed.Stderr, StringComparison.OrdinalIgnoreCase);

            var blocked = s.Operation(hash: Enumerable.Repeat((byte)10, 32).ToArray());
            var holder = await s.Transaction();
            var resource = "MyDmsVn.WebOperation:" + s.Actor + ":" + blocked.OperationId.ToString("N");
            Assert.True(await holder.Sql.QuerySingleAsync<int>(new SqlTestCommand(
                "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=0; SELECT @r;",
                new { resource }, transaction: holder.Tx, cancellationToken: s.Stop.Token)) >= 0);
            var bounded = Stopwatch.StartNew();
            Assert.Equal((int)OperatorExitCode.Timeout,
                (await RunExecutable(executable, Arguments(s, blocked, timeoutSeconds: 1),
                    login.ConnectionString(s.Database.DatabaseName), login.Name, login.Name)).ExitCode);
            Assert.True(bounded.Elapsed < TimeSpan.FromSeconds(4), "The executable process deadline was not bounded.");
            holder.Tx.Rollback();

            await s.Observer.ExecuteAsync("ALTER ROLE MyDmsVnWebOperationOperatorRole DROP MEMBER [" + login.Name + "]; ");
            Assert.Equal((int)OperatorExitCode.Denied,
                (await RunExecutable(executable, arguments, login.ConnectionString(s.Database.DatabaseName), login.Name, login.Name)).ExitCode);

            await s.Observer.ExecuteAsync("ALTER ROLE MyDmsVnWebOperationOperatorRole ADD MEMBER [" + login.Name + "]; " +
                "ALTER ROLE db_datareader ADD MEMBER [" + login.Name + "]; ");
            Assert.Equal((int)OperatorExitCode.Denied,
                (await RunExecutable(executable, arguments, login.ConnectionString(s.Database.DatabaseName), login.Name, login.Name)).ExitCode);
            await s.Observer.ExecuteAsync("ALTER ROLE db_datareader DROP MEMBER [" + login.Name + "]; " +
                "ALTER ROLE db_owner ADD MEMBER [" + login.Name + "]; ");
            Assert.Equal((int)OperatorExitCode.Denied,
                (await RunExecutable(executable, arguments, login.ConnectionString(s.Database.DatabaseName), login.Name, login.Name)).ExitCode);
            await s.Observer.ExecuteAsync("ALTER ROLE db_owner DROP MEMBER [" + login.Name + "]; ");

            await s.Observer.ExecuteAsync("ALTER ROLE db_datareader ADD MEMBER MyDmsVnWebOperationOperatorRole;");
            Assert.Equal((int)OperatorExitCode.Denied,
                (await RunExecutable(executable, arguments, login.ConnectionString(s.Database.DatabaseName), login.Name, login.Name)).ExitCode);
            await s.Observer.ExecuteAsync("ALTER ROLE db_datareader DROP MEMBER MyDmsVnWebOperationOperatorRole; " +
                "GRANT INSERT ON dbo.WebOperationJournal TO MyDmsVnWebOperationOperatorRole;");
            Assert.Equal((int)OperatorExitCode.Denied,
                (await RunExecutable(executable, arguments, login.ConnectionString(s.Database.DatabaseName), login.Name, login.Name)).ExitCode);
            await s.Observer.ExecuteAsync("REVOKE INSERT ON dbo.WebOperationJournal FROM MyDmsVnWebOperationOperatorRole; " +
                "GRANT INSERT ON dbo.WebOperationJournal TO public;");
            Assert.Equal((int)OperatorExitCode.Denied,
                (await RunExecutable(executable, arguments, login.ConnectionString(s.Database.DatabaseName), login.Name, login.Name)).ExitCode);
            await s.Observer.ExecuteAsync("REVOKE INSERT ON dbo.WebOperationJournal FROM public;");

            Assert.Equal((int)OperatorExitCode.EnvironmentFailure,
                (await RunExecutable(executable, Replace(arguments, "--database", "AnotherOwnedDatabase"),
                    login.ConnectionString(s.Database.DatabaseName), login.Name, login.Name)).ExitCode);
            Assert.Equal(before, await SqlSnapshot(s.Observer));
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try { await login.CleanupAsync(); }
            catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); }
        }
    });

    public static IEnumerable<object[]> InvalidArguments()
    {
        yield return new object[] { Array.Empty<string>() };
        yield return new object[] { new[] { "unknown" } };
        yield return new object[] { Replace("--actor", "0") };
        yield return new object[] { Replace("--actor", "01") };
        yield return new object[] { Replace("--epoch", Guid.Empty.ToString("D")) };
        yield return new object[] { Replace("--operation", Guid.NewGuid().ToString("B")) };
        yield return new object[] { Replace("--operation", "11111111-1111-1111-1111-111111111111") };
        yield return new object[] { Replace("--fingerprint", "abc") };
        yield return new object[] { Replace("--environment", "unsafe value") };
        yield return new object[] { Replace("--database", "master") };
        yield return new object[] { Replace("--timeout-seconds", "0") };
        yield return new object[] { Replace("--timeout-seconds", "26") };
        yield return new object[] { Replace("--timeout-seconds", "61") };
        yield return new object[] { Append(Valid(), "--actor", "42") };
        yield return new object[] { Append(Valid(), "--unknown", "value") };
        yield return new object[] { Remove(Valid(), "--database") };
    }

    private static string[] Valid() => new[]
    {
        "inspect", "--environment", "owned-test", "--database", "MyDmsVn_Test_Operator",
        "--epoch", Epoch, "--actor", "42", "--operation", Operation,
        "--fingerprint", Fingerprint, "--timeout-seconds", "15"
    };

    private static string[] Replace(string name, string value)
    {
        return Replace(Valid(), name, value);
    }

    private static string[] Replace(string[] source, string name, string value)
    {
        var args = (string[])source.Clone();
        args[Array.IndexOf(args, name) + 1] = value;
        return args;
    }

    private static string[] Append(string[] args, params string[] values)
    {
        var result = new string[args.Length + values.Length];
        Array.Copy(args, result, args.Length);
        Array.Copy(values, 0, result, args.Length, values.Length);
        return result;
    }

    private static string[] Remove(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        var result = new List<string>(args);
        result.RemoveRange(index, 2);
        return result.ToArray();
    }

    private static WebOperationInspection Inspection(string disposition) => new WebOperationInspection(
        Guid.Parse(Epoch), 42, Guid.Parse(Operation),
        disposition == "INDETERMINATE" ? (WebOperationKind?)null : WebOperationKind.SalesCreate,
        null, disposition, disposition == "COMMITTED", disposition == "COMMITTED", disposition == "COMMITTED", null);

    private static string[] Arguments(WebOperationJournalTests.Scenario s, WebOperationContext operation,
        string? database = null, Guid? epoch = null, int? actor = null, byte[]? fingerprint = null, int timeoutSeconds = 15) => new[]
    {
        "inspect", "--environment", "owned-test", "--database", database ?? s.Database.DatabaseName,
        "--epoch", (epoch ?? s.Epoch).ToString("D"), "--actor", (actor ?? s.Actor).ToString(),
        "--operation", operation.OperationId.ToString("D"), "--fingerprint", Hex(fingerprint ?? operation.Fingerprint),
        "--timeout-seconds", timeoutSeconds.ToString()
    };

    private static string Hex(byte[] bytes) => string.Concat(bytes.Select(value => value.ToString("x2")));

    private static async Task<int> RunSql(string[] args, IOperatorSqlConnectionFactory factory) =>
        await OperatorInspectApplication.RunAsync(args, _ => new SqlOperatorInspectionClient(factory),
            new StringWriter(), new StringWriter(), CancellationToken.None);

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

    private static async Task<ExecutableResult> RunExecutable(string executable, string[] arguments, string connectionString,
        string expectedDatabaseUser, string expectedLogin)
    {
        var start = new ProcessStartInfo(executable, string.Join(" ", arguments.Select(Quote)))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.EnvironmentVariables[OperatorInspectApplication.ConnectionStringEnvironmentVariable] = connectionString;
        start.EnvironmentVariables[OperatorInspectApplication.DatabaseUserEnvironmentVariable] = expectedDatabaseUser;
        start.EnvironmentVariables[OperatorInspectApplication.LoginEnvironmentVariable] = expectedLogin;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Operator process did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(15000))
        {
            process.Kill();
            process.WaitForExit();
            throw new TimeoutException("Operator process did not exit within the owned timeout.");
        }
        return new ExecutableResult(process.ExitCode, await stdout, await stderr);
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private sealed class ExecutableResult
    {
        public ExecutableResult(int exitCode, string stdout, string stderr)
        {
            ExitCode = exitCode; Stdout = stdout; Stderr = stderr;
        }
        public int ExitCode { get; }
        public string Stdout { get; }
        public string Stderr { get; }
    }

    private sealed class OwnedOperatorLogin
    {
        private readonly string _admin;
        private readonly byte[] _sid = Guid.NewGuid().ToByteArray();
        private readonly string _password = "Web6!" + Guid.NewGuid().ToString("N");
        private string? _serverIdentity;
        private bool _creationAttempted;
        public OwnedOperatorLogin(string admin) { _admin = admin; }
        public string Name { get; } = "WEB6_Cli_" + Guid.NewGuid().ToString("N");
        public string ConnectionString(string database) => new SqlConnectionStringBuilder(_admin)
        {
            InitialCatalog = database,
            IntegratedSecurity = false,
            Authentication = SqlAuthenticationMethod.NotSpecified,
            UserID = Name,
            Password = _password,
            Pooling = false,
        }.ConnectionString;
        public bool IsSecretFree(string output) => output.IndexOf(_password, StringComparison.Ordinal) < 0;
        public async Task CreateAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var connection = new SqlConnection(_admin);
            await connection.OpenAsync(timeout.Token);
            using var command = connection.CreateCommand();
            command.CommandTimeout = 30;
            command.CommandText = "SELECT CONVERT(nvarchar(256),SERVERPROPERTY(N'ServerName'));";
            _serverIdentity = (string)await command.ExecuteScalarAsync(timeout.Token);
            if (string.IsNullOrWhiteSpace(_serverIdentity)) throw new InvalidOperationException("Test login instance identity is missing.");
            command.CommandText = "CREATE LOGIN [" + Name + "] WITH PASSWORD=N'" + _password + "', SID=0x" +
                BitConverter.ToString(_sid).Replace("-", string.Empty) + ", CHECK_POLICY=OFF;";
            _creationAttempted = true;
            await command.ExecuteNonQueryAsync(timeout.Token);
        }
        public async Task CleanupAsync()
        {
            if (!_creationAttempted) return;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var connection = new SqlConnection(_admin);
            await connection.OpenAsync(timeout.Token);
            using var command = connection.CreateCommand();
            command.CommandTimeout = 30;
            command.CommandText = "SELECT CONVERT(nvarchar(256),SERVERPROPERTY(N'ServerName'));";
            if (!string.Equals(_serverIdentity, (string)await command.ExecuteScalarAsync(timeout.Token), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test login instance changed; retain the exact owned login.");
            command.Parameters.AddWithValue("@name", Name);
            command.Parameters.AddWithValue("@sid", _sid);
            command.CommandText = "IF EXISTS(SELECT 1 FROM sys.server_principals WHERE name=@name) BEGIN " +
                "IF NOT EXISTS(SELECT 1 FROM sys.server_principals WHERE name=@name AND sid=@sid AND type='S') THROW 51998,'Test login identity changed.',1; " +
                "IF EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE original_security_id=@sid OR security_id=@sid) THROW 51998,'Owned login sessions have not drained.',1; " +
                "DROP LOGIN [" + Name + "]; END;";
            await command.ExecuteNonQueryAsync(timeout.Token);
        }
    }

    private sealed class ImpersonatingConnectionFactory : IOperatorSqlConnectionFactory
    {
        private readonly string _connectionString;
        private readonly string _databaseUser;
        public ImpersonatingConnectionFactory(string connectionString, string databaseUser)
        {
            _connectionString = connectionString;
            _databaseUser = databaseUser;
        }
        public async Task<SqlConnection> OpenAsync(InspectOptions options, CancellationToken cancellationToken)
        {
            var connection = new SqlConnection(new SqlConnectionStringBuilder(_connectionString) { Pooling = false }.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = "EXECUTE AS USER=N'" + _databaseUser + "';";
            await command.ExecuteNonQueryAsync(cancellationToken);
            return connection;
        }
    }

    private sealed class FakeClient : IOperatorInspectionClient
    {
        private readonly WebOperationInspection? _result;
        private readonly Exception? _error;
        private readonly bool _waitForCancellation;
        public FakeClient(WebOperationInspection result) { _result = result; }
        public FakeClient(Exception error) { _error = error; }
        public FakeClient(bool waitForCancellation) { _waitForCancellation = waitForCancellation; }
        public async Task<WebOperationInspection> InspectAsync(InspectOptions options, CancellationToken cancellationToken)
        {
            if (_waitForCancellation) await Task.Delay(Timeout.Infinite, cancellationToken);
            if (_error != null) throw _error;
            return _result!;
        }
    }

    private sealed class RawSqlTimeoutClient : IOperatorInspectionClient
    {
        private readonly string _connectionString;
        public RawSqlTimeoutClient(string connectionString) { _connectionString = connectionString; }
        public async Task<WebOperationInspection> InspectAsync(InspectOptions options, CancellationToken cancellationToken)
        {
            using var connection = new SqlConnection(new SqlConnectionStringBuilder(_connectionString) { Pooling = false }.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandTimeout = 1;
            command.CommandText = "WAITFOR DELAY '00:00:03'; SELECT 1;";
            await command.ExecuteScalarAsync(cancellationToken);
            throw new InvalidOperationException("The deterministic SQL command timeout did not occur.");
        }
    }
}
