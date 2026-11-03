using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web5OwnedSupervisorTests
{
    private sealed class Fixture : IWeb5OwnedResources
    {
        public readonly List<string> Calls = new List<string>();
        public string? Fail;
        public bool FailQuarantine;
        public bool HostOwned = true;
        public bool Owned = true;
        public bool Absent = true;
        public TaskCompletionSource<bool>? HeldDrain;
        public TaskCompletionSource<bool>? HeldVerifyHost;
        private Task Step(string name) { Calls.Add(name); if (Fail == name) throw new InvalidOperationException(name); return Task.CompletedTask; }
        public Task Allocate(CancellationToken token) => Step("allocate");
        public Task Start(CancellationToken token) => Step("start");
        public async Task<bool> VerifyHost(CancellationToken token)
        {
            await (HeldVerifyHost?.Task ?? Step("verify-host"));
            if (HeldVerifyHost != null) Calls.Add("verify-host");
            return HostOwned;
        }
        public Task Admit(CancellationToken token) => Step("admit");
        public Task StopAdmission(CancellationToken token) => Step("stop");
        public Task Drain(CancellationToken token) { var result = Step("drain"); return HeldDrain?.Task ?? result; }
        public Task<bool> VerifyOwnershipAndQuiescence(CancellationToken token) { Calls.Add("verify"); return Task.FromResult(Owned); }
        public Task Oracle(CancellationToken token) => Step("oracle");
        public Task RemoveOwned(CancellationToken token) => Step("remove");
        public Task<bool> VerifyAbsent(CancellationToken token) { Calls.Add("absent"); return Task.FromResult(Absent); }
        public Task Quarantine(CancellationToken token) { if (FailQuarantine) throw new IOException("private record failed"); return Step("quarantine"); }
    }
    [Fact]
    public async Task Sql_adapter_refuses_unverified_or_quarantined_removal_without_touching_sql()
    {
        var directory = Path.Combine(Path.GetTempPath(), "web5-owned-sql-" + Guid.NewGuid().ToString("N"));
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var acl = new System.Security.AccessControl.DirectorySecurity(); acl.SetAccessRuleProtection(true, false); acl.SetOwner(identity.User!);
        acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(identity.User!, System.Security.AccessControl.FileSystemRights.FullControl,
            System.Security.AccessControl.InheritanceFlags.ContainerInherit | System.Security.AccessControl.InheritanceFlags.ObjectInherit,
            System.Security.AccessControl.PropagationFlags.None, System.Security.AccessControl.AccessControlType.Allow));
        Directory.CreateDirectory(directory, acl);
        try
        {
            var sql = Web5OwnedSqlAllocation.Create("Server=unreachable.invalid;Database=master;Integrated Security=true;", directory);
            await Assert.ThrowsAsync<InvalidOperationException>(() => sql.RemoveOwned(CancellationToken.None));
            sql.Quarantine();
            await Assert.ThrowsAsync<InvalidOperationException>(() => sql.RemoveOwned(CancellationToken.None));
            Assert.Contains("phase=quarantined-uncertain", File.ReadAllText(Path.Combine(directory, "sql-owner.txt")));
            Assert.Throws<IOException>(() => Web5OwnedSqlAllocation.Create("unused", directory));
        }
        finally { File.Delete(Path.Combine(directory, "sql-owner.txt")); Directory.Delete(directory); }
    }

    [Fact]
    public async Task Allocation_identity_is_reported_before_any_connection_attempt()
    {
        string? name = null;
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => SqlTestDatabase.CreateForSupervisorAsync(
            "Server=unreachable.invalid;Database=master;Integrated Security=true;Connect Timeout=1;", value => { name = value; throw new InvalidDataException("record refused"); }));
        Assert.Equal("record refused", error.Message);
        Assert.NotNull(name); SqlTestDatabaseName.EnsureDisposable(name!);
    }

    [Fact]
    public async Task Success_requires_ordered_drain_oracle_and_verified_absence()
    {
        var f = new Fixture(); var s = new Web5OwnedSupervisor(f, TimeSpan.FromSeconds(2));
        await s.Start(); await s.Finish();
        Assert.Equal(new[] { "allocate", "start", "verify-host", "admit", "stop", "drain", "verify", "oracle", "remove", "absent" }, f.Calls);
        Assert.Equal(Web5SupervisorState.Complete, s.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Finish());
    }
    [Theory]
    [InlineData("allocate")][InlineData("start")][InlineData("verify-host")][InlineData("admit")][InlineData("stop")][InlineData("drain")][InlineData("oracle")][InlineData("remove")]
    public async Task Fault_quarantines_and_never_admits_later_steps(string fault)
    {
        var f = new Fixture { Fail = fault }; var s = new Web5OwnedSupervisor(f, TimeSpan.FromSeconds(2));
        if (fault == "allocate" || fault == "start" || fault == "verify-host" || fault == "admit") await Assert.ThrowsAsync<InvalidOperationException>(() => s.Start());
        else { await s.Start(); await Assert.ThrowsAsync<InvalidOperationException>(() => s.Finish()); }
        Assert.Equal(Web5SupervisorState.Quarantined, s.State); Assert.Equal("quarantine", f.Calls[f.Calls.Count - 1]);
        if (fault != "remove") Assert.DoesNotContain("remove", f.Calls);
    }

    [Fact]
    public async Task Host_ownership_mismatch_quarantines_before_admission()
    {
        var f = new Fixture { HostOwned = false };
        var s = new Web5OwnedSupervisor(f, TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<InvalidDataException>(() => s.Start());
        Assert.Equal(new[] { "allocate", "start", "verify-host", "quarantine" }, f.Calls);
        Assert.DoesNotContain("admit", f.Calls);
        Assert.Equal(Web5SupervisorState.Quarantined, s.State);
    }

    [Fact]
    public async Task Host_verification_timeout_never_admits_late_worker()
    {
        var f = new Fixture { HeldVerifyHost = new TaskCompletionSource<bool>() };
        var s = new Web5OwnedSupervisor(f, TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAsync<TimeoutException>(() => s.Start());
        Assert.Equal(Web5SupervisorState.Quarantined, s.State);
        Assert.DoesNotContain("admit", f.Calls);
        f.HeldVerifyHost.SetResult(true);
        await Task.Delay(100);
        Assert.DoesNotContain("admit", f.Calls);
        Assert.DoesNotContain("remove", f.Calls);
    }
    [Fact]
    public async Task Quarantine_record_failure_preserves_both_errors_and_never_completes()
    {
        var f = new Fixture { Fail = "drain", FailQuarantine = true };
        var s = new Web5OwnedSupervisor(f, TimeSpan.FromSeconds(2)); await s.Start();
        var error = await Assert.ThrowsAsync<AggregateException>(() => s.Finish());
        Assert.Equal(2, error.InnerExceptions.Count); Assert.Equal(Web5SupervisorState.Quarantined, s.State);
        Assert.DoesNotContain("remove", f.Calls);
    }

    [Fact]
    public async Task Timeout_quarantines_and_never_removes_while_worker_ignores_cancellation()
    {
        var f = new Fixture { HeldDrain = new TaskCompletionSource<bool>() };
        var s = new Web5OwnedSupervisor(f, TimeSpan.FromMilliseconds(50));
        await s.Start();
        await Assert.ThrowsAsync<TimeoutException>(() => s.Finish());
        Assert.Equal(Web5SupervisorState.Quarantined, s.State); Assert.DoesNotContain("remove", f.Calls);
        f.HeldDrain.SetResult(true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Finish());
    }

    [Fact]
    public async Task Concurrent_finish_cannot_run_a_second_cleanup()
    {
        var f = new Fixture { HeldDrain = new TaskCompletionSource<bool>() };
        var s = new Web5OwnedSupervisor(f, TimeSpan.FromSeconds(2));
        await s.Start(); var first = s.Finish();
        await Assert.ThrowsAsync<InvalidOperationException>(() => s.Finish());
        f.HeldDrain.SetResult(true); await first;
        Assert.Equal(Web5SupervisorState.Complete, s.State);
        Assert.Single(f.Calls.FindAll(value => value == "remove"));
    }

    [Fact]
    public async Task Ownership_mismatch_never_removes_and_absence_failure_is_not_success()
    {
        var f = new Fixture { Owned = false }; var s = new Web5OwnedSupervisor(f, TimeSpan.FromSeconds(2));
        await s.Start(); await Assert.ThrowsAsync<InvalidDataException>(() => s.Finish());
        Assert.DoesNotContain("oracle", f.Calls); Assert.DoesNotContain("remove", f.Calls); Assert.Equal(Web5SupervisorState.Quarantined, s.State);
        f = new Fixture { Absent = false }; s = new Web5OwnedSupervisor(f, TimeSpan.FromSeconds(2));
        await s.Start(); await Assert.ThrowsAsync<InvalidDataException>(() => s.Finish()); Assert.Equal(Web5SupervisorState.Quarantined, s.State);
    }
}
