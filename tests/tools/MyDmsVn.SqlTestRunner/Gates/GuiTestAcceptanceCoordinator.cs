using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
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
}

public sealed record GuiTestAcceptanceResult(
    bool IsComplete,
    int SourceMethods,
    int DiscoveredCases,
    int ExecutedCases,
    TimeSpan Duration);

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
            !string.Equals(options.Framework, "net48", StringComparison.Ordinal) ||
            options.Timeout <= TimeSpan.Zero ||
            !File.Exists(options.ProjectPath))
        {
            return Incomplete();
        }

        using var lease = _lease.TryAcquire(TimeSpan.Zero);
        if (lease is null) return Incomplete();

        Directory.CreateDirectory(options.ArtifactRoot);
        if (File.Exists(options.TrxPath)) File.Delete(options.TrxPath);
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
        var discoveredRows = discovered.Select(item => item.PhysicalRowLocator).ToHashSet(StringComparer.Ordinal);
        var executedRows = trx.Results.Select(item => item.PhysicalRowLocator).ToHashSet(StringComparer.Ordinal);
        var complete = sourceMethods.SetEquals(discoveredMethods) && discoveredRows.SetEquals(executedRows) &&
            discovered.Count == trx.Results.Count;
        return new GuiTestAcceptanceResult(complete, source.Count, discovered.Count, trx.Results.Count,
            discovery.Duration + execution.Duration);
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
