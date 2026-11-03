using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
public sealed class Web5OriginalRaceManifestTests
{
    [Fact]
    public void Manifest_refuses_incomplete_or_duplicate_cleanup_and_preserves_evidence()
    {
        using var user = WindowsIdentity.GetCurrent();
        var parent = Path.Combine(Path.GetTempPath(), "web5-race-parent-" + Guid.NewGuid().ToString("N"));
        var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false); acl.SetOwner(user.User!);
        acl.AddAccessRule(new FileSystemAccessRule(user.User!, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        Directory.CreateDirectory(parent, acl);
        try
        {
            var manifest = Web5OriginalRaceManifest.Create(parent); var key = Guid.NewGuid();
            var database = SqlTestDatabaseName.Create(); var trigger = "Web5Race_" + Guid.NewGuid().ToString("N");
            manifest.Record(database, key, trigger);
            Assert.Throws<InvalidDataException>(() => manifest.Record(database, key, trigger));
            Assert.Throws<InvalidDataException>(() => manifest.Complete());
            Assert.True(Directory.Exists(manifest.DirectoryPath));
            manifest.MarkClean(key);
            Assert.Throws<InvalidDataException>(() => manifest.MarkClean(key));
            var unexpected = Path.Combine(manifest.DirectoryPath, "unexpected.txt");
            File.WriteAllText(unexpected, "owned by test");
            Assert.Throws<InvalidDataException>(() => manifest.Complete());
            Assert.True(File.Exists(unexpected));
            File.Delete(unexpected);
            manifest.Complete();
            Assert.False(Directory.Exists(manifest.DirectoryPath));
        }
        finally { Directory.Delete(parent); }
    }
}
