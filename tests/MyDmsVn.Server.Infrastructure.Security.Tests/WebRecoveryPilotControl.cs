using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Testing.Sql;
using MyDmsVn.Desktop.Tests;
using MyDmsVn.Web6.CandidateBootstrap;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Test-owner control channel, never an HTTP endpoint or a connection in browser code.
// One bounded request per connection lets independent Playwright cases share the fixture.
internal sealed class WebRecoveryPilotControl
{
    private readonly string _connectionString;
    private readonly string _name = "mydmsvn-web4-recovery-" + Guid.NewGuid().ToString("N");
    private readonly string _token = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _stop;
    private readonly List<Guid> _epochs = new List<Guid>();
    private Task _serving = Task.CompletedTask;
    private bool _stopped;
    private readonly Web5NegativeSequence _negativeSequence = new Web5NegativeSequence();
    private readonly List<Web5NegativePilot.Identity> _negativeIdentities = new List<Web5NegativePilot.Identity>();
    private Web5NegativePilot.Identity? _pendingNegative;
    private readonly bool _web5;
    private string? _negativeSnapshot;
    private SqlTestDatabase? _database;
    private Web5OriginalRaceManifest? _raceManifest;
    private readonly Web5NegativeSequence _raceSequence = new Web5NegativeSequence();
    private readonly List<(Guid Key, Web5OriginalFirstRace Race)> _races = new List<(Guid, Web5OriginalFirstRace)>();
    private Guid? _pendingRace;
    private readonly bool _web6;
    private readonly string? _runtimeUser;
    private readonly Dictionary<Guid, WebOperationHttpSqlTests.Intent> _web6Operations = new Dictionary<Guid, WebOperationHttpSqlTests.Intent>();
    private readonly Web6CandidateHostMismatch? _candidateMismatch;
    private readonly Func<Web6CutoverRequest, Web6CutoverDeadline, CancellationToken, Task<JObject>>? _cutover;
    private readonly Action<NamedPipeServerStream>? _closeCutoverResponse;
    private readonly Stopwatch? _cutoverLifetime;
    private readonly TimeSpan _cutoverControllerLifetime;
    private readonly OwnedCancellation? _cutoverController;
    private readonly object _cutoverDrainLock = new object();
    private OwnedCancellation? _cutoverPhase;
    private Exception? _cutoverPrimary;
    private bool _cutoverJoinUncertain;
    private IOException? _cutoverDrainFailure;
    private bool _eligibilityUserDisabled;
    private bool _eligibilityJournalDenied;
    private WebRecoveryPilotControl(string connectionString, bool web5, bool web6 = false, string? runtimeUser = null,
        Web6CandidateHostMismatch? candidateMismatch = null, bool candidatePair = false,
        Func<Web6CutoverRequest, Web6CutoverDeadline, CancellationToken, Task<JObject>>? cutover = null,
        Action<NamedPipeServerStream>? closeCutoverResponse = null, TimeSpan? cutoverControllerLifetime = null, CancellationToken cutoverParent = default)
    {
        _cutover = cutover;
        _closeCutoverResponse = closeCutoverResponse;
        _cutoverLifetime = cutover == null ? null : Stopwatch.StartNew();
        _cutoverControllerLifetime = cutoverControllerLifetime ?? Web6CutoverBudgets.Controller;
        _cutoverController = cutover == null ? null : new OwnedCancellation(_cutoverControllerLifetime, parent: cutoverParent);
        _stop = _cutoverController?.Source ?? new CancellationTokenSource(Web6CandidatePairBudgets.ControllerLifetime(candidatePair));
        _connectionString = connectionString;
        _web5 = web5;
        _web6 = web6;
        _runtimeUser = runtimeUser;
        _candidateMismatch = candidateMismatch;
    }
    public Guid[] Epochs => _epochs.ToArray();
    public bool NegativeVerified => _negativeSequence.Complete && _negativeIdentities.Count == 6;
    public Web5NegativePilot.Identity[] NegativeIdentities => _negativeIdentities.ToArray();
    public bool OriginalFirstVerified => _raceSequence.Complete && _races.Count == 6;
    public Guid[] OriginalFirstKeys => _races.Select(entry => entry.Key).ToArray();
    public string Redact(string output) => output.Replace(_token, "<redacted>");
    public JObject Manifest => new JObject
    {
        ["version"] = 1, ["deploymentEpoch"] = _epochs[0].ToString("D"),
        ["controlPipe"] = @"\\.\pipe\" + _name, ["controlToken"] = _token,
    };

