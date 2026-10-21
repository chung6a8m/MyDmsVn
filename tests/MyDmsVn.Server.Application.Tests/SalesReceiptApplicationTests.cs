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
using Xunit;

namespace MyDmsVn.Server.Application.Tests;

public sealed class SalesReceiptApplicationTests
{
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
