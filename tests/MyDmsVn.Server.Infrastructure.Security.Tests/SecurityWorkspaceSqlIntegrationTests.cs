using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Application.Security;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Infrastructure;
using MyDmsVn.Testing.Sql;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

[Collection(ApplicationSqlCollectionFixture.CollectionName)]
public sealed class SecurityWorkspaceSqlIntegrationTests
{
    private const string FirstKey = PermissionKeys.CatalogProductsRead;
    private const string SecondKey = PermissionKeys.CatalogProductsWrite;
    private const string ThirdKey = PermissionKeys.InventoryBalancesRead;
    private const string ExternalKey = PermissionKeys.CatalogCustomersRead;
    private readonly ApplicationSqlCollectionFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SecurityWorkspaceSqlIntegrationTests(ApplicationSqlCollectionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Serial_role_save_chains_real_returned_revisions_and_noops() =>
        _fixture.RunAsync(nameof(Serial_role_save_chains_real_returned_revisions_and_noops), _output, async test =>
        {
            var (actorId, roleId) = await SeedAsync(test, seedFirstGrant: true);
            var provider = ApplicationSqlCollectionFixture.Track(test, CreateLocalProvider(test.ConnectionString, actorId));
            var (session, api) = await LoginAndBindAsync(provider);
            var draft = Draft(roleId, session.Version, FirstKey, SecondKey);
            var outcome = await new SecurityAssignmentSaveCoordinator(api, Current(session))
                .SaveRolePermissionsAsync(draft, CancellationToken.None);

            Assert.Equal(SecuritySaveCompletion.Complete, outcome.Completion);
            Assert.Equal(SecuritySaveStopReason.None, outcome.StopReason);
            Assert.Equal(new[] { FirstKey, SecondKey }, outcome.ConfirmedKeys);
            Assert.Equal(1, outcome.LastConfirmedRevision);
            using var verification = new SqlConnection(test.ConnectionString);
            Assert.Equal(1, await RoleRevisionAsync(verification, roleId));
            Assert.Equal(new[] { FirstKey, SecondKey }, await GrantedKeysAsync(verification, roleId));
            var audits = await RoleAuditStatesAsync(verification, roleId);
            Assert.Single(audits);
            Assert.Equal(actorId, audits[0].ActorUserId);
            Assert.Contains("Permission=" + SecondKey + ";Decision=Allow", audits[0].AfterState);
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Conflict_after_one_commit_preserves_that_commit_and_stops_remaining_items() =>
        _fixture.RunAsync(nameof(Conflict_after_one_commit_preserves_that_commit_and_stops_remaining_items), _output, async test =>
        {
            var (actorId, roleId) = await SeedAsync(test);
            var provider = ApplicationSqlCollectionFixture.Track(test, CreateLocalProvider(test.ConnectionString, actorId));
            var (session, api) = await LoginAndBindAsync(provider);
            var interleaved = new InterleavingClient(api, roleId);
            var outcome = await new SecurityAssignmentSaveCoordinator(interleaved, Current(session))
                .SaveRolePermissionsAsync(Draft(roleId, session.Version, FirstKey, SecondKey, ThirdKey), CancellationToken.None);

            Assert.True(interleaved.ExternalMutationSucceeded);
            Assert.Equal(2, interleaved.SaveCalls);
            Assert.Equal(SecuritySaveCompletion.Partial, outcome.Completion);
            Assert.Equal(SecuritySaveStopReason.Conflict, outcome.StopReason);
            Assert.Equal(new[] { FirstKey }, outcome.ConfirmedKeys);
            Assert.Equal(new[] { SecondKey }, outcome.RejectedKeys);
            Assert.Equal(new[] { ThirdKey }, outcome.UnattemptedKeys);
            Assert.Equal(1, outcome.LastConfirmedRevision);
            using var verification = new SqlConnection(test.ConnectionString);
            Assert.Equal(2, await RoleRevisionAsync(verification, roleId));
            Assert.Equal(new[] { ExternalKey, FirstKey }, await GrantedKeysAsync(verification, roleId));
            var audits = await RoleAuditStatesAsync(verification, roleId);
            Assert.Equal(2, audits.Length);
            Assert.All(audits, audit => Assert.Equal(actorId, audit.ActorUserId));
            Assert.Contains("Permission=" + FirstKey + ";Decision=Allow", audits[0].AfterState);
            Assert.Contains("Permission=" + ExternalKey + ";Decision=Allow", audits[1].AfterState);
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Membership_change_invalidates_a_stale_role_draft() =>
        _fixture.RunAsync(nameof(Membership_change_invalidates_a_stale_role_draft), _output, async test =>
        {
            var (actorId, roleId) = await SeedAsync(test);
            var connection = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
            var memberId = await InsertUserAsync(connection, "member", "MEMBER");
            var provider = ApplicationSqlCollectionFixture.Track(test, CreateLocalProvider(test.ConnectionString, actorId));
            var (session, api) = await LoginAndBindAsync(provider);
            var staleDraft = Draft(roleId, session.Version, FirstKey);
            var membership = await api.MutateAsync(new SecurityAdministrationMutationRequest(
                SecurityAdministrationTargetDto.User, SecurityAdministrationOperationDto.SetMembership,
                targetId: memberId, relatedId: roleId, isActive: true,
                expectedRevision: 0, expectedRelatedRevision: 0), CancellationToken.None);
            Assert.True(membership.IsSuccess, membership.Error?.Code);
            Assert.Equal(1, membership.Data!.AssignmentRevision);
            Assert.Equal(1, membership.Data.RelatedAssignmentRevision);

            var outcome = await new SecurityAssignmentSaveCoordinator(api, Current(session))
                .SaveRolePermissionsAsync(staleDraft, CancellationToken.None);
            Assert.Equal(SecuritySaveCompletion.NoneConfirmed, outcome.Completion);
            Assert.Equal(SecuritySaveStopReason.Conflict, outcome.StopReason);
            Assert.Equal(new[] { FirstKey }, outcome.RejectedKeys);
            using var verification = new SqlConnection(test.ConnectionString);
            Assert.Equal(1, await RoleRevisionAsync(verification, roleId));
            Assert.Equal(0, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.RolePermissions WHERE RoleId = @roleId;", new { roleId }));
            Assert.Equal(1, await verification.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM dbo.UserRoles WHERE UserId = @memberId AND RoleId = @roleId;",
                new { memberId, roleId }));
            var membershipAudit = await verification.QuerySingleAsync<string>(
                "SELECT AfterState FROM dbo.SecurityAuditEvents WHERE Operation = N'SetMembership' AND TargetId = @memberId;",
                new { memberId });
            Assert.Contains("RoleId=" + roleId + ";Member=True", membershipAudit);
        });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Local_invalid_permission_returns_normalized_validation_envelope_without_a_write() =>
        _fixture.RunAsync(nameof(Local_invalid_permission_returns_normalized_validation_envelope_without_a_write), _output, async test =>
        {
            var (actorId, roleId) = await SeedAsync(test);
            var provider = ApplicationSqlCollectionFixture.Track(test, CreateLocalProvider(test.ConnectionString, actorId));
            var (_, api) = await LoginAndBindAsync(provider);
            var response = await api.MutateAsync(new SecurityAdministrationMutationRequest(
                SecurityAdministrationTargetDto.Role, SecurityAdministrationOperationDto.SetPermission,
                targetId: roleId, permissionKey: "Unknown.Permission", decision: true,
                expectedRevision: 0), CancellationToken.None);

            Assert.False(response.IsSuccess);
            Assert.NotNull(response.Error);
            Assert.Equal(ApiStatusCode.BadRequest, response.Error!.Status);
            Assert.Equal("ValidationError", response.Error.Code);
            Assert.Contains(response.Error.Details,
                detail => detail.Code == "PredicateValidator" && detail.Field == "permissionKeyValue");
            using var verification = new SqlConnection(test.ConnectionString);
            Assert.Equal(0, await RoleRevisionAsync(verification, roleId));
            Assert.Empty(await GrantedKeysAsync(verification, roleId));
            Assert.Empty(await RoleAuditStatesAsync(verification, roleId));
        });

    private static Func<SecurityRolePermissionSubmission, bool> Current(IDesktopSession session) =>
        submitted => session.IsAuthenticated && session.Version == submitted.SessionVersion;

    private static SecurityRolePermissionDraft Draft(int roleId, long sessionVersion, params string[] keys)
    {
        var draft = new SecurityRolePermissionDraft(roleId, 0, Array.Empty<string>(), keys, sessionVersion);
        foreach (var key in keys) draft.SetGranted(key, true);
        return draft;
    }

    private static async Task<(int ActorId, int RoleId)> SeedAsync(SqlFixtureTestScope test, bool seedFirstGrant = false)
    {
        var connection = ApplicationSqlCollectionFixture.Track(test, new SqlConnection(test.ConnectionString));
        var actorId = await InsertUserAsync(connection, "manager", "MANAGER");
        var managerRoleId = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Roles (RoleName, NormalizedRoleName) " +
            "OUTPUT INSERTED.RoleId VALUES (N'Security managers', N'SECURITY MANAGERS');");
        await connection.ExecuteAsync(
            "INSERT dbo.UserRoles (UserId, RoleId) VALUES (@actorId, @managerRoleId); " +
            "INSERT dbo.RolePermissions (RoleId, PermissionKey) VALUES " +
            "(@managerRoleId, N'Security.Users.Manage'), (@managerRoleId, N'Security.Roles.Manage');",
            new { actorId, managerRoleId });
        var roleId = await connection.QuerySingleAsync<int>(
            "INSERT dbo.Roles (RoleName, NormalizedRoleName) " +
            "OUTPUT INSERTED.RoleId VALUES (N'Workspace target', N'WORKSPACE TARGET');");
        if (seedFirstGrant)
            await connection.ExecuteAsync(
                "INSERT dbo.RolePermissions (RoleId, PermissionKey) VALUES (@roleId, @key);",
                new { roleId, key = FirstKey });
        return (actorId, roleId);
    }

    private static Task<int> InsertUserAsync(SqlConnection connection, string username, string normalizedUsername) =>
        connection.QuerySingleAsync<int>(
            "INSERT dbo.Users (Username, NormalizedUsername, DisplayName, Source, PasswordHash, PasswordSalt, PasswordAlgorithm) " +
            "OUTPUT INSERTED.UserId VALUES (@username, @normalizedUsername, N'Operator', N'Local', N'hash', N'', N'BCrypt');",
            new { username, normalizedUsername });

    private static ServiceProvider CreateLocalProvider(string connectionString, int actorId)
    {
        var services = new ServiceCollection();
        services.AddServerApplication("ROOT.ADMIN");
        services.AddSqlPersistence(connectionString);
        services.AddLocalDesktopAdapter();
        services.AddSingleton(new ActorId(actorId));
        services.AddTransient<IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>, LoginHandler>();
        return services.BuildServiceProvider();
    }

    private static async Task<(IDesktopSession Session, ISecurityAdministrationApiClient Api)> LoginAndBindAsync(ServiceProvider provider)
    {
        var login = await provider.GetRequiredService<IIdentityApiClient>()
            .LoginAsync(new LoginRequest("manager", "fixture-only"), CancellationToken.None);
        Assert.True(login.IsSuccess, login.Error?.Code);
        var session = provider.GetRequiredService<IDesktopSession>();
        return (session, provider.GetRequiredService<ISecurityAdministrationApiClient>().ForSession(session.Version));
    }

    private static Task<long> RoleRevisionAsync(SqlConnection connection, int roleId) =>
        connection.QuerySingleAsync<long>("SELECT AssignmentRevision FROM dbo.Roles WHERE RoleId = @roleId;", new { roleId });

    private static async Task<string[]> GrantedKeysAsync(SqlConnection connection, int roleId) =>
        (await connection.QueryAsync<string>(
            "SELECT PermissionKey FROM dbo.RolePermissions WHERE RoleId = @roleId ORDER BY PermissionKey;", new { roleId })).ToArray();

    private static async Task<(int ActorUserId, string AfterState)[]> RoleAuditStatesAsync(SqlConnection connection, int roleId) =>
        (await connection.QueryAsync<(int ActorUserId, string AfterState)>(
            "SELECT ActorUserId, AfterState FROM dbo.SecurityAuditEvents " +
            "WHERE TargetType = N'Role' AND TargetId = @roleId AND Operation = N'SetPermission' ORDER BY AuditEventId;",
            new { roleId })).ToArray();

    private sealed class ActorId
    {
        public ActorId(int value) => Value = value;
        public int Value { get; }
    }

    private sealed class LoginHandler : IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>
    {
        private readonly ActorId _actor;
        public LoginHandler(ActorId actor) => _actor = actor;
        public Task<ErrorOr<CurrentUserDto>> Handle(LoginCommand request, CancellationToken token) =>
            Task.FromResult<ErrorOr<CurrentUserDto>>(new CurrentUserDto(_actor.Value, "manager", "Manager"));
    }

    private sealed class InterleavingClient : ISecurityAdministrationApiClient
    {
        private readonly ISecurityAdministrationApiClient _inner;
        private readonly int _roleId;
        public InterleavingClient(ISecurityAdministrationApiClient inner, int roleId) { _inner = inner; _roleId = roleId; }
        public bool ExternalMutationSucceeded { get; private set; }
        public int SaveCalls { get; private set; }
        public ISecurityAdministrationApiClient ForSession(long expectedSessionVersion) =>
            ReferenceEquals(_inner.ForSession(expectedSessionVersion), _inner) ? this :
                throw new InvalidOperationException("The interleaved client changed session.");
        public Task<ApiResponse<IReadOnlyList<SecurityUserDto>>> ListUsersAsync(CancellationToken token) =>
            _inner.ListUsersAsync(token);
        public Task<ApiResponse<IReadOnlyList<SecurityRoleDto>>> ListRolesAsync(CancellationToken token) =>
            _inner.ListRolesAsync(token);
        public async Task<ApiResponse<SecurityAdministrationResultDto>> MutateAsync(
            SecurityAdministrationMutationRequest request, CancellationToken token)
        {
            SaveCalls++;
            var response = await _inner.MutateAsync(request, token);
            if (SaveCalls == 1 && response.IsSuccess)
            {
                var competing = await _inner.MutateAsync(new SecurityAdministrationMutationRequest(
                    SecurityAdministrationTargetDto.Role, SecurityAdministrationOperationDto.SetPermission,
                    targetId: _roleId, permissionKey: ExternalKey, decision: true,
                    expectedRevision: response.Data!.AssignmentRevision), token);
                ExternalMutationSucceeded = competing.IsSuccess;
                Assert.True(ExternalMutationSucceeded, competing.Error?.Code);
            }
            return response;
        }
        public Task<ApiResponse<PermissionAssignmentImportPreviewDto>> PreviewPermissionImportAsync(
            PermissionImportTargetTypeDto targetType, int targetId, string json, CancellationToken token) =>
            _inner.PreviewPermissionImportAsync(targetType, targetId, json, token);
        public Task<ApiResponse<PermissionAssignmentImportResultDto>> ApplyPermissionImportAsync(
            PermissionImportTargetTypeDto targetType, int targetId, PermissionAssignmentImportRequest request, CancellationToken token) =>
            _inner.ApplyPermissionImportAsync(targetType, targetId, request, token);
    }
}