    public static async Task<WebRecoveryPilotControl> Start(string connectionString, bool web5 = false)
    {
        var control = new WebRecoveryPilotControl(connectionString, web5);
        using (var sql = new SqlConnection(connectionString))
            control._epochs.Add(await sql.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;"));
        control._serving = control.Serve();
        return control;
    }

    public static async Task<WebRecoveryPilotControl> StartCandidate(string connectionString, string runtimeUser, Web6CandidateHostMismatch candidateMismatch, bool candidatePair = false)
    {
        if (string.IsNullOrEmpty(runtimeUser) || !runtimeUser.All(character => char.IsLetterOrDigit(character) || character == '_'))
            throw new ArgumentException("Candidate runtime identity is invalid.", nameof(runtimeUser));
        if (candidateMismatch == null) throw new ArgumentNullException(nameof(candidateMismatch));
        var control = new WebRecoveryPilotControl(connectionString, web5: true, web6: true, runtimeUser: runtimeUser, candidateMismatch: candidateMismatch, candidatePair: candidatePair);
        using (var sql = new SqlConnection(connectionString))
            control._epochs.Add(await sql.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;"));
        control._serving = control.Serve();
        return control;
    }

    internal static WebRecoveryPilotControl StartCutover(Guid epoch, Func<Web6CutoverRequest, Web6CutoverDeadline, CancellationToken, Task<JObject>> handler) =>
        StartCutover(epoch, handler, closeResponse: null);

    internal static WebRecoveryPilotControl StartCutover(Guid epoch, Func<Web6CutoverRequest, Web6CutoverDeadline, CancellationToken, Task<JObject>> handler,
        Action<NamedPipeServerStream>? closeResponse, TimeSpan? controllerLifetime = null, CancellationToken parent = default)
    {
        var control = new WebRecoveryPilotControl("", web5: true, web6: true,
            cutover: handler ?? throw new ArgumentNullException(nameof(handler)), closeCutoverResponse: closeResponse,
            cutoverControllerLifetime: controllerLifetime, cutoverParent: parent);
        control._epochs.Add(epoch);
        control._serving = control.Serve();
        return control;
    }

    public static async Task<WebRecoveryPilotControl> StartOriginalFirst(SqlTestDatabase database, string parent)
    {
        var control = new WebRecoveryPilotControl(database.ConnectionString, true);
        control._database = database;
        control._raceManifest = Web5OriginalRaceManifest.Create(parent);
        using (var sql = new SqlConnection(database.ConnectionString))
            control._epochs.Add(await sql.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;"));
        control._serving = control.Serve();
        return control;
    }

    public async Task Stop(bool browserDrained = true)
    {
        if (_cutover != null)
        {
            await StopCutover(browserDrained).ConfigureAwait(false);
            return;
        }
        if (_stopped) return;
        _stopped = true; _stop.Cancel();
        var failures = new List<Exception>();
        try { await _serving.ConfigureAwait(false); } catch (Exception error) { failures.Add(error); }
        if (_web6 && ((_candidateMismatch?.IsArmed ?? false) || _eligibilityUserDisabled || _eligibilityJournalDenied))
        {
            try
            {
                using var sql = new SqlConnection(_connectionString);
                await sql.OpenAsync().ConfigureAwait(false);
                if (_candidateMismatch?.IsArmed == true) await _candidateMismatch.RestoreIfNeededAsync().ConfigureAwait(false);
                if (_eligibilityUserDisabled)
                    await sql.ExecuteAsync("UPDATE dbo.Users SET IsActive=1 WHERE Username=N'integratedEditor' AND IsActive=0;").ConfigureAwait(false);
                if (_eligibilityJournalDenied && _runtimeUser != null)
                    await sql.ExecuteAsync("REVOKE SELECT ON dbo.WebOperationJournal TO [" + _runtimeUser + "];").ConfigureAwait(false);
                _eligibilityUserDisabled = false; _eligibilityJournalDenied = false;
            }
            catch (Exception error) { failures.Add(error); }
        }
        if (_raceManifest != null)
        {
            var cleanup = Enumerable.Reverse(_races).Select(entry => (Func<Task>)(async () =>
            {
                try { Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.CleanupStarted); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)); await entry.Race.CleanupAfterBrowserDrain(timeout.Token); _raceManifest.MarkClean(entry.Key); Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.CleanupCompleted); }
                catch { Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.CleanupFailed); throw; }
            }));
            var cleanupFailures = await Web5OriginalFirstCleanup.Run(browserDrained, GuiTestSafety.IsPoisoned, cleanup, _raceManifest.Complete);
            if (cleanupFailures.Count != 0) GuiTestSafety.Poison(new AggregateException("Original-first fixture quarantined.", cleanupFailures));
            failures.AddRange(cleanupFailures);
        }
        if (failures.Count != 0) throw new AggregateException("Recovery fixture drain failed.", failures);
    }

    internal bool CutoverJoined
    {
        get { lock (_cutoverDrainLock) return _serving.IsCompleted && (_cutoverController?.CallbacksJoined ?? true); }
    }

    internal bool CutoverJoinUncertain
    {
        get { lock (_cutoverDrainLock) return _cutoverJoinUncertain; }
    }

    private async Task StopCutover(bool browserDrained)
    {
        var controller = _cutoverController!;
        controller.RequestCancellation();
        if (!browserDrained) RecordUncertainDrain(unjoined: false);

        using var wait = new CancellationTokenSource();
        var timeout = Task.Delay(Web6CutoverBudgets.ControllerJoin, wait.Token);
        if (await Task.WhenAny(_serving, timeout).ConfigureAwait(false) != _serving ||
            await Task.WhenAny(controller.Completion, timeout).ConfigureAwait(false) != controller.Completion)
            RecordUncertainDrain(unjoined: true);
        wait.Cancel();

        var serveFailure = _serving.Exception;
        var failures = new List<Exception>();
        lock (_cutoverDrainLock)
        {
            if (_cutoverPrimary != null) failures.Add(_cutoverPrimary);
            else if (serveFailure != null) failures.AddRange(serveFailure.InnerExceptions);
            else if (_serving.IsCanceled) failures.Add(CutoverDeadline("controller-cancelled"));
            if (_cutoverPhase != null) failures.AddRange(_cutoverPhase.Failures);
            failures.AddRange(controller.Failures);
            if (_cutoverDrainFailure != null) failures.Add(_cutoverDrainFailure);
        }
        if (failures.Count != 0) throw new AggregateException("Cutover controller drain failed.", failures);
    }

    private void RecordUncertainDrain(bool unjoined)
    {
        lock (_cutoverDrainLock)
        {
            _cutoverJoinUncertain |= unjoined;
            _cutoverDrainFailure ??= new IOException("WEB6 cutover drain-uncertain.");
        }
    }

    private TimeSpan CutoverControllerRemaining => _cutoverControllerLifetime - _cutoverLifetime!.Elapsed;

    private async Task ServeCutover(NamedPipeServerStream pipe, JObject frame, Stopwatch elapsed, TimeSpan controllerRemaining)
    {
        var request = Web6CutoverControl.Validate(frame, _token);
        var allowance = Web6CutoverBudgets.PhaseAllowance(request.RequestAllowance, controllerRemaining);
        var workAllowance = Web6CutoverBudgets.PhaseAllowance(request.WorkAllowance, controllerRemaining - Web6CutoverBudgets.ResponseReserve);
        var workRemaining = workAllowance - elapsed.Elapsed;
        if (_stop.IsCancellationRequested || CutoverControllerRemaining <= Web6CutoverBudgets.ResponseReserve)
            throw CutoverDeadline("controller-cancelled");
        if (workRemaining <= TimeSpan.Zero) throw CutoverDeadline("action-deadline");

        var workDeadline = new Web6CutoverDeadline(workRemaining);
        var reply = await RunCutoverPhase(workRemaining, pipe.Dispose, "action-deadline",
            token => _cutover!(request, workDeadline, token)).ConfigureAwait(false);
        if (_stop.IsCancellationRequested || CutoverControllerRemaining <= TimeSpan.Zero) throw CutoverDeadline("controller-cancelled");
        if (elapsed.Elapsed >= workAllowance) throw CutoverDeadline("action-deadline");

        var responseRemaining = Web6CutoverBudgets.PhaseAllowance(Web6CutoverBudgets.ResponseReserve, allowance - elapsed.Elapsed);
        if (responseRemaining <= TimeSpan.Zero) throw CutoverDeadline("response-timeout");
        await RunCutoverPhase(responseRemaining, _closeCutoverResponse == null ? (Action)pipe.Dispose : () => _closeCutoverResponse(pipe),
            "response-timeout", async token =>
            {
                reply["id"] = frame["id"]!.DeepClone();
                reply["ok"] = true;
                await WebFrontendPortBridge.WriteFrame(pipe, reply, token).ConfigureAwait(false);
                return true;
            }).ConfigureAwait(false);
    }

    private async Task<T> RunCutoverPhase<T>(TimeSpan allowance, Action closePipe, string deadline, Func<CancellationToken, Task<T>> operation)
    {
        var phase = new OwnedCancellation(allowance, closePipe);
        lock (_cutoverDrainLock) _cutoverPhase = phase;
        using var parent = _stop.Token.Register(phase.RequestCancellation);
        Exception? primary = null;
        T result = default!;
        try
        {
            _stop.Token.ThrowIfCancellationRequested();
            phase.Token.ThrowIfCancellationRequested();
            result = await operation(phase.Token).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            primary = error;
        }
        // If the monotonic deadline won before disarm, run its owned cancellation even if the timer was delayed.
        phase.Disarm();
        if (phase.CancellationRequested || _stop.IsCancellationRequested)
        {
            if (primary == null || primary is OperationCanceledException ||
                (deadline == "response-timeout" && (primary is ObjectDisposedException || primary is IOException)))
                primary = CutoverDeadline(_stop.IsCancellationRequested ? "controller-cancelled" : deadline);
        }

        // Publish a completed operation's failure even if cancellation callbacks still hold the join.
        if (primary != null) lock (_cutoverDrainLock) _cutoverPrimary = primary;
        await phase.Completion.ConfigureAwait(false);
        parent.Dispose();
        phase.Source.Dispose();
        if (primary != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        if (phase.Failures.Count != 0) throw new IOException("WEB6 cutover cancellation callback failed.");
        lock (_cutoverDrainLock) _cutoverPhase = null;
        return result;
    }

    private static TimeoutException CutoverDeadline(string category) => new TimeoutException("WEB6 cutover " + category + ".");

    private async Task Serve()
    {
        var cutoverDispatched = false;
        try
        {
            for (var count = 0; count < (_web6 || _raceManifest != null ? 64 : 32) && !_stop.IsCancellationRequested; count++)
            {
                if (_cutover != null && CutoverControllerRemaining <= TimeSpan.Zero)
                    throw CutoverDeadline("controller-cancelled");
                using var identity = WindowsIdentity.GetCurrent();
                var security = new PipeSecurity();
                security.SetAccessRuleProtection(true, false);
                security.AddAccessRule(new PipeAccessRule(identity.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
                using var pipe = new NamedPipeServerStream(_name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous, 4096, 4096, security, HandleInheritability.None);
                JObject frame;
                using (_stop.Token.Register(pipe.Dispose))
                {
                    await pipe.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                    frame = await WebFrontendPortBridge.ReadFrame(pipe, _stop.Token).ConfigureAwait(false);
                }
                if (_cutover != null)
                {
                    cutoverDispatched = true;
                    var controllerRemaining = CutoverControllerRemaining;
                    await ServeCutover(pipe, frame, Stopwatch.StartNew(), controllerRemaining).ConfigureAwait(false);
                    cutoverDispatched = false;
                    continue;
                }
                using var close = _stop.Token.Register(pipe.Dispose);
                var action = Validate(frame, _token, _web6);
                using var sql = new SqlConnection(_connectionString);
                await sql.OpenAsync(_stop.Token).ConfigureAwait(false);
                var reply = new JObject { ["id"] = frame["id"]!.DeepClone(), ["ok"] = true };
                if (action == "verifyNotObserved" || action == "verifyCommitted" || action == "verifyOneEffect")
                {
                    if (!_web6) throw new InvalidDataException("Candidate control is not enabled.");
                    var frozen = (JObject)frame["operation"]!;
                    var actor = await sql.QuerySingleAsync<int>(new SqlTestCommand("SELECT UserId FROM dbo.Users WHERE Username=N'integratedEditor';", cancellationToken: _stop.Token));
                    var epoch = await sql.QuerySingleAsync<Guid>(new SqlTestCommand("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;", cancellationToken: _stop.Token));
                    var candidateIntent = Web5NegativePilot.Validate(frozen, actor, action == "verifyNotObserved" ? epoch : _epochs[0]);
                    var operationId = Guid.ParseExact((string)frame["operationId"]!, "D");
                    if (candidateIntent.Key != operationId) throw new InvalidDataException("Candidate operation identity changed.");
                    var journal = await sql.QuerySingleAsync<int>(new SqlTestCommand(
                        "SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@operation;",
                        new { actor, operation = operationId }, cancellationToken: _stop.Token));
                    if (action == "verifyNotObserved")
                    {
                        if (journal != 0 || _web6Operations.ContainsKey(operationId)) throw new InvalidDataException("Candidate pre-observation cut is not empty.");
                        _web6Operations.Add(operationId, candidateIntent);
                    }
                    else if (action == "verifyCommitted")
                    {
                        _web6Operations.TryGetValue(operationId, out var previous);
                        ValidateCommittedCandidate(false, previous?.Kind, candidateIntent.Kind, journal);
                        if (!_web6Operations.ContainsKey(operationId)) _web6Operations.Add(operationId, candidateIntent);
                        await Web6CandidateOracle.VerifyOneEffectAsync(sql, operationId, _stop.Token);
                    }
                    else
                    {
                        _web6Operations.TryGetValue(operationId, out var expected);
                        ValidateCommittedCandidate(true, expected?.Kind, candidateIntent.Kind, journal);
                        await Web6CandidateOracle.VerifyOneEffectAsync(sql, operationId, _stop.Token);
                    }
                }
                else if (action == "candidateMismatchOn" || action == "candidateMismatchOff")
                {
                    if (!_web6 || _candidateMismatch == null) throw new InvalidDataException("Candidate control is not enabled.");
                    if (action == "candidateMismatchOn") await _candidateMismatch.ArmAsync().ConfigureAwait(false);
                    else await _candidateMismatch.RestoreAsync().ConfigureAwait(false);
                }
                else if (action == "eligibility401" || action == "eligibility500" || action == "eligibilityNormal")
                {
                    if (!_web6 || _runtimeUser == null) throw new InvalidDataException("Candidate control is not enabled.");
                    if (action == "eligibility401")
                    {
                        if (_eligibilityUserDisabled || await sql.ExecuteAsync(new SqlTestCommand(
                            "UPDATE dbo.Users SET IsActive=0 WHERE Username=N'integratedEditor' AND IsActive=1;", cancellationToken: _stop.Token)) != 1)
                            throw new InvalidDataException("Candidate user state changed.");
                        _eligibilityUserDisabled = true;
                    }
                    else if (action == "eligibility500")
                    {
                        if (_eligibilityJournalDenied) throw new InvalidDataException("Candidate SQL failure is already armed.");
                        await sql.ExecuteAsync(new SqlTestCommand("DENY SELECT ON dbo.WebOperationJournal TO [" + _runtimeUser + "];", cancellationToken: _stop.Token));
                        _eligibilityJournalDenied = true;
                    }
                    else
                    {
                        if (_eligibilityUserDisabled)
                        {
                            if (await sql.ExecuteAsync(new SqlTestCommand("UPDATE dbo.Users SET IsActive=1 WHERE Username=N'integratedEditor' AND IsActive=0;", cancellationToken: _stop.Token)) != 1)
                                throw new InvalidDataException("Candidate user state could not be restored.");
                            _eligibilityUserDisabled = false;
                        }
                        if (_eligibilityJournalDenied)
                        {
                            await sql.ExecuteAsync(new SqlTestCommand("REVOKE SELECT ON dbo.WebOperationJournal TO [" + _runtimeUser + "];", cancellationToken: _stop.Token));
                            _eligibilityJournalDenied = false;
                        }
                    }
                }
                else if (action == "rotateEpoch")
                {
                    if (_epochs.Count != 1) throw new InvalidDataException("Recovery fixture permits one epoch rotation.");
                    var next = Guid.NewGuid();
                    var changed = await sql.ExecuteAsync(new SqlTestCommand(
                        "UPDATE dbo.WebDeploymentState SET DeploymentEpoch=@next,RotatedAtUtc=SYSUTCDATETIME() WHERE Id=1 AND DeploymentEpoch=@previous;",
                        new { next, previous = _epochs[0] }, cancellationToken: _stop.Token));
                    if (changed != 1) throw new InvalidDataException("Recovery fixture epoch ownership changed.");
                    _epochs.Add(next);
                    reply["deploymentEpoch"] = next.ToString("D");
                }
                else if (action == "resolveNegative")
                {
                    if (!_web5) throw new InvalidDataException("Negative fixture requires WEB5.");
                    var frozen = (JObject)frame["operation"]!;
                    var actor = await sql.QuerySingleAsync<int>(new SqlTestCommand("SELECT UserId FROM dbo.Users WHERE Username=N'integratedEditor';", cancellationToken: _stop.Token));
                    var epoch = await sql.QuerySingleAsync<Guid>(new SqlTestCommand("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;", cancellationToken: _stop.Token));
                    var intent = Web5NegativePilot.Validate(frozen, actor, epoch);
                    _negativeSequence.Begin(intent.Key, intent.Kind);
                    _negativeSnapshot = await Web5NegativePilot.Snapshot(_connectionString);
                    _pendingNegative = await Web5NegativePilot.Record(_connectionString, frozen, _stop.Token);
                }
                else if (action == "verifyNegative")
                {
                    var id = Guid.ParseExact((string)frame["operationId"]!, "D");
                    if (!_web5 || _pendingNegative == null || _pendingNegative.Key != id || _negativeSnapshot == null ||
                        _negativeSnapshot != await Web5NegativePilot.Snapshot(_connectionString))
                        throw new InvalidDataException("Late original changed physical state or operation identity.");
                    await Web5NegativePilot.VerifyDurable(sql, _pendingNegative, _stop.Token);
                    _negativeSequence.Verify(id); _negativeIdentities.Add(_pendingNegative); _pendingNegative = null; _negativeSnapshot = null;
                }
                else if (action == "armOriginal")
                {
                    Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.ArmStarted);
                    if (_database == null || _raceManifest == null || _pendingRace.HasValue) throw new InvalidDataException("Original-first fixture not available.");
                    var frozen = (JObject)frame["operation"]!;
                    var actor = await sql.QuerySingleAsync<int>(new SqlTestCommand("SELECT UserId FROM dbo.Users WHERE Username=N'integratedEditor';", cancellationToken: _stop.Token));
                    var epoch = await sql.QuerySingleAsync<Guid>(new SqlTestCommand("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;", cancellationToken: _stop.Token));
                    var intent = Web5NegativePilot.Validate(frozen, actor, epoch);
                    _raceSequence.Begin(intent.Key, intent.Kind);
                    var race = await Web5OriginalFirstRace.Arm(_database, frozen, _raceManifest.Record, _stop.Token);
                    _races.Add((intent.Key, race)); _pendingRace = intent.Key;
                    Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.ArmCompleted);
                }
                else if (action == "waitOriginal" || action == "releaseOriginal")
                {
                    var id = Guid.ParseExact((string)frame["operationId"]!, "D");
                    if (!_pendingRace.HasValue || _pendingRace.Value != id || _races.Count == 0) throw new InvalidDataException("Original-first operation mismatch.");
                    var race = _races[_races.Count - 1].Race;
                    if (action == "waitOriginal") { Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.WaitStarted); await race.ObserveWriterAndOperator(id, _stop.Token); Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.WaitCompleted); }
                    else { Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.ReleaseStarted); await race.Release(id, _stop.Token); _raceSequence.Verify(id); _pendingRace = null; Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.ReleaseCompleted); }
                }
                else
                {
                    var changed = await sql.ExecuteAsync(new SqlTestCommand(
                        _web6
                            ? "UPDATE p SET Granted=@granted FROM dbo.UserPermissions p JOIN dbo.Users u ON u.UserId=p.UserId WHERE u.Username=N'integratedEditor' AND p.PermissionKey IN (N'Sales.Receipts.Write',N'Inventory.GoodsReceipts.Write');"
                            : "UPDATE p SET Granted=@granted FROM dbo.UserPermissions p JOIN dbo.Users u ON u.UserId=p.UserId WHERE u.Username=N'integratedEditor' AND p.PermissionKey=N'Sales.Receipts.Write';",
                        new { granted = (bool)frame["granted"]! }, cancellationToken: _stop.Token));
                    if (changed != (_web6 ? 2 : 1)) throw new InvalidDataException("Recovery fixture permission owner missing.");
                }
                await WebFrontendPortBridge.WriteFrame(pipe, reply, _stop.Token).ConfigureAwait(false);
                if (action == "armOriginal") Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.ArmReplySent);
                else if (action == "waitOriginal") Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.WaitReplySent);
                else if (action == "releaseOriginal") Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.ReleaseReplySent);
            }
            if (!_stop.IsCancellationRequested) throw new InvalidDataException("Recovery fixture control request budget exceeded.");
        }
        catch (Exception error) when (!cutoverDispatched && _stop.IsCancellationRequested && (error is OperationCanceledException || error is ObjectDisposedException || error is IOException)) { }
        catch { if (_raceManifest != null) Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.ControlFailed); throw; }
    }

    private sealed class OwnedCancellation
    {
        private readonly object _sync = new object();
        private readonly List<Exception> _callbackFailures = new List<Exception>();
        private readonly List<Exception> _closeFailures = new List<Exception>();
        private readonly TaskCompletionSource<bool> _stopTimer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Stopwatch _elapsed = Stopwatch.StartNew();
        private readonly TimeSpan _allowance;
        private readonly Action? _close;
        private readonly CancellationToken _parent;
        private bool _disarmed;
        private Task? _cancellation;

        internal OwnedCancellation(TimeSpan allowance, Action? close = null, CancellationToken parent = default)
        {
            _allowance = allowance;
            _close = close;
            _parent = parent;
            Completion = Run();
        }

        internal CancellationTokenSource Source { get; } = new CancellationTokenSource();
        internal CancellationToken Token => Source.Token;
        internal Task Completion { get; }
        internal bool CancellationRequested { get { lock (_sync) return _cancellation != null; } }
        internal bool CallbacksJoined { get { lock (_sync) return _cancellation == null || Completion.IsCompleted; } }
        internal IReadOnlyList<Exception> Failures { get { lock (_sync) return _callbackFailures.Concat(_closeFailures).ToArray(); } }

        internal void RequestCancellation()
        {
            lock (_sync)
            {
                if (!_disarmed && _cancellation == null)
                {
                    _cancellation = Task.Run(Cancel);
                    _stopTimer.TrySetResult(true);
                }
            }
        }

        internal void Disarm()
        {
            lock (_sync)
            {
                if (_elapsed.Elapsed >= _allowance) RequestCancellation();
                _disarmed = true;
                _stopTimer.TrySetResult(true);
            }
        }

        private async Task Run()
        {
            // Parent callbacks only schedule cancellation; the owned Completion joins
            // all handler callbacks, including noncooperative ones, through Stop.
            using var parentRegistration = _parent.Register(RequestCancellation);
            using var timerStop = new CancellationTokenSource();
            var timer = Task.Delay(_allowance, timerStop.Token);
            if (await Task.WhenAny(timer, _stopTimer.Task).ConfigureAwait(false) == timer) RequestCancellation();
            timerStop.Cancel();
            Task? cancellation;
            lock (_sync) cancellation = _cancellation;
            if (cancellation != null) await cancellation.ConfigureAwait(false);
        }

        private void Cancel()
        {
            // Closing is the first cancellation callback, including when an operation callback blocks.
            using var close = _close == null ? default : Source.Token.Register(() => Record(_closeFailures, _close));
            try { Source.Cancel(); }
            catch (AggregateException failure) { lock (_sync) _callbackFailures.AddRange(failure.InnerExceptions); }
        }

        private void Record(List<Exception> failures, Action action)
        {
            try { action(); }
            catch (Exception failure) { lock (_sync) failures.Add(failure); }
        }
    }

    internal static void ValidateCommittedCandidate(bool requireObserved, string? expectedKind, string candidateKind, int journal)
    {
        // Static categories only: never copy keys, kinds, row counts, SQL or
        // underlying exceptions into the browser/control-owner diagnostic.
        if (requireObserved && expectedKind == null)
            throw new InvalidDataException("WEB6 candidate-commit expected-operation-absent");
        if (expectedKind != null && expectedKind != candidateKind)
            throw new InvalidDataException("WEB6 candidate-commit operation-kind-mismatch");
        if (journal != 1)
            throw new InvalidDataException("WEB6 candidate-commit journal-not-single");
    }

    internal static string Validate(JObject frame, string token, bool web6 = false)
    {
        var action = frame["action"]?.Type == JTokenType.String ? (string)frame["action"]! : "";
        var web6Action = action == "verifyNotObserved" || action == "verifyCommitted" || action == "verifyOneEffect" ||
            action == "candidateMismatchOn" || action == "candidateMismatchOff" || action == "eligibility401" ||
            action == "eligibility500" || action == "eligibilityNormal";
        var keys = web6Action ? new[] { "id", "token", "action", "operation", "operationId" } :
            action == "setWritePermission" ? new[] { "id", "token", "action", "granted" } :
            action == "resolveNegative" || action == "armOriginal" ? new[] { "id", "token", "action", "operation" } :
            action == "verifyNegative" || action == "waitOriginal" || action == "releaseOriginal" ? new[] { "id", "token", "action", "operationId" } :
            new[] { "id", "token", "action" };
        if (!frame.Properties().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(keys.OrderBy(x => x, StringComparer.Ordinal)) ||
            frame["id"]?.Type != JTokenType.Integer || (long)frame["id"]! <= 0 ||
            frame["token"]?.Type != JTokenType.String || (string)frame["token"]! != token ||
            (action != "rotateEpoch" && action != "setWritePermission" && action != "resolveNegative" && action != "verifyNegative" && action != "armOriginal" && action != "waitOriginal" && action != "releaseOriginal" && !(web6 && web6Action)) ||
            ((action == "resolveNegative" || action == "armOriginal") && frame["operation"] is not JObject) ||
            ((action == "verifyNegative" || action == "waitOriginal" || action == "releaseOriginal") && (frame["operationId"]?.Type != JTokenType.String || !Guid.TryParseExact((string?)frame["operationId"], "D", out var operationId) || operationId.ToString("D") != (string?)frame["operationId"])) ||
            (web6Action && (frame["operation"] is not JObject || frame["operationId"]?.Type != JTokenType.String ||
                !Guid.TryParseExact((string?)frame["operationId"], "D", out var web6OperationId) || web6OperationId.ToString("D") != (string?)frame["operationId"])) ||
            (action == "setWritePermission" && frame["granted"]?.Type != JTokenType.Boolean))
            throw new InvalidDataException("Invalid recovery fixture control request.");
        return action;
    }
}

