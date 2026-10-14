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
                    Fingerprint(item.Method)))
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
}

public sealed record TestMethodDescriptor(
    string FullyQualifiedName,
    bool IsTheory,
    string BodyFingerprint);
