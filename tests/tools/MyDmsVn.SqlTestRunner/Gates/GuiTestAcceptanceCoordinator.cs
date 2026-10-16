using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Processes;
using MyDmsVn.SqlTestRunner.Trx;

namespace MyDmsVn.SqlTestRunner.Gates;

public sealed record GuiTestAcceptanceOptions(
    string RepositoryRoot,
    string ProjectPath,
    string Framework,
    string ArtifactRoot,
    TimeSpan Timeout,
    string Revision = "")
{
    public string TrxPath => Path.Combine(ArtifactRoot, "desktop-d2-" + Framework + ".trx");
    public string ApprovedProjectPath { get; init; } = string.Empty;
    public IReadOnlyList<string> ApprovedFrameworks { get; init; } = Array.Empty<string>();
}

public sealed record GuiTestAcceptanceResult(
    bool IsComplete,
    int SourceMethods,
    int DiscoveredCases,
    int ExecutedCases,
    TimeSpan Duration)
{
    public string Project { get; init; } = string.Empty;
    public string Framework { get; init; } = string.Empty;
    public IReadOnlyList<TestMethodDescriptor> SourceMethodEvidence { get; init; } = Array.Empty<TestMethodDescriptor>();
    public IReadOnlyList<DiscoveredTestCase> DiscoveredCaseEvidence { get; init; } = Array.Empty<DiscoveredTestCase>();
    public IReadOnlyList<TrxTestResult> ExecutedCaseEvidence { get; init; } = Array.Empty<TrxTestResult>();
}

public static class GuiTestAcceptanceEvidenceWriter
{
    public static Task WriteAsync(
        GuiTestAcceptanceOptions options,
        GuiTestAcceptanceResult result,
        CancellationToken cancellationToken)
    {
        if (!result.IsComplete)
            throw new InvalidOperationException("Incomplete GUI acceptance cannot produce a passing evidence artifact.");

        var evidence = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            revision = options.Revision,
            framework = result.Framework,
            project = result.Project,
            sourceMethods = result.SourceMethodEvidence,
            discoveredCases = result.DiscoveredCaseEvidence,
            executedCases = result.ExecutedCaseEvidence,
        }, new JsonSerializerOptions { WriteIndented = true });
        return File.WriteAllTextAsync(Path.Combine(options.ArtifactRoot, "desktop-d2.inventory.json"), evidence, cancellationToken);
    }
}

public interface IGuiTestSourceInventory
{
    IReadOnlyList<TestMethodDescriptor> Read(string assemblyPath);
}

public sealed class GuiTestSourceInventory : IGuiTestSourceInventory
{
    public IReadOnlyList<TestMethodDescriptor> Read(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata) throw new InvalidDataException("GUI test assembly has no managed metadata.");

        var reader = pe.GetMetadataReader();
        var fingerprint = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assemblyPath))).ToLowerInvariant();
        var methods = new List<TestMethodDescriptor>();
        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            var attributes = method.GetCustomAttributes()
                .Select(attribute => AttributeTypeName(reader, attribute))
                .ToArray();
            var isTheory = attributes.Contains("TheoryAttribute", StringComparer.Ordinal);
            if (!isTheory && !attributes.Contains("FactAttribute", StringComparer.Ordinal)) continue;

            var type = reader.GetTypeDefinition(method.GetDeclaringType());
            var typeName = reader.GetString(type.Name);
            var typeNamespace = reader.GetString(type.Namespace);
            var methodName = reader.GetString(method.Name);
            var fullyQualifiedName = string.IsNullOrEmpty(typeNamespace)
                ? typeName + "." + methodName
                : typeNamespace + "." + typeName + "." + methodName;
            methods.Add(new TestMethodDescriptor(
                fullyQualifiedName, isTheory, fingerprint, new[] { "NonSql" }, true)
            {
                NormalizedSignature = fullyQualifiedName + "()",
            });
        }

        return methods.OrderBy(method => method.FullyQualifiedName, StringComparer.Ordinal).ToArray();
    }

    private static string AttributeTypeName(MetadataReader reader, CustomAttributeHandle handle)
    {
        var constructor = reader.GetCustomAttribute(handle).Constructor;
        EntityHandle type = constructor.Kind switch
        {
            HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)constructor).Parent,
            HandleKind.MethodDefinition => reader.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType(),
            _ => default,
        };
        return type.Kind switch
        {
            HandleKind.TypeReference => reader.GetString(reader.GetTypeReference((TypeReferenceHandle)type).Name),
            HandleKind.TypeDefinition => reader.GetString(reader.GetTypeDefinition((TypeDefinitionHandle)type).Name),
            _ => string.Empty,
        };
    }
}

