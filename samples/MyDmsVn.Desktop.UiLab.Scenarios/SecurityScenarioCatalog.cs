using System;
using System.Linq;
using System.Threading.Tasks;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.UiLab.Scenarios;
public enum SecurityScenarioId
{
    SEC_L01,
    SEC_L02,
    SEC_L03,
    SEC_L04,
    SEC_L05,
    SEC_L06,
    SEC_L07,
    SEC_L08,
    SEC_L09,
    SEC_L10,
    SEC_L11,
    SEC_L12
}

public sealed class SecurityScenario : IDisposable
{
    public SecurityScenario(SecurityScenarioId id)
    {
        Id = id;
        Administration = new ScenarioSecurityAdministrationApiClient(Data, Session, Session.Version, Journal);
        Catalog = new ScenarioPermissionCatalogApiClient(Data, Session, Session.Version, Journal);
    }

    public SecurityScenarioId Id { get; }
    public SecurityScenarioData Data { get; } = new SecurityScenarioData();
    public ScenarioDesktopSession Session { get; } = new ScenarioDesktopSession();
    public ScenarioJournal Journal { get; } = new ScenarioJournal();
    public ScenarioSecurityAdministrationApiClient Administration { get; }
    public ScenarioPermissionCatalogApiClient Catalog { get; }
    public ScenarioSecurityAdministrationApiClient AdministrationApi => Administration;
    public ScenarioPermissionCatalogApiClient CatalogApi => Catalog;

    public void CancelPending()
    {
        Administration.CancelGates();
        Catalog.CancelGates();
    }

    public Task<bool> DrainAsync(TimeSpan timeout) => DrainAsync(checked((int)timeout.TotalMilliseconds));
    public async Task<bool> DrainAsync(int timeoutMilliseconds)
    {
        var tasks = Administration.OutstandingOperations.Concat(Catalog.OutstandingOperations).ToArray();
        try
        {
            var all = Task.WhenAll(tasks);
            if (await Task.WhenAny(all, Task.Delay(timeoutMilliseconds)) != all)
                return false;
            try
            {
                await all;
            }
            catch (OperationCanceledException)
            {
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        Administration.CancelGates();
        Catalog.CancelGates();
    }
}

public static class SecurityScenarioCatalog
{
    public static SecurityScenario Create(SecurityScenarioId id)
    {
        var s = new SecurityScenario(id);
        if (id == SecurityScenarioId.SEC_L02)
            s.Administration.UsersError = new ApiError(ApiStatusCode.Forbidden, "Forbidden", "Không được phép xem tài khoản.");
        if (id == SecurityScenarioId.SEC_L08)
        {
            s.Administration.MutationErrors.Enqueue(null);
            s.Administration.MutationErrors.Enqueue(new ApiError(ApiStatusCode.Conflict, "Security.RevisionConflict", "Dữ liệu đã thay đổi."));
        }

        return s;
    }
}
