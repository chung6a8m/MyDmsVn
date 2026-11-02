using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Desktop.Tests;
using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reply = MyDmsVn.Server.Infrastructure.IntegrationTests.WebAuthHttpSqlTests.Reply;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Test-only supervisor. GuiOwnedProcess assigns the suspended root to a Windows Job Object
// before execution; cancellation terminates that owned tree and verifies exit/output drain.
internal sealed class WebOperationCrashProcess
{
    private static readonly TimeSpan ProtocolBudget = TimeSpan.FromSeconds(45);
    private readonly NamedPipeServerStream _pipe;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly CancellationTokenSource _stop = new CancellationTokenSource();
    private readonly Task<GuiProcessResult> _run;
    private readonly TaskCompletionSource<int> _started = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly char[] _readBuffer = new char[4096];
    private int _readOffset;
    private int _readLength;
    private bool _closed;
    private bool _responseObserved;

    private WebOperationCrashProcess(NamedPipeServerStream pipe, string executable, string pipeName)
    {
        _pipe = pipe;
        _reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, true);
        _writer = new StreamWriter(pipe, new UTF8Encoding(false, true), 4096, true);
        var start = new ProcessStartInfo(executable, pipeName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(executable)!
        };
        // Only the private pipe carries the owned database and request credentials.
        start.EnvironmentVariables.Remove("MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING");
        start.EnvironmentVariables.Remove("MYDMSVN_WEB_SQL_CONNECTION_STRING");
        _run = GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(90), _stop.Token, id => _started.SetResult(id));
    }

    public static bool IsPoisoned => GuiTestSafety.IsPoisoned;
    public int ProcessId { get; private set; }

    public static async Task<WebOperationCrashProcess> StartAsync(JObject configuration)
    {
        GuiTestSafety.ThrowIfPoisoned();
        var executable = Path.Combine(AppContext.BaseDirectory, "CrashHost", "MyDmsVn.WebOperation.CrashHost.exe");
        if (!File.Exists(executable)) throw new FileNotFoundException("Build the test-only CrashHost with the Security test project.");
        var name = "MyDmsVn-Web5-" + Guid.NewGuid().ToString("N");
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(true, false);
        using (var identity = WindowsIdentity.GetCurrent())
            security.AddAccessRule(new PipeAccessRule(identity.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous, 4096, 4096, security);
        WebOperationCrashProcess? child = null;
        try
        {
            child = new WebOperationCrashProcess(pipe, executable, name);
            await child.IoAsync(pipe.WaitForConnectionAsync()).ConfigureAwait(false);
            var ownedId = await child.IoAsync(child._started.Task).ConfigureAwait(false);
            if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var peerId) || peerId != (uint)ownedId)
                throw new InvalidOperationException("Control peer is not the owned child process; configuration was withheld.");
            child._writer.AutoFlush = true;
            await child.WriteAsync(configuration).ConfigureAwait(false);
            var ready = await child.EventAsync("ready").ConfigureAwait(false);
            child.ProcessId = (int?)ready["processId"] ?? throw new InvalidDataException("CrashHost did not identify its owned process.");
            if (child.ProcessId != ownedId) throw new InvalidDataException("CrashHost ready identity mismatched the owned root.");
            return child;
        }
        catch (Exception original)
        {
            if (child == null) pipe.Dispose();
            else
            {
                try { await child.CloseAsync().ConfigureAwait(false); }
                catch (Exception cleanup) { throw new AggregateException(original, cleanup); }
            }
            throw;
        }
    }

    public Task DispatchAsync() => WriteAsync(new JObject { ["command"] = "dispatch" });
    public Task ReleaseAsync() => WriteAsync(new JObject { ["command"] = "release" });

    public async Task<JObject> EventAsync(string expected)
    {
        var line = await IoAsync(ReadBoundedLineAsync()).ConfigureAwait(false);
        if (line == null) throw new EndOfStreamException("CrashHost closed before the expected protocol event: " + expected);
        JObject value;
        try
        {
            using var text = new StringReader(line);
            using var json = new JsonTextReader(text) { MaxDepth = 8, DateParseHandling = DateParseHandling.None };
            value = JObject.Load(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (json.Read()) throw new InvalidDataException("Trailing control data was refused.");
        }
        catch (JsonException) { throw new InvalidDataException("Malformed control JSON was refused."); }
        if ((string?)value["event"] != expected)
            throw new InvalidDataException("Unexpected CrashHost protocol event; expected " + expected + ".");
        return value;
    }

    public async Task<Reply> ReplyAsync()
    {
        var response = await EventAsync("response").ConfigureAwait(false);
        var reply = new Reply((int)response["status"]!, (string)response["body"]!, Array.Empty<string>(), new Dictionary<string, string[]>(response["headers"]!.ToObject<Dictionary<string, string[]>>()!, StringComparer.OrdinalIgnoreCase));
        _responseObserved = true;
        return reply;
    }

    public async Task KillAsync()
    {
        if (_closed) throw new InvalidOperationException("The owned process is already closed.");
        if (_responseObserved) throw new InvalidOperationException("Crash proof requires loss before response observation.");
        _stop.Cancel();
        try
        {
            await _run.ConfigureAwait(false);
            throw new InvalidOperationException("CrashHost exited before the supervisor's external kill.");
        }
        catch (GuiProcessException error) when (error.Reason == "Canceled" && error.CleanupVerified)
        {
            // This result is produced only after TerminateJobObject, root/descendant exit,
            // and both redirected streams reached EOF. No client cancellation inference.
        }
        finally
        {
            _closed = true;
            DisposeProtocol();
        }
    }

    public async Task CloseAsync()
    {
        if (_closed) return;
        try
        {
            if (!_responseObserved) _stop.Cancel();
            try
            {
                var result = await _run.ConfigureAwait(false);
                if (result.ExitCode != 0) throw new InvalidOperationException("CrashHost failed without exposing request or SQL details.");
            }
            catch (GuiProcessException error) when (!_responseObserved && error.Reason == "Canceled" && error.CleanupVerified) { }
        }
        finally
        {
            _closed = true;
            DisposeProtocol();
        }
    }

    private Task WriteAsync(JObject value) => IoAsync(_writer.WriteLineAsync(value.ToString(Formatting.None)));

    private async Task IoAsync(Task operation)
    {
        if (await Task.WhenAny(operation, Task.Delay(ProtocolBudget)).ConfigureAwait(false) != operation)
        {
            _pipe.Dispose(); // Abort the one owned I/O before reporting timeout.
            if (await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false) != operation)
            {
                var undrained = new TimeoutException("CrashHost pipe I/O did not drain; retain the owned SQL allocation.");
                GuiTestSafety.Poison(undrained);
                throw undrained;
            }
            try { await operation.ConfigureAwait(false); } catch (Exception) { }
            throw new TimeoutException("CrashHost protocol exceeded its finite budget.");
        }
        await operation.ConfigureAwait(false);
    }

    private async Task<T> IoAsync<T>(Task<T> operation)
    {
        await IoAsync((Task)operation).ConfigureAwait(false);
        return await operation.ConfigureAwait(false);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);

    private async Task<string?> ReadBoundedLineAsync()
    {
        const int maximum = 2 * 1024 * 1024;
        var line = new StringBuilder();
        while (true)
        {
            if (_readOffset == _readLength)
            {
                _readLength = await _reader.ReadAsync(_readBuffer, 0, _readBuffer.Length).ConfigureAwait(false);
                _readOffset = 0;
                if (_readLength == 0) return line.Length == 0 ? null : throw new InvalidDataException("Incomplete control frame.");
            }
            var start = _readOffset;
            while (_readOffset < _readLength && _readBuffer[_readOffset] != '\n') _readOffset++;
            var count = _readOffset - start;
            if (count > maximum - line.Length) throw new InvalidDataException("Control frame exceeded its size limit.");
            line.Append(_readBuffer, start, count);
            if (_readOffset < _readLength)
            {
                _readOffset++;
                if (line.Length > 0 && line[line.Length - 1] == '\r') line.Length--;
                return line.ToString();
            }
        }
    }
    private void DisposeProtocol()
    {
        _reader.Dispose();
        _writer.Dispose();
        _pipe.Dispose();
        _stop.Dispose();
    }
}