public sealed record GuiProcessSafetyResult(bool NoUnexpectedDialog, IReadOnlyList<int> OrphanProcessIds);

public interface IGuiProcessSafetyProbe
{
    GuiProcessSafetyResult Inspect(CommandResult result);
}

public sealed class GuiProcessSafetyProbe : IGuiProcessSafetyProbe
{
    public GuiProcessSafetyResult Inspect(CommandResult result)
    {
        var unexpectedUi = result.StandardError.Contains("[MYDMSVN-UNEXPECTED-UI]", StringComparison.Ordinal);
        return new GuiProcessSafetyResult(!unexpectedUi, result.TerminatedProcessIds);
    }
}

public interface IGuiExecutionLease
{
    IDisposable? TryAcquire(TimeSpan timeout);
}

public sealed class GlobalGuiExecutionLease : IGuiExecutionLease
{
    public IDisposable? TryAcquire(TimeSpan timeout)
    {
        var semaphore = new Semaphore(1, 1, "Global\\MyDmsVn.GuiAcceptance.v1");
        if (!semaphore.WaitOne(timeout))
        {
            semaphore.Dispose();
            return null;
        }

        return new SemaphoreReleaser(semaphore);
    }

    private sealed class SemaphoreReleaser : IDisposable
    {
        private Semaphore? _semaphore;
        public SemaphoreReleaser(Semaphore semaphore) => _semaphore = semaphore;
        public void Dispose()
        {
            var semaphore = Interlocked.Exchange(ref _semaphore, null);
            if (semaphore is null) return;
            semaphore.Release();
            semaphore.Dispose();
        }
    }
}

public sealed class GuiTestAcceptanceCoordinator
{
    private readonly IProcessRunner _processes;
    private readonly IGuiTestSourceInventory _source;
    private readonly IGuiProcessSafetyProbe _safety;
    private readonly IGuiExecutionLease _lease;

    public GuiTestAcceptanceCoordinator(
        IProcessRunner processes,
        IGuiTestSourceInventory source,
        IGuiProcessSafetyProbe safety,
        IGuiExecutionLease lease)
    {
        _processes = processes;
        _source = source;
        _safety = safety;
        _lease = lease;
    }

    public async Task<GuiTestAcceptanceResult> RunAsync(
        GuiTestAcceptanceOptions options,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows() ||
            options.Timeout <= TimeSpan.Zero ||
            !IsApprovedTarget(options) ||
            !File.Exists(options.ProjectPath))
        {
            return Incomplete();
        }

        using var lease = _lease.TryAcquire(TimeSpan.Zero);
        if (lease is null) return Incomplete();

        Directory.CreateDirectory(options.ArtifactRoot);
        if (File.Exists(options.TrxPath)) File.Delete(options.TrxPath);

        var build = await RunBuild(options, cancellationToken).ConfigureAwait(false);
        if (!Accept(build)) return Incomplete();

        var projectName = Path.GetFileNameWithoutExtension(options.ProjectPath);
        var assembly = Path.Combine(Path.GetDirectoryName(options.ProjectPath)!, "bin", "Release",
            options.Framework, projectName + ".dll");
        IReadOnlyList<TestMethodDescriptor> source;
        try { source = _source.Read(assembly); }
        catch (Exception exception) when (exception is IOException or InvalidOperationException) { return Incomplete(); }
        if (source.Count == 0 || source.Any(item => !item.IsBodyFingerprintComplete)) return Incomplete();

