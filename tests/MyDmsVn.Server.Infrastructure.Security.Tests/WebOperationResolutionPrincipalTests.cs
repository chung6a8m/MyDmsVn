using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using Xunit;
using static MyDmsVn.Server.Infrastructure.IntegrationTests.WebOperationResolutionScenario;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebOperationResolutionPrincipalTests
{
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Exact13_provisioned_runtime_has_read_only_resolution_authority_and_old_validator_refuses() => Owned(async s =>
    {
        await Provision(s.Observer);
        await s.Observer.ExecuteAsync("EXECUTE AS USER=N'Web5RuntimeProbe';");
        try
        {
            await new SqlWebRuntimePrincipalValidator(requireJournal: true, requireResolution: true).ValidateConnectionAsync(s.Observer, CancellationToken.None);
            await Assert.ThrowsAsync<DesktopStartupException>(() => new SqlWebRuntimePrincipalValidator().ValidateConnectionAsync(s.Observer, CancellationToken.None));
            Assert.Equal(1, await s.Observer.QuerySingleAsync<int>("SELECT HAS_PERMS_BY_NAME(N'dbo.WebOperationTerminalAudit',N'OBJECT',N'SELECT');"));
            Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT HAS_PERMS_BY_NAME(N'dbo.WebOperationTerminalAudit',N'OBJECT',N'INSERT');"));
            Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT HAS_PERMS_BY_NAME(N'dbo.RecordWebOperationNotExecuted',N'OBJECT',N'EXECUTE');"));
        }
        finally { await s.Observer.ExecuteAsync("REVERT;"); }
    });

    [SqlServerTheory(SqlTestCategory.SecurityProvisioning)]
    [InlineData("REVOKE SELECT ON dbo.WebOperationTerminalAudit FROM MyDmsVnWebRuntimeRole;", "Security.WebRuntimePrincipalUnderprivileged")]
    [InlineData("DENY SELECT(CaseId) ON dbo.WebOperationTerminalAudit TO Web5RuntimeProbe;", "Security.WebRuntimePrincipalUnderprivileged")]
    [InlineData("REVOKE INSERT ON dbo.WebOperationTerminalAudit FROM MyDmsVnWebRuntimeRole; GRANT INSERT ON dbo.WebOperationTerminalAudit TO Web5RuntimeProbe;", "Security.WebRuntimePrincipalOverprivileged")]
    [InlineData("REVOKE EXECUTE ON dbo.RecordWebOperationNotExecuted FROM MyDmsVnWebRuntimeRole; ALTER ROLE MyDmsVnWebOperationOperatorRole ADD MEMBER Web5RuntimeProbe;", "Security.WebRuntimePrincipalOverprivileged")]
    public Task Missing_resolution_read_or_terminal_write_authority_refuses_runtime_startup(string mutation, string code) => Owned(async s =>
    {
        await Provision(s.Observer); await s.Observer.ExecuteAsync(mutation);
        await s.Observer.ExecuteAsync("EXECUTE AS USER=N'Web5RuntimeProbe';");
        try { Assert.Equal(code, (await Assert.ThrowsAsync<DesktopStartupException>(() => new SqlWebRuntimePrincipalValidator(requireJournal: true, requireResolution: true).ValidateConnectionAsync(s.Observer, CancellationToken.None))).Code); }
        finally { await s.Observer.ExecuteAsync("REVERT;"); }
    });

    private static async Task Provision(Microsoft.Data.SqlClient.SqlConnection sql)
    {
        await sql.ExecuteAsync("CREATE USER Web5RuntimeProbe WITHOUT LOGIN; CREATE USER Web5MigrationProbe WITHOUT LOGIN;");
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "deployment", "sql", "Provision-MyDmsVnWebPrincipals.sql");
            if (!File.Exists(path)) continue;
            await sql.ExecuteAsync(File.ReadAllText(path).Replace("$(WebRuntimeUser)", "Web5RuntimeProbe").Replace("$(MigrationUser)", "Web5MigrationProbe"));
            return;
        }
        throw new FileNotFoundException("Production Web principal provisioner was not found.");
    }
}
