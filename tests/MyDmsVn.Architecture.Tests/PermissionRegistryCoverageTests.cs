using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using MyDmsVn.Server.Application.Security;
using Xunit;

namespace MyDmsVn.Architecture.Tests;

public sealed class PermissionRegistryCoverageTests
{
    private static readonly Assembly ApplicationAssembly = typeof(PermissionRegistryComposition).Assembly;

    [Fact]
    public void Module_definitions_cover_every_declared_permission_constant_once()
    {
        var constants = ApplicationAssembly.GetTypes()
            .Where(type => type.IsClass && type.IsAbstract && type.IsSealed &&
                type.Name.EndsWith("PermissionKeys", StringComparison.Ordinal) &&
                type.Name != nameof(PermissionKeys))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        var definitions = ApplicationAssembly.GetTypes()
            .Where(type => type.IsClass && type.IsAbstract && type.IsSealed &&
                type.Name.EndsWith("PermissionDefinitions", StringComparison.Ordinal))
            .SelectMany(ReadDefinitions)
            .Select(definition => definition.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(constants);
        Assert.Equal(constants, constants.Distinct(StringComparer.Ordinal).ToArray());
        Assert.Equal(constants, definitions);
        Assert.Equal(constants, PermissionRegistryComposition.CreateDefault()
            .Definitions.Select(definition => definition.Key)
            .OrderBy(key => key, StringComparer.Ordinal));
    }

    [Fact]
    public void Every_authorized_use_case_references_a_declared_composed_key()
    {
        var declared = ApplicationAssembly.GetTypes()
            .Where(type => type.IsClass && type.IsAbstract && type.IsSealed &&
                type.Name.EndsWith("PermissionKeys", StringComparison.Ordinal) &&
                type.Name != nameof(PermissionKeys))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);
        var registry = PermissionRegistryComposition.CreateDefault();
        var references = ApplicationAssembly.GetTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface && !type.ContainsGenericParameters &&
                typeof(IAuthorizedRequest).IsAssignableFrom(type))
            .Select(type => (IAuthorizedRequest)FormatterServices.GetUninitializedObject(type))
            .Select(request => request.PermissionKey)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(references);
        Assert.Equal(references, references.Where(declared.Contains));
        Assert.Equal(references, references.Where(key => registry.TryGet(key, out _)));
    }

    private static IEnumerable<PermissionDefinition> ReadDefinitions(Type type)
    {
        var property = type.GetProperty("All", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(property);
        var values = Assert.IsAssignableFrom<IEnumerable<PermissionDefinition>>(property!.GetValue(null));
        return values;
    }
}
