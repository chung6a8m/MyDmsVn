using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

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
        var groups = materialized.GroupBy(sample => sample.MetricClass).ToDictionary(group => group.Key, group => group.ToArray());
        return Enum.GetValues(typeof(Web6MetricClass)).Cast<Web6MetricClass>().ToDictionary(metricClass => metricClass, metricClass =>
        {
            var group = groups.TryGetValue(metricClass, out var present) ? present : Array.Empty<Web6MetricSample>();
            var values = group.Select(sample => (sample.EndedTimestamp - sample.StartedTimestamp) * 1000d / timestampFrequency)
                .OrderBy(value => value).ToArray();
            double Rank(double percentile) => values.Length == 0 ? 0 : values[Math.Max(0, (int)Math.Ceiling(percentile * values.Length) - 1)];
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
    internal Web6WorkloadObservation(bool duplicateEffect, int deadlocks, bool resourceRunaway, double cpuPercent,
        long privateBytes, int sqlLocks, int poolConnections, long journalBytes)
    {
        DuplicateEffect = duplicateEffect; Deadlocks = deadlocks; ResourceRunaway = resourceRunaway; CpuPercent = cpuPercent;
        PrivateBytes = privateBytes; SqlLocks = sqlLocks; PoolConnections = poolConnections; JournalBytes = journalBytes;
    }
    internal bool DuplicateEffect { get; }
    internal int Deadlocks { get; }
    internal bool ResourceRunaway { get; }
    internal double CpuPercent { get; }
    internal long PrivateBytes { get; }
    internal int SqlLocks { get; }
    internal int PoolConnections { get; }
    internal long JournalBytes { get; }
}

internal static class Web6CandidateWorkloadGuard
{
    internal static void RequireContinue(Web6WorkloadObservation observation)
    {
        if (observation == null || observation.DuplicateEffect || observation.Deadlocks != 0 || observation.ResourceRunaway ||
            double.IsNaN(observation.CpuPercent) || double.IsInfinity(observation.CpuPercent) || observation.CpuPercent < 0 ||
            observation.PrivateBytes < 0 || observation.SqlLocks < 0 || observation.PoolConnections < 0 || observation.JournalBytes < 0)
            throw new InvalidDataException("WEB6 workload stop rule fired.");
    }
}

internal static class Web6CandidateWorkloadPlan
{
    internal static readonly IReadOnlyList<int> ConcurrencyProfiles = new[] { 1, 4, 8, 16 };
    internal const int DesiredSamplesPerCriticalClass = 1000;
    internal const int ProfileBudgetSeconds = 60;
    internal static readonly IReadOnlyList<Web6MetricClass> CriticalClasses = new[]
    {
        Web6MetricClass.SalesCreate, Web6MetricClass.SalesUpdate, Web6MetricClass.SalesPost,
        Web6MetricClass.ReceiptCreate, Web6MetricClass.ReceiptUpdate, Web6MetricClass.ReceiptPost,
    };
    internal static readonly IReadOnlyList<Web6MetricClass> SupportingClasses = new[]
    {
        Web6MetricClass.AuthRefresh, Web6MetricClass.ProtectedStatus, Web6MetricClass.ProtectedEligibility, Web6MetricClass.StockRead,
    };
    internal static readonly IReadOnlyList<Web6MetricClass> RequiredClasses = CriticalClasses.Concat(SupportingClasses).ToArray();
}

internal sealed class Web6WorkloadHealth
{
    internal Web6WorkloadHealth(bool endpointHealthy, bool candidateBound, int workers, bool auditReady)
    { EndpointHealthy = endpointHealthy; CandidateBound = candidateBound; Workers = workers; AuditReady = auditReady; }
    internal bool EndpointHealthy { get; }
    internal bool CandidateBound { get; }
    internal int Workers { get; }
    internal bool AuditReady { get; }
    internal void Require()
    {
        if (!EndpointHealthy || !CandidateBound || Workers != 1 || !AuditReady)
            throw new InvalidDataException("WEB6 workload health degraded; stop and retain candidate resources.");
    }
}

internal static class Web6BoundedSampleLoop
{
    internal static async Task Execute(int concurrency, int desiredSamples, long frequency, long budgetTicks,
        Func<long> timestamp, Func<int, Task<int>> batch, Func<Task> guard)
    {
        if (concurrency <= 0 || desiredSamples <= 0 || frequency <= 0 || budgetTicks <= 0 || timestamp == null || batch == null || guard == null)
            throw new InvalidDataException("WEB6 workload sampling budget is invalid.");
        var started = timestamp(); var last = started; var completed = 0;
        while (completed < desiredSamples)
        {
            var now = timestamp();
            if (now < last) throw new InvalidDataException("WEB6 workload profile clock moved backwards.");
            last = now;
            if (now - started >= budgetTicks) break;
            var size = Math.Min(concurrency, desiredSamples - completed);
            var observed = await batch(size);
            if (observed != size) throw new InvalidDataException("WEB6 workload batch did not produce the requested per-class observations.");
            completed += observed;
            await guard();
        }
    }
}

internal interface IWeb6CandidateWorkloadOperations
{
    Task<Web6WorkloadAdmission> VerifyAdmission(CancellationToken token);
    Task<Web6WorkloadHealth> Health(CancellationToken token);
    Task<IReadOnlyDictionary<Web6MetricClass, Web6MetricSummary>> Smoke(IReadOnlyList<Web6MetricClass> criticalClasses, CancellationToken token);
    Task<IReadOnlyDictionary<Web6MetricClass, Web6MetricSummary>> Measure(int concurrency, int desiredSamples, CancellationToken token);
    Task<Web6WorkloadObservation> Observe(CancellationToken token);
    Task SealForOuterCleanup(bool completed, CancellationToken token);
    Task Complete(IReadOnlyList<Web6WorkloadProfileResult> profiles, CancellationToken token);
}

internal sealed class Web6WorkloadProfileResult
{
    internal Web6WorkloadProfileResult(int concurrency, IReadOnlyDictionary<Web6MetricClass, Web6MetricSummary> metrics,
        Web6WorkloadObservation resources)
    { Concurrency = concurrency; Metrics = metrics; Resources = resources; }
    internal int Concurrency { get; }
    internal IReadOnlyDictionary<Web6MetricClass, Web6MetricSummary> Metrics { get; }
    internal Web6WorkloadObservation Resources { get; }
}

internal sealed class Web6CandidateWorkloadRunner
{
    private readonly IWeb6CandidateWorkloadOperations _operations;
    internal Web6CandidateWorkloadRunner(IWeb6CandidateWorkloadOperations operations) =>
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));

    internal async Task Execute(CancellationToken token)
    {
        var admitted = false;
        var profiles = new List<Web6WorkloadProfileResult>();
        try
        {
            (await _operations.VerifyAdmission(token)).Require(); admitted = true;
            (await _operations.Health(token)).Require();
            var smoke = await _operations.Smoke(Web6CandidateWorkloadPlan.RequiredClasses, token);
            RequireComplete(smoke);
            var smokeResources = await _operations.Observe(token); Web6CandidateWorkloadGuard.RequireContinue(smokeResources);
            (await _operations.Health(token)).Require();
            profiles.Add(new Web6WorkloadProfileResult(0, smoke, smokeResources));
            foreach (var concurrency in Web6CandidateWorkloadPlan.ConcurrencyProfiles)
            {
                var result = await _operations.Measure(concurrency, Web6CandidateWorkloadPlan.DesiredSamplesPerCriticalClass, token);
                RequireComplete(result);
                var resources = await _operations.Observe(token); Web6CandidateWorkloadGuard.RequireContinue(resources);
                (await _operations.Health(token)).Require();
                profiles.Add(new Web6WorkloadProfileResult(concurrency, result, resources));
            }
            await _operations.SealForOuterCleanup(completed: true, token); admitted = false;
            await _operations.Complete(profiles, token);
        }
        finally
        {
            if (admitted) await _operations.SealForOuterCleanup(completed: false, CancellationToken.None);
        }
    }

    private static void RequireComplete(IReadOnlyDictionary<Web6MetricClass, Web6MetricSummary> result)
    {
        if (result == null || Web6CandidateWorkloadPlan.RequiredClasses.Any(metricClass => !result.ContainsKey(metricClass)) ||
            result.Values.Any(summary => summary.Disposition != "MEASURED" && summary.Disposition != "INSUFFICIENT_SAMPLE / NOT MEASURED"))
            throw new InvalidDataException("WEB6 workload omitted or mislabeled a required class.");
    }
}

