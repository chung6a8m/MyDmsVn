using System;

namespace MyDmsVn.SqlTestHarness.Approval;

public sealed class SqlInstanceIdentity
{
    public SqlInstanceIdentity(
        string serverName,
        string machineName,
        string? instanceName,
        string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(serverName))
        {
            throw new ArgumentException("SQL ServerName is required.", nameof(serverName));
        }

        if (string.IsNullOrWhiteSpace(machineName))
        {
            throw new ArgumentException("SQL MachineName is required.", nameof(machineName));
        }

        ServerName = serverName;
        MachineName = machineName;
        InstanceName = NormalizeOptional(instanceName);
        Endpoint = NormalizeOptional(endpoint);
    }

    public string ServerName { get; }

    public string MachineName { get; }

    public string? InstanceName { get; }

    public string? Endpoint { get; }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
