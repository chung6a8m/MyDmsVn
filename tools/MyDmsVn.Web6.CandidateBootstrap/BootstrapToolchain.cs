using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Web6.CandidateBootstrap.Processes;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal sealed class BootstrapProcessRequest
{
    internal BootstrapProcessRequest(string executable, string directory, string[] arguments,
        IReadOnlyDictionary<string, string> environment, TimeSpan timeout)
    { Executable = executable; WorkingDirectory = directory; Arguments = arguments; Environment = environment; Timeout = timeout; }
    internal string Executable { get; }
    internal string WorkingDirectory { get; }
    internal IReadOnlyList<string> Arguments { get; }
    internal IReadOnlyDictionary<string, string> Environment { get; }
    internal TimeSpan Timeout { get; }
}

internal sealed class BootstrapCertificate
{
    internal BootstrapCertificate(bool privateKey, DateTimeOffset notBefore, DateTimeOffset notAfter, string digest, string[] hosts)
    { PrivateKey = privateKey; NotBefore = notBefore; NotAfter = notAfter; Digest = digest; Hosts = hosts; }
    internal bool PrivateKey { get; }
    internal DateTimeOffset NotBefore { get; }
    internal DateTimeOffset NotAfter { get; }
    internal string Digest { get; }
    internal IReadOnlyList<string> Hosts { get; }
}

internal sealed class BootstrapToolchain
{
    internal const string CandidateVariable = "VITE_MYDMSVN_WEB6_CANDIDATE_ID";
    private const string SqlVariable = "MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING";
    private readonly Func<BootstrapProcessRequest, CancellationToken, Task<string>> _run;
    private readonly Func<string, string[]> _discover;
    private readonly Func<string, BootstrapCertificate[]> _certificates;
    private readonly Func<string[]> _edge;
    private readonly Func<string?> _connectionString;
    private readonly Func<string, CancellationToken, Task<(string Catalog, string Server)>> _readMaster;

    internal BootstrapToolchain(Func<BootstrapProcessRequest, CancellationToken, Task<string>> run,
        Func<string, string[]> discover, Func<string, BootstrapCertificate[]> certificates, Func<string[]> edge,
        Func<string?> connectionString, Func<string, CancellationToken, Task<(string Catalog, string Server)>> readMaster)
    { _run = run; _discover = discover; _certificates = certificates; _edge = edge; _connectionString = connectionString; _readMaster = readMaster; }

    internal static BootstrapToolchain Local() => new(RunOwnedAsync, DiscoverOnPath, ReadCertificates, FindEdge,
        () => Environment.GetEnvironmentVariable(SqlVariable), ReadMasterAsync);

