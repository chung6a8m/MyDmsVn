using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Desktop.Tests;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using MyDmsVn.WebOperation.Operator;
using Xunit;
using Scenario = MyDmsVn.Server.Infrastructure.IntegrationTests.WebOperationJournalTests.Scenario;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebOperationMaintenanceOperatorTests
{
    private const string CurrentEpoch = "a23e4567-e89b-12d3-a456-426614174000";
    private const string StoredEpoch = "b23e4567-e89b-12d3-a456-426614174000";
    private const string Operation = "c23e4567-e89b-42d3-a456-426614174000";
    private const string Correlation = "d23e4567-e89b-42d3-a456-426614174000";
    private const string Fingerprint = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
    private const string Confirmation = "compact-execute|environment=owned-test|database=MyDmsVn_Test_Maintenance" +
        "|current-epoch=" + CurrentEpoch + "|stored-epoch=" + StoredEpoch + "|actor=42|operation=" + Operation +
        "|fingerprint=" + Fingerprint + "|correlation=" + Correlation + "|case=case-123|timeout-seconds=15";

    [Theory]
    [InlineData("compact-preview")]
    [InlineData("compact-execute")]
    public async Task Compact_help_is_sql_free(string command)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var result = await OperatorCompactionApplication.RunAsync(new[] { command, "--help" }, _ => throw new Xunit.Sdk.XunitException("SQL was reached."),
            stdout, stderr, CancellationToken.None);
        Assert.Equal((int)OperatorExitCode.Completed, result);
        Assert.Contains(command, stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("MYDMSVN_WEB_OPERATION_MAINTENANCE_CONNECTION_STRING", stdout.ToString(), StringComparison.Ordinal);
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public void Compact_modes_and_exact_confirmation_are_strict()
    {
        Assert.True(CompactOptions.TryParse(Valid(), out var preview, out _));
        Assert.False(preview!.Execute);
        Assert.Equal("compact-preview", preview.Command);
        Assert.Equal("owned-test", preview.EnvironmentName);
        Assert.Equal("MyDmsVn_Test_Maintenance", preview.DatabaseName);
        Assert.Equal(Guid.Parse(CurrentEpoch), preview.CurrentEpoch);
        Assert.Equal(Guid.Parse(StoredEpoch), preview.StoredEpoch);
        Assert.Equal(42, preview.ActorUserId);
        Assert.Equal(Guid.Parse(Operation), preview.OperationId);
        Assert.Equal(Fingerprint, preview.FingerprintHex);
        Assert.Equal(0xab, preview.FingerprintBytes()[0]);
        Assert.Equal(Guid.Parse(Correlation), preview.CorrelationId);
        Assert.Equal("case-123", preview.CaseId);
        Assert.Equal(15, preview.TimeoutSeconds);

        Assert.True(CompactOptions.TryParse(Valid("compact-execute", Confirmation), out var execute, out _));
        Assert.True(execute!.Execute);
        Assert.Equal("compact-execute", execute.Command);
        Assert.Equal(Confirmation, execute.ConfirmationText);
        var executeArgs = Valid("compact-execute", Confirmation);
        var timeoutIndex = Array.IndexOf(executeArgs, "--timeout-seconds");
        var withoutTimeout = executeArgs.Take(timeoutIndex).Concat(executeArgs.Skip(timeoutIndex + 2)).ToArray();
        Assert.True(CompactOptions.TryParse(withoutTimeout,
            out var defaultTimeout, out _));
        Assert.Equal(15, defaultTimeout!.TimeoutSeconds);

        Assert.False(CompactOptions.TryParse(Valid("compact-execute"), out _, out _));
        Assert.False(CompactOptions.TryParse(Valid("compact-preview", Confirmation), out _, out _));
        Assert.False(CompactOptions.TryParse(Valid().Concat(new[] { "--dry-run" }).ToArray(), out _, out _));
        Assert.False(CompactOptions.TryParse(Valid().Concat(new[] { "--execute" }).ToArray(), out _, out _));
        Assert.False(CompactOptions.TryParse(Valid().Concat(new[] { "--actor", "42" }).ToArray(), out _, out _));
        Assert.False(CompactOptions.TryParse(Valid("COMPACT-PREVIEW"), out _, out _));
        Assert.False(CompactOptions.TryParse(Valid("compact-execute", Confirmation + " "), out _, out _));
        foreach (var item in new[]
        {
            ("--environment", "other-test"), ("--database", "MyDmsVn_Test_Other"), ("--current-epoch", StoredEpoch),
            ("--stored-epoch", CurrentEpoch), ("--actor", "43"), ("--operation", "e23e4567-e89b-42d3-a456-426614174000"),
            ("--fingerprint", new string('0', 64)), ("--correlation", "f23e4567-e89b-42d3-a456-426614174000"),
            ("--case", "case-124"), ("--timeout-seconds", "16")
        })
            Assert.False(CompactOptions.TryParse(Replace(Valid("compact-execute", Confirmation), item.Item1, item.Item2), out _, out _));

        foreach (var seconds in new[] { 1, 25 })
        {
            Assert.True(CompactOptions.TryParse(Replace(Valid(), "--timeout-seconds", seconds.ToString(CultureInfo.InvariantCulture)), out var bounded, out _));
            Assert.Equal(seconds, bounded!.TimeoutSeconds);
        }
    }

    [Theory]
    [InlineData("--environment", "owned/test")]
    [InlineData("--database", "MASTER")]
    [InlineData("--current-epoch", "00000000-0000-0000-0000-000000000000")]
    [InlineData("--current-epoch", " A23E4567-E89B-12D3-A456-426614174000 ")]
    [InlineData("--stored-epoch", "{b23e4567-e89b-12d3-a456-426614174000}")]
    [InlineData("--actor", "042")]
    [InlineData("--actor", "2147483648")]
    [InlineData("--operation", "c23e4567-e89b-12d3-a456-426614174000")]
    [InlineData("--operation", "c23e4567-e89b-42d3-7456-426614174000")]
    [InlineData("--correlation", Operation)]
    [InlineData("--correlation", "D23E4567-E89B-42D3-A456-426614174000")]
    [InlineData("--case", "../case")]
    [InlineData("--fingerprint", "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789")]
    [InlineData("--timeout-seconds", "01")]
    [InlineData("--timeout-seconds", "0")]
    [InlineData("--timeout-seconds", "26")]
    public async Task Compact_rejects_noncanonical_identity_before_SQL(string flag, string value)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var result = await OperatorCompactionApplication.RunAsync(Replace(Valid(), flag, value),
            _ => throw new Xunit.Sdk.XunitException("SQL was reached."), stdout, stderr, CancellationToken.None);
        Assert.Equal((int)OperatorExitCode.InvalidArguments, result);
        Assert.Empty(stdout.ToString());
        Assert.Contains("Operator.InvalidArguments", stderr.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(OperatorCompactionOutcome.Eligible, OperatorExitCode.Preview, "ELIGIBLE")]
    [InlineData(OperatorCompactionOutcome.NoChange, OperatorExitCode.Preview, "NO_CHANGE")]
    [InlineData(OperatorCompactionOutcome.Completed, OperatorExitCode.Unknown, null)]
    [InlineData(OperatorCompactionOutcome.Unknown, OperatorExitCode.Unknown, null)]
    [InlineData(OperatorCompactionOutcome.AuditDeliveryFailed, OperatorExitCode.Unknown, null)]
    public async Task Compact_preview_reports_only_eligible_or_no_change(OperatorCompactionOutcome outcome,
        OperatorExitCode expectedCode, string? expectedLabel)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var result = await OperatorCompactionApplication.RunAsync(Valid(), _ => new PreviewClient(outcome), stdout, stderr, CancellationToken.None);
        Assert.Equal((int)expectedCode, result);
        Assert.Equal(expectedLabel == null ? string.Empty : "status=" + expectedLabel + Environment.NewLine, stdout.ToString());
        if (expectedLabel == null)
            Assert.Equal("status=UNKNOWN/ESCALATE" + Environment.NewLine, stderr.ToString());
        else
            Assert.Empty(stderr.ToString());
    }

    [Fact]
    public async Task Compact_preview_never_calls_execute()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var client = new PreviewClient(OperatorCompactionOutcome.Eligible);
        Assert.Equal((int)OperatorExitCode.Preview, await OperatorCompactionApplication.RunAsync(
            Valid(), options =>
            {
                Assert.False(options.Execute);
                return client;
            }, stdout, stderr, CancellationToken.None));
        Assert.Equal(1, client.PreviewCalls);
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Maintenance_provisioning_is_exact_database_control_and_distinct_from_runtime_and_operator() =>
        OwnedPrincipals(async (scenario, principals) =>
        {
            var script = principals.ProvisionScript();
            await Assert.ThrowsAsync<SqlException>(() =>
                scenario.Observer.ExecuteAsync(principals.ProvisionScript(maintenance: principals.Operator)));
            await Assert.ThrowsAsync<SqlException>(() =>
                scenario.Observer.ExecuteAsync(principals.ProvisionScript(maintenance: principals.Runtime)));
            await scenario.Observer.ExecuteAsync("CREATE TABLE dbo.Task1SchemaDrift(Id int);");
            await Assert.ThrowsAsync<SqlException>(() => scenario.Observer.ExecuteAsync(script));
            await scenario.Observer.ExecuteAsync("DROP TABLE dbo.Task1SchemaDrift;");
            Assert.Equal(0, await scenario.Observer.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.database_permissions WHERE class=0 AND permission_name=N'CONTROL' " +
                "AND grantee_principal_id=USER_ID(@name);", new { name = principals.Maintenance.Name }));

            await scenario.Observer.ExecuteAsync(script);
            Assert.Equal(1, await scenario.Observer.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.database_permissions WHERE class=0 AND major_id=0 AND minor_id=0 AND state='G' " +
                "AND permission_name=N'CONTROL' AND grantee_principal_id=USER_ID(@name);", new { name = principals.Maintenance.Name }));
            Assert.Equal(0, await scenario.Observer.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM sys.database_role_members WHERE member_principal_id=USER_ID(@name);",
                new { name = principals.Maintenance.Name }));
            foreach (var principal in new[] { principals.Maintenance, principals.Operator, principals.Runtime })
            {
                using var connection = await principal.OpenAsync(scenario);
                Assert.Equal(principal == principals.Maintenance ? 1 : 0, await connection.QuerySingleAsync<int>(
                    "SELECT ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'CONTROL'),0);"));
                Assert.Equal(0, await connection.QuerySingleAsync<int>(
                    "SELECT ISNULL(HAS_PERMS_BY_NAME(NULL,NULL,N'CONTROL SERVER'),0);"));
                Assert.Equal(0, await connection.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM sys.login_token WHERE type=N'SERVER ROLE' AND name<>N'public';"));
            }
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task B02_operator_and_runtime_cannot_preview_or_compact() =>
        OwnedPrincipals(async (scenario, principals) =>
        {
            await scenario.Observer.ExecuteAsync(principals.ProvisionScript());
            var operation = await SeedAsync(scenario, scenario.Epoch, elapsed: true);
            var args = SqlArguments(scenario, scenario.Epoch, operation);
            var before = await SnapshotAsync(scenario.Observer);
            foreach (var restricted in new[] { principals.Operator, principals.Runtime })
            {
                var denied = await RunReleaseAsync(args, scenario, restricted, principals.Operator);
                Assert.Equal((int)OperatorExitCode.Denied, denied.ExitCode);
                Assert.Equal("status=DENIED" + Environment.NewLine, denied.StandardError);

                using var context = new WebOperationJournalTests.Context(await restricted.OpenAsync(scenario));
                context.Tx = context.Sql.BeginTransaction();
                var auditCalled = false;
                var error = await Assert.ThrowsAsync<SqlException>(() => new SqlWebOperationMaintenance(context).CompactAsync(
                    scenario.Epoch, scenario.Epoch, scenario.Actor, operation, Guid.NewGuid(), (_, _, _, _, _, _) =>
                    {
                        auditCalled = true;
                        return Task.CompletedTask;
                    }, scenario.Stop.Token));
                Assert.Equal(51202, error.Number);
                Assert.False(auditCalled);
                context.Tx.Rollback();
            }
            Assert.Equal(before, await SnapshotAsync(scenario.Observer));
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Maintenance_factory_rejects_server_control_roles_other_database_and_unexpected_identity() =>
        OwnedPrincipals(async (scenario, principals) =>
        {
            await scenario.Observer.ExecuteAsync(principals.ProvisionScript());
            var operation = await SeedAsync(scenario, scenario.Epoch, elapsed: true);
            var args = SqlArguments(scenario, scenario.Epoch, operation);
            var maintenance = principals.Maintenance;
            Assert.Equal((int)OperatorExitCode.Preview, (await RunReleaseAsync(args, scenario, maintenance)).ExitCode);

            Assert.Equal((int)OperatorExitCode.Denied,
                (await RunReleaseAsync(args, scenario, maintenance, expectedUser: principals.Runtime.Name)).ExitCode);
            Assert.Equal((int)OperatorExitCode.Denied,
                (await RunReleaseAsync(args, scenario, maintenance, expectedLogin: principals.Operator.Name)).ExitCode);
            Assert.Equal((int)OperatorExitCode.Denied,
                (await RunReleaseAsync(Replace(args, "--database", "MyDmsVn_Test_Other"), scenario, maintenance)).ExitCode);

            using var master = new SqlConnection(new SqlConnectionStringBuilder(scenario.Database.ConnectionString)
            {
                InitialCatalog = "master",
                Pooling = false
            }.ConnectionString);
            await master.OpenAsync(scenario.Stop.Token);
            foreach (var (connection, grant, revoke) in new[]
            {
                (master, "GRANT CONTROL SERVER TO " + maintenance.Identifier, "REVOKE CONTROL SERVER FROM " + maintenance.Identifier),
                (master, "ALTER SERVER ROLE securityadmin ADD MEMBER " + maintenance.Identifier, "ALTER SERVER ROLE securityadmin DROP MEMBER " + maintenance.Identifier),
                (scenario.Observer, "ALTER ROLE db_owner ADD MEMBER " + maintenance.Identifier, "ALTER ROLE db_owner DROP MEMBER " + maintenance.Identifier),
                (scenario.Observer, "ALTER ROLE MyDmsVnWebRuntimeRole ADD MEMBER " + maintenance.Identifier, "ALTER ROLE MyDmsVnWebRuntimeRole DROP MEMBER " + maintenance.Identifier),
                (scenario.Observer, "ALTER ROLE MyDmsVnWebOperationOperatorRole ADD MEMBER " + maintenance.Identifier, "ALTER ROLE MyDmsVnWebOperationOperatorRole DROP MEMBER " + maintenance.Identifier)
            })
            {
                await connection.ExecuteAsync(grant);
                try
                {
                    await Assert.ThrowsAsync<SqlException>(() => scenario.Observer.ExecuteAsync(principals.ProvisionScript()));
                    Assert.Equal((int)OperatorExitCode.Denied, (await RunReleaseAsync(args, scenario, maintenance)).ExitCode);
                }
                finally
                {
                    await connection.ExecuteAsync(revoke);
                }
            }

            await scenario.Observer.ExecuteAsync("CREATE TABLE dbo.Task1SchemaDrift(Id int);");
            Assert.Equal((int)OperatorExitCode.Denied, (await RunReleaseAsync(args, scenario, maintenance)).ExitCode);
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Release_preview_uses_only_the_dedicated_maintenance_environment() =>
        OwnedPrincipals(async (scenario, principals) =>
        {
            await scenario.Observer.ExecuteAsync(principals.ProvisionScript());
            var storedEpoch = Guid.NewGuid();
            var operation = await SeedAsync(scenario, storedEpoch, elapsed: true);
            var retained = await SeedAsync(scenario, storedEpoch, elapsed: false);
            var expired = await SeedAsync(scenario, storedEpoch, elapsed: true);
            await scenario.Observer.ExecuteAsync("UPDATE dbo.WebOperationJournal SET Disposition='EXPIRED', ResultBytes=NULL WHERE OperationId=@id;",
                new { id = expired });
            var cancelled = WebOperationResolutionScenario.Operation(scenario);
            var decision = await scenario.Transaction();
            await WebOperationResolutionScenario.Record(scenario, decision, cancelled);
            decision.Tx.Commit();
            var before = await SnapshotAsync(scenario.Observer);
            var args = SqlArguments(scenario, storedEpoch, operation);

            var missing = await RunReleaseAsync(args, scenario, maintenance: null, operatorOnly: principals.Maintenance);
            Assert.Equal((int)OperatorExitCode.EnvironmentFailure, missing.ExitCode);
            Assert.Empty(missing.StandardOutput);
            var preview = await RunReleaseAsync(args, scenario, principals.Maintenance, principals.Operator);
            Assert.Equal((int)OperatorExitCode.Preview, preview.ExitCode);
            Assert.Equal("status=ELIGIBLE" + Environment.NewLine, preview.StandardOutput);
            Assert.Empty(preview.StandardError);
            Assert.True(principals.Maintenance.IsSecretFree(preview.StandardOutput + preview.StandardError));
            Assert.DoesNotContain("private-payload", preview.StandardOutput + preview.StandardError, StringComparison.Ordinal);

            var noChange = await RunReleaseAsync(SqlArguments(scenario, storedEpoch, expired), scenario, principals.Maintenance);
            Assert.Equal((int)OperatorExitCode.Preview, noChange.ExitCode);
            Assert.Equal("status=NO_CHANGE" + Environment.NewLine, noChange.StandardOutput);
            foreach (var invalid in new[]
            {
                Replace(args, "--current-epoch", storedEpoch.ToString("D")),
                Replace(args, "--stored-epoch", scenario.Epoch.ToString("D")),
                Replace(args, "--actor", (scenario.Actor + 1).ToString(CultureInfo.InvariantCulture)),
                Replace(args, "--fingerprint", new string('0', 64)),
                SqlArguments(scenario, storedEpoch, retained),
                SqlArguments(scenario, storedEpoch, Guid.NewGuid()),
                Replace(SqlArguments(scenario, scenario.Epoch, cancelled.OperationId), "--fingerprint", Hex(cancelled.Fingerprint))
            })
            {
                var denied = await RunReleaseAsync(invalid, scenario, principals.Maintenance);
                Assert.Equal((int)OperatorExitCode.Denied, denied.ExitCode);
                Assert.Empty(denied.StandardOutput);
            }
            Assert.Equal(before, await SnapshotAsync(scenario.Observer));
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Compact_preview_waits_for_the_exact_journal_fence_and_rolls_back() =>
        OwnedPrincipals(async (scenario, principals) =>
        {
            await scenario.Observer.ExecuteAsync(principals.ProvisionScript());
            var operation = await SeedAsync(scenario, scenario.Epoch, elapsed: true);
            var args = Replace(SqlArguments(scenario, scenario.Epoch, operation), "--timeout-seconds", "7");
            var before = await SnapshotAsync(scenario.Observer);
            var holder = await scenario.Transaction();
            var resource = "MyDmsVn.WebOperation:" + scenario.Actor.ToString(CultureInfo.InvariantCulture) + ":" + operation.ToString("N");
            Assert.True(await holder.Sql.QuerySingleAsync<int>(new SqlTestCommand(
                "DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive'," +
                "@LockOwner='Transaction',@LockTimeout=0; SELECT @result;", new { resource }, transaction: holder.Tx)) >= 0);
            var blocked = await RunReleaseAsync(args, scenario, principals.Maintenance);
            Assert.Equal((int)OperatorExitCode.Timeout, blocked.ExitCode);
            Assert.Empty(blocked.StandardOutput);
            holder.Tx.Rollback();

            var released = await RunReleaseAsync(args, scenario, principals.Maintenance);
            Assert.Equal((int)OperatorExitCode.Preview, released.ExitCode);
            Assert.Equal(before, await SnapshotAsync(scenario.Observer));
        });

    private static Task OwnedPrincipals(Func<Scenario, TestPrincipals, Task> body) =>
        WebOperationResolutionScenario.Owned(async scenario =>
        {
            var principals = new TestPrincipals();
            Exception? failure = null;
            try
            {
                await principals.CreateAsync(scenario);
                await body(scenario, principals);
            }
            catch (Exception error)
            {
                failure = error;
                throw;
            }
            finally
            {
                try { await principals.CleanupAsync(scenario); }
                catch (Exception cleanup)
                {
                    GuiTestSafety.Poison(cleanup);
                    if (failure != null) throw new AggregateException(failure, cleanup);
                    throw;
                }
            }
        });

    private static async Task<Guid> SeedAsync(Scenario scenario, Guid epoch, bool elapsed)
    {
        var operation = Guid.NewGuid();
        await scenario.Observer.ExecuteAsync(
            "DECLARE @now datetime2(7)=DATEADD(day,@days,SYSUTCDATETIME()); " +
            "INSERT dbo.WebOperationJournal VALUES(@actor,@operation,@epoch,1,1,1,NULL,@hash,41,'COMMITTED',@bytes,@now,@now,DATEADD(day,7,@now));",
            new { scenario.Actor, operation, epoch, days = elapsed ? -8 : -1, hash = FingerprintBytes(),
                bytes = System.Text.Encoding.UTF8.GetBytes("{\"data\":\"private-payload\"}") });
        return operation;
    }

    private static byte[] FingerprintBytes() => Enumerable.Range(0, 32)
        .Select(index => Convert.ToByte(Fingerprint.Substring(index * 2, 2), 16)).ToArray();

    private static string Hex(byte[] bytes) => string.Concat(bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));

    private static string[] SqlArguments(Scenario scenario, Guid storedEpoch, Guid operation)
    {
        var args = Replace(Valid(), "--database", scenario.Database.DatabaseName);
        args = Replace(args, "--current-epoch", scenario.Epoch.ToString("D"));
        args = Replace(args, "--stored-epoch", storedEpoch.ToString("D"));
        args = Replace(args, "--actor", scenario.Actor.ToString(CultureInfo.InvariantCulture));
        return Replace(args, "--operation", operation.ToString("D"));
    }

    private static async Task<string> SnapshotAsync(SqlConnection connection)
    {
        var snapshot = new List<string>();
        foreach (var source in new[]
        {
            "WebDeploymentState ORDER BY Id", "WebOperationJournal ORDER BY ActorUserId,OperationId",
            "WebOperationTerminalAudit ORDER BY ActorUserId,OperationId", "SalesReceipts ORDER BY SaleId",
            "SalesReceiptLines ORDER BY SaleLineId", "GoodsReceipts ORDER BY ReceiptId", "GoodsReceiptLines ORDER BY ReceiptLineId",
            "StockBalances ORDER BY WarehouseId,ProductId", "StockLedger ORDER BY LedgerId", "StockValuationEntries ORDER BY LedgerId"
        })
            snapshot.Add(await connection.QuerySingleAsync<string>(
                "SELECT COALESCE((SELECT * FROM dbo." + source + " FOR JSON PATH,INCLUDE_NULL_VALUES),N'[]');"));
        return string.Join("|", snapshot);
    }

    private static async Task<GuiProcessResult> RunReleaseAsync(string[] args, Scenario scenario, TestPrincipal? maintenance,
        TestPrincipal? operatorOnly = null, string? expectedUser = null, string? expectedLogin = null)
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "MyDmsVn.WebOperation.Operator.exe");
        Assert.True(File.Exists(executable));
        var start = new ProcessStartInfo(executable, string.Join(" ", args.Select(value => "\"" + value + "\"")))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        start.EnvironmentVariables.Remove(SqlTestDatabase.ConnectionStringEnvironmentVariable);
        var environment = start.EnvironmentVariables;
        foreach (var name in new[]
        {
            OperatorCompactionApplication.ConnectionStringEnvironmentVariable,
            OperatorCompactionApplication.DatabaseUserEnvironmentVariable,
            OperatorCompactionApplication.LoginEnvironmentVariable,
            OperatorInspectApplication.ConnectionStringEnvironmentVariable,
            OperatorInspectApplication.DatabaseUserEnvironmentVariable,
            OperatorInspectApplication.LoginEnvironmentVariable
        })
            environment.Remove(name);
        if (maintenance != null)
        {
            environment[OperatorCompactionApplication.ConnectionStringEnvironmentVariable] = maintenance.ConnectionString(scenario);
            environment[OperatorCompactionApplication.DatabaseUserEnvironmentVariable] = expectedUser ?? maintenance.Name;
            environment[OperatorCompactionApplication.LoginEnvironmentVariable] = expectedLogin ?? maintenance.Name;
        }
        if (operatorOnly != null)
        {
            environment[OperatorInspectApplication.ConnectionStringEnvironmentVariable] = operatorOnly.ConnectionString(scenario);
            environment[OperatorInspectApplication.DatabaseUserEnvironmentVariable] = operatorOnly.Name;
            environment[OperatorInspectApplication.LoginEnvironmentVariable] = operatorOnly.Name;
        }
        return await GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(20), scenario.Stop.Token);
    }

    private sealed class TestPrincipals
    {
        public TestPrincipal Maintenance { get; } = new TestPrincipal();
        public TestPrincipal Operator { get; } = new TestPrincipal();
        public TestPrincipal Runtime { get; } = new TestPrincipal();
        private TestPrincipal Migration { get; } = new TestPrincipal();

        public async Task CreateAsync(Scenario scenario)
        {
            await scenario.Database.VerifyOwnershipAsync(scenario.Observer, scenario.Stop.Token);
            foreach (var principal in new[] { Maintenance, Operator, Runtime, Migration })
                await principal.CreateAsync(scenario.Observer);
            await scenario.Observer.ExecuteAsync(ReadProvisionScript("Provision-MyDmsVnWebPrincipals.sql")
                .Replace("$(WebRuntimeUser)", Runtime.Name).Replace("$(MigrationUser)", Migration.Name));
            await scenario.Observer.ExecuteAsync(ReadProvisionScript("Provision-MyDmsVnWebOperationOperator.sql")
                .Replace("$(OperatorUser)", Operator.Name).Replace("$(WebRuntimeUser)", Runtime.Name));
        }

        public string ProvisionScript(TestPrincipal? maintenance = null) =>
            ReadProvisionScript("Provision-MyDmsVnWebOperationMaintenance.sql")
                .Replace("$(MaintenanceUser)", (maintenance ?? Maintenance).Name)
                .Replace("$(OperatorUser)", Operator.Name).Replace("$(WebRuntimeUser)", Runtime.Name);

        private static string ReadProvisionScript(string filename)
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                var path = Path.Combine(directory.FullName, "deployment", "sql", filename);
                if (File.Exists(path))
                    return File.ReadAllText(path);
            }
            throw new FileNotFoundException(filename);
        }

        public async Task CleanupAsync(Scenario scenario)
        {
            if (WebOperationCrashProcess.IsPoisoned)
                throw new InvalidOperationException("The owned process did not drain; retain the exact SQL principals.");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await scenario.Database.VerifyOwnershipAsync(scenario.Observer, timeout.Token);
            var errors = new List<Exception>();
            foreach (var principal in new[] { Migration, Runtime, Operator, Maintenance })
            {
                try { await principal.CleanupAsync(scenario.Observer, timeout.Token); }
                catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count != 0) throw new AggregateException(errors);
        }
    }

    private sealed class TestPrincipal
    {
        private readonly byte[] _sid = Guid.NewGuid().ToByteArray();
        private readonly string _password = "Web6!" + Guid.NewGuid().ToString("N");
        private bool _creationAttempted;

        public string Name { get; } = "WEB6_Mnt_" + Guid.NewGuid().ToString("N");
        public string Identifier => "[" + Name + "]";

        public async Task CreateAsync(SqlConnection connection)
        {
            _creationAttempted = true;
            await connection.ExecuteAsync("CREATE LOGIN " + Identifier + " WITH PASSWORD=N'" + _password +
                "', SID=0x" + Hex(_sid) + ", CHECK_POLICY=OFF;");
            await connection.ExecuteAsync("CREATE USER " + Identifier + " FOR LOGIN " + Identifier + ";");
        }

        public async Task CleanupAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            if (!_creationAttempted) return;
            await connection.ExecuteAsync(new SqlTestCommand(
                "IF EXISTS(SELECT 1 FROM sys.database_principals WHERE name=@name AND (sid<>@sid OR type<>N'S')) " +
                "OR EXISTS(SELECT 1 FROM sys.server_principals WHERE name=@name AND (sid<>@sid OR type<>N'S')) " +
                "THROW 51998,'Owned principal identity changed.',1; " +
                "IF EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE original_security_id=@sid OR security_id=@sid) " +
                "THROW 51998,'Owned principal sessions have not drained.',1; " +
                "IF EXISTS(SELECT 1 FROM sys.database_principals WHERE name=@name AND sid=@sid) DROP USER " + Identifier + "; " +
                "IF EXISTS(SELECT 1 FROM sys.server_principals WHERE name=@name AND sid=@sid) DROP LOGIN " + Identifier + "; " +
                "IF USER_ID(@name) IS NOT NULL OR SUSER_ID(@name) IS NOT NULL THROW 51998,'Owned principal remains.',1;",
                new { name = Name, sid = _sid }, cancellationToken: cancellationToken));
        }

        public string ConnectionString(Scenario scenario) => new SqlConnectionStringBuilder(scenario.Database.ConnectionString)
        {
            IntegratedSecurity = false,
            Authentication = SqlAuthenticationMethod.NotSpecified,
            UserID = Name,
            Password = _password,
            Pooling = false
        }.ConnectionString;

        public async Task<SqlConnection> OpenAsync(Scenario scenario)
        {
            var connection = new SqlConnection(ConnectionString(scenario));
            try
            {
                await connection.OpenAsync(scenario.Stop.Token);
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        public bool IsSecretFree(string output) => !output.Contains(_password);
    }

    private static string[] Valid(string command = "compact-preview", string? confirmation = null)
    {
        var args = new[]
        {
            command, "--environment", "owned-test", "--database", "MyDmsVn_Test_Maintenance",
            "--current-epoch", CurrentEpoch, "--stored-epoch", StoredEpoch, "--actor", "42",
            "--operation", Operation, "--fingerprint", Fingerprint, "--correlation", Correlation,
            "--case", "case-123", "--timeout-seconds", "15"
        };
        return confirmation == null ? args : args.Concat(new[] { "--confirm", confirmation }).ToArray();
    }

    private static string[] Replace(string[] args, string flag, string value)
    {
        var changed = (string[])args.Clone();
        changed[Array.IndexOf(changed, flag) + 1] = value;
        return changed;
    }

    private sealed class PreviewClient : IOperatorCompactionClient
    {
        private readonly OperatorCompactionOutcome _outcome;

        public PreviewClient(OperatorCompactionOutcome outcome) => _outcome = outcome;

        public int PreviewCalls { get; private set; }

        public Task<OperatorCompactionResult> PreviewAsync(CompactOptions options, CancellationToken cancellationToken)
        {
            PreviewCalls++;
            Assert.Equal("compact-preview", options.Command);
            return Task.FromResult(new OperatorCompactionResult(_outcome));
        }

    }
}
