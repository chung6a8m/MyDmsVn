using System;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal static class SqlTestDatabaseName
{
    private const string Prefix = "MyDmsVn_Test_";
    private const string GuiPrefix = "MyDmsVn_GuiP5_";

    public static string CreateGui() => GuiPrefix + Guid.NewGuid().ToString("N");

    public static void EnsureGuiDisposable(string databaseName) => EnsureName(databaseName, GuiPrefix);

    public static string Create()
    {
        return Prefix + Guid.NewGuid().ToString("N");
    }

    public static void EnsureDisposable(string databaseName) => EnsureName(databaseName, Prefix);

    private static void EnsureName(string databaseName, string prefix)
    {
        if (string.IsNullOrWhiteSpace(databaseName) ||
            !databaseName.StartsWith(prefix, StringComparison.Ordinal) ||
            databaseName.Length != prefix.Length + 32 ||
            !Guid.TryParseExact(databaseName.Substring(prefix.Length), "N", out _))
        {
            throw new InvalidOperationException(
                "Refusing to dispose a database that was not named by the MyDmsVn test database helper.");
        }
    }
}
