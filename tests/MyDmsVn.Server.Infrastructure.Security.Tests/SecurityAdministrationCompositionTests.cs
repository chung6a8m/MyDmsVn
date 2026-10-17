using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Infrastructure;
using MyDmsVn.Server.Infrastructure.Security;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SecurityAdministrationCompositionTests
{
    [Fact]
    public void Sql_persistence_registers_security_administration_without_opening_a_connection()
    {
        var services = new ServiceCollection();
        services.AddServerApplication("root.admin");
        services.AddSqlPersistence("Server=127.0.0.1;Database=NotOpened;User ID=unused;Password=unused;");

        using var provider = services.BuildServiceProvider();

        Assert.IsType<SqlSecurityAdministrationStore>(
            provider.GetRequiredService<ISecurityAdministrationStore>());
    }
}
