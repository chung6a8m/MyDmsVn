using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;

namespace MyDmsVn.Desktop.Tests;

internal sealed class SalesSession : IDesktopSession
{
    public event EventHandler? SessionChanged;
    public bool IsAuthenticated { get; private set; } = true;
    public DesktopSessionState State => IsAuthenticated ? DesktopSessionState.Authenticated : DesktopSessionState.Unauthenticated;
    public CurrentUserDto? CurrentUser => IsAuthenticated ? new CurrentUserDto(1, "test", "Test") : null;
    public long Version { get; private set; } = 1;
    public void SignOut() { IsAuthenticated = false; Version++; SessionChanged?.Invoke(this, EventArgs.Empty); }
    public void Replace() { Version++; IsAuthenticated = true; SessionChanged?.Invoke(this, EventArgs.Empty); }
}

internal sealed class SalesApi : ISalesReceiptApiClient
{
    public SalesSession? Owner;
    public Action? BeforeGetAdmission;
    public Action? BeforeListAdmission;
    public int GetAttempts;
    public int ListAttempts;
    public ISalesReceiptApiClient ForSession(long expectedSessionVersion) => new BoundSalesApi(this, expectedSessionVersion);
    public readonly List<SalesReceiptListRequest> Lists = new List<SalesReceiptListRequest>();
    public readonly List<SaveSalesReceiptRequest> Creates = new List<SaveSalesReceiptRequest>();
    public readonly List<UpdateSalesReceiptDraftRequest> Updates = new List<UpdateSalesReceiptDraftRequest>();
    public readonly List<PostSalesReceiptRequest> Posts = new List<PostSalesReceiptRequest>();
    public readonly List<long> Gets = new List<long>();
    public Func<SalesReceiptListRequest, Task<ApiResponse<SalesReceiptListPage>>> List = _ => Task.FromResult(Ok(Page(41, "next")));
    public Func<long, Task<ApiResponse<SalesReceiptDto>>> Get = id => Task.FromResult(Ok(SalesTestData.Receipt(id)));
    public Func<SaveSalesReceiptRequest, Task<ApiResponse<SalesReceiptDto>>> Create = _ => Task.FromResult(Ok(SalesTestData.Receipt()));
    public Func<UpdateSalesReceiptDraftRequest, Task<ApiResponse<SalesReceiptDto>>> Update = _ => Task.FromResult(Ok(SalesTestData.Receipt()));
    public Func<PostSalesReceiptRequest, Task<ApiResponse<PostSalesReceiptResponse>>> Post = _ => Task.FromResult(Ok(new PostSalesReceiptResponse(41, "Posted", DateTime.UtcNow, "new-version")));
    public Task<ApiResponse<SalesReceiptListPage>> ListAsync(SalesReceiptListRequest r, CancellationToken token) => AdmitList(r, token, null);
    public Task<ApiResponse<SalesReceiptDto>> GetAsync(long id, CancellationToken token) => AdmitGet(id, token, null);
    private bool Owned(long? expected) => !expected.HasValue || Owner == null || (Owner.IsAuthenticated && Owner.Version == expected.Value);
    private Task<ApiResponse<SalesReceiptListPage>> AdmitList(SalesReceiptListRequest r, CancellationToken token, long? expected)
    {
        ListAttempts++; BeforeListAdmission?.Invoke();
        if (!Owned(expected)) return Task.FromResult(Error<SalesReceiptListPage>(ApiStatusCode.Unauthorized, "Auth.SessionChanged"));
        Lists.Add(r); return LocalSalesReceiptApiClientTests.Within(List(r));
    }
    private Task<ApiResponse<SalesReceiptDto>> AdmitGet(long id, CancellationToken token, long? expected)
    {
        GetAttempts++; BeforeGetAdmission?.Invoke();
        if (!Owned(expected)) return Task.FromResult(Error<SalesReceiptDto>(ApiStatusCode.Unauthorized, "Auth.SessionChanged"));
        Gets.Add(id); return LocalSalesReceiptApiClientTests.Within(Get(id));
    }
    public Task<ApiResponse<SalesReceiptDto>> CreateDraftAsync(SaveSalesReceiptRequest r, CancellationToken token) { Creates.Add(r); return LocalSalesReceiptApiClientTests.Within(Create(r)); }
    public Task<ApiResponse<SalesReceiptDto>> UpdateDraftAsync(UpdateSalesReceiptDraftRequest r, CancellationToken token) { Updates.Add(r); return LocalSalesReceiptApiClientTests.Within(Update(r)); }
    public Task<ApiResponse<PostSalesReceiptResponse>> PostAsync(PostSalesReceiptRequest r, CancellationToken token) { Posts.Add(r); return LocalSalesReceiptApiClientTests.Within(Post(r)); }
    public static ApiResponse<T> Ok<T>(T data) => ApiResponse<T>.Success(data);
    public static ApiResponse<T> Error<T>(ApiStatusCode status = ApiStatusCode.InternalServerError, string code = "Test.Error") =>
        ApiResponse<T>.Failure(new ApiError(status, code, "Failure", correlationId: "test-correlation"));
    public static SalesReceiptListPage Page(long id, string? next = null, string? previous = null) =>
        new SalesReceiptListPage(id == 0 ? Array.Empty<SalesReceiptListRow>() : new[] {
            new SalesReceiptListRow(id, "SA" + id, DateTime.Today, 1, "Customer", 2, "Warehouse", 3, "Employee", "Draft", 1, 8m) }, next, previous);
    public static ISalesReceiptPreviewCalculator Calculator()
    {
        using var provider = new ServiceCollection().AddLocalDesktopAdapter().BuildServiceProvider();
        return provider.GetRequiredService<ISalesReceiptPreviewCalculator>();
    }
    private sealed class BoundSalesApi : ISalesReceiptApiClient
    {
        private readonly SalesApi _owner;
        private readonly long _expected;
        public BoundSalesApi(SalesApi owner, long expected) { _owner = owner; _expected = expected; }
        public ISalesReceiptApiClient ForSession(long expectedSessionVersion) => expectedSessionVersion == _expected
            ? this : throw new InvalidOperationException("Cannot rebind a Sales client.");
        public Task<ApiResponse<SalesReceiptListPage>> ListAsync(SalesReceiptListRequest request, CancellationToken token) => _owner.AdmitList(request, token, _expected);
        public Task<ApiResponse<SalesReceiptDto>> GetAsync(long id, CancellationToken token) => _owner.AdmitGet(id, token, _expected);
        public Task<ApiResponse<SalesReceiptDto>> CreateDraftAsync(SaveSalesReceiptRequest request, CancellationToken token) =>
            _owner.Owned(_expected) ? _owner.CreateDraftAsync(request, token) : Task.FromResult(Error<SalesReceiptDto>(ApiStatusCode.Unauthorized, "Auth.SessionChanged"));
        public Task<ApiResponse<SalesReceiptDto>> UpdateDraftAsync(UpdateSalesReceiptDraftRequest request, CancellationToken token) =>
            _owner.Owned(_expected) ? _owner.UpdateDraftAsync(request, token) : Task.FromResult(Error<SalesReceiptDto>(ApiStatusCode.Unauthorized, "Auth.SessionChanged"));
        public Task<ApiResponse<PostSalesReceiptResponse>> PostAsync(PostSalesReceiptRequest request, CancellationToken token) =>
            _owner.Owned(_expected) ? _owner.PostAsync(request, token) : Task.FromResult(Error<PostSalesReceiptResponse>(ApiStatusCode.Unauthorized, "Auth.SessionChanged"));
    }
}

internal sealed class SalesBuffers : ISalesCommandBufferAdmission
{
    public Func<SalesCommandIntent, Task<bool>> Commit = _ => Task.FromResult(true);
    public Task<bool> TryCommitPendingEditsAsync(SalesCommandIntent intent, CancellationToken token) => LocalSalesReceiptApiClientTests.Within(Commit(intent));
}

internal sealed class SalesQueuedUi : IUiDispatcher
{
    public bool Queue;
    public readonly Queue<Action> Actions = new Queue<Action>();
    public Task InvokeAsync(Action action, CancellationToken token)
    {
        if (!Queue) { action(); return Task.CompletedTask; }
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Actions.Enqueue(() => { try { action(); tcs.SetResult(true); } catch (Exception e) { tcs.SetException(e); } });
        return tcs.Task;
    }
    public void Drain() { while (Actions.Count > 0) Actions.Dequeue()(); }
}
