using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
internal static class Web5BackupFixture
{
    internal static void EnsureCloneName(string name) => SqlTestDatabaseName.EnsureDisposable(name);
    internal static void VerifyPrivateDirectory(string path, SecurityIdentifier user, SecurityIdentifier service)
    {
        if (!Path.IsPathRooted(path) || !Directory.Exists(path)) throw new InvalidOperationException("Backup parent must exist and be absolute.");
        var info = new DirectoryInfo(Path.GetFullPath(path));
        for (var cursor = info; cursor != null; cursor = cursor.Parent)
            if ((cursor.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Backup path contains a reparse point.");
        var acl = info.GetAccessControl(AccessControlSections.Owner | AccessControlSections.Access);
        if (!acl.AreAccessRulesProtected || !user.Equals(acl.GetOwner(typeof(SecurityIdentifier)))) throw new InvalidOperationException("Backup directory owner or inheritance is unsafe.");
        var allowed = acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().Where(rule => rule.AccessControlType == AccessControlType.Allow).ToArray();
        if (!allowed.Any(rule => user.Equals(rule.IdentityReference)) || !allowed.Any(rule => service.Equals(rule.IdentityReference)) ||
            allowed.Any(rule => !user.Equals(rule.IdentityReference) && !service.Equals(rule.IdentityReference)))
            throw new InvalidOperationException("Backup directory principal is unsafe.");
    }

    internal static void EnsureNewDevice(string path)
    {
        if (!Path.IsPathRooted(path) || File.Exists(path) || Directory.Exists(path)) throw new InvalidOperationException("Backup device must be a new absolute file.");
        for (var cursor = new DirectoryInfo(Path.GetDirectoryName(path)!); cursor != null; cursor = cursor.Parent)
            if (!cursor.Exists || (cursor.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Unsafe backup directory.");
    }
}
