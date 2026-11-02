using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Server.Application.Security;
using Xunit;
namespace MyDmsVn.Server.Application.Tests;

public sealed class WebOperationContextTests
{
    [Theory]
    [InlineData("11111111-1111-4111-0111-111111111111")]
    [InlineData("11111111-1111-3111-8111-111111111111")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Metadata_requires_version4_and_RFC4122_variant(string id)
    {
        Assert.Throws<ArgumentException>(() => Create(Guid.Parse(id), WebOperationKind.SalesCreate, null));
    }

    [Fact]
    public void Fingerprint_copies_are_immutable_and_targets_match_the_kind()
    {
        var hash = new byte[32];
        var context = new WebOperationContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), WebOperationKind.SalesCreate, null, hash, new Codec());
        hash[0] = 1;
        var returned = context.Fingerprint;
        returned[1] = 2;
        Assert.Equal(new byte[32], context.Fingerprint);
        Assert.Throws<ArgumentException>(() => Create(Guid.NewGuid(), WebOperationKind.SalesCreate, 1));
        Assert.Throws<ArgumentException>(() => Create(Guid.NewGuid(), WebOperationKind.SalesUpdate, null));
        Assert.Throws<ArgumentException>(() => Create(Guid.NewGuid(), WebOperationKind.ReceiptPost, 0));
        Assert.Throws<ArgumentException>(() => Create(Guid.NewGuid(), (WebOperationKind)7, null));
    }

    [Theory]
    [InlineData(WebOperationKind.SalesCreate)]
    [InlineData(WebOperationKind.SalesUpdate)]
    [InlineData(WebOperationKind.SalesPost)]
    [InlineData(WebOperationKind.ReceiptCreate)]
    [InlineData(WebOperationKind.ReceiptUpdate)]
    [InlineData(WebOperationKind.ReceiptPost)]
    public async Task Replay_binds_decoded_document_to_immutable_result_identity_before_commit(WebOperationKind kind)
    {
        foreach (var decodedId in new[] { 41L, 42L, 43L })
        {
            var fixture = new ReplayFixture(decodedId == 43 ? new object() : Result(kind, decodedId));
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddSingleton<IUnitOfWorkFactory>(fixture);
            services.AddSingleton<ICurrentUserAccessor>(fixture);
            services.AddSingleton<IPermissionStore>(fixture);
            using var provider = services.BuildServiceProvider();
            var sender = provider.GetRequiredService<ISender>();
            var target = kind == WebOperationKind.SalesCreate || kind == WebOperationKind.ReceiptCreate ? (long?)null : 41L;
            var operation = new WebOperationContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), kind, target, new byte[32], fixture);
            var date = new DateTime(2026, 11, 2);
            var salesLines = new[] { new SaveSalesReceiptLineRequest(0, 1, 1, 1, false) };
            var receiptLines = new[] { new SaveGoodsReceiptLineRequest(1, 1, 1) };
            switch (kind)
            {
                case WebOperationKind.SalesCreate:
                    Check(await sender.Send(new CreateSalesReceiptDraftCommand(new SaveSalesReceiptRequest(date, 1, 1, 1, null, salesLines), operation))); break;
                case WebOperationKind.SalesUpdate:
                    Check(await sender.Send(new UpdateSalesReceiptDraftCommand(new UpdateSalesReceiptDraftRequest(41, "AAAAAAAAAAA=", date, 1, 1, 1, null, salesLines), operation))); break;
                case WebOperationKind.SalesPost:
                    Check(await sender.Send(new PostSalesReceiptCommand(new PostSalesReceiptRequest(41, "AAAAAAAAAAA="), operation))); break;
                case WebOperationKind.ReceiptCreate:
                    Check(await sender.Send(new CreateGoodsReceiptDraftCommand(new SaveGoodsReceiptRequest(date, 1, 1, null, receiptLines), operation))); break;
                case WebOperationKind.ReceiptUpdate:
                    Check(await sender.Send(new UpdateGoodsReceiptDraftCommand(new UpdateGoodsReceiptDraftRequest(41, "AAAAAAAAAAA=", date, 1, 1, null, receiptLines), operation))); break;
                case WebOperationKind.ReceiptPost:
                    Check(await sender.Send(new PostGoodsReceiptCommand(new PostGoodsReceiptRequest(41, "AAAAAAAAAAA="), operation))); break;
            }

            void Check<T>(ErrorOr<T> result)
            {
                if (decodedId == 41)
                {
                    Assert.False(result.IsError);
                    Assert.True(fixture.Committed);
                }
                else
                {
                    Assert.True(result.IsError);
                    Assert.Equal("Operation.ResultUnavailable", result.FirstError.Code);
                    Assert.False(fixture.Committed);
                }
            }
        }
    }

    private static object Result(WebOperationKind kind, long id) => kind switch
    {
        WebOperationKind.SalesCreate or WebOperationKind.SalesUpdate => new SalesReceiptDto(id, "SA0000000041", new DateTime(2026, 11, 2), 1, 1, 1, "Draft", null, null, "AAAAAAAAAAA=", new[] { new SalesReceiptLineDto(1, 1, 1, "EA", 1, 1, false, 1, null, null, "Product", true) }, 1, null, null, "Customer", true, "Warehouse", true, "Employee", true),
        WebOperationKind.SalesPost => new PostSalesReceiptResponse(id, "Posted", new DateTime(2026, 11, 2, 1, 2, 3, DateTimeKind.Utc), "AAAAAAAAAAA="),
        WebOperationKind.ReceiptCreate or WebOperationKind.ReceiptUpdate => new GoodsReceiptDto(id, "GR0000000041", new DateTime(2026, 11, 2), 1, 1, "Draft", null, null, "AAAAAAAAAAA=", new[] { new GoodsReceiptLineDto(1, 1, 1, 1, 1) }),
        WebOperationKind.ReceiptPost => new PostGoodsReceiptResponse(id, "Posted", new DateTime(2026, 11, 2, 1, 2, 3, DateTimeKind.Utc)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    // Persistence and codec boundaries supply controlled stored metadata and decoded DTOs; the
    // real Application pipeline must bind them before returning success or committing the UoW.
    private sealed class ReplayFixture : IUnitOfWorkFactory, IUnitOfWork, IWebOperationJournal,
        IWebOperationResultCodec, ICurrentUserAccessor, IPermissionStore
    {
        private readonly object _decoded;
        public ReplayFixture(object decoded) { _decoded = decoded; }
        public bool Committed { get; private set; }
        public CurrentUser Current => CurrentUser.Authenticated(42, "actor", "Actor", true);
        public UnitOfWorkState State { get; private set; }
        public IUnitOfWork Create() => this;
        public Task<IUnitOfWork> CreateAsync(CancellationToken token) => Task.FromResult<IUnitOfWork>(this);
        public void BeginTransaction() { State = UnitOfWorkState.ActiveTransaction; }
        public T Repository<T>() where T : class => typeof(T) == typeof(IWebOperationJournal) ? (T)(object)this : throw new InvalidOperationException("Replay cannot perform business I/O.");
        public void Commit() { Committed = true; State = UnitOfWorkState.Committed; }
        public void Rollback() { State = UnitOfWorkState.RolledBack; }
        public void Dispose() { }
        public Task<WebOperationReplay?> AdmitAsync(WebOperationContext context, int actor, CancellationToken token)
            => Task.FromResult<WebOperationReplay?>(new WebOperationReplay(41, new byte[] { 1 }));
        public Task CompleteAsync(WebOperationContext context, int actor, long resultDocumentId, byte[] bytes, CancellationToken token)
            => throw new InvalidOperationException("Replay cannot capture a second result.");
        public byte[] Encode<T>(WebOperationKind kind, T originalResult) => throw new NotSupportedException();
        public T Decode<T>(WebOperationKind kind, byte[] bytes) => (T)_decoded;
        public Task<PermissionSnapshot> GetSnapshotAsync(int userId, string permissionKey, CancellationToken token)
            => Task.FromResult(new PermissionSnapshot(null, true));
    }

    private static WebOperationContext Create(Guid id, WebOperationKind kind, long? target)
        => new WebOperationContext(id, Guid.NewGuid(), Guid.NewGuid(), kind, target, new byte[32], new Codec());
    private sealed class Codec : IWebOperationResultCodec
    {
        public byte[] Encode<T>(WebOperationKind kind, T result) => throw new NotSupportedException();
        public T Decode<T>(WebOperationKind kind, byte[] bytes) => throw new NotSupportedException();
    }
}
