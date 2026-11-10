using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using MyDmsVn.Web6.CandidateBootstrap.Processes;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal sealed class SelectorProcessRunner
{
    private const string FilterPrefix = "FullyQualifiedName=";
    private static readonly Regex ExactFilter = new Regex(
        @"\AFullyQualifiedName=[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*){2,}\z", RegexOptions.CultureInvariant);
    private static readonly XNamespace Trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    private static readonly string[] PlatformVariables =
    {
        "SystemRoot", "WINDIR", "TEMP", "TMP", "USERPROFILE", "LOCALAPPDATA", "APPDATA",
        "PROGRAMFILES", "PROGRAMFILES(X86)", "PROGRAMW6432",
    };
    private static readonly HashSet<string> SelectorVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING", "MYDMSVN_SQL_TEST_APPROVAL_FILE",
        "MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY", "MYDMSVN_WEB_TEST_TLS_THUMBPRINT", "MYDMSVN_WEB_TEST_BROWSER_DIRECTORY", "MYDMSVN_WEB_TEST_NODE",
        "MYDMSVN_WEB_PILOT_PROJECT", "PLAYWRIGHT_BROWSERS_PATH",
        "MYDMSVN_WEB6_CANDIDATE_ENABLE", "MYDMSVN_WEB6_CANDIDATE_ID", "MYDMSVN_WEB6_CANDIDATE_ROOT",
        "MYDMSVN_WEB6_CUTOVER_ENABLE", "MYDMSVN_WEB6_CUTOVER_FRONTEND_ARCHIVE_DIRECTORY",
        "MYDMSVN_WEB6_CUTOVER_BACKEND_ARCHIVE_DIRECTORY", "MYDMSVN_WEB6_CUTOVER_FRONTEND_DIST_DIRECTORY",
        "MYDMSVN_WEB6_CUTOVER_OLD_PUBLISH_DIRECTORY", "MYDMSVN_WEB6_CUTOVER_CURRENT_PUBLISH_DIRECTORY",
        "MYDMSVN_WEB6_CUTOVER_BUILD_AUTHORITY_PATH", "MYDMSVN_WEB6_CUTOVER_BUILD_AUTHORITY_SHA256",
        "MYDMSVN_WEB6_CUTOVER_SQL_CONTROL_PARENT", "MYDMSVN_WEB6_CUTOVER_CURRENT_EPOCH", "MYDMSVN_WEB6_CUTOVER_RESTORED_EPOCH",
        "MYDMSVN_WEB6_TASK4_ACCEPTANCE_PATH", "MYDMSVN_WEB6_TASK4_ACCEPTANCE_SHA256",
        "MYDMSVN_WEB6_WORKLOAD_ENABLE", "MYDMSVN_WEB6_WORKLOAD_ROOT", "MYDMSVN_WEB6_WORKLOAD_ACTOR_COHORT",
        "MYDMSVN_WEB6_RECOVERY_ENABLE", "MYDMSVN_WEB6_RECOVERY_ROOT",
        "MYDMSVN_WEB6_BOOTSTRAP_RUN_ID", "MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_PATH", "MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_SHA_PATH",
        "MYDMSVN_WEB6_BOOTSTRAP_GIT_PATH", "MYDMSVN_WEB6_BOOTSTRAP_GIT_SHA256",
    };

    private readonly string _dotnet;
    private readonly string _git;
    private readonly string _gitSha256;
    private readonly string _directory;
    private readonly string _project;
    private readonly Func<BootstrapProcessRequest, CancellationToken, Task<GuiProcessResult>> _run;

    internal SelectorProcessRunner(string dotnet, string git, string gitSha256, string directory, string project,
        Func<BootstrapProcessRequest, CancellationToken, Task<GuiProcessResult>>? run = null)
    {
        _dotnet = BootstrapJson.AbsolutePath(dotnet);
        _git = BootstrapJson.AbsolutePath(git);
        if (!string.Equals(Path.GetFileName(_git), "git.exe", StringComparison.OrdinalIgnoreCase) ||
            !BootstrapJson.IsLowerHex(gitSha256, 64))
            throw new IOException("Bootstrap selector Git executable or digest is invalid.");
        _gitSha256 = gitSha256;
        _directory = BootstrapJson.AbsolutePath(directory);
        _project = BootstrapJson.AbsolutePath(project);
        if (!BootstrapJson.IsChild(_project, _directory) || Path.GetExtension(_project) != ".csproj")
            throw new IOException("Bootstrap selector project must be inside its source checkout.");
        _run = run ?? RunOwnedAsync;
    }

    internal async Task<SelectorResult> RunAsync(SelectorInvocation invocation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        RequireExactFilter(invocation.Filter);
        BootstrapJson.RequireCandidateId(invocation.Name);
        var trxPath = BootstrapJson.AbsolutePath(invocation.TrxPath);
        var trxDirectory = Path.GetDirectoryName(trxPath)!;
        var selectorDll = BootstrapJson.AbsolutePath(invocation.SelectorDllPath);
        if (!BootstrapJson.IsChild(selectorDll, _directory) || !string.Equals(Path.GetExtension(selectorDll), ".dll", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Bootstrap selector DLL must be inside its source checkout.");
        if (Path.GetExtension(trxPath) != ".trx" || Path.GetFileName(trxPath).IndexOfAny(new[] { ';', '=' }) >= 0 ||
            invocation.Timeout <= TimeSpan.Zero || invocation.Timeout > TimeSpan.FromMinutes(30))
            throw new IOException("Bootstrap selector result file or timeout is invalid.");

        var environment = ChildEnvironment(invocation.Environment);
        using var dotnetParents = new ProtectedJsonFile.PathLease(_dotnet);
        using var dotnet = dotnetParents.OpenRead(installedTool: true);
        using var gitParents = new ProtectedJsonFile.PathLease(_git);
        using var git = gitParents.OpenRead(installedTool: true);
        VerifyPinnedFile(_git, gitParents, git, _gitSha256, installedTool: true);
        using var projectParents = new ProtectedJsonFile.PathLease(_project);
        using var project = projectParents.OpenRead();
        using var assemblyParents = new ProtectedJsonFile.PathLease(selectorDll);
        using var assembly = assemblyParents.OpenRead();
        var assemblySha256 = BootstrapPathPolicy.HashStream(assembly);
        // Create-new makes this path a one-shot evidence location, even across
        // process restarts. Never adopt an existing directory or TRX.
        using var results = new BootstrapPathPolicy.PinnedDirectory(trxDirectory, create: true);
        var arguments = new[]
        {
            // The orchestrator restores and builds before the selector DLL is pinned.
            "test", _project, "--configuration", "Release", "--framework", "net48", "--no-build", "--no-restore", "--disable-build-servers",
            "-nr:false", "--filter", invocation.Filter,
            "--logger", "trx;LogFileName=" + Path.GetFileName(trxPath), "--results-directory", trxDirectory,
        };

        var started = DateTimeOffset.UtcNow;
        GuiProcessResult child;
        try
        {
            child = await _run(new BootstrapProcessRequest(_dotnet, _directory, arguments, environment, invocation.Timeout), token)
                .ConfigureAwait(false);
        }
        catch (GuiProcessException error)
        {
            var disposition = error.Reason switch
            {
                "TimedOut" => SelectorDisposition.TimedOut,
                "Canceled" => SelectorDisposition.Canceled,
                _ => SelectorDisposition.ProcessUncertain,
            };
            return new SelectorResult(invocation, disposition, started, DateTimeOffset.UtcNow, error.CleanupVerified);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return new SelectorResult(invocation, SelectorDisposition.Canceled, started, DateTimeOffset.UtcNow, processTreeDrained: false);
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException or AggregateException)
        {
            return new SelectorResult(invocation, SelectorDisposition.ProcessUncertain, started, DateTimeOffset.UtcNow, processTreeDrained: false);
        }

        SelectorTrx? counts = null;
        var trxDisposition = SelectorDisposition.MissingTrx;
        try
        {
            VerifyPinnedFile(_git, gitParents, git, _gitSha256, installedTool: true);
            VerifyPinnedFile(selectorDll, assemblyParents, assembly, assemblySha256);
            results.Verify();
            if (File.Exists(trxPath))
            {
                counts = BootstrapPathPolicy.ReadFile(trxPath,
                    file => ReadTrx(file, invocation.Filter.Substring(FilterPrefix.Length), selectorDll)).Value;
                trxDisposition = counts.Failed == 0 && counts.Skipped == 0 ? SelectorDisposition.Passed : SelectorDisposition.MandatoryTestFailed;
            }
            results.Verify();
        }
        catch (Exception error) when (error is XmlException or IOException or UnauthorizedAccessException)
        {
            counts = null;
            trxDisposition = SelectorDisposition.InvalidTrx;
        }

        // Stdout and stderr are drained by the owned runner and never copied
        // into the result. Child text can contain arbitrary secrets.
        return new SelectorResult(invocation, child.ExitCode == 0 ? trxDisposition : SelectorDisposition.NonzeroExit,
            started, DateTimeOffset.UtcNow, child.CleanupVerified, child.ExitCode, counts);
    }

    internal static void RequireExactFilter(string filter)
    {
        if (filter == null || filter.Length > 512 || !ExactFilter.IsMatch(filter))
            throw new IOException("Bootstrap requires a single exact fully qualified selector.");
    }

    private Dictionary<string, string> ChildEnvironment(IReadOnlyDictionary<string, string> input)
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in PlatformVariables)
            if (Environment.GetEnvironmentVariable(name) is { } value) environment.Add(name, value);
        foreach (var variable in input)
        {
            if (!SelectorVariables.Contains(variable.Key)) continue;
            if (variable.Value == null || variable.Value.IndexOf('\0') >= 0 || environment.ContainsKey(variable.Key))
                throw new IOException("Bootstrap selector environment contains an invalid entry.");
            environment.Add(variable.Key, variable.Value);
        }

        var path = new List<string> { Path.GetDirectoryName(_git)!, Path.GetDirectoryName(_dotnet)!, Environment.SystemDirectory };
        if (environment.TryGetValue("MYDMSVN_WEB_TEST_NODE", out var node))
            path.Insert(1, Path.GetDirectoryName(BootstrapJson.AbsolutePath(node))!);
        if (path.Any(directory => directory.Contains(";")))
            throw new IOException("Bootstrap tool path contains a PATH separator.");
        environment["PATH"] = string.Join(";", path.Distinct(StringComparer.OrdinalIgnoreCase));
        environment["COMSPEC"] = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        environment["DOTNET_NOLOGO"] = "1";
        environment["MSBUILDDISABLENODEREUSE"] = "1";
        environment["MYDMSVN_WEB6_BOOTSTRAP_GIT_PATH"] = _git;
        environment["MYDMSVN_WEB6_BOOTSTRAP_GIT_SHA256"] = _gitSha256;
        return environment;
    }

    private static Task<GuiProcessResult> RunOwnedAsync(BootstrapProcessRequest command, CancellationToken token)
    {
        var info = new ProcessStartInfo(command.Executable, string.Join(" ", command.Arguments.Select(BootstrapToolchain.Quote)))
        { WorkingDirectory = command.WorkingDirectory, UseShellExecute = false };
        info.EnvironmentVariables.Clear();
        foreach (var variable in command.Environment) info.EnvironmentVariables.Add(variable.Key, variable.Value);
        return GuiOwnedProcess.RunAttendedAsync(info, command.Timeout, token);
    }

    private static void VerifyPinnedFile(string path, ProtectedJsonFile.PathLease parents, FileStream file,
        string expectedDigest, bool installedTool = false)
    {
        using var reopened = parents.OpenRead(installedTool);
        var identity = ProtectedJsonFile.Inspect(file.SafeFileHandle, path, directory: false, installedTool: installedTool);
        file.Position = 0;
        if (identity != ProtectedJsonFile.Inspect(reopened.SafeFileHandle, path, directory: false, installedTool: installedTool) ||
            BootstrapPathPolicy.HashStream(file) != expectedDigest)
            throw new IOException("Bootstrap selector tool or assembly changed.");
    }

    private static SelectorTrx ReadTrx(FileStream file, string expectedName, string expectedAssembly)
    {
        const int maxSize = 8 * 1024 * 1024;
        if (file.Length > maxSize) throw new IOException("Bootstrap TRX exceeds its size budget.");
        var hash = BootstrapPathPolicy.HashStream(file);
        file.Position = 0;
        using var reader = XmlReader.Create(file, new XmlReaderSettings
        { XmlResolver = null, DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = maxSize });
        var root = XDocument.Load(reader).Root;
        if (root?.Name != Trx + "TestRun") throw new IOException("Bootstrap TRX has the wrong root.");

        // Each current selector is one mandatory Fact. The result, definition
        // and entry must describe the same execution of that exact method.
        var definition = SingleRow(Only(root, "TestDefinitions"), "UnitTest");
        var execution = Only(definition, "Execution");
        var method = Only(definition, "TestMethod");
        var entry = SingleRow(Only(root, "TestEntries"), "TestEntry");
        var result = SingleRow(Only(root, "Results"), "UnitTestResult");
        if ((string?)method.Attribute("className") + "." + (string?)method.Attribute("name") != expectedName ||
            !MatchesAssembly(definition, "storage", expectedAssembly) || !MatchesAssembly(method, "codeBase", expectedAssembly) ||
            !Guid.TryParse((string?)definition.Attribute("id"), out var testId) || testId == Guid.Empty ||
            !Guid.TryParse((string?)execution.Attribute("id"), out var executionId) || executionId == Guid.Empty ||
            !Matches(entry, "testId", testId) || !Matches(result, "testId", testId) ||
            !Matches(entry, "executionId", executionId) || !Matches(result, "executionId", executionId) ||
            result.Descendants(Trx + "UnitTestResult").Any())
            throw new IOException("Bootstrap TRX contains a foreign or inconsistent test execution.");

        var summary = Only(root, "ResultSummary");
        var counters = Only(summary, "Counters");
        var passed = Count(counters, "passed");
        var failed = Count(counters, "failed");
        var skipped = Count(counters, "notExecuted");
        var executed = Count(counters, "executed");
        var discovered = Count(counters, "total");
        var outcome = (string?)result.Attribute("outcome");
        if (discovered != 1 || discovered != passed + failed + skipped || executed != passed + failed ||
            passed != (outcome == "Passed" ? 1 : 0) || failed != (outcome == "Failed" ? 1 : 0) ||
            skipped != (outcome == "NotExecuted" ? 1 : 0) ||
            new[] { "error", "timeout", "aborted", "inconclusive", "passedButRunAborted", "notRunnable",
                "disconnected", "warning", "completed", "inProgress", "pending" }
                .Any(name => counters.Attribute(name) != null && Count(counters, name) != 0) ||
            (passed == 1 && (string?)summary.Attribute("outcome") is not ("Completed" or "Passed")))
            throw new IOException("Bootstrap TRX counts or outcome are inconsistent.");
        return new SelectorTrx(discovered, executed, passed, failed, skipped, hash);
    }

    private static XElement Only(XElement parent, string name)
    {
        var matches = parent.Elements(Trx + name).Take(2).ToArray();
        if (matches.Length != 1) throw new IOException("Bootstrap TRX is missing or repeats a required entry.");
        return matches[0];
    }

    private static XElement SingleRow(XElement parent, string name)
    {
        var rows = parent.Elements().Take(2).ToArray();
        if (rows.Length != 1 || rows[0].Name != Trx + name)
            throw new IOException("Bootstrap TRX must contain exactly one mandatory Fact.");
        return rows[0];
    }

    private static bool Matches(XElement element, string name, Guid value) =>
        Guid.TryParse((string?)element.Attribute(name), out var actual) && actual == value;

    private static bool MatchesAssembly(XElement element, string name, string path) =>
        (string?)element.Attribute(name) is { } value && BootstrapJson.SamePath(BootstrapJson.AbsolutePath(value), path);

    private static int Count(XElement counters, string name)
    {
        if (!int.TryParse((string?)counters.Attribute(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value > 1)
            throw new IOException("Bootstrap TRX contains an invalid mandatory Fact count.");
        return value;
    }
}