        var discovery = await Run(options, " --list-tests", cancellationToken).ConfigureAwait(false);
        if (!Accept(discovery)) return Incomplete();
        var discovered = VstestDiscoveryParser.Parse(discovery.StandardOutput, options.Framework);
        if (discovered.Count == 0) return Incomplete();

        var execution = await Run(options,
            " --logger \"trx;LogFileName=" + Path.GetFileName(options.TrxPath) + "\" --results-directory \"" +
            options.ArtifactRoot + "\"", cancellationToken).ConfigureAwait(false);
        if (!Accept(execution)) return Incomplete();
        var trx = TrxResultParser.TryParse(options.TrxPath, options.Framework);
        if (!trx.IsComplete || trx.Results.Count == 0 || trx.Results.Any(item => item.Outcome != TestOutcome.Passed))
            return Incomplete();

        var sourceMethods = source.Select(item => item.FullyQualifiedName).ToHashSet(StringComparer.Ordinal);
        var discoveredMethods = discovered.Select(item => item.MethodName).ToHashSet(StringComparer.Ordinal);
        var discoveredRows = discovered.Select(item => item.PhysicalRowLocator);
        var executedRows = trx.Results.Select(item => item.PhysicalRowLocator);
        var complete = sourceMethods.SetEquals(discoveredMethods) && SameMultiset(discoveredRows, executedRows) &&
            discovered.Count == trx.Results.Count;
        var project = Path.GetRelativePath(Path.GetFullPath(options.RepositoryRoot), Path.GetFullPath(options.ProjectPath))
            .Replace(Path.DirectorySeparatorChar, '/');
        return new GuiTestAcceptanceResult(complete, source.Count, discovered.Count, trx.Results.Count,
            build.Duration + discovery.Duration + execution.Duration)
        {
            Project = project,
            Framework = options.Framework,
            SourceMethodEvidence = source,
            DiscoveredCaseEvidence = discovered,
            ExecutedCaseEvidence = trx.Results,
        };
    }

    private static bool IsApprovedTarget(GuiTestAcceptanceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApprovedProjectPath) ||
            options.ApprovedFrameworks.Count == 0 ||
            !options.ApprovedFrameworks.Contains(options.Framework, StringComparer.Ordinal))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(options.ProjectPath),
                Path.GetFullPath(options.ApprovedProjectPath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    private async Task<CommandResult> RunBuild(
        GuiTestAcceptanceOptions options,
        CancellationToken cancellationToken)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MYDMSVN_GUI_ACCEPTANCE"] = "1",
            ["MSBUILDDISABLENODEREUSE"] = "1",
        };
        return await _processes.RunAsync(new CommandSpec("dotnet",
            "build \"" + options.ProjectPath + "\" -c Release -f " + options.Framework + " --no-restore --disable-build-servers -m:1",
            options.RepositoryRoot, options.Timeout, environment), cancellationToken).ConfigureAwait(false);
    }

    private static bool SameMultiset(IEnumerable<string> left, IEnumerable<string> right)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var value in left)
        {
            counts.TryGetValue(value, out var count);
            counts[value] = count + 1;
        }

        foreach (var value in right)
        {
            if (!counts.TryGetValue(value, out var count)) return false;
            if (count == 1) counts.Remove(value);
            else counts[value] = count - 1;
        }

        return counts.Count == 0;
    }

    private async Task<CommandResult> Run(
        GuiTestAcceptanceOptions options,
        string suffix,
        CancellationToken cancellationToken)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MYDMSVN_GUI_ACCEPTANCE"] = "1",
        };
        return await _processes.RunAsync(new CommandSpec("dotnet",
            "test \"" + options.ProjectPath + "\" -c Release -f " + options.Framework +
            " --no-build --no-restore" + suffix, options.RepositoryRoot, options.Timeout, environment),
            cancellationToken).ConfigureAwait(false);
    }

    private bool Accept(CommandResult result)
    {
        var safety = _safety.Inspect(result);
        return result.ExitCode == 0 && !result.TimedOut && safety.NoUnexpectedDialog &&
            safety.OrphanProcessIds.Count == 0;
    }

    private static GuiTestAcceptanceResult Incomplete() => new(false, 0, 0, 0, TimeSpan.Zero);
}
