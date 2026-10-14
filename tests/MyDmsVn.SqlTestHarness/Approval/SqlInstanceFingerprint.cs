using System;
using System.Security.Cryptography;
using System.Text;

namespace MyDmsVn.SqlTestHarness.Approval;

public static class SqlInstanceFingerprint
{
    public static string Create(SqlInstanceIdentity identity)
    {
        if (identity is null)
        {
            throw new ArgumentNullException(nameof(identity));
        }

        var canonical =
            "server=" + identity.ServerName + "\n" +
            "machine=" + identity.MachineName + "\n" +
            "instance=" + (identity.InstanceName ?? string.Empty) + "\n" +
            "endpoint=" + (identity.Endpoint ?? string.Empty);

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var value in hash)
        {
            builder.Append(value.ToString("x2"));
        }

        return builder.ToString();
    }
}
