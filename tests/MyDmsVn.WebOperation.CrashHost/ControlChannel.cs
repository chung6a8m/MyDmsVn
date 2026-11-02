using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.HttpHost;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.WebOperation.CrashHost;

internal sealed class ControlChannel
{
    internal const int TimeoutMilliseconds = 45000;
    private const int MaximumLineCharacters = 2 * 1024 * 1024;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly char[] _readBuffer = new char[4096];
    private int _readOffset;
    private int _readCount;

    internal ControlChannel(Stream stream)
    {
        var utf8 = new UTF8Encoding(false, true);
        _reader = new StreamReader(stream, utf8, false, 4096, true);
        _writer = new StreamWriter(stream, utf8, 4096, true) { NewLine = "\n", AutoFlush = true };
    }

    internal async Task SendAsync(object value, int timeoutMilliseconds = TimeoutMilliseconds)
    {
        await BoundedAsync(_writer.WriteLineAsync(JsonConvert.SerializeObject(value)), timeoutMilliseconds).ConfigureAwait(false);
    }

    internal async Task ExpectAsync(string command)
    {
        var value = await ReadAsync().ConfigureAwait(false);
        if (value.Count != 1 || (string?)value["command"] != command) throw new InvalidDataException();
    }

    internal async Task<CrashRequest> ReadRequestAsync()
    {
        var value = await ReadAsync().ConfigureAwait(false);
        var expected = new[] { "connectionString", "phase", "method", "path", "body", "access", "headers" };
        if (value.Count != expected.Length || expected.Any(name => value.Property(name) == null)) throw new InvalidDataException();
        var connectionString = RequiredString(value, "connectionString");
        var phase = RequiredString(value, "phase");
        var method = RequiredString(value, "method");
        var path = RequiredString(value, "path");
        if (!new[] { "none", "before-commit", "commit-window", "after-commit" }.Contains(phase) ||
            !new[] { "GET", "POST", "PUT", "OPERATOR" }.Contains(method) || path.StartsWith("/", StringComparison.Ordinal) ||
            path.Contains("\\") || path.Contains("..") || path.Contains(":") || path.Contains("#") || path.Length > 4096)
            throw new InvalidDataException();
        if (!(value["headers"] is JObject headerObject) || headerObject.Count > 64) throw new InvalidDataException();
        var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headerObject.Properties())
        {
            if (!(header.Value is JArray items) || items.Count > 16 || items.Any(item => item.Type != JTokenType.String)) throw new InvalidDataException();
            headers.Add(header.Name, items.Select(item => (string)item!).ToArray());
        }
        if (method == "OPERATOR" && (path != "operations/terminal" || headers.Count != 0 || value["access"]!.Type != JTokenType.Null || phase == "commit-window")) throw new InvalidDataException();
        var mutation = phase == "none" || method == "OPERATOR" ? null : SelectedMutation(method, path, headers);
        return new CrashRequest(connectionString, phase, method, path, NullableString(value, "body"), NullableString(value, "access"), headers, mutation);
    }

    private static CrashMutation SelectedMutation(string method, string path, Dictionary<string, string[]> headers)
    {
        if (!headers.TryGetValue("Idempotency-Key", out var keys) || keys.Length != 1 ||
            !headers.TryGetValue("X-MyDmsVn-Deployment-Epoch", out var epochs) || epochs.Length != 1)
            throw new InvalidDataException();
        long? target = null;
        var parts = path.Split('/');
        if (parts.Length == 3 && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) &&
            parsed > 0 && parsed.ToString(CultureInfo.InvariantCulture) == parts[1]) target = parsed;
        WebOperationKind? selected = null;
        foreach (WebOperationKind kind in Enum.GetValues(typeof(WebOperationKind)))
        {
            var create = kind == WebOperationKind.SalesCreate || kind == WebOperationKind.ReceiptCreate;
            if (!create && !target.HasValue) continue;
            // The production contract owns the six exact method/route combinations.
            var route = WebOperationWireInput.RouteScope(kind).Replace("{saleId}", target?.ToString(CultureInfo.InvariantCulture))
                .Replace("{receiptId}", target?.ToString(CultureInfo.InvariantCulture));
            if (method == WebOperationWireInput.Method(kind) && "/api/v1/" + path == route) selected = kind;
        }
        if (!selected.HasValue) throw new InvalidDataException();
        try
        {
            using var request = new HttpRequestMessage();
            request.Headers.TryAddWithoutValidation("X-MyDmsVn-Deployment-Epoch", epochs);
            return new CrashMutation(WebOperationWireInput.OperationId(keys[0]), WebOperationWireInput.DeploymentEpoch(request), selected.Value, target);
        }
        catch { throw new InvalidDataException(); }
    }

    private async Task<JObject> ReadAsync()
    {
        // One deadline covers the complete line, including every partial pipe read.
        var line = await BoundedAsync(ReadBoundedLineAsync()).ConfigureAwait(false);
        if (line.Length == 0) throw new InvalidDataException();
        using var text = new StringReader(line);
        using var reader = new JsonTextReader(text) { MaxDepth = 16, DateParseHandling = DateParseHandling.None };
        var value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (reader.Read()) throw new InvalidDataException();
        return value;
    }

    private async Task<string> ReadBoundedLineAsync()
    {
        var line = new StringBuilder(_readBuffer.Length, MaximumLineCharacters);
        while (true)
        {
            if (_readOffset == _readCount)
            {
                _readCount = await _reader.ReadAsync(_readBuffer, 0, _readBuffer.Length).ConfigureAwait(false);
                _readOffset = 0;
                if (_readCount == 0) throw new InvalidDataException();
            }
            var newline = Array.IndexOf(_readBuffer, '\n', _readOffset, _readCount - _readOffset);
            var end = newline < 0 ? _readCount : newline;
            var count = end - _readOffset;
            // Refuse an oversized unterminated line before retaining another chunk.
            if (count > MaximumLineCharacters - line.Length) throw new InvalidDataException();
            line.Append(_readBuffer, _readOffset, count);
            _readOffset = end;
            if (newline < 0) continue;
            _readOffset++;
            // Retain any following line in the fixed buffer for the next protocol read.
            if (line.Length > 0 && line[line.Length - 1] == '\r') line.Length--;
            return line.ToString();
        }
    }

    private static string RequiredString(JObject value, string name)
    {
        var result = NullableString(value, name);
        return string.IsNullOrWhiteSpace(result) ? throw new InvalidDataException() : result!;
    }

    private static string? NullableString(JObject value, string name)
    {
        var token = value[name];
        if (token == null || token.Type != JTokenType.String && token.Type != JTokenType.Null) throw new InvalidDataException();
        return (string?)token;
    }

    internal static async Task BoundedAsync(Task task, int timeoutMilliseconds = TimeoutMilliseconds)
    {
        if (await Task.WhenAny(task, Task.Delay(timeoutMilliseconds)).ConfigureAwait(false) != task) throw new TimeoutException();
        await task.ConfigureAwait(false);
    }

    internal static async Task<T> BoundedAsync<T>(Task<T> task, int timeoutMilliseconds = TimeoutMilliseconds)
    {
        await BoundedAsync((Task)task, timeoutMilliseconds).ConfigureAwait(false);
        return await task.ConfigureAwait(false);
    }
}

internal sealed class CrashRequest
{
    internal CrashRequest(string connectionString, string phase, string method, string path, string? body, string? access, Dictionary<string, string[]> headers, CrashMutation? mutation)
    { ConnectionString = connectionString; Phase = phase; Method = method; Path = path; Body = body; Access = access; Headers = headers; Mutation = mutation; }
    internal string ConnectionString { get; }
    internal string Phase { get; }
    internal string Method { get; }
    internal string Path { get; }
    internal string? Body { get; }
    internal string? Access { get; }
    internal Dictionary<string, string[]> Headers { get; }
    internal CrashMutation? Mutation { get; }
}

internal sealed class CrashMutation
{
    internal CrashMutation(Guid operationId, Guid deploymentEpoch, WebOperationKind kind, long? requestTargetId)
    { OperationId = operationId; DeploymentEpoch = deploymentEpoch; Kind = kind; RequestTargetId = requestTargetId; }
    internal Guid OperationId { get; }
    internal Guid DeploymentEpoch { get; }
    internal WebOperationKind Kind { get; }
    internal long? RequestTargetId { get; }
}
