using System;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(
                SqlTestDatabase.ConnectionStringEnvironmentVariable)) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(
                SqlTestDatabase.ApprovalFileEnvironmentVariable)))
        {
            Skip =
                $"Set {SqlTestDatabase.ConnectionStringEnvironmentVariable} to a SQL Server master-database connection string " +
                $"and {SqlTestDatabase.ApprovalFileEnvironmentVariable} to a separately maintained non-production approval file.";
        }
    }
}
