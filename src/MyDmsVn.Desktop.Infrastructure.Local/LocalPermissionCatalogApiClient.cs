using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application.Security;

namespace MyDmsVn.Desktop.Infrastructure.Local;

internal sealed class LocalPermissionCatalogApiClient : IPermissionCatalogApiClient
{
    private readonly LocalRequestDispatcher _dispatcher;

    public LocalPermissionCatalogApiClient(LocalRequestDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task<ApiResponse<PermissionCatalogDto>> GetCatalogAsync(CancellationToken cancellationToken) =>
        _dispatcher.SendAsync(
            new ListPermissionCatalogQuery(),
            null,
            cancellationToken);
}
