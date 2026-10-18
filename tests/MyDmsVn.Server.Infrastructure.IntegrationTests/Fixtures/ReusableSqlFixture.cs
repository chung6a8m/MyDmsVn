using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.DbMigrator;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal sealed class SqlFixtureObservation
{
    internal SqlFixtureObservation(SqlFixtureRunMode mode, string operation, string? caseLabel, bool succeeded, TimeSpan elapsed)
    {
        Mode = mode;
        Operation = operation;
        CaseLabel = caseLabel;
        Succeeded = succeeded;
        Elapsed = elapsed;
    }
    public SqlFixtureRunMode Mode { get; }
    public string Operation { get; }
    public string? CaseLabel { get; }
    public bool Succeeded { get; }
    public TimeSpan Elapsed { get; }
    public override string ToString() => string.Format(CultureInfo.InvariantCulture,
        "SQL fixture mode={0} profile=APP phase={1} case={2} succeeded={3} ms={4:F1}",
        Mode.ToString().ToLowerInvariant(), Operation, CaseLabel ?? "(fixture)", Succeeded, Elapsed.TotalMilliseconds);
}

// One instance per xUnit collection. No static cache and no shared application connection/UoW.
internal sealed class ReusableSqlFixture
{
    private readonly SemaphoreSlim _admission = new SemaphoreSlim(1, 1);
    private readonly object _disposeSync = new object();
    private readonly List<SqlFixtureObservation> _observations = new List<SqlFixtureObservation>();
    private readonly Func<CancellationToken, Task<SqlTestDatabase>> _allocate;
    private readonly Func<string, CancellationToken, Task> _migrate;
    private readonly Func<SqlTestDatabase, CancellationToken, Task<RespawnResetProfile>> _prepare;
    private SqlTestDatabase? _database;
    private RespawnResetProfile? _profile;
    private bool _initialized;
    private int _poisoned;
    private int _closing;
    private Task? _disposal;

    internal ReusableSqlFixture(SqlFixtureRunMode mode,
        Func<CancellationToken, Task<SqlTestDatabase>> allocate,
        Func<string, CancellationToken, Task> migrate,
        Func<SqlTestDatabase, CancellationToken, Task<RespawnResetProfile>> prepare)
    {
        if (mode != SqlFixtureRunMode.Fresh && mode != SqlFixtureRunMode.Reuse)
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        Mode = mode;
        _allocate = allocate ?? throw new ArgumentNullException(nameof(allocate));
        _migrate = migrate ?? throw new ArgumentNullException(nameof(migrate));
        _prepare = prepare ?? throw new ArgumentNullException(nameof(prepare));
    }

    public SqlFixtureRunMode Mode { get; }
    public bool IsPoisoned => Volatile.Read(ref _poisoned) != 0;
    public IReadOnlyList<SqlFixtureObservation> Observations
    {
        get { lock (_observations) { return _observations.ToArray(); } }
    }

