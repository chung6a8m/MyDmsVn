using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class GuiOwnedProcessTests : IClassFixture<GuiProcessProbeFixture>
    {
        private readonly GuiProcessProbeFixture _probe;
        private readonly ITestOutputHelper _output;

        public GuiOwnedProcessTests(GuiProcessProbeFixture probe, ITestOutputHelper output)
        {
            _probe = probe;
            _output = output;
        }

        [Fact]
        public async Task Packaged_lookup_keyboard_recovers_its_owned_window_from_a_background_editor()
        {
            using var keyboard = new GuiProcessProbeFixture(package: true);
            PreserveFocusProbe(_probe);
            PreserveFocusProbe(keyboard);
            var start = keyboard.Start("\"" + _probe.Start("unused").FileName + "\" keyboard-focus-audit Light");
            start.EnvironmentVariables["MYDMSVN_GUI_P5_EXPECTED_SCALE"] =
                Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_EXPECTED_SCALE") ?? "100";
            var result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(25));
            _output.WriteLine(result.StandardOutput);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("BACKGROUND_TARGET_OBSERVED=PASS", result.StandardOutput);
            Assert.Contains("OWNED_LOOKUP_FOREGROUND_RECOVERED=PASS", result.StandardOutput);
            Assert.True(result.CleanupVerified);
        }

        [Fact]
        public async Task Packaged_lookup_input_preserves_value_when_the_editor_refuses_keyboard_focus()
        {
            using var keyboard = new GuiProcessProbeFixture(package: true);
            PreserveFocusProbe(_probe);
            PreserveFocusProbe(keyboard);
            var start = keyboard.Start("\"" + _probe.Start("unused").FileName + "\" keyboard-focus-refusal-audit Light");
            start.EnvironmentVariables["MYDMSVN_GUI_P5_EXPECTED_SCALE"] =
                Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_EXPECTED_SCALE") ?? "100";
            var result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(25));
            _output.WriteLine(result.StandardOutput);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("REFUSED_LOOKUP_FOCUS_NO_INPUT=PASS", result.StandardOutput);
            Assert.True(result.CleanupVerified);
        }

        private void PreserveFocusProbe(GuiProcessProbeFixture probe)
        {
            var archive = Environment.GetEnvironmentVariable("MYDMSVN_GUI_PROBE_ARCHIVE_DIRECTORY");
            if (string.IsNullOrWhiteSpace(archive)) return;
            Assert.True(Path.IsPathRooted(archive), "Probe archive directory must be absolute.");
            var executable = probe.Start("unused").FileName;
            var destination = Path.Combine(archive, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(destination);
            foreach (var path in new[] { executable, Path.ChangeExtension(executable, ".cs") })
            {
                var copy = Path.Combine(destination, Path.GetFileName(path));
                File.Copy(path, copy);
                using var hash = System.Security.Cryptography.SHA256.Create();
                var digest = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(copy))).Replace("-", "");
                File.AppendAllText(Path.Combine(destination, "before-launch.sha256"), Path.GetFileName(copy) + " " + digest + Environment.NewLine);
                _output.WriteLine("FOCUS_PROBE_BEFORE_LAUNCH_SHA256=" + Path.GetFileName(copy) + ":" + digest);
            }
        }

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

        [Fact]
        public async Task Infrastructure_probe_does_not_inherit_SQL_credentials()
        {
            var names = new[] { "MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING", "MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING", "MYDMSVN_RUNTIME_SQLSERVER_CONNECTION_STRING" };
            var original = names.Select(Environment.GetEnvironmentVariable).ToArray();
            try
            {
                foreach (var name in names) Environment.SetEnvironmentVariable(name, "synthetic infrastructure secret");
                var result = await GuiOwnedProcess.RunAsync(_probe.Start("environment"), TimeSpan.FromSeconds(10));
                Assert.Equal(0, result.ExitCode);
                Assert.Contains("SQL_ENVIRONMENT_EXCLUDED=True", result.StandardOutput);
            }
            finally
            {
                for (var index = 0; index < names.Length; index++) Environment.SetEnvironmentVariable(names[index], original[index]);
            }
        }

        [Fact]
        public async Task Poisoned_gui_session_blocks_SQL_allocation_before_configuration_or_database_work()
        {
            var result = await GuiOwnedProcess.RunAsync(_probe.Start("poison-admission \"" + Assembly.GetExecutingAssembly().Location + "\""), TimeSpan.FromSeconds(15));
            Assert.True(result.ExitCode == 0, result.StandardOutput + Environment.NewLine + result.StandardError);
            Assert.Contains("POISON_BLOCKS_SQL_ADMISSION=PASS", result.StandardOutput);
            Assert.False(GuiTestSafety.IsPoisoned); // The synthetic latch lives only in the owned child.
        }

        [Theory]
        [InlineData("uncertain")]
        [InlineData("sql-error")]
        [InlineData("record-error")]
        public async Task SQL_quarantine_blocks_next_SQL_and_process_admission(string mode)
        {
            var result = await GuiOwnedProcess.RunAsync(_probe.Start("sql-quarantine \"" + Assembly.GetExecutingAssembly().Location + "\" " + mode + " \"" + AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) + "\""), TimeSpan.FromSeconds(15));
            Assert.True(result.ExitCode == 0, result.StandardOutput + Environment.NewLine + result.StandardError);
            Assert.Contains("SQL_QUARANTINE_FAIL_STOP=PASS", result.StandardOutput);
            Assert.False(GuiTestSafety.IsPoisoned);
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
        public async Task Attended_budget_accepts_thirty_minutes_but_cancellation_still_drains_the_owned_tree()
        {
            var name = "Local\\MyDmsVn.GuiProbe.Attended." + Guid.NewGuid().ToString("N");
            using (var ready = new EventWaitHandle(false, EventResetMode.ManualReset, name))
            using (var cancellation = new CancellationTokenSource())
            {
                var start = _probe.Start("tree");
                start.EnvironmentVariables["MYDMSVN_GUI_PROBE_READY"] = name;
                var run = GuiOwnedProcess.RunAttendedAsync(start, TimeSpan.FromMinutes(30), cancellation.Token);
                var observedReady = await Task.Run(() => ready.WaitOne(TimeSpan.FromSeconds(10)));
                cancellation.Cancel();
                var failure = await Assert.ThrowsAsync<GuiProcessException>(() => run);
                Assert.True(observedReady, "Attended probe did not signal readiness.");
                Assert.Equal("Canceled", failure.Reason);
                Assert.True(failure.CleanupVerified, failure.ToString());
                AssertOwnedPidsExited(failure.StandardOutput);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(31)]
        public async Task Attended_budget_rejects_nonpositive_or_above_thirty_minutes_before_launch(int minutes)
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                GuiOwnedProcess.RunAttendedAsync(_probe.Start("hang"), TimeSpan.FromMinutes(minutes)));
        }

        [Fact]
        public async Task Automated_budget_still_rejects_above_five_minutes_before_launch()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                GuiOwnedProcess.RunAsync(_probe.Start("hang"), TimeSpan.FromMinutes(6)));
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

        internal GuiProcessProbeFixture(bool package, bool inline = false)
        {
            Directory.CreateDirectory(_directory);
            var sourceName = inline ? "GuiInlineEditorProbe.cs" : package ? "GuiPackageProbe.cs" : "GuiProcessProbe.cs";
            _executable = Path.Combine(_directory, Path.GetFileNameWithoutExtension(sourceName) + ".exe");
            using (var source = Assembly.GetExecutingAssembly().GetManifestResourceStream("MyDmsVn.Desktop.Tests." + sourceName)!)
            using (var reader = new StreamReader(source))
            {
                var sourcePath = Path.Combine(_directory, sourceName);
                File.WriteAllText(sourcePath, reader.ReadToEnd());
                var compiler = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "Microsoft.NET", "Framework", "v4.0.30319", "csc.exe");
                var references = "/reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:Accessibility.dll ";
                if (package || inline)
                {
                    foreach (var name in new[] { "UIAutomationClient", "UIAutomationTypes", "WindowsBase" })
                        references += "/reference:\"" + Path.Combine(Path.GetDirectoryName(compiler)!, "WPF", name + ".dll") + "\" ";
                }
                var compilerStart = new ProcessStartInfo(compiler,
                    "/nologo /platform:x86 /target:exe " + references + "/out:\"" +
                    _executable + "\" \"" + sourcePath + "\"")
                {
                    UseShellExecute = false,
                    WorkingDirectory = _directory,
                };
                ExcludeSqlEnvironment(compilerStart);
                var result = GuiOwnedProcess.RunAsync(compilerStart, TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                Assert.True(result.ExitCode == 0, result.StandardOutput + Environment.NewLine + result.StandardError);
            }
        }

        public ProcessStartInfo Start(string mode)
        {
            var start = new ProcessStartInfo(_executable, mode)
            {
                UseShellExecute = false,
                WorkingDirectory = _directory,
            };
            ExcludeSqlEnvironment(start);
            return start;
        }

        private static void ExcludeSqlEnvironment(ProcessStartInfo start)
        {
            foreach (var key in start.EnvironmentVariables.Keys.Cast<string>().ToArray())
                if (key.StartsWith("MYDMSVN_", StringComparison.OrdinalIgnoreCase) || key.StartsWith("Security__", StringComparison.OrdinalIgnoreCase))
                    start.EnvironmentVariables.Remove(key);
        }

        public void Dispose()
        {
            GuiTestSafety.ThrowIfPoisoned();
            Directory.Delete(_directory, recursive: true);
        }
    }
}
