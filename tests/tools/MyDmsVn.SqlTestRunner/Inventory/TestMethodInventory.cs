using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;

namespace MyDmsVn.SqlTestRunner.Inventory;

public static class TestMethodInventory
{
    public static IReadOnlyList<TestMethodDescriptor> Read(string assemblyPath)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
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
                    Fingerprint(item.Method),
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

    private static string Fingerprint(MethodInfo method)
    {
        var body = method.GetMethodBody()?.GetILAsByteArray() ?? Array.Empty<byte>();
        return Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
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