    public static ReusableSqlFixture CreateApplication(SqlFixtureRunMode? mode = null,
        Func<string, CancellationToken, Task>? migrate = null,
        Func<SqlTestDatabase, CancellationToken, Task<RespawnResetProfile>>? prepare = null) =>
        new ReusableSqlFixture(mode ?? SqlFixtureRunModeSetting.Parse(
            Environment.GetEnvironmentVariable(SqlFixtureRunModeSetting.EnvironmentVariable)),
            token => SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(
                SqlTestDatabase.ConnectionStringEnvironmentVariable)!, token),
            migrate ?? ((connectionString, token) =>
            {
                token.ThrowIfCancellationRequested();
                var result = new DatabaseMigrationRunner().Migrate(connectionString);
                if (!result.Successful) { throw result.Error ?? new InvalidOperationException("Fixture migration failed."); }
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }),
            prepare ?? ((database, token) => RespawnResetProfile.CreateAsync(database, cancellationToken: token)));

    public async Task InitializeAsync(CancellationToken token = default)
    {
        await _admission.WaitAsync(token).ConfigureAwait(false);
        try { ThrowIfUnavailable(); }
        catch { _admission.Release(); throw; }
        try
        {
            await InitializeCoreAsync(token).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            Poison();
            await DropAfterFailureAsync(error).ConfigureAwait(false);
            throw;
        }
        finally { _admission.Release(); }
    }

    private async Task InitializeCoreAsync(CancellationToken token)
    {
        if (_initialized) { return; }
        if (Mode == SqlFixtureRunMode.Reuse) { await AllocateAsync(token).ConfigureAwait(false); }
        _initialized = true;
    }

    private async Task AllocateAsync(CancellationToken token)
    {
        await MeasureAsync("allocate", null, async () =>
            _database = await _allocate(token).ConfigureAwait(false)).ConfigureAwait(false);
        await MeasureAsync("migrate", null, () => _migrate(_database!.ConnectionString, token)).ConfigureAwait(false);
        // T01 profile creation includes one first reset to build and validate cached metadata.
        await MeasureAsync("initialize-reset", null, async () =>
        {
            _profile = await _prepare(_database!, token).ConfigureAwait(false);
            if (_profile is null || !_profile.BelongsTo(_database!))
            {
                throw new InvalidOperationException("The reset profile must belong to this allocation.");
            }
        }).ConfigureAwait(false);
    }

    public async Task<SqlFixtureTestScope> BeginTestAsync(string label,
        Func<SqlFixtureTestScope, CancellationToken, Task> seed, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Length > 160 || label.Any(char.IsControl))
        {
            throw new ArgumentException("A bounded non-sensitive method/row label is required.", nameof(label));
        }
        if (seed is null) { throw new ArgumentNullException(nameof(seed)); }
        await _admission.WaitAsync(token).ConfigureAwait(false);
        try { ThrowIfUnavailable(); }
        catch { _admission.Release(); throw; }
        SqlFixtureTestScope? scope = null;
        try
        {
            await InitializeCoreAsync(token).ConfigureAwait(false);
            if (Mode == SqlFixtureRunMode.Fresh) { await AllocateAsync(token).ConfigureAwait(false); }
            scope = new SqlFixtureTestScope(label, _database!.ConnectionString,
                clean => FinishScopeAsync(scope!, clean));
            await MeasureAsync("reset", label, () => _profile!.ResetAsync(token)).ConfigureAwait(false);
            await MeasureAsync("seed", label, () => seed(scope, token)).ConfigureAwait(false);
            await MeasureAsync("verify", label, () => _profile!.VerifyAsync(token)).ConfigureAwait(false);
            return scope; // The scope owns admission until workers and resources have drained.
        }
        catch (Exception error)
        {
            Poison();
            if (scope != null)
            {
                try { await scope.DisposeAsync().ConfigureAwait(false); }
                catch (Exception cleanup) { throw new AggregateException("Admission and scope cleanup failed.", error, cleanup); }
            }
            else
            {
                try { await DropAfterFailureAsync(error).ConfigureAwait(false); }
                finally { _admission.Release(); }
            }
            throw;
        }
    }

    public async Task RunTestAsync(string label, Func<SqlFixtureTestScope, CancellationToken, Task> seed,
        Func<SqlFixtureTestScope, CancellationToken, Task> scenario, CancellationToken token = default)
    {
        if (scenario is null) { throw new ArgumentNullException(nameof(scenario)); }
        var scope = await BeginTestAsync(label, seed, token).ConfigureAwait(false);
        Exception? failure = null;
        try { await scenario(scope, token).ConfigureAwait(false); }
        catch (Exception error) { failure = error; }
        try { await scope.DisposeAsync().ConfigureAwait(false); }
        catch (Exception cleanup)
        {
            if (failure != null) { throw new AggregateException("Scenario and cleanup failed.", failure, cleanup); }
            throw;
        }
        if (failure != null) { ExceptionDispatchInfo.Capture(failure).Throw(); }
    }

    private async Task FinishScopeAsync(SqlFixtureTestScope scope, bool clean)
    {
        var succeeded = clean;
        if (!clean) { Poison(); }
        try
        {
            if (Mode == SqlFixtureRunMode.Fresh) { await DropAsync().ConfigureAwait(false); }
        }
        catch { succeeded = false; Poison(); throw; }
        finally
        {
            Record("cleanup", scope.CaseLabel, succeeded, scope.CleanupElapsed);
            _admission.Release();
        }
    }

    public Task DisposeAsync()
    {
        lock (_disposeSync)
        {
            Interlocked.Exchange(ref _closing, 1);
            // An explicit teardown retry may drop the same positively owned allocation.
            // It never clears poison or allocates a replacement.
            if (_disposal is null || _disposal.IsFaulted) { _disposal = DisposeCoreAsync(); }
            return _disposal;
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _admission.WaitAsync().ConfigureAwait(false);
        try { await DropAsync().ConfigureAwait(false); }
        catch { Poison(); throw; }
        finally { _admission.Release(); }
    }

    private async Task DropAfterFailureAsync(Exception original)
    {
        try { await DropAsync().ConfigureAwait(false); }
        catch (Exception cleanup) { throw new AggregateException("Fixture setup and guarded cleanup failed.", original, cleanup); }
    }

    private async Task DropAsync()
    {
        if (_database is null) { return; }
        await MeasureAsync("drop", null, () => _database.DisposeAsync()).ConfigureAwait(false);
        _database = null;
        _profile = null;
    }

    private void ThrowIfUnavailable()
    {
        if (Volatile.Read(ref _closing) != 0) { throw new ObjectDisposedException(nameof(ReusableSqlFixture)); }
        if (IsPoisoned) { throw new InvalidOperationException("The SQL fixture is poisoned; no later body may run."); }
    }
    private void Poison() => Volatile.Write(ref _poisoned, 1);

    private async Task MeasureAsync(string operation, string? label, Func<Task> action)
    {
        var watch = Stopwatch.StartNew();
        var succeeded = false;
        try { await action().ConfigureAwait(false); succeeded = true; }
        finally { Record(operation, label, succeeded, watch.Elapsed); }
    }
    private void Record(string operation, string? label, bool succeeded, TimeSpan elapsed)
    {
        lock (_observations) { _observations.Add(new SqlFixtureObservation(Mode, operation, label, succeeded, elapsed)); }
    }
}
