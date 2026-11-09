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
            new Web6WorkloadObservation(duplicate, deadlocks, runaway, journalBytes: 1)));
    }

    [Fact]
    public void Workload_profiles_are_serial_bounded_and_keep_six_critical_classes_separate()
    {
        Assert.Equal(new[] { 1, 4, 8, 16 }, Web6CandidateWorkloadPlan.ConcurrencyProfiles);
        Assert.Equal(1000, Web6CandidateWorkloadPlan.DesiredSamplesPerCriticalClass);
        Assert.Equal(6, Web6CandidateWorkloadPlan.CriticalClasses.Count);
        Assert.Equal(6, Web6CandidateWorkloadPlan.CriticalClasses.Distinct().Count());
        Assert.DoesNotContain(Web6MetricClass.AuthRefresh, Web6CandidateWorkloadPlan.CriticalClasses);
    }

    [Fact]
    public async Task Workload_runner_is_serial_smoke_then_profiles_and_drains_on_failure()
    {
        var operations = new RecordingWorkloadOperations();
        await new Web6CandidateWorkloadRunner(operations).Execute(CancellationToken.None);
        Assert.Equal(new[] { "admit", "smoke", "observe", "measure:1", "observe", "measure:4", "observe",
            "measure:8", "observe", "measure:16", "observe", "drain", "complete" }, operations.Calls);

        operations = new RecordingWorkloadOperations { FailAtConcurrency = 4 };
        await Assert.ThrowsAsync<IOException>(() => new Web6CandidateWorkloadRunner(operations).Execute(CancellationToken.None));
        Assert.Equal(1, operations.Calls.Count(call => call == "drain"));
        Assert.DoesNotContain("complete", operations.Calls);
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
        public Task<Web6WorkloadAdmission> VerifyAdmission(CancellationToken token)
        { Calls.Add("admit"); return Task.FromResult(new Web6WorkloadAdmission(true, 1, true, true, true, true)); }
        public Task Smoke(IReadOnlyList<Web6MetricClass> criticalClasses, CancellationToken token)
        { Calls.Add("smoke"); Assert.Equal(6, criticalClasses.Count); return Task.CompletedTask; }
        public Task<IReadOnlyDictionary<Web6MetricClass, Web6MetricSummary>> Measure(int concurrency, int desiredSamples, CancellationToken token)
        {
            Calls.Add("measure:" + concurrency); if (concurrency == FailAtConcurrency) throw new IOException("measurement failed");
            IReadOnlyDictionary<Web6MetricClass, Web6MetricSummary> result = Web6CandidateWorkloadPlan.CriticalClasses.ToDictionary(
                metricClass => metricClass, _ => new Web6MetricSummary(1, 1, 1, 1, 0, 0, 1, "INSUFFICIENT_SAMPLE / NOT MEASURED"));
            return Task.FromResult(result);
        }
        public Task<Web6WorkloadObservation> Observe(CancellationToken token)
        { Calls.Add("observe"); return Task.FromResult(new Web6WorkloadObservation(false, 0, false, 1)); }
        public Task Drain(CancellationToken token) { Calls.Add("drain"); return Task.CompletedTask; }
        public Task Complete(CancellationToken token) { Calls.Add("complete"); return Task.CompletedTask; }
    }
}
