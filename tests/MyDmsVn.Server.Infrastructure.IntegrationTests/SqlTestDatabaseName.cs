using System;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal static class SqlTestDatabaseName
{
    private const string Prefix = "MyDmsVn_Test_";

    public static string Create()
    {
        return Prefix + Guid.NewGuid().ToString("N");
    }

    public static void EnsureDisposable(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName) ||
            !databaseName.StartsWith(Prefix, StringComparison.Ordinal) ||
            databaseName.Length != Prefix.Length + 32 ||
            !Guid.TryParseExact(databaseName.Substring(Prefix.Length), "N", out _))
        {
            throw new InvalidOperationException(
                "Refusing to dispose a database that was not named by the MyDmsVn test harness.");
        }
    }
}
