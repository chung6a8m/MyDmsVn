using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.UiLab.Scenarios;

public sealed class SalesScenarioData : IDisposable
{
    public const long SeedSaleId = 41;
    public static readonly DateTime ReferenceDate = new DateTime(2026, 10, 22);
    private readonly WeakReferenceMessenger _messenger = new WeakReferenceMessenger();
    private readonly List<ScenarioLookupSource> _lookupSources = new List<ScenarioLookupSource>();
    private readonly List<CatalogLookupViewModel> _lookupViewModels = new List<CatalogLookupViewModel>();
    private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
    private bool _disposed;

    private SalesScenarioData(SalesScenarioId id)
    {
        Id = id;
        Session = new ScenarioDesktopSession();
        Journal = new ScenarioJournal();
        Preview = new ScenarioPreviewCalculator();
        Permissions = new ScenarioPermissionClient();
        var products = new[]
        {
            new ProductDto(11, "P-011", "Demo Product A", "pcs", true),
            new ProductDto(12, "P-012", "Demo Product B", "box", true),
        };
        ProductApi = new ScenarioProductApiClient(products);
        LookupSources = new ScenarioLookupSourceFactory(new Dictionary<CatalogKind, IReadOnlyList<CatalogLookupDto>>
        {
            [CatalogKind.Product] = products.Select(product => new CatalogLookupDto(product.Id, product.Code, product.Name, product.IsActive)).ToArray(),
            [CatalogKind.Customer] = new[] { new CatalogLookupDto(1, "C-001", "Demo Customer", true) },
            [CatalogKind.Warehouse] = new[] { new CatalogLookupDto(2, "W-002", "Demo Warehouse", true) },
            [CatalogKind.Employee] = new[] { new CatalogLookupDto(3, "E-003", "Demo Employee", true) },
        });
        SalesApi = new ScenarioSalesApiClient(Session, Journal, Preview, id == SalesScenarioId.S02Validation ? null : Seed());
        if (id == SalesScenarioId.S06UnknownPost) SalesApi.UnknownPostOutcome = true;
    }

    public SalesScenarioId Id { get; }
    public ScenarioDesktopSession Session { get; }
    public ScenarioJournal Journal { get; }
    public ScenarioPreviewCalculator Preview { get; }
    public ScenarioPermissionClient Permissions { get; }
    public ScenarioProductApiClient ProductApi { get; }
    public ScenarioLookupSourceFactory LookupSources { get; }
    public ScenarioSalesApiClient SalesApi { get; }
    public CancellationToken LifetimeToken => _lifetime.Token;
    public long? InitialSaleId => Id == SalesScenarioId.S02Validation ? null : SeedSaleId;

    public static SalesScenarioData Create(SalesScenarioId id)
    {
        if (!Enum.IsDefined(typeof(SalesScenarioId), id)) throw new ArgumentOutOfRangeException(nameof(id));
        return new SalesScenarioData(id);
    }

    public CatalogLookupViewModel CreateLookup(CatalogKind kind, IUiDispatcher dispatcher)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SalesScenarioData));
        var source = LookupSources.Create(kind);
        _lookupSources.Add(source);
        var viewModel = new CatalogLookupViewModel(source, _messenger, dispatcher,
            new DesktopNotificationCenter(), new SystemAsyncDelay(), TimeSpan.FromMilliseconds(20));
        _lookupViewModels.Add(viewModel);
        return viewModel;
    }

    public IReadOnlyList<ScenarioLookupSource> CreatedLookupSources => _lookupSources.ToArray();
    public IReadOnlyList<CatalogLookupViewModel> CreatedLookupViewModels => _lookupViewModels.ToArray();

    public void CancelPending()
    {
        if (!_lifetime.IsCancellationRequested) _lifetime.Cancel();
        ReleaseGates();
    }

    public void ReleaseGates()
    {
        SalesApi.CancelGates();
        ProductApi.CancelGates();
        foreach (var source in _lookupSources)
        {
            source.CancelQueuedGates();
        }
    }

    public async Task DrainAsync(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        var operations = SalesApi.OutstandingOperations.Concat(ProductApi.OutstandingOperations)
            .Concat(_lookupSources.SelectMany(source => source.OutstandingOperations)).ToArray();
        if (operations.Length == 0) return;
        var all = Task.WhenAll(operations);
        if (await Task.WhenAny(all, Task.Delay(timeout)).ConfigureAwait(false) != all)
            throw new TimeoutException("UI Lab sales operations did not drain.");
        try { await all.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelPending();
        _lifetime.Dispose();
    }

    public ScenarioLookupRace ArmLookupRace()
    {
        var source = _lookupSources.FirstOrDefault(item => item.Kind == CatalogKind.Product)
            ?? throw new InvalidOperationException("Create the Sales control before arming the product lookup race.");
        return ArmLookupRace(source);
    }

    public ScenarioLookupRace ArmLookupRace(ScenarioLookupSource source)
    {
        if (Id != SalesScenarioId.S03LookupRace) throw new InvalidOperationException("Lookup race is available only in S03.");
        var race = new ScenarioLookupRace();
        source.EnqueueLookupGate(race.A);
        source.EnqueueLookupGate(race.B);
        return race;
    }

    private static SalesReceiptDto Seed()
    {
        var lines = new[]
        {
            new SalesReceiptLineDto(101, 1, 11, "pcs", 2m, 12m, false, 24m, null, null, "Demo Product A", true),
            new SalesReceiptLineDto(102, 2, 11, "pcs", 1m, 12m, true, 0m, null, null, "Demo Product A", true),
        };
        return new SalesReceiptDto(SeedSaleId, "SA0000000041", ReferenceDate, 1, 2, 3, "Draft", "Demo draft",
            null, "lab-v1", lines, 24m, null, null, "Demo Customer", true, "Demo Warehouse", true,
            "Demo Employee", true);
    }
}

public sealed class ScenarioLookupRace
{
    public ScenarioGate<ApiResponse<IReadOnlyList<CatalogLookupDto>>> A { get; } =
        new ScenarioGate<ApiResponse<IReadOnlyList<CatalogLookupDto>>> { IgnoreCancellation = true };
    public ScenarioGate<ApiResponse<IReadOnlyList<CatalogLookupDto>>> B { get; } =
        new ScenarioGate<ApiResponse<IReadOnlyList<CatalogLookupDto>>>();

    public void CompleteA() => A.Complete(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(
        new[] { new CatalogLookupDto(11, "P-011", "Demo Product A", true) }));
    public void CompleteB() => B.Complete(ApiResponse<IReadOnlyList<CatalogLookupDto>>.Success(
        new[] { new CatalogLookupDto(12, "P-012", "Demo Product B", true) }));
}
