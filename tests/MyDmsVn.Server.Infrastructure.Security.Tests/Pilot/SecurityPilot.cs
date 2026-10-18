using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Server.Infrastructure.Security;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests.Pilot;

// Temporary native procedure escape through T01's borrowed session; reuse the production store's error mapping.
internal sealed class SecurityPilot(ISqlExecutionContext context, ILinqToDbSessionFactory sessions) : IPermissionAssignmentImportRepository
{
    public async Task<PermissionAssignmentImportTargetState> ReadTargetAsync(PermissionImportTargetTypeDto type, int id, int actor, CancellationToken token)
    {
        using var db = sessions.Borrow(context, false);
        using var command = PilotSql.Command((SqlConnection)db.OpenDbConnection(), (SqlTransaction?)context.Transaction, "dbo.ReadSecurityPermissionImportTarget",
            new SqlParameter("@TargetType", SqlDbType.NVarChar, 8) { Value = type == PermissionImportTargetTypeDto.User ? "User" : "Role" },
            PilotSql.P("@TargetId", SqlDbType.Int, id), PilotSql.P("@ActorUserId", SqlDbType.Int, actor));
        command.CommandType = CommandType.StoredProcedure; using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token))
        {
            // Drain to surface a procedure THROW delivered after an empty first result set.
            while (await reader.NextResultAsync(token)) while (await reader.ReadAsync(token)) { }
            throw new InvalidOperationException("Import target procedure returned no target and no SQL error.");
        }
        var name = reader.GetString(reader.GetOrdinal("TargetName")); var active = reader.GetBoolean(reader.GetOrdinal("IsActive"));
        var revision = reader.GetInt64(reader.GetOrdinal("AssignmentRevision")); var protect = reader.GetBoolean(reader.GetOrdinal("IsProtected"));
        if (!await reader.NextResultAsync(token)) throw new InvalidOperationException("Import decisions result set missing.");
        var decisions = new Dictionary<string, bool>(StringComparer.Ordinal);
        while (await reader.ReadAsync(token)) decisions.Add(reader.GetString(0), reader.GetBoolean(1));
        while (await reader.NextResultAsync(token)) while (await reader.ReadAsync(token)) { }
        return new PermissionAssignmentImportTargetState(name, active, protect, revision, decisions);
    }
    public async Task<PermissionAssignmentImportResultDto> ApplyAsync(ParsedPermissionAssignmentImportDto import, int actor, long revision, Guid correlation, CancellationToken token)
    {
        using var db = sessions.Borrow(context, true);
        var json = JsonConvert.SerializeObject(import.Entries.Select(e => new { e.PermissionKey, e.Granted }));
        using var command = PilotSql.Command((SqlConnection)db.OpenDbConnection(), (SqlTransaction)context.Transaction!, "dbo.ApplySecurityPermissionImport",
            new SqlParameter("@TargetType", SqlDbType.NVarChar, 8) { Value = import.TargetType == PermissionImportTargetTypeDto.User ? "User" : "Role" },
            PilotSql.P("@TargetId", SqlDbType.Int, import.TargetId), PilotSql.P("@ActorUserId", SqlDbType.Int, actor), PilotSql.P("@ExpectedRevision", SqlDbType.BigInt, revision),
            new SqlParameter("@EntriesJson", SqlDbType.NVarChar, -1) { Value = json }, PilotSql.P("@CorrelationId", SqlDbType.UniqueIdentifier, correlation));
        command.CommandType = CommandType.StoredProcedure; using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw new InvalidOperationException("Import result missing.");
        var result = new PermissionAssignmentImportResultDto(reader.GetInt64(reader.GetOrdinal("TargetRevision")), reader.GetInt32(reader.GetOrdinal("AddedCount")), reader.GetBoolean(reader.GetOrdinal("Changed")));
        while (await reader.NextResultAsync(token)) while (await reader.ReadAsync(token)) { }
        return result;
    }
}
