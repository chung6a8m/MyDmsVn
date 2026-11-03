using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
// Private operator-only record. Never remove on uncertain trigger/SQL cleanup.
internal sealed class Web5OriginalRaceManifest
{
    private readonly Dictionary<Guid, bool> _records = new Dictionary<Guid, bool>();
    internal string DirectoryPath { get; }
    private Web5OriginalRaceManifest(string directory) { DirectoryPath = directory; }
    internal static Web5OriginalRaceManifest Create(string parent)
    {
        using var user = WindowsIdentity.GetCurrent(); Web5AttendedLease.VerifyPrivateDirectory(parent, user.User!);
        var path = Path.Combine(parent, "race-" + Guid.NewGuid().ToString("N"));
        var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false); acl.SetOwner(user.User!);
        acl.AddAccessRule(new FileSystemAccessRule(user.User!, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        Directory.CreateDirectory(path, acl); Web5AttendedLease.VerifyPrivateDirectory(path, user.User!);
        return new Web5OriginalRaceManifest(path);
    }
    internal void Record(string database, Guid operation, string trigger)
    {
        using var user = WindowsIdentity.GetCurrent(); Web5AttendedLease.VerifyPrivateDirectory(DirectoryPath, user.User!);
        SqlTestDatabaseName.EnsureDisposable(database);
        var text = operation.ToString("D");
        if (!Regex.IsMatch(text, @"\A[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\z") ||
            !Regex.IsMatch(trigger, @"\AWeb5Race_[0-9a-f]{32}\z") || _records.ContainsKey(operation) || _records.Count >= 6)
            throw new InvalidDataException("Invalid original-first manifest identity.");
        var path = Path.Combine(DirectoryPath, operation.ToString("N") + ".txt");
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
            { writer.Write("database=" + database + "\noperation=" + text + "\ntrigger=" + trigger + "\n"); writer.Flush(); }
            stream.Flush(true);
        }
        _records.Add(operation, false);
    }
    internal void MarkClean(Guid key)
    {
        if (!_records.TryGetValue(key, out var clean) || clean) throw new InvalidDataException("Original-first cleanup identity is invalid.");
        _records[key] = true;
    }
    internal void Complete()
    {
        using var user = WindowsIdentity.GetCurrent(); Web5AttendedLease.VerifyPrivateDirectory(DirectoryPath, user.User!);
        if (_records.Values.Any(clean => !clean)) throw new InvalidDataException("Original-first cleanup incomplete; retain manifest.");
        var expected = new HashSet<string>(_records.Keys.Select(key => key.ToString("N") + ".txt"), StringComparer.OrdinalIgnoreCase);
        var actual = Directory.GetFileSystemEntries(DirectoryPath);
        if (actual.Length != _records.Count || actual.Any(path => (File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 || !expected.Contains(Path.GetFileName(path))))
            throw new InvalidDataException("Unexpected original-first manifest entry; retain allocation.");
        foreach (var path in actual) File.Delete(path);
        Directory.Delete(DirectoryPath);
    }
}
