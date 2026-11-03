using System;
using System.Collections.Generic;
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
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Test-owner control channel, never an HTTP endpoint or a connection in browser code.
// One bounded request per connection lets independent Playwright cases share the fixture.
internal sealed class WebRecoveryPilotControl
{
    private readonly string _connectionString;
    private readonly string _name = "mydmsvn-web4-recovery-" + Guid.NewGuid().ToString("N");
    private readonly string _token = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _stop = new CancellationTokenSource(TimeSpan.FromMinutes(6));
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
    private WebRecoveryPilotControl(string connectionString, bool web5) { _connectionString = connectionString; _web5 = web5; }
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
        if (_stopped) return;
        _stopped = true; _stop.Cancel();
        var failures = new List<Exception>();
        try { await _serving.ConfigureAwait(false); } catch (Exception error) { failures.Add(error); }
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

    private async Task Serve()
    {
        try
        {
            for (var count = 0; count < (_raceManifest == null ? 32 : 64) && !_stop.IsCancellationRequested; count++)
            {
                using var identity = WindowsIdentity.GetCurrent();
                var security = new PipeSecurity();
                security.SetAccessRuleProtection(true, false);
                security.AddAccessRule(new PipeAccessRule(identity.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
                using var pipe = new NamedPipeServerStream(_name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous, 4096, 4096, security, HandleInheritability.None);
                using var close = _stop.Token.Register(() => pipe.Dispose());
                await pipe.WaitForConnectionAsync(_stop.Token).ConfigureAwait(false);
                var frame = await WebFrontendPortBridge.ReadFrame(pipe, _stop.Token).ConfigureAwait(false);
                var action = Validate(frame, _token);
                using var sql = new SqlConnection(_connectionString);
                await sql.OpenAsync(_stop.Token).ConfigureAwait(false);
                var reply = new JObject { ["id"] = frame["id"]!.DeepClone(), ["ok"] = true };
                if (action == "rotateEpoch")
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
                        "UPDATE p SET Granted=@granted FROM dbo.UserPermissions p JOIN dbo.Users u ON u.UserId=p.UserId WHERE u.Username=N'integratedEditor' AND p.PermissionKey=N'Sales.Receipts.Write';",
                        new { granted = (bool)frame["granted"]! }, cancellationToken: _stop.Token));
                    if (changed != 1) throw new InvalidDataException("Recovery fixture permission owner missing.");
                }
                await WebFrontendPortBridge.WriteFrame(pipe, reply, _stop.Token).ConfigureAwait(false);
                if (action == "armOriginal") Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.ArmReplySent);
                else if (action == "waitOriginal") Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.WaitReplySent);
                else if (action == "releaseOriginal") Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.ReleaseReplySent);
            }
            if (!_stop.IsCancellationRequested) throw new InvalidDataException("Recovery fixture control request budget exceeded.");
        }
        catch (Exception error) when (_stop.IsCancellationRequested && (error is OperationCanceledException || error is ObjectDisposedException || error is IOException)) { }
        catch { if (_raceManifest != null) Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.ControlFailed); throw; }
    }

    internal static string Validate(JObject frame, string token)
    {
        var action = frame["action"]?.Type == JTokenType.String ? (string)frame["action"]! : "";
        var keys = action == "setWritePermission" ? new[] { "id", "token", "action", "granted" } : action == "resolveNegative" || action == "armOriginal" ? new[] { "id", "token", "action", "operation" } : action == "verifyNegative" || action == "waitOriginal" || action == "releaseOriginal" ? new[] { "id", "token", "action", "operationId" } : new[] { "id", "token", "action" };
        if (!frame.Properties().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(keys.OrderBy(x => x, StringComparer.Ordinal)) ||
            frame["id"]?.Type != JTokenType.Integer || (long)frame["id"]! <= 0 ||
            frame["token"]?.Type != JTokenType.String || (string)frame["token"]! != token ||
            (action != "rotateEpoch" && action != "setWritePermission" && action != "resolveNegative" && action != "verifyNegative" && action != "armOriginal" && action != "waitOriginal" && action != "releaseOriginal") ||
            ((action == "resolveNegative" || action == "armOriginal") && frame["operation"] is not JObject) ||
            ((action == "verifyNegative" || action == "waitOriginal" || action == "releaseOriginal") && (frame["operationId"]?.Type != JTokenType.String || !Guid.TryParseExact((string?)frame["operationId"], "D", out var operationId) || operationId.ToString("D") != (string?)frame["operationId"])) ||
            (action == "setWritePermission" && frame["granted"]?.Type != JTokenType.Boolean))
            throw new InvalidDataException("Invalid recovery fixture control request.");
        return action;
    }
}
