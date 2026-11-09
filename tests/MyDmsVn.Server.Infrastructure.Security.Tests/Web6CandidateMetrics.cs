using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal enum Web6MetricClass
{
    SalesCreate, SalesUpdate, SalesPost,
    ReceiptCreate, ReceiptUpdate, ReceiptPost,
    AuthRefresh, ProtectedStatus, ProtectedEligibility, StockRead,
}

internal enum Web6MetricOutcome { Success, Error, Timeout }

internal sealed class Web6MetricSample
{
    internal Web6MetricSample(Web6MetricClass metricClass, long startedTimestamp, long endedTimestamp, Web6MetricOutcome outcome)
    {
        MetricClass = metricClass; StartedTimestamp = startedTimestamp; EndedTimestamp = endedTimestamp; Outcome = outcome;
    }
    internal Web6MetricClass MetricClass { get; }
    internal long StartedTimestamp { get; }
    internal long EndedTimestamp { get; }
    internal Web6MetricOutcome Outcome { get; }
}

internal sealed class Web6MetricSummary
{
    internal Web6MetricSummary(int count, double p50Ms, double p95Ms, double p99Ms, int errors, int timeouts,
        double throughputPerSecond, string disposition)
    {
        Count = count; P50Ms = p50Ms; P95Ms = p95Ms; P99Ms = p99Ms; Errors = errors; Timeouts = timeouts;
        ThroughputPerSecond = throughputPerSecond; Disposition = disposition;
    }
    internal int Count { get; }
    internal double P50Ms { get; }
    internal double P95Ms { get; }
    internal double P99Ms { get; }
    internal int Errors { get; }
    internal int Timeouts { get; }
    internal double ThroughputPerSecond { get; }
    internal string Disposition { get; }
}

internal static class Web6CandidateMetrics
{
    internal static IReadOnlyDictionary<Web6MetricClass, Web6MetricSummary> Summarize(
        IEnumerable<Web6MetricSample> samples, long timestampFrequency, long elapsedTicks, int desiredSamples)
    {
        if (samples == null || timestampFrequency <= 0 || elapsedTicks <= 0 || desiredSamples <= 0)
            throw new InvalidDataException("WEB6 measurement inputs are invalid.");
        var materialized = samples.ToArray();
        if (materialized.Any(sample => sample.EndedTimestamp < sample.StartedTimestamp))
            throw new InvalidDataException("WEB6 measurements require a monotonic clock.");
        var elapsedSeconds = (double)elapsedTicks / timestampFrequency;
        return materialized.GroupBy(sample => sample.MetricClass).ToDictionary(group => group.Key, group =>
        {
            var values = group.Select(sample => (sample.EndedTimestamp - sample.StartedTimestamp) * 1000d / timestampFrequency)
                .OrderBy(value => value).ToArray();
            double Rank(double percentile) => values[Math.Max(0, (int)Math.Ceiling(percentile * values.Length) - 1)];
            var errors = group.Count(sample => sample.Outcome == Web6MetricOutcome.Error);
            var timeouts = group.Count(sample => sample.Outcome == Web6MetricOutcome.Timeout);
            return new Web6MetricSummary(values.Length, Rank(.50), Rank(.95), Rank(.99), errors, timeouts,
                values.Length / elapsedSeconds, values.Length >= desiredSamples ? "MEASURED" : "INSUFFICIENT_SAMPLE / NOT MEASURED");
        });
    }
}

internal sealed class Web6WorkloadAdmission
{
    private readonly bool _admitted, _auditHealthy, _artifactsClean, _actorCohort, _sqlOwned;
    private readonly int _workers;
    internal Web6WorkloadAdmission(bool admitted, int workers, bool auditHealthy, bool artifactsClean, bool actorCohort, bool sqlOwned)
    {
        _admitted = admitted; _workers = workers; _auditHealthy = auditHealthy; _artifactsClean = artifactsClean;
        _actorCohort = actorCohort; _sqlOwned = sqlOwned;
    }
    internal void Require()
    {
        if (!_admitted || _workers != 1 || !_auditHealthy || !_artifactsClean || !_actorCohort || !_sqlOwned)
            throw new InvalidDataException("WEB6 workload requires one admitted, healthy, pinned and owned candidate.");
    }
}

internal sealed class Web6WorkloadObservation
{
    internal Web6WorkloadObservation(bool duplicateEffect, int deadlocks, bool resourceRunaway, long journalBytes)
    {
        DuplicateEffect = duplicateEffect; Deadlocks = deadlocks; ResourceRunaway = resourceRunaway; JournalBytes = journalBytes;
    }
    internal bool DuplicateEffect { get; }
    internal int Deadlocks { get; }
    internal bool ResourceRunaway { get; }
    internal long JournalBytes { get; }
}

