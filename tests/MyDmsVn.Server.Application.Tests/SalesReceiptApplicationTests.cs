using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Domain.Sales;
using MyDmsVn.Server.Domain.Inventory;
using Xunit;

namespace MyDmsVn.Server.Application.Tests;

public sealed class SalesReceiptApplicationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("AQ==")]
    [InlineData("AAAAAAAAAAAA")]
    public async Task Malformed_post_version_never_opens_uow(string? version)
    {
        var factory = new RejectingFactory(); using var provider = Provider(factory, "post");
        var result = await provider.GetRequiredService<ISender>().Send(new PostSalesReceiptCommand(new PostSalesReceiptRequest(1, version!)));
        Assert.True(result.IsError); Assert.Contains(result.Errors, error => error.Code == "Validation.InvalidVersion"); Assert.Equal(0, factory.Opened);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Invalid_post_id_never_opens_uow(long id)
    {
        var factory = new RejectingFactory(); using var provider = Provider(factory, "post");
        var result = await provider.GetRequiredService<ISender>().Send(new PostSalesReceiptCommand(new PostSalesReceiptRequest(id, "AAAAAAAAAAA=")));
        Assert.True(result.IsError); Assert.Equal(ErrorType.Validation, result.FirstError.Type); Assert.Equal(0, factory.Opened);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    [InlineData("deny")]
    [InlineData("inactive")]
    public async Task Post_requires_its_independent_permission_and_current_active_actor(string grant)
    {
        var factory = new RejectingFactory(); using var provider = Provider(factory, grant);
        var result = await provider.GetRequiredService<ISender>().Send(new PostSalesReceiptCommand(new PostSalesReceiptRequest(1, "AAAAAAAAAAA=")));
        Assert.True(result.IsError); Assert.Equal(grant == "inactive" ? ErrorType.Unauthorized : ErrorType.Forbidden, result.FirstError.Type); Assert.Equal(0, factory.Opened);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Post_materializes_authoritative_response_before_commit_and_audits_only_committed_result(bool invalidResponse)
    {
        var fixture = new PostFixture { InvalidResponse = invalidResponse };
        var services = new ServiceCollection(); services.AddServerApplication();
        services.AddSingleton<IUnitOfWorkFactory>(fixture); services.AddSingleton<ICurrentUserAccessor>(new Actor(true));
        services.AddSingleton<IPermissionStore>(new Permissions("post")); services.AddSingleton<ISecurityAuditSink>(fixture);
        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<ISender>().Send(new PostSalesReceiptCommand(new PostSalesReceiptRequest(1, "AAAAAAAAAAA=")));
        if (invalidResponse) { Assert.True(result.IsError); Assert.DoesNotContain("commit", fixture.Events); Assert.DoesNotContain("audit", fixture.Events); }
        else
        {
            Assert.False(result.IsError); Assert.Equal("AQEBAQEBAQE=", result.Value.Version);
            Assert.Equal(new[] { "begin", "gate", "header", "snapshot", "balances", "mark", "apply", "commit", "audit", "dispose" }, fixture.Events);
            Assert.Equal(42, fixture.PostedActor); Assert.Equal(42, fixture.AuditActor);
        }
    }

    private sealed class PostFixture : IUnitOfWorkFactory, IUnitOfWork, ISalesReceiptWriteRepository, ISecurityAuditSink
    {
        public bool InvalidResponse { get; set; }
        public string? Failure { get; set; }
        public int PostedActor { get; private set; }
        public int? AuditActor { get; private set; }
        public List<string> Events { get; } = new();
        public UnitOfWorkState State { get; private set; }
        public IUnitOfWork Create() => this;
        public Task<IUnitOfWork> CreateAsync(CancellationToken token) => Task.FromResult<IUnitOfWork>(this);
        public void BeginTransaction() { Events.Add("begin"); State = UnitOfWorkState.ActiveTransaction; }
        public T Repository<T>() where T : class => (this as T)!;
        public void Commit() { Events.Add("commit"); State = UnitOfWorkState.Committed; }
        public void Rollback() { }
        public void Dispose() => Events.Add("dispose");
        public Task AcquirePostingGateAsync(CancellationToken token) { Events.Add("gate"); return Task.CompletedTask; }
        public Task<SalesReceipt?> LockHeaderAsync(long id, CancellationToken token)
        { Events.Add("header"); return Task.FromResult<SalesReceipt?>(Failure == "missing" ? null : new SalesReceipt { SaleId = 1, WarehouseId = 1, CustomerId = 1, EmployeeId = 1, Status = Failure == "posted" ? "Posted" : "Draft", Version = Failure == "stale" ? Enumerable.Repeat((byte)1, 8).ToArray() : new byte[8] }); }
        public Task<SalesReceiptPostSnapshot> LockPostSnapshotAsync(SalesReceipt header, CancellationToken token)
        {
            Events.Add("snapshot"); var reference = new SalesCatalogReference(CatalogReferenceState.Active, "Valid");
            var missing = new SalesCatalogReference(CatalogReferenceState.Missing, null);
            return Task.FromResult(new SalesReceiptPostSnapshot(new[] { new SalesReceiptLine { SaleId = 1, SaleLineId = 1, LineNo = Failure == "order" ? 2 : 1, ProductId = 1, Unit = "EA", Qty = 1, UnitPrice = 2, LineAmount = 2 } },
                new SalesReceiptReferenceSnapshot(Failure == "customer" ? missing : reference, Failure == "warehouse" ? missing : reference, Failure == "employee" ? missing : reference,
                    new Dictionary<int, SalesProductReference> { [1] = new SalesProductReference(Failure == "product" ? CatalogReferenceState.Missing : CatalogReferenceState.Active, "P", "EA") })));
        }
        public Task<IReadOnlyDictionary<int, StockValuationState>> LockValuationBalancesAsync(int w, IReadOnlyCollection<int> ids, CancellationToken token)
        { Events.Add("balances"); return Task.FromResult<IReadOnlyDictionary<int, StockValuationState>>(new Dictionary<int, StockValuationState> { [1] = new StockValuationState(1, 1) }); }
        public Task<SalesReceipt?> TryMarkPostedAsync(long id, byte[] version, DateTime stamp, int actor, CancellationToken token)
        { Events.Add("mark"); PostedActor = actor; return Task.FromResult<SalesReceipt?>(new SalesReceipt { SaleId = 1, Status = "Posted", PostedAtUtc = stamp, Version = InvalidResponse ? Array.Empty<byte>() : Enumerable.Repeat((byte)1, 8).ToArray() }); }
        public Task ApplyPostedInventoryAsync(SalesReceipt h, IReadOnlyList<SalesReceiptLine> lines, IReadOnlyDictionary<int, InventoryIssueCost> costs, DateTime stamp, int actor, CancellationToken token)
        { Events.Add("apply"); Assert.Equal(42, actor); Assert.Equal(1m, costs[1].CostAmount); return Task.CompletedTask; }
        public Task WriteAsync(SecurityAuditEntry entry, CancellationToken token)
        { Assert.Equal(UnitOfWorkState.Committed, State); Assert.False(token.CanBeCanceled); Assert.Equal(SecurityAuditAction.SalesReceiptPost, entry.Action); Assert.Equal(SecurityAuditOutcome.Succeeded, entry.Outcome); AuditActor = entry.UserId; Events.Add("audit"); throw new InvalidOperationException("Expected sink failure after committed business result"); }
        public Task<SalesReceiptReferenceSnapshot> GetReferenceSnapshotAsync(int c, int w, int e, IReadOnlyCollection<int> p, CancellationToken t) => throw new NotSupportedException();
        public Task<SalesReceipt> InsertDraftAsync(SalesReceipt h, IReadOnlyList<SalesReceiptLine> l, CancellationToken t) => throw new NotSupportedException();
        public Task<IReadOnlyList<SalesReceiptLine>> LoadLinesAsync(long id, CancellationToken t) => throw new NotSupportedException();
        public Task<SalesReceipt> ReplaceDraftAsync(SalesReceipt h, IReadOnlyList<SalesReceiptLine> l, CancellationToken t) => throw new NotSupportedException();
    }

    [Theory]
    [InlineData("missing", "SalesReceipt.NotFound")]
    [InlineData("posted", "SalesReceipt.AlreadyPosted")]
    [InlineData("stale", "SalesReceipt.ConcurrencyConflict")]
    [InlineData("customer", "SalesReceipt.CustomerNotFound")]
    [InlineData("warehouse", "SalesReceipt.WarehouseNotFound")]
    [InlineData("employee", "SalesReceipt.EmployeeNotFound")]
    [InlineData("product", "SalesReceipt.ProductNotFound")]
    [InlineData("order", "SalesReceipt.InvalidStoredLines")]
    public async Task Post_stops_before_mutation_for_invalid_header_or_missing_references(string failure, string code)
    {
        var fixture = new PostFixture { Failure = failure }; var services = new ServiceCollection(); services.AddServerApplication();
        services.AddSingleton<IUnitOfWorkFactory>(fixture); services.AddSingleton<ICurrentUserAccessor>(new Actor(true));
        services.AddSingleton<IPermissionStore>(new Permissions("post")); services.AddSingleton<ISecurityAuditSink>(fixture);
        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<ISender>().Send(new PostSalesReceiptCommand(new PostSalesReceiptRequest(1, "AAAAAAAAAAA=")));
        Assert.Equal(code, result.FirstError.Code); Assert.DoesNotContain("mark", fixture.Events); Assert.DoesNotContain("apply", fixture.Events); Assert.DoesNotContain("commit", fixture.Events); Assert.DoesNotContain("audit", fixture.Events);
    }
    // These cases catch a validator that admits invalid buffers and reaches persistence.
    [Theory]
    [InlineData("date")]
    [InlineData("customer")]
    [InlineData("warehouse")]
    [InlineData("employee")]
    [InlineData("note-length")]
    [InlineData("note-nul")]
    [InlineData("empty")]
    [InlineData("201")]
    [InlineData("null-line")]
    [InlineData("product")]
    [InlineData("negative-id")]
    [InlineData("existing-id")]
    [InlineData("qty-zero")]
    [InlineData("qty-scale")]
    [InlineData("qty-range")]
    [InlineData("price-negative")]
    [InlineData("price-scale")]
    [InlineData("price-range")]
    [InlineData("line-overflow")]
    [InlineData("group-overflow")]
    [InlineData("total-overflow")]
    public async Task Invalid_create_buffer_is_validation_before_opening_uow(string boundary)
    {
        var factory = new RejectingFactory();
        using var provider = Provider(factory);
        var request = Invalid(boundary);
        var result = await provider.GetRequiredService<ISender>().Send(new CreateSalesReceiptDraftCommand(request));
        Assert.True(result.IsError);
        Assert.All(result.Errors, error => Assert.Equal(ErrorType.Validation, error.Type));
        Assert.Contains(result.Errors, error => error.Metadata?.ContainsKey("Field") == true);
        Assert.Equal(0, factory.Opened);
    }

    [Theory]
    [InlineData("date")]
    [InlineData("customer")]
    [InlineData("warehouse")]
    [InlineData("employee")]
    [InlineData("note-length")]
    [InlineData("note-nul")]
    [InlineData("empty")]
    [InlineData("201")]
    [InlineData("null-line")]
    [InlineData("product")]
    [InlineData("negative-id")]
    [InlineData("qty-zero")]
    [InlineData("qty-scale")]
    [InlineData("qty-range")]
    [InlineData("price-negative")]
    [InlineData("price-scale")]
    [InlineData("price-range")]
    [InlineData("line-overflow")]
    [InlineData("group-overflow")]
    [InlineData("total-overflow")]
    public async Task Invalid_update_buffer_is_validation_before_opening_uow(string boundary)
    {
        var factory = new RejectingFactory();
        using var provider = Provider(factory);
        var body = Invalid(boundary);
        var result = await provider.GetRequiredService<ISender>().Send(new UpdateSalesReceiptDraftCommand(
            new UpdateSalesReceiptDraftRequest(1, "AAAAAAAAAAA=", body.SaleDate, body.CustomerId, body.WarehouseId, body.EmployeeId, body.Note, body.Lines)));
        Assert.True(result.IsError);
        Assert.All(result.Errors, error => Assert.Equal(ErrorType.Validation, error.Type));
        Assert.Equal(0, factory.Opened);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64")]
    [InlineData("AQ==")]
    [InlineData("AAAAAAAAAAAA")]
    public async Task Malformed_update_version_is_rejected_before_opening_uow(string? version)
    {
        var factory = new RejectingFactory();
        using var provider = Provider(factory);
        var body = Valid();
        var result = await provider.GetRequiredService<ISender>().Send(new UpdateSalesReceiptDraftCommand(
            new UpdateSalesReceiptDraftRequest(1, version!, body.SaleDate, 1, 1, 1, null, body.Lines)));
        Assert.True(result.IsError);
        Assert.Contains(result.Errors, error => error.Code == "Validation.InvalidVersion");
        Assert.Equal(0, factory.Opened);
    }

    [Theory]
    [InlineData("read", "write")]
    [InlineData("write", "read")]
    [InlineData("post", "write")]
    [InlineData("post", "read")]
    [InlineData("deny", "write")]
    [InlineData("inactive", "read")]
    public async Task Each_endpoint_requires_its_own_permission_and_active_actor(string grant, string operation)
    {
        var factory = new RejectingFactory();
        using var provider = Provider(factory, grant);
        var sender = provider.GetRequiredService<ISender>();
        var result = operation == "read"
            ? await sender.Send(new GetSalesReceiptByIdQuery(1))
            : await sender.Send(new CreateSalesReceiptDraftCommand(Valid()));
        Assert.True(result.IsError);
        Assert.Equal(grant == "inactive" ? ErrorType.Unauthorized : ErrorType.Forbidden, result.FirstError.Type);
        Assert.Equal(0, factory.Opened);
    }

    [Fact]
    public async Task Authorized_get_missing_returns_sales_not_found()
    {
        var factory = new RejectingFactory();
        using var provider = Provider(factory, "read");
        var result = await provider.GetRequiredService<ISender>().Send(new GetSalesReceiptByIdQuery(13));
        Assert.True(result.IsError);
        Assert.Equal("SalesReceipt.NotFound", result.FirstError.Code);
        Assert.Equal(0, factory.Opened);
    }

    [Fact]
    public async Task Invalid_get_id_is_rejected_before_query_service()
    {
        var factory = new RejectingFactory();
        using var provider = Provider(factory, "read");
        var result = await provider.GetRequiredService<ISender>().Send(new GetSalesReceiptByIdQuery(0));
        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    private static SaveSalesReceiptRequest Valid() => new SaveSalesReceiptRequest(new DateTime(2026, 10, 22), 1, 1, 1, null,
        new[] { new SaveSalesReceiptLineRequest(0, 1, 1m, 1m, false) });

    private static SaveSalesReceiptRequest Invalid(string boundary)
    {
        var lines = Valid().Lines;
        if (boundary == "empty") lines = Array.Empty<SaveSalesReceiptLineRequest>();
        if (boundary == "201") lines = Enumerable.Range(0, 201).Select(_ => new SaveSalesReceiptLineRequest(0, 1, 1, 1, false)).ToArray();
        if (boundary == "null-line") lines = new SaveSalesReceiptLineRequest[] { null! };
        if (boundary == "group-overflow") lines = new[] { new SaveSalesReceiptLineRequest(0, 1, 99999999999999m, 0, true), new SaveSalesReceiptLineRequest(0, 1, 1, 0, false) };
        if (boundary == "total-overflow") lines = new[] { new SaveSalesReceiptLineRequest(0, 1, 1000000000m, 600000000000000m, false), new SaveSalesReceiptLineRequest(0, 2, 1000000000m, 600000000000000m, false) };
        if (new[] { "product", "negative-id", "existing-id", "qty-zero", "qty-scale", "qty-range", "price-negative", "price-scale", "price-range", "line-overflow" }.Contains(boundary))
            lines = new[] { new SaveSalesReceiptLineRequest(boundary == "negative-id" ? -1 : boundary == "existing-id" ? 7 : 0,
                boundary == "product" ? 0 : 1,
                boundary == "qty-zero" ? 0 : boundary == "qty-scale" ? .00001m : boundary == "qty-range" ? 100000000000000m : boundary == "line-overflow" ? 99999999999999.9999m : 1,
                boundary == "price-negative" ? -1 : boundary == "price-scale" ? .00001m : boundary == "price-range" ? 1000000000000000m : boundary == "line-overflow" ? 999999999999999.9999m : 1, false) };
        return new SaveSalesReceiptRequest(boundary == "date" ? default : new DateTime(2026, 10, 22), boundary == "customer" ? 0 : 1,
            boundary == "warehouse" ? 0 : 1, boundary == "employee" ? 0 : 1,
            boundary == "note-length" ? new string('x', 1001) : boundary == "note-nul" ? "a\0b" : null, lines);
    }

    private static ServiceProvider Provider(RejectingFactory factory, string grant = "write")
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUnitOfWorkFactory>(factory);
        services.AddSingleton<ICurrentUserAccessor>(new Actor(grant != "inactive"));
        services.AddSingleton<IPermissionStore>(new Permissions(grant));
        services.AddSingleton<ISalesReceiptQueryService, MissingQuery>();
        services.AddServerApplication();
        return services.BuildServiceProvider();
    }
    private sealed class RejectingFactory : IUnitOfWorkFactory
    {
        public int Opened { get; private set; }
        public IUnitOfWork Create() { Opened++; throw new InvalidOperationException("Unexpected persistence admission."); }
        public Task<IUnitOfWork> CreateAsync(CancellationToken cancellationToken) => Task.FromResult(Create());
    }
    private sealed class Actor : ICurrentUserAccessor
    {
        public Actor(bool active) => Current = CurrentUser.Authenticated(42, "operator", "Operator", active);
        public CurrentUser Current { get; }
    }
    private sealed class Permissions : IPermissionStore
    {
        private readonly string _grant;
        public Permissions(string grant) => _grant = grant;
        public Task<PermissionSnapshot> GetSnapshotAsync(int userId, string permissionKey, CancellationToken cancellationToken) =>
            Task.FromResult(new PermissionSnapshot(_grant == "deny" ? false : (bool?)null,
                hasRoleGrant: _grant == "deny" || permissionKey == (_grant == "read" ? PermissionKeys.SalesReceiptsRead : _grant == "post" ? PermissionKeys.SalesReceiptsPost : PermissionKeys.SalesReceiptsWrite)));
    }
    private sealed class MissingQuery : ISalesReceiptQueryService
    {
        public Task<SalesReceiptDto?> GetByIdAsync(long saleId, CancellationToken cancellationToken) => Task.FromResult<SalesReceiptDto?>(null);
    }
}
