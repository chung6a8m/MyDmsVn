using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace MyDmsVn.Architecture.Tests;

public sealed class OperationalPersistenceBoundaryTests
{
    [Fact]
    public void Identity_security_operations_and_migrator_have_no_legacy_orm_consumers()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "MyDmsVn.sln"))) root = root.Parent;
        Assert.NotNull(root);
        var paths = new[] { "MyDmsVn.Server.Infrastructure/Identity", "MyDmsVn.Server.Infrastructure/Security", "MyDmsVn.Server.Infrastructure/Operations", "MyDmsVn.Server.DbMigrator" };
        var files = paths.SelectMany(path => Directory.GetFiles(Path.Combine(root!.FullName, "src", path), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar));
        Assert.Empty(files.Where(path => Regex.IsMatch(File.ReadAllText(path), @"\b(Dapper|RepoDb)\b")).Select(Path.GetFileName));
    }
}
