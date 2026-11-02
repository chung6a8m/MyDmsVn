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
    private WebRecoveryPilotControl(string connectionString) { _connectionString = connectionString; }
    public Guid[] Epochs => _epochs.ToArray();
    public string Redact(string output) => output.Replace(_token, "<redacted>");
    public JObject Manifest => new JObject
    {
        ["version"] = 1, ["deploymentEpoch"] = _epochs[0].ToString("D"),
        ["controlPipe"] = @"\\.\pipe\" + _name, ["controlToken"] = _token,
    };

    public static async Task<WebRecoveryPilotControl> Start(string connectionString)
    {
        var control = new WebRecoveryPilotControl(connectionString);
        using (var sql = new SqlConnection(connectionString))
            control._epochs.Add(await sql.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;"));
        control._serving = control.Serve();
        return control;
    }

    public async Task Stop()
    {
        if (!_stopped) { _stopped = true; _stop.Cancel(); }
        await _serving.ConfigureAwait(false); // Drain all administrative work before SQL teardown.
    }

    private async Task Serve()
    {
        try
        {
            for (var count = 0; count < 32 && !_stop.IsCancellationRequested; count++)
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
                else
                {
                    var changed = await sql.ExecuteAsync(new SqlTestCommand(
                        "UPDATE p SET Granted=@granted FROM dbo.UserPermissions p JOIN dbo.Users u ON u.UserId=p.UserId WHERE u.Username=N'integratedEditor' AND p.PermissionKey=N'Sales.Receipts.Write';",
                        new { granted = (bool)frame["granted"]! }, cancellationToken: _stop.Token));
                    if (changed != 1) throw new InvalidDataException("Recovery fixture permission owner missing.");
                }
                await WebFrontendPortBridge.WriteFrame(pipe, reply, _stop.Token).ConfigureAwait(false);
            }
            if (!_stop.IsCancellationRequested) throw new InvalidDataException("Recovery fixture control request budget exceeded.");
        }
        catch (Exception error) when (_stop.IsCancellationRequested && (error is OperationCanceledException || error is ObjectDisposedException || error is IOException)) { }
    }

    internal static string Validate(JObject frame, string token)
    {
        var action = frame["action"]?.Type == JTokenType.String ? (string)frame["action"]! : "";
        var keys = action == "setWritePermission" ? new[] { "id", "token", "action", "granted" } : new[] { "id", "token", "action" };
        if (!frame.Properties().Select(p => p.Name).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(keys.OrderBy(x => x, StringComparer.Ordinal)) ||
            frame["id"]?.Type != JTokenType.Integer || (long)frame["id"]! <= 0 ||
            frame["token"]?.Type != JTokenType.String || (string)frame["token"]! != token ||
            (action != "rotateEpoch" && action != "setWritePermission") ||
            (action == "setWritePermission" && frame["granted"]?.Type != JTokenType.Boolean))
            throw new InvalidDataException("Invalid recovery fixture control request.");
        return action;
    }
}
