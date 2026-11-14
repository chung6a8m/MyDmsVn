using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web5IisExpressHostAdmissionTests
{
    [Fact]
    public async Task Queue_poll_final_iteration_never_restarts_an_expired_phase()
    {
        var now = TimeSpan.Zero;
        var calls = 0;
        var deadline = new Web6CutoverDeadline(TimeSpan.FromMilliseconds(10), () => now);
        await Assert.ThrowsAsync<TimeoutException>(() => Web5IisExpressHostAdmission.Poll(
            () => { calls++; throw new InvalidDataException(); }, deadline, default,
            (duration, token) => { Assert.Equal(TimeSpan.FromMilliseconds(10), duration); now += duration; return Task.CompletedTask; }));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Process_termination_requires_successful_fresh_identity_verification(bool expired)
    {
        var now = TimeSpan.Zero;
        var deadline = new Web6CutoverDeadline(TimeSpan.FromSeconds(1), () => now);
        var killed = false;
        Assert.ThrowsAny<Exception>(() => Web6CutoverNative.TerminateVerified(
            () => { if (expired) now = TimeSpan.FromSeconds(2); else throw new InvalidDataException(); },
            () => killed = true, deadline, default));
        Assert.False(killed);
    }

    [Fact]
    public async Task Owned_process_wait_is_clamped_to_final_phase_remainder()
    {
        var now = TimeSpan.Zero;
        var polls = 0;
        var deadline = new Web6CutoverDeadline(TimeSpan.FromMilliseconds(3), () => now);
        await Assert.ThrowsAsync<TimeoutException>(() => Web6CutoverNative.WaitForExit(() => { polls++; return false; }, deadline, default,
            (duration, token) => { Assert.Equal(TimeSpan.FromMilliseconds(3), duration); now += duration; return Task.CompletedTask; }));
        Assert.Equal(1, polls);
    }

    [Fact]
    public async Task Health_poll_clamps_its_request_and_final_delay_to_the_shared_phase()
    {
        var now = TimeSpan.Zero;
        var probes = 0;
        var deadline = new Web6CutoverDeadline(TimeSpan.FromMilliseconds(50), () => now);
        await Assert.ThrowsAsync<TimeoutException>(() => WebHostIisExpressTests.PollCutoverHealth((request, token) =>
        {
            probes++;
            Assert.True(token.CanBeCanceled);
            Assert.Equal(TimeSpan.FromMilliseconds(50), request.Remaining);
            now = TimeSpan.FromMilliseconds(40);
            return Task.FromResult(false);
        }, deadline, default, (delay, token) =>
        {
            Assert.Equal(TimeSpan.FromMilliseconds(10), delay);
            now += delay;
            return Task.CompletedTask;
        }));
        Assert.Equal(1, probes);
    }

    private const string QueueSnapshot = @"
Snapshot of HTTP service state (Request Queue View):
-----------------------------------------------------

Request queue name: IISExpressAppPool{11111111-1111-1111-1111-111111111111}
    Version: 2.0
    State: Active
    Number of active processes attached: 1
    Controller process:
        ID: 18404, image: C:\Program Files\IIS Express\iisexpress.exe
    Processes:
        ID: 18404, image: C:\Program Files\IIS Express\iisexpress.exe
    URL groups:
    URL group ID: FB00000B20000003
        State: Active
        Request queue name: IISExpressAppPool{11111111-1111-1111-1111-111111111111}
        Properties:
            Number of registered URLs: 1
            Registered URLs:
                HTTPS://LOCALHOST:44321/
        Server session ID: FD00000B1000019F
";

    [Fact]
    public void Cutover_start_info_and_WMI_share_the_exact_native_launch_plan_through_drain()
    {
        using var fixture = new IdentityFixture();
        var plan = WebHostIisExpressTests.CreateCutoverIisLaunch(fixture.ConfigurationPath, new Uri("https://localhost:44321/"));
        var start = plan.CreateStartInfo();
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IIS Express", "iisexpress.exe");
        Assert.Contains(" ", start.FileName);
        Assert.Contains(" ", fixture.ConfigurationPath);
        Assert.Equal(executable, start.FileName);
        Assert.Equal(Path.GetFullPath(start.FileName), start.FileName);
        Assert.DoesNotContain(Path.AltDirectorySeparatorChar, start.FileName);
        Assert.Equal("/config:\"" + fixture.ConfigurationPath + "\" /site:Web2Smoke /systray:false", start.Arguments);
        Assert.Equal("\"" + start.FileName + "\" " + start.Arguments, plan.CommandLine);
        Assert.False(start.UseShellExecute);
        Assert.True(start.CreateNoWindow);
        Assert.Equal(ProcessWindowStyle.Hidden, start.WindowStyle);
        Assert.True(start.RedirectStandardInput && start.RedirectStandardOutput && start.RedirectStandardError);
        start.Arguments += " /trace:error";
        Assert.DoesNotContain("/trace:error", plan.CommandLine, StringComparison.Ordinal);
        Assert.Equal("/config:\"" + fixture.ConfigurationPath + "\" /site:Web2Smoke /systray:false", plan.CreateStartInfo().Arguments);

        var original = fixture.Observation();
        var observed = new Web5IisExpressProcessObservation(original.ProcessId, original.StartTimeUtc, executable, plan.CommandLine);
        var identity = Web5IisExpressHostAdmission.Capture(original.ProcessId, original.StartTimeUtc.AddTicks(9), observed, plan);
        Assert.Same(plan, identity.Launch);
        Assert.Equal(original.ProcessId, Web5IisExpressHostAdmission.ValidateEvidence(identity, observed, QueueSnapshot).ProcessIds[0]);
        Assert.Throws<InvalidDataException>(() => Web5IisExpressHostAdmission.ValidateAbsent(identity, observed, EmptyQueues));
        Assert.Throws<InvalidDataException>(() => Web5IisExpressHostAdmission.ValidateAbsent(identity, null, QueueSnapshot));
        Web5IisExpressHostAdmission.ValidateAbsent(identity, null, EmptyQueues);
    }

    [Theory]
    [InlineData("alternate-separator")]
    [InlineData("image")]
    [InlineData("command-executable")]
    [InlineData("configuration")]
    [InlineData("site")]
    [InlineData("extra-argument")]
    [InlineData("quote")]
    [InlineData("pid")]
    [InlineData("start")]
    public void Cutover_launch_capture_refuses_altered_process_evidence(string drift)
    {
        using var fixture = new IdentityFixture();
        var plan = WebHostIisExpressTests.CreateCutoverIisLaunch(fixture.ConfigurationPath, new Uri("https://localhost:44321/"));
        var original = fixture.Observation();
        var executable = drift == "image" ? Path.Combine(Path.GetDirectoryName(fixture.ConfigurationPath)!, "iisexpress.exe") : plan.ExecutablePath;
        var command = drift switch
        {
            "alternate-separator" => plan.CommandLine.Replace("IIS Express\\iisexpress.exe", "IIS Express/iisexpress.exe"),
            "command-executable" => plan.CommandLine.Replace("iisexpress.exe", "other.exe"),
            "configuration" => plan.CommandLine.Replace("applicationhost.config", "other.config"),
            "site" => plan.CommandLine.Replace("Web2Smoke", "OtherSite"),
            "extra-argument" => plan.CommandLine + " /trace:error",
            "quote" => plan.CommandLine.Replace("/config:\"", "/config:"),
            _ => plan.CommandLine,
        };
        var observed = new Web5IisExpressProcessObservation(drift == "pid" ? original.ProcessId + 1 : original.ProcessId,
            drift == "start" ? original.StartTimeUtc.AddTicks(10) : original.StartTimeUtc, executable, command);
        var failure = Assert.Throws<InvalidDataException>(() =>
            Web5IisExpressHostAdmission.Capture(original.ProcessId, original.StartTimeUtc, observed, plan));
        Assert.DoesNotContain(fixture.ConfigurationPath, failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Cutover_launch_refuses_configuration_that_can_break_quoting()
    {
        using var fixture = new IdentityFixture();
        var origin = new Uri("https://localhost:44321/");
        Assert.Throws<ArgumentException>(() => WebHostIisExpressTests.CreateCutoverIisLaunch(fixture.ConfigurationPath + "\" /extra", origin));
        Assert.Throws<ArgumentException>(() => WebHostIisExpressTests.CreateCutoverIisLaunch(fixture.ConfigurationPath + "\n", origin));
    }

    [Fact]
    public void Exact_process_config_command_and_queue_are_admitted()
    {
        using var fixture = new IdentityFixture();
        var identity = fixture.Capture();

        var queue = Web5IisExpressHostAdmission.ValidateEvidence(identity, fixture.Observation(), QueueSnapshot);

        Assert.Equal(18404, queue.ProcessIds[0]);
        Assert.Equal("Web5Owned", identity.SiteName);
    }

    [Theory]
    [InlineData("pid")]
    [InlineData("start")]
    [InlineData("executable")]
    [InlineData("command")]
    public void Process_replacement_or_command_mismatch_is_rejected(string mutation)
    {
        using var fixture = new IdentityFixture();
        var identity = fixture.Capture();
        var observation = fixture.Observation(mutation);

        Assert.Throws<InvalidDataException>(() =>
            Web5IisExpressHostAdmission.ValidateEvidence(identity, observation, QueueSnapshot));
    }

    [Fact]
    public void Fixture_handle_start_identity_must_match_WMI_observation()
    {
        using var fixture = new IdentityFixture();
        var observation = fixture.Observation();
        Web5IisExpressHostAdmission.ValidateOwnedHandleIdentity(18404, observation.StartTimeUtc, observation);
        Assert.Throws<InvalidDataException>(() => Web5IisExpressHostAdmission.ValidateOwnedHandleIdentity(
            18404, observation.StartTimeUtc.AddTicks(10), observation));
        Assert.Throws<InvalidDataException>(() => Web5IisExpressHostAdmission.ValidateOwnedHandleIdentity(
            19000, observation.StartTimeUtc, observation));
    }

    [Fact]
    public void Capture_process_overload_rejects_a_live_non_IIS_process_handle()
    {
        using var fixture = new IdentityFixture();
        using var current = Process.GetCurrentProcess();
        Assert.Throws<InvalidDataException>(() => Web5IisExpressHostAdmission.Capture(
            current, fixture.ConfigurationPath, "Web5Owned", new Uri("https://localhost:44321/")));
    }

    [Fact]
    public void Changed_config_is_rejected_after_capture()
    {
        using var fixture = new IdentityFixture();
        var identity = fixture.Capture();
        File.AppendAllText(fixture.ConfigurationPath, "changed");

        Assert.Throws<InvalidDataException>(() =>
            Web5IisExpressHostAdmission.ValidateEvidence(identity, fixture.Observation(), QueueSnapshot));
    }

    [Fact]
    public void Relative_config_path_is_rejected()
    {
        using var fixture = new IdentityFixture();
        Assert.Throws<ArgumentException>(() => Web5IisExpressHostAdmission.Capture(
            fixture.Observation(), "applicationhost.config", "Web5Owned", new Uri("https://localhost:44321/")));
    }

    [Fact]
    public void Queue_worker_replacement_is_rejected()
    {
        using var fixture = new IdentityFixture();
        var identity = fixture.Capture();

        Assert.Throws<InvalidDataException>(() => Web5IisExpressHostAdmission.ValidateEvidence(
            identity, fixture.Observation(), QueueSnapshot.Replace("ID: 18404, image:", "ID: 19000, image:")));
    }

    [Fact]
    public void Direct_request_disables_proxy_and_redirects()
    {
        var request = Web5IisExpressHostAdmission.CreateDirectRequest(new Uri("https://localhost:44321/api/v1/health"));
        Assert.Null(request.Proxy);
        Assert.False(request.AllowAutoRedirect);
        Assert.Equal(10000, request.Timeout);
    }

    [Fact]
    public void Exact_direct_HTTP_rejection_probe_disables_proxy_and_redirects()
    {
        var request = Web5IisExpressHostAdmission.CreateDirectHttpRejectionRequest(
            new Uri("http://localhost:18081/api/v1/health"));
        Assert.Null(request.Proxy);
        Assert.False(request.AllowAutoRedirect);
    }

    [Theory]
    [InlineData("http://localhost:18082/api/v1/health")]
    [InlineData("http://localhost:18081/other")]
    [InlineData("http://outside.invalid:18081/api/v1/health")]
    [InlineData("https://localhost:18081/api/v1/health")]
    public void Direct_HTTP_rejection_probe_refuses_any_other_target(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            Web5IisExpressHostAdmission.CreateDirectHttpRejectionRequest(new Uri(value)));
    }

    [Fact]
    public void Absence_rejects_same_process_or_registered_origin()
    {
        using var fixture = new IdentityFixture();
        var identity = fixture.Capture();
        Assert.Throws<InvalidDataException>(() =>
            Web5IisExpressHostAdmission.ValidateAbsent(identity, fixture.Observation(), QueueSnapshot));
        Assert.Throws<InvalidDataException>(() =>
            Web5IisExpressHostAdmission.ValidateAbsent(identity, null, QueueSnapshot));
    }

    [Fact]
    public void Absence_ignores_valid_foreign_HTTP_sys_wildcard_prefix()
    {
        using var fixture = new IdentityFixture();
        var identity = fixture.Capture();
        var foreign = QueueSnapshot
            .Replace("IISExpressAppPool{11111111-1111-1111-1111-111111111111}", "DefaultAppPool")
            .Replace("HTTPS://LOCALHOST:44321/", "HTTP://*:80/");

        Web5IisExpressHostAdmission.ValidateAbsent(identity, null, foreign);
    }

    [Theory]
    [InlineData("HTTPS://+:44321/")]
    [InlineData("HTTPS://LOCALHOST:44321/API/")]
    public void Absence_rejects_foreign_active_registration_that_can_capture_owned_paths(string registeredUrl)
    {
        using var fixture = new IdentityFixture();
        var identity = fixture.Capture();
        var foreign = QueueSnapshot
            .Replace("IISExpressAppPool{11111111-1111-1111-1111-111111111111}", "DefaultAppPool")
            .Replace("ID: 18404", "ID: 19000")
            .Replace("HTTPS://LOCALHOST:44321/", registeredUrl);

        Assert.Throws<InvalidDataException>(() =>
            Web5IisExpressHostAdmission.ValidateAbsent(identity, null, foreign));
    }

    [Fact]
    public void Absence_accepts_valid_empty_request_queue_snapshot()
    {
        using var fixture = new IdentityFixture();
        Web5IisExpressHostAdmission.ValidateAbsent(fixture.Capture(), null, EmptyQueues);
    }

    [Fact]
    public async Task Precancelled_live_verification_does_not_read_service_state()
    {
        using var fixture = new IdentityFixture();
        var identity = fixture.Capture();
        using var stop = new CancellationTokenSource();
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Web5IisExpressHostAdmission.VerifyAsync(identity, stop.Token));
    }

    [Web5OwnedHostFact, Trait("Category", "Web5OwnedHostAdmission")]
    public Task Live_IIS_Express_queue_is_exclusive_before_during_after_direct_TLS_and_absent_after_stop() =>
        Web5OwnedIisExpressAdmissionFixture.Run();

    private sealed class IdentityFixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "web5 host " + Guid.NewGuid().ToString("N"));
        private readonly DateTime _start = new DateTime(638977248000000000, DateTimeKind.Utc);
        internal IdentityFixture()
        {
            Directory.CreateDirectory(_directory);
            ConfigurationPath = Path.Combine(_directory, "applicationhost.config");
            File.WriteAllText(ConfigurationPath, "<configuration />");
        }
        internal string ConfigurationPath { get; }
        internal Web5IisExpressIdentity Capture() => Web5IisExpressHostAdmission.Capture(
            Observation(), ConfigurationPath, "Web5Owned", new Uri("https://localhost:44321/"));
        internal Web5IisExpressProcessObservation Observation(string? mutation = null)
        {
            var executable = Web5IisExpressHostAdmission.ExpectedExecutablePath;
            var command = "\"" + executable + "\" /config:\"" + ConfigurationPath + "\" /site:Web5Owned /systray:false";
            return new Web5IisExpressProcessObservation(
                mutation == "pid" ? 19000 : 18404,
                mutation == "start" ? _start.AddSeconds(1) : _start,
                mutation == "executable" ? Path.Combine(_directory, "iisexpress.exe") : executable,
                mutation == "command" ? command.Replace("Web5Owned", "RelaySite") : command);
        }
        public void Dispose() => Directory.Delete(_directory, true);
    }

    private const string EmptyQueues = "Snapshot of HTTP service state (Request Queue View):\n-----------------------------------------------------\n";

    private sealed class Web5OwnedHostFactAttribute : FactAttribute
    {
        public Web5OwnedHostFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("MYDMSVN_WEB5_OWNED_HOST_ADMISSION") != "1" ||
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_TLS_THUMBPRINT")))
                Skip = "Owned IIS Express admission requires explicit opt-in and the existing localhost TLS thumbprint.";
        }
    }
}
