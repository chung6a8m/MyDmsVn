using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using System.Linq;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Test-only attended lease. A local ACL restricts the ephemeral operator login to
// the current Windows user. Host, SQL and credentials are drained by the parent.
internal static class Web5AttendedLease
{
    internal static void VerifyPrivateDirectory(string path, SecurityIdentifier user)
    {
        if (!Path.IsPathRooted(path) || !Directory.Exists(path)) throw new InvalidOperationException("Attended parent must exist and be absolute.");
        var info = new DirectoryInfo(Path.GetFullPath(path));
        for (var cursor = info; cursor != null; cursor = cursor.Parent)
            if ((cursor.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Reparse points are not permitted in attended path.");
        var acl = info.GetAccessControl(AccessControlSections.Owner | AccessControlSections.Access);
        if (!acl.AreAccessRulesProtected || !user.Equals(acl.GetOwner(typeof(SecurityIdentifier))))
            throw new InvalidOperationException("Attended directory owner or inheritance is unsafe.");
        var rules = acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
        if (!rules.Any(rule => rule.AccessControlType == AccessControlType.Allow && user.Equals(rule.IdentityReference)) ||
            rules.Any(rule => rule.AccessControlType == AccessControlType.Allow && !user.Equals(rule.IdentityReference)))
            throw new InvalidOperationException("Attended directory grants access to another principal.");
    }

    internal static async Task Wait(Process host, string path, string password, TimeSpan duration, CancellationToken token)
    {
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(30)) throw new ArgumentOutOfRangeException(nameof(duration));
        using var identity = WindowsIdentity.GetCurrent();
        VerifyPrivateDirectory(path, identity.User!);
        path = Path.Combine(path, "lease-" + Guid.NewGuid().ToString("N"));
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(true, false);
        acl.SetOwner(identity.User!);
        acl.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        Directory.CreateDirectory(path, acl);
        var credentials = Path.Combine(path, "operator.txt");
        var stop = Path.Combine(path, "done");
        try
        {
            VerifyPrivateDirectory(path, identity.User!);
            using (var stream = new FileStream(credentials, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write("URL: https://localhost:44321/\nUser: integratedEditor\nPassword: " + password + "\nCreate an empty file named done in this directory when finished.\n");
            Console.WriteLine("WEB5_ATTENDED_LOCAL " + credentials);
            var deadline = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();
                if (host.HasExited) throw new InvalidOperationException("Attended IIS worker exited.");
                if (File.Exists(stop)) return;
                await Task.Delay(1000, token).ConfigureAwait(false);
            }
            throw new TimeoutException("Attended session expired; cleanup follows.");
        }
        finally
        {
            if (File.Exists(credentials)) File.Delete(credentials);
            if (File.Exists(stop)) File.Delete(stop);
            Directory.Delete(path);
        }
    }
}
