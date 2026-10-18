using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using ErrorOr;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
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

public interface IPermissionAssignmentImportRepository
{
    Task<PermissionAssignmentImportTargetState> ReadTargetAsync(
        PermissionImportTargetTypeDto targetType,
        int targetId,
        int actorUserId,
        CancellationToken cancellationToken);

    Task<PermissionAssignmentImportResultDto> ApplyAsync(
        ParsedPermissionAssignmentImportDto import,
        int actorUserId,
        long expectedTargetRevision,
        Guid correlationId,
        CancellationToken cancellationToken);
}

public sealed class SqlSecurityAdministrationStore : ISecurityAdministrationStore, IPermissionAssignmentImportStore
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

    public async Task<ErrorOr<PermissionAssignmentImportTargetState>> ReadTargetAsync(
        PermissionImportTargetTypeDto targetType,
        int targetId,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        using var unitOfWork = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await unitOfWork.Repository<IPermissionAssignmentImportRepository>()
                .ReadTargetAsync(targetType, targetId, actorUserId, cancellationToken)
                .ConfigureAwait(false);
            return result;
        }
        catch (SqlException exception) when (TryMapImport(exception, out var error))
        {
            return error;
        }
    }

    public async Task<ErrorOr<PermissionAssignmentImportResultDto>> ApplyAsync(
        ParsedPermissionAssignmentImportDto import,
        int actorUserId,
        long expectedTargetRevision,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        using var unitOfWork = await _unitOfWorkFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        unitOfWork.BeginTransaction();
        try
        {
            var result = await unitOfWork.Repository<IPermissionAssignmentImportRepository>()
                .ApplyAsync(import, actorUserId, expectedTargetRevision, correlationId, cancellationToken)
                .ConfigureAwait(false);
            unitOfWork.Commit();
            return result;
        }
        catch (SqlException exception) when (TryMapImport(exception, out var error))
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

    private static bool TryMapImport(SqlException exception, out Error error)
    {
        switch (exception.Number)
        {
            case 51030:
                error = Error.Conflict("Security.RevisionConflict", "The security assignment changed. Preview the import again.");
                return true;
            case 51031:
                error = Error.NotFound("Security.TargetNotFound", "The requested security target no longer exists.");
                return true;
            case 51035:
                error = Error.Forbidden("Auth.Forbidden", "The security actor is no longer authorized for this operation.");
                return true;
            case 51036:
                error = Error.Conflict("Security.ImportDecisionConflict", "An existing decision conflicts with the import.");
                return true;
            case 51037:
                error = Error.Conflict("Security.ImportTargetUnavailable", "The selected target is inactive or protected.");
                return true;
            case 51038:
                error = Error.Validation("Security.ImportInvalid", "The permission import request is invalid.");
                return true;
            default:
                error = default;
                return false;
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

public sealed class SqlSecurityAdministrationRepository : ISecurityAdministrationRepository, IPermissionAssignmentImportRepository
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

    public async Task<PermissionAssignmentImportTargetState> ReadTargetAsync(
        PermissionImportTargetTypeDto targetType,
        int targetId,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        using var results = await _context.Connection.QueryMultipleAsync(new Dapper.CommandDefinition(
            "dbo.ReadSecurityPermissionImportTarget",
            new
            {
                TargetType = targetType == PermissionImportTargetTypeDto.User ? "User" : "Role",
                TargetId = targetId,
                ActorUserId = actorUserId,
            },
            _context.Transaction,
            commandType: System.Data.CommandType.StoredProcedure,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        // Consume the empty grid so a deferred procedure THROW reaches the store's SQL error mapper.
        // ReadSingleAsync throws locally first and masks the missing-target SQL 51031 error.
        var target = await results.ReadSingleOrDefaultAsync<ImportTargetRow>().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Import target procedure returned no target and no SQL error.");
        var decisions = (await results.ReadAsync<ImportDecisionRow>().ConfigureAwait(false))
            .ToDictionary(row => row.PermissionKey, row => row.Granted, StringComparer.Ordinal);
        return new PermissionAssignmentImportTargetState(
            target.TargetName,
            target.IsActive,
            target.IsProtected,
            target.AssignmentRevision,
            decisions);
    }

    public Task<PermissionAssignmentImportResultDto> ApplyAsync(
        ParsedPermissionAssignmentImportDto import,
        int actorUserId,
        long expectedTargetRevision,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var entriesJson = JsonConvert.SerializeObject(import.Entries.Select(entry => new
        {
            PermissionKey = entry.PermissionKey,
            entry.Granted,
        }));
        return _context.Connection.QuerySingleAsync<PermissionAssignmentImportResultDto>(
            new Dapper.CommandDefinition(
                "dbo.ApplySecurityPermissionImport",
                new
                {
                    TargetType = import.TargetType == PermissionImportTargetTypeDto.User ? "User" : "Role",
                    TargetId = import.TargetId,
                    ActorUserId = actorUserId,
                    ExpectedRevision = expectedTargetRevision,
                    EntriesJson = entriesJson,
                    CorrelationId = correlationId,
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

    private sealed class ImportTargetRow
    {
        public string TargetName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsProtected { get; set; }
        public long AssignmentRevision { get; set; }
    }

    private sealed class ImportDecisionRow
    {
        public string PermissionKey { get; set; } = string.Empty;
        public bool Granted { get; set; }
    }
}