internal sealed class Web6CandidateHostMismatch
{
    private readonly Func<string, Task> _switchHost;
    internal Web6CandidateHostMismatch(string expectedCandidateId, Func<string, Task> switchHost)
    {
        if (string.IsNullOrWhiteSpace(expectedCandidateId) || !System.Text.RegularExpressions.Regex.IsMatch(expectedCandidateId, @"\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z"))
            throw new ArgumentException("Candidate identity is required.", nameof(expectedCandidateId));
        ExpectedCandidateId = expectedCandidateId;
        MismatchCandidateId = expectedCandidateId == "web6-mismatch" ? "web6-mismatch-2" : "web6-mismatch";
        _switchHost = switchHost ?? throw new ArgumentNullException(nameof(switchHost));
    }
    internal string ExpectedCandidateId { get; }
    internal string MismatchCandidateId { get; }
    internal bool IsArmed { get; private set; }
    internal async Task ArmAsync()
    {
        if (IsArmed) throw new InvalidOperationException("Candidate mismatch is already armed.");
        // A failed restart is ambiguous: cleanup must restore the exact admitted pair.
        IsArmed = true;
        await _switchHost(MismatchCandidateId).ConfigureAwait(false);
    }
    internal async Task RestoreAsync()
    {
        if (!IsArmed) throw new InvalidOperationException("Candidate mismatch is not armed.");
        await _switchHost(ExpectedCandidateId).ConfigureAwait(false);
        IsArmed = false;
    }
    internal Task RestoreIfNeededAsync() => IsArmed ? RestoreAsync() : Task.CompletedTask;
}
