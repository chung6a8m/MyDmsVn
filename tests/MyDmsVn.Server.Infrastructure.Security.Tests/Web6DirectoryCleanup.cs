using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal interface IWeb6RecoveryCleanup : IDisposable
{
    void Verify();
    Task DeleteSqlResources();
    void DeleteEvidence();
}

internal static class Web6RecoveryCleanupDisposition
{
    internal static async Task Execute(IReadOnlyList<Web6PrivateDirectoryLease> runtimeRoots, Func<Task<IWeb6RecoveryCleanup>> prepareRecovery)
    {
        var runtime = new List<Web6DirectoryCleanup>();
        try
        {
            foreach (var root in runtimeRoots) runtime.Add(root.PrepareCleanup());
            using var recovery = await prepareRecovery();
            recovery.Verify();
            foreach (var tree in runtime) tree.Verify();
            // SQL DROP is not transactional across databases. Preserve every diagnostic
            // until all SQL cleanup and physical-file absence checks have succeeded.
            await recovery.DeleteSqlResources();
            recovery.Verify();
            foreach (var tree in runtime) tree.Verify();
            recovery.DeleteEvidence();
            foreach (var tree in runtime.AsEnumerable().Reverse()) tree.Delete();
        }
        finally
        {
            foreach (var tree in runtime.AsEnumerable().Reverse()) tree.Dispose();
            foreach (var root in runtimeRoots) root.Dispose();
        }
    }
}

// Preflight is non-destructive. Every evidence child stays open with DELETE access,
// no write/delete sharing, until the owner chooses one verified disposition.
internal sealed class Web6DirectoryCleanup : IDisposable
{
    private readonly Web6PrivateDirectoryLease _lease;
    private readonly List<Entry> _entries = new List<Entry>();
    private readonly HashSet<string> _sqlFiles;
    private readonly Dictionary<string, Web6CandidateFileSystem.ChildIdentity> _external = new Dictionary<string, Web6CandidateFileSystem.ChildIdentity>(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    internal Web6DirectoryCleanup(Web6PrivateDirectoryLease lease, IEnumerable<string> sqlFiles, Action<string>? beforeOpen)
    {
        _lease = lease;
        _sqlFiles = new HashSet<string>(sqlFiles, StringComparer.OrdinalIgnoreCase);
        if (_sqlFiles.Any(path => Path.GetDirectoryName(path) != lease.Path))
            throw new IOException("SQL-owned physical files must be direct recovery children.");
        try { lease.Verify(); Pin(lease.RetainedHandle, lease.Path, beforeOpen); Verify(); }
        catch { Dispose(); throw; }
    }

    private void Pin(SafeFileHandle parent, string path, Action<string>? beforeOpen)
    {
        foreach (var observed in Web6CandidateFileSystem.EnumerateRelative(parent))
        {
            var childPath = Path.Combine(path, observed.Name);
            beforeOpen?.Invoke(childPath);
            if (_sqlFiles.Contains(childPath))
            {
                if (observed.Directory) throw new IOException("SQL physical file became a directory.");
                _external.Add(childPath, observed);
                continue; // SQL owns these handles and deletes them through DROP DATABASE.
            }
            var handle = Web6CandidateFileSystem.OpenRelativeForDelete(parent, observed.Name);
            var entry = new Entry(childPath, handle, observed);
            _entries.Add(entry); // Retain even if verification throws; Dispose releases every handle.
            entry.Verify();
            if (observed.Directory) Pin(handle, childPath, beforeOpen);
        }
    }

    internal void Verify()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(Web6DirectoryCleanup));
        _lease.Verify();
        foreach (var entry in _entries) entry.Verify();
        VerifyChildren(_lease.Path, _lease.RetainedHandle);
        foreach (var entry in _entries.Where(entry => entry.Observed.Directory)) VerifyChildren(entry.Path, entry.Handle);
    }

    private void VerifyChildren(string parent, SafeFileHandle handle)
    {
        var expected = _entries.Where(entry => Path.GetDirectoryName(entry.Path) == parent)
            .ToDictionary(entry => entry.Observed.Name, entry => entry.Observed, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _external.Where(entry => Path.GetDirectoryName(entry.Key) == parent))
            expected.Add(entry.Value.Name, entry.Value);
        foreach (var child in Web6CandidateFileSystem.EnumerateRelative(handle))
        {
            if (!expected.TryGetValue(child.Name, out var original) || !child.Same(original))
                throw new IOException("Cleanup child inventory or native identity changed.");
            expected.Remove(child.Name);
        }
        if (expected.Count != 0) throw new IOException("Cleanup child disappeared.");
    }

    internal void VerifySeals(IReadOnlyDictionary<string, (DateTime Created, string Hash)> files,
        IReadOnlyDictionary<string, DateTime> directories)
    {
        Verify();
        if (!_entries.Where(entry => !entry.Observed.Directory).Select(entry => entry.Path).OrderBy(path => path)
                .SequenceEqual(files.Keys.OrderBy(path => path)) ||
            !_entries.Where(entry => entry.Observed.Directory).Select(entry => entry.Path).OrderBy(path => path)
                .SequenceEqual(directories.Keys.OrderBy(path => path)))
            throw new IOException("Recovery cleanup inventory differs from its ownership manifest.");
        foreach (var entry in _entries)
        {
            if (entry.Observed.Directory)
            {
                if (entry.Observed.Created != directories[entry.Path]) throw new IOException("Owned evidence directory changed.");
            }
            else
            {
                var seal = files[entry.Path];
                using var stream = new FileStream(new SafeFileHandle(entry.Handle.DangerousGetHandle(), false), FileAccess.Read);
                using var sha = SHA256.Create();
                stream.Position = 0;
                var hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                if (seal.Created != entry.Observed.Created || seal.Hash != hash) throw new IOException("Owned evidence file changed.");
            }
        }
    }

    internal void VerifySqlFilesRemoved()
    {
        // Check by the retained recovery directory; never follow a substituted absolute path.
        var children = Web6CandidateFileSystem.EnumerateRelative(_lease.RetainedHandle);
        if (children.Any(child => _sqlFiles.Contains(Path.Combine(_lease.Path, child.Name))))
            throw new IOException("Restored SQL physical file remains.");
        _external.Clear();
        Verify();
    }

    internal void Delete()
    {
        Verify();
        if (_external.Count != 0) throw new IOException("SQL physical file removal must be verified first.");
        foreach (var entry in _entries.AsEnumerable().Reverse())
        {
            Web6CandidateFileSystem.MarkDirectoryForDeletion(entry.Handle);
            entry.Handle.Dispose();
        }
        _lease.DeleteVerified();
        Dispose();
    }

    public void Dispose()
    {
        foreach (var entry in _entries.AsEnumerable().Reverse()) entry.Handle.Dispose();
        _disposed = true;
    }

    private sealed class Entry
    {
        internal Entry(string path, SafeFileHandle handle, Web6CandidateFileSystem.ChildIdentity observed)
        { Path = path; Handle = handle; Observed = observed; }
        internal string Path { get; }
        internal SafeFileHandle Handle { get; }
        internal Web6CandidateFileSystem.ChildIdentity Observed { get; }
        internal void Verify() => Web6CandidateFileSystem.VerifyChild(Handle, Observed);
    }
}