internal sealed class Web6RuntimeRootCleanupState
{
    internal bool StartAttempted { get; private set; }
    internal bool WorkerExitVerified { get; private set; }
    internal bool QueueAbsenceVerified { get; private set; }
    internal void MarkStartAttempted() => StartAttempted = true;
    internal void MarkWorkerExitVerified()
    {
        if (!StartAttempted) throw new InvalidOperationException("Worker exit cannot precede start attempt.");
        WorkerExitVerified = true;
    }
    internal void MarkQueueAbsenceVerified()
    {
        if (!WorkerExitVerified) throw new InvalidOperationException("Queue absence cannot precede verified worker exit.");
        QueueAbsenceVerified = true;
    }
    internal bool CanDelete(bool safetyPoisoned) =>
        StartAttempted && WorkerExitVerified && QueueAbsenceVerified && !safetyPoisoned;

    internal bool Release(bool safetyPoisoned, Action delete, Action dispose)
    {
        if (delete == null || dispose == null) throw new ArgumentNullException();
        try
        {
            if (!CanDelete(safetyPoisoned)) return false;
            delete(); return true;
        }
        finally { dispose(); }
    }
}

internal static class Web6OuterCleanupCoordinator
{
    internal static async Task<bool> Execute(bool admitted, Func<Task> deleteRuntime, Func<Task> cleanupSql,
        Func<Task> deleteWorkload, Action releaseRuntime, Action releaseWorkload)
    {
        if (deleteRuntime == null || cleanupSql == null || deleteWorkload == null || releaseRuntime == null || releaseWorkload == null)
            throw new ArgumentNullException();
        try
        {
            if (!admitted) return false;
            await deleteRuntime();
            await cleanupSql();
            await deleteWorkload();
            return true;
        }
        finally
        {
            releaseWorkload();
            releaseRuntime();
        }
    }
}

