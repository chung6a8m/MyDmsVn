using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.WebOperation.Operator;

public interface IMaintenanceEvidenceStore
{
    Task<MaintenanceEvidenceReceipt> WriteAttemptAsync(MaintenanceEvidenceRecord record, CancellationToken cancellationToken);
    Task<MaintenanceEvidenceReceipt> WriteOutcomeAsync(MaintenanceEvidenceRecord record, CancellationToken cancellationToken);
    Task<MaintenanceEvidenceReceipt> FinalizeAndBackupAsync(string caseId, CancellationToken cancellationToken);
    Task<MaintenanceEvidencePurge> PurgeExpiredAsync(string primaryPath, string backupPath, string caseId,
        string primaryManifestHash, string backupManifestHash, CancellationToken cancellationToken);
}

internal enum MaintenanceEvidenceCheckpoint { AfterPrimaryManifestWrite, BeforePurgeEntryVerification }

public sealed class MaintenanceEvidenceStore : IMaintenanceEvidenceStore
{
    private const string ManifestName = "manifest.json";
    private const int MaximumEvidenceBytes = 8192;
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly MaintenanceEvidencePolicy _policy;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Action<MaintenanceEvidenceCheckpoint>? _onCheckpoint;
    private readonly object _gate = new object();
    private readonly Dictionary<string, ActiveCase> _cases = new Dictionary<string, ActiveCase>(StringComparer.Ordinal);

    public MaintenanceEvidenceStore(MaintenanceEvidencePolicy policy, Func<DateTimeOffset>? utcNow = null)
        : this(policy, utcNow, null) { }

