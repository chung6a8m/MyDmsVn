using System.Threading;
using System.Threading.Tasks;
using System;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.UiLab.Scenarios;
public sealed class ScenarioPermissionCatalogApiClient : IPermissionCatalogApiClient
{
    readonly SecurityScenarioData data;
    readonly ScenarioDesktopSession session;
    readonly long owner;
    readonly ScenarioJournal journal;
    public ScenarioPermissionCatalogApiClient(SecurityScenarioData data, ScenarioDesktopSession session, long owner, ScenarioJournal journal)
    {
        this.data = data;
        this.session = session;
        this.owner = owner;
        this.journal = journal;
    }

    public ApiError? Error { get; set; }

    private readonly System.Collections.Generic.List<ScenarioGate<ApiResponse<PermissionCatalogDto>>> gates = new System.Collections.Generic.List<ScenarioGate<ApiResponse<PermissionCatalogDto>>>();
    private ScenarioGate<ApiResponse<PermissionCatalogDto>>? gate;
    public ScenarioGate<ApiResponse<PermissionCatalogDto>>? Gate
    {
        get => gate;
        set
        {
            gate = value;
            if (value != null)
                gates.Add(value);
        }
    }

    public void CancelGates()
    {
        foreach (var pending in gates)
            pending.Cancel();
    }

    readonly ScenarioTaskTracker tasks = new ScenarioTaskTracker();
    public System.Collections.Generic.IReadOnlyList<Task> OutstandingOperations => tasks.Outstanding;

    public Task<ApiResponse<PermissionCatalogDto>> GetCatalogAsync(CancellationToken token) => tasks.Track(ReadCatalog(token));
    async Task<ApiResponse<PermissionCatalogDto>> ReadCatalog(CancellationToken token)
    {
        if (session.Version != owner || !session.IsAuthenticated)
            return ScenarioErrors.SessionChanged<PermissionCatalogDto>();
        token.ThrowIfCancellationRequested();
        journal.Record("Security.Catalog");
        var next = gate;
        gate = null;
        try
        {
            var result = next != null ? await next.WaitAsync(token) : Error != null ? ApiResponse<PermissionCatalogDto>.Failure(Error) : ApiResponse<PermissionCatalogDto>.Success(data.Catalog);
            result = session.Version == owner && session.IsAuthenticated ? result : ScenarioErrors.SessionChanged<PermissionCatalogDto>();
            journal.Record("Security.Catalog.Result", new SecurityResponseJournalEntry(result.IsSuccess ? "Success" : result.Error?.Status.ToString() ?? "Failure", count: result.Data?.Items.Count));
            return result;
        }
        catch (OperationCanceledException)
        {
            journal.Record("Security.Catalog.Result", new SecurityResponseJournalEntry("Canceled"));
            throw;
        }
    }
}
