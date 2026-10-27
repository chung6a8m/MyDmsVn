using System;
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
    private readonly long? _expectedSessionVersion;

    public LocalPermissionCatalogApiClient(LocalRequestDispatcher dispatcher) : this(dispatcher, null) { }

    private LocalPermissionCatalogApiClient(LocalRequestDispatcher dispatcher, long? expectedSessionVersion)
    {
        _dispatcher = dispatcher;
        _expectedSessionVersion = expectedSessionVersion;
    }

    public IPermissionCatalogApiClient ForSession(long expectedSessionVersion)
    {
        if (_expectedSessionVersion.HasValue)
        {
            if (_expectedSessionVersion.Value != expectedSessionVersion)
                throw new InvalidOperationException("A Permission catalog client cannot be rebound to another session.");
            return this;
        }
        return new LocalPermissionCatalogApiClient(_dispatcher, expectedSessionVersion);
    }

    public Task<ApiResponse<PermissionCatalogDto>> GetCatalogAsync(CancellationToken cancellationToken) =>
        _expectedSessionVersion.HasValue
            ? _dispatcher.SendForCurrentSessionAsync(new ListPermissionCatalogQuery(), null, cancellationToken, _expectedSessionVersion)
            : _dispatcher.SendAsync(new ListPermissionCatalogQuery(), null, cancellationToken);
}
