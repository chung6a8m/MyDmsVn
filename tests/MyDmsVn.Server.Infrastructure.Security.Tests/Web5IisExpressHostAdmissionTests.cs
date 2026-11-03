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

    [Fact]
    public void Absence_accepts_valid_empty_request_queue_snapshot()
    {
        using var fixture = new IdentityFixture();
        Web5IisExpressHostAdmission.ValidateAbsent(fixture.Capture(), null,
            "Snapshot of HTTP service state (Request Queue View):\n-----------------------------------------------------\n");
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
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "web5-host-" + Guid.NewGuid().ToString("N"));
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
            var command = Web5IisExpressHostAdmission.ExpectedCommandLine(executable, ConfigurationPath, "Web5Owned");
            return new Web5IisExpressProcessObservation(
                mutation == "pid" ? 19000 : 18404,
                mutation == "start" ? _start.AddSeconds(1) : _start,
                mutation == "executable" ? Path.Combine(_directory, "iisexpress.exe") : executable,
                mutation == "command" ? command.Replace("Web5Owned", "RelaySite") : command);
        }
        public void Dispose() => Directory.Delete(_directory, true);
    }

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