    internal MaintenanceEvidenceStore(MaintenanceEvidencePolicy policy, Func<DateTimeOffset>? utcNow,
        Action<MaintenanceEvidenceCheckpoint>? onCheckpoint)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _onCheckpoint = onCheckpoint;
    }

    // Durable writes and native handle operations are synchronous. The async
    // interface preserves faulted-task semantics for the operator lifecycle.
    public Task<MaintenanceEvidenceReceipt> WriteAttemptAsync(MaintenanceEvidenceRecord record, CancellationToken cancellationToken) =>
        AsTask(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (record.Phase != MaintenanceEvidencePhase.Attempt || _cases.ContainsKey(record.CaseId))
                throw new IOException("Evidence must begin with an attempt.");

            using var primary = _policy.OpenPrimary();
            using var backup = _policy.OpenBackup();
            var utc = ReadUtc();
            using var directory = primary.OpenCase(record.CaseId, create: true);
            var active = new ActiveCase(directory.Identity, record, utc);
            active.Events.Add(WriteEvent(primary, backup, directory, record, utc));
            _cases.Add(record.CaseId, active);
            return EventReceipt(record.CaseId, active);
        });

    public Task<MaintenanceEvidenceReceipt> WriteOutcomeAsync(MaintenanceEvidenceRecord record, CancellationToken cancellationToken) =>
        AsTask(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (record == null) throw new ArgumentNullException(nameof(record));
            var active = GetActive(record.CaseId);
            if (!record.Follows(active.LastRecord))
                throw new IOException("Evidence outcome does not follow the preceding event.");

            using var primary = _policy.OpenPrimary();
            using var backup = _policy.OpenBackup();
            using var directory = primary.OpenCase(record.CaseId);
            RequireIdentity(directory, active.Identity);
            using var eventHandles = new EvidenceHandles();
            directory.VerifyContents(active.Events.Select(entry => entry.Event.Name));
            foreach (var entry in active.Events)
                OpenWrittenEvent(directory, entry, eventHandles);
            var utc = ReadUtc(active.LastUtc);
            try
            {
                active.Events.Add(WriteEvent(primary, backup, directory, record, utc));
                active.LastRecord = record;
                active.LastUtc = utc;
                return EventReceipt(record.CaseId, active);
            }
            catch
            {
                _cases.Remove(record.CaseId);
                throw;
            }
        });

    public Task<MaintenanceEvidenceReceipt> FinalizeAndBackupAsync(string caseId, CancellationToken cancellationToken) =>
        AsTask(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var active = GetActive(caseId);
            _cases.Remove(caseId);
            var hashes = active.Events.Select(entry => entry.Event.Sha256);
            string? primaryManifestHash = null;
            string? backupManifestHash = null;
            try
            {
                using var primary = _policy.OpenPrimary();
                using var backup = _policy.OpenBackup();
                using var primaryCase = primary.OpenCase(caseId);
                RequireIdentity(primaryCase, active.Identity);
                var events = active.Events.Select(entry => entry.Event).ToArray();
                var names = events.Select(entry => entry.Name).ToArray();
                primaryCase.VerifyContents(names);

                using var backupCase = backup.OpenCase(caseId, create: true);
                using var eventHandles = new EvidenceHandles();
                var backupIds = new List<string>();
                foreach (var entry in active.Events)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    VerifyCasePair(primary, backup, primaryCase, backupCase);
                    var bytes = OpenWrittenEvent(primaryCase, entry, eventHandles);
                    VerifyCasePair(primary, backup, primaryCase, backupCase);
                    var copy = eventHandles.Add(backupCase.OpenFile(entry.Event.Name, create: true), entry.Event.Sha256);
                    copy.Write(bytes);
                    ReadVerified(copy, entry.Event.Sha256);
                    backupIds.Add(copy.Identity);
                }

                primaryCase.VerifyContents(names);
                backupCase.VerifyContents(names);
                cancellationToken.ThrowIfCancellationRequested();
                var utc = ReadUtc(active.LastUtc);
                var primaryId = Guid.NewGuid().ToString("N");
                var backupId = Guid.NewGuid().ToString("N");
                VerifyCasePair(primary, backup, primaryCase, backupCase);
                using var backupManifestFile = backupCase.OpenFile(ManifestName, create: true);
                VerifyCasePair(primary, backup, primaryCase, backupCase);
                using var primaryManifestFile = primaryCase.OpenFile(ManifestName, create: true);
                var backupManifest = new EvidenceManifest(1, caseId, utc, backupId, primaryId, backup.Identity, primary.Identity,
                    backupCase.Identity, primaryCase.Identity, backupManifestFile.Identity, primaryManifestFile.Identity,
                    MaintenanceEvidenceDelivery.Delivered.ToString(), events, backupIds.ToArray());
                var primaryManifest = new EvidenceManifest(1, caseId, utc, primaryId, backupId, primary.Identity, backup.Identity,
                    primaryCase.Identity, backupCase.Identity, primaryManifestFile.Identity, backupManifestFile.Identity,
                    MaintenanceEvidenceDelivery.Delivered.ToString(), events,
                    active.Events.Select(entry => entry.FileIdentity).ToArray());

                // Only a verified pair counts as delivered. The primary manifest
                // is the final marker, after the backup contents are durable.
                var finalNames = names.Append(ManifestName).ToArray();
                primaryCase.VerifyContents(finalNames);
                backupCase.VerifyContents(finalNames);
                VerifyCasePair(primary, backup, primaryCase, backupCase);
                eventHandles.Verify();
                backupManifestHash = WriteManifest(backupManifestFile, backupManifest);
                cancellationToken.ThrowIfCancellationRequested();
                primaryCase.VerifyContents(finalNames);
                backupCase.VerifyContents(finalNames);
                VerifyCasePair(primary, backup, primaryCase, backupCase);
                eventHandles.Verify();
                ReadVerified(backupManifestFile, backupManifestHash);
                primaryManifestHash = WriteManifest(primaryManifestFile, primaryManifest);
                _onCheckpoint?.Invoke(MaintenanceEvidenceCheckpoint.AfterPrimaryManifestWrite);
                VerifyCasePair(primary, backup, primaryCase, backupCase);
                primaryCase.VerifyContents(finalNames);
                backupCase.VerifyContents(finalNames);
                eventHandles.Verify();
                ReadVerified(backupManifestFile, backupManifestHash);
                ReadVerified(primaryManifestFile, primaryManifestHash);
                return new MaintenanceEvidenceReceipt(caseId, hashes, MaintenanceEvidenceDelivery.Delivered,
                    primaryManifestHash, backupManifestHash);
            }
            catch (Exception exception) when (IsEvidenceFailure(exception) || exception is OperationCanceledException)
            {
                return new MaintenanceEvidenceReceipt(caseId, hashes, MaintenanceEvidenceDelivery.Escalate,
                    primaryManifestHash, backupManifestHash);
            }
        });

    public Task<MaintenanceEvidencePurge> PurgeExpiredAsync(string primaryPath, string backupPath, string caseId,
        string primaryManifestHash, string backupManifestHash, CancellationToken cancellationToken) =>
        AsTask(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!MaintenanceEvidenceRecord.IsCaseId(caseId) ||
                !string.Equals(primaryPath, Path.Combine(_policy.PrimaryRoot, caseId), StringComparison.Ordinal) ||
                !string.Equals(backupPath, Path.Combine(_policy.BackupRoot, caseId), StringComparison.Ordinal) ||
                !MaintenanceEvidenceRecord.IsHash(primaryManifestHash) ||
                !MaintenanceEvidenceRecord.IsHash(backupManifestHash))
                return MaintenanceEvidencePurge.Denied;

            var failure = MaintenanceEvidencePurge.Denied;
            try
            {
                using var primaryRoot = _policy.OpenPrimary();
                using var backupRoot = _policy.OpenBackup();
                using var primary = OpenCopyForPurge(primaryRoot, caseId, primaryManifestHash);
                using var backup = OpenCopyForPurge(backupRoot, caseId, backupManifestHash);
                if (!AreReciprocal(primary.Manifest, backup.Manifest))
                    return MaintenanceEvidencePurge.Denied;
                var utc = ReadUtc();
                if (utc - primary.Manifest.Utc < _policy.Retention || utc - backup.Manifest.Utc < _policy.Retention)
                    return MaintenanceEvidencePurge.Denied;
                cancellationToken.ThrowIfCancellationRequested();

                failure = MaintenanceEvidencePurge.BackupUncertain;
                VerifyCasePair(primaryRoot, backupRoot, primary.Directory, backup.Directory);
                primary.Verify();
                backup.Verify();
                backup.Delete(backupRoot, primaryRoot, primary.Verify, _onCheckpoint, cancellationToken);

                failure = MaintenanceEvidencePurge.PrimaryUncertain;
                primary.Verify();
                primary.Delete(primaryRoot, backupRoot, () => backupRoot.VerifyCaseRemoved(caseId), _onCheckpoint, cancellationToken);
                return MaintenanceEvidencePurge.Purged;
            }
            catch (Exception exception) when (IsEvidenceFailure(exception) ||
                (failure != MaintenanceEvidencePurge.Denied && exception is OperationCanceledException))
            {
                return failure;
            }
        });

    private static PurgeCopy OpenCopyForPurge(EvidenceRoot root, string caseId, string expectedHash)
    {
        var directory = root.OpenCase(caseId, delete: true);
        var files = new List<PurgeFile>();
        try
        {
            var manifestFile = directory.OpenFile(ManifestName, delete: true);
            files.Add(new PurgeFile(ManifestName, expectedHash, manifestFile));
            var bytes = ReadVerified(manifestFile, expectedHash);
            var manifest = JsonSerializer.Deserialize<EvidenceManifest>(bytes, JsonOptions);
            if (manifest == null || !bytes.SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions)) ||
                !IsManifestForCopy(manifest, caseId, root, directory, manifestFile))
                throw new IOException("Evidence manifest does not identify the exact copy.");

            for (var index = 0; index < manifest.Events.Length; index++)
            {
                var item = manifest.Events[index];
                var entry = directory.OpenFile(item.Name, delete: true);
                files.Add(new PurgeFile(item.Name, item.Sha256, entry));
                RequireIdentity(entry, manifest.EventFileIds[index]);
            }
            var copy = new PurgeCopy(directory, manifest, files);
            copy.Verify();
            return copy;
        }
        catch
        {
            foreach (var file in files)
                file.Entry.Dispose();
            directory.Dispose();
            throw;
        }
    }

    private static bool IsManifestForCopy(EvidenceManifest manifest, string caseId, EvidenceRoot root,
        EvidenceEntry directory, EvidenceEntry manifestFile)
    {
        if (manifest.Version != 1 || manifest.CaseId != caseId || manifest.Utc.Offset != TimeSpan.Zero ||
            manifest.Utc == default || manifest.RootIdentity != root.Identity || manifest.CaseIdentity != directory.Identity ||
            manifest.ManifestFileIdentity != manifestFile.Identity || manifest.CopyId == manifest.PeerCopyId ||
            !Guid.TryParseExact(manifest.CopyId, "N", out _) || !Guid.TryParseExact(manifest.PeerCopyId, "N", out _) ||
            manifest.Delivery != MaintenanceEvidenceDelivery.Delivered.ToString() ||
            manifest.Events == null || manifest.Events.Length < 1 || manifest.Events.Length > 3 ||
            manifest.EventFileIds == null || manifest.EventFileIds.Length != manifest.Events.Length)
            return false;

        var lastOrder = 0;
        foreach (var item in manifest.Events)
        {
            if (item == null || !MaintenanceEvidenceRecord.IsHash(item.Sha256))
                return false;
            var order = item.Name switch
            {
                "01-attempt.json" => 1,
                "02-transaction.json" => 2,
                "03-readback.json" => 3,
                _ => 0
            };
            if (order <= lastOrder || (lastOrder == 0 && order != 1))
                return false;
            lastOrder = order;
        }
        return true;
    }

    private static bool AreReciprocal(EvidenceManifest primary, EvidenceManifest backup) =>
        primary.Utc == backup.Utc && primary.CopyId == backup.PeerCopyId && backup.CopyId == primary.PeerCopyId &&
        primary.RootIdentity == backup.PeerRootIdentity && backup.RootIdentity == primary.PeerRootIdentity &&
        primary.CaseIdentity == backup.PeerCaseIdentity && backup.CaseIdentity == primary.PeerCaseIdentity &&
        primary.ManifestFileIdentity == backup.PeerManifestFileIdentity && backup.ManifestFileIdentity == primary.PeerManifestFileIdentity &&
        primary.Events.Select(item => (item.Name, item.Sha256)).SequenceEqual(backup.Events.Select(item => (item.Name, item.Sha256)));

    private Task<T> AsTask<T>(Func<T> operation)
    {
        try
        {
            lock (_gate)
                return Task.FromResult(operation());
        }
        catch (OperationCanceledException exception) when (exception.CancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            return Task.FromException<T>(exception);
        }
    }

    private ActiveCase GetActive(string caseId)
    {
        if (!MaintenanceEvidenceRecord.IsCaseId(caseId) || !_cases.TryGetValue(caseId, out var active))
            throw new IOException("No active evidence case matches the requested identity.");
        return active;
    }

    private DateTimeOffset ReadUtc(DateTimeOffset? previous = null)
    {
        var utc = _utcNow();
        if (utc.Offset != TimeSpan.Zero || (previous.HasValue && utc < previous.Value))
            throw new IOException("Evidence UTC is unavailable or has moved backwards.");
        return utc;
    }

    private static bool IsEvidenceFailure(Exception exception) =>
        exception is IOException || exception is UnauthorizedAccessException || exception is JsonException;

    private static MaintenanceEvidenceReceipt EventReceipt(string caseId, ActiveCase active) =>
        new MaintenanceEvidenceReceipt(caseId, active.Events.Select(entry => entry.Event.Sha256), MaintenanceEvidenceDelivery.PrimaryOnly);

    private static WrittenEvent WriteEvent(EvidenceRoot primary, EvidenceRoot backup, EvidenceEntry directory,
        MaintenanceEvidenceRecord record, DateTimeOffset utc)
    {
        var name = record.Phase switch
        {
            MaintenanceEvidencePhase.Attempt => "01-attempt.json",
            MaintenanceEvidencePhase.Transaction => "02-transaction.json",
            MaintenanceEvidencePhase.Readback => "03-readback.json",
            _ => throw new IOException("Evidence phase is invalid.")
        };
        var bytes = record.Serialize(utc);
        primary.Verify();
        backup.Verify();
        directory.Verify();
        using var entry = directory.OpenFile(name, create: true);
        entry.Write(bytes);
        var hash = MaintenanceEvidenceRecord.Hash(bytes);
        ReadVerified(entry, hash);
        return new WrittenEvent(new ManifestEvent(name, hash), entry.Identity);
    }

    private static string WriteManifest(EvidenceEntry file, EvidenceManifest manifest)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        var hash = MaintenanceEvidenceRecord.Hash(bytes);
        file.Write(bytes);
        ReadVerified(file, hash);
        return hash;
    }

    private static byte[] ReadVerified(EvidenceEntry file, string expectedHash)
    {
        var bytes = file.Read(MaximumEvidenceBytes);
        if (MaintenanceEvidenceRecord.Hash(bytes) != expectedHash)
            throw new IOException("Evidence bytes do not match the expected hash.");
        return bytes;
    }

    private static byte[] OpenWrittenEvent(EvidenceEntry directory, WrittenEvent entry, EvidenceHandles handles)
    {
        var file = handles.Add(directory.OpenFile(entry.Event.Name), entry.Event.Sha256);
        RequireIdentity(file, entry.FileIdentity);
        return ReadVerified(file, entry.Event.Sha256);
    }

    private static void RequireIdentity(EvidenceEntry file, string expectedIdentity)
    {
        file.Verify();
        if (file.Identity != expectedIdentity)
            throw new IOException("Evidence creation identity does not match.");
    }

    private static void VerifyCasePair(EvidenceRoot primary, EvidenceRoot backup, EvidenceEntry primaryCase, EvidenceEntry backupCase)
    {
        primary.Verify();
        backup.Verify();
        primaryCase.Verify();
        backupCase.Verify();
    }

    private sealed class ActiveCase
    {
        internal ActiveCase(string identity, MaintenanceEvidenceRecord attempt, DateTimeOffset utc)
        {
            Identity = identity;
            LastRecord = attempt;
            LastUtc = utc;
        }

        internal string Identity { get; }
        internal MaintenanceEvidenceRecord LastRecord { get; set; }
        internal DateTimeOffset LastUtc { get; set; }
        internal List<WrittenEvent> Events { get; } = new List<WrittenEvent>();
    }

    private sealed class WrittenEvent(ManifestEvent @event, string fileIdentity)
    {
        internal ManifestEvent Event { get; } = @event;
        internal string FileIdentity { get; } = fileIdentity;
    }

    private sealed class EvidenceHandles : IDisposable
    {
        private readonly List<(EvidenceEntry Entry, string Hash)> _files = new List<(EvidenceEntry, string)>();

        internal EvidenceEntry Add(EvidenceEntry file, string hash)
        {
            _files.Add((file, hash));
            return file;
        }

        internal void Verify()
        {
            foreach (var file in _files)
                ReadVerified(file.Entry, file.Hash);
        }

        public void Dispose()
        {
            foreach (var file in _files)
                file.Entry.Dispose();
        }
    }

    private sealed class ManifestEvent(string name, string sha256)
    {
        public string Name { get; } = name;
        public string Sha256 { get; } = sha256;
    }

    private sealed class EvidenceManifest(int version, string caseId, DateTimeOffset utc, string copyId,
        string peerCopyId, string rootIdentity, string peerRootIdentity, string caseIdentity, string peerCaseIdentity,
        string manifestFileIdentity, string peerManifestFileIdentity, string delivery, ManifestEvent[] events, string[] eventFileIds)
    {
        public int Version { get; } = version;
        public string CaseId { get; } = caseId;
        public DateTimeOffset Utc { get; } = utc;
        public string CopyId { get; } = copyId;
        public string PeerCopyId { get; } = peerCopyId;
        public string RootIdentity { get; } = rootIdentity;
        public string PeerRootIdentity { get; } = peerRootIdentity;
        public string CaseIdentity { get; } = caseIdentity;
        public string PeerCaseIdentity { get; } = peerCaseIdentity;
        public string ManifestFileIdentity { get; } = manifestFileIdentity;
        public string PeerManifestFileIdentity { get; } = peerManifestFileIdentity;
        public string Delivery { get; } = delivery;
        public ManifestEvent[] Events { get; } = events;
        public string[] EventFileIds { get; } = eventFileIds;
    }

    private sealed class PurgeFile(string name, string hash, EvidenceEntry entry)
    {
        internal string Name { get; } = name;
        internal string Hash { get; } = hash;
        internal EvidenceEntry Entry { get; } = entry;
    }

    private sealed class PurgeCopy(EvidenceEntry directory, EvidenceManifest manifest, List<PurgeFile> files) : IDisposable
    {
        internal EvidenceEntry Directory { get; } = directory;
        internal EvidenceManifest Manifest { get; } = manifest;

        internal void Verify() => Verify(files);

        private void Verify(IEnumerable<PurgeFile> remaining)
        {
            Directory.VerifyContents(remaining.Select(file => file.Name));
            foreach (var file in remaining)
                ReadVerified(file.Entry, file.Hash);
        }

        internal void Delete(EvidenceRoot root, EvidenceRoot peer, Action verifyPeer,
            Action<MaintenanceEvidenceCheckpoint>? onCheckpoint, CancellationToken cancellationToken)
        {
            // Keep the final manifest until after the named event files, so an
            // interrupted delete retains its case and disposition metadata.
            var order = files.Skip(1).Append(files[0]).ToArray();
            for (var index = 0; index < order.Length; index++)
            {
                var file = order[index];
                cancellationToken.ThrowIfCancellationRequested();
                onCheckpoint?.Invoke(MaintenanceEvidenceCheckpoint.BeforePurgeEntryVerification);
                root.Verify();
                peer.Verify();
                verifyPeer();
                Verify(order.Skip(index));
                file.Entry.Delete();
            }
            cancellationToken.ThrowIfCancellationRequested();
            root.Verify();
            peer.Verify();
            verifyPeer();
            Directory.VerifyContents(Array.Empty<string>());
            Directory.Delete();
            root.VerifyCaseRemoved(Manifest.CaseId);
        }

        public void Dispose()
        {
            foreach (var file in files)
                file.Entry.Dispose();
            Directory.Dispose();
        }
    }
}
