using System.IO;
using System.Linq;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using Newtonsoft.Json;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class PermissionCatalogCommandTests
{
    [Fact]
    public void Offline_json_command_exports_the_runtime_registry_without_database_configuration()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionCatalogCommand.Run(
            new[] { "list", "--format", "json" },
            output,
            error);
        var catalog = JsonConvert.DeserializeObject<PermissionCatalogDto>(output.ToString());

        Assert.Equal(0, exitCode);
        Assert.Empty(error.ToString());
        Assert.NotNull(catalog);
        Assert.Equal(
            PermissionCatalogFactory.Create(
                    PermissionRegistryComposition.CreateDefault(),
                    catalog!.ApplicationVersion)
                .CatalogDigest,
            catalog.CatalogDigest);
        Assert.Equal(
            PermissionRegistryComposition.CreateDefault().Definitions.Select(definition => definition.Key),
            catalog.Items.Select(item => item.Key));
    }
}
