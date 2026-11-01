using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Desktop.Tests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using HttpScenario = MyDmsVn.Server.Infrastructure.IntegrationTests.WebAuthHttpSqlTests.Scenario;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// F03 port integration only: real production TypeScript ports -> this byte-preserving
// fetch boundary -> actual OWIN/SQL. It is not an IIS, browser, TLS or crash harness.
internal static class WebFrontendPortBridge
{
    internal const string Origin = "https://web2.mydmsvn.test";
    internal const string CheckoutVariable = "MYDMSVN_WEB4_PORT_FRONTEND_DIRECTORY";
    internal const int MaximumFrameBytes = 1048576;
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
    private static readonly TimeSpan Budget = TimeSpan.FromMinutes(4);

    internal sealed class Observation
    {
        public Dictionary<string, int> MutationDispatches { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
        public int StatusReads { get; set; }
        public int DeniedDocumentReads { get; set; }
        public int Restarts { get; set; }
        public int CompletedTests { get; set; }
        public int Requests { get; set; }
    }

    public static async Task<Observation> RunAsync(HttpScenario scenario, JObject bootstrap)
    {
        var checkout = Path.GetFullPath(Environment.GetEnvironmentVariable(CheckoutVariable)!);
        var node = Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_NODE")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe");
        var cli = Path.Combine(checkout, "node_modules", "vitest", "vitest.mjs");
        foreach (var file in new[] { node, cli, Path.Combine(checkout, "vitest.web4.integration.config.ts"), Path.Combine(checkout, "tests", "integration", "web4-ports.test.ts") })
            if (!Path.IsPathRooted(file) || !File.Exists(file)) throw new InvalidOperationException("The explicitly selected frontend port test checkout or Node installation is incomplete.");

        var name = "mydmsvn-web4-ports-" + Guid.NewGuid().ToString("N");
        using var identity = WindowsIdentity.GetCurrent();
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new PipeAccessRule(identity.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        using var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous, 65536, 65536, security, HandleInheritability.None);
        using var cancellation = new CancellationTokenSource(Budget);
        // Disposing the async pipe also releases pending net48 reads/connects. An active
        // OWIN request is separately awaited below before this method permits SQL disposal.
        using var close = cancellation.Token.Register(() => pipe.Dispose());
        var start = new ProcessStartInfo(node, "\"" + cli + "\" run --config vitest.web4.integration.config.ts")
        { WorkingDirectory = checkout, UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var variable in start.EnvironmentVariables.Keys.Cast<string>().Where(value => value.StartsWith("MYDMSVN_", StringComparison.OrdinalIgnoreCase)).ToArray())
            start.EnvironmentVariables.Remove(variable);
        start.EnvironmentVariables["MYDMSVN_WEB4_PORT_PIPE"] = @"\\.\pipe\" + name;
        var observation = new Observation();
        var serving = Serve(pipe, scenario, bootstrap, observation, cancellation.Token);
        var child = GuiOwnedProcess.RunAsync(start, Budget, cancellation.Token);
        Exception? failure = null;
        try
        {
            var first = await Task.WhenAny(serving, child).ConfigureAwait(false);
            if (first.IsFaulted || first.IsCanceled || (first == child && !serving.IsCompleted)) cancellation.Cancel();
            await Task.WhenAll(serving, child).ConfigureAwait(false);
            if (child.Result.ExitCode != 0) throw new InvalidOperationException("Frontend port test process returned a nonzero exit code.");
            if (observation.CompletedTests != 1) throw new InvalidOperationException("Frontend port test did not confirm one completed lifecycle case.");
        }
        catch (Exception error) { failure = error; }
        finally
        {
            cancellation.Cancel();
            // Never abandon an HTTP/SQL worker on a Task.WhenAny timeout. Serve is serial
            // and awaits Scenario.Send (its existing finite HTTP/SQL budgets) before exit.
            try { await serving.ConfigureAwait(false); } catch (Exception error) { failure ??= error; }
            try { await child.ConfigureAwait(false); } catch (Exception error) { failure ??= error; }
        }
        if (failure != null)
        {
            var cleanup = failure is GuiProcessException process ? process.CleanupVerified.ToString() : "see owned process result";
            // GuiProcessException embeds captured stdout/stderr. Do not attach it, log
            // subprocess output, or expose an auth request/response through test failures.
            throw new InvalidOperationException("Frontend port bridge failed after " + observation.Requests +
                " HTTP requests; completed cases=" + observation.CompletedTests + "; process cleanup=" + cleanup +
                ". Request bodies, credentials, tokens and child output were not logged.");
        }
        return observation;
    }

