using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal sealed class SqlConcurrencyGate : IDisposable
{
    private const int PollDelayMilliseconds = 25;
    private readonly SqlConnection _controllerConnection;
    private readonly string _observerConnectionString;
    private int _releaseState;

    private SqlConcurrencyGate(
        SqlConnection controllerConnection,
        string observerConnectionString,
        string resourceName,
        int controllerSessionId)
    {
        _controllerConnection = controllerConnection;
        _observerConnectionString = observerConnectionString;
        ResourceName = resourceName;
        ControllerSessionId = controllerSessionId;
        TransactionWaitSql =
            "DECLARE @testGateResult int; " +
            "EXEC @testGateResult = sys.sp_getapplock " +
            "@Resource = N'" + EscapeSqlLiteral(resourceName) + "', " +
            "@LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = -1; " +
            "IF @testGateResult < 0 THROW 51000, 'The SQL test concurrency gate timed out.', 1;";
    }

    public int ControllerSessionId { get; }

    public string ResourceName { get; }

    public string TransactionWaitSql { get; }

    public static async Task<SqlConcurrencyGate> AcquireAsync(
        string connectionString,
        string scenarioName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL connection string is required.", nameof(connectionString));
        }

        if (string.IsNullOrWhiteSpace(scenarioName))
        {
            throw new ArgumentException("A scenario name is required.", nameof(scenarioName));
        }

        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var resourceName =
                "MyDmsVn.SqlTestGate." + scenarioName + "." + Guid.NewGuid().ToString("N");
            var result = await connection.QuerySingleAsync<int>(
                    new CommandDefinition(
                        "DECLARE @result int; " +
                        "EXEC @result = sys.sp_getapplock " +
                        "@Resource = @resourceName, @LockMode = N'Exclusive', " +
                        "@LockOwner = N'Session', @LockTimeout = 0; SELECT @result;",
                        new { resourceName },
                        cancellationToken: cancellationToken))
                .ConfigureAwait(false);
            if (result < 0)
            {
                throw new InvalidOperationException(
                    "The SQL test concurrency controller could not acquire its application lock: " + result + ".");
            }

            var sessionId = await connection.QuerySingleAsync<int>(
                    new CommandDefinition("SELECT @@SPID;", cancellationToken: cancellationToken))
                .ConfigureAwait(false);
            return new SqlConcurrencyGate(connection, connectionString, resourceName, sessionId);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public Task<SqlWaitObservation> WaitForHolderAsync(
        int holderSessionId,
        string qualifiedBusinessTable,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        WaitForObservationAsync(
            holderSessionId,
            timeout,
            cancellationToken,
            (connection, observationToken, commandTimeout) => connection.QuerySingleOrDefaultAsync<SqlWaitObservation>(
                new CommandDefinition(
                    "SELECT TOP (1) request.wait_type AS WaitType, request.blocking_session_id AS BlockingSessionId " +
                    "FROM sys.dm_exec_requests AS request " +
                    "WHERE request.session_id = @holderSessionId " +
                    "AND request.blocking_session_id = @controllerSessionId " +
                    "AND request.wait_type LIKE N'LCK[_]M[_]%' " +
                    "AND EXISTS (SELECT 1 FROM sys.dm_tran_locks AS gateLock " +
                    "WHERE gateLock.request_session_id = request.session_id " +
                    "AND gateLock.resource_database_id = DB_ID() " +
                    "AND gateLock.resource_type = N'APPLICATION' " +
                    "AND gateLock.request_status = N'WAIT') " +
                    "AND EXISTS (SELECT 1 FROM sys.dm_tran_locks AS businessLock " +
                    "LEFT JOIN sys.partitions AS partitionInfo " +
                    "ON partitionInfo.hobt_id = businessLock.resource_associated_entity_id " +
                    "WHERE businessLock.request_session_id = request.session_id " +
                    "AND businessLock.resource_database_id = DB_ID() " +
                    "AND businessLock.request_status = N'GRANT' " +
                    "AND businessLock.request_mode IN (N'IX', N'X', N'U', N'SIX') " +
                    "AND (partitionInfo.object_id = OBJECT_ID(@qualifiedBusinessTable) " +
                    "OR (businessLock.resource_type = N'OBJECT' " +
                    "AND businessLock.resource_associated_entity_id = OBJECT_ID(@qualifiedBusinessTable))));",
                    new
                    {
                        holderSessionId,
                        controllerSessionId = ControllerSessionId,
                        qualifiedBusinessTable,
                    },
                    commandTimeout: commandTimeout,
                    cancellationToken: observationToken)));

    public Task<SqlWaitObservation> WaitForBusinessBlockAsync(
        int contenderSessionId,
        int holderSessionId,
        string qualifiedBusinessTable,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        WaitForObservationAsync(
            contenderSessionId,
            timeout,
            cancellationToken,
            (connection, observationToken, commandTimeout) => connection.QuerySingleOrDefaultAsync<SqlWaitObservation>(
                new CommandDefinition(
                    "SELECT TOP (1) request.wait_type AS WaitType, request.blocking_session_id AS BlockingSessionId " +
                    "FROM sys.dm_exec_requests AS request " +
                    "INNER JOIN sys.dm_tran_locks AS waitingLock " +
                    "ON waitingLock.request_session_id = request.session_id " +
                    "LEFT JOIN sys.partitions AS partitionInfo " +
                    "ON partitionInfo.hobt_id = waitingLock.resource_associated_entity_id " +
                    "WHERE request.session_id = @contenderSessionId " +
                    "AND request.blocking_session_id = @holderSessionId " +
                    "AND request.wait_type LIKE N'LCK[_]M[_]%' " +
                    "AND waitingLock.resource_database_id = DB_ID() " +
                    "AND waitingLock.request_status = N'WAIT' " +
                    "AND waitingLock.resource_type <> N'APPLICATION' " +
                    "AND (partitionInfo.object_id = OBJECT_ID(@qualifiedBusinessTable) " +
                    "OR (waitingLock.resource_type = N'OBJECT' " +
                    "AND waitingLock.resource_associated_entity_id = OBJECT_ID(@qualifiedBusinessTable)));",
                    new { contenderSessionId, holderSessionId, qualifiedBusinessTable },
                    commandTimeout: commandTimeout,
                    cancellationToken: observationToken)));

    public async Task ReleaseAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _releaseState, 1, 0) != 0)
        {
            return;
        }

        try
        {
            if (_controllerConnection.State == System.Data.ConnectionState.Open)
            {
                var result = await _controllerConnection.QuerySingleAsync<int>(
                        new CommandDefinition(
                            "DECLARE @result int; " +
                            "EXEC @result = sys.sp_releaseapplock " +
                            "@Resource = @resourceName, @LockOwner = N'Session'; SELECT @result;",
                            new { resourceName = ResourceName },
                            cancellationToken: cancellationToken))
                    .ConfigureAwait(false);
                if (result < 0)
                {
                    throw new InvalidOperationException(
                        "The SQL test concurrency controller could not release its application lock: " + result + ".");
                }
            }

            Volatile.Write(ref _releaseState, 2);
        }
        catch
        {
            Volatile.Write(ref _releaseState, 0);
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            ReleaseAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            _controllerConnection.Dispose();
        }
    }

    private async Task<SqlWaitObservation> WaitForObservationAsync(
        int sessionId,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Func<SqlConnection, CancellationToken, int, Task<SqlWaitObservation?>> observe)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        using var observationTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        observationTimeout.CancelAfter(timeout);
        var commandTimeout = Math.Max(1, (int)Math.Ceiling(timeout.TotalSeconds));
        using var observer = new SqlConnection(_observerConnectionString);
        try
        {
            await observer.OpenAsync(observationTimeout.Token).ConfigureAwait(false);
            while (true)
            {
                var observation = await observe(
                        observer,
                        observationTimeout.Token,
                        commandTimeout)
                    .ConfigureAwait(false);
                if (observation != null)
                {
                    return observation;
                }

                await Task.Delay(PollDelayMilliseconds, observationTimeout.Token).ConfigureAwait(false);
            }
        }
        catch (SqlException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                "SQL wait observation was canceled.",
                exception,
                cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "SQL session " + sessionId + " did not reach the expected bounded lock wait.");
        }
    }

    private static string EscapeSqlLiteral(string value) => value.Replace("'", "''");
}

internal sealed class SqlWaitObservation
{
    public string WaitType { get; set; } = string.Empty;

    public int BlockingSessionId { get; set; }
}
