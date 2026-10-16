using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Benchmark;
using MyDmsVn.SqlTestRunner.Inventory;
using MyDmsVn.SqlTestRunner.Trx;

namespace MyDmsVn.SqlTestRunner.Reporting;

public sealed class SqlAcceptanceReportExporter : IBenchmarkReportExporter
{
    private static readonly Regex RevisionPattern = new Regex(
        "^[0-9a-fA-F]{40}$",
        RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public IReadOnlyList<string> ExpectedReportPaths(BenchmarkReportEvidence evidence) =>
        evidence.TargetFrameworks
            .Select(tfm => Path.Combine(
                evidence.ArtifactRoot,
                "sql-pr-acceptance-" + SafeFileComponent(tfm) + ".json"))
            .ToArray();

    public Task<BenchmarkReportExportResult> ExportAsync(
        BenchmarkReportEvidence evidence,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(evidence.Mode, "pr-acceptance", StringComparison.Ordinal) ||
                !RevisionPattern.IsMatch(evidence.Revision) ||
                evidence.RequiredMeasuredPassCount != 1 ||
                evidence.TargetFrameworks.Count == 0)
            {
                return Task.FromResult(BenchmarkReportExportResult.Incomplete());
            }

            var reports = new List<(string Path, SqlAcceptanceReport Report)>();
            foreach (var tfm in evidence.TargetFrameworks)
            {
                var matching = evidence.Runs.Where(run =>
                    run.Kind == BenchmarkPassKind.Acceptance &&
                    run.PassNumber == 1 &&
                    string.Equals(run.Tfm, tfm, StringComparison.Ordinal)).ToArray();
                if (matching.Length != 1)
                {
                    return Task.FromResult(BenchmarkReportExportResult.Incomplete());
                }

                var run = matching[0];
                var discovered = VstestDiscoveryParser.Parse(run.DiscoveryOutput, tfm);
                var trx = TrxResultParser.TryParse(run.TrxPath, tfm);
                if (run.Command.ExitCode != 0 || run.Command.TimedOut ||
                    discovered.Count == 0 || !trx.IsComplete || trx.Results.Count == 0)
                {
                    return Task.FromResult(BenchmarkReportExportResult.Incomplete());
                }

                var passed = trx.Results.Count(row => row.Outcome == TestOutcome.Passed);
                var failed = trx.Results.Count(row => row.Outcome == TestOutcome.Failed);
                var skipped = trx.Results.Count(row => row.Outcome == TestOutcome.Skipped);
                if (failed != 0 || skipped != 0 || passed != trx.Results.Count)
                {
                    return Task.FromResult(BenchmarkReportExportResult.Incomplete());
                }

                reports.Add((
                    Path.Combine(
                        evidence.ArtifactRoot,
                        "sql-pr-acceptance-" + SafeFileComponent(tfm) + ".json"),
                    new SqlAcceptanceReport(
                        "pr-acceptance",
                        evidence.Revision.ToLowerInvariant(),
                        tfm,
                        discovered.Select(testCase => testCase.MethodName)
                            .Distinct(StringComparer.Ordinal)
                            .Count(),
                        discovered.Count,
                        trx.Results.Count,
                        passed,
                        failed,
                        skipped,
                        (decimal)run.Command.Duration.TotalMilliseconds,
                        run.Command.ExitCode,
                        run.Command.TimedOut,
                        "passed",
                        "passed",
                        "passed")));
            }

            Directory.CreateDirectory(evidence.ArtifactRoot);
            foreach (var item in reports)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteAtomic(item.Path, JsonSerializer.Serialize(item.Report, JsonOptions));
            }

            return Task.FromResult(new BenchmarkReportExportResult(
                true,
                reports.Select(item => item.Path).ToArray()));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Task.FromResult(BenchmarkReportExportResult.Incomplete());
        }
    }

    private static string SafeFileComponent(string value) =>
        new string(value.Where(character =>
            char.IsLetterOrDigit(character) || character == '.' || character == '-').ToArray());

    private static void WriteAtomic(string path, string content)
    {
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, content, new UTF8Encoding(false));
        File.Move(temporary, path, true);
    }

    private sealed record SqlAcceptanceReport(
        string Mode,
        string Revision,
        string Tfm,
        int Methods,
        int Discovered,
        int Executed,
        int Passed,
        int Failed,
        int Skipped,
        decimal DurationMs,
        int ExitCode,
        bool TimedOut,
        string Preflight,
        string Watchdog,
        string Ownership);
}
