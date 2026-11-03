using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal sealed class Web5HttpRequestQueue
{
    internal Web5HttpRequestQueue(string name, bool active, int attachedProcessCount, int controllerProcessId,
        IReadOnlyList<int> processIds, IReadOnlyList<string> registeredUrls)
    {
        Name = name; Active = active; AttachedProcessCount = attachedProcessCount;
        ControllerProcessId = controllerProcessId; ProcessIds = processIds; RegisteredUrls = registeredUrls;
    }
    internal string Name { get; }
    internal bool Active { get; }
    internal int AttachedProcessCount { get; }
    internal int ControllerProcessId { get; }
    internal IReadOnlyList<int> ProcessIds { get; }
    internal IReadOnlyList<string> RegisteredUrls { get; }
}

internal static class Web5HttpServiceState
{
    private const int MaximumSnapshotLength = 1024 * 1024;

    internal static IReadOnlyList<Web5HttpRequestQueue> Parse(string snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.Length == 0 || snapshot.Length > MaximumSnapshotLength)
            throw new InvalidDataException("HTTP service state snapshot size is invalid.");
        var lines = snapshot.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var nonempty = lines.Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.TrimEnd(' ', '\t')).ToArray();
        if (nonempty.Length < 2 || nonempty[0] != "Snapshot of HTTP service state (Request Queue View):" ||
            nonempty[1] != "-----------------------------------------------------")
            throw new InvalidDataException("HTTP service state request-queue view header is invalid.");
        var starts = Enumerable.Range(0, lines.Length)
            .Where(index => lines[index].StartsWith("Request queue name: ", StringComparison.Ordinal)).ToArray();
        if (starts.Length == 0)
        {
            if (nonempty.Length == 2) return Array.Empty<Web5HttpRequestQueue>();
            throw new InvalidDataException("HTTP service state contains no valid request-queue view.");
        }
        var queues = new List<Web5HttpRequestQueue>();
        for (var queueIndex = 0; queueIndex < starts.Length; queueIndex++)
        {
            var start = starts[queueIndex];
            var end = queueIndex + 1 < starts.Length ? starts[queueIndex + 1] : lines.Length;
            var name = lines[start].Substring("Request queue name: ".Length);
            if (string.IsNullOrWhiteSpace(name) || name.Length > 256) throw new InvalidDataException("HTTP request queue name is invalid.");
            var active = RequireSingle(lines, start, end, "    State: ") == "Active";
            var attached = ParseNonnegative(RequireSingle(lines, start, end, "    Number of active processes attached: "));
            var controllerHeader = FindSingle(lines, start, end, "    Controller process:");
            var controller = ParseProcess(lines, controllerHeader + 1, end);
            var processesHeader = FindSingle(lines, start, end, "    Processes:");
            var urlGroupsHeader = FindSingle(lines, start, end, "    URL groups:");
            if (processesHeader >= urlGroupsHeader) throw new InvalidDataException("HTTP request queue process section is invalid.");
            var processes = new List<int>();
            for (var index = processesHeader + 1; index < urlGroupsHeader; index++)
                if (lines[index].StartsWith("        ID: ", StringComparison.Ordinal)) processes.Add(ParseProcess(lines, index, end));
                else if (!string.IsNullOrWhiteSpace(lines[index])) throw new InvalidDataException("HTTP request queue process entry is invalid.");
            if (processes.Count != attached) throw new InvalidDataException("HTTP request queue process count is inconsistent.");
            var urls = new List<string>();
            var nestedQueueNames = new List<string>();
            for (var index = urlGroupsHeader + 1; index < end; index++)
            {
                if (lines[index].StartsWith("        Request queue name: ", StringComparison.Ordinal))
                    nestedQueueNames.Add(lines[index].Substring("        Request queue name: ".Length));
                if (!lines[index].StartsWith("            Number of registered URLs: ", StringComparison.Ordinal)) continue;
                var count = ParseNonnegative(lines[index].Substring("            Number of registered URLs: ".Length));
                if (++index >= end || lines[index] != "            Registered URLs:") throw new InvalidDataException("HTTP registered URL section is invalid.");
                for (var offset = 0; offset < count; offset++)
                {
                    index++;
                    if (index >= end || !lines[index].StartsWith("                ", StringComparison.Ordinal))
                        throw new InvalidDataException("HTTP registered URL count is inconsistent.");
                    var value = lines[index].Substring(16);
                    NormalizeRegisteredUrl(value);
                    urls.Add(value);
                }
            }
            if (nestedQueueNames.Count == 0 || nestedQueueNames.Any(value => !string.Equals(value, name, StringComparison.Ordinal)))
                throw new InvalidDataException("HTTP URL group ownership is inconsistent.");
            queues.Add(new Web5HttpRequestQueue(name, active, attached, controller, processes, urls));
        }
        return queues;
    }

    internal static Web5HttpRequestQueue RequireOwnedQueue(IEnumerable<Web5HttpRequestQueue> queues, Uri origin, int ownedProcessId)
    {
        if (queues == null) throw new ArgumentNullException(nameof(queues));
        if (origin == null || !origin.IsAbsoluteUri || origin.Scheme != Uri.UriSchemeHttps || origin.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment) || ownedProcessId <= 0)
            throw new ArgumentException("Owned HTTP queue identity is invalid.");
        var matches = queues.Where(queue => queue.RegisteredUrls.Any(url => MatchesOrigin(url, origin))).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("Expected exactly one HTTP request queue for the owned origin.");
        var match = matches[0];
        if (!match.Active || match.AttachedProcessCount != 1 || match.ControllerProcessId != ownedProcessId ||
            match.ProcessIds.Count != 1 || match.ProcessIds[0] != ownedProcessId)
            throw new InvalidDataException("HTTP request queue is not exclusively attached to the owned worker.");
        return match;
    }

    internal static async Task<string> ReadAsync(TimeSpan budget, CancellationToken token)
    {
        if (budget <= TimeSpan.Zero || budget > TimeSpan.FromSeconds(30)) throw new ArgumentOutOfRangeException(nameof(budget));
        token.ThrowIfCancellationRequested();
        var executable = Path.Combine(Environment.SystemDirectory, "netsh.exe");
        if (!File.Exists(executable)) throw new InvalidDataException("HTTP service-state reader is unavailable.");
        var start = new ProcessStartInfo(executable, "http show servicestate view=requestq verbose=yes")
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new InvalidDataException("HTTP service-state reader did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        var exited = Task.Run(() => process.WaitForExit());
        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = token.Register(() => cancelled.TrySetResult(true));
        var timeout = Task.Delay(budget);
        var completed = await Task.WhenAny(exited, timeout, cancelled.Task);
        if (completed != exited)
        {
            try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { }
            if (!process.WaitForExit(5000)) throw new TimeoutException("HTTP service-state reader cleanup is uncertain.");
            token.ThrowIfCancellationRequested();
            throw new TimeoutException("HTTP service-state read timed out.");
        }
        return ValidateCommandResult(process.ExitCode, await output, await error);
    }

    internal static string ValidateCommandResult(int exitCode, string output, string error)
    {
        if (exitCode != 0) throw new InvalidDataException("HTTP service-state read failed.");
        if (output == null || output.Length == 0 || output.Length > MaximumSnapshotLength || (error?.Length ?? 0) > 4096)
            throw new InvalidDataException("HTTP service-state command output is invalid.");
        return output;
    }

    internal static bool MatchesOrigin(string registeredUrl, Uri origin) =>
        NormalizeRegisteredUrl(registeredUrl) == NormalizeOrigin(origin);

    private static string NormalizeOrigin(Uri uri) => uri.GetComponents(
        UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.SafeUnescaped).TrimEnd('/').ToUpperInvariant() + "/";

    private static string NormalizeRegisteredUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 || value.Any(char.IsWhiteSpace))
            throw new InvalidDataException("HTTP registered URL is invalid.");
        var separator = value.IndexOf("://", StringComparison.Ordinal);
        if (separator <= 0) throw new InvalidDataException("HTTP registered URL is invalid.");
        var scheme = value.Substring(0, separator);
        if (!string.Equals(scheme, "HTTP", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(scheme, "HTTPS", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("HTTP registered URL scheme is invalid.");
        var authorityStart = separator + 3;
        var pathStart = value.IndexOf('/', authorityStart);
        if (pathStart <= authorityStart || value.IndexOfAny(new[] { '?', '#' }, pathStart) >= 0)
            throw new InvalidDataException("HTTP registered URL path is invalid.");
        var authority = value.Substring(authorityStart, pathStart - authorityStart);
        string host;
        string portText;
        if (authority.StartsWith("[", StringComparison.Ordinal))
        {
            var close = authority.IndexOf("]:", StringComparison.Ordinal);
            if (close <= 1) throw new InvalidDataException("HTTP registered URL authority is invalid.");
            host = authority.Substring(0, close + 1);
            portText = authority.Substring(close + 2);
            if (!IPAddress.TryParse(host.Substring(1, host.Length - 2), out _))
                throw new InvalidDataException("HTTP registered URL host is invalid.");
        }
        else
        {
            var colon = authority.LastIndexOf(':');
            if (colon <= 0) throw new InvalidDataException("HTTP registered URL authority is invalid.");
            host = authority.Substring(0, colon);
            portText = authority.Substring(colon + 1);
            if (host != "*" && host != "+" && Uri.CheckHostName(host) == UriHostNameType.Unknown)
                throw new InvalidDataException("HTTP registered URL host is invalid.");
        }
        if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port <= 0 || port > 65535)
            throw new InvalidDataException("HTTP registered URL port is invalid.");
        return scheme.ToUpperInvariant() + "://" + host.ToUpperInvariant() + ":" +
            port.ToString(CultureInfo.InvariantCulture) + value.Substring(pathStart).TrimEnd('/').ToUpperInvariant() + "/";
    }
    private static int FindSingle(string[] lines, int start, int end, string exact)
    {
        var matches = Enumerable.Range(start + 1, end - start - 1).Where(index => lines[index] == exact).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("HTTP service-state structure is ambiguous.");
        return matches[0];
    }
    private static string RequireSingle(string[] lines, int start, int end, string prefix)
    {
        var matches = Enumerable.Range(start + 1, end - start - 1).Where(index => lines[index].StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("HTTP service-state field is ambiguous.");
        return lines[matches[0]].Substring(prefix.Length);
    }
    private static int ParseProcess(string[] lines, int index, int end)
    {
        if (index >= end || !lines[index].StartsWith("        ID: ", StringComparison.Ordinal)) throw new InvalidDataException("HTTP process identity is missing.");
        var value = lines[index].Substring("        ID: ".Length);
        var comma = value.IndexOf(", image: ", StringComparison.Ordinal);
        if (comma <= 0 || string.IsNullOrWhiteSpace(value.Substring(comma + ", image: ".Length))) throw new InvalidDataException("HTTP process identity is invalid.");
        if (!int.TryParse(value.Substring(0, comma), NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
            throw new InvalidDataException("HTTP process ID is invalid.");
        return id;
    }
    private static int ParseNonnegative(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result < 0)
            throw new InvalidDataException("HTTP service-state count is invalid.");
        return result;
    }
}
