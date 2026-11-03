using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Desktop.Tests;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web5WriterDrainSqlTests
{
    private sealed class WriterSqlFact : FactAttribute
    {
        public WriterSqlFact()
        {
            if (Environment.GetEnvironmentVariable("MYDMSVN_WEB5_OWNED_SQL_ENABLE") != "1" ||
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYDMSVN_WEB5_OWNED_SQL_DIRECTORY")) ||
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)))
                Skip = "Writer drain SQL test requires explicit nonproduction opt-in and private directory.";
        }
    }
    [WriterSqlFact]
    public async Task Physical_writer_mapping_pool_reuse_and_drain_are_observed_on_owned_sql()
    {
        var parent = Environment.GetEnvironmentVariable("MYDMSVN_WEB5_OWNED_SQL_DIRECTORY")!;
        using var user = WindowsIdentity.GetCurrent(); Web5AttendedLease.VerifyPrivateDirectory(parent, user.User!);
        var path = Path.Combine(parent, "writer-drain-" + Guid.NewGuid().ToString("N"));
        var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false); acl.SetOwner(user.User!);
        acl.AddAccessRule(new FileSystemAccessRule(user.User!, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        Directory.CreateDirectory(path, acl); Web5AttendedLease.VerifyPrivateDirectory(path, user.User!);
        var manifest = Path.Combine(path, "sql-owner.txt"); SqlTestDatabase? db = null; SqlConcurrencyGate? gate = null; SqlConnection? writer = null; Task? work = null; string? writerString = null; var safe = false;
        Exception? scenarioFailure = null, cleanupFailure = null, workerFailure = null;
        try
        {
            db = await SqlTestDatabase.CreateForSupervisorAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!, name =>
            {
                using var stream = new FileStream(manifest, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
                using var output = new StreamWriter(stream, new System.Text.UTF8Encoding(false), 1024, true);
                output.Write("database=" + name + "\n"); output.Flush(); stream.Flush(true);
            });
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var unpooled = new SqlConnectionStringBuilder(db.ConnectionString) { Pooling = false }.ConnectionString;
            gate = await SqlConcurrencyGate.AcquireAsync(unpooled, "writer-drain", deadline.Token);
            writerString = new SqlConnectionStringBuilder(db.ConnectionString) { ApplicationName = "writer-drain-" + Guid.NewGuid().ToString("N"), Pooling = true, MultipleActiveResultSets = false }.ConnectionString;
            writer = new SqlConnection(writerString); await writer.OpenAsync(deadline.Token);
            var command = writer.CreateCommand(); command.CommandTimeout = 15; command.CommandText = "BEGIN TRAN; " + gate.TransactionWaitSql + " COMMIT;";
            work = command.ExecuteNonQueryAsync(deadline.Token);
            var observerString = new SqlConnectionStringBuilder(db.ConnectionString) { Pooling = false, MultipleActiveResultSets = false }.ConnectionString;
            using (var observer = new SqlConnection(observerString))
            {
                await observer.OpenAsync(deadline.Token); await db.VerifyOwnershipAsync(observer, deadline.Token);
                Web5WriterIdentity? identity = null;
                for (var count = 0; count < 200 && identity == null; count++) { identity = await Web5WriterDrain.Observe(observer, gate.ControllerSessionId, deadline.Token); if (identity == null) await Task.Delay(25, deadline.Token); }
                Assert.NotNull(identity); Assert.Equal(writer.ServerProcessId, identity!.SessionId); Assert.NotEqual(writer.ServerProcessId, observer.ServerProcessId);
                Assert.True(await Web5WriterDrain.IsBusy(observer, identity, deadline.Token));
                await gate.ReleaseAsync(deadline.Token); await work; work = null; command.Dispose();
                writer.Dispose(); writer = null;
                using var reused = new SqlConnection(writerString); await reused.OpenAsync(deadline.Token);
                Assert.Equal(identity.SessionId, reused.ServerProcessId);
                Assert.False(await Web5WriterDrain.IsBusy(reused, identity, deadline.Token));
            }
        }
        catch (Exception error) { scenarioFailure = error; }
        finally
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                if (gate != null) await gate.ReleaseAsync(timeout.Token);
                if (work != null) workerFailure = await Web5WriterDrain.AwaitWorker(work, TimeSpan.FromSeconds(15), timeout.Token);
                writer?.Dispose(); gate?.Dispose();
                if (writerString != null) { using var ownedPool = new SqlConnection(writerString); SqlConnection.ClearPool(ownedPool); }
                if (db != null) await db.DisposeIfQuiescentAsync();
                safe = true;
            }
            catch (Exception error) { GuiTestSafety.Poison(error); cleanupFailure = error; }
            finally
            {
                try
                {
                    if (safe) { if (File.Exists(manifest)) File.Delete(manifest); Directory.Delete(path); }
                    else Console.WriteLine("Writer drain SQL test retained private manifest and database for ownership review.");
                }
                catch (Exception error) { cleanupFailure = cleanupFailure == null ? error : new AggregateException(cleanupFailure, error); }
            }
        }
        Web5WriterDrain.ThrowFailures(scenarioFailure, cleanupFailure, workerFailure);
    }
}