    private static async Task Serve(NamedPipeServerStream pipe, HttpScenario scenario, JObject bootstrap, Observation observed, CancellationToken token)
    {
        await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
        bootstrap["type"] = "bootstrap";
        bootstrap["protocol"] = "web4-port-bridge-v1";
        bootstrap["origin"] = Origin;
        await WriteFrame(pipe, bootstrap, token).ConfigureAwait(false);
        for (var expectedId = 1; expectedId <= 200; expectedId++)
        {
            var frame = await ReadFrame(pipe, token).ConfigureAwait(false);
            if (frame["id"]?.Type != JTokenType.Integer || (long)frame["id"]! != expectedId || frame["type"]?.Type != JTokenType.String)
                throw InvalidFrame();
            var type = (string)frame["type"]!;
            if (type == "http")
            {
                using var request = CreateRequest(frame);
                observed.Requests++;
                var path = request.RequestUri!.AbsolutePath;
                if (request.Method != HttpMethod.Get && (path.StartsWith("/api/v1/sales-receipts", StringComparison.Ordinal) || path.StartsWith("/api/v1/goods-receipts", StringComparison.Ordinal)))
                {
                    // Count the exact frontend key; never synthesize one for the client.
                    var key = request.Headers.TryGetValues("Idempotency-Key", out var values) ? string.Join(",", values) : "missing";
                    observed.MutationDispatches.TryGetValue(key, out var count);
                    observed.MutationDispatches[key] = count + 1;
                }
                var response = await scenario.Send(request).ConfigureAwait(false);
                if (request.Method == HttpMethod.Get && path.StartsWith("/api/v1/operations/", StringComparison.Ordinal) && !path.EndsWith("/context", StringComparison.Ordinal)) observed.StatusReads++;
                if (request.Method == HttpMethod.Get && response.Status == 403 && Regex.IsMatch(path, @"\A/api/v1/(sales-receipts|goods-receipts)/[0-9]+\z")) observed.DeniedDocumentReads++;
                var headers = new JArray(response.Headers.SelectMany(header => header.Value.Select(value => new JArray(header.Key, value))));
                await WriteFrame(pipe, new JObject { ["id"] = expectedId, ["type"] = type, ["status"] = response.Status, ["headers"] = headers, ["body"] = response.Body }, token).ConfigureAwait(false);
            }
            else if (type == "restart")
            {
                RequireProperties(frame, "id", "type");
                scenario.RestartHost();
                observed.Restarts++;
                await WriteFrame(pipe, new JObject { ["id"] = expectedId, ["type"] = type }, token).ConfigureAwait(false);
            }
            else if (type == "complete")
            {
                RequireProperties(frame, "id", "type", "executedTests");
                if (frame["executedTests"]?.Type != JTokenType.Integer || (long)frame["executedTests"]! != 1) throw InvalidFrame();
                observed.CompletedTests = 1;
                await WriteFrame(pipe, new JObject { ["id"] = expectedId, ["type"] = type }, token).ConfigureAwait(false);
                return;
            }
            else throw InvalidFrame();
        }
        throw new InvalidDataException("Frontend port bridge request limit exceeded.");
    }

