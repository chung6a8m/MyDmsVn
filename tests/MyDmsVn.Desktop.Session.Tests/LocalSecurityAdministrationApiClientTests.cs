using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class LocalSecurityAdministrationApiClientTests
{
    [Fact]
    public void Administration_and_catalog_ports_offer_session_binding()
    {
        Assert.Equal(typeof(ISecurityAdministrationApiClient),
            typeof(ISecurityAdministrationApiClient).GetMethod("ForSession", new[] { typeof(long) })?.ReturnType);
        Assert.Equal(typeof(IPermissionCatalogApiClient),
            typeof(IPermissionCatalogApiClient).GetMethod("ForSession", new[] { typeof(long) })?.ReturnType);
    }

    [Fact]
    public async Task Bound_clients_reject_all_operations_before_new_session_admission()
    {
        var state = new State();
        using var provider = Provider(state);
        await Login(provider, "first");
        var session = provider.GetRequiredService<IDesktopSession>();
        var owner = session.Version;
        var administration = provider.GetRequiredService<ISecurityAdministrationApiClient>().ForSession(owner);
        var catalog = provider.GetRequiredService<IPermissionCatalogApiClient>().ForSession(owner);
        Assert.Same(administration, administration.ForSession(owner));
        Assert.Same(catalog, catalog.ForSession(owner));

        session.SignOut();
        await Login(provider, "second");
        Assert.Throws<InvalidOperationException>(() => administration.ForSession(session.Version));
        Assert.Throws<InvalidOperationException>(() => catalog.ForSession(session.Version));
        var validations = state.AccountValidations;

        await SessionChanged(administration.ListUsersAsync(default));
        await SessionChanged(administration.ListRolesAsync(default));
        await SessionChanged(administration.MutateAsync(
            new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.Role,
                SecurityAdministrationOperationDto.SetPermission, targetId: 201,
                permissionKey: "Inventory.Read", decision: true, expectedRevision: 1), default));
        await SessionChanged(administration.PreviewPermissionImportAsync(
            PermissionImportTargetTypeDto.Role, 201, "{}", default));
        await SessionChanged(administration.ApplyPermissionImportAsync(
            PermissionImportTargetTypeDto.Role, 201,
            new PermissionAssignmentImportRequest("{}", 1, "digest"), default));
        await SessionChanged(catalog.GetCatalogAsync(default));

        Assert.Equal(validations, state.AccountValidations);
        Assert.Empty(state.Keys);
        Assert.Empty(state.Actors);
        Assert.Equal(22, session.CurrentUser!.UserId);
    }

    [Fact]
    public async Task Replacement_during_account_validation_stops_security_admission()
    {
        var state = new State();
        using var provider = Provider(state);
        await Login(provider, "first");
        state.PauseValidation = true;
        var session = provider.GetRequiredService<IDesktopSession>();
        var administration = provider.GetRequiredService<ISecurityAdministrationApiClient>().ForSession(session.Version);
        var pending = administration.ListUsersAsync(default);
        await Within(state.ValidationStarted.Task);
        session.SignOut();
        state.PauseValidation = false;
        await Login(provider, "second");
        state.ValidationRelease.TrySetResult(true);

        await SessionChanged(await Within(pending));
        Assert.Empty(state.Keys);
        Assert.Empty(state.Actors);
        Assert.Equal(22, session.CurrentUser!.UserId);
    }

    [Fact]
    public async Task Admitted_old_security_and_catalog_reads_fence_late_output()
    {
        var state = new State { PauseHandler = true };
        using var provider = Provider(state);
        await Login(provider, "first");
        var session = provider.GetRequiredService<IDesktopSession>();
        var administration = provider.GetRequiredService<ISecurityAdministrationApiClient>().ForSession(session.Version);
        var catalog = provider.GetRequiredService<IPermissionCatalogApiClient>().ForSession(session.Version);
        var oldRead = administration.ListUsersAsync(default);
        await Within(state.HandlerStarted.Task);
        session.SignOut();
        await Login(provider, "second");
        state.HandlerRelease.TrySetResult(true);
        await SessionChanged(await Within(oldRead));
        Assert.Equal(new[] { 11, 11 }, state.Actors);
        Assert.Equal(22, session.CurrentUser!.UserId);
        await SessionChanged(catalog.GetCatalogAsync(default));
        Assert.Single(state.Keys);
    }

    [Fact]
    public async Task Serial_bound_operations_stop_at_replacement_boundary()
    {
        var state = new State();
        using var provider = Provider(state);
        await Login(provider, "first");
        var session = provider.GetRequiredService<IDesktopSession>();
        var administration = provider.GetRequiredService<ISecurityAdministrationApiClient>().ForSession(session.Version);
        Assert.True((await administration.ListUsersAsync(default)).IsSuccess);
        Assert.Equal(new[] { 11, 11 }, state.Actors);
        session.SignOut();
        await Login(provider, "second");
        var admissions = state.AccountValidations;

        await SessionChanged(administration.ListRolesAsync(default));
        Assert.Equal(admissions, state.AccountValidations);
        Assert.Single(state.Keys);
        Assert.Equal(new[] { 11, 11 }, state.Actors);
        Assert.Equal(22, session.CurrentUser!.UserId);
    }

    [Fact]
    public async Task Catalog_result_admitted_by_old_actor_is_fenced_after_replacement()
    {
        var state = new State { PauseCatalog = true };
        using var provider = Provider(state);
        await Login(provider, "first");
        var session = provider.GetRequiredService<IDesktopSession>();
        var catalog = provider.GetRequiredService<IPermissionCatalogApiClient>().ForSession(session.Version);
        var pending = catalog.GetCatalogAsync(default);
        await Within(state.CatalogStarted.Task);
        session.SignOut();
        await Login(provider, "second");
        state.CatalogRelease.TrySetResult(true);

        await SessionChanged(await Within(pending));
        Assert.Equal(new[] { 11, 11 }, state.Actors);
        Assert.Equal(22, session.CurrentUser!.UserId);
    }

    [Fact]
    public async Task Late_old_unauthorized_completion_does_not_expire_replacement_session()
    {
        var state = new State
        {
            PauseHandler = true,
            HandlerError = Error.Unauthorized("Auth.Unauthorized", "Old actor lost access.")
        };
        using var provider = Provider(state);
        await Login(provider, "first");
        var session = provider.GetRequiredService<IDesktopSession>();
        var administration = provider.GetRequiredService<ISecurityAdministrationApiClient>().ForSession(session.Version);
        var pending = administration.ListUsersAsync(default);
        await Within(state.HandlerStarted.Task);
        session.SignOut();
        await Login(provider, "second");
        state.HandlerRelease.TrySetResult(true);

        await SessionChanged(await Within(pending));
        Assert.Equal(22, session.CurrentUser!.UserId);
        Assert.True(session.IsAuthenticated);
    }

    [Fact]
    public async Task Bound_operations_preserve_request_fields_and_permission_keys()
    {
        var state = new State();
        using var provider = Provider(state);
        await Login(provider, "first");
        var session = provider.GetRequiredService<IDesktopSession>();
        var administration = provider.GetRequiredService<ISecurityAdministrationApiClient>().ForSession(session.Version);
        var catalog = provider.GetRequiredService<IPermissionCatalogApiClient>().ForSession(session.Version);
        var mutation = new SecurityAdministrationMutationRequest(
            SecurityAdministrationTargetDto.Role, SecurityAdministrationOperationDto.SetPermission,
            targetId: 201, permissionKey: "Catalog.Products.Read", decision: true, expectedRevision: 7);
        var import = new PermissionAssignmentImportRequest("{}", 8, "digest-8");

        Assert.True((await administration.ListUsersAsync(default)).IsSuccess);
        Assert.True((await administration.ListRolesAsync(default)).IsSuccess);
        Assert.True((await administration.MutateAsync(mutation, default)).IsSuccess);
        Assert.True((await administration.PreviewPermissionImportAsync(PermissionImportTargetTypeDto.Role, 201, "{}", default)).IsSuccess);
        Assert.True((await administration.ApplyPermissionImportAsync(PermissionImportTargetTypeDto.Role, 201, import, default)).IsSuccess);
        Assert.True((await catalog.GetCatalogAsync(default)).IsSuccess);

        Assert.Equal(new[] { "Security.Users.Manage", "Security.Roles.Manage", "Security.Roles.Manage",
            "Security.Roles.Manage", "Security.Roles.Manage", "Security.Permissions.Read" }, state.Keys);
        var sent = Assert.IsType<MutateSecurityAdministrationCommand>(state.Requests[2]);
        Assert.Equal(201, sent.TargetId);
        Assert.Equal("Catalog.Products.Read", sent.PermissionKeyValue);
        Assert.True(sent.Decision);
        Assert.Equal(7, sent.ExpectedRevision);
        var preview = Assert.IsType<PreviewPermissionAssignmentImportQuery>(state.Requests[3]);
        Assert.Equal(PermissionImportTargetTypeDto.Role, preview.TargetType);
        Assert.Equal(201, preview.TargetId);
        Assert.Equal("{}", preview.Json);
        var apply = Assert.IsType<ApplyPermissionAssignmentImportCommand>(state.Requests[4]);
        Assert.Equal(8, apply.ExpectedTargetRevision);
        Assert.Equal("digest-8", apply.ExpectedCatalogDigest);
        Assert.Equal("{}", apply.Json);
    }

    private static async Task SessionChanged<T>(Task<ApiResponse<T>> request) =>
        await SessionChanged(await Within(request));

    private static Task SessionChanged<T>(ApiResponse<T> response)
    {
        Assert.False(response.IsSuccess);
        Assert.Null(response.Data);
        Assert.Equal(ApiStatusCode.Unauthorized, response.Error!.Status);
        Assert.Equal("Auth.SessionChanged", response.Error.Code);
        Assert.False(string.IsNullOrWhiteSpace(response.Error.CorrelationId));
        return Task.CompletedTask;
    }

    private static async Task<T> Within<T>(Task<T> task)
    {
        Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5))));
        return await task;
    }

    private static async Task Login(ServiceProvider provider, string name) =>
        Assert.True((await provider.GetRequiredService<IIdentityApiClient>()
            .LoginAsync(new LoginRequest(name, "test"), default)).IsSuccess);

    private static ServiceProvider Provider(State state)
    {
        var services = new ServiceCollection();
        services.AddServerApplication();
        services.AddLocalDesktopAdapter();
        services.AddSingleton(state);
        services.AddSingleton<IUserAccountValidator, ActiveAccount>();
        services.AddSingleton<IPermissionStore, Grants>();
        services.AddTransient<IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>, LoginHandler>();
        services.AddTransient<IRequestHandler<ListSecurityUsersQuery, ErrorOr<IReadOnlyList<SecurityUserDto>>>, UserHandler>();
        services.AddTransient<IRequestHandler<ListSecurityRolesQuery, ErrorOr<IReadOnlyList<SecurityRoleDto>>>, RoleHandler>();
        services.AddTransient<IRequestHandler<MutateSecurityAdministrationCommand, ErrorOr<SecurityAdministrationResultDto>>, MutationHandler>();
        services.AddTransient<IRequestHandler<PreviewPermissionAssignmentImportQuery, ErrorOr<PermissionAssignmentImportPreviewDto>>, PreviewHandler>();
        services.AddTransient<IRequestHandler<ApplyPermissionAssignmentImportCommand, ErrorOr<PermissionAssignmentImportResultDto>>, ApplyHandler>();
        services.AddTransient<IRequestHandler<ListPermissionCatalogQuery, ErrorOr<PermissionCatalogDto>>, CatalogHandler>();
        return services.BuildServiceProvider();
    }

    private sealed class State
    {
        public bool PauseValidation;
        public bool PauseHandler;
        public bool PauseCatalog;
        public Error? HandlerError;
        public int AccountValidations;
        public readonly List<string> Keys = new();
        public readonly List<int> Actors = new();
        public readonly List<object> Requests = new();
        public readonly TaskCompletionSource<bool> ValidationStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> ValidationRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> HandlerStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> HandlerRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> CatalogStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> CatalogRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class LoginHandler : IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>
    {
        public Task<ErrorOr<CurrentUserDto>> Handle(LoginCommand request, CancellationToken token) =>
            Task.FromResult<ErrorOr<CurrentUserDto>>(new CurrentUserDto(
                request.Username == "first" ? 11 : 22, request.Username, request.Username));
    }

    private sealed class ActiveAccount : IUserAccountValidator
    {
        private readonly State _state;
        public ActiveAccount(State state) => _state = state;
        public async Task<bool> IsActiveAsync(int userId, CancellationToken token)
        {
            _state.AccountValidations++;
            if (_state.PauseValidation)
            {
                _state.ValidationStarted.TrySetResult(true);
                await Within(_state.ValidationRelease.Task);
            }
            return true;
        }
    }

    private sealed class Grants : IPermissionStore
    {
        private readonly State _state;
        public Grants(State state) => _state = state;
        public Task<PermissionSnapshot> GetSnapshotAsync(int userId, string permissionKey, CancellationToken token)
        {
            _state.Keys.Add(permissionKey);
            return Task.FromResult(new PermissionSnapshot(null, true));
        }
    }

    private sealed class UserHandler : IRequestHandler<ListSecurityUsersQuery, ErrorOr<IReadOnlyList<SecurityUserDto>>>
    {
        private readonly State _state;
        private readonly ICurrentUserAccessor _actor;
        public UserHandler(State state, ICurrentUserAccessor actor) { _state = state; _actor = actor; }
        public async Task<ErrorOr<IReadOnlyList<SecurityUserDto>>> Handle(ListSecurityUsersQuery request, CancellationToken token)
        {
            _state.Requests.Add(request);
            _state.Actors.Add(_actor.Current.UserId!.Value);
            _state.HandlerStarted.TrySetResult(true);
            if (_state.PauseHandler) await Within(_state.HandlerRelease.Task);
            _state.Actors.Add(_actor.Current.UserId!.Value);
            if (_state.HandlerError.HasValue) return _state.HandlerError.Value;
            return Array.Empty<SecurityUserDto>();
        }
    }

    private sealed class CatalogHandler : IRequestHandler<ListPermissionCatalogQuery, ErrorOr<PermissionCatalogDto>>
    {
        private readonly State _state;
        private readonly ICurrentUserAccessor _actor;
        public CatalogHandler(State state, ICurrentUserAccessor actor) { _state = state; _actor = actor; }
        public async Task<ErrorOr<PermissionCatalogDto>> Handle(ListPermissionCatalogQuery request, CancellationToken token)
        {
            _state.Requests.Add(request);
            _state.Actors.Add(_actor.Current.UserId!.Value);
            _state.CatalogStarted.TrySetResult(true);
            if (_state.PauseCatalog) await Within(_state.CatalogRelease.Task);
            _state.Actors.Add(_actor.Current.UserId!.Value);
            return new PermissionCatalogDto("1.0", "digest", Array.Empty<PermissionCatalogItemDto>());
        }
    }

    private sealed class RoleHandler : IRequestHandler<ListSecurityRolesQuery, ErrorOr<IReadOnlyList<SecurityRoleDto>>>
    {
        private readonly State _state;
        public RoleHandler(State state) => _state = state;
        public Task<ErrorOr<IReadOnlyList<SecurityRoleDto>>> Handle(ListSecurityRolesQuery request, CancellationToken token)
        {
            _state.Requests.Add(request);
            return Task.FromResult<ErrorOr<IReadOnlyList<SecurityRoleDto>>>(Array.Empty<SecurityRoleDto>());
        }
    }

    private sealed class MutationHandler : IRequestHandler<MutateSecurityAdministrationCommand, ErrorOr<SecurityAdministrationResultDto>>
    {
        private readonly State _state;
        public MutationHandler(State state) => _state = state;
        public Task<ErrorOr<SecurityAdministrationResultDto>> Handle(MutateSecurityAdministrationCommand request, CancellationToken token)
        {
            _state.Requests.Add(request);
            return Task.FromResult<ErrorOr<SecurityAdministrationResultDto>>(new SecurityAdministrationResultDto(8, true));
        }
    }

    private sealed class PreviewHandler : IRequestHandler<PreviewPermissionAssignmentImportQuery, ErrorOr<PermissionAssignmentImportPreviewDto>>
    {
        private readonly State _state;
        public PreviewHandler(State state) => _state = state;
        public Task<ErrorOr<PermissionAssignmentImportPreviewDto>> Handle(PreviewPermissionAssignmentImportQuery request, CancellationToken token)
        {
            _state.Requests.Add(request);
            var parsed = new ParsedPermissionAssignmentImportDto(1, request.TargetType, request.TargetId,
                Array.Empty<PermissionAssignmentImportEntryDto>());
            return Task.FromResult<ErrorOr<PermissionAssignmentImportPreviewDto>>(
                new PermissionAssignmentImportPreviewDto(parsed, "Role", 8, "digest-8", Array.Empty<PermissionAssignmentImportEntryDto>()));
        }
    }

    private sealed class ApplyHandler : IRequestHandler<ApplyPermissionAssignmentImportCommand, ErrorOr<PermissionAssignmentImportResultDto>>
    {
        private readonly State _state;
        public ApplyHandler(State state) => _state = state;
        public Task<ErrorOr<PermissionAssignmentImportResultDto>> Handle(ApplyPermissionAssignmentImportCommand request, CancellationToken token)
        {
            _state.Requests.Add(request);
            return Task.FromResult<ErrorOr<PermissionAssignmentImportResultDto>>(new PermissionAssignmentImportResultDto(8, 0, false));
        }
    }
}
