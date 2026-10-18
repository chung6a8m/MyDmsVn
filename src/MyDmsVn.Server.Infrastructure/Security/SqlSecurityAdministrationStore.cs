using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
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
        using var usersCommand = SqlRead.Command(_context.Connection, _context.Transaction,
                "SELECT users.UserId, users.Username, users.DisplayName, users.Email, users.IsActive, " +
                "users.AssignmentRevision, CONVERT(bit, CASE WHEN settings.SuperuserUserId = users.UserId THEN 1 ELSE 0 END) AS IsProtectedAccount " +
                "FROM dbo.Users AS users LEFT JOIN dbo.SystemSecuritySettings AS settings ON settings.Id = 1 " +
                "ORDER BY users.NormalizedUsername COLLATE Latin1_General_100_BIN2;");
        var users = (await SqlRead.QueryAsync(usersCommand, reader => new UserRow
        {
            UserId = reader.GetInt32(0),
            Username = reader.GetString(1),
            DisplayName = reader.GetString(2),
            Email = reader.IsDBNull(3) ? null : reader.GetString(3),
            IsActive = reader.GetBoolean(4),
            AssignmentRevision = reader.GetInt64(5),
            IsProtectedAccount = reader.GetBoolean(6)
        }, cancellationToken).ConfigureAwait(false));
        using var membershipsCommand = SqlRead.Command(_context.Connection, _context.Transaction,
                "SELECT UserId, RoleId FROM dbo.UserRoles ORDER BY UserId, RoleId;");
        var memberships = (await SqlRead.QueryAsync(membershipsCommand, reader => new MembershipRow
        {
            UserId = reader.GetInt32(0),
            RoleId = reader.GetInt32(1)
        }, cancellationToken).ConfigureAwait(false)).ToLookup(row => row.UserId);
        using var decisionsCommand = SqlRead.Command(_context.Connection, _context.Transaction,
                "SELECT UserId, PermissionKey, Granted FROM dbo.UserPermissions " +
                "ORDER BY UserId, PermissionKey COLLATE Latin1_General_100_BIN2;");
        var decisions = (await SqlRead.QueryAsync(decisionsCommand, reader => new DecisionRow
        {
            UserId = reader.GetInt32(0),
            PermissionKey = reader.GetString(1),
            Granted = reader.GetBoolean(2)
        }, cancellationToken).ConfigureAwait(false)).ToLookup(row => row.UserId);
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
        using var rolesCommand = SqlRead.Command(_context.Connection, _context.Transaction,
                "SELECT RoleId, RoleName, IsActive, AssignmentRevision " +
                "FROM dbo.Roles ORDER BY NormalizedRoleName COLLATE Latin1_General_100_BIN2;");
        var roles = (await SqlRead.QueryAsync(rolesCommand, reader => new RoleRow
        {
            RoleId = reader.GetInt32(0),
            RoleName = reader.GetString(1),
            IsActive = reader.GetBoolean(2),
            AssignmentRevision = reader.GetInt64(3)
        }, cancellationToken).ConfigureAwait(false));
        using var membershipsCommand = SqlRead.Command(_context.Connection, _context.Transaction,
                "SELECT UserId, RoleId FROM dbo.UserRoles ORDER BY RoleId, UserId;");
        var memberships = (await SqlRead.QueryAsync(membershipsCommand, reader => new MembershipRow
        {
            UserId = reader.GetInt32(0),
            RoleId = reader.GetInt32(1)
        }, cancellationToken).ConfigureAwait(false)).ToLookup(row => row.RoleId);
        using var grantsCommand = SqlRead.Command(_context.Connection, _context.Transaction,
                "SELECT RoleId, PermissionKey FROM dbo.RolePermissions " +
                "ORDER BY RoleId, PermissionKey COLLATE Latin1_General_100_BIN2;");
        var grants = (await SqlRead.QueryAsync(grantsCommand, reader => new RoleGrantRow
        {
            RoleId = reader.GetInt32(0),
            PermissionKey = reader.GetString(1)
        }, cancellationToken).ConfigureAwait(false)).ToLookup(row => row.RoleId);
        return roles.Select(role => new SecurityRoleDto(
                role.RoleId,
                role.RoleName,
                role.IsActive,
                role.AssignmentRevision,
                Array.AsReadOnly(memberships[role.RoleId].Select(row => row.UserId).ToArray()),
                Array.AsReadOnly(grants[role.RoleId].Select(row => row.PermissionKey).ToArray())))
            .ToArray();
    }

    public async Task<SecurityAdministrationResultDto> ApplyAsync(
        SecurityAdministrationMutation mutation,
        CancellationToken cancellationToken)
    {
        using var command = SqlRead.Command(_context.Connection, _context.Transaction, "dbo.ApplySecurityAdministration",
            SqlRead.Text("@Target", mutation.Target == SecurityAdministrationTarget.User ? "User" : "Role"),
            SqlRead.Text("@Operation", mutation.Operation.ToString()), SqlRead.P("@ActorUserId", SqlDbType.Int, mutation.ActorUserId),
            SqlRead.P("@TargetId", SqlDbType.Int, mutation.TargetId), SqlRead.P("@RelatedId", SqlDbType.Int, mutation.RelatedId),
            SqlRead.Text("@Name", mutation.Name), SqlRead.Text("@NormalizedName", mutation.NormalizedName),
            SqlRead.Text("@DisplayName", mutation.DisplayName), SqlRead.Text("@Email", mutation.Email), SqlRead.P("@IsActive", SqlDbType.Bit, mutation.IsActive),
            SqlRead.Text("@PasswordHash", mutation.PasswordHash), SqlRead.Text("@PasswordAlgorithm", mutation.PasswordAlgorithm),
            SqlRead.Text("@PermissionKey", mutation.PermissionKey), SqlRead.P("@Decision", SqlDbType.Bit, mutation.Decision),
            SqlRead.P("@ExpectedRevision", SqlDbType.BigInt, mutation.ExpectedRevision), SqlRead.P("@ExpectedRelatedRevision", SqlDbType.BigInt, mutation.ExpectedRelatedRevision),
            SqlRead.P("@CorrelationId", SqlDbType.UniqueIdentifier, mutation.CorrelationId));
        command.CommandType = CommandType.StoredProcedure;
        return await SqlRead.SingleAsync(command, reader => new SecurityAdministrationResultDto(
            reader.GetInt64(0), reader.GetBoolean(1), reader.IsDBNull(2) ? null : reader.GetInt64(2)), cancellationToken).ConfigureAwait(false);
    }

    public async Task<PermissionAssignmentImportTargetState> ReadTargetAsync(
        PermissionImportTargetTypeDto targetType,
        int targetId,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        using var command = SqlRead.Command(_context.Connection, _context.Transaction, "dbo.ReadSecurityPermissionImportTarget",
            SqlRead.Text("@TargetType", targetType == PermissionImportTargetTypeDto.User ? "User" : "Role"),
            SqlRead.P("@TargetId", SqlDbType.Int, targetId), SqlRead.P("@ActorUserId", SqlDbType.Int, actorUserId));
        command.CommandType = CommandType.StoredProcedure;
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            // Drain the empty grid before the invariant guard so deferred SQL51031 is mapped.
            await SqlRead.DrainAsync(reader, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Import target procedure returned no target and no SQL error.");
        }
        var target = new ImportTargetRow
        {
            TargetName = reader.GetString(reader.GetOrdinal("TargetName")), IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            IsProtected = reader.GetBoolean(reader.GetOrdinal("IsProtected")), AssignmentRevision = reader.GetInt64(reader.GetOrdinal("AssignmentRevision")),
        };
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            await SqlRead.DrainAsync(reader, cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Import target procedure returned multiple targets.");
        }
        if (!await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Import decisions result set is missing.");
        var decisions = new Dictionary<string, bool>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            decisions.Add(reader.GetString(0), reader.GetBoolean(1));
        await SqlRead.DrainAsync(reader, cancellationToken).ConfigureAwait(false);
        return new PermissionAssignmentImportTargetState(target.TargetName, target.IsActive, target.IsProtected, target.AssignmentRevision, decisions);
    }

    public async Task<PermissionAssignmentImportResultDto> ApplyAsync(
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
        using var command = SqlRead.Command(_context.Connection, _context.Transaction, "dbo.ApplySecurityPermissionImport",
            SqlRead.Text("@TargetType", import.TargetType == PermissionImportTargetTypeDto.User ? "User" : "Role"),
            SqlRead.P("@TargetId", SqlDbType.Int, import.TargetId), SqlRead.P("@ActorUserId", SqlDbType.Int, actorUserId),
            SqlRead.P("@ExpectedRevision", SqlDbType.BigInt, expectedTargetRevision),
            new SqlParameter("@EntriesJson", SqlDbType.NVarChar, -1) { Value = entriesJson }, SqlRead.P("@CorrelationId", SqlDbType.UniqueIdentifier, correlationId));
        command.CommandType = CommandType.StoredProcedure;
        return await SqlRead.SingleAsync(command, reader => new PermissionAssignmentImportResultDto(
            reader.GetInt64(0), reader.GetInt32(1), reader.GetBoolean(2)), cancellationToken).ConfigureAwait(false);
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

}
