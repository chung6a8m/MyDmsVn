using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Catalog;

// T02-approved native escape: preserve one count/page round trip and a 64-bit offset.
// Identifiers/projections are internal constants; all request values are typed parameters.
internal static class SqlCatalogPageReader
{
    internal static string? NormalizeSearch(string? search) => string.IsNullOrWhiteSpace(search) ? null : search!.Trim();

    internal static async Task<PagedResult<TDto>> ReadAsync<TDto>(
        ILinqToDbSessionFactory sessions, string table, string key, string columns,
        CatalogListRequest request, Func<SqlDataReader, TDto> map, CancellationToken token)
    {
        using var db = await sessions.OpenOwnedAsync(token).ConfigureAwait(false);
        const string filter = "(@IncludeInactive = 1 OR IsActive = 1) AND (@Search IS NULL OR CHARINDEX(@Search, Code) > 0 OR CHARINDEX(@Search, Name) > 0)";
        using var command = new SqlCommand(
            "SELECT COUNT_BIG(*) FROM " + table + " WHERE " + filter + "; " +
            "SELECT " + columns + " FROM " + table + " WHERE " + filter +
            " ORDER BY Code, " + key + " OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;",
            (SqlConnection)db.OpenDbConnection()) { CommandTimeout = 30 };
        var search = NormalizeSearch(request.Search);
        command.Parameters.Add("@Search", SqlDbType.NVarChar, search?.Length > 4000 ? -1 : 4000).Value = (object?)search ?? DBNull.Value;
        command.Parameters.Add("@IncludeInactive", SqlDbType.Bit).Value = request.IncludeInactive;
        command.Parameters.Add("@Offset", SqlDbType.BigInt).Value = (long)(request.PageNumber - 1) * request.PageSize;
        command.Parameters.Add("@PageSize", SqlDbType.Int).Value = request.PageSize;
        using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Catalog count result is missing.");
        var total = reader.GetInt64(0);
        if (!await reader.NextResultAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Catalog page result is missing.");
        var rows = new List<TDto>();
        while (await reader.ReadAsync(token).ConfigureAwait(false)) rows.Add(map(reader));
        return new PagedResult<TDto>(rows, request.PageNumber, request.PageSize, total);
    }
}
