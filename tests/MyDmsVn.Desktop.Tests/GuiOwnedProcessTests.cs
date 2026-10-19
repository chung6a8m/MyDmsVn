using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class GuiOwnedProcessTests : IClassFixture<GuiProcessProbeFixture>
    {
        private readonly GuiProcessProbeFixture _probe;

        public GuiOwnedProcessTests(GuiProcessProbeFixture probe) => _probe = probe;

        [Fact]
        public async Task Large_stdout_and_stderr_are_drained_without_deadlock_and_retention_is_bounded()
        {
            var result = await GuiOwnedProcess.RunAsync(_probe.Start("output"), TimeSpan.FromSeconds(10));
            Assert.True(result.ExitCode == 0, "Exit=" + result.ExitCode + " stderr=" + result.StandardError + " stdout=" + result.StandardOutput);
            Assert.Contains("STDOUT", result.StandardOutput);
            Assert.Contains("BITNESS=32", result.StandardOutput);
            Assert.Contains("STDERR", result.StandardError);
            Assert.True(result.StandardOutput.Length <= GuiOwnedProcess.OutputLimit);
            Assert.True(result.StandardError.Length <= GuiOwnedProcess.OutputLimit);
            Assert.True(result.CleanupVerified);
        }

        [Theory]
        [InlineData("hang")]
        [InlineData("modal")]
        [InlineData("tree")]
        public async Task Timeout_terminates_and_drains_owned_GUI_and_descendants(string mode)
        {
            var failure = await Assert.ThrowsAsync<GuiProcessException>(
                () => GuiOwnedProcess.RunAsync(_probe.Start(mode), TimeSpan.FromSeconds(3)));
            Assert.Equal("TimedOut", failure.Reason);
            Assert.True(failure.CleanupVerified, failure.ToString());
            Assert.Contains("READY", failure.StandardOutput);
            AssertOwnedPidsExited(failure.StandardOutput);
        }

        [Fact]
        public async Task Cancellation_after_start_drains_the_owned_tree_before_returning()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                var start = _probe.Start("tree");
                // A named event avoids timing assumptions about CLR startup on a loaded machine.
                var name = "Local\\MyDmsVnGui_" + Guid.NewGuid().ToString("N");
                using (var started = new EventWaitHandle(false, EventResetMode.ManualReset, name))
                {
                    start.EnvironmentVariables["MYDMSVN_GUI_PROBE_READY"] = name;
                    var run = GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(15), cancellation.Token);
                    var observedReady = await Task.Run(() => started.WaitOne(TimeSpan.FromSeconds(10)));
                    cancellation.Cancel();
                    var failure = await Assert.ThrowsAsync<GuiProcessException>(() => run);
                    Assert.True(observedReady, "Probe did not signal readiness.");
                    Assert.Equal("Canceled", failure.Reason);
                    Assert.True(failure.CleanupVerified, failure.ToString());
                    AssertOwnedPidsExited(failure.StandardOutput);
                }
            }
        }

        [Fact]
        public async Task Parent_exit_does_not_leave_a_descendant_holding_redirected_pipes()
        {
            var failure = await Assert.ThrowsAsync<GuiProcessException>(
                () => GuiOwnedProcess.RunAsync(_probe.Start("orphan"), TimeSpan.FromSeconds(10)));
            Assert.Equal("DescendantsOutlivedParent", failure.Reason);
            Assert.True(failure.CleanupVerified, failure.ToString());
            AssertOwnedPidsExited(failure.StandardOutput);
        }

        [Fact]
        public async Task Normal_tree_exit_allows_a_short_bounded_natural_drain()
        {
            var result = await GuiOwnedProcess.RunAsync(_probe.Start("draining-parent"), TimeSpan.FromSeconds(10));
            Assert.Equal(0, result.ExitCode);
            Assert.True(result.CleanupVerified);
            AssertOwnedPidsExited(result.StandardOutput);
        }

        [Fact]
        public async Task Fatal_child_failure_returns_exit_and_diagnostics_without_poisoning_testhost()
        {
            var result = await GuiOwnedProcess.RunAsync(_probe.Start("crash"), TimeSpan.FromSeconds(15));
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("READY", result.StandardOutput);
            Assert.True(result.CleanupVerified);
            var next = await GuiOwnedProcess.RunAsync(_probe.Start("exit"), TimeSpan.FromSeconds(10));
            Assert.Equal(0, next.ExitCode);
        }

        [Fact]
        public async Task Pre_canceled_launch_creates_no_child()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => GuiOwnedProcess.RunAsync(_probe.Start("hang"), TimeSpan.FromSeconds(1), cancellation.Token));
            }
        }

        [Fact]
        public async Task Pipe_read_failure_keeps_already_captured_failure_diagnostics()
        {
            var capture = new GuiOutputCapture();
            var error = await Assert.ThrowsAsync<IOException>(() => capture.DrainAsync(new FailingPipeReader()));
            Assert.Equal("Synthetic pipe read failure", error.Message);
            Assert.Contains("READY: original failure evidence", capture.Snapshot());
        }

        private sealed class FailingPipeReader : TextReader
        {
            private bool _read;
            public override Task<int> ReadAsync(char[] buffer, int index, int count)
            {
                if (_read) return Task.FromException<int>(new IOException("Synthetic pipe read failure"));
                _read = true;
                var text = "READY: original failure evidence";
                text.CopyTo(0, buffer, index, text.Length);
                return Task.FromResult(text.Length);
            }
        }

        private static void AssertOwnedPidsExited(string output)
        {
            var pids = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.StartsWith("PID=", StringComparison.Ordinal)).Select(line => int.Parse(line.Substring(4))).ToArray();
            Assert.NotEmpty(pids);
            foreach (var pid in pids)
            {
                try
                {
                    using (var process = Process.GetProcessById(pid))
                    {
                        Assert.True(process.HasExited, "An owned GUI probe process is still alive.");
                    }
                }
                catch (ArgumentException)
                {
                    // Absence is expected after verified job drain.
                }
            }
        }
    }

    public sealed class GuiProcessProbeFixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "MyDmsVn.GuiProbe", Guid.NewGuid().ToString("N"));
        private readonly string _executable;

        public GuiProcessProbeFixture()
            : this(package: false)
        {
        }

        internal GuiProcessProbeFixture(bool package)
        {
            Directory.CreateDirectory(_directory);
            var sourceName = package ? "GuiPackageProbe.cs" : "GuiProcessProbe.cs";
            _executable = Path.Combine(_directory, Path.GetFileNameWithoutExtension(sourceName) + ".exe");
            using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("MyDmsVn.Desktop.Tests." + sourceName)!)
            using (var reader = new StreamReader(source))
            {
                var sourcePath = Path.Combine(_directory, sourceName);
                File.WriteAllText(sourcePath, reader.ReadToEnd());
                var compiler = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "Microsoft.NET", "Framework", "v4.0.30319", "csc.exe");
                var references = "/reference:System.Windows.Forms.dll /reference:System.Drawing.dll ";
                if (package)
                {
                    foreach (var name in new[] { "UIAutomationClient", "UIAutomationTypes", "WindowsBase" })
                        references += "/reference:\"" + Path.Combine(Path.GetDirectoryName(compiler)!, "WPF", name + ".dll") + "\" ";
                }
                var result = GuiOwnedProcess.RunAsync(new ProcessStartInfo(compiler,
                    "/nologo /platform:x86 /target:exe " + references + "/out:\"" +
                    _executable + "\" \"" + sourcePath + "\"")
                {
                    UseShellExecute = false,
                    WorkingDirectory = _directory,
                }, TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                Assert.True(result.ExitCode == 0, result.StandardOutput + Environment.NewLine + result.StandardError);
            }
        }

        public ProcessStartInfo Start(string mode) => new ProcessStartInfo(_executable, mode)
        {
            UseShellExecute = false,
            WorkingDirectory = _directory,
        };

        public void Dispose()
        {
            GuiTestSafety.ThrowIfPoisoned();
            Directory.Delete(_directory, recursive: true);
        }
    }
}
