using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal sealed class Web5OriginalRaceForensicRecord
{
    internal Web5OriginalRaceForensicRecord(Guid operationId, string triggerName)
    {
        OperationId = operationId;
        TriggerName = triggerName;
    }
    internal Guid OperationId { get; }
    internal string TriggerName { get; }
}

internal sealed class Web5OriginalRaceForensicManifest
{
    internal Web5OriginalRaceForensicManifest(string directoryPath, string databaseName,
        IReadOnlyList<Web5OriginalRaceForensicRecord> records)
    {
        DirectoryPath = directoryPath;
        DatabaseName = databaseName;
        Records = records;
    }
    internal string DirectoryPath { get; }
    internal string DatabaseName { get; }
    internal IReadOnlyList<Web5OriginalRaceForensicRecord> Records { get; }
    internal bool ContainsOwnershipToken => false;
    internal bool ContainsDatabaseIdentity => false;
    internal bool CleanupAuthorityProven => false;
}

internal sealed class Web5OriginalRaceActivitySnapshot
{
    internal Web5OriginalRaceActivitySnapshot(int sessions, int locks)
    {
        if (sessions < 0 || locks < 0) throw new ArgumentOutOfRangeException();
        Sessions = sessions;
        Locks = locks;
    }
    internal int Sessions { get; }
    internal int Locks { get; }
}

internal sealed class Web5OriginalRaceSqlObservation
{
    internal Web5OriginalRaceSqlObservation(string databaseName, string serverName, int databaseId,
        DateTime createDate, bool ownershipMarkerPresent, Web5OriginalRaceActivitySnapshot first,
        Web5OriginalRaceActivitySnapshot second)
    {
        DatabaseName = databaseName;
        ServerName = serverName;
        DatabaseId = databaseId;
        CreateDate = createDate;
        OwnershipMarkerPresent = ownershipMarkerPresent;
        First = first;
        Second = second;
    }
    internal string DatabaseName { get; }
    internal string ServerName { get; }
    internal int DatabaseId { get; }
    internal DateTime CreateDate { get; }
    internal bool OwnershipMarkerPresent { get; }
    internal Web5OriginalRaceActivitySnapshot First { get; }
    internal Web5OriginalRaceActivitySnapshot Second { get; }
}

internal sealed class Web5OriginalRaceForensicReview
{
    internal Web5OriginalRaceForensicReview(bool stableQuiescenceObserved, IReadOnlyList<string> blockers)
    {
        StableQuiescenceObserved = stableQuiescenceObserved;
        Blockers = blockers;
    }
    internal bool StableQuiescenceObserved { get; }
    internal bool CleanupAuthorityProven => false;
    internal IReadOnlyList<string> Blockers { get; }
}

internal static class Web5OriginalRaceForensics
{
    private const int MaximumManifestLength = 1024;
    private static readonly Regex DirectoryPattern = new Regex(
        "\\Arace-[0-9a-f]{32}\\z", RegexOptions.CultureInvariant);
    private static readonly Regex OperationPattern = new Regex(
        "\\A[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\\z",
        RegexOptions.CultureInvariant);
    private static readonly Regex TriggerPattern = new Regex(
        "\\AWeb5Race_[0-9a-f]{32}\\z", RegexOptions.CultureInvariant);
    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

