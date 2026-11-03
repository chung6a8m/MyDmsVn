using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
public sealed class Web5BackupSafetyTests
{
    [Fact]
    public void Restore_refuses_unowned_names_and_non_new_device()
    {
        Assert.Throws<InvalidOperationException>(() => Web5BackupFixture.EnsureCloneName("master"));
        Assert.Throws<InvalidOperationException>(() => Web5BackupFixture.EnsureCloneName("MyDmsVn_Test_not-a-guid"));
        var path = Path.GetTempFileName();
        try { Assert.Throws<InvalidOperationException>(() => Web5BackupFixture.EnsureNewDevice(path)); }
        finally { File.Delete(path); }
    }
    [Fact]
    public void Private_parent_rejects_other_allowed_principals()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var path = Path.Combine(Path.GetTempPath(), "web5-backup-acl-" + Guid.NewGuid().ToString("N"));
        var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false); acl.SetOwner(identity.User!);
        acl.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl, AccessControlType.Allow));
        Directory.CreateDirectory(path, acl);
        try
        {
            Web5BackupFixture.VerifyPrivateDirectory(path, identity.User!, identity.User!);
            acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.Read, AccessControlType.Allow));
            Directory.SetAccessControl(path, acl);
            Assert.Throws<InvalidOperationException>(() => Web5BackupFixture.VerifyPrivateDirectory(path, identity.User!, identity.User!));
        }
        finally { Directory.Delete(path); }
    }
}