internal static class Web6CandidateWorkloadGuard
{
    internal static void RequireContinue(Web6WorkloadObservation observation)
    {
        if (observation == null || observation.DuplicateEffect || observation.Deadlocks != 0 || observation.ResourceRunaway || observation.JournalBytes < 0)
            throw new InvalidDataException("WEB6 workload stop rule fired.");
    }
}

internal static class Web6CandidateWorkloadPlan
{
    internal static readonly IReadOnlyList<int> ConcurrencyProfiles = new[] { 1, 4, 8, 16 };
    internal const int DesiredSamplesPerCriticalClass = 1000;
    internal static readonly IReadOnlyList<Web6MetricClass> CriticalClasses = new[]
    {
        Web6MetricClass.SalesCreate, Web6MetricClass.SalesUpdate, Web6MetricClass.SalesPost,
        Web6MetricClass.ReceiptCreate, Web6MetricClass.ReceiptUpdate, Web6MetricClass.ReceiptPost,
    };
}

internal interface IWeb6CandidateWorkloadOperations
{
    Task<Web6WorkloadAdmission> VerifyAdmission(CancellationToken token);
    Task Smoke(IReadOnlyList<Web6MetricClass> criticalClasses, CancellationToken token);
    Task<IReadOnlyDictionary<Web6MetricClass, Web6MetricSummary>> Measure(int concurrency, int desiredSamples, CancellationToken token);
    Task<Web6WorkloadObservation> Observe(CancellationToken token);
    Task Drain(CancellationToken token);
    Task Complete(CancellationToken token);
}

internal sealed class Web6CandidateWorkloadRunner
{
    private readonly IWeb6CandidateWorkloadOperations _operations;
    internal Web6CandidateWorkloadRunner(IWeb6CandidateWorkloadOperations operations) =>
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));

    internal async Task Execute(CancellationToken token)
    {
        var admitted = false;
        try
        {
            (await _operations.VerifyAdmission(token)).Require(); admitted = true;
            await _operations.Smoke(Web6CandidateWorkloadPlan.CriticalClasses, token);
            Web6CandidateWorkloadGuard.RequireContinue(await _operations.Observe(token));
            foreach (var concurrency in Web6CandidateWorkloadPlan.ConcurrencyProfiles)
            {
                var result = await _operations.Measure(concurrency, Web6CandidateWorkloadPlan.DesiredSamplesPerCriticalClass, token);
                if (result == null || Web6CandidateWorkloadPlan.CriticalClasses.Any(metricClass => !result.ContainsKey(metricClass)))
                    throw new InvalidDataException("WEB6 workload omitted a critical class.");
                Web6CandidateWorkloadGuard.RequireContinue(await _operations.Observe(token));
            }
            await _operations.Drain(token); admitted = false;
            await _operations.Complete(token);
        }
        finally
        {
            if (admitted) await _operations.Drain(CancellationToken.None);
        }
    }
}

internal static class Web6WorkloadAcceptanceGate
{
    private static readonly string[] Required =
    {
        "MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY", "MYDMSVN_WEB_TEST_TLS_THUMBPRINT",
        "MYDMSVN_WEB_TEST_BROWSER_DIRECTORY", "MYDMSVN_WEB_PILOT_PROJECT",
        "MYDMSVN_WEB6_CANDIDATE_ROOT", "MYDMSVN_WEB6_CANDIDATE_ID",
        "MYDMSVN_WEB6_WORKLOAD_ROOT", "MYDMSVN_WEB6_WORKLOAD_ACTOR_COHORT",
        "MYDMSVN_WEB6_TASK4_ACCEPTANCE_PATH", "MYDMSVN_SQL_TEST_APPROVAL_FILE",
    };

    internal static IReadOnlyDictionary<string, string?> EnvironmentValues() => Required
        .Concat(new[] { "MYDMSVN_WEB6_WORKLOAD_ENABLE" })
        .ToDictionary(name => name, Environment.GetEnvironmentVariable, StringComparer.OrdinalIgnoreCase);

    internal static string? OrdinarySkipReason(IReadOnlyDictionary<string, string?> values) =>
        values.TryGetValue("MYDMSVN_WEB6_WORKLOAD_ENABLE", out var enabled) && enabled == "1"
            ? null : "WEB6 workload requires explicit bounded nonproduction opt-in.";

    internal static void RequireLive(IReadOnlyDictionary<string, string?> values)
    {
        if (!values.TryGetValue("MYDMSVN_WEB6_WORKLOAD_ENABLE", out var enabled) || enabled != "1")
            throw new InvalidOperationException("BLOCKED: WEB6 workload is not enabled.");
        var missing = Required.Where(name => !values.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value)).ToArray();
        if (missing.Length != 0)
            throw new InvalidOperationException("BLOCKED: WEB6 workload prerequisites are absent: " + string.Join(", ", missing));
    }
}
