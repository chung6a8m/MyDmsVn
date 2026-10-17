using System;
using System.IO;
using System.Linq;
using Xunit;

namespace MyDmsVn.Architecture.Tests;

public sealed class PermissionRemovalInventoryTests
{
    [Fact]
    public void Removed_permission_sync_implementation_and_schema_objects_are_absent()
    {
        var root = FindRepositoryRoot();
        var forbidden = new[]
        {
            "PermissionSynchronizationEngine",
            "PermissionSyncCommand",
            "AdministratorGrantCommand",
            "AdministratorGrantPolicy",
            "PermissionSynchronizationState",
            "ManagedRolePermissionGrants",
            "PermissionGrantApprovals",
            "SystemManagedRoles",
            "PendingActivation",
        };
        var productionFiles = Directory.GetFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) &&
                           !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar));

        var hits = productionFiles
            .SelectMany(path => forbidden.Where(token => File.ReadAllText(path)
                .IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(token => path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar) + ":" + token))
            .ToArray();

        Assert.Empty(hits);
        Assert.False(File.Exists(Path.Combine(root, "docs", "SECURITY_PROVISIONING_RUNBOOK.md")));
        Assert.True(File.Exists(Path.Combine(root, "docs", "SECURITY_ADMINISTRATION_RUNBOOK.md")));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "MyDmsVn.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException(
            "Could not locate MyDmsVn.sln.");
    }
}
