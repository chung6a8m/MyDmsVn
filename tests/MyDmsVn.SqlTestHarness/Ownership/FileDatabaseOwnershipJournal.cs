using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MyDmsVn.SqlTestHarness.Ownership;

public sealed class FileDatabaseOwnershipJournal : IDatabaseOwnershipJournal
{
    private const int UnableToRemoveReplacedError = 1175;
    private const int ReplaceAttempts = 3;

    private static readonly Regex DisposableDatabaseName = new Regex(
        "^MyDmsVn_Test_[0-9a-f]{32}$",
        RegexOptions.CultureInvariant);

    private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        Formatting = Formatting.Indented,
        Converters = { new StringEnumConverter() },
    };

    private readonly string _rootDirectory;
    private readonly Action<FileStream> _durableFlush;
    private readonly Action<string, string, string?> _replaceFile;
    private readonly Action<TimeSpan> _replacementRetryDelay;
    private readonly string _mutexName;

    public FileDatabaseOwnershipJournal(string rootDirectory)
        : this(rootDirectory, stream => stream.Flush(flushToDisk: true))
    {
    }

    internal FileDatabaseOwnershipJournal(
        string rootDirectory,
        Action<FileStream> durableFlush)
        : this(
            rootDirectory,
            durableFlush,
            (source, destination, backup) => File.Replace(source, destination, backup),
            delay => System.Threading.Thread.Sleep(delay))
    {
    }

    internal FileDatabaseOwnershipJournal(
        string rootDirectory,
        Action<FileStream> durableFlush,
        Action<string, string, string?> replaceFile,
        Action<TimeSpan> replacementRetryDelay)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("A journal root directory is required.", nameof(rootDirectory));
        }

        _rootDirectory = Path.GetFullPath(rootDirectory);
        _durableFlush = durableFlush ?? throw new ArgumentNullException(nameof(durableFlush));
        _replaceFile = replaceFile ?? throw new ArgumentNullException(nameof(replaceFile));
        _replacementRetryDelay = replacementRetryDelay ??
                                 throw new ArgumentNullException(nameof(replacementRetryDelay));
        _mutexName = "Local\\MyDmsVn.SqlTestJournal." + Hash(_rootDirectory).Substring(0, 24);
    }

    public Task WriteCreateIntentAsync(
        DatabaseOwnershipRecord record,
        CancellationToken cancellationToken = default)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        if (record.State != DatabaseOwnershipState.CreateIntent)
        {
            throw new InvalidOperationException("A new ownership record must start at CreateIntent.");
        }

        ValidateDatabaseName(record.DatabaseName);
        cancellationToken.ThrowIfCancellationRequested();
        WithLock(() =>
        {
            Directory.CreateDirectory(_rootDirectory);
            EnsureNoIncompleteWrites();
            var path = GetRecordPath(record.DatabaseName);
            if (File.Exists(path))
            {
                var existing = ReadRecord(path);
                if (existing.State == DatabaseOwnershipState.CreateIntent &&
                    existing.HasSameIdentity(record))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "A conflicting ownership record already exists for the database.");
            }

            WriteAtomically(path, record);
        });
        return Task.CompletedTask;
    }

    public Task<DatabaseOwnershipRecord> TransitionAsync(
        DatabaseOwnershipRecord expectedRecord,
        DatabaseOwnershipState newState,
        int? databaseId = null,
        DateTimeOffset? databaseCreateDateUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (expectedRecord is null)
        {
            throw new ArgumentNullException(nameof(expectedRecord));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = WithLock(() =>
        {
            EnsureNoIncompleteWrites();
            var path = GetRecordPath(expectedRecord.DatabaseName);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException("The ownership record does not exist.");
            }

            var current = ReadRecord(path);
            if (!current.HasSameIdentity(expectedRecord))
            {
                throw new InvalidOperationException("The ownership identity changed before transition.");
            }

            if (current.State == newState)
            {
                return current;
            }

            if (!CanTransition(current.State, newState))
            {
                throw new InvalidOperationException(
                    $"Ownership cannot transition from {current.State} to {newState}.");
            }

            var updated = current.WithState(
                newState,
                DateTimeOffset.UtcNow,
                databaseId,
                databaseCreateDateUtc);
            WriteAtomically(path, updated);
            return updated;
        });
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<DatabaseOwnershipRecord>> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var records = WithLock<IReadOnlyList<DatabaseOwnershipRecord>>(() =>
        {
            if (!Directory.Exists(_rootDirectory))
            {
                return Array.Empty<DatabaseOwnershipRecord>();
            }

            EnsureNoIncompleteWrites();
            return Directory.GetFiles(_rootDirectory, "*.json")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(ReadRecord)
                .ToArray();
        });
        return Task.FromResult(records);
    }

    private static bool CanTransition(DatabaseOwnershipState current, DatabaseOwnershipState next)
    {
        switch (current)
        {
            case DatabaseOwnershipState.CreateIntent:
                return next == DatabaseOwnershipState.Created ||
                       next == DatabaseOwnershipState.CleanupFailed ||
                       next == DatabaseOwnershipState.Quarantined;
            case DatabaseOwnershipState.Created:
                return next == DatabaseOwnershipState.DropIntent ||
                       next == DatabaseOwnershipState.Quarantined;
            case DatabaseOwnershipState.DropIntent:
                return next == DatabaseOwnershipState.Disposed ||
                       next == DatabaseOwnershipState.CleanupFailed ||
                       next == DatabaseOwnershipState.Quarantined;
            case DatabaseOwnershipState.CleanupFailed:
                return next == DatabaseOwnershipState.DropIntent ||
                       next == DatabaseOwnershipState.Quarantined;
            default:
                return false;
        }
    }

    private void EnsureNoIncompleteWrites()
    {
        if (Directory.Exists(_rootDirectory) &&
            Directory.EnumerateFiles(_rootDirectory, "*.tmp").Any())
        {
            throw new InvalidDataException(
                "The ownership journal contains an incomplete atomic write.");
        }
    }

    private void WriteAtomically(string targetPath, DatabaseOwnershipRecord record)
    {
        var temporaryPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var json = JsonConvert.SerializeObject(record, SerializerSettings);
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                _durableFlush(stream);
            }

            if (File.Exists(targetPath))
            {
                ReplaceWithRetry(temporaryPath, targetPath);
            }
            else
            {
                File.Move(temporaryPath, targetPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void ReplaceWithRetry(string temporaryPath, string targetPath)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                _replaceFile(temporaryPath, targetPath, null);
                return;
            }
            catch (IOException exception) when (
                IsUnableToRemoveReplaced(exception) && attempt < ReplaceAttempts)
            {
                _replacementRetryDelay(TimeSpan.FromMilliseconds(25 * attempt));
            }
        }
    }

    private static bool IsUnableToRemoveReplaced(IOException exception) =>
        (exception.HResult & 0xffff) == UnableToRemoveReplacedError;

    private static DatabaseOwnershipRecord ReadRecord(string path)
    {
        try
        {
            return JsonConvert.DeserializeObject<DatabaseOwnershipRecord>(
                       File.ReadAllText(path),
                       SerializerSettings)
                   ?? throw new InvalidDataException("The ownership journal record is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The ownership journal record is invalid.", exception);
        }
    }

    private string GetRecordPath(string databaseName)
    {
        ValidateDatabaseName(databaseName);
        return Path.Combine(_rootDirectory, databaseName + ".json");
    }

    private static void ValidateDatabaseName(string databaseName)
    {
        if (!DisposableDatabaseName.IsMatch(databaseName ?? string.Empty))
        {
            throw new InvalidOperationException("The database name is not a disposable test database name.");
        }
    }

    private void WithLock(Action action) =>
        WithLock(() =>
        {
            action();
            return true;
        });

    private T WithLock<T>(Func<T> action)
    {
        using var mutex = new Mutex(initiallyOwned: false, _mutexName);
        var lockTaken = false;
        try
        {
            try
            {
                lockTaken = mutex.WaitOne(TimeSpan.FromSeconds(30));
            }
            catch (AbandonedMutexException)
            {
                lockTaken = true;
            }

            if (!lockTaken)
            {
                throw new TimeoutException("Timed out waiting for the ownership journal lock.");
            }

            return action();
        }
        finally
        {
            if (lockTaken)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private static string Hash(string value)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}
