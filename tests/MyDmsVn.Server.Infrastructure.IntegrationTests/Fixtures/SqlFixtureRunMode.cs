using System;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal enum SqlFixtureRunMode { Fresh, Reuse }

internal static class SqlFixtureRunModeSetting
{
    public const string EnvironmentVariable = "MYDMSVN_SQL_TEST_FIXTURE_MODE";
    public static SqlFixtureRunMode Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return SqlFixtureRunMode.Reuse; }
        if (string.Equals(value?.Trim(), "fresh", StringComparison.OrdinalIgnoreCase))
        {
            return SqlFixtureRunMode.Fresh;
        }
        if (string.Equals(value?.Trim(), "reuse", StringComparison.OrdinalIgnoreCase)) { return SqlFixtureRunMode.Reuse; }
        throw new ArgumentException("The SQL fixture mode must be fresh or reuse.", nameof(value));
    }
}
