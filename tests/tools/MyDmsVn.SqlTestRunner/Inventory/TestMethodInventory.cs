using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;

namespace MyDmsVn.SqlTestRunner.Inventory;

public static class TestMethodInventory
{
    public static IReadOnlyList<TestMethodDescriptor> Read(string assemblyPath)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
        var reviewedCode = ReadReviewedCode(fullPath);
        var context = new AssemblyLoadContext("sql-inventory-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        var resolver = new AssemblyDependencyResolver(fullPath);
        context.Resolving += (_, assemblyName) =>
        {
            var dependencyPath = resolver.ResolveAssemblyToPath(assemblyName);
            return dependencyPath is null ? null : context.LoadFromAssemblyPath(dependencyPath);
        };
        try
        {
            var assembly = context.LoadFromAssemblyPath(fullPath);
            return assembly.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                .Select(method => new { Method = method, Attributes = CustomAttributeData.GetCustomAttributes(method) })
                .Where(item => item.Attributes.Any(attribute =>
                    IsAttributeOrDerived(attribute.AttributeType, "Xunit.FactAttribute") ||
                    IsAttributeOrDerived(attribute.AttributeType, "Xunit.TheoryAttribute")))
                .Select(item => new TestMethodDescriptor(
                    item.Method.DeclaringType!.FullName + "." + item.Method.Name,
                    item.Attributes.Any(attribute =>
                        IsAttributeOrDerived(attribute.AttributeType, "Xunit.TheoryAttribute")),
                    FingerprintReviewedCode(
                        item.Method.DeclaringType!.FullName + "." + item.Method.Name,
                        reviewedCode),
                    ReadCategories(item.Method)))
                .OrderBy(item => item.FullyQualifiedName, StringComparer.Ordinal)
                .ToArray();
        }
        finally
        {
            context.Unload();
        }
    }

    private static bool IsAttributeOrDerived(Type attributeType, string expectedFullName)
    {
        for (var current = attributeType; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.FullName, expectedFullName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static string FingerprintReviewedCode(
        string methodName,
        IReadOnlyDictionary<string, byte[]> reviewedFiles)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(methodName));
        foreach (var file in reviewedFiles.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(file.Key.ToLowerInvariant()));
            hash.AppendData(file.Value);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static IReadOnlyDictionary<string, byte[]> ReadReviewedCode(string assemblyPath)
    {
        var repositoryRoot = FindRepositoryRoot(Path.GetDirectoryName(assemblyPath)!);
        if (repositoryRoot is null)
        {
            return new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                [Path.GetFileName(assemblyPath)] = File.ReadAllBytes(assemblyPath),
            };
        }

        // Deliberately invalidate every exemption when reviewed source changes.
        // The broad, source-stable boundary includes async/generated bodies and
        // local helper call paths without depending on commit metadata embedded
        // in deterministic build outputs.
        var roots = new[]
        {
            Path.Combine(repositoryRoot, "src"),
            Path.Combine(repositoryRoot, "tests", "MyDmsVn.Server.Infrastructure.IntegrationTests"),
            Path.Combine(repositoryRoot, "tests", "MyDmsVn.SqlTestHarness"),
        };
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".csproj", ".json", ".props", ".resx", ".sql", ".targets",
        };
        return roots
            .Where(Directory.Exists)
            .SelectMany(root => Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            .Where(path => !HasBuildOutputSegment(repositoryRoot, path))
            .Where(path => extensions.Contains(Path.GetExtension(path)))
            .ToDictionary(
                path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'),
                File.ReadAllBytes,
                StringComparer.OrdinalIgnoreCase);
    }

    private static string? FindRepositoryRoot(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MyDmsVn.sln")))
            {
                return directory.FullName;
            }
        }

        return null;
    }

    private static bool HasBuildOutputSegment(string repositoryRoot, string path)
    {
        var relative = Path.GetRelativePath(repositoryRoot, path);
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment =>
                string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> ReadCategories(MethodInfo method) =>
        CustomAttributeData.GetCustomAttributes(method.DeclaringType!)
            .Concat(CustomAttributeData.GetCustomAttributes(method))
            .SelectMany(ReadAttributeCategories)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<string> ReadAttributeCategories(CustomAttributeData attribute)
    {
        if (string.Equals(attribute.AttributeType.FullName, "Xunit.TraitAttribute", StringComparison.Ordinal) &&
            attribute.ConstructorArguments.Count == 2 &&
            string.Equals(attribute.ConstructorArguments[0].Value as string, "Category", StringComparison.Ordinal) &&
            attribute.ConstructorArguments[1].Value is string traitCategory)
        {
            yield return traitCategory;
        }

        if (!string.Equals(attribute.AttributeType.Name, "SqlServerFactAttribute", StringComparison.Ordinal))
        {
            yield break;
        }

        if (attribute.ConstructorArguments.Count == 0 ||
            attribute.ConstructorArguments[0].Value is not IReadOnlyCollection<CustomAttributeTypedArgument> arguments ||
            arguments.Count == 0)
        {
            yield return SqlTestCategories.FastSql;
            yield break;
        }

        foreach (var argument in arguments)
        {
            if (argument.Value is string category)
            {
                yield return category;
            }
        }
    }
}

public sealed record TestMethodDescriptor(
    string FullyQualifiedName,
    bool IsTheory,
    string BodyFingerprint,
    IReadOnlyList<string> Categories)
{
    public TestMethodDescriptor(
        string FullyQualifiedName,
        bool IsTheory,
        string BodyFingerprint)
        : this(FullyQualifiedName, IsTheory, BodyFingerprint, Array.Empty<string>())
    {
    }
}
