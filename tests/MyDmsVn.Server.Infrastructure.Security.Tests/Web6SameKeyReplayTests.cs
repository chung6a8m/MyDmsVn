using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed partial class WebOperationHttpSqlTests
{
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Resend_first_and_two_delayed_same_key_workers_converge_for_all_six_commands() =>
        ResendFirstMultiContenderMatrix();

    private async Task ResendFirstMultiContenderMatrix()
    {
        foreach (var kind in CrashKinds)
            await OwnedCrash(async scope =>
            {
                var arranged = await ArrangeCrash(scope.Data, kind);
                scope.Operation = arranged.Intent;

                var resendFirst = await scope.Start(CrashConfiguration(scope, arranged, "before-commit"));
                await resendFirst.DispatchAsync();
                var barrier = await resendFirst.EventAsync("before-commit");
                var holderSession = (int)barrier["sqlSessionId"]!;
                Assert.True(holderSession > 0);

                var delayedOriginal = await scope.Start(CrashConfiguration(scope, arranged, "none"));
                var secondResend = await scope.Start(CrashConfiguration(scope, arranged, "none"));
                await delayedOriginal.DispatchAsync();
                await secondResend.DispatchAsync();
                using (var observer = await CrashOracle(scope.ConnectionString))
                {
                    await WaitForCrashFence(observer, holderSession, delayedOriginal.ProcessId);
                    await WaitForCrashFence(observer, holderSession, secondResend.ProcessId);
                }

                await resendFirst.ReleaseAsync();
                var winner = await resendFirst.ReplyAsync();
                Success(winner);
                await resendFirst.CloseAsync();
                var originalReply = await delayedOriginal.ReplyAsync();
                var resendReply = await secondResend.ReplyAsync();
                Success(originalReply);
                Success(resendReply);
                await delayedOriginal.CloseAsync();
                await secondResend.CloseAsync();
                Assert.True(JToken.DeepEquals(winner.Json, originalReply.Json));
                Assert.True(JToken.DeepEquals(winner.Json, resendReply.Json));

                await AwaitCrashQuiescence(scope, scope.Data.Http.Admin.ServerProcessId);
                using var oracle = await CrashOracle(scope.ConnectionString);
                await VerifyCrashCommitted(oracle, scope.Data, arranged);
                await RecoverAndReplayCrash(scope, arranged);
                _crashOutput.WriteLine("WEB6 resend-first: {0}; three independent workers converged; one committed effect; children drained", kind);
            });
    }
}
