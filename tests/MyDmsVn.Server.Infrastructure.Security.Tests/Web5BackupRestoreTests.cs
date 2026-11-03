using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Opt-in, local disposable SQL only. Unknown ownership/partial restore is quarantined.
public sealed class Web5BackupRestoreTests
{
    private sealed class BackupFactAttribute : FactAttribute
    {
        public BackupFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("MYDMSVN_WEB5_BACKUP_TEST_ENABLE") != "1" ||
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYDMSVN_WEB5_BACKUP_TEST_DIRECTORY")) ||
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)))
                Skip = "Backup rehearsal requires explicit local nonproduction SQL and backup directory opt-in.";
        }
    }

    [BackupFact]
    public async Task Copy_only_backup_restores_owned_exact13_clone()
    {
        var admin = Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!;
        var options = new SqlConnectionStringBuilder(admin) { Pooling = false };
        if (!string.Equals(options.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Explicit master required.");
        var directory = Environment.GetEnvironmentVariable("MYDMSVN_WEB5_BACKUP_TEST_DIRECTORY")!;
        using var user = WindowsIdentity.GetCurrent();
        using var preflight = new SqlConnection(options.ConnectionString);
        await preflight.OpenAsync();
        var machine = await preflight.QuerySingleAsync<string>("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'));");
        if (!string.Equals(machine, Environment.MachineName, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Backup fixture requires local SQL.");
        var account = await preflight.QuerySingleAsync<string>("SELECT service_account FROM sys.dm_server_services WHERE servicename=N'SQL Server ('+CONVERT(nvarchar(128),@@SERVICENAME)+N')';");
        var service = (SecurityIdentifier)new NTAccount(account).Translate(typeof(SecurityIdentifier));
        Web5BackupFixture.VerifyPrivateDirectory(directory, user.User!, service);
        var suffix = Guid.NewGuid().ToString("N");
        var privateDirectory = Path.Combine(directory, "backup-" + suffix);
        var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false); acl.SetOwner(user.User!);
        foreach (var principal in new[] { user.User!, service }.Distinct())
            acl.AddAccessRule(new FileSystemAccessRule(principal, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        Directory.CreateDirectory(privateDirectory, acl);
        Web5BackupFixture.VerifyPrivateDirectory(privateDirectory, user.User!, service);
        var backup = Path.Combine(privateDirectory, "fixture.bak");
        Web5BackupFixture.EnsureNewDevice(backup);
        using (new FileStream(backup, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        var deviceCreation = File.GetCreationTimeUtc(backup);
        var clone = SqlTestDatabaseName.Create();
        Web5BackupFixture.EnsureCloneName(clone);
        var manifest = Path.Combine(privateDirectory, "quarantine.txt");
        void Record(string value) => File.WriteAllText(manifest, value, new UTF8Encoding(false));
        Record("phase=preparing\nclone=" + clone + "\nbackup=" + backup + "\n");
        var marker = Guid.NewGuid().ToString("N");
        SqlTestDatabase? source = null;
        var sourceRecorded = false;
        var complete = false;
        try
        {
            source = await SqlTestDatabase.CreateAsync(admin);
            Record("phase=source-created\nsource=" + source.DatabaseName + "\nclone=" + clone + "\nbackup=" + backup + "\n");
            sourceRecorded = true;
            Assert.True(new DatabaseMigrationRunner().MigrateWeb5(source.ConnectionString).Successful);
            using var sourceSql = new SqlConnection(source.ConnectionString);
            await sourceSql.OpenAsync();
            await source.VerifyOwnershipAsync(sourceSql, default);
            var sourceIdentity = await sourceSql.QuerySingleAsync<(int Id, DateTime Created)>("SELECT database_id,create_date FROM sys.databases WHERE database_id=DB_ID();");
            var originalEpoch = await sourceSql.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;");
            await sourceSql.ExecuteAsync("EXEC sys.sp_addextendedproperty @name=N'Web5BackupProbe', @value=@marker;", new { marker });
            using var sql = new SqlConnection(options.ConnectionString);
            await sql.OpenAsync();
            var server = await sql.QuerySingleAsync<string>("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'));");

            if (await sql.QuerySingleAsync<int>("SELECT CASE WHEN DB_ID(@clone) IS NULL THEN 0 ELSE 1 END;", new { clone }) != 0) throw new InvalidOperationException("Clone name already exists.");
            Web5BackupFixture.VerifyPrivateDirectory(privateDirectory, user.User!, service);
            var beforeBackup = new FileInfo(backup);
            if (!beforeBackup.Exists || beforeBackup.Length != 0 || beforeBackup.CreationTimeUtc != deviceCreation || (beforeBackup.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Backup device changed; retain resources.");
            var backupStarted = DateTime.UtcNow;
            await sql.ExecuteAsync("BACKUP DATABASE [" + source.DatabaseName + "] TO DISK=@backup WITH FORMAT, INIT, COPY_ONLY, CHECKSUM;", new { backup });
            var backupCompleted = DateTime.UtcNow;
            var backupInfo = new FileInfo(backup);
            if (!backupInfo.Exists || (backupInfo.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Backup ownership uncertain; retain resources.");
            var backupCreated = backupInfo.CreationTimeUtc;
            byte[] backupHash;
            using (var file = File.OpenRead(backup)) using (var sha = SHA256.Create()) backupHash = sha.ComputeHash(file);
            using (var verify = sql.CreateCommand())
            {
                verify.CommandText = "RESTORE VERIFYONLY FROM DISK=@backup WITH CHECKSUM;";
                verify.CommandTimeout = 120;
                verify.Parameters.AddWithValue("@backup", backup);
                await verify.ExecuteNonQueryAsync();
            }
            var files = new List<(string Name, string Physical, string Type)>();
            using (var list = sql.CreateCommand())
            {
                list.CommandText = "RESTORE FILELISTONLY FROM DISK=@backup;";
                list.CommandTimeout = 120;
                list.Parameters.AddWithValue("@backup", backup);
                using var reader = await list.ExecuteReaderAsync();
                while (await reader.ReadAsync()) files.Add((reader.GetString(reader.GetOrdinal("LogicalName")), reader.GetString(reader.GetOrdinal("PhysicalName")), reader.GetString(reader.GetOrdinal("Type"))));
            }
            if (files.Count != 2 || files.Count(f => f.Type == "D") != 1 || files.Count(f => f.Type == "L") != 1) throw new InvalidOperationException("Unexpected backup file layout; retain allocation.");
            var data = files.Single(f => f.Type == "D"); var log = files.Single(f => f.Type == "L");
            var dataPath = Path.Combine(Path.GetDirectoryName(data.Physical)!, clone + ".mdf");
            var logPath = Path.Combine(Path.GetDirectoryName(log.Physical)!, clone + ".ldf");
            Web5BackupFixture.EnsureNewDevice(dataPath); Web5BackupFixture.EnsureNewDevice(logPath);
            Record("phase=restore-start\nserver=" + server + "\nsource=" + source.DatabaseName + "\nsourceId=" + sourceIdentity.Id + "\nsourceCreated=" + sourceIdentity.Created.ToString("O") + "\nmarker=" + marker + "\nclone=" + clone + "\nbackup=" + backup + "\ndata=" + dataPath + "\nlog=" + logPath + "\n");
            var rto = Stopwatch.StartNew();
            using (var restore = sql.CreateCommand())
            {
                restore.CommandText = "RESTORE DATABASE [" + clone + "] FROM DISK=@backup WITH CHECKSUM, MOVE @dataName TO @dataPath, MOVE @logName TO @logPath;";
                restore.CommandTimeout = 120;
                restore.Parameters.AddWithValue("@backup", backup); restore.Parameters.AddWithValue("@dataName", data.Name); restore.Parameters.AddWithValue("@dataPath", dataPath);
                restore.Parameters.AddWithValue("@logName", log.Name); restore.Parameters.AddWithValue("@logPath", logPath);
                await restore.ExecuteNonQueryAsync();
            }
            var identity = await sql.QuerySingleAsync<(int Id, DateTime Created)>("SELECT database_id,create_date FROM sys.databases WHERE name=@clone;", new { clone });
            Record("phase=restored\nserver=" + server + "\nsource=" + source.DatabaseName + "\nsourceId=" + sourceIdentity.Id + "\nsourceCreated=" + sourceIdentity.Created.ToString("O") + "\nmarker=" + marker + "\nclone=" + clone + "\ncloneId=" + identity.Id + "\ncloneCreated=" + identity.Created.ToString("O") + "\nbackup=" + backup + "\ndata=" + dataPath + "\nlog=" + logPath + "\n");
            var cloneOptions = new SqlConnectionStringBuilder(options.ConnectionString) { InitialCatalog = clone, Pooling = false };
            using (var check = new SqlConnection(cloneOptions.ConnectionString))
            {
                await check.OpenAsync();
                Assert.Equal(marker, await check.QuerySingleAsync<string>("SELECT CONVERT(nvarchar(32),value) FROM sys.extended_properties WHERE class=0 AND name=N'Web5BackupProbe';"));
                Assert.Equal(originalEpoch, await check.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;"));
                Assert.True((await SqlWeb5SchemaCompatibilityChecker.CheckConnectionAsync(check, default)).IsCompatible, "Restored schema13 fingerprint must be compatible.");
            }
            rto.Stop();
            var actualServer = await sql.QuerySingleAsync<string>("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'));");
            var identityAgain = await sql.QuerySingleAsync<(int Id, DateTime Created)>("SELECT database_id,create_date FROM sys.databases WHERE name=@clone;", new { clone });
            var sessions = await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE database_id=DB_ID(@clone);", new { clone });
            if (actualServer != server || identityAgain != identity || sessions != 0) throw new InvalidOperationException("Clone ownership or quiescence changed; retain resources.");
            using (var check = new SqlConnection(cloneOptions.ConnectionString))
            {
                await check.OpenAsync();
                if (await check.QuerySingleAsync<string>("SELECT CONVERT(nvarchar(32),value) FROM sys.extended_properties WHERE class=0 AND name=N'Web5BackupProbe';") != marker) throw new InvalidOperationException("Clone marker changed; retain resources.");
            }
            await sql.ExecuteAsync("DROP DATABASE [" + clone + "];");
            if (await sql.QuerySingleAsync<int>("SELECT CASE WHEN DB_ID(@clone) IS NULL THEN 0 ELSE 1 END;", new { clone }) != 0) throw new InvalidOperationException("Clone cleanup not confirmed.");
            sourceSql.Close();
            await source.DisposeAsync();
            if (await sql.QuerySingleAsync<int>("SELECT CASE WHEN DB_ID(@name) IS NULL THEN 0 ELSE 1 END;", new { name = source.DatabaseName }) != 0) throw new InvalidOperationException("Source cleanup not confirmed.");
            backupInfo.Refresh();
            if (!backupInfo.Exists || (backupInfo.Attributes & FileAttributes.ReparsePoint) != 0 || backupInfo.CreationTimeUtc != backupCreated) throw new InvalidOperationException("Backup identity changed; retain file.");
            byte[] finalHash;
            using (var file = File.OpenRead(backup)) using (var sha = SHA256.Create()) finalHash = sha.ComputeHash(file);
            if (!backupHash.SequenceEqual(finalHash)) throw new InvalidOperationException("Backup bytes changed; retain file.");
            Web5BackupFixture.VerifyPrivateDirectory(privateDirectory, user.User!, service);
            File.Delete(backup);
            bool ExistsWithDirectoryAccess(string path) => Directory.EnumerateFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path)).Any();
            if (ExistsWithDirectoryAccess(backup) || ExistsWithDirectoryAccess(dataPath) || ExistsWithDirectoryAccess(logPath)) throw new InvalidOperationException("Owned file cleanup not confirmed.");
            File.Delete(manifest); Directory.Delete(privateDirectory);
            complete = true;
            Console.WriteLine("WEB5_BACKUP_RESTORE verified=1 backupDurationMilliseconds=" + (long)(backupCompleted - backupStarted).TotalMilliseconds + " sqlRestoreAndReadVerifyMilliseconds=" + rto.ElapsedMilliseconds + " sourceAndCloneAbsent=1 backupAbsent=1");
        }
        finally
        {
            if (!complete && source != null && !sourceRecorded)
            {
                // Before any source mutation, failure to persist its identity is resolved
                // only by its existing guarded owner cleanup. Uncertain cleanup is fatal.
                try { await source.DisposeAsync(); }
                catch (Exception cleanup)
                {
                    try { Record("phase=source-cleanup-failed\nsource=" + source.DatabaseName + "\nclone=" + clone + "\nbackup=" + backup + "\n"); }
                    catch (Exception manifestFailure) { throw new AggregateException("Owned source cleanup and private quarantine record both failed. Local operator identifiers: source=" + source.DatabaseName + "; clone=" + clone + "; manifest=" + manifest + ". Keep this local diagnostic private.", cleanup, manifestFailure); }
                    throw;
                }
            }
            if (!complete) Console.WriteLine("WEB5_BACKUP_RESTORE incomplete; SQL allocations and backup are quarantined for exact-identity review.");
        }
    }
}
