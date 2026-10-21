using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Desktop.Tests;

public sealed class GuiPackageFactAttribute : FactAttribute
{
    public GuiPackageFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_PACKAGE")))
            Skip = "BLOCKED: set MYDMSVN_GUI_P5_PACKAGE to a clean published GUI-P5 candidate.";
    }
}

public sealed class GuiPackageTheoryAttribute : TheoryAttribute
{
    public GuiPackageTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_PACKAGE")))
            Skip = "BLOCKED: set MYDMSVN_GUI_P5_PACKAGE to a clean published GUI-P5 candidate.";
    }
}

public sealed class BcfAttendedLookupFactAttribute : FactAttribute
{
    public BcfAttendedLookupFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYDMSVN_BCF_ATTENDED_DIRECTORY")))
            Skip = "ATTENDED ONLY: requires an explicitly prepared owned BCF walkthrough directory.";
    }
}

public sealed class PackagedGuiAcceptanceTests
{
    private readonly ITestOutputHelper _output;
    public PackagedGuiAcceptanceTests(ITestOutputHelper output)
    {
        _output = output;
        _output.WriteLine("TESTHOST_BITNESS=" + (IntPtr.Size * 8));
        Assert.Equal(4, IntPtr.Size);
        Assert.True(Environment.Is64BitOperatingSystem);
    }

