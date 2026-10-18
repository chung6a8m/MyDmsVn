using System;
using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

[TraitDiscoverer(
    "MyDmsVn.Server.Infrastructure.IntegrationTests.SqlTestCategoryDiscoverer",
    "MyDmsVn.Server.Infrastructure.IntegrationTests")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SqlServerFactAttribute : FactAttribute, ITraitAttribute
{
    public SqlServerFactAttribute(params string[] categories)
    {
        Categories = categories is { Length: > 0 }
            ? categories
            : new[] { SqlTestCategory.FastSql };
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(
                SqlTestDatabase.ConnectionStringEnvironmentVariable)))
        {
            Skip =
                $"Set {SqlTestDatabase.ConnectionStringEnvironmentVariable} to a master-database connection string " +
                "for a designated non-production SQL Server test instance.";
        }
    }

    public IReadOnlyList<string> Categories { get; }
}

internal static class SqlTestCategory
{
    public const string FastSql = "FastSQL";
    public const string MigrationSchema = "MigrationSchema";
    public const string SecurityProvisioning = "SecurityProvisioning";
    public const string SqlConcurrency = "SqlConcurrency";
}

public sealed class SqlTestCategoryDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        var arguments = traitAttribute.GetConstructorArguments().ToArray();
        var categories = arguments.Length == 0
            ? Array.Empty<string>()
            : arguments[0] as string[] ?? Array.Empty<string>();
        if (categories.Length == 0)
        {
            categories = new[] { SqlTestCategory.FastSql };
        }

        return categories.Select(category =>
            new KeyValuePair<string, string>("Category", category));
    }
}