internal static partial class Web6CandidateFileSystem
{
    internal sealed class ChildIdentity
    {
        internal ChildIdentity(string name, long id, long created, FileAttributes attributes)
        { Name = name; Id = id; Created = DateTime.FromFileTimeUtc(created); Attributes = attributes; }
        internal string Name { get; }
        internal long Id { get; }
        internal DateTime Created { get; }
        internal FileAttributes Attributes { get; }
        internal bool Directory => (Attributes & FileAttributes.Directory) != 0;
        internal bool Same(ChildIdentity other) => Id == other.Id && Created == other.Created && Attributes == other.Attributes;
    }

    internal static ChildIdentity[] EnumerateRelative(SafeFileHandle parent)
    {
        const int capacity = 65536, nameOffset = 104;
        var result = new List<ChildIdentity>();
        var buffer = Marshal.AllocHGlobal(capacity);
        try
        {
            var infoClass = 11; // FileIdBothDirectoryRestartInfo / FileIdBothDirectoryInfo
            while (GetFileInformationByHandleEx(parent, infoClass, buffer, capacity))
            {
                infoClass = 10;
                var offset = 0;
                while (true)
                {
                    var next = Marshal.ReadInt32(buffer, offset);
                    var length = Marshal.ReadInt32(buffer, offset + 60);
                    if (length <= 0 || length % 2 != 0 || length > capacity - offset - nameOffset)
                        throw new IOException("Cleanup enumeration is invalid.");
                    var name = Marshal.PtrToStringUni(IntPtr.Add(buffer, offset + nameOffset), length / 2)!;
                    if (name != "." && name != "..")
                    {
                        var attributes = (FileAttributes)Marshal.ReadInt32(buffer, offset + 56);
                        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Device | FileAttributes.ReadOnly)) != 0)
                            throw new IOException("Cleanup refuses reparse, device or readonly children.");
                        result.Add(new ChildIdentity(name, Marshal.ReadInt64(buffer, offset + 96), Marshal.ReadInt64(buffer, offset + 8), attributes));
                    }
                    if (next == 0) break;
                    if (next < nameOffset + length || next > capacity - offset - nameOffset) throw new IOException("Cleanup enumeration is invalid.");
                    offset += next;
                }
            }
            if (Marshal.GetLastWin32Error() != 18) throw new IOException("Cleanup enumeration failed.");
            return result.ToArray();
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    internal static void VerifyChild(SafeFileHandle handle, ChildIdentity expected)
    {
        _ = Identity(handle, expected.Directory);
        if (!GetFileInformationByHandle(handle, out var info) ||
            (((long)info.IndexHigh << 32) | info.IndexLow) != expected.Id ||
            DateTime.FromFileTimeUtc(((long)info.CreatedHigh << 32) | info.CreatedLow) != expected.Created ||
            info.Attributes != expected.Attributes)
            throw new IOException("Cleanup child native identity or attributes changed.");
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int infoClass, IntPtr buffer, int size);
}