    internal BootstrapTools Bind(BootstrapRequest request)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["iisExpress"] = request.IisExpressPath, ["node"] = request.NodePath,
            ["npm"] = Path.Combine(Path.GetDirectoryName(request.NodePath)!, "node_modules", "npm", "bin", "npm-cli.js"),
            ["git"] = FindOnPath("git"), ["dotnet"] = FindOnPath("dotnet"), ["edge"] = Single(_edge()),
        };
        var certificate = Single(_certificates(request.TlsThumbprint));
        VerifyCertificate(request, certificate);
        return new BootstrapTools(this, files, certificate.Digest);
    }

    internal void VerifyCertificate(BootstrapRequest request, string digest)
    {
        var certificate = Single(_certificates(request.TlsThumbprint));
        VerifyCertificate(request, certificate);
        if (certificate.Digest != digest) throw new IOException("Bootstrap TLS certificate changed.");
    }

    private static void VerifyCertificate(BootstrapRequest request, BootstrapCertificate certificate)
    {
        var now = DateTimeOffset.UtcNow;
        if (!certificate.PrivateKey || certificate.NotBefore > now || certificate.NotAfter <= now ||
            !BootstrapJson.IsLowerHex(certificate.Digest, 64) ||
            request.Origins.Any(origin => !certificate.Hosts.Contains(new Uri(origin).DnsSafeHost, StringComparer.OrdinalIgnoreCase)))
            throw new IOException("Bootstrap TLS certificate is unsuitable for the approved origins.");
    }

    private string FindOnPath(string name) => _discover(name).FirstOrDefault() ?? throw new IOException("Bootstrap build tool is missing.");

    private static T Single<T>(T[] results) => results.Length == 1 ? results[0] : throw new IOException("Bootstrap prerequisite is missing or ambiguous.");

    internal async Task<(string ServerDigest, DateTimeOffset Expires)> VerifySqlAsync(ProtectedJsonFile approval, CancellationToken token)
    {
        var json = BootstrapJson.Parse(approval.Bytes, "version", "purpose", "serverSha256", "expiresUtc");
        var expected = BootstrapJson.Text(json, "serverSha256");
        var now = DateTimeOffset.UtcNow;
        if (BootstrapJson.Text(json, "purpose") != "web6-cutover" || !BootstrapJson.IsLowerHex(expected, 64) ||
            !DateTimeOffset.TryParse(BootstrapJson.Text(json, "expiresUtc"), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var expires) || expires <= now || expires > now.AddHours(24))
            throw new IOException("Bootstrap SQL approval purpose, server or expiry was refused.");

        var configured = _connectionString();
        if (string.IsNullOrWhiteSpace(configured)) throw new IOException("Bootstrap requires an explicit test SQL connection.");
        var builder = new SqlConnectionStringBuilder(configured);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || !string.Equals(builder.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase) ||
            builder.AttachDBFilename.Length != 0 || builder.UserInstance || builder.FailoverPartner.Length != 0)
            throw new IOException("Bootstrap requires a dedicated SQL instance and explicit master catalog.");
        builder.Pooling = false;
        builder.ConnectRetryCount = 0;
        builder.ConnectTimeout = builder.ConnectTimeout == 0 ? 15 : Math.Min(builder.ConnectTimeout, 15);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(TimeSpan.FromSeconds(20));
        var (catalog, server) = await _readMaster(builder.ConnectionString, budget.Token).ConfigureAwait(false);
        if (!string.Equals(catalog, "master", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(server) ||
            ProtectedJsonFile.Digest(Encoding.UTF8.GetBytes(server)) != expected)
            throw new IOException("Bootstrap effective SQL catalog or server was refused.");
        approval.Verify();
        return (expected, expires);
    }

    private static async Task<(string, string)> ReadMasterAsync(string connectionString, CancellationToken token)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandTimeout = 10;
        command.CommandText = "SELECT DB_NAME(), CONVERT(nvarchar(128), SERVERPROPERTY('ServerName'));";
        using var result = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        if (!await result.ReadAsync(token).ConfigureAwait(false) || result.IsDBNull(0) || result.IsDBNull(1))
            throw new IOException("Bootstrap could not verify SQL server identity.");
        return (result.GetString(0), result.GetString(1));
    }

    private static BootstrapCertificate[] ReadCertificates(string thumbprint)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var matches = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
        try
        {
            return matches.Cast<X509Certificate2>().Select(cert => new BootstrapCertificate(
                cert.HasPrivateKey && AllowsServerAuthentication(cert), cert.NotBefore, cert.NotAfter,
                ProtectedJsonFile.Digest(cert.RawData), new[] { cert.GetNameInfo(X509NameType.DnsName, forIssuer: false) })).ToArray();
        }
        finally { foreach (var certificate in matches) certificate.Dispose(); }
    }

    private static bool AllowsServerAuthentication(X509Certificate2 certificate) => certificate.Extensions
        .OfType<X509EnhancedKeyUsageExtension>().All(usage => usage.EnhancedKeyUsages.Cast<Oid>()
            .Any(oid => oid.Value is "1.3.6.1.5.5.7.3.1" or "2.5.29.37.0"));

    private static string[] DiscoverOnPath(string executable) => (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(';').Select(value => value.Trim().Trim('"')).Where(Path.IsPathRooted)
        .Select(directory => Path.Combine(directory, executable + ".exe")).Where(File.Exists).ToArray();

    private static string[] FindEdge() => new[] { "LOCALAPPDATA", "PROGRAMFILES", "PROGRAMFILES(X86)", "PROGRAMW6432" }
        .Select(Environment.GetEnvironmentVariable).Where(path => !string.IsNullOrWhiteSpace(path))
        .Select(path => Path.Combine(path!, "Microsoft", "Edge", "Application", "msedge.exe"))
        .Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    internal static async Task<string> RunOwnedAsync(BootstrapProcessRequest command, CancellationToken token)
    {
        var info = new ProcessStartInfo(command.Executable, string.Join(" ", command.Arguments.Select(Quote)))
        { WorkingDirectory = command.WorkingDirectory, UseShellExecute = false };
        info.EnvironmentVariables.Clear();
        foreach (var variable in command.Environment) info.EnvironmentVariables[variable.Key] = variable.Value;
        try
        {
            // This existing bounded job runner starts suspended and assigns the job before any child can spawn.
            var result = await GuiOwnedProcess.RunAttendedAsync(info, command.Timeout, token).ConfigureAwait(false);
            if (result.ExitCode != 0) throw new IOException("Bootstrap child process returned a nonzero exit.");
            return result.StandardOutput.Trim();
        }
        catch (Exception error) when (error is GuiProcessException or Win32Exception or AggregateException)
        {
            token.ThrowIfCancellationRequested();
            throw new IOException("Bootstrap child process could not complete and drain.");
        }
    }

    // net48 has no ProcessStartInfo.ArgumentList. Encode each argument independently
    // using Windows argv quoting, including backslashes before quotes and closing delimiters.
    internal static string Quote(string argument)
    {
        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var c in argument)
        {
            if (c == '\\') { slashes++; continue; }
            result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes).Append(c);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }

    internal sealed class BootstrapTools : IDisposable
    {
        private readonly BootstrapToolchain _owner;
        private readonly Dictionary<string, (string Path, ProtectedJsonFile.PathLease Parents, FileStream File, string Digest)> _files = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _environment = new(StringComparer.OrdinalIgnoreCase);
        internal string CertificateDigest { get; }
        internal JObject ToolRecords => new(_files.Select(entry => new JProperty(entry.Key,
            new JObject { ["path"] = entry.Value.Path, ["sha256"] = entry.Value.Digest })));

        internal BootstrapTools(BootstrapToolchain owner, IReadOnlyDictionary<string, string> files, string certificateDigest)
        {
            _owner = owner;
            CertificateDigest = certificateDigest;
            try
            {
                foreach (var item in files) AddTool(item.Key, item.Value);
                foreach (var name in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP", "USERPROFILE", "LOCALAPPDATA", "APPDATA", "PROGRAMFILES", "PROGRAMFILES(X86)", "PROGRAMW6432" })
                    if (Environment.GetEnvironmentVariable(name) is { } value) _environment.Add(name, value);
                _environment["COMSPEC"] = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                _environment["PATH"] = string.Join(";", new[] { files["node"], files["git"], files["dotnet"] }
                    .Select(Path.GetDirectoryName).Concat(new[] { Environment.SystemDirectory }).Distinct(StringComparer.OrdinalIgnoreCase));
                _environment["GIT_CONFIG_NOSYSTEM"] = "1";
                _environment["GIT_CONFIG_GLOBAL"] = "NUL";
                _environment["GIT_TERMINAL_PROMPT"] = "0";
                _environment["PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD"] = "1";
                _environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
                _environment["DOTNET_NOLOGO"] = "1";
                _environment["MSBUILDDISABLENODEREUSE"] = "1";
            }
            catch { Dispose(); throw; }
        }

        private void AddTool(string name, string path)
        {
            var parents = new ProtectedJsonFile.PathLease(path);
            try
            {
                // Installed tools can have vendor-created hard links (Git for Windows
                // does). This handle denies writes/deletes through every link.
                var file = parents.OpenRead(installedTool: true);
                try { _files.Add(name, (path, parents, file, Measure(file))); }
                catch { file.Dispose(); throw; }
            }
            catch { parents.Dispose(); throw; }
        }

        internal async Task<string> RunAsync(string tool, string directory, CancellationToken token,
            string[] arguments, string? candidate = null, bool build = false)
        {
            token.ThrowIfCancellationRequested();
            var env = new Dictionary<string, string>(_environment, StringComparer.OrdinalIgnoreCase);
            if (candidate != null) env.Add(CandidateVariable, candidate);
            var executable = _files[tool];
            using var working = new BootstrapPathPolicy.PinnedDirectory(directory);
            VerifyIdentity(executable);
            var command = new BootstrapProcessRequest(executable.Path, directory, arguments, env,
                build ? TimeSpan.FromMinutes(10) : TimeSpan.FromSeconds(30));
            string result;
            try { result = await _owner._run(command, token).ConfigureAwait(false); }
            catch (TimeoutException) { throw new IOException("Bootstrap child process exceeded its execution budget."); }
            working.Verify();
            VerifyIdentity(executable);
            return result;
        }

        internal Task<string> NpmAsync(string directory, CancellationToken token, string[] args, string? candidate = null, bool build = false)
        {
            var npm = _files["npm"];
            VerifyIdentity(npm);
            return RunAsync("node", directory, token, new[] { npm.Path, "--script-shell=" + _environment["COMSPEC"] }
                .Concat(args).ToArray(), candidate, build);
        }

        internal async Task ValidateBrowsersAsync(string frontend, CancellationToken token)
        {
            const string script = "const r = require('playwright-core/lib/coreBundle').registry.registry; " +
                "console.log(JSON.stringify(['chromium','chromium-headless-shell','msedge'].map(name => r.findExecutable(name).executablePath())));";
            var values = JArray.Parse(await RunAsync("node", frontend, token, new[] { "-e", script }).ConfigureAwait(false));
            if (values.Count != 3 || values.Any(value => value.Type != JTokenType.String) ||
                !BootstrapJson.SamePath((string)values[2]!, _files["edge"].Path))
                throw new IOException("Bootstrap Playwright browser paths are missing or ambiguous.");
            AddTool("chromium", (string)values[0]!);
            AddTool("chromiumHeadlessShell", (string)values[1]!);
        }

        internal void Verify()
        {
            foreach (var file in _files.Values)
            {
                VerifyIdentity(file);
                if (Measure(file.File) != file.Digest) throw new IOException("Bootstrap tool bytes changed.");
            }
        }

        private static void VerifyIdentity((string Path, ProtectedJsonFile.PathLease Parents, FileStream File, string Digest) file)
        {
            using var reopened = file.Parents.OpenRead(installedTool: true);
            if (ProtectedJsonFile.Inspect(file.File.SafeFileHandle, file.Path, directory: false, installedTool: true) !=
                ProtectedJsonFile.Inspect(reopened.SafeFileHandle, file.Path, directory: false, installedTool: true))
                throw new IOException("Bootstrap tool identity changed.");
        }

        private static string Measure(FileStream file)
        {
            file.Position = 0;
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        }

        public void Dispose()
        {
            foreach (var file in _files.Values.Reverse()) { file.File.Dispose(); file.Parents.Dispose(); }
        }
    }
}
