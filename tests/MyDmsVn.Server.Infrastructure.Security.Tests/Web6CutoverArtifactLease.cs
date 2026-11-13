using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32.SafeHandles;
using MyDmsVn.Web6.CandidateBootstrap;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal sealed class Web6CutoverArtifactLease : IDisposable
{
    private readonly string _root;
    private readonly Dictionary<string, BootstrapPathPolicy.PinnedDirectory> _directories = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, (FileStream Stream, string Hash)> _files = new(StringComparer.Ordinal);

    internal Web6CutoverArtifactLease(string root)
    {
        _root = Web6CandidateFileSystem.CanonicalPath(root);
        try { Visit("", _root); }
        catch { Dispose(); throw; }
    }

    internal string Digest(string prefix = "") => DigestEntries(FilesUnder(prefix).Select(file => (file.Name, file.Hash)));

    internal string DeploymentDigest(Web6CutoverArtifactLease frontend, string frontendPrefix) => DigestEntries(
        FilesUnder("").Where(file => !file.Name.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase))
            .Select(file => (file.Name, file.Hash))
            .Concat(frontend.FilesUnder(frontendPrefix).Select(file => ("wwwroot/" + file.Name, file.Hash)))
            .OrderBy(file => file.Item1, StringComparer.Ordinal));

    private IEnumerable<(string Name, FileStream Stream, string Hash)> FilesUnder(string prefix) =>
        _files.Where(entry => entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(entry => (entry.Key.Substring(prefix.Length), entry.Value.Stream, entry.Value.Hash));

    private static string DigestEntries(IEnumerable<(string Name, string Hash)> entries)
    {
        var manifest = string.Concat(entries.Select(entry =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(entry.Name)) + "=" + entry.Hash + "\n"));
        if (manifest.Length == 0) throw new IOException("Cutover artifact inventory is empty.");
        return ProtectedJsonFile.Digest(Encoding.ASCII.GetBytes(manifest));
    }

    private void Visit(string relative, string path)
    {
        var directory = new BootstrapPathPolicy.PinnedDirectory(path);
        _directories.Add(relative, directory);
        foreach (var child in Children(path))
        {
            var name = relative + Path.GetFileName(child);
            var attributes = Web6CandidateFileSystem.Attributes(child);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                Visit(name + "/", child);
                continue;
            }
            // PathLease opens each component relative to its admitted parent and rejects reparses.
            // The stream shares reads only and is retained until the caller finishes using the inventory.
            using var parents = new ProtectedJsonFile.PathLease(child);
            var stream = parents.OpenRead();
            try { _files.Add(name, (stream, BootstrapPathPolicy.HashStream(stream))); }
            catch { stream.Dispose(); throw; }
        }
    }

    internal void Verify()
    {
        foreach (var entry in _directories)
        {
            var relative = entry.Key;
            entry.Value.Verify();
            foreach (var child in Children(ToPath(relative)))
            {
                var name = relative + Path.GetFileName(child);
                if (!_files.ContainsKey(name) && !_directories.ContainsKey(name + "/"))
                    throw new IOException("Cutover artifact inventory gained an unsealed entry.");
            }
        }
        foreach (var entry in _files)
            ProtectedJsonFile.Inspect(entry.Value.Stream.SafeFileHandle, ToPath(entry.Key), directory: false);
    }

    internal void CopyFiles(string destination, SafeFileHandle target, string prefix = "", string? omitPrefix = null)
    {
        var owner = ProtectedJsonFile.CurrentOwner();
        var directoryAcl = SqlTestPrivateRecord.DirectoryAcl(owner);
        var fileAcl = SqlTestPrivateRecord.FileAcl(owner);
        var directories = new Dictionary<string, SafeFileHandle>(StringComparer.Ordinal) { [destination] = target };
        try
        {
            foreach (var file in FilesUnder(prefix))
            {
                if (omitPrefix != null && file.Name.StartsWith(omitPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                var outputPath = Path.Combine(destination, file.Name.Replace('/', Path.DirectorySeparatorChar));
                using var handle = ProtectedJsonFile.CreateChildFile(GetDirectory(Path.GetDirectoryName(outputPath)!), outputPath, fileAcl);
                using var output = new FileStream(handle, FileAccess.Write);
                file.Stream.Position = 0;
                file.Stream.CopyTo(output);
                output.Flush(flushToDisk: true);
            }
        }
        finally
        {
            foreach (var directory in directories.Values.Reverse())
                if (!ReferenceEquals(directory, target)) directory.Dispose();
        }

        SafeFileHandle GetDirectory(string path)
        {
            if (directories.TryGetValue(path, out var existing)) return existing;
            var directory = ProtectedJsonFile.CreateChildDirectory(GetDirectory(Path.GetDirectoryName(path)!), path, directoryAcl);
            directories.Add(path, directory);
            return directory;
        }
    }

    private string ToPath(string relative) => relative.Length == 0 ? _root :
        Path.Combine(_root, relative.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));

    private static string[] Children(string directory)
    {
        var children = Directory.GetFileSystemEntries(directory);
        if (children.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != children.Length)
            throw new IOException("Cutover artifact inventory contains ambiguous paths.");
        return children;
    }

    public void Dispose()
    {
        foreach (var entry in _files.Values) entry.Stream.Dispose();
        foreach (var directory in _directories.Values.Reverse()) directory.Dispose();
    }
}
