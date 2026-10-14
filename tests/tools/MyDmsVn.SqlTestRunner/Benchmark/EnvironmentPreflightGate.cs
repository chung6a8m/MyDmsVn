using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestHarness;
using MyDmsVn.SqlTestHarness.Approval;
using MyDmsVn.SqlTestHarness.Preflight;

namespace MyDmsVn.SqlTestRunner.Benchmark;

public sealed class EnvironmentPreflightGate : IBenchmarkPreflightGate
{
    public async Task<bool> ValidateAsync(CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            SqlTestHarnessEnvironment.ConnectionStringVariable);
        var approvalPath = Environment.GetEnvironmentVariable(
            SqlTestHarnessEnvironment.ApprovalFileVariable);
        if (string.IsNullOrWhiteSpace(connectionString) ||
            string.IsNullOrWhiteSpace(approvalPath) ||
            !File.Exists(approvalPath))
        {
            return false;
        }

        SqlInstanceApproval approval;
        try
        {
            approval = SqlInstanceApprovalLoader.Load(approvalPath);
        }
        catch (Exception exception) when (exception is IOException || exception is InvalidDataException)
        {
            return false;
        }

        var result = await new SqlPreflightValidator()
            .ValidateAsync(
                connectionString,
                approval,
                SqlPreflightRequirements.Mutation,
                cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess;
    }
}
