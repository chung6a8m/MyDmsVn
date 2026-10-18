using System;
using System.Linq;
using System.Collections.Generic;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Infrastructure.Persistence;
using Xunit;

namespace MyDmsVn.Architecture.Tests;

public sealed class LinqToDbBoundaryTests
{
    [Fact]
    public void Inner_and_ui_assemblies_do_not_reference_linq_provider()
    {
        var assemblies = new[] { typeof(ApiResponseMapper).Assembly,
            typeof(MyDmsVn.Desktop.Application.IFoundationApiClient).Assembly,
            typeof(MyDmsVn.Contracts.ApiResponse<>).Assembly,
            typeof(MyDmsVn.Server.Domain.Inventory.GoodsReceipt).Assembly };
        foreach (var assembly in assemblies)
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), r =>
                (r.Name ?? string.Empty).StartsWith("linq2db", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Session_and_mapping_composition_are_internal_infrastructure_contracts()
    {
        var assembly = typeof(SqlUnitOfWorkFactory).Assembly;
        var session = assembly.GetType("MyDmsVn.Server.Infrastructure.Persistence.ILinqToDbSessionFactory");
        Assert.NotNull(session);
        Assert.False(session!.IsVisible);
        Assert.False(assembly.GetType("MyDmsVn.Server.Infrastructure.Persistence.ILinqToDbMapping")!.IsVisible);
        Assert.False(assembly.GetType("MyDmsVn.Server.Infrastructure.Persistence.LinqToDbMappingSchemaProvider")!.IsVisible);
        Assert.DoesNotContain(assembly.GetExportedTypes().SelectMany(t => t.GetMethods())
            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)).SelectMany(Flatten),
            t => (t.Namespace ?? string.Empty).StartsWith("LinqToDB", StringComparison.Ordinal));
    }
    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        if (type.HasElementType)
            foreach (var element in Flatten(type.GetElementType()!)) yield return element;
        foreach (var argument in type.GetGenericArguments())
            foreach (var nested in Flatten(argument)) yield return nested;
    }
}
