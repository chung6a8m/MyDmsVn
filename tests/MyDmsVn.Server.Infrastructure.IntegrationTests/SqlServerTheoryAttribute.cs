using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Sdk;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

[TraitDiscoverer(
    "MyDmsVn.Server.Infrastructure.IntegrationTests.SqlTestCategoryDiscoverer",
    "MyDmsVn.Server.Infrastructure.IntegrationTests")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SqlServerTheoryAttribute : TheoryAttribute, Xunit.Sdk.ITraitAttribute
{
    public SqlServerTheoryAttribute(params string[] categories)
    {
        Categories = categories is { Length: > 0 } ? categories : new[] { SqlTestCategory.FastSql };
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)))
        {
            Skip = "Set " + SqlTestDatabase.ConnectionStringEnvironmentVariable +
                " to a designated non-production master connection before SQL Theory execution.";
        }
    }
    public IReadOnlyList<string> Categories { get; }
}