internal static class Web6WorkloadAcceptanceGate
{
    private static readonly string[] Required =
    {
        "MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY", "MYDMSVN_WEB_TEST_TLS_THUMBPRINT",
        "MYDMSVN_WEB_TEST_BROWSER_DIRECTORY", "MYDMSVN_WEB_PILOT_PROJECT",
        "MYDMSVN_WEB6_CANDIDATE_ENABLE", "MYDMSVN_WEB6_CANDIDATE_ROOT", "MYDMSVN_WEB6_CANDIDATE_ID",
        "MYDMSVN_WEB6_WORKLOAD_ROOT", "MYDMSVN_WEB6_WORKLOAD_ACTOR_COHORT",
        "MYDMSVN_WEB6_TASK4_ACCEPTANCE_PATH", "MYDMSVN_WEB6_TASK4_ACCEPTANCE_SHA256",
        "MYDMSVN_SQL_TEST_APPROVAL_FILE", "MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING",
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

internal sealed class Web6WorkloadTask4Receipt
{
    private Web6WorkloadTask4Receipt(string candidateId, string backendSha, string frontendSha,
        string frontendDistHash, string sqlReceiptHash, string manifestHash)
    {
        CandidateId = candidateId; BackendSha = backendSha; FrontendSha = frontendSha; FrontendDistHash = frontendDistHash;
        SqlReceiptHash = sqlReceiptHash; ManifestHash = manifestHash;
    }
    internal string CandidateId { get; }
    internal string BackendSha { get; }
    internal string FrontendSha { get; }
    internal string FrontendDistHash { get; }
    internal string SqlReceiptHash { get; }
    internal string ManifestHash { get; }

    internal static Web6WorkloadTask4Receipt ReadProtected(string path, string expectedDigest)
    {
        path = Web6CandidateFileSystem.CanonicalPath(path);
        Web6CandidateFileSystem.RequireRegularFile(path);
        using var identity = WindowsIdentity.GetCurrent();
        var acl = File.GetAccessControl(path, SqlTestPrivateRecord.Sections);
        Web6CandidateManifest.RequirePrivateOwner(acl, identity.User!);
        var bytes = File.ReadAllBytes(path);
        if (SqlTestPrivateRecord.Hash(bytes) != Web6CandidateInputs.RequireHash(expectedDigest))
            throw new InvalidDataException("Task4 acceptance receipt digest changed.");
        using var reader = new Newtonsoft.Json.JsonTextReader(new StringReader(new UTF8Encoding(false, true).GetString(bytes)))
        { DateParseHandling = Newtonsoft.Json.DateParseHandling.None, MaxDepth = 8 };
        var json = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        var expected = new[] { "version", "mode", "status", "candidateId", "backendSha", "frontendSha",
            "frontendDistManifestSha256", "sqlOwnershipReceiptSha256", "manifestSha256", "completedAtUtc" };
        if (!json.Properties().Select(property => property.Name).OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(expected.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal) ||
            (int?)json["version"] != 1 || (string?)json["mode"] != "web6-cutover" || (string?)json["status"] != "ACCEPTED" ||
            !DateTimeOffset.TryParse((string?)json["completedAtUtc"], out var completed) || completed > DateTimeOffset.UtcNow ||
            completed < DateTimeOffset.UtcNow.AddDays(-7))
            throw new InvalidDataException("Task4 acceptance receipt is not a recent exact accepted record.");
        var candidate = (string?)json["candidateId"];
        if (candidate == null || candidate.Length is < 1 or > 64 || candidate.Any(c => !(char.IsLetterOrDigit(c) || c is '.' or '_' or '-')))
            throw new InvalidDataException("Task4 candidate identity is invalid.");
        return new Web6WorkloadTask4Receipt(candidate,
            Web6CandidateInputs.RequireHash((string?)json["backendSha"] ?? "", 40),
            Web6CandidateInputs.RequireHash((string?)json["frontendSha"] ?? "", 40),
            Web6CandidateInputs.RequireHash((string?)json["frontendDistManifestSha256"] ?? ""),
            Web6CandidateInputs.RequireHash((string?)json["sqlOwnershipReceiptSha256"] ?? ""),
            Web6CandidateInputs.RequireHash((string?)json["manifestSha256"] ?? ""));
    }
}