    internal static HttpRequestMessage CreateRequest(JObject frame)
    {
        RequireProperties(frame, "id", "type", "method", "path", "headers", "body");
        if (frame["method"]?.Type != JTokenType.String || frame["path"]?.Type != JTokenType.String || frame["headers"] is not JArray headers ||
            (frame["body"]?.Type != JTokenType.Null && frame["body"]?.Type != JTokenType.String)) throw InvalidFrame();
        var method = (string)frame["method"]!;
        var path = (string)frame["path"]!;
        if ((method != "GET" && method != "POST" && method != "PUT") || path.Length > 2048 ||
            !path.StartsWith("/api/v1/", StringComparison.Ordinal) || path.Contains("..") || path.IndexOfAny(new[] { '#', '\\', '\r', '\n' }) >= 0 ||
            !Uri.TryCreate(Origin + path, UriKind.Absolute, out var uri) || uri.GetLeftPart(UriPartial.Authority) != Origin || uri.PathAndQuery != path || headers.Count > 32) throw InvalidFrame();
        var request = new HttpRequestMessage(new HttpMethod(method), uri);
        try
        {
            if (frame["body"]!.Type == JTokenType.String) request.Content = new ByteArrayContent(Utf8.GetBytes((string)frame["body"]!));
            foreach (var header in headers)
            {
                if (header is not JArray pair || pair.Count != 2 || pair.Any(value => value.Type != JTokenType.String)) throw InvalidFrame();
                var name = (string)pair[0]!;
                var value = (string)pair[1]!;
                if (!Regex.IsMatch(name, @"\A[A-Za-z0-9-]{1,80}\z") || value.Length > 16384 || value.IndexOfAny(new[] { '\r', '\n' }) >= 0 ||
                    new[] { "Origin", "Sec-Fetch-Site", "Host", "Content-Length", "Transfer-Encoding", "Connection" }.Contains(name, StringComparer.OrdinalIgnoreCase)) throw InvalidFrame();
                if (!request.Headers.TryAddWithoutValidation(name, value) && (request.Content == null || !request.Content.Headers.TryAddWithoutValidation(name, value))) throw InvalidFrame();
            }
            request.Headers.TryAddWithoutValidation("Origin", Origin);
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
            return request;
        }
        catch { request.Dispose(); throw; }
    }

    internal static async Task<JObject> ReadFrame(Stream stream, CancellationToken token)
    {
        var prefix = new byte[4];
        await ReadExactly(stream, prefix, token).ConfigureAwait(false);
        var length = (uint)(prefix[0] | prefix[1] << 8 | prefix[2] << 16 | prefix[3] << 24);
        if (length == 0 || length > MaximumFrameBytes) throw InvalidFrame();
        var bytes = new byte[(int)length];
        await ReadExactly(stream, bytes, token).ConfigureAwait(false);
        try
        {
            using var reader = new JsonTextReader(new StringReader(Utf8.GetString(bytes))) { DateParseHandling = DateParseHandling.None, MaxDepth = 16 };
            var value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error, LineInfoHandling = LineInfoHandling.Ignore });
            if (reader.Read()) throw InvalidFrame();
            return value;
        }
        catch (Exception error) when (error is JsonException || error is DecoderFallbackException) { throw InvalidFrame(); }
    }

    internal static async Task WriteFrame(Stream stream, JObject frame, CancellationToken token)
    {
        var bytes = Utf8.GetBytes(frame.ToString(Formatting.None));
        if (bytes.Length == 0 || bytes.Length > MaximumFrameBytes) throw InvalidFrame();
        var prefix = new[] { (byte)bytes.Length, (byte)(bytes.Length >> 8), (byte)(bytes.Length >> 16), (byte)(bytes.Length >> 24) };
        await stream.WriteAsync(prefix, 0, prefix.Length, token).ConfigureAwait(false);
        await stream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }

    private static async Task ReadExactly(Stream stream, byte[] bytes, CancellationToken token)
    {
        var offset = 0;
        while (offset < bytes.Length)
        {
            var count = await stream.ReadAsync(bytes, offset, bytes.Length - offset, token).ConfigureAwait(false);
            if (count == 0) throw new EndOfStreamException("Frontend port bridge closed before a complete frame.");
            offset += count;
        }
    }

    private static void RequireProperties(JObject frame, params string[] names)
    {
        if (!frame.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(names.OrderBy(name => name, StringComparer.Ordinal))) throw InvalidFrame();
    }
    private static InvalidDataException InvalidFrame() => new InvalidDataException("Frontend port bridge received an invalid or oversized frame.");
}

internal sealed class WebFrontendPortSqlFactAttribute : FactAttribute
{
    public WebFrontendPortSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(WebFrontendPortBridge.CheckoutVariable)))
            Skip = "Actual frontend port integration requires the designated nonproduction SQL connection and an explicit frontend checkout.";
    }
}
