using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web5OwnedSqlAllocationTests
{
    private sealed class OwnedSqlFact : FactAttribute
    {
        public OwnedSqlFact()
        {
            if (Environment.GetEnvironmentVariable("MYDMSVN_WEB5_OWNED_SQL_ENABLE") != "1" ||
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYDMSVN_WEB5_OWNED_SQL_DIRECTORY")) ||
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)))
                Skip = "Owned SQL fixture requires explicit nonproduction opt-in and private parent.";
        }
    }
    [OwnedSqlFact]
    public async Task Owned_sql_allocation_records_verifies_and_cleans_exact_database()
    {
        var parent = Environment.GetEnvironmentVariable("MYDMSVN_WEB5_OWNED_SQL_DIRECTORY")!;
        using var user = WindowsIdentity.GetCurrent(); Web5AttendedLease.VerifyPrivateDirectory(parent, user.User!);
        var path = Path.Combine(parent, "owned-sql-" + Guid.NewGuid().ToString("N"));
        var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false); acl.SetOwner(user.User!);
        acl.AddAccessRule(new FileSystemAccessRule(user.User!, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        Directory.CreateDirectory(path, acl);
        var owner = Web5OwnedSqlAllocation.Create(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!, path);
        var complete = false;
        try
        {
            await owner.Allocate(CancellationToken.None);
            Assert.True(await owner.VerifyOwnershipAndQuiescence(CancellationToken.None));
            await owner.RemoveOwned(CancellationToken.None);
            Assert.True(await owner.VerifyAbsent(CancellationToken.None));
            complete = true;
        }
        finally
        {
            if (complete) { File.Delete(Path.Combine(path, "sql-owner.txt")); Directory.Delete(path); }
            else owner.Quarantine();
        }
    }
    [OwnedSqlFact]
    public async Task Quiescent_dispose_refuses_active_owned_session_without_forcing_rollback()
    {
        var parent = Environment.GetEnvironmentVariable("MYDMSVN_WEB5_OWNED_SQL_DIRECTORY")!;
        using var user = WindowsIdentity.GetCurrent(); Web5AttendedLease.VerifyPrivateDirectory(parent, user.User!);
        var path = Path.Combine(parent, "sql-fault-" + Guid.NewGuid().ToString("N"));
        var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false); acl.SetOwner(user.User!);
        acl.AddAccessRule(new FileSystemAccessRule(user.User!, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        Directory.CreateDirectory(path, acl); Web5AttendedLease.VerifyPrivateDirectory(path, user.User!);
        var manifest = Path.Combine(path, "sql-owner.txt");
        SqlTestDatabase? db = null; var safe = false;
        try
        {
            db = await SqlTestDatabase.CreateForSupervisorAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!, name =>
            {
                using var stream = new FileStream(manifest, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
                using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), 1024, true);
                writer.Write("database=" + name + "\n"); writer.Flush(); stream.Flush(true);
            });
            using (var connection = new SqlConnection(db.ConnectionString))
            {
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();
                await Assert.ThrowsAsync<InvalidOperationException>(() => db.DisposeIfQuiescentAsync());
                using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "SELECT 1;";
                Assert.Equal(1, (int)(await command.ExecuteScalarAsync())!);
                transaction.Rollback();
            }
            await db.DisposeIfQuiescentAsync(); safe = true;
        }
        finally
        {
            if (safe) { File.Delete(manifest); Directory.Delete(path); }
            else Console.WriteLine("Owned SQL fault test requires private exact-identity quarantine review.");
        }
    }
}
