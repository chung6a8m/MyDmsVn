using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MyDmsVn.SqlTestRunner.Inventory;

public static class IlSqlCallGraphAuditor
{
    private static readonly string[] SqlRoots =
    {
        "Microsoft.Data.SqlClient.SqlConnection",
        "MyDmsVn.Server.Infrastructure.IntegrationTests.SqlTestDatabase",
        "DatabaseMigrationRunner",
        "SqlPrincipal",
        "Provisioning",
    };

    public static SqlCallGraphAudit Audit(MethodInfo method)
    {
        var references = ReadReferencedMembers(method).ToArray();
        var sqlReference = references.FirstOrDefault(reference =>
            SqlRoots.Any(root => reference.IndexOf(root, StringComparison.OrdinalIgnoreCase) >= 0));
        return sqlReference is null
            ? new SqlCallGraphAudit(false, references)
            : new SqlCallGraphAudit(true, references, sqlReference);
    }

    private static IEnumerable<string> ReadReferencedMembers(MethodInfo method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray() ?? Array.Empty<byte>();
        for (var index = 0; index <= il.Length - 5; index++)
        {
            if (il[index] != 0x28 && il[index] != 0x6f && il[index] != 0x73)
            {
                continue;
            }

            var token = BitConverter.ToInt32(il, index + 1);
            MemberInfo? member = null;
            try
            {
                member = method.Module.ResolveMember(token);
            }
            catch (ArgumentException)
            {
            }

            if (member is not null)
            {
                yield return member.DeclaringType?.FullName + "." + member.Name;
            }

            index += 4;
        }
    }
}

public sealed record SqlCallGraphAudit(
    bool ReachesSql,
    IReadOnlyList<string> ReferencedMembers,
    string? SqlRoot = null);
