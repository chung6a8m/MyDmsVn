using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Server.Application.Security;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class LocalSalesReceiptApiClientTests
{
    [Theory]
    [InlineData("List")]
    [InlineData("Get")]
    [InlineData("Create")]
    [InlineData("Update")]
    [InlineData("Post")]
    public async Task R2_Bound_client_rejects_replacement_before_account_permission_or_handler_admission(string operation)
    {
        var state = new LocalState(); using var provider = Provider(state); await Login(provider, "first");
        var session = provider.GetRequiredService<IDesktopSession>();
        var originalVersion = session.Version;
        var bound = provider.GetRequiredService<ISalesReceiptApiClient>().ForSession(originalVersion);
        session.SignOut(); await Login(provider, "second"); var accounts = state.AccountValidations;
        var save = ValidSave();
        var response = operation switch
        {
            "List" => await Rejected(bound.ListAsync(new SalesReceiptListRequest(null, null, null, null, null, null, null, null), default)),
            "Get" => await Rejected(bound.GetAsync(41, default)),
            "Create" => await Rejected(bound.CreateDraftAsync(save, default)),
            "Update" => await Rejected(bound.UpdateDraftAsync(new UpdateSalesReceiptDraftRequest(41, SalesTestData.Version, save.SaleDate, 1, 2, 3, null, save.Lines), default)),
            _ => await Rejected(bound.PostAsync(new PostSalesReceiptRequest(41, SalesTestData.Version), default)),
        };
        Assert.NotNull(response); Assert.Equal("Auth.SessionChanged", response!.Code); Assert.Equal(ApiStatusCode.Unauthorized, response.Status);
        Assert.False(string.IsNullOrWhiteSpace(response.CorrelationId));
        Assert.Equal(accounts, state.AccountValidations); Assert.Empty(state.Requests); Assert.Empty(state.Keys);
        Assert.Equal(22, session.CurrentUser!.UserId);
        Assert.Same(bound, bound.ForSession(originalVersion));
        Assert.Throws<InvalidOperationException>(() => bound.ForSession(session.Version));
    }

    [Fact]
    public async Task R2_Bound_client_after_signout_does_not_admit_account_or_handler()
    {
        var state = new LocalState(); using var provider = Provider(state); await Login(provider, "first");
        var session = provider.GetRequiredService<IDesktopSession>();
        var bound = provider.GetRequiredService<ISalesReceiptApiClient>().ForSession(session.Version);
        session.SignOut(); var accounts = state.AccountValidations;
        var error = await Rejected(bound.GetAsync(41, default));
        Assert.Equal("Auth.SessionChanged", error!.Code); Assert.Empty(state.Requests); Assert.Empty(state.Keys);
        Assert.Equal(accounts, state.AccountValidations); Assert.False(session.IsAuthenticated);
    }

    private static async Task<ApiError?> Rejected<T>(Task<ApiResponse<T>> request)
    { var result = await request; Assert.False(result.IsSuccess); Assert.Null(result.Data); return result.Error; }

    [Fact]
    public async Task All_five_operations_preserve_request_shapes_and_independent_permission_keys()
    {
        var state = new LocalState(); using var provider = Provider(state); await Login(provider, "first");
        var client = provider.GetRequiredService<ISalesReceiptApiClient>();
        var save = ValidSave();
        var update = new UpdateSalesReceiptDraftRequest(41, SalesTestData.Version, save.SaleDate, 1, 2, 3, "note", save.Lines);
        var list = new SalesReceiptListRequest(null, null, 1, 2, 3, "Draft", 7, "SA", 1);
        var post = new PostSalesReceiptRequest(41, SalesTestData.Version);
        Assert.True((await client.ListAsync(list, default)).IsSuccess);
        Assert.True((await client.GetAsync(41, default)).IsSuccess);
        Assert.True((await client.CreateDraftAsync(save, default)).IsSuccess);
        Assert.True((await client.UpdateDraftAsync(update, default)).IsSuccess);
        Assert.True((await client.PostAsync(post, default)).IsSuccess);
        Assert.Equal(new[] { "Sales.Receipts.Read", "Sales.Receipts.Read", "Sales.Receipts.Write", "Sales.Receipts.Write", "Sales.Receipts.Post" }, state.Keys);
        Assert.Same(list, ((ListSalesReceiptsQuery)state.Requests[0]).Request);
        Assert.Equal(41, ((GetSalesReceiptByIdQuery)state.Requests[1]).SaleId);
        Assert.Same(save, ((CreateSalesReceiptDraftCommand)state.Requests[2]).Request);
        Assert.Same(update, ((UpdateSalesReceiptDraftCommand)state.Requests[3]).Request);
        Assert.Same(post, ((PostSalesReceiptCommand)state.Requests[4]).Request);
    }

    [Theory]
    [InlineData("Sales.Receipts.Read")]
    [InlineData("Sales.Receipts.Write")]
    [InlineData("Sales.Receipts.Post")]
    public async Task Each_service_checks_its_own_permission_and_direct_deny_blocks_dispatch(string denied)
    {
        var state = new LocalState { DeniedKey = denied }; using var provider = Provider(state); await Login(provider, "first");
        var client = provider.GetRequiredService<ISalesReceiptApiClient>();
        if (denied.EndsWith("Read", StringComparison.Ordinal))
        { Assert.Equal(ApiStatusCode.Forbidden, (await client.ListAsync(new SalesReceiptListRequest(null, null, null, null, null, null, null, null), default)).Error!.Status);
          Assert.Equal(ApiStatusCode.Forbidden, (await client.GetAsync(41, default)).Error!.Status); }
        else if (denied.EndsWith("Write", StringComparison.Ordinal))
        { var save = ValidSave(); Assert.Equal(ApiStatusCode.Forbidden, (await client.CreateDraftAsync(save, default)).Error!.Status);
          Assert.Equal(ApiStatusCode.Forbidden, (await client.UpdateDraftAsync(new UpdateSalesReceiptDraftRequest(41, SalesTestData.Version, save.SaleDate, 1, 2, 3, null, save.Lines), default)).Error!.Status); }
        else Assert.Equal(ApiStatusCode.Forbidden, (await client.PostAsync(new PostSalesReceiptRequest(41, SalesTestData.Version), default)).Error!.Status);
        Assert.Empty(state.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_and_conflict_keep_typed_code_details_and_correlation(bool conflict)
    {
        var code = conflict ? "SalesReceipt.AlreadyPosted" : "SalesReceipt.NotFound";
        var state = new LocalState { Error = conflict ? Error.Conflict(code, "business") : Error.NotFound(code, "business") };
        using var provider = Provider(state); await Login(provider, "first");
        var response = await provider.GetRequiredService<ISalesReceiptApiClient>().GetAsync(41, default);
        Assert.Equal(conflict ? ApiStatusCode.Conflict : ApiStatusCode.NotFound, response.Error!.Status);
        Assert.Equal(code, response.Error.Code); Assert.Equal(code, Assert.Single(response.Error.Details).Code);
        Assert.False(string.IsNullOrWhiteSpace(response.Error.CorrelationId)); Assert.Null(response.Data);
    }

    [Fact]
    public async Task Validation_strips_request_root_and_cancellation_propagates()
    {
        var state = new LocalState(); using var provider = Provider(state); await Login(provider, "first");
        var client = provider.GetRequiredService<ISalesReceiptApiClient>();
        var invalid = await client.CreateDraftAsync(new SaveSalesReceiptRequest(DateTime.Today, 0, 2, 3, null, Array.Empty<SaveSalesReceiptLineRequest>()), default);
        Assert.Equal(ApiStatusCode.BadRequest, invalid.Error!.Status);
        Assert.Contains(invalid.Error.Details, detail => detail.Field == "customerId"); Assert.Empty(state.Requests);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync(41, canceled.Token));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Replacement_during_account_validation_fences_success_inactive_and_unavailable(bool inactive, bool unavailable)
    {
        var state = new LocalState();
        using var provider = Provider(state); await Login(provider, "first");
        state.AccountPause = true; state.Active = !inactive; state.AccountThrows = unavailable;
        var pending = provider.GetRequiredService<ISalesReceiptApiClient>().GetAsync(41, default);
        await Within(state.ValidationStarted.Task); provider.GetRequiredService<IDesktopSession>().SignOut();
        state.AccountPause = false; state.Active = true; state.AccountThrows = false; await Login(provider, "second");
        state.ValidationRelease.SetResult(true);
        var response = await Within(pending); Assert.Equal("Auth.SessionChanged", response.Error!.Code); Assert.Empty(state.Requests);
        Assert.Equal(22, provider.GetRequiredService<IDesktopSession>().CurrentUser!.UserId);
    }

    [Fact]
    public async Task Current_inactive_expires_session_and_no_actor_never_dispatches()
    {
        var state = new LocalState(); using var provider = Provider(state); await Login(provider, "first"); state.Active = false;
        var client = provider.GetRequiredService<ISalesReceiptApiClient>();
        Assert.Equal("Auth.Unauthorized", (await client.GetAsync(41, default)).Error!.Code);
        Assert.False(provider.GetRequiredService<IDesktopSession>().IsAuthenticated);
        Assert.Equal(ApiStatusCode.Unauthorized, (await client.GetAsync(41, default)).Error!.Status); Assert.Empty(state.Requests);
    }

    [Fact]
    public async Task Admitted_actor_finishes_but_replacement_session_never_receives_old_sale()
    {
        var state = new LocalState { Pause = true };
        using var provider = Provider(state);
        await Login(provider, "first");
        var pending = provider.GetRequiredService<ISalesReceiptApiClient>()
            .ForSession(provider.GetRequiredService<IDesktopSession>().Version).GetAsync(41, CancellationToken.None);
        await Within(state.Started.Task);
        provider.GetRequiredService<IDesktopSession>().SignOut();
        await Login(provider, "second");
        state.Release.SetResult(true);
        var response = await Within(pending);
        Assert.False(response.IsSuccess);
        Assert.Null(response.Data);
        Assert.Equal("Auth.SessionChanged", response.Error!.Code);
        Assert.Equal(ApiStatusCode.Unauthorized, response.Error.Status);
        Assert.False(string.IsNullOrWhiteSpace(response.Error.CorrelationId));
        Assert.Equal(state.Correlation, response.Error.CorrelationId);
        Assert.Equal(new[] { 11, 11 }, state.Actors);
        Assert.Equal(22, provider.GetRequiredService<IDesktopSession>().CurrentUser!.UserId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unauthorized_expires_only_the_current_request_session(bool replace)
    {
        var state = new LocalState { Pause = true, Error = Error.Unauthorized("Auth.Unauthorized", "Inactive") };
        using var provider = Provider(state);
        await Login(provider, "first");
        var pending = provider.GetRequiredService<ISalesReceiptApiClient>()
            .ForSession(provider.GetRequiredService<IDesktopSession>().Version).GetAsync(41, CancellationToken.None);
        await Within(state.Started.Task);
        if (replace)
        {
            provider.GetRequiredService<IDesktopSession>().SignOut();
            await Login(provider, "second");
        }
        state.Release.SetResult(true);
        var response = await Within(pending);
        Assert.Equal(replace ? "Auth.SessionChanged" : "Auth.Unauthorized", response.Error!.Code);
        Assert.Equal(replace, provider.GetRequiredService<IDesktopSession>().IsAuthenticated);
    }

    internal static async Task<T> Within<T>(Task<T> task)
    {
        Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5))));
        return await task;
    }
    internal static async Task Within(Task task)
    {
        Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5))));
        await task;
    }
    private static async Task Login(ServiceProvider provider, string name) => Assert.True((await provider
        .GetRequiredService<IIdentityApiClient>().LoginAsync(new LoginRequest(name, "test"), CancellationToken.None)).IsSuccess);

    private static ServiceProvider Provider(LocalState state)
    {
        var services = new ServiceCollection();
        services.AddServerApplication();
        services.AddLocalDesktopAdapter();
        services.AddSingleton(state);
        services.AddSingleton<IUserAccountValidator, ActiveAccount>();
        services.AddSingleton<IPermissionStore, Grants>();
        services.AddTransient<IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>, LoginHandler>();
        services.AddTransient<IRequestHandler<GetSalesReceiptByIdQuery, ErrorOr<SalesReceiptDto>>, GetHandler>();
        services.AddTransient<IRequestHandler<ListSalesReceiptsQuery, ErrorOr<SalesReceiptListPage>>, GetHandler>();
        services.AddTransient<IRequestHandler<CreateSalesReceiptDraftCommand, ErrorOr<SalesReceiptDto>>, GetHandler>();
        services.AddTransient<IRequestHandler<UpdateSalesReceiptDraftCommand, ErrorOr<SalesReceiptDto>>, GetHandler>();
        services.AddTransient<IRequestHandler<PostSalesReceiptCommand, ErrorOr<PostSalesReceiptResponse>>, GetHandler>();
        return services.BuildServiceProvider();
    }
    private sealed class LocalState
    {
        public bool Pause;
        public bool AccountPause;
        public bool AccountThrows;
        public bool Active = true;
        public string? DeniedKey;
        public string? Correlation;
        public int AccountValidations;
        public readonly List<string> Keys = new List<string>();
        public readonly List<object> Requests = new List<object>();
        public readonly TaskCompletionSource<bool> ValidationStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> ValidationRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public Error? Error;
        public readonly TaskCompletionSource<bool> Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> Release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly List<int> Actors = new List<int>();
    }
    private sealed class GetHandler : IRequestHandler<GetSalesReceiptByIdQuery, ErrorOr<SalesReceiptDto>>,
        IRequestHandler<ListSalesReceiptsQuery, ErrorOr<SalesReceiptListPage>>,
        IRequestHandler<CreateSalesReceiptDraftCommand, ErrorOr<SalesReceiptDto>>,
        IRequestHandler<UpdateSalesReceiptDraftCommand, ErrorOr<SalesReceiptDto>>,
        IRequestHandler<PostSalesReceiptCommand, ErrorOr<PostSalesReceiptResponse>>
    {
        private readonly LocalState _state;
        private readonly ICurrentUserAccessor _actor;
        private readonly IRequestCorrelationAccessor _correlation;
        public GetHandler(LocalState state, ICurrentUserAccessor actor, IRequestCorrelationAccessor correlation) { _state = state; _actor = actor; _correlation = correlation; }
        public async Task<ErrorOr<SalesReceiptDto>> Handle(GetSalesReceiptByIdQuery query, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); _state.Requests.Add(query);
            _state.Correlation = _correlation.CorrelationId;
            _state.Actors.Add(_actor.Current.UserId!.Value);
            _state.Started.TrySetResult(true);
            if (_state.Pause) await Within(_state.Release.Task);
            _state.Actors.Add(_actor.Current.UserId!.Value);
            if (_state.Error.HasValue) return _state.Error.Value;
            return SalesTestData.Receipt();
        }
        public Task<ErrorOr<SalesReceiptListPage>> Handle(ListSalesReceiptsQuery query, CancellationToken token)
        { _state.Requests.Add(query); return Task.FromResult<ErrorOr<SalesReceiptListPage>>(SalesApi.Page(41)); }
        public Task<ErrorOr<SalesReceiptDto>> Handle(CreateSalesReceiptDraftCommand query, CancellationToken token)
        { _state.Requests.Add(query); return Task.FromResult<ErrorOr<SalesReceiptDto>>(SalesTestData.Receipt()); }
        public Task<ErrorOr<SalesReceiptDto>> Handle(UpdateSalesReceiptDraftCommand query, CancellationToken token)
        { _state.Requests.Add(query); return Task.FromResult<ErrorOr<SalesReceiptDto>>(SalesTestData.Receipt()); }
        public Task<ErrorOr<PostSalesReceiptResponse>> Handle(PostSalesReceiptCommand query, CancellationToken token)
        { _state.Requests.Add(query); return Task.FromResult<ErrorOr<PostSalesReceiptResponse>>(new PostSalesReceiptResponse(41, "Posted", DateTime.UtcNow, SalesTestData.Version)); }
    }
    private sealed class LoginHandler : IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>
    {
        public Task<ErrorOr<CurrentUserDto>> Handle(LoginCommand request, CancellationToken token) =>
            Task.FromResult<ErrorOr<CurrentUserDto>>(new CurrentUserDto(request.Username == "first" ? 11 : 22, request.Username, request.Username));
    }
    private sealed class ActiveAccount : IUserAccountValidator
    {
        private readonly LocalState _state;
        public ActiveAccount(LocalState state) => _state = state;
        public async Task<bool> IsActiveAsync(int userId, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _state.AccountValidations++;
            var active = _state.Active; var unavailable = _state.AccountThrows;
            if (_state.AccountPause) { _state.ValidationStarted.TrySetResult(true); await Within(_state.ValidationRelease.Task); }
            if (unavailable) throw new InvalidOperationException("unavailable");
            return active;
        }
    }
    private sealed class Grants : IPermissionStore
    {
        private readonly LocalState _state;
        public Grants(LocalState state) => _state = state;
        public Task<PermissionSnapshot> GetSnapshotAsync(int userId, string key, CancellationToken token)
        { _state.Keys.Add(key); return Task.FromResult(new PermissionSnapshot(key != _state.DeniedKey, true)); }
    }
    private static SaveSalesReceiptRequest ValidSave() => new SaveSalesReceiptRequest(DateTime.Today, 1, 2, 3, "note", new[] { new SaveSalesReceiptLineRequest(0, 7, 2, 4, false) });
}

internal static class SalesTestData
{
    public static readonly string Version = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
    public static SalesReceiptDto Receipt(long id = 41, bool posted = false, params SalesReceiptLineDto[] lines) =>
        new SalesReceiptDto(id, "SA0000000041", new DateTime(2026, 10, 22), 1, 2, 3, posted ? "Posted" : "Draft", "note",
            posted ? new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc) : (DateTime?)null, Version,
            lines.Length == 0 ? new[] { Line(91, 1, posted) } : lines, 8m, posted ? 5m : (decimal?)null, posted ? 3m : (decimal?)null,
            "Customer", true, "Warehouse", true, "Employee", true);
    public static SalesReceiptLineDto Line(long id = 91, int ordinal = 1, bool posted = false) =>
        new SalesReceiptLineDto(id, ordinal, 7, "box", 2m, 4m, false, 8m, posted ? 2.5m : (decimal?)null,
            posted ? 5m : (decimal?)null, "Product", true);
}
