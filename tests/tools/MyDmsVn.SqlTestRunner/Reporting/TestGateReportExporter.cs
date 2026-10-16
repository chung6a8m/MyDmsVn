using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Gates;

namespace MyDmsVn.SqlTestRunner.Reporting;

public interface ITestGateReportExporter
{
    Task<TestGateReportExportResult> ExportAsync(
        TestGateReportEvidence evidence,
        CancellationToken cancellationToken);
}
public sealed record TestGateReportExportResult(bool IsComplete, string ReportPath);

public sealed record TestGateReportEvidence(
    string OutputPath,
    string PlanId,
    string TaskId,
    TestGateMode Mode,
    string GateId,
    string Revision,
    string ManifestFingerprint,
    IReadOnlyList<string> LogicalLabels,
    int SourceCount,
    int DiscoveredCount,
    int ExecutedCount,
    int PassedCount,
    int FailedCount,
    int SkippedCount,
    TimeSpan Duration,
    bool TimedOut,
    bool ResourceDenied,
    bool RecoveryRequired,
    bool WatchdogClean,
    bool OwnershipClean,
    bool InventoryComplete,
    IReadOnlyList<string> PrivateValues);

public sealed class TestGateReportExporter : ITestGateReportExporter
{
    private static readonly Regex GuidPattern = new Regex(
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<TestGateReportExportResult> ExportAsync(
        TestGateReportEvidence evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(evidence.OutputPath) ||
            evidence.LogicalLabels.Count == 0 ||
            evidence.LogicalLabels.Any(label =>
                string.IsNullOrWhiteSpace(label) ||
                Path.IsPathFullyQualified(label) ||
                label.Split('/', '\\').Contains("..", StringComparer.Ordinal)))
        {
            return new TestGateReportExportResult(false, string.Empty);
        }

        var document = new SanitizedReport(
            SchemaVersion: 1,
            IdentityVersion: 1,
            evidence.PlanId,
            evidence.TaskId,
            evidence.Mode,
            evidence.GateId,
            evidence.Revision,
            evidence.ManifestFingerprint,
            evidence.LogicalLabels,
            evidence.SourceCount,
            evidence.DiscoveredCount,
            evidence.ExecutedCount,
            evidence.PassedCount,
            evidence.FailedCount,
            evidence.SkippedCount,
            DurationMilliseconds: Math.Max(0, (long)evidence.Duration.TotalMilliseconds),
            evidence.TimedOut,
            evidence.ResourceDenied,
            evidence.RecoveryRequired,
            evidence.WatchdogClean,
            evidence.OwnershipClean,
            evidence.InventoryComplete);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        var json = JsonSerializer.Serialize(document, options);
        if (GuidPattern.IsMatch(json) || evidence.PrivateValues
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Any(value => json.Contains(value, StringComparison.OrdinalIgnoreCase)))
        {
            return new TestGateReportExportResult(false, string.Empty);
        }

        var fullPath = Path.GetFullPath(evidence.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(
                    temporary,
                    json + "\r\n",
                    new UTF8Encoding(false),
                    cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporary, fullPath, overwrite: true);
            return new TestGateReportExportResult(true, fullPath);
        }
        catch (Exception exception) when (
            exception is IOException || exception is UnauthorizedAccessException)
        {
            return new TestGateReportExportResult(false, string.Empty);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private sealed record SanitizedReport(
        int SchemaVersion,
        int IdentityVersion,
        string PlanId,
        string TaskId,
        TestGateMode Mode,
        string GateId,
        string Revision,
        string ManifestFingerprint,
        IReadOnlyList<string> LogicalLabels,
        int SourceCount,
        int DiscoveredCount,
        int ExecutedCount,
        int PassedCount,
        int FailedCount,
        int SkippedCount,
        long DurationMilliseconds,
        bool TimedOut,
        bool ResourceDenied,
        bool RecoveryRequired,
        bool WatchdogClean,
        bool OwnershipClean,
        bool InventoryComplete);
}