    [BcfAttendedLookupFact]
    [Trait("Category", "GuiSql")]
    public async Task Bcf_attended_lookup_package_session_keeps_human_input_separate_from_automation()
    {
        var package = Candidate();
        var barrier = Path.GetFullPath(Environment.GetEnvironmentVariable("MYDMSVN_BCF_ATTENDED_DIRECTORY")!);
        Assert.True(Directory.Exists(barrier));
        Assert.Empty(Directory.GetFileSystemEntries(barrier));
        var theme = Environment.GetEnvironmentVariable("MYDMSVN_BCF_THEME");
        Assert.Contains(theme, new[] { "Light", "Dark" });
        var scope = Environment.GetEnvironmentVariable("MYDMSVN_BCF_ATTENDED_SCOPE") ?? "both";
        Assert.Contains(scope, new[] { "both", "detail" });
        await GuiSqlEnvironment.RunAsync(package, async fixture =>
        {
            await fixture.SeedReceiptWriterAsync();
            using var probe = new GuiProcessProbeFixture(package: true);
            var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" bcf-lookup-attended " + theme);
            fixture.ConfigureRuntime(start);
            start.EnvironmentVariables["MYDMSVN_GUI_P5_BARRIER"] = barrier;
            start.EnvironmentVariables["MYDMSVN_BCF_ATTENDED_SCOPE"] = scope;
            var result = await GuiOwnedProcess.RunAttendedAsync(start, TimeSpan.FromMinutes(22));
            _output.WriteLine(result.StandardOutput);
            Assert.True(result.CleanupVerified);
            Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
            Assert.Contains("ATTENDED_LOOKUP_SCOPE=" + scope, result.StandardOutput);
            Assert.Contains("ATTENDED_LOOKUP_SESSION_DRAINED=PASS", result.StandardOutput);
            Assert.Contains("APPLICATION_DRAINED=PASS", result.StandardOutput);
            await fixture.AssertNoInventoryAsync();
            _output.WriteLine("ATTENDED_NO_RECEIPT_OR_INVENTORY_WRITES=PASS; human outcome recorded separately.");
        });
        // Exact coordination files only; retained on failure for diagnosis, never an approval gate.
        foreach (var name in new[] { "ready", "observe", "observed", "enter-detail", "complete" }) File.Delete(Path.Combine(barrier, name));
        Directory.Delete(barrier);
    }
    [GuiPackageFact]
    [Trait("Category", "GuiSql")]
    public async Task Disposable_gui_topology_separates_provisioning_migration_runtime_and_readonly_oracle()
    {
        await GuiSqlEnvironment.RunAsync(Candidate(), async fixture =>
        {
            _output.WriteLine(await fixture.DescribeSqlEngineAsync());
            await fixture.VerifySeparationAsync();
        });
    }

    [GuiPackageTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [Trait("Category", "GuiSql")]
    public async Task Actual_packaged_login_uses_native_uia_and_authenticates_against_the_owned_database(string theme)
    {
        var package = Candidate();
        await GuiSqlEnvironment.RunAsync(package, async fixture =>
        {
            using var probe = new GuiProcessProbeFixture(package: true);
            var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" login-audit " + theme);
            fixture.ConfigureRuntime(start);
            var result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(90));
            _output.WriteLine(result.StandardOutput);
            Assert.True(result.ExitCode == 0, result.StandardOutput + Environment.NewLine + result.StandardError);
            Assert.Contains("REAL_LOGIN=PASS", result.StandardOutput);
            Assert.Contains("APPLICATION_THEME=" + theme, result.StandardOutput);
            Assert.Contains("LOGOUT_RELOGIN_RESTART=PASS", result.StandardOutput);
            Assert.Contains("APPLICATION_DRAINED=PASS", result.StandardOutput);
            await fixture.VerifySeparationAsync();
        });
    }

    [GuiPackageTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [Trait("Category", "GuiSql")]
    public async Task Catalog_rows_support_semantic_uia_selection_and_persist_create_edit_active_changes(string theme)
    {
        var package = Candidate();
        await GuiSqlEnvironment.RunAsync(package, async fixture =>
        {
            using var probe = new GuiProcessProbeFixture(package: true);
            var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" catalog-audit " + theme);
            fixture.ConfigureRuntime(start);
            var result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(120));
            _output.WriteLine(result.StandardOutput);
            Assert.True(result.ExitCode == 0, result.StandardOutput + Environment.NewLine + result.StandardError);
            Assert.Contains("CATALOG_PRODUCT=PASS", result.StandardOutput);
            Assert.Contains("CATALOGS=PASS", result.StandardOutput);
            Assert.Contains("APPLICATION_THEME=" + theme, result.StandardOutput);
            Assert.Contains("APPLICATION_DRAINED=PASS", result.StandardOutput);
            await fixture.AssertCatalogProductAsync();
        });
    }

    [GuiPackageTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [Trait("Category", "GuiSql")]
    public async Task Packaged_receipt_draft_reload_edit_post_and_stock_reconcile_with_readonly_sql(string theme)
    {
        var package = Candidate();
        await GuiSqlEnvironment.RunAsync(package, async fixture =>
        {
            using var probe = new GuiProcessProbeFixture(package: true);
            var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" inventory-audit " + theme);
            fixture.ConfigureRuntime(start);
            var result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromMinutes(3));
            _output.WriteLine(result.StandardOutput);
            Assert.True(result.ExitCode == 0, result.StandardOutput + Environment.NewLine + result.StandardError);
            Assert.Contains("INLINE_RECEIPT_NATIVE_EDIT=PASS", result.StandardOutput);
            Assert.Contains("RECEIPT_POST_STOCK=PASS", result.StandardOutput);
            Assert.Contains("SERVER_DUPLICATE_POST_REJECTED=PASS", result.StandardOutput);
            Assert.Contains("APPLICATION_THEME=" + theme, result.StandardOutput);
            Assert.Contains("APPLICATION_DRAINED=PASS", result.StandardOutput);
            await fixture.AssertPostedReceiptAsync();
        });
    }

    [GuiPackageTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [Trait("Category", "GuiSql")]
    public async Task Packaged_inline_receipt_flow_reconciles_with_readonly_sql(string theme)
    {
        var package = Candidate();
        await GuiSqlEnvironment.RunAsync(package, async fixture =>
        {
            await fixture.SeedReceiptWriterAsync();
            using var probe = new GuiProcessProbeFixture(package: true);
            var barrier = Path.Combine(Path.GetTempPath(), "MyDmsVn.GuiBarrier-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(barrier);
            using var cancellation = new CancellationTokenSource();
            var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" inventory-deny " + theme);
            fixture.ConfigureRuntime(start);
            start.EnvironmentVariables["MYDMSVN_GUI_P5_ACTOR_PASSWORD"] = fixture.ActorPassword;
            start.EnvironmentVariables["MYDMSVN_GUI_P5_BARRIER"] = barrier;
            var execution = GuiOwnedProcess.RunAsync(start, TimeSpan.FromMinutes(3), cancellation.Token);
            GuiProcessResult? result = null;
            Exception? initial = null;
            try
            {
                await WaitForSignalAsync(barrier, "receipt-deny-ready", execution, TimeSpan.FromSeconds(110));
                await fixture.DenyReceiptWriteAsync();
                File.WriteAllText(Path.Combine(barrier, "allow-denied-save"), "release");
                await WaitForSignalAsync(barrier, "receipt-denied", execution, TimeSpan.FromSeconds(25));
                await fixture.AssertReceiptDeniedAndRestoreWriteAsync();
                _output.WriteLine("RECEIPT_DENIED_SQL_STATE_AND_ROWVERSION_UNCHANGED=PASS");
                File.WriteAllText(Path.Combine(barrier, "allow-explicit-save"), "release");
                result = await execution;
            }
            catch (Exception error) { initial = error; }
            finally
            {
                if (!execution.IsCompleted) cancellation.Cancel();
                try { await execution; }
                catch (Exception error) { if (!ReferenceEquals(initial, error)) initial = initial == null ? error : new AggregateException(initial, error); }
                foreach (var name in new[] { "receipt-deny-ready", "allow-denied-save", "receipt-denied", "allow-explicit-save" }) File.Delete(Path.Combine(barrier, name));
                Directory.Delete(barrier);
            }
            if (initial != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(initial).Throw();
            _output.WriteLine(result!.StandardOutput);
            Assert.True(result.ExitCode == 0, result.StandardOutput + Environment.NewLine + result.StandardError);
            Assert.Contains("ORDINARY_RECEIPT_ACTOR_LOGIN=PASS", result.StandardOutput);
            Assert.Contains("INLINE_RECEIPT_DIRECT_DENY_INPUT_RETAINED=PASS", result.StandardOutput);
            Assert.Contains("INLINE_RECEIPT_EXPLICIT_SAVE_AFTER_REGRANT=PASS", result.StandardOutput);
            Assert.Contains("INLINE_RECEIPT_ACTOR_POST=PASS", result.StandardOutput);
            Assert.Contains("APPLICATION_DRAINED=PASS", result.StandardOutput);
            await fixture.AssertPostedReceiptAsync("GUI.BCI.RECEIPT.WRITER");
            _output.WriteLine("INLINE_RECEIPT_READONLY_SQL_RECONCILED=PASS");
        });
    }

    [GuiPackageTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [Trait("Category", "GuiSql")]
    public async Task Two_actual_clients_reject_stale_receipt_save_and_preserve_losing_input(string theme)
    {
        var package = Candidate();
        await GuiSqlEnvironment.RunAsync(package, async fixture =>
        {
            using var probe = new GuiProcessProbeFixture(package: true);
            var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" inventory-stale " + theme);
            fixture.ConfigureRuntime(start);
            var result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromMinutes(3));
            _output.WriteLine(result.StandardOutput);
            Assert.True(result.ExitCode == 0, result.StandardOutput + Environment.NewLine + result.StandardError);
            Assert.Contains("STALE_RECEIPT_REJECTED_INPUT_RETAINED=PASS", result.StandardOutput);
            Assert.Contains("APPLICATION_DRAINED=PASS", result.StandardOutput);
            await fixture.AssertConcurrentWinnerAsync();
        });
    }

    [GuiPackageTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [Trait("Category", "GuiSql")]
    public async Task Disabled_login_and_server_direct_deny_reject_a_previously_enabled_write_without_mutation(string theme)
    {
        var package = Candidate();
        await GuiSqlEnvironment.RunAsync(package, async fixture =>
        {
            await fixture.SeedNegativeActorsAsync();
            using var probe = new GuiProcessProbeFixture(package: true);
            var barrier = Path.Combine(Path.GetTempPath(), "MyDmsVn.GuiBarrier-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(barrier);
            using var cancellation = new CancellationTokenSource();
            var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" negative-audit " + theme);
            fixture.ConfigureRuntime(start);
            start.EnvironmentVariables["MYDMSVN_GUI_P5_ACTOR_PASSWORD"] = fixture.ActorPassword;
            start.EnvironmentVariables["MYDMSVN_GUI_P5_BARRIER"] = barrier;
            var execution = GuiOwnedProcess.RunAsync(start, TimeSpan.FromMinutes(2), cancellation.Token);
            GuiProcessResult? result = null;
            Exception? initial = null;
            try
            {
                var wait = Stopwatch.StartNew();
                while (!File.Exists(Path.Combine(barrier, "ready")) && !execution.IsCompleted && wait.Elapsed < TimeSpan.FromSeconds(40))
                    await Task.Delay(25);
                if (!File.Exists(Path.Combine(barrier, "ready")))
                {
                    await execution;
                    throw new TimeoutException("GUI permission barrier was not reached.");
                }
                await fixture.DenyProductWriteAsync();
                File.WriteAllText(Path.Combine(barrier, "release"), "release");
                result = await execution;
            }
            catch (Exception error) { initial = error; }
            finally
            {
                if (!execution.IsCompleted) cancellation.Cancel();
                try { await execution; }
                catch (Exception error) { if (initial == null) initial = error; }
                File.Delete(Path.Combine(barrier, "ready"));
                File.Delete(Path.Combine(barrier, "release"));
                Directory.Delete(barrier); // Exact owned, now empty directory; never recursive/prefix cleanup.
            }
            if (initial != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(initial).Throw();
            _output.WriteLine(result!.StandardOutput);
            Assert.True(result.ExitCode == 0, result.StandardOutput + Environment.NewLine + result.StandardError);
            Assert.Contains("DISABLED_LOGIN_REJECTED=PASS", result.StandardOutput);
            Assert.Contains("SERVER_DIRECT_DENY=PASS", result.StandardOutput);
            Assert.Contains("APPLICATION_DRAINED=PASS", result.StandardOutput);
            await fixture.AssertDeniedAndNoMutationAsync();
        });
    }

    [GuiPackageTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [Trait("Category", "GuiSql")]
    public async Task Same_process_lookup_messages_preserve_history_reject_inactive_new_choices_and_reopen_fresh(string theme)
    {
        var package = Candidate();
        await GuiSqlEnvironment.RunAsync(package, async fixture =>
        {
            using var probe = new GuiProcessProbeFixture(package: true);
            var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" inventory-lookup " + theme);
            fixture.ConfigureRuntime(start);
            var result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromMinutes(3));
            _output.WriteLine(result.StandardOutput);
            Assert.True(result.ExitCode == 0, result.StandardOutput + Environment.NewLine + result.StandardError);
            foreach (var kind in new[] { "Product", "Warehouse", "Employee" })
            {
                Assert.Contains("SAME_PROCESS_LOOKUP_REFRESH=" + kind + ":PASS", result.StandardOutput);
                Assert.Contains("HISTORICAL_LOOKUP_RETAINED=" + kind + ":PASS", result.StandardOutput);
            }
            Assert.Contains("INACTIVE_NEW_SELECTION_REJECTED=PASS", result.StandardOutput);
            Assert.Contains("CHANGED_LOOKUPS_REAL_REOPEN=PASS", result.StandardOutput);
            Assert.Contains("APPLICATION_DRAINED=PASS", result.StandardOutput);
            await fixture.AssertChangedLookupsAsync();
        });
    }

    [GuiPackageTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [Trait("Category", "GuiSql")]
    public async Task Real_sql_post_timeout_locks_mutation_until_explicit_reload_and_checked_retry(string theme)
    {
        var package = Candidate();
        await GuiSqlEnvironment.RunAsync(package, async fixture =>
        {
            using var probe = new GuiProcessProbeFixture(package: true);
            var barrier = Path.Combine(Path.GetTempPath(), "MyDmsVn.GuiBarrier-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(barrier);
            using var cancellation = new CancellationTokenSource();
            var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" inventory-fault " + theme);
            fixture.ConfigureRuntime(start);
            start.EnvironmentVariables["MYDMSVN_GUI_P5_BARRIER"] = barrier;
            var execution = GuiOwnedProcess.RunAsync(start, TimeSpan.FromMinutes(3), cancellation.Token);
            GuiProcessResult? result = null;
            Exception? initial = null;
            try
            {
                await WaitForSignalAsync(barrier, "posting", execution, TimeSpan.FromSeconds(100));
                await fixture.InstallOwnedPostTimeoutAsync();
                File.WriteAllText(Path.Combine(barrier, "allow-post"), "release");
                var waiting = Stopwatch.StartNew();
                var observed = false;
                while (!execution.IsCompleted && waiting.Elapsed < TimeSpan.FromSeconds(35))
                {
                    if (await fixture.OwnedRuntimeIsWaitingInsideSqlAsync()) { observed = true; break; }
                    await Task.Delay(100);
                }
                Assert.True(observed, "The owned runtime SQL session never reached the controlled WAITFOR fault.");
                _output.WriteLine("RUNTIME_SQL_WAITFOR_OBSERVED=PASS");
                await WaitForSignalAsync(barrier, "uncertain", execution, TimeSpan.FromSeconds(45));
                await fixture.AssertTimedOutDraftAndRemoveFaultAsync();
                File.WriteAllText(Path.Combine(barrier, "allow-reload"), "release");
                await WaitForSignalAsync(barrier, "reloaded", execution, TimeSpan.FromSeconds(25));
                await fixture.AssertSavedDraftWithoutPostingAsync(); // Check persisted state again after the UI's explicit read.
                File.WriteAllText(Path.Combine(barrier, "allow-retry"), "release");
                result = await execution;
            }
            catch (Exception error) { initial = error; }
            finally
            {
                if (!execution.IsCompleted) cancellation.Cancel();
                try { await execution; }
                catch (Exception error)
                {
                    if (!ReferenceEquals(initial, error)) initial = initial == null ? error : new AggregateException(initial, error);
                }
                foreach (var name in new[] { "posting", "allow-post", "uncertain", "allow-reload", "reloaded", "allow-retry" }) File.Delete(Path.Combine(barrier, name));
                Directory.Delete(barrier);
            }
            if (initial != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(initial).Throw();
            _output.WriteLine(result!.StandardOutput);
            Assert.True(result.ExitCode == 0, result.StandardOutput + Environment.NewLine + result.StandardError);
            Assert.Contains("RECOVERY_NATIVE_TAB_REACHES_REFRESH=PASS", result.StandardOutput);
            Assert.Contains("RECOVERY_NATIVE_ENTER_SENT=PASS", result.StandardOutput);
            Assert.Contains("UNCERTAIN_POST_RELOAD_EXPLICIT_RETRY=PASS", result.StandardOutput);
            Assert.Contains("FAULT_REMOVED_STARTUP_REVALIDATED=PASS", result.StandardOutput);
            Assert.Contains("APPLICATION_DRAINED=PASS", result.StandardOutput);
            await fixture.AssertPostedReceiptAsync();
        });
    }

    private static async Task WaitForSignalAsync(string directory, string name, Task execution, TimeSpan timeout)
    {
        var wait = Stopwatch.StartNew();
        while (!File.Exists(Path.Combine(directory, name)))
        {
            if (execution.IsCompleted) { await execution; throw new InvalidOperationException("Owned probe exited before coordination point " + name); }
            if (wait.Elapsed >= timeout) throw new TimeoutException("Owned probe did not reach coordination point " + name);
            await Task.Delay(25);
        }
    }

    [GuiPackageTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [Trait("Category", "GuiSql")]
    public async Task Invalid_receipt_fields_preserve_input_and_create_no_inventory(string theme)
    {
        var package = Candidate();
        await GuiSqlEnvironment.RunAsync(package, async fixture =>
        {
            using var probe = new GuiProcessProbeFixture(package: true);
            var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" invalid-audit " + theme);
            fixture.ConfigureRuntime(start);
            var result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(90));
            _output.WriteLine(result.StandardOutput);
            Assert.True(result.ExitCode == 0, result.StandardOutput + Environment.NewLine + result.StandardError);
            Assert.Contains("INVALID_FIELDS_NO_WRITE=PASS", result.StandardOutput);
            Assert.Contains("APPLICATION_DRAINED=PASS", result.StandardOutput);
            await fixture.AssertNoInventoryAsync();
        });
    }

    [GuiPackageFact]
    public async Task Unavailable_sql_startup_is_safe_owned_and_drains_under_native_uia()
    {
        var package = Candidate();
        using var probe = new GuiProcessProbeFixture(package: true);
        var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" startup-unavailable");
        ClearSqlEnvironment(start);
        start.EnvironmentVariables["Security__SuperuserUsername"] = "gui.p5.owner";
        start.EnvironmentVariables["MYDMSVN_GUI_P5_EXPECTED_SCALE"] = Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_EXPECTED_SCALE") ?? "100";
        start.EnvironmentVariables["MYDMSVN_RUNTIME_SQLSERVER_CONNECTION_STRING"] =
            "Server=127.0.0.1,1;Database=Unavailable;User ID=GuiP5_Unavailable;Password=" + Guid.NewGuid().ToString("N") +
            ";Integrated Security=false;Encrypt=false;Connect Timeout=1;ConnectRetryCount=0;Pooling=false;";
        var result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(30));
        _output.WriteLine(result.StandardOutput);
        Assert.True(result.ExitCode == 0, result.StandardOutput + Environment.NewLine + result.StandardError);
        Assert.Contains("BITNESS=32", result.StandardOutput);
        Assert.Contains("UIA_DPI_CONTEXT=PerMonitorV2", result.StandardOutput);
        Assert.Contains("STARTUP_SAFE_ERROR=PASS", result.StandardOutput);
        Assert.Contains("APPLICATION_DRAINED=PASS", result.StandardOutput);
    }

    internal static void ClearSqlEnvironment(ProcessStartInfo start)
    {
        foreach (var key in start.EnvironmentVariables.Keys.Cast<string>().ToArray())
            if (key.StartsWith("MYDMSVN_", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("Security__", StringComparison.OrdinalIgnoreCase))
                start.EnvironmentVariables.Remove(key);
    }

    private string Candidate()
    {
        var root = ValidatedPackage();
        var manifestPath = Path.Combine(root, "release-manifest.json");
        var manifest = JObject.Parse(File.ReadAllText(manifestPath));
        _output.WriteLine("PACKAGE_SOURCE=" + manifest.Value<string>("sourceCommit"));
        using (var stream = File.OpenRead(manifestPath))
        using (var hash = SHA256.Create())
            _output.WriteLine("MANIFEST_SHA256=" + BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""));
        // Legacy variable name: it pins package provenance; the actual test build is separate below.
        var revision = Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_TEST_REVISION");
        if (revision != null && System.Text.RegularExpressions.Regex.IsMatch(revision, "^[a-fA-F0-9]{40}$"))
        {
            Assert.Equal(manifest.Value<string>("sourceCommit"), revision, ignoreCase: true);
            _output.WriteLine("EXPECTED_PACKAGE_SOURCE=" + revision);
        }
        else _output.WriteLine("EXPECTED_PACKAGE_SOURCE=UNPINNED_TRIAL");
        // Product bytes can stay frozen while the test-only driver changes.
        _output.WriteLine("TEST_BUILD_INFORMATIONAL_VERSION=" +
            typeof(PackagedGuiAcceptanceTests).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        return root;
    }
    internal static string ValidatedPackage()
    {
        var root = Path.GetFullPath(Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_PACKAGE")!);
        var manifest = JObject.Parse(File.ReadAllText(Path.Combine(root, "release-manifest.json")));
        Assert.Equal(JTokenType.Boolean, manifest["sourceTreeDirty"]!.Type);
        Assert.False(manifest.Value<bool>("sourceTreeDirty"));
        Assert.Matches("^[a-fA-F0-9]{40}$", manifest.Value<string>("sourceCommit")!);
        Assert.Equal(9, manifest["schemaSupport"]!.Value<int>("maximum"));
        Assert.Equal(2, manifest["packages"]!.Count());
        foreach (var package in manifest["packages"]!)
        {
            Assert.Equal("net48", package.Value<string>("targetFramework"));
            Assert.Equal("win-x86", package.Value<string>("architecture"));
            Assert.Contains(package.Value<string>("path"), new[] { "desktop-net48-win-x86", "admin-migrator-net48-win-x86" });
            var directory = Path.Combine(root, package.Value<string>("path")!);
            foreach (var file in package["files"]!)
            {
                var path = Path.GetFullPath(Path.Combine(directory, file.Value<string>("path")!));
                Assert.StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, path, StringComparison.OrdinalIgnoreCase);
                using var stream = File.OpenRead(path);
                Assert.Equal(file.Value<long>("length"), stream.Length);
                using var hash = SHA256.Create();
                Assert.Equal(file.Value<string>("sha256"), BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""));
            }
        }
        var desktop = Assembly.ReflectionOnlyLoadFrom(Path.Combine(root, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe"));
        desktop.ManifestModule.GetPEKind(out var pe, out var machine);
        Assert.Equal(ImageFileMachine.I386, machine);
        Assert.True((pe & PortableExecutableKinds.Required32Bit) != 0);
        return root;
    }
}