    internal static Web5OriginalRaceForensicManifest ReadManifest(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Path.IsPathRooted(directoryPath))
            throw new ArgumentException("Original-first manifest path must be absolute.", nameof(directoryPath));
        var fullPath = Path.GetFullPath(directoryPath);
        if (!DirectoryPattern.IsMatch(Path.GetFileName(fullPath)))
            throw new InvalidDataException("Original-first manifest directory identity is invalid.");
        using var user = WindowsIdentity.GetCurrent();
        Web5AttendedLease.VerifyPrivateDirectory(fullPath, user.User!);
        var entries = Directory.GetFileSystemEntries(fullPath);
        if (entries.Length == 0 || entries.Length > 6)
            throw new InvalidDataException("Original-first manifest entry count is invalid.");
        var records = new List<Web5OriginalRaceForensicRecord>();
        string? database = null;
        var operations = new HashSet<Guid>();
        var triggers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries.OrderBy(value => value, StringComparer.Ordinal))
        {
            var info = new FileInfo(entry);
            if (!info.Exists || (info.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 ||
                info.Length <= 0 || info.Length > MaximumManifestLength)
                throw new InvalidDataException("Original-first manifest entry is unsafe.");
            var bytes = File.ReadAllBytes(entry);
            if (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf)
                throw new InvalidDataException("Original-first manifest encoding is invalid.");
            var text = StrictUtf8.GetString(bytes);
            if (text.IndexOf('\r') >= 0 || text.IndexOf('\0') >= 0)
                throw new InvalidDataException("Original-first manifest line encoding is invalid.");
            var lines = text.Split('\n');
            if (lines.Length != 4 || lines[3].Length != 0 || !lines[0].StartsWith("database=", StringComparison.Ordinal) ||
                !lines[1].StartsWith("operation=", StringComparison.Ordinal) ||
                !lines[2].StartsWith("trigger=", StringComparison.Ordinal))
                throw new InvalidDataException("Original-first manifest schema is invalid.");
            var currentDatabase = lines[0].Substring("database=".Length);
            SqlTestDatabaseName.EnsureDisposable(currentDatabase);
            if (database == null) database = currentDatabase;
            else if (!string.Equals(database, currentDatabase, StringComparison.Ordinal))
                throw new InvalidDataException("Original-first manifest mixes database identities.");
            var operationText = lines[1].Substring("operation=".Length);
            if (!OperationPattern.IsMatch(operationText) || !Guid.TryParseExact(operationText, "D", out var operation) ||
                !string.Equals(Path.GetFileName(entry), operation.ToString("N") + ".txt", StringComparison.Ordinal) ||
                !operations.Add(operation))
                throw new InvalidDataException("Original-first manifest operation identity is invalid.");
            var trigger = lines[2].Substring("trigger=".Length);
            if (!TriggerPattern.IsMatch(trigger) || !triggers.Add(trigger))
                throw new InvalidDataException("Original-first manifest trigger identity is invalid.");
            records.Add(new Web5OriginalRaceForensicRecord(operation, trigger));
        }
        return new Web5OriginalRaceForensicManifest(fullPath, database!, records);
    }

    internal static Web5OriginalRaceForensicReview Review(Web5OriginalRaceForensicManifest manifest,
        Web5OriginalRaceSqlObservation observation)
    {
        if (manifest == null) throw new ArgumentNullException(nameof(manifest));
        if (observation == null) throw new ArgumentNullException(nameof(observation));
        if (!string.Equals(manifest.DatabaseName, observation.DatabaseName, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(observation.ServerName) || observation.DatabaseId <= 0 ||
            observation.CreateDate == default || observation.First == null || observation.Second == null)
            throw new InvalidDataException("Historical SQL observation identity is invalid.");
        var stable = observation.First.Sessions == 0 && observation.First.Locks == 0 &&
            observation.Second.Sessions == 0 && observation.Second.Locks == 0;
        var blockers = new List<string>
        {
            "Historical manifest does not contain the original SQL ownership token.",
            "Historical manifest does not contain the database creation identity tuple.",
        };
        if (!observation.OwnershipMarkerPresent) blockers.Add("Live SQL ownership marker is absent.");
        if (!stable) blockers.Add("Two bounded activity snapshots do not prove stable quiescence.");
        return new Web5OriginalRaceForensicReview(stable, blockers);
    }

    internal static async Task<Web5OriginalRaceSqlObservation> ObserveAsync(string adminConnectionString,
        Web5OriginalRaceForensicManifest manifest, TimeSpan interval, CancellationToken token)
    {
        if (manifest == null) throw new ArgumentNullException(nameof(manifest));
        if (interval < TimeSpan.FromMilliseconds(100) || interval > TimeSpan.FromSeconds(5))
            throw new ArgumentOutOfRangeException(nameof(interval));
        var builder = new SqlConnectionStringBuilder(adminConnectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) ||
            !string.Equals(builder.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(builder.AttachDBFilename) || builder.UserInstance)
            throw new InvalidOperationException("Historical SQL forensics requires an explicit master connection.");
        builder.InitialCatalog = "master";
        builder.Pooling = false;
        builder.ConnectRetryCount = 0;
        builder.ConnectTimeout = Math.Min(builder.ConnectTimeout == 0 ? 10 : builder.ConnectTimeout, 10);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(timeout.Token);
        var database = manifest.DatabaseName;
        using (var identity = connection.CreateCommand())
        {
            identity.CommandTimeout = 10;
            identity.CommandText = "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),database_id,create_date," +
                "(SELECT COUNT(*) FROM [" + database + "].sys.extended_properties WHERE class=0 AND name=N'MyDmsVn.TestOwnership') " +
                "FROM sys.databases WHERE name=@database;";
            identity.Parameters.Add("@database", SqlDbType.NVarChar, 128).Value = database;
            using var reader = await identity.ExecuteReaderAsync(timeout.Token);
            if (!await reader.ReadAsync(timeout.Token) || reader.IsDBNull(0))
                throw new InvalidDataException("Historical database identity is unavailable.");
            var server = reader.GetString(0);
            var id = reader.GetInt32(1);
            var created = reader.GetDateTime(2);
            var marker = reader.GetInt32(3) == 1;
            if (await reader.ReadAsync(timeout.Token)) throw new InvalidDataException("Historical database identity is ambiguous.");
            reader.Close();
            var first = await ReadActivity(connection, database, timeout.Token);
            await Task.Delay(interval, timeout.Token);
            var second = await ReadActivity(connection, database, timeout.Token);
            return new Web5OriginalRaceSqlObservation(database, server, id, created, marker, first, second);
        }
    }

    private static async Task<Web5OriginalRaceActivitySnapshot> ReadActivity(SqlConnection connection,
        string database, CancellationToken token)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = 10;
        command.CommandText = "SELECT " +
            "(SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE database_id=DB_ID(@database) AND session_id<>@@SPID)," +
            "(SELECT COUNT(*) FROM sys.dm_tran_locks WHERE resource_database_id=DB_ID(@database) AND request_session_id<>@@SPID);";
        command.Parameters.Add("@database", SqlDbType.NVarChar, 128).Value = database;
        using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw new InvalidDataException("Historical activity snapshot is unavailable.");
        return new Web5OriginalRaceActivitySnapshot(reader.GetInt32(0), reader.GetInt32(1));
    }
}
