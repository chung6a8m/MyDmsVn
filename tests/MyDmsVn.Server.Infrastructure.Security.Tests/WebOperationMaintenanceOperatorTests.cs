using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
    private const string TargetIdentityVariable = "MYDMSVN_WEB_OPERATION_MAINTENANCE_TARGET_IDENTITY";
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

    [Theory]
    [InlineData(OperatorCompactionOutcome.Completed, OperatorExitCode.Completed, "COMPLETED")]
    [InlineData(OperatorCompactionOutcome.AuditDeliveryFailed, OperatorExitCode.AuditDeliveryFailed,
        "COMMITTED/AUDIT_DELIVERY_FAILED/ESCALATE; inspect independently")]
    [InlineData(OperatorCompactionOutcome.Unknown, OperatorExitCode.Unknown, "UNKNOWN/ESCALATE")]
    public async Task Compact_execute_preserves_returned_certainty_when_cancellation_arrives_after_execution(
        OperatorCompactionOutcome outcome, OperatorExitCode expected, string label)
    {
        using var stop = new CancellationTokenSource();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var client = new ExecuteResultClient(outcome, stop);
        var result = await OperatorCompactionApplication.RunAsync(Valid("compact-execute", Confirmation), _ => client,
            stdout, stderr, stop.Token);
        Assert.Equal((int)expected, result);
        Assert.Equal(1, client.Calls);
        Assert.Equal("status=" + label + Environment.NewLine, stdout.ToString() + stderr.ToString());
    }

    private sealed class ExecuteResultClient(OperatorCompactionOutcome outcome, CancellationTokenSource stop) : IOperatorCompactionClient
    {
        internal int Calls;
        public Task<OperatorCompactionResult> PreviewAsync(CompactOptions options, CancellationToken token) => throw new NotSupportedException();
        public Task<OperatorCompactionResult> ExecuteAsync(CompactOptions options, CancellationToken token)
        {
            Calls++;
            stop.Cancel();
            return Task.FromResult(new OperatorCompactionResult(outcome));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public async Task Compaction_mutation_failure_attempts_every_cleanup_and_preserves_certainty(int failures)
    {
        var calls = new List<string>();
        var primary = new IOException("mutation failed");
        Action Cleanup(string name, int flag) => () =>
        {
            calls.Add(name);
            if ((failures & flag) != 0) throw new IOException(name);
        };
        var result = await OperatorCompactionCommitBoundary.RunAsync(
            _ => throw primary, () => calls.Add("commit"), Cleanup("rollback", 1), Cleanup("close", 2),
            Cleanup("transaction", 4), Cleanup("connection", 8), CancellationToken.None);
        Assert.Equal(new[] { "rollback", "close", "transaction", "connection" }, calls);
        Assert.Same(primary, result.PrimaryFailure);
        Assert.False(result.CommitStarted);
        Assert.Equal(failures == 0 ? MaintenanceTransactionCertainty.RolledBack : MaintenanceTransactionCertainty.Unknown, result.Certainty);
        Assert.Equal(failures, (int)result.CleanupFailures);
    }

    [Fact]
    public async Task Compaction_cancellation_after_mutation_rolls_back_without_commit()
    {
        using var stop = new CancellationTokenSource();
        var calls = new List<string>();
        var result = await OperatorCompactionCommitBoundary.RunAsync(_ =>
        {
            calls.Add("compact"); stop.Cancel(); return Task.FromResult(1);
        }, () => calls.Add("commit"), () => calls.Add("rollback"), () => calls.Add("close"),
            () => calls.Add("transaction"), () => calls.Add("connection"), stop.Token);
        Assert.Equal(MaintenanceTransactionCertainty.RolledBack, result.Certainty);
        Assert.IsType<OperationCanceledException>(result.PrimaryFailure);
        Assert.Equal(new[] { "compact", "rollback", "close", "transaction", "connection" }, calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Compaction_requires_exactly_one_changed_row(int changed)
    {
        var committed = false;
        var rolledBack = false;
        var result = await OperatorCompactionCommitBoundary.RunAsync(_ => Task.FromResult(changed),
            () => committed = true, () => rolledBack = true, () => { }, () => { }, () => { }, CancellationToken.None);
        Assert.False(committed);
        Assert.True(rolledBack);
        Assert.Equal(MaintenanceTransactionCertainty.RolledBack, result.Certainty);
        Assert.IsType<MyDmsVn.Server.Application.Operations.WebOperationException>(result.PrimaryFailure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Compaction_commit_throw_is_unknown_and_never_rollback_or_retry(bool cleanupFails)
    {
        var calls = new List<string>();
        Action Cleanup(string name) => () => { calls.Add(name); if (cleanupFails) throw new IOException(name); };
        var result = await OperatorCompactionCommitBoundary.RunAsync(_ => { calls.Add("compact"); return Task.FromResult(1); },
            () => { calls.Add("commit"); throw new IOException("commit"); }, () => calls.Add("rollback"),
            Cleanup("close"), Cleanup("transaction"), Cleanup("connection"), CancellationToken.None);
        Assert.Equal(MaintenanceTransactionCertainty.Unknown, result.Certainty);
        Assert.True(result.CommitStarted);
        Assert.Equal(new[] { "compact", "commit", "close", "transaction", "connection" }, calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(14)]
    public async Task Compaction_known_commit_survives_all_cleanup_failures(int failures)
    {
        var calls = new List<string>();
        Action Cleanup(string name, int flag) => () => { calls.Add(name); if ((failures & flag) != 0) throw new IOException(name); };
        var result = await OperatorCompactionCommitBoundary.RunAsync(_ => Task.FromResult(1),
            () => calls.Add("commit"), () => calls.Add("rollback"), Cleanup("close", 2), Cleanup("transaction", 4),
            Cleanup("connection", 8), CancellationToken.None);
        Assert.True(result.CommitStarted);
        Assert.Equal(MaintenanceTransactionCertainty.Committed, result.Certainty);
        Assert.Equal(failures, (int)result.CleanupFailures);
        Assert.Equal(new[] { "commit", "close", "transaction", "connection" }, calls);
    }

    [Fact]
    public async Task Known_commit_with_failed_outcome_delivery_requires_escalation_without_second_compaction()
    {
        var run = new ExecutionProbe { FailOutcome = true };
        Assert.Equal(OperatorCompactionOutcome.AuditDeliveryFailed, (await run.RunAsync()).Outcome);
        Assert.Equal(1, run.Mutations);
        Assert.Equal(1, run.Readbacks);
        Assert.Equal(1, run.Finalizations);
    }

    [Theory]
    [InlineData("attempt", OperatorCompactionOutcome.Unknown, 0, 0)]
    [InlineData("commit", OperatorCompactionOutcome.Unknown, 1, 0)]
    [InlineData("mismatch", OperatorCompactionOutcome.Unknown, 1, 1)]
    [InlineData("readback", OperatorCompactionOutcome.Unknown, 1, 1)]
    [InlineData("finalize", OperatorCompactionOutcome.AuditDeliveryFailed, 1, 1)]
    [InlineData("partial", OperatorCompactionOutcome.AuditDeliveryFailed, 1, 1)]
    [InlineData("none", OperatorCompactionOutcome.Completed, 1, 1)]
    public async Task Compaction_execution_preserves_phase_certainty_and_single_attempt(string failure,
        OperatorCompactionOutcome expected, int mutations, int readbacks)
    {
        var run = new ExecutionProbe { Failure = failure };
        var result = await run.RunAsync();
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(mutations, run.Mutations);
        Assert.Equal(readbacks, run.Readbacks);
        if (failure == "none")
            Assert.Equal(new[] { "preview", "login", "attempt", "mutate", "readback", "Transaction", "Readback", "finalize" }, run.Calls);
    }

    private sealed class ExecutionProbe : IMaintenanceEvidenceStore
    {
        internal string Failure = "none";
        internal bool FailOutcome;
        internal int Mutations, Readbacks, Finalizations;
        internal readonly List<string> Calls = new List<string>();
        internal Task<OperatorCompactionResult> RunAsync()
        {
            Assert.True(CompactOptions.TryParse(Valid("compact-execute", Confirmation), out var options, out _));
            return OperatorCompactionExecution.RunAsync(options!, this,
                _ => { Calls.Add("preview"); return Task.FromResult(new OperatorCompactionResult(OperatorCompactionOutcome.Eligible)); },
                _ => { Calls.Add("login"); return Task.FromResult("maintenance"); },
                (receipt, login, token) =>
                {
                    Assert.Equal("case-123", receipt.CaseId);
                    Assert.Equal("maintenance", login);
                    Calls.Add("mutate"); Mutations++;
                    return OperatorCompactionCommitBoundary.RunAsync(_ => Task.FromResult(1),
                        () => { if (Failure == "commit") throw new IOException("commit"); },
                        () => { }, () => { }, () => { }, () => { }, token);
                },
                _ => { Calls.Add("readback"); Readbacks++; if (Failure == "readback") throw new IOException("readback"); return Task.FromResult(Failure != "mismatch"); },
                CancellationToken.None);
        }
        public Task<MaintenanceEvidenceReceipt> WriteAttemptAsync(MaintenanceEvidenceRecord record, CancellationToken token)
        {
            Calls.Add("attempt");
            if (Failure == "attempt") throw new IOException("attempt");
            return Task.FromResult(new MaintenanceEvidenceReceipt(record.CaseId, new[] { new string('a', 64) }, MaintenanceEvidenceDelivery.PrimaryOnly));
        }
        public Task<MaintenanceEvidenceReceipt> WriteOutcomeAsync(MaintenanceEvidenceRecord record, CancellationToken token)
        {
            Calls.Add(record.Phase.ToString());
            if (FailOutcome) throw new IOException("outcome");
            return Task.FromResult(new MaintenanceEvidenceReceipt(record.CaseId, new[] { new string('b', 64) }, MaintenanceEvidenceDelivery.PrimaryOnly));
        }
        public Task<MaintenanceEvidenceReceipt> FinalizeAndBackupAsync(string caseId, CancellationToken token)
        {
            Calls.Add("finalize"); Finalizations++;
            if (Failure == "finalize") throw new IOException("finalize");
            return Task.FromResult(new MaintenanceEvidenceReceipt(caseId, new[] { new string('b', 64) },
                Failure == "partial" ? MaintenanceEvidenceDelivery.Escalate : MaintenanceEvidenceDelivery.Delivered,
                new string('c', 64), new string('d', 64)));
        }
        public Task<MaintenanceEvidencePurge> PurgeExpiredAsync(string a, string b, string c, string d, string e, CancellationToken token) => throw new NotSupportedException();
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

    [Fact]
    public void Maintenance_target_digest_has_a_canonical_vector()
    {
        const string expected = "8b53d905d15b0d0ac0855a6428d8934ab17e14bfff8a832123a5bdf8679a2a6e";
        var owner = (Server: @"NODE-Å\SQL", Id: 42, CreatedAt: new DateTime(2026, 11, 9, 1, 23, 45).AddTicks(6789012),
            Token: "0123456789abcdef0123456789abcdef");
        Assert.Equal(expected, OwnerTargetDigest("MyDmsVn_Test_Vector", owner));
        Assert.Equal(expected, Hex(MaintenanceSqlConnectionFactory.ComputeTargetIdentity("owned-test", "MyDmsVn_Test_Vector",
            owner.Server, owner.Id, owner.CreatedAt, owner.Token)));
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Maintenance_factory_rejects_same_names_with_wrong_owned_target_identity() =>
        OwnedPrincipals(async (scenario, principals) =>
        {
            await scenario.Observer.ExecuteAsync(principals.ProvisionScript());
            var operation = await SeedAsync(scenario, scenario.Epoch, elapsed: true);
            var args = SqlArguments(scenario, scenario.Epoch, operation);
            var maintenance = principals.Maintenance;
            var before = await SnapshotAsync(scenario.Observer);
            Assert.Equal((int)OperatorExitCode.Preview, (await RunReleaseAsync(args, scenario, maintenance)).ExitCode);

            var owner = principals.Allocation;
            var wrongIdentities = new[]
            {
                OwnerTargetDigest(scenario.Database.DatabaseName, (owner.Server + "-other", owner.Id, owner.CreatedAt, owner.Token)),
                OwnerTargetDigest(scenario.Database.DatabaseName, (owner.Server, owner.Id + 1, owner.CreatedAt, owner.Token)),
                OwnerTargetDigest(scenario.Database.DatabaseName, (owner.Server, owner.Id, owner.CreatedAt.AddSeconds(1), owner.Token)),
                OwnerTargetDigest(scenario.Database.DatabaseName, (owner.Server, owner.Id, owner.CreatedAt, Guid.NewGuid().ToString("N"))),
                string.Empty,
                new string('A', 64),
                new string('a', 63)
            };
            foreach (var wrongIdentity in wrongIdentities)
            {
                var denied = await RunReleaseAsync(args, scenario, maintenance, expectedTargetIdentity: wrongIdentity);
                Assert.Equal((int)OperatorExitCode.Denied, denied.ExitCode);
                Assert.Empty(denied.StandardOutput);
                Assert.Equal("status=DENIED" + Environment.NewLine, denied.StandardError);
            }
            Assert.Equal((int)OperatorExitCode.Denied,
                (await RunReleaseAsync(Replace(args, "--environment", "other-test"), scenario, maintenance)).ExitCode);
            Assert.Equal(before, await SnapshotAsync(scenario.Observer));
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Release_preview_denies_impersonate_permission_on_another_owned_login() =>
        OwnedPrincipals(async (scenario, principals) =>
        {
            await scenario.Observer.ExecuteAsync(principals.ProvisionScript());
            var operation = await SeedAsync(scenario, scenario.Epoch, elapsed: true);
            var args = SqlArguments(scenario, scenario.Epoch, operation);
            var maintenance = principals.Maintenance;
            Assert.Equal((int)OperatorExitCode.Preview, (await RunReleaseAsync(args, scenario, maintenance)).ExitCode);
            using var master = new SqlConnection(new SqlConnectionStringBuilder(scenario.Database.ConnectionString)
            {
                InitialCatalog = "master",
                Pooling = false
            }.ConnectionString);
            await master.OpenAsync(scenario.Stop.Token);
            var securable = "IMPERSONATE ON LOGIN::" + principals.Migration.Identifier;
            await master.ExecuteAsync("GRANT " + securable + " TO " + maintenance.Identifier + ";");
            try
            {
                using (var granted = await maintenance.OpenAsync(scenario))
                    Assert.Equal(1, await granted.QuerySingleAsync<int>("SELECT HAS_PERMS_BY_NAME(@name,N'LOGIN',N'IMPERSONATE');",
                        new { name = principals.Migration.Name }));
                var denied = await RunReleaseAsync(args, scenario, maintenance);
                Assert.Equal((int)OperatorExitCode.Denied, denied.ExitCode);
                Assert.Equal("status=DENIED" + Environment.NewLine, denied.StandardError);
                Assert.Empty(denied.StandardOutput);
            }
            finally
            {
                await master.ExecuteAsync("REVOKE " + securable + " FROM " + maintenance.Identifier + ";");
            }
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Compact_preview_denies_future_expired_tombstone() =>
        OwnedPrincipals(async (scenario, principals) =>
        {
            await scenario.Observer.ExecuteAsync(principals.ProvisionScript());
            var operation = Guid.NewGuid();
            await scenario.Observer.ExecuteAsync(
                "DECLARE @now datetime2(7)=SYSUTCDATETIME(); " +
                "INSERT dbo.WebOperationJournal VALUES(@actor,@operation,@epoch,1,1,1,NULL,@hash,41,'EXPIRED',NULL,@now,@now,DATEADD(day,7,@now));",
                new { scenario.Actor, operation, scenario.Epoch, hash = FingerprintBytes() });
            var before = await SnapshotAsync(scenario.Observer);
            var denied = await RunReleaseAsync(SqlArguments(scenario, scenario.Epoch, operation), scenario, principals.Maintenance);
            Assert.Equal((int)OperatorExitCode.Denied, denied.ExitCode);
            Assert.Equal("status=DENIED" + Environment.NewLine, denied.StandardError);
            Assert.Empty(denied.StandardOutput);
            Assert.Equal(before, await SnapshotAsync(scenario.Observer));
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Compact_preview_denies_elapsed_tombstone_with_trailing_space() =>
        OwnedPrincipals(async (scenario, principals) =>
        {
            await scenario.Observer.ExecuteAsync(principals.ProvisionScript());
            var operation = Guid.NewGuid();
            await scenario.Observer.ExecuteAsync(
                "DECLARE @completed datetime2(7)=DATEADD(day,-8,SYSUTCDATETIME()); " +
                "INSERT dbo.WebOperationJournal VALUES(@actor,@operation,@epoch,1,1,1,NULL,@hash,41,'EXPIRED ',NULL,@completed,@completed,DATEADD(day,7,@completed));",
                new { scenario.Actor, operation, scenario.Epoch, hash = FingerprintBytes() });
            var storedDisposition = await scenario.Observer.QuerySingleAsync<byte[]>(
                "SELECT CONVERT(varbinary(9),Disposition) FROM dbo.WebOperationJournal " +
                "WHERE OperationId=@operation AND ResultExpiresAtUtc<=SYSUTCDATETIME();", new { operation });
            Assert.Equal(Encoding.ASCII.GetBytes("EXPIRED "), storedDisposition);
            var before = await SnapshotAsync(scenario.Observer);

            var denied = await RunReleaseAsync(SqlArguments(scenario, scenario.Epoch, operation), scenario, principals.Maintenance);

            Assert.Equal(before, await SnapshotAsync(scenario.Observer));
            Assert.Equal((int)OperatorExitCode.Denied, denied.ExitCode);
            Assert.Equal("status=DENIED" + Environment.NewLine, denied.StandardError);
            Assert.Empty(denied.StandardOutput);
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

            var missingTarget = await RunReleaseAsync(args, scenario, maintenance: null, operatorOnly: principals.Maintenance);
            Assert.Equal((int)OperatorExitCode.Denied, missingTarget.ExitCode);
            var missing = await RunReleaseAsync(args, scenario, maintenance: null, operatorOnly: principals.Maintenance,
                expectedTargetIdentity: principals.Maintenance.TargetIdentity);
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

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Compact_execute_release_changes_only_result_storage_and_second_attempt_is_no_change() =>
        OwnedPrincipals(async (scenario, principals) =>
        {
            await scenario.Observer.ExecuteAsync(principals.ProvisionScript());
            await SeedBusinessSentinelsAsync(scenario);
            var operation = await SeedAsync(scenario, scenario.Epoch, true);
            var before = await SnapshotAsync(scenario.Observer);
            var immutable = await ImmutableAsync(scenario.Observer, operation);
            using var roots = new ExecutionEvidenceRoots();
            var args = ExecuteArguments(SqlArguments(scenario, scenario.Epoch, operation));
            var result = await RunReleaseAsync(args, scenario, principals.Maintenance, roots: roots);
            Assert.Equal((int)OperatorExitCode.Completed, result.ExitCode);
            Assert.Equal("status=COMPLETED" + Environment.NewLine, result.StandardOutput);
            Assert.Empty(result.StandardError);
            Assert.Equal(immutable, await ImmutableAsync(scenario.Observer, operation));
            Assert.Equal(("EXPIRED", 1), await scenario.Observer.QuerySingleAsync<(string, int)>(
                "SELECT Disposition,CASE WHEN ResultBytes IS NULL THEN 1 ELSE 0 END FROM dbo.WebOperationJournal WHERE OperationId=@operation;", new { operation }));
            var after = await SnapshotAsync(scenario.Observer);
            Assert.Equal(before.Split('|').Where((_, index) => index != 1), after.Split('|').Where((_, index) => index != 1));
            Assert.Equal(0, await scenario.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationTerminalAudit;"));
            var repeat = await RunReleaseAsync(args, scenario, principals.Maintenance, roots: roots);
            Assert.Equal((int)OperatorExitCode.Preview, repeat.ExitCode);
            Assert.Equal("status=NO_CHANGE" + Environment.NewLine, repeat.StandardOutput);
            Assert.Equal(after, await SnapshotAsync(scenario.Observer));
            await Assert.ThrowsAsync<SqlException>(() => scenario.Observer.ExecuteAsync(
                "DELETE dbo.WebOperationJournal WHERE OperationId=@operation;", new { operation }));
            await Assert.ThrowsAsync<SqlException>(() => scenario.Observer.ExecuteAsync(
                "UPDATE dbo.WebOperationJournal SET ResultDocumentId=42 WHERE OperationId=@operation;", new { operation }));
            roots.AssertDelivered("case-123");
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Compact_execute_rejects_noneligible_targets_without_changing_any_table()
    {
        foreach (var invalid in new[] { "retained", "actor", "operation", "current", "stored", "fingerprint", "cancelled" })
            await OwnedPrincipals(async (scenario, principals) =>
            {
                await scenario.Observer.ExecuteAsync(principals.ProvisionScript());
                var operation = await SeedAsync(scenario, scenario.Epoch, invalid != "retained");
                var args = SqlArguments(scenario, scenario.Epoch, operation);
                if (invalid == "actor") args = Replace(args, "--actor", (scenario.Actor + 1).ToString(CultureInfo.InvariantCulture));
                if (invalid == "operation") args = Replace(args, "--operation", Guid.NewGuid().ToString("D"));
                if (invalid == "current") args = Replace(args, "--current-epoch", Guid.NewGuid().ToString("D"));
                if (invalid == "stored") args = Replace(args, "--stored-epoch", Guid.NewGuid().ToString("D"));
                if (invalid == "fingerprint") args = Replace(args, "--fingerprint", new string('0', 64));
                if (invalid == "cancelled")
                {
                    var cancelled = WebOperationResolutionScenario.Operation(scenario);
                    var decision = await scenario.Transaction();
                    await WebOperationResolutionScenario.Record(scenario, decision, cancelled);
                    decision.Tx.Commit();
                    args = Replace(SqlArguments(scenario, scenario.Epoch, cancelled.OperationId), "--fingerprint", Hex(cancelled.Fingerprint));
                }
                var before = await SnapshotAsync(scenario.Observer);
                using var roots = new ExecutionEvidenceRoots();
                var result = await RunReleaseAsync(ExecuteArguments(args), scenario, principals.Maintenance, roots: roots);
                Assert.Equal((int)OperatorExitCode.Denied, result.ExitCode);
                Assert.Equal(before, await SnapshotAsync(scenario.Observer));
                Assert.Empty(Directory.GetFileSystemEntries(roots.Primary));
            });
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Compact_execute_missing_private_evidence_prevents_mutation() =>
        OwnedPrincipals(async (scenario, principals) =>
        {
            await scenario.Observer.ExecuteAsync(principals.ProvisionScript());
            var operation = await SeedAsync(scenario, scenario.Epoch, true);
            var before = await SnapshotAsync(scenario.Observer);
            var result = await RunReleaseAsync(ExecuteArguments(SqlArguments(scenario, scenario.Epoch, operation)), scenario, principals.Maintenance);
            Assert.Equal((int)OperatorExitCode.EnvironmentFailure, result.ExitCode);
            Assert.Equal(before, await SnapshotAsync(scenario.Observer));
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Compact_execute_attempt_evidence_failure_never_opens_mutation_transaction() =>
        OwnedPrincipals(async (scenario, principals) =>
        {
            await scenario.Observer.ExecuteAsync(principals.ProvisionScript());
            var operation = await SeedAsync(scenario, scenario.Epoch, true);
            var before = await SnapshotAsync(scenario.Observer);
            using var roots = new ExecutionEvidenceRoots();
            var factory = new TrackingMaintenanceFactory(scenario, principals.Maintenance);
            var evidence = new InterceptEvidence(roots) { FailAttempt = true };
            var client = new SqlOperatorCompactionClient(factory, evidence);
            var result = await client.ExecuteAsync(ParseExecute(scenario, operation), scenario.Stop.Token);
            Assert.Equal(OperatorCompactionOutcome.Unknown, result.Outcome);
            Assert.Equal(2, factory.Opens);
            Assert.Equal(1, evidence.Attempts);
            Assert.Equal(before, await SnapshotAsync(scenario.Observer));
            Assert.Empty(Directory.GetFileSystemEntries(roots.Primary));
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Compact_execute_fresh_readback_detects_epoch_change_after_known_commit() =>
        OwnedPrincipals(async (scenario, principals) =>
        {
            await scenario.Observer.ExecuteAsync(principals.ProvisionScript());
            var operation = await SeedAsync(scenario, scenario.Epoch, true);
            using var roots = new ExecutionEvidenceRoots();
            var factory = new TrackingMaintenanceFactory(scenario, principals.Maintenance)
            {
                BeforeOpen = async number =>
                {
                    if (number == 4)
                        await scenario.Observer.ExecuteAsync("UPDATE dbo.WebDeploymentState SET DeploymentEpoch=NEWID(),RotatedAtUtc=SYSUTCDATETIME();");
                }
            };
            var result = await new SqlOperatorCompactionClient(factory, new InterceptEvidence(roots))
                .ExecuteAsync(ParseExecute(scenario, operation), scenario.Stop.Token);
            Assert.Equal(OperatorCompactionOutcome.Unknown, result.Outcome);
            Assert.Equal(4, factory.Opens);
            Assert.Equal("EXPIRED", await scenario.Observer.QuerySingleAsync<string>(
                "SELECT Disposition FROM dbo.WebOperationJournal WHERE OperationId=@operation;", new { operation }));
            Assert.Contains("\"transactionCertainty\":\"Committed\"", File.ReadAllText(Path.Combine(roots.Primary, "case-123", "02-transaction.json")));
            Assert.Contains("\"readback\":\"Unknown\"", File.ReadAllText(Path.Combine(roots.Primary, "case-123", "03-readback.json")));
            roots.AssertDelivered("case-123");
        });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Compact_execute_serializes_with_original_writer_before_mutation() => ExecuteWhileWriterHoldsFence("release");

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Compact_execute_cancelled_while_waiting_for_original_writer_does_not_mutate() => ExecuteWhileWriterHoldsFence("cancel");

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Compact_execute_lock_timeout_does_not_mutate_or_retry() => ExecuteWhileWriterHoldsFence("timeout");

    private static Task ExecuteWhileWriterHoldsFence(string mode) => OwnedPrincipals(async (scenario, principals) =>
    {
        await scenario.Observer.ExecuteAsync(principals.ProvisionScript());
        var operation = await SeedAsync(scenario, scenario.Epoch, true);
        var before = await SnapshotAsync(scenario.Observer);
        using var roots = new ExecutionEvidenceRoots();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(scenario.Stop.Token);
        var factory = new TrackingMaintenanceFactory(scenario, principals.Maintenance);
        var holder = await scenario.Transaction();
        var holderSession = await holder.Sql.QuerySingleAsync<int>(new SqlTestCommand("SELECT @@SPID;", transaction: holder.Tx));
        var evidence = new InterceptEvidence(roots)
        {
            AfterAttempt = async () =>
            {
                // The original runtime writer takes the exact lock through the journal, not a test-created lock spelling.
                var expired = await Assert.ThrowsAsync<MyDmsVn.Server.Application.Operations.WebOperationException>(() =>
                    scenario.Journal(holder).AdmitAsync(scenario.Operation(operation, FingerprintBytes()), scenario.Actor, scenario.Stop.Token));
                Assert.Equal("Operation.ResultExpired", expired.Code);
            }
        };
        var client = new SqlOperatorCompactionClient(factory, evidence);
        var worker = scenario.Track(client.ExecuteAsync(ParseExecute(scenario, operation), stop.Token));
        try
        {
            Assert.Same(factory.MutationSession.Task,
                await Task.WhenAny(factory.MutationSession.Task, worker, Task.Delay(TimeSpan.FromSeconds(10), scenario.Stop.Token)));
            var session = await factory.MutationSession.Task;
            await scenario.Blocked(session, holderSession);
            Assert.False(worker.IsCompleted);
            if (mode == "release")
            {
                holder.Tx.Commit();
                Assert.Equal(OperatorCompactionOutcome.Completed, (await worker).Outcome);
                Assert.Equal(4, factory.Opens);
                roots.AssertDelivered("case-123");
            }
            else
            {
                if (mode == "cancel") stop.Cancel();
                if (mode == "timeout")
                {
                    var error = await Assert.ThrowsAsync<MyDmsVn.Server.Application.Operations.WebOperationException>(() => worker);
                    Assert.Equal("Operation.Unavailable", error.Code);
                }
                else Assert.Equal(OperatorCompactionOutcome.Unknown, (await worker).Outcome);
                holder.Tx.Rollback();
                Assert.Equal(3, factory.Opens);
                Assert.Equal(before, await SnapshotAsync(scenario.Observer));
            }
        }
        finally
        {
            if (holder.Tx.Connection != null) holder.Tx.Rollback();
            stop.Cancel();
            try { await worker; } catch { /* Asserted above; ensure actual worker drain before principal cleanup. */ }
        }
        Assert.Equal(1, evidence.Attempts);
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Compact_explicit_rollback_preserves_result_and_business_bytes() =>
        OwnedPrincipals(async (scenario, principals) =>
        {
            await scenario.Observer.ExecuteAsync(principals.ProvisionScript());
            var operation = await SeedAsync(scenario, scenario.Epoch, true);
            var before = await SnapshotAsync(scenario.Observer);
            using var connection = await principals.Maintenance.OpenAsync(scenario);
            using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.ReadCommitted);
            var context = new WebOperationJournalTests.Context(connection) { Tx = transaction };
            var attempts = 0;
            Assert.Equal(1, await new SqlWebOperationMaintenance(context).CompactAsync(scenario.Epoch, scenario.Epoch,
                scenario.Actor, operation, Guid.NewGuid(), (_, _, _, _, _, _) => { attempts++; return Task.CompletedTask; }, scenario.Stop.Token));
            Assert.Equal("EXPIRED", await connection.QuerySingleAsync<string>(new SqlTestCommand(
                "SELECT Disposition FROM dbo.WebOperationJournal WHERE OperationId=@operation;", new { operation }, transaction: transaction)));
            transaction.Rollback();
            Assert.Equal(1, attempts);
            Assert.Equal(before, await SnapshotAsync(scenario.Observer));
        });

    private static CompactOptions ParseExecute(Scenario scenario, Guid operation)
    {
        Assert.True(CompactOptions.TryParse(ExecuteArguments(SqlArguments(scenario, scenario.Epoch, operation)), out var options, out _));
        return options!;
    }

    private sealed class TrackingMaintenanceFactory : IMaintenanceSqlConnectionFactory
    {
        private readonly MaintenanceSqlConnectionFactory _inner;
        internal int Opens;
        internal Func<int, Task>? BeforeOpen;
        internal readonly TaskCompletionSource<int> MutationSession = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TrackingMaintenanceFactory(Scenario scenario, TestPrincipal principal) =>
            _inner = new MaintenanceSqlConnectionFactory(principal.ConnectionString(scenario), principal.Name, principal.Name, principal.TargetIdentity);
        public async Task<SqlConnection> OpenAsync(IOperatorTargetOptions options, CancellationToken token)
        {
            var number = ++Opens;
            if (BeforeOpen != null) await BeforeOpen(number);
            var connection = await _inner.OpenAsync(options, token);
            if (number == 3)
                MutationSession.TrySetResult(await connection.QuerySingleAsync<int>("SELECT @@SPID;"));
            return connection;
        }
    }

    private sealed class InterceptEvidence : IMaintenanceEvidenceStore
    {
        private readonly MaintenanceEvidenceStore _inner;
        internal bool FailAttempt;
        internal int Attempts;
        internal Func<Task>? AfterAttempt;
        internal InterceptEvidence(ExecutionEvidenceRoots roots) => _inner = new MaintenanceEvidenceStore(
            new MaintenanceEvidencePolicy(roots.Primary, roots.Backup, TimeSpan.FromDays(90)));
        public async Task<MaintenanceEvidenceReceipt> WriteAttemptAsync(MaintenanceEvidenceRecord record, CancellationToken token)
        {
            Attempts++;
            if (FailAttempt) throw new IOException("attempt unavailable");
            var receipt = await _inner.WriteAttemptAsync(record, token);
            if (AfterAttempt != null) await AfterAttempt();
            return receipt;
        }
        public Task<MaintenanceEvidenceReceipt> WriteOutcomeAsync(MaintenanceEvidenceRecord record, CancellationToken token) => _inner.WriteOutcomeAsync(record, token);
        public Task<MaintenanceEvidenceReceipt> FinalizeAndBackupAsync(string caseId, CancellationToken token) => _inner.FinalizeAndBackupAsync(caseId, token);
        public Task<MaintenanceEvidencePurge> PurgeExpiredAsync(string a, string b, string c, string d, string e, CancellationToken token) => throw new NotSupportedException();
    }

    private static Task SeedBusinessSentinelsAsync(Scenario scenario) => scenario.Observer.ExecuteAsync(@"
INSERT dbo.Products(Code,Name,Unit) VALUES(N'B03',N'Private business sentinel',N'ea');
DECLARE @product int=SCOPE_IDENTITY();
INSERT dbo.Warehouses(Code,Name) VALUES(N'B03',N'Sentinel warehouse');
DECLARE @warehouse int=SCOPE_IDENTITY();
INSERT dbo.Employees(Code,Name) VALUES(N'B03',N'Sentinel employee');
DECLARE @employee int=SCOPE_IDENTITY();
INSERT dbo.Customers(Code,Name) VALUES(N'B03',N'Sentinel customer');
DECLARE @customer int=SCOPE_IDENTITY();
INSERT dbo.GoodsReceipts(ReceiptNo,ReceiptDate,WarehouseId,EmployeeId,Status,CreatedByUserId,PostedAtUtc,PostedByUserId)
VALUES(N'B03-R','20260101',@warehouse,@employee,'Posted',@actor,SYSUTCDATETIME(),@actor);
DECLARE @receipt bigint=SCOPE_IDENTITY();
INSERT dbo.GoodsReceiptLines(ReceiptId,[LineNo],ProductId,Quantity,UnitCost,CreatedByUserId)
VALUES(@receipt,1,@product,2,7,@actor);
DECLARE @receiptLine bigint=SCOPE_IDENTITY();
INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId)
VALUES(@warehouse,@product,'GoodsReceipt',@receipt,@receiptLine,2,SYSUTCDATETIME(),@actor);
DECLARE @ledger bigint=SCOPE_IDENTITY();
INSERT dbo.StockValuationEntries(LedgerId,ValueChange) VALUES(@ledger,14);
INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@warehouse,@product,2,14);
SET IDENTITY_INSERT dbo.SalesReceipts ON;
INSERT dbo.SalesReceipts(SaleId,SaleNo,SaleDate,WarehouseId,EmployeeId,CustomerId,CreatedByUserId)
VALUES(41,N'B03-S','20260102',@warehouse,@employee,@customer,@actor);
SET IDENTITY_INSERT dbo.SalesReceipts OFF;
INSERT dbo.SalesReceiptLines(SaleId,[LineNo],ProductId,Qty,Unit,IsGift,LineAmount,UnitPrice,CreatedByUserId)
VALUES(41,1,@product,1,N'ea',0,9,9,@actor);", new { actor = scenario.Actor });

    private static Task<string> ImmutableAsync(SqlConnection connection, Guid operation) => connection.QuerySingleAsync<string>(
        "SELECT (SELECT ActorUserId,OperationId,DeploymentEpoch,ContractVersion,CodecVersion,OperationKind,RequestTargetId,RequestFingerprint," +
        "ResultDocumentId,CreatedAtUtc,CompletedAtUtc,ResultExpiresAtUtc FROM dbo.WebOperationJournal WHERE OperationId=@operation FOR JSON PATH,INCLUDE_NULL_VALUES);", new { operation });

    private static string[] ExecuteArguments(string[] preview)
    {
        Assert.True(CompactOptions.TryParse(preview, out var parsed, out _));
        var args = (string[])preview.Clone();
        args[0] = "compact-execute";
        return args.Concat(new[] { "--confirm", parsed!.ConfirmationText.Replace("compact-preview|", "compact-execute|") }).ToArray();
    }

    private sealed class ExecutionEvidenceRoots : IDisposable
    {
        private readonly string _workspace = Path.Combine(Path.GetTempPath(), "b03-execute-" + Guid.NewGuid().ToString("N"));
        internal string Primary => Path.Combine(_workspace, "primary");
        internal string Backup => Path.Combine(_workspace, "backup");
        internal ExecutionEvidenceRoots()
        {
            Directory.CreateDirectory(_workspace);
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            foreach (var path in new[] { Primary, Backup })
            {
                var acl = new System.Security.AccessControl.DirectorySecurity();
                acl.SetOwner(identity.User!);
                acl.SetAccessRuleProtection(true, false);
                acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(identity.User!,
                    System.Security.AccessControl.FileSystemRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));
                acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                    new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid, null),
                    System.Security.AccessControl.FileSystemRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));
                Directory.CreateDirectory(path, acl);
            }
        }
        internal void AssertDelivered(string caseId)
        {
            foreach (var root in new[] { Primary, Backup })
            {
                Assert.Equal(4, Directory.GetFiles(Path.Combine(root, caseId)).Length);
                var manifest = File.ReadAllText(Path.Combine(root, caseId, "manifest.json"));
                Assert.Contains("Delivered", manifest);
                Assert.DoesNotContain("private-payload", manifest);
            }
            foreach (var name in new[] { "01-attempt.json", "02-transaction.json", "03-readback.json" })
                Assert.Equal(File.ReadAllBytes(Path.Combine(Primary, caseId, name)), File.ReadAllBytes(Path.Combine(Backup, caseId, name)));
        }
        public void Dispose()
        {
            Assert.Equal(Path.Combine(Path.GetFullPath(Path.GetTempPath()), Path.GetFileName(_workspace)), Path.GetFullPath(_workspace));
            Assert.All(Directory.GetDirectories(_workspace, "*", SearchOption.AllDirectories), path =>
                Assert.Equal((FileAttributes)0, File.GetAttributes(path) & FileAttributes.ReparsePoint));
            Directory.Delete(_workspace, true);
            Assert.False(Directory.Exists(_workspace));
        }
    }

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

    private static string OwnerTargetDigest(string database, (string Server, int Id, DateTime CreatedAt, string Token) owner)
    {
        var fields = new[]
        {
            "mydmsvn-maintenance-target-v1", "owned-test", database, owner.Server,
            owner.Id.ToString(CultureInfo.InvariantCulture), owner.CreatedAt.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            owner.Token
        };
        var canonical = fields.SelectMany(field =>
        {
            var bytes = Encoding.UTF8.GetBytes(field);
            var length = bytes.Length;
            return new[] { (byte)(length >> 24), (byte)(length >> 16), (byte)(length >> 8), (byte)length }.Concat(bytes);
        }).ToArray();
        using var sha256 = SHA256.Create();
        return Hex(sha256.ComputeHash(canonical));
    }

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
            "StockBalances ORDER BY WarehouseId,ProductId", "StockLedger ORDER BY LedgerId", "StockValuationEntries ORDER BY LedgerId",
            "Products ORDER BY ProductId", "Warehouses ORDER BY WarehouseId", "Employees ORDER BY EmployeeId", "Customers ORDER BY CustomerId"
        })
            snapshot.Add(await connection.QuerySingleAsync<string>(
                "SELECT COALESCE((SELECT * FROM dbo." + source + " FOR JSON PATH,INCLUDE_NULL_VALUES),N'[]');"));
        return string.Join("|", snapshot);
    }

    private static async Task<GuiProcessResult> RunReleaseAsync(string[] args, Scenario scenario, TestPrincipal? maintenance,
        TestPrincipal? operatorOnly = null, string? expectedUser = null, string? expectedLogin = null, string? expectedTargetIdentity = null, ExecutionEvidenceRoots? roots = null)
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
            TargetIdentityVariable,
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
        environment.Remove("MYDMSVN_WEB_OPERATION_MAINTENANCE_EVIDENCE_ROOT");
        environment.Remove("MYDMSVN_WEB_OPERATION_MAINTENANCE_EVIDENCE_BACKUP_ROOT");
        if (roots != null)
        {
            environment["MYDMSVN_WEB_OPERATION_MAINTENANCE_EVIDENCE_ROOT"] = roots.Primary;
            environment["MYDMSVN_WEB_OPERATION_MAINTENANCE_EVIDENCE_BACKUP_ROOT"] = roots.Backup;
        }
        var targetIdentity = expectedTargetIdentity ?? maintenance?.TargetIdentity;
        if (!string.IsNullOrEmpty(targetIdentity))
            environment[TargetIdentityVariable] = targetIdentity;
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
        public TestPrincipal Migration { get; } = new TestPrincipal();
        public (string Server, int Id, DateTime CreatedAt, string Token) Allocation { get; private set; }

        public async Task CreateAsync(Scenario scenario)
        {
            await scenario.Database.VerifyOwnershipAsync(scenario.Observer, scenario.Stop.Token);
            Allocation = await scenario.Observer.QuerySingleAsync<(string, int, DateTime, string)>(
                "SELECT CONVERT(nvarchar(128),SERVERPROPERTY(N'ServerName')), database_id, create_date, " +
                "(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'MyDmsVn.TestOwnership') " +
                "FROM sys.databases WHERE name=@database;", new { database = scenario.Database.DatabaseName });
            var ownerDigest = OwnerTargetDigest(scenario.Database.DatabaseName, Allocation);
            foreach (var principal in new[] { Maintenance, Operator, Runtime, Migration })
                await principal.CreateAsync(scenario.Observer, ownerDigest);
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
        public string TargetIdentity { get; private set; } = string.Empty;

        public async Task CreateAsync(SqlConnection connection, string targetIdentity)
        {
            TargetIdentity = targetIdentity;
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

        public Task<OperatorCompactionResult> ExecuteAsync(CompactOptions options, CancellationToken token) => throw new Xunit.Sdk.XunitException("Preview reached execute.");

        public int PreviewCalls { get; private set; }

        public Task<OperatorCompactionResult> PreviewAsync(CompactOptions options, CancellationToken cancellationToken)
        {
            PreviewCalls++;
            Assert.Equal("compact-preview", options.Command);
            return Task.FromResult(new OperatorCompactionResult(_outcome));
        }

    }
}
