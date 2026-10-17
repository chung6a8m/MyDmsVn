using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using ErrorOr;
using Microsoft.Data.SqlClient;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Security;

public interface ISecurityAdministrationRepository
{
    Task<IReadOnlyList<SecurityUserDto>> ListUsersAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SecurityRoleDto>> ListRolesAsync(CancellationToken cancellationToken);

    Task<SecurityAdministrationResultDto> ApplyAsync(
        SecurityAdministrationMutation mutation,
        CancellationToken cancellationToken);
}

public sealed class SqlSecurityAdministrationStore : ISecurityAdministrationStore
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;

    public SqlSecurityAdministrationStore(IUnitOfWorkFactory unitOfWorkFactory) =>
        _unitOfWorkFactory = unitOfWorkFactory ?? throw new ArgumentNullException(nameof(unitOfWorkFactory));

    public async Task<IReadOnlyList<SecurityUserDto>> ListUsersAsync(CancellationToken cancellationToken)
    {
        using var unitOfWork = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        return await unitOfWork.Repository<ISecurityAdministrationRepository>()
            .ListUsersAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SecurityRoleDto>> ListRolesAsync(CancellationToken cancellationToken)
    {
        using var unitOfWork = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        return await unitOfWork.Repository<ISecurityAdministrationRepository>()
            .ListRolesAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ErrorOr<SecurityAdministrationResultDto>> ApplyAsync(
        SecurityAdministrationMutation mutation,
        CancellationToken cancellationToken)
    {
        if (mutation == null)
        {
            throw new ArgumentNullException(nameof(mutation));
        }

        using var unitOfWork = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        unitOfWork.BeginTransaction();
        try
        {
            var result = await unitOfWork.Repository<ISecurityAdministrationRepository>()
                .ApplyAsync(mutation, cancellationToken)
                .ConfigureAwait(false);
            unitOfWork.Commit();
            return result;
        }
        catch (SqlException exception) when (TryMap(exception, out var error))
        {
            unitOfWork.Rollback();
            return error;
        }
        catch
        {
            if (unitOfWork.State == UnitOfWorkState.ActiveTransaction)
            {
                unitOfWork.Rollback();
            }

            throw;
        }
    }

    private static bool TryMap(SqlException exception, out Error error)
    {
        switch (exception.Number)
        {
            case 2601:
            case 2627:
            case 51034:
                error = Error.Conflict(
                    "Security.NameConflict",
                    "The username or role name is already in use.");
                return true;
            case 51030:
                error = Error.Conflict(
                    "Security.RevisionConflict",
                    "The security assignment changed. Reload the target before applying another change.");
                return true;
            case 51031:
                error = Error.NotFound(
                    "Security.TargetNotFound",
                    "The requested security target no longer exists.");
                return true;
            case 51032:
                error = Error.Validation(
                    "Security.PermissionKeyInvalid",
                    "The permission key is not valid for this assignment.");
                return true;
            case 51033:
                error = Error.Validation(
                    "Security.ReservedSuperuser",
                    "The configured superuser account can only be changed through maintenance operations.");
                return true;
            case 51035:
                error = Error.Forbidden(
                    "Auth.Forbidden",
                    "The security actor is no longer authorized for this operation.");
                return true;
            default:
                error = default;
                return false;
        }
    }
}

public sealed class SqlSecurityAdministrationRepository : ISecurityAdministrationRepository
{
    private readonly ISqlExecutionContext _context;

    public SqlSecurityAdministrationRepository(ISqlExecutionContext context) =>
        _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<IReadOnlyList<SecurityUserDto>> ListUsersAsync(CancellationToken cancellationToken)
    {
        var users = (await _context.Connection.QueryAsync<UserRow>(
            new Dapper.CommandDefinition(
                "SELECT users.UserId, users.Username, users.DisplayName, users.Email, users.IsActive, " +
                "users.AssignmentRevision, CONVERT(bit, CASE WHEN settings.SuperuserUserId = users.UserId THEN 1 ELSE 0 END) AS IsProtectedAccount " +
                "FROM dbo.Users AS users LEFT JOIN dbo.SystemSecuritySettings AS settings ON settings.Id = 1 " +
                "ORDER BY users.NormalizedUsername COLLATE Latin1_General_100_BIN2;",
                transaction: _context.Transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToArray();
        var memberships = (await _context.Connection.QueryAsync<MembershipRow>(
            new Dapper.CommandDefinition(
                "SELECT UserId, RoleId FROM dbo.UserRoles ORDER BY UserId, RoleId;",
                transaction: _context.Transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToLookup(row => row.UserId);
        var decisions = (await _context.Connection.QueryAsync<DecisionRow>(
            new Dapper.CommandDefinition(
                "SELECT UserId, PermissionKey, Granted FROM dbo.UserPermissions " +
                "ORDER BY UserId, PermissionKey COLLATE Latin1_General_100_BIN2;",
                transaction: _context.Transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToLookup(row => row.UserId);
        return users.Select(user => new SecurityUserDto(
                user.UserId,
                user.Username,
                user.DisplayName,
                user.Email,
                user.IsActive,
                user.AssignmentRevision,
                Array.AsReadOnly(memberships[user.UserId].Select(row => row.RoleId).ToArray()),
                Array.AsReadOnly(decisions[user.UserId]
                    .Select(row => new SecurityDecisionDto(row.PermissionKey, row.Granted))
                    .ToArray()),
                user.IsProtectedAccount))
            .ToArray();
    }

    public async Task<IReadOnlyList<SecurityRoleDto>> ListRolesAsync(CancellationToken cancellationToken)
    {
        var roles = (await _context.Connection.QueryAsync<RoleRow>(
            new Dapper.CommandDefinition(
                "SELECT RoleId, RoleName, IsActive, AssignmentRevision " +
                "FROM dbo.Roles ORDER BY NormalizedRoleName COLLATE Latin1_General_100_BIN2;",
                transaction: _context.Transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToArray();
        var memberships = (await _context.Connection.QueryAsync<MembershipRow>(
            new Dapper.CommandDefinition(
                "SELECT UserId, RoleId FROM dbo.UserRoles ORDER BY RoleId, UserId;",
                transaction: _context.Transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToLookup(row => row.RoleId);
        var grants = (await _context.Connection.QueryAsync<RoleGrantRow>(
            new Dapper.CommandDefinition(
                "SELECT RoleId, PermissionKey FROM dbo.RolePermissions " +
                "ORDER BY RoleId, PermissionKey COLLATE Latin1_General_100_BIN2;",
                transaction: _context.Transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToLookup(row => row.RoleId);
        return roles.Select(role => new SecurityRoleDto(
                role.RoleId,
                role.RoleName,
                role.IsActive,
                role.AssignmentRevision,
                Array.AsReadOnly(memberships[role.RoleId].Select(row => row.UserId).ToArray()),
                Array.AsReadOnly(grants[role.RoleId].Select(row => row.PermissionKey).ToArray())))
            .ToArray();
    }

    public Task<SecurityAdministrationResultDto> ApplyAsync(
        SecurityAdministrationMutation mutation,
        CancellationToken cancellationToken)
    {
        return _context.Connection.QuerySingleAsync<SecurityAdministrationResultDto>(
            new Dapper.CommandDefinition(
                "dbo.ApplySecurityAdministration",
                new
                {
                    Target = mutation.Target == SecurityAdministrationTarget.User ? "User" : "Role",
                    Operation = mutation.Operation.ToString(),
                    mutation.ActorUserId,
                    mutation.TargetId,
                    mutation.RelatedId,
                    mutation.Name,
                    mutation.NormalizedName,
                    mutation.DisplayName,
                    mutation.Email,
                    mutation.IsActive,
                    mutation.PasswordHash,
                    mutation.PasswordAlgorithm,
                    mutation.PermissionKey,
                    mutation.Decision,
                    mutation.ExpectedRevision,
                    mutation.ExpectedRelatedRevision,
                    mutation.CorrelationId,
                },
                _context.Transaction,
                commandType: System.Data.CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
    }

    private sealed class UserRow
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public long AssignmentRevision { get; set; }
        public bool IsProtectedAccount { get; set; }
    }

    private sealed class RoleRow
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public long AssignmentRevision { get; set; }
    }

    private sealed class MembershipRow
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }
    }

    private sealed class DecisionRow
    {
        public int UserId { get; set; }
        public string PermissionKey { get; set; } = string.Empty;
        public bool Granted { get; set; }
    }

    private sealed class RoleGrantRow
    {
        public int RoleId { get; set; }
        public string PermissionKey { get; set; } = string.Empty;
    }
}
