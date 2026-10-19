using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
internal sealed class SqlCommandObserverConnectionFactory : IDbConnectionFactory, IDisposable, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    private SqlConnection? _last;
    private readonly string _connectionString;
    private readonly IDisposable _all;
    private readonly List<IDisposable> _listeners = new();
    public List<(string Sql, string Parameters)> Commands { get; } = new();
    public SqlCommandObserverConnectionFactory(string connectionString) { _connectionString = connectionString; _all = DiagnosticListener.AllListeners.Subscribe(this); }
    public void OnNext(DiagnosticListener listener) { if (listener.Name == "SqlClientDiagnosticListener") _listeners.Add(listener.Subscribe(this)); }
    public void OnNext(KeyValuePair<string, object?> item)
    {
        if (!item.Key.EndsWith("WriteCommandBefore", StringComparison.Ordinal)) return;
        var command = item.Value?.GetType().GetProperty("Command")?.GetValue(item.Value) as SqlCommand;
        if (command is null || !ReferenceEquals(command.Connection, _last)) return;
        Commands.Add((command.CommandText, string.Join(",", command.Parameters.Cast<SqlParameter>().Select(p => p.SqlDbType + "(" + p.Size + ")"))));
    }
    public void OnCompleted() { }
    public void OnError(Exception error) { throw new InvalidOperationException("SQL command observation failed.", error); }
    public void Dispose() { _all.Dispose(); foreach (var listener in _listeners) listener.Dispose(); }
    public long RoundTrips => (long)(_last!.RetrieveStatistics()["ServerRoundtrips"]!);
    public IDbConnection OpenConnection() => OpenConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();
    public async Task<IDbConnection> OpenConnectionAsync(CancellationToken token)
    {
        _last = new SqlConnection(_connectionString);
        try { await _last.OpenAsync(token); _last.StatisticsEnabled = true; return _last; }
        catch { _last.Dispose(); throw; }
    }
}
