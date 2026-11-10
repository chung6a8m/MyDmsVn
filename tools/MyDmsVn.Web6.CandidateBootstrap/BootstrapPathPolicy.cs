using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal sealed class BootstrapPathPolicy : IDisposable
{
    internal static readonly IReadOnlyList<string> RunRootNames = Array.AsReadOnly(new[] { "pair-chromium", "pair-edge", "cutover", "workload", "recovery" });
    private readonly List<PinnedDirectory> _directories = new();
    private readonly string _taskRoot;

    internal BootstrapPathPolicy(BootstrapRequest request, BootstrapAuthority authority)
    {
        _taskRoot = request.TaskRoot;
        if (Path.GetDirectoryName(_taskRoot) != authority.TaskParent)
            throw new IOException("Bootstrap task root must be a new direct child of its approved parent.");
        BootstrapJson.RequireSeparateRoots(authority.TaskParent, request.BackendRepository, request.FrontendRepository, request.SqlControlParent);
        try
        {
            foreach (var path in new[] { authority.TaskParent, request.BackendRepository, request.FrontendRepository, request.SqlControlParent })
                _directories.Add(new PinnedDirectory(path));
            if (Directory.EnumerateFileSystemEntries(authority.TaskParent).Any(path => BootstrapJson.SamePath(path, _taskRoot)))
                throw new IOException("Bootstrap task root already exists.");
            Verify();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal void CreateTaskRoot() => _directories.Add(new PinnedDirectory(_taskRoot, create: true));

    internal string CreateChild(string name)
    {
        var path = Path.Combine(_taskRoot, name);
        _directories.Add(new PinnedDirectory(path, create: true));
        return path;
    }

    internal void Verify()
    {
        foreach (var directory in _directories) directory.Verify();
    }

    internal void VerifyRunRootsAreNew()
    {
        var existing = Directory.EnumerateFileSystemEntries(_taskRoot).Select(Path.GetFileName);
        if (existing.Intersect(RunRootNames, StringComparer.OrdinalIgnoreCase).Any())
            throw new IOException("Bootstrap live roots must remain unused during preparation.");
    }

    public void Dispose()
    {
        for (var index = _directories.Count - 1; index >= 0; index--) _directories[index].Dispose();
    }

    internal static string HashFile(string path) => ReadFile(path, stream =>
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }).Value;

    internal static (T Value, string Identity) ReadFile<T>(string path, Func<FileStream, T> read, string? expectedIdentity = null)
    {
        using var parents = new ProtectedJsonFile.PathLease(path);
        using var file = parents.OpenRead();
        var identity = ProtectedJsonFile.Inspect(file.SafeFileHandle, path, directory: false);
        if (expectedIdentity != null && identity.ToString() != expectedIdentity)
            throw new IOException("Bootstrap file identity differs from the measured artifact.");
        var value = read(file);
        using var fresh = parents.OpenRead();
        if (identity != ProtectedJsonFile.Inspect(file.SafeFileHandle, path, directory: false) ||
            identity != ProtectedJsonFile.Inspect(fresh.SafeFileHandle, path, directory: false))
            throw new IOException("Bootstrap file identity changed while measuring it.");
        return (value, identity.ToString());
    }

    internal static void CopyNewFile(string source, string destination, string? expectedIdentity = null)
    {
        ReadFile(source, input =>
        {
            using var parent = new ProtectedJsonFile.PathLease(destination);
            var acl = new FileSecurity();
            acl.SetOwner(ProtectedJsonFile.CurrentOwner());
            acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            acl.AddAccessRule(new FileSystemAccessRule(ProtectedJsonFile.CurrentOwner(), FileSystemRights.FullControl, AccessControlType.Allow));
            using var handle = parent.CreateNewFile(acl);
            using var output = new FileStream(handle, FileAccess.Write);
            input.CopyTo(output);
            output.Flush(flushToDisk: true);
            return true;
        }, expectedIdentity);
    }

    internal sealed class PinnedDirectory : IDisposable
    {
        private readonly string _path;
        private readonly ProtectedJsonFile.PathLease _parents;
        private readonly SafeFileHandle _handle;
        internal string Identity { get; }

        internal PinnedDirectory(string path, bool create = false)
        {
            _path = BootstrapJson.AbsolutePath(path);
            _parents = new ProtectedJsonFile.PathLease(path);
            try
            {
                _handle = _parents.OpenDirectory(create ? PrivateAcl() : null);
                try { Identity = Inspect(_handle); }
                catch { _handle.Dispose(); throw; }
            }
            catch { _parents.Dispose(); throw; }
        }

        internal void Verify()
        {
            using var fresh = _parents.OpenDirectory();
            if (Inspect(_handle) != Identity || Inspect(fresh) != Identity)
                throw new IOException("Bootstrap directory identity changed.");
        }

        private string Inspect(SafeFileHandle handle) => ProtectedJsonFile.Inspect(handle, _path, directory: true).ToString();

        private static DirectorySecurity PrivateAcl()
        {
            var owner = ProtectedJsonFile.CurrentOwner();
            var acl = new DirectorySecurity();
            acl.SetOwner(owner);
            acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            acl.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            return acl;
        }

        public void Dispose()
        {
            _handle.Dispose();
            _parents.Dispose();
        }
    }
}

internal sealed class BootstrapTree
{
    private readonly SortedDictionary<string, (bool Directory, string Identity, string Digest)> _entries = new(StringComparer.Ordinal);
    internal string Root { get; }
    internal IReadOnlyDictionary<string, string> Files { get; }
    internal string Digest { get; }

    internal BootstrapTree(string root)
    {
        Root = root;
        Visit(root, "");
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in _entries.Where(entry => !entry.Value.Directory)) files.Add(entry.Key, entry.Value.Digest);
        if (files.Count == 0) throw new IOException("Bootstrap artifact tree is empty.");
        Files = files;
        // Match Web6CandidateFileSet.MeasureTree exactly for the selector inputs.
        Digest = ProtectedJsonFile.Digest(Encoding.ASCII.GetBytes(string.Concat(Files.Select(entry =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(entry.Key)) + "=" + entry.Value + "\n"))));
    }

    private void Visit(string path, string name)
    {
        if (path.Length > 240) throw new IOException("Bootstrap artifact path exceeds the candidate selector budget.");
        using var directory = new BootstrapPathPolicy.PinnedDirectory(path);
        _entries.Add(name, (true, directory.Identity, ""));
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in Directory.EnumerateFileSystemEntries(path))
        {
            var basename = Path.GetFileName(child);
            if (!names.Add(basename)) throw new IOException("Bootstrap artifact contains an ambiguous path.");
            var relative = name.Length == 0 ? basename : name + "/" + basename;
            if (child.Length > 240) throw new IOException("Bootstrap artifact path exceeds the candidate selector budget.");
            var attributes = File.GetAttributes(child);
            if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
                throw new IOException("Bootstrap artifact contains a reparse point or device.");
            directory.Verify();
            if ((attributes & FileAttributes.Directory) != 0)
            {
                Visit(child, relative);
                continue;
            }
            var (digest, identity) = BootstrapPathPolicy.ReadFile(child, file =>
            {
                using var sha = SHA256.Create();
                return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
            });
            _entries.Add(relative, (false, identity, digest));
        }
        directory.Verify();
    }

    internal void Verify()
    {
        if (!_entries.SequenceEqual(new BootstrapTree(Root)._entries))
            throw new IOException("Bootstrap artifact bytes or identities changed after measurement.");
    }

    internal T ReadFile<T>(string relativePath, Func<FileStream, T> read) => BootstrapPathPolicy.ReadFile(
        Path.Combine(Root, relativePath.Replace('/', '\\')), read, _entries[relativePath].Identity).Value;

    internal void VerifyExtension(BootstrapTree next, string child)
    {
        if (!_entries.SequenceEqual(next._entries.Where(entry => entry.Key != child &&
            !entry.Key.StartsWith(child + "/", StringComparison.Ordinal))))
            throw new IOException("Bootstrap host files or identities changed while composing the publish tree.");
    }

    internal void CopyTo(string path)
    {
        using var target = new BootstrapPathPolicy.PinnedDirectory(path, create: true);
        CopyChildren(path, "");
        target.Verify();
        Verify();
        var copy = new BootstrapTree(path);
        if (Digest != copy.Digest || !_entries.Keys.SequenceEqual(copy._entries.Keys))
            throw new IOException("Bootstrap copied artifact differs from source.");
    }

    private void CopyChildren(string target, string prefix)
    {
        foreach (var entry in _entries.Where(entry => entry.Key.Length > prefix.Length &&
            entry.Key.StartsWith(prefix, StringComparison.Ordinal) && entry.Key.IndexOf('/', prefix.Length) < 0))
        {
            var destination = Path.Combine(target, entry.Key.Substring(prefix.Length));
            if (entry.Value.Directory)
            {
                using var directory = new BootstrapPathPolicy.PinnedDirectory(destination, create: true);
                CopyChildren(destination, entry.Key + "/");
                directory.Verify();
            }
            else
            {
                BootstrapPathPolicy.CopyNewFile(Path.Combine(Root, entry.Key.Replace('/', '\\')), destination, entry.Value.Identity);
            }
        }
    }
}
