using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CandidateWorkloadTests
{
    [Fact]
    public void Private_directory_lease_is_create_new_pinned_private_and_handle_deleted()
    {
        using var fixture = new PrivateParent();
        var path = Path.Combine(fixture.Path, "leased");
        using var lease = Web6PrivateDirectoryLease.CreateNew(path);
        Assert.True(Directory.Exists(path));
        lease.Verify();
        Assert.ThrowsAny<IOException>(() => Directory.Move(path, path + "-moved"));
        lease.DeleteVerified();
        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public void Private_directory_lease_refuses_reparse_ancestor_before_create()
    {
        using var fixture = new PrivateParent();
        var target = Directory.CreateDirectory(Path.Combine(fixture.Path, "target")).FullName;
        var junction = Path.Combine(fixture.Path, "junction");
        using var command = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            "/d /c mklink /J \"" + junction + "\" \"" + target + "\"") { UseShellExecute = false, CreateNoWindow = true });
        Assert.NotNull(command); Assert.True(command!.WaitForExit(5000)); Assert.Equal(0, command.ExitCode);
        try
        {
            Assert.Throws<IOException>(() => Web6PrivateDirectoryLease.CreateNew(Path.Combine(junction, "refused")));
            Assert.Empty(Directory.GetFileSystemEntries(target));
        }
        finally { Directory.Delete(junction); }
    }

    [Fact]
    public void Private_directory_lease_refuses_outstanding_delete_authority_on_parent()
    {
        using var fixture = new PrivateParent();
        const uint deleteAndList = 0x10001, shareAll = 7, openExisting = 3, openDirectory = 0x02000000;
        using (var conflicting = OpenTestDirectory(fixture.Path, deleteAndList, shareAll, IntPtr.Zero, openExisting, openDirectory, IntPtr.Zero))
        {
            Assert.False(conflicting.IsInvalid);
            Assert.Throws<IOException>(() => Web6PrivateDirectoryLease.CreateNew(Path.Combine(fixture.Path, "refused")));
        }
        using var lease = Web6PrivateDirectoryLease.CreateNew(Path.Combine(fixture.Path, "accepted"));
        lease.DeleteVerified();
    }

    [Fact]
    public void Private_directory_cleanup_refuses_acl_drift_and_retains_root()
    {
        using var fixture = new PrivateParent();
        var path = Path.Combine(fixture.Path, "leased");
        using var lease = Web6PrivateDirectoryLease.CreateNew(path);
        var original = Directory.GetAccessControl(path, SqlTestPrivateRecord.Sections);
        var broad = SqlTestPrivateRecord.RestoreDirectory(original.GetSecurityDescriptorBinaryForm());
        broad.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            FileSystemRights.Read, AccessControlType.Allow));
        Directory.SetAccessControl(path, broad);
        Assert.Throws<IOException>(() => lease.DeleteVerified());
        Assert.True(Directory.Exists(path));
        // A root whose descriptor drifted is retained; it is never normalized and reused by the candidate.
    }

    [Fact]
    public void Private_directory_tree_cleanup_deletes_owned_children_but_refuses_reparse_children()
    {
        using var fixture = new PrivateParent();
        var path = Path.Combine(fixture.Path, "leased");
        using (var lease = Web6PrivateDirectoryLease.CreateNew(path))
        {
            var nested = Directory.CreateDirectory(Path.Combine(path, "nested")).FullName;
            File.WriteAllText(Path.Combine(nested, "owned.txt"), "owned");
            lease.DeleteTreeVerified();
            Assert.False(Directory.Exists(path));
        }

        var outside = Directory.CreateDirectory(Path.Combine(fixture.Path, "outside")).FullName;
        using var refused = Web6PrivateDirectoryLease.CreateNew(path);
        var junction = Path.Combine(path, "junction");
        using var command = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            "/d /c mklink /J \"" + junction + "\" \"" + outside + "\"") { UseShellExecute = false, CreateNoWindow = true });
        Assert.NotNull(command); Assert.True(command!.WaitForExit(5000)); Assert.Equal(0, command.ExitCode);
        Assert.Throws<IOException>(() => refused.DeleteTreeVerified());
        Assert.True(Directory.Exists(path)); Assert.Empty(Directory.GetFileSystemEntries(outside));
        Directory.Delete(junction);
    }

    [Fact]
    public void Private_directory_cleanup_refuses_child_swapped_to_junction_between_enumeration_and_open()
    {
        using var fixture = new PrivateParent();
        var path = Path.Combine(fixture.Path, "leased");
        var outside = Directory.CreateDirectory(Path.Combine(fixture.Path, "outside-race")).FullName;
        using var lease = Web6PrivateDirectoryLease.CreateNew(path);
        var victim = Directory.CreateDirectory(Path.Combine(path, "victim")).FullName;
        var evidence = Path.Combine(path, "a-evidence.log");
        File.WriteAllText(evidence, "retain on junction race");
        var swapped = false;
        Assert.Throws<IOException>(() => lease.DeleteTreeVerified(entry =>
        {
            if (swapped || !string.Equals(entry, victim, StringComparison.OrdinalIgnoreCase)) return;
            Directory.Delete(victim); swapped = true;
            using var command = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                "/d /c mklink /J \"" + victim + "\" \"" + outside + "\"") { UseShellExecute = false, CreateNoWindow = true });
            Assert.NotNull(command); Assert.True(command!.WaitForExit(5000)); Assert.Equal(0, command.ExitCode);
        }));
        Assert.True(swapped); Assert.True(Directory.Exists(path)); Assert.Empty(Directory.GetFileSystemEntries(outside));
        Assert.Equal("retain on junction race", File.ReadAllText(evidence));
        Directory.Delete(victim);
    }

    [Fact]
    public void Cleanup_preflight_refuses_replaced_regular_child_even_with_preserved_creation_time()
    {
        using var fixture = new PrivateParent();
        using var lease = Web6PrivateDirectoryLease.CreateNew(Path.Combine(fixture.Path, "leased"));
        var victim = Path.Combine(lease.Path, "victim.log");
        File.WriteAllText(victim, "original");
        var created = File.GetCreationTimeUtc(victim);
        var swapped = false;
        Assert.Throws<IOException>(() => lease.PrepareCleanup(beforeChildOpen: path =>
        {
            if (swapped || path != victim) return;
            File.Delete(victim); File.WriteAllText(victim, "replacement"); File.SetCreationTimeUtc(victim, created); swapped = true;
        }));
        Assert.True(swapped);
        Assert.Equal("replacement", File.ReadAllText(victim));
    }

    [Fact]
    public void Cleanup_preflight_retains_earlier_evidence_when_later_child_is_not_deletable()
    {
        using var fixture = new PrivateParent();
        using var lease = Web6PrivateDirectoryLease.CreateNew(Path.Combine(fixture.Path, "leased"));
        var evidence = Path.Combine(lease.Path, "a-evidence.log");
        var blocked = Path.Combine(lease.Path, "z-readonly.log");
        File.WriteAllText(evidence, "retain"); File.WriteAllText(blocked, "retain too");
        File.SetAttributes(blocked, FileAttributes.ReadOnly);
        try
        {
            Assert.Throws<IOException>(() => lease.DeleteTreeVerified());
            Assert.Equal("retain", File.ReadAllText(evidence));
            Assert.Equal("retain too", File.ReadAllText(blocked));
        }
        finally { File.SetAttributes(blocked, FileAttributes.Normal); }
    }

    [Fact]
    public void Cleanup_preflight_pins_all_children_until_disposition_and_rejects_path_swap()
    {
        using var fixture = new PrivateParent();
        using var lease = Web6PrivateDirectoryLease.CreateNew(Path.Combine(fixture.Path, "leased"));
        var nested = Directory.CreateDirectory(Path.Combine(lease.Path, "nested")).FullName;
        var evidence = Path.Combine(nested, "evidence.log"); File.WriteAllText(evidence, "retain");
        using (var cleanup = lease.PrepareCleanup())
        {
            Assert.ThrowsAny<IOException>(() => Directory.Move(nested, nested + "-swapped"));
            Assert.ThrowsAny<IOException>(() => File.Delete(evidence));
            cleanup.Verify();
        }
        Assert.Equal("retain", File.ReadAllText(evidence));
        lease.DeleteTreeVerified();
        Assert.False(Directory.Exists(lease.Path));
    }

    [Fact]
    public void Metrics_use_nearest_rank_and_keep_classes_isolated()
    {
        var samples = new[] { 1L, 2, 3, 4, 100 }.Select(value => Sample(Web6MetricClass.SalesCreate, value)).ToList();
        samples.Add(Sample(Web6MetricClass.SalesUpdate, 50));
        var result = Web6CandidateMetrics.Summarize(samples, timestampFrequency: 1000, elapsedTicks: 1000, desiredSamples: 5);
        Assert.Equal(3, result[Web6MetricClass.SalesCreate].P50Ms);
        Assert.Equal(100, result[Web6MetricClass.SalesCreate].P95Ms);
        Assert.Equal(100, result[Web6MetricClass.SalesCreate].P99Ms);
        Assert.Equal(50, result[Web6MetricClass.SalesUpdate].P50Ms);
        Assert.Equal("MEASURED", result[Web6MetricClass.SalesCreate].Disposition);
        Assert.Equal("INSUFFICIENT_SAMPLE / NOT MEASURED", result[Web6MetricClass.SalesUpdate].Disposition);
        Assert.Equal(0, result[Web6MetricClass.AuthRefresh].Count);
        Assert.Equal("INSUFFICIENT_SAMPLE / NOT MEASURED", result[Web6MetricClass.AuthRefresh].Disposition);
    }

    [Fact]
    public void Metrics_count_errors_timeouts_throughput_and_refuse_nonmonotonic_time()
    {
        var samples = new[]
        {
            Sample(Web6MetricClass.ReceiptPost, 10),
            Sample(Web6MetricClass.ReceiptPost, 20, Web6MetricOutcome.Error),
            Sample(Web6MetricClass.ReceiptPost, 30, Web6MetricOutcome.Timeout),
        };
        var result = Web6CandidateMetrics.Summarize(samples, 1000, 2000, 3)[Web6MetricClass.ReceiptPost];
        Assert.Equal(3, result.Count); Assert.Equal(1, result.Errors); Assert.Equal(1, result.Timeouts);
        Assert.Equal(1.5, result.ThroughputPerSecond, 6);
        Assert.Throws<InvalidDataException>(() => Web6CandidateMetrics.Summarize(
            new[] { new Web6MetricSample(Web6MetricClass.SalesPost, 2, 1, Web6MetricOutcome.Success) }, 1000, 1, 1));
    }

    [Theory]
    [InlineData(false, 1, true, true, true, true)]
    [InlineData(true, 2, true, true, true, true)]
    [InlineData(true, 1, false, true, true, true)]
    [InlineData(true, 1, true, false, true, true)]
    [InlineData(true, 1, true, true, false, true)]
    [InlineData(true, 1, true, true, true, false)]
    public void Workload_refuses_unadmitted_or_unowned_candidate(bool admitted, int workers, bool audit, bool artifact, bool cohort, bool sql)
    {
        Assert.Throws<InvalidDataException>(() => new Web6WorkloadAdmission(admitted, workers, audit, artifact, cohort, sql).Require());
    }

    [Theory]
    [InlineData(true, 0, false)]
    [InlineData(false, 1, false)]
    [InlineData(false, 0, true)]
    public void Workload_stops_on_duplicate_deadlock_or_resource_runaway(bool duplicate, int deadlocks, bool runaway)
    {
        Assert.Throws<InvalidDataException>(() => Web6CandidateWorkloadGuard.RequireContinue(
            new Web6WorkloadObservation(duplicate, deadlocks, runaway, 10, 1024, 1, 1, journalBytes: 1)));
    }

    [Fact]
    public void Workload_profiles_are_serial_bounded_and_keep_six_critical_classes_separate()
    {
        Assert.Equal(new[] { 1, 4, 8, 16 }, Web6CandidateWorkloadPlan.ConcurrencyProfiles);
        Assert.Equal(1000, Web6CandidateWorkloadPlan.DesiredSamplesPerCriticalClass);
        Assert.Equal(6, Web6CandidateWorkloadPlan.CriticalClasses.Count);
        Assert.Equal(6, Web6CandidateWorkloadPlan.CriticalClasses.Distinct().Count());
        Assert.Equal(4, Web6CandidateWorkloadPlan.SupportingClasses.Count);
        Assert.Equal(10, Web6CandidateWorkloadPlan.RequiredClasses.Distinct().Count());
        Assert.DoesNotContain(Web6MetricClass.AuthRefresh, Web6CandidateWorkloadPlan.CriticalClasses);
    }

    [Fact]
    public async Task Workload_runner_is_serial_smoke_then_profiles_and_drains_on_failure()
    {
        var operations = new RecordingWorkloadOperations();
        await new Web6CandidateWorkloadRunner(operations).Execute(CancellationToken.None);
        Assert.Equal(new[] { "admit", "health", "smoke", "observe", "health", "measure:1", "observe", "health",
            "measure:4", "observe", "health", "measure:8", "observe", "health", "measure:16", "observe", "health", "seal", "complete" }, operations.Calls);
        Assert.Equal(new[] { 0, 1, 4, 8, 16 }, operations.CompletedProfiles!.Select(profile => profile.Concurrency));
        Assert.All(operations.CompletedProfiles!, profile => Assert.Equal(10, profile.Metrics.Count));

        operations = new RecordingWorkloadOperations { FailAtConcurrency = 4 };
        await Assert.ThrowsAsync<IOException>(() => new Web6CandidateWorkloadRunner(operations).Execute(CancellationToken.None));
        Assert.Equal(1, operations.Calls.Count(call => call == "seal"));
        Assert.DoesNotContain("complete", operations.Calls);
    }

    [Fact]
    public async Task Workload_runner_refuses_omitted_supporting_class_and_drains()
    {
        var operations = new RecordingWorkloadOperations { OmitClass = Web6MetricClass.ProtectedEligibility };
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new Web6CandidateWorkloadRunner(operations).Execute(CancellationToken.None));
        Assert.Contains("omitted", error.Message);
        Assert.Equal(new[] { "admit", "health", "smoke", "seal" }, operations.Calls);
    }

    [Fact]
    public async Task Bounded_sample_loop_attempts_target_in_concurrency_batches_and_stops_on_monotonic_budget()
    {
        long now = 0; var batches = new List<int>(); var guards = 0;
        await Web6BoundedSampleLoop.Execute(4, 10, 1000, 100, () => now,
            size => { batches.Add(size); now += 20; return Task.FromResult(size); },
            () => { guards++; return Task.CompletedTask; });
        Assert.Equal(new[] { 4, 4, 2 }, batches); Assert.Equal(3, guards);

        now = 0; batches.Clear(); guards = 0;
        await Web6BoundedSampleLoop.Execute(4, 1000, 1000, 50, () => now,
            size => { batches.Add(size); now += 30; return Task.FromResult(size); },
            () => { guards++; return Task.CompletedTask; });
        Assert.Equal(new[] { 4, 4 }, batches); Assert.Equal(2, guards);

        var clock = new Queue<long>(new[] { 10L, 11L, 9L });
        await Assert.ThrowsAsync<InvalidDataException>(() => Web6BoundedSampleLoop.Execute(1, 2, 1000, 100,
            () => clock.Dequeue(), _ => Task.FromResult(1), () => Task.CompletedTask));
    }

    [Fact]
    public async Task Workload_runner_stops_and_drains_when_health_flips_between_profiles()
    {
        var operations = new RecordingWorkloadOperations { UnhealthyAtHealthCall = 3 };
        await Assert.ThrowsAsync<InvalidDataException>(() => new Web6CandidateWorkloadRunner(operations).Execute(CancellationToken.None));
        Assert.Equal(new[] { "admit", "health", "smoke", "observe", "health", "measure:1", "observe", "health", "seal" }, operations.Calls);
        Assert.Null(operations.CompletedProfiles);
    }

    [Fact]
    public async Task Workload_runner_refuses_foreign_second_worker_between_profiles()
    {
        var operations = new RecordingWorkloadOperations { ForeignWorkerAtHealthCall = 3 };
        await Assert.ThrowsAsync<InvalidDataException>(() => new Web6CandidateWorkloadRunner(operations).Execute(CancellationToken.None));
        Assert.Equal(new[] { "admit", "health", "smoke", "observe", "health", "measure:1", "observe", "health", "seal" }, operations.Calls);
        Assert.Null(operations.CompletedProfiles);
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void Runtime_root_cleanup_requires_start_exit_and_queue_absence(bool started, bool exited, bool absent, bool expected)
    {
        var state = new Web6RuntimeRootCleanupState();
        if (started) state.MarkStartAttempted();
        if (exited) state.MarkWorkerExitVerified();
        if (absent) state.MarkQueueAbsenceVerified();
        Assert.Equal(expected, state.CanDelete(safetyPoisoned: false));
        Assert.False(state.CanDelete(safetyPoisoned: true));
    }

    [Fact]
    public void Runtime_root_cleanup_failure_injection_retains_path_and_always_releases_handles()
    {
        using var fixture = new PrivateParent(); var retained = Path.Combine(fixture.Path, "retained-runtime");
        var lease = Web6PrivateDirectoryLease.CreateNew(retained);
        var incomplete = new Web6RuntimeRootCleanupState(); var deleted = false; var disposed = false;
        Assert.False(incomplete.Release(false, () => { deleted = true; lease.DeleteTreeVerified(); }, () => { disposed = true; lease.Dispose(); }));
        Assert.False(deleted); Assert.True(disposed); Assert.True(Directory.Exists(retained));

        var complete = new Web6RuntimeRootCleanupState(); complete.MarkStartAttempted();
        complete.MarkWorkerExitVerified(); complete.MarkQueueAbsenceVerified(); disposed = false;
        Assert.Throws<IOException>(() => complete.Release(false, () => throw new IOException("delete injected"), () => disposed = true));
        Assert.True(disposed); Assert.True(complete.CanDelete(false));
    }

    [Fact]
    public async Task Outer_cleanup_keeps_workload_root_until_verified_drain_and_orders_runtime_sql_workload()
    {
        using var fixture = new PrivateParent(); var path = Path.Combine(fixture.Path, "workload");
        var retainedLease = Web6PrivateDirectoryLease.CreateNew(path);
        Assert.False(await Web6OuterCleanupCoordinator.Execute(false,
            () => throw new InvalidOperationException("runtime delete must not run"),
            () => throw new InvalidOperationException("SQL cleanup must not run"),
            () => throw new InvalidOperationException("workload delete must not run"),
            () => { }, retainedLease.Dispose));
        Assert.True(Directory.Exists(path));

        var calls = new List<string>(); var lease = Web6PrivateDirectoryLease.CreateNew(Path.Combine(fixture.Path, "admitted-workload"));
        Assert.True(await Web6OuterCleanupCoordinator.Execute(true,
            () => { calls.Add("runtime"); Assert.True(Directory.Exists(lease.Path)); return Task.CompletedTask; },
            () => { calls.Add("sql"); Assert.True(Directory.Exists(lease.Path)); return Task.CompletedTask; },
            () => { calls.Add("workload"); lease.DeleteTreeVerified(); return Task.CompletedTask; },
            () => calls.Add("release-runtime"), () => { calls.Add("release-workload"); lease.Dispose(); }));
        Assert.Equal(new[] { "runtime", "sql", "workload", "release-workload", "release-runtime" }, calls);
        Assert.False(Directory.Exists(lease.Path));
    }

    [Fact]
    public async Task Outer_cleanup_late_sql_failure_releases_handles_and_retains_workload_evidence()
    {
        using var fixture = new PrivateParent(); var path = Path.Combine(fixture.Path, "failed-workload");
        var lease = Web6PrivateDirectoryLease.CreateNew(path); var calls = new List<string>();
        await Assert.ThrowsAsync<IOException>(() => Web6OuterCleanupCoordinator.Execute(true,
            () => { calls.Add("runtime"); return Task.CompletedTask; },
            () => { calls.Add("sql"); throw new IOException("injected SQL cleanup failure"); },
            () => { calls.Add("workload"); lease.DeleteTreeVerified(); return Task.CompletedTask; },
            () => calls.Add("release-runtime"), () => { calls.Add("release-workload"); lease.Dispose(); }));
        Assert.Equal(new[] { "runtime", "sql", "release-workload", "release-runtime" }, calls);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void Workload_acceptance_gate_is_non_success_when_explicit_prerequisites_are_missing()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        { ["MYDMSVN_WEB6_WORKLOAD_ENABLE"] = "1" };
        Assert.Null(Web6WorkloadAcceptanceGate.OrdinarySkipReason(values));
        var error = Assert.Throws<InvalidOperationException>(() => Web6WorkloadAcceptanceGate.RequireLive(values));
        Assert.Contains("BLOCKED", error.Message); Assert.Contains("MYDMSVN_WEB6_TASK4_ACCEPTANCE_PATH", error.Message);
    }

    private static Web6MetricSample Sample(Web6MetricClass metricClass, long milliseconds,
        Web6MetricOutcome outcome = Web6MetricOutcome.Success) => new Web6MetricSample(metricClass, 0, milliseconds, outcome);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle OpenTestDirectory(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    private sealed class PrivateParent : IDisposable
    {
        internal PrivateParent()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "web6-private-root-" + Guid.NewGuid().ToString("N"));
            using var identity = WindowsIdentity.GetCurrent();
            Directory.CreateDirectory(Path, SqlTestPrivateRecord.DirectoryAcl(identity.User!));
        }
        internal string Path { get; }
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }

    private sealed class RecordingWorkloadOperations : IWeb6CandidateWorkloadOperations
    {
        internal readonly List<string> Calls = new List<string>();
        internal int FailAtConcurrency { get; set; }
        internal Web6MetricClass? OmitClass { get; set; }
        internal int UnhealthyAtHealthCall { get; set; }
        internal int ForeignWorkerAtHealthCall { get; set; }
        private int _healthCalls;
        internal IReadOnlyList<Web6WorkloadProfileResult>? CompletedProfiles { get; private set; }
        public Task<Web6WorkloadAdmission> VerifyAdmission(CancellationToken token)
        { Calls.Add("admit"); return Task.FromResult(new Web6WorkloadAdmission(true, 1, true, true, true, true)); }
        public Task<Web6WorkloadHealth> Health(CancellationToken token)
        {
            Calls.Add("health"); _healthCalls++;
            var foreign = _healthCalls == ForeignWorkerAtHealthCall;
            return Task.FromResult(new Web6WorkloadHealth(_healthCalls != UnhealthyAtHealthCall, !foreign, foreign ? 2 : 1, true));
        }
        public Task<IReadOnlyDictionary<Web6MetricClass, Web6MetricSummary>> Smoke(IReadOnlyList<Web6MetricClass> criticalClasses, CancellationToken token)
        { Calls.Add("smoke"); Assert.Equal(10, criticalClasses.Count); return CompleteMetrics(OmitClass); }
        public Task<IReadOnlyDictionary<Web6MetricClass, Web6MetricSummary>> Measure(int concurrency, int desiredSamples, CancellationToken token)
        {
            Calls.Add("measure:" + concurrency); if (concurrency == FailAtConcurrency) throw new IOException("measurement failed");
            return CompleteMetrics(OmitClass);
        }
        public Task<Web6WorkloadObservation> Observe(CancellationToken token)
        { Calls.Add("observe"); return Task.FromResult(new Web6WorkloadObservation(false, 0, false, 10, 1024, 1, 1, 1)); }
        public Task SealForOuterCleanup(bool completed, CancellationToken token) { Calls.Add("seal"); return Task.CompletedTask; }
        public Task Complete(IReadOnlyList<Web6WorkloadProfileResult> profiles, CancellationToken token)
        { Calls.Add("complete"); CompletedProfiles = profiles; return Task.CompletedTask; }
        private static Task<IReadOnlyDictionary<Web6MetricClass, Web6MetricSummary>> CompleteMetrics(Web6MetricClass? omit = null)
        {
            IReadOnlyDictionary<Web6MetricClass, Web6MetricSummary> result = Web6CandidateWorkloadPlan.RequiredClasses
                .Where(metricClass => metricClass != omit).ToDictionary(
                metricClass => metricClass, _ => new Web6MetricSummary(1, 1, 1, 1, 0, 0, 1, "INSUFFICIENT_SAMPLE / NOT MEASURED"));
            return Task.FromResult(result);
        }
    }
}
