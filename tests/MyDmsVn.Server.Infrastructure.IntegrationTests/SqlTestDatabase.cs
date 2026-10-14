using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.SqlTestHarness;
using MyDmsVn.SqlTestHarness.Approval;
using MyDmsVn.SqlTestHarness.Ownership;
using MyDmsVn.SqlTestHarness.Preflight;
using MyDmsVn.SqlTestHarness.Timing;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal sealed class SqlTestDatabase : IDisposable
{
    public const string ConnectionStringEnvironmentVariable =
        SqlTestHarnessEnvironment.ConnectionStringVariable;

    public const string ApprovalFileEnvironmentVariable =
        SqlTestHarnessEnvironment.ApprovalFileVariable;

    private static readonly string DirectRunId =
        "direct-" + Process.GetCurrentProcess().Id + "-" + Guid.NewGuid().ToString("N");

    private readonly string _adminConnectionString;
    private readonly string _approvalFilePath;
    private readonly IDatabaseOwnershipJournal _journal;
    private readonly Func<string, ISqlAdministrativeSession> _sessionFactory;
    private DatabaseOwnershipRecord _record;
    private int _disposeState;

    private SqlTestDatabase(
        string adminConnectionString,
        string approvalFilePath,
        IDatabaseOwnershipJournal journal,
        Func<string, ISqlAdministrativeSession> sessionFactory,
        DatabaseOwnershipRecord record,
        SqlTestMeasurementContext measurementContext)
    {
        _adminConnectionString = adminConnectionString;
        _approvalFilePath = approvalFilePath;
        _journal = journal;
        _sessionFactory = sessionFactory;
        _record = record;
        MeasurementContext = measurementContext;
        DatabaseName = record.DatabaseName;

        var builder = new SqlConnectionStringBuilder(adminConnectionString)
        {
            InitialCatalog = DatabaseName,
            Pooling = false,
        };
        ConnectionString = builder.ConnectionString;
    }

    public string ConnectionString { get; }

    public string DatabaseName { get; }

    public SqlTestMeasurementContext MeasurementContext { get; }

    public static Task<SqlTestDatabase> CreateAsync(
        string adminConnectionString,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string testCaseId = "")
    {
        return CreateAsync(
            adminConnectionString,
            CreateMeasurementContextForTest(testCaseId),
            cancellationToken);
    }

    public static Task<SqlTestDatabase> CreateAsync(
        string adminConnectionString,
        SqlTestMeasurementContext? measurementContext,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string testCaseId = "")
    {
        ValidateAdminConnectionString(adminConnectionString);
        var approvalFilePath = Environment.GetEnvironmentVariable(ApprovalFileEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(approvalFilePath))
        {
            throw new InvalidOperationException(
                $"Set {ApprovalFileEnvironmentVariable} to an operator-maintained non-production SQL approval file.");
        }

        return CreateAsync(
            adminConnectionString,
            approvalFilePath,
            CreateDefaultJournal(),
            connectionString => new SqlClientAdministrativeSession(connectionString),
            cancellationToken,
            measurementContext ?? CreateMeasurementContextForTest(testCaseId),
            _ => { });
    }

    internal static Task<SqlTestDatabase> CreateAsync(
        string adminConnectionString,
        string approvalFilePath,
        IDatabaseOwnershipJournal journal,
        Func<string, ISqlAdministrativeSession> sessionFactory,
        CancellationToken cancellationToken,
        [CallerMemberName] string testCaseId = "") =>
        CreateAsync(
            adminConnectionString,
            approvalFilePath,
            journal,
            sessionFactory,
            cancellationToken,
            CreateMeasurementContextForTest(testCaseId),
            _ => { });

    internal static Task<SqlTestDatabase> CreateAsync(
        string adminConnectionString,
        CancellationToken cancellationToken,
        Func<string, ISqlAdministrativeSession> sessionFactory,
        Action<string> afterCreate,
        [CallerMemberName] string testCaseId = "")
    {
        ValidateAdminConnectionString(adminConnectionString);
        var approvalFilePath = Environment.GetEnvironmentVariable(ApprovalFileEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(approvalFilePath))
        {
            throw new InvalidOperationException(
                $"Set {ApprovalFileEnvironmentVariable} to an operator-maintained non-production SQL approval file.");
        }

        return CreateAsync(
            adminConnectionString,
            approvalFilePath,
            CreateDefaultJournal(),
            sessionFactory,
            cancellationToken,
            CreateMeasurementContextForTest(testCaseId),
            afterCreate);
    }

    private static async Task<SqlTestDatabase> CreateAsync(
        string adminConnectionString,
        string approvalFilePath,
        IDatabaseOwnershipJournal journal,
        Func<string, ISqlAdministrativeSession> sessionFactory,
        CancellationToken cancellationToken,
        SqlTestMeasurementContext measurementContext,
        Action<string> afterCreate)
    {
        ValidateAdminConnectionString(adminConnectionString);
        if (journal is null)
        {
            throw new ArgumentNullException(nameof(journal));
        }

        if (sessionFactory is null)
        {
            throw new ArgumentNullException(nameof(sessionFactory));
        }

        if (afterCreate is null)
        {
            throw new ArgumentNullException(nameof(afterCreate));
        }

        var approval = SqlInstanceApprovalLoader.Load(approvalFilePath);
        var databaseName = SqlTestDatabaseName.Create();
        SqlTestDatabaseName.EnsureDisposable(databaseName);
        var runId = GetRunId();
        var record = new DatabaseOwnershipRecord(
            runId,
            Guid.NewGuid().ToString("N"),
            databaseName,
            Guid.NewGuid().ToString("N"),
            approval.FingerprintSha256,
            approval.ApprovalId,
            DatabaseOwnershipState.CreateIntent,
            DateTimeOffset.UtcNow,
            databaseId: null,
            databaseCreateDateUtc: null);
        var database = new SqlTestDatabase(
            adminConnectionString,
            approvalFilePath,
            journal,
            sessionFactory,
            record,
            measurementContext);

        var createIssued = false;
        var ownershipConfirmed = false;
        using var timing = measurementContext.StartOperation(
            "create",
            "database-create-" + record.OwnershipToken);
        try
        {
            using var session = sessionFactory(adminConnectionString);
            var preflight = await new SqlPreflightValidator()
                .ValidateOpenSessionAsync(
                    adminConnectionString,
                    approval,
                    SqlPreflightRequirements.Mutation,
                    session,
                    cancellationToken)
                .ConfigureAwait(false);
            EnsureAuthorized(preflight);

            await journal.WriteCreateIntentAsync(record, cancellationToken).ConfigureAwait(false);
            await session.ExecuteAsync(
                    $"CREATE DATABASE [{databaseName}];",
                    EmptyParameters,
                    cancellationToken)
                .ConfigureAwait(false);
            createIssued = true;

            var marker = new DatabaseOwnershipMarker(runId, record.OwnershipToken);
            await session.ExecuteAsync(
                    $"EXEC [{databaseName}].sys.sp_addextendedproperty " +
                    "@name = N'MyDmsVn.TestOwnership', @value = @markerValue;",
                    new Dictionary<string, object?>
                    {
                        ["@markerValue"] = marker.Serialize(),
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            var live = await session.ReadDatabaseOwnershipAsync(databaseName, cancellationToken)
                .ConfigureAwait(false);
            if (live is null || live.Marker is null ||
                !string.Equals(live.Marker.RunId, runId, StringComparison.Ordinal) ||
                !string.Equals(live.Marker.OwnershipToken, record.OwnershipToken, StringComparison.Ordinal))
            {
                record = await journal.TransitionAsync(
                        record,
                        DatabaseOwnershipState.Quarantined,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                database._record = record;
                throw new InvalidOperationException(
                    "The created SQL test database ownership marker could not be corroborated.");
            }

            record = await journal.TransitionAsync(
                    record,
                    DatabaseOwnershipState.Created,
                    live.DatabaseId,
                    live.CreateDateUtc,
                    cancellationToken)
                .ConfigureAwait(false);
            database._record = record;
            ownershipConfirmed = true;
            afterCreate(databaseName);
            return database;
        }
        catch (Exception provisioningError)
        {
            if (provisioningError is OperationCanceledException)
            {
                timing.Cancel();
            }
            else
            {
                timing.Fail();
            }
            if (createIssued && !ownershipConfirmed &&
                database._record.State != DatabaseOwnershipState.Quarantined)
            {
                try
                {
                    database._record = await journal.TransitionAsync(
                            database._record,
                            DatabaseOwnershipState.Quarantined,
                            cancellationToken: CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // Preserve the original failure. An incomplete journal remains ambiguous and is never auto-dropped.
                }
            }

            if (ownershipConfirmed)
            {
                try
                {
                    await database.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception cleanupError)
                {
                    throw new AggregateException(
                        "SQL test database provisioning failed and guarded cleanup also failed.",
                        provisioningError,
                        cleanupError);
                }
            }

            throw;
        }
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }

    public async Task DisposeAsync()
    {
        var previousState = Interlocked.CompareExchange(ref _disposeState, 1, 0);
        if (previousState == 2)
        {
            return;
        }

        if (previousState == 1)
        {
            throw new InvalidOperationException("SQL test database cleanup is already in progress.");
        }

        using var timing = MeasurementContext.StartOperation(
            "drop",
            "database-drop-" + _record.OwnershipToken);
        try
        {
            SqlTestDatabaseName.EnsureDisposable(DatabaseName);
            var approval = SqlInstanceApprovalLoader.Load(_approvalFilePath);
            using var session = _sessionFactory(_adminConnectionString);
            var preflight = await new SqlPreflightValidator()
                .ValidateOpenSessionAsync(
                    _adminConnectionString,
                    approval,
                    SqlPreflightRequirements.Mutation,
                    session,
                    CancellationToken.None)
                .ConfigureAwait(false);
            EnsureAuthorized(preflight);

            var live = await session.ReadDatabaseOwnershipAsync(DatabaseName, CancellationToken.None)
                .ConfigureAwait(false);
            if (live is null)
            {
                _record = await TransitionToDisposedWithoutDdlAsync().ConfigureAwait(false);
                Volatile.Write(ref _disposeState, 2);
                return;
            }

            _record = await _journal.TransitionAsync(
                    _record,
                    DatabaseOwnershipState.DropIntent)
                .ConfigureAwait(false);
            var reconciliation = await DatabaseOwnershipReconciler.ClassifyAsync(
                    _record,
                    live,
                    _record.RunId)
                .ConfigureAwait(false);
            if (!reconciliation.MayAutomaticallyClean)
            {
                _record = await _journal.TransitionAsync(
                        _record,
                        DatabaseOwnershipState.Quarantined)
                    .ConfigureAwait(false);
                throw new InvalidOperationException(
                    "SQL test database cleanup was quarantined because ownership could not be corroborated.");
            }

            await session.ExecuteAsync(
                    $"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                    $"DROP DATABASE [{DatabaseName}];",
                    EmptyParameters,
                    CancellationToken.None)
                .ConfigureAwait(false);
            _record = await _journal.TransitionAsync(
                    _record,
                    DatabaseOwnershipState.Disposed)
                .ConfigureAwait(false);
            Volatile.Write(ref _disposeState, 2);
        }
        catch (Exception cleanupError)
        {
            if (cleanupError is OperationCanceledException)
            {
                timing.Cancel();
            }
            else
            {
                timing.Fail();
            }
            if (_record.State == DatabaseOwnershipState.DropIntent)
            {
                try
                {
                    _record = await _journal.TransitionAsync(
                            _record,
                            DatabaseOwnershipState.CleanupFailed)
                        .ConfigureAwait(false);
                }
                catch
                {
                    // Keep the original cleanup failure and the durable DropIntent for operator reconciliation.
                }
            }

            Volatile.Write(ref _disposeState, 0);
            throw;
        }
    }

    private async Task<DatabaseOwnershipRecord> TransitionToDisposedWithoutDdlAsync()
    {
        var record = _record;
        if (record.State == DatabaseOwnershipState.Created ||
            record.State == DatabaseOwnershipState.CleanupFailed)
        {
            record = await _journal.TransitionAsync(record, DatabaseOwnershipState.DropIntent)
                .ConfigureAwait(false);
        }

        return await _journal.TransitionAsync(record, DatabaseOwnershipState.Disposed)
            .ConfigureAwait(false);
    }

    private static void ValidateAdminConnectionString(string adminConnectionString)
    {
        if (string.IsNullOrWhiteSpace(adminConnectionString))
        {
            throw new ArgumentException("A SQL Server connection string is required.", nameof(adminConnectionString));
        }

        var builder = new SqlConnectionStringBuilder(adminConnectionString);
        if (!string.Equals(builder.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The SQL test connection must explicitly target the master database.");
        }
    }

    private static void EnsureAuthorized(SqlPreflightResult result)
    {
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"SQL test mutation preflight failed ({result.Failure!.Kind}): {result.Failure.Message}");
        }
    }

    private static IDatabaseOwnershipJournal CreateDefaultJournal()
    {
        var configured = Environment.GetEnvironmentVariable(
            SqlTestHarnessEnvironment.JournalDirectoryVariable);
        var root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MyDmsVn",
                "SqlTestHarness",
                "journal")
            : configured;
        return new FileDatabaseOwnershipJournal(root);
    }

    private static string GetRunId()
    {
        var configured = Environment.GetEnvironmentVariable(SqlTestHarnessEnvironment.RunIdVariable);
        return string.IsNullOrWhiteSpace(configured) ? DirectRunId : configured;
    }

    internal static SqlTestMeasurementContext CreateMeasurementContextForTest(string testCaseId)
    {
        var enabled = string.Equals(
            Environment.GetEnvironmentVariable(SqlTestHarnessEnvironment.TimingEnabledVariable),
            "1",
            StringComparison.Ordinal);
        var outputPath = Environment.GetEnvironmentVariable(
            SqlTestHarnessEnvironment.TimingOutputVariable);
        if (enabled && string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException(
                $"Set {SqlTestHarnessEnvironment.TimingOutputVariable} when SQL timing is enabled.");
        }

        ISqlTestTimingSink sink = enabled
            ? new JsonLinesSqlTestTimingSink(outputPath!)
            : NullTimingSink.Instance;
        return SqlTestMeasurementContext.Create(
            GetRunId(),
            string.IsNullOrWhiteSpace(testCaseId) ? "unknown-test" : testCaseId,
#if NET48
            "net48",
#else
            "net8.0",
#endif
            Guid.NewGuid().ToString("N"),
            sink,
            enabled);
    }

    private sealed class NullTimingSink : ISqlTestTimingSink
    {
        public static NullTimingSink Instance { get; } = new NullTimingSink();

        public void Write(SqlTestTimingEvent timingEvent)
        {
        }
    }

    private static IReadOnlyDictionary<string, object?> EmptyParameters { get; } =
        new Dictionary<string, object?>();
}
