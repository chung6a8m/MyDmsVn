using System;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Identity;

namespace MyDmsVn.Server.Infrastructure.Operations;

public interface ISuperuserBindingValidator
{
    Task ValidateAsync(
        string connectionString,
        string configuredUsername,
        CancellationToken cancellationToken);
}

public sealed class SqlSuperuserBindingValidator : ISuperuserBindingValidator
{
    public async Task ValidateAsync(
        string connectionString,
        string configuredUsername,
        CancellationToken cancellationToken)
    {
        var configuredNormalizedUsername = UsernameNormalizer.Normalize(configuredUsername);
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = SqlRead.Command(connection, null,
            "SELECT CAST(CASE WHEN EXISTS (" +
            "SELECT 1 FROM dbo.SystemSecuritySettings AS settings " +
            "INNER JOIN dbo.Users AS account ON account.UserId = settings.SuperuserUserId " +
            "WHERE settings.Id = 1 " +
            "AND settings.BoundNormalizedUsername = @configuredNormalizedUsername " +
            "AND account.NormalizedUsername = settings.BoundNormalizedUsername) " +
            "THEN 1 ELSE 0 END AS bit);",
            SqlRead.Text("@configuredNormalizedUsername", configuredNormalizedUsername));
        var matches = await SqlRead.SingleAsync(command, reader => reader.GetBoolean(0), cancellationToken).ConfigureAwait(false);
        if (!matches)
        {
            throw new DesktopStartupException(
                "Security.SuperuserBindingMismatch",
                "Cấu hình superuser chưa được liên kết với tài khoản trong cơ sở dữ liệu. Chạy quy trình bootstrap hoặc recovery đã được phê duyệt.");
        }
    }
}
