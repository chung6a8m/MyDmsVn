using System;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Testing.Sql;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Narrow SQL ownership adapter; no HTTP, migration, runtime grant, or workload.
// The private manifest is created before SQL and keeps the intended name even
// when an allocation times out. The outer supervisor must never retry removal.
internal sealed class Web5OwnedSqlAllocation
{
    private readonly string _admin;
    private readonly string _directory;
    private readonly string _manifest;
    private readonly object _recordLock = new object();
    private SqlTestDatabase? _database;
    private string? _name;
    private string? _server;
    private int _busy;
    private bool _removalAttempted;
    private bool _verified;
    private bool _quarantined;
    private Web5OwnedSqlAllocation(string admin, string directory) { _admin = admin; _directory = directory; _manifest = Path.Combine(directory, "sql-owner.txt"); }

    internal static Web5OwnedSqlAllocation Create(string admin, string privateDirectory)
    {
        using var identity = WindowsIdentity.GetCurrent();
        Web5AttendedLease.VerifyPrivateDirectory(privateDirectory, identity.User!);
        var manifest = Path.Combine(privateDirectory, "sql-owner.txt");
        using (new FileStream(manifest, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { }
        var allocation = new Web5OwnedSqlAllocation(admin, privateDirectory);
        allocation.Record("prepared");
        return allocation;
    }
    internal async Task Allocate(CancellationToken token)
    {
        Enter();
        try
        {
            if (_database != null || _name != null) throw new InvalidOperationException("SQL allocation already attempted.");
            _database = await SqlTestDatabase.CreateForSupervisorAsync(_admin, name => { _name = name; Record("create-intended"); }, token);
            using (var connection = new SqlConnection(_database.ConnectionString))
            {
                await connection.OpenAsync(token); await _database.VerifyOwnershipAsync(connection, token);
                _server = await connection.QuerySingleAsync<string>("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'));");
            }
            if (string.IsNullOrWhiteSpace(_server)) throw new InvalidDataException("SQL server identity not confirmed.");
            Record("created");
        }
        finally { Exit(); }
    }
    internal async Task<bool> VerifyOwnershipAndQuiescence(CancellationToken token)
    {
        Enter();
        try
        {
            _verified = false;
            var database = _database ?? throw new InvalidOperationException("SQL allocation not confirmed.");
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.OpenAsync(token); await database.VerifyOwnershipAsync(connection, token);
            }
            using var master = Master(); await master.OpenAsync(token);
            var count = await master.QuerySingleAsync<int>("SELECT (SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE database_id=DB_ID(@name)) + (SELECT COUNT(*) FROM sys.dm_tran_locks WHERE resource_database_id=DB_ID(@name));", new { name = database.DatabaseName });
            if (count != 0) return false;
            _verified = true; Record("owned-and-quiescent"); return true;
        }
        finally { Exit(); }
    }
    internal async Task RemoveOwned(CancellationToken token)
    {
        Enter();
        try
        {
            token.ThrowIfCancellationRequested();
            if (_removalAttempted || !_verified || _quarantined) throw new InvalidOperationException("SQL removal is not admitted.");
            var database = _database ?? throw new InvalidOperationException("SQL allocation not confirmed.");
            _removalAttempted = true; Record("remove-intended");
            await database.DisposeIfQuiescentAsync();
            Record("remove-returned");
        }
        finally { Exit(); }
    }
    internal async Task<bool> VerifyAbsent(CancellationToken token)
    {
        Enter();
        try
        {
            if (!_removalAttempted || _name == null) throw new InvalidOperationException("SQL removal was not attempted.");
            using var master = Master(); await master.OpenAsync(token);
            var server = await master.QuerySingleAsync<string>("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'));");
            if (string.IsNullOrWhiteSpace(_server) || !string.Equals(server, _server, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SQL server identity changed.");
            var absent = await master.QuerySingleAsync<int>("SELECT CASE WHEN DB_ID(@name) IS NULL THEN 1 ELSE 0 END;", new { name = _name });
            if (absent == 1) Record("absent");
            return absent == 1;
        }
        finally { Exit(); }
    }
    internal void Quarantine() { lock (_recordLock) { _quarantined = true; Record("quarantined-uncertain"); } }
    private SqlConnection Master() => new SqlConnection(new SqlConnectionStringBuilder(_admin) { InitialCatalog = "master", Pooling = false }.ConnectionString);
    private void Enter() { if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) throw new InvalidOperationException("Owned SQL action already in flight."); }
    private void Exit() => Volatile.Write(ref _busy, 0);
    private void Record(string phase)
    {
        lock (_recordLock)
        {
            using var identity = WindowsIdentity.GetCurrent();
            Web5AttendedLease.VerifyPrivateDirectory(_directory, identity.User!);
            if (!File.Exists(_manifest) || (File.GetAttributes(_manifest) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("SQL ownership manifest is unsafe.");
            if (_quarantined) phase = "quarantined-uncertain";
            var pending = Path.Combine(_directory, "manifest-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
                    {
                        writer.Write("phase=" + phase + "\ndatabase=" + (_name ?? "unallocated") + "\nserver=" + (_server ?? "unconfirmed") + "\n"); writer.Flush();
                    }
                    stream.Flush(true);
                }
                File.Replace(pending, _manifest, null);
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }
    }
}
