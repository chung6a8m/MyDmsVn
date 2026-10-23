using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed partial class PackagedGuiAcceptanceTests
{
    [GuiPackageFact]
    public void Current_and_historical_package_metadata_remain_distinct_in_one_process()
    {
        var current = ValidatedPackage();
        var historical = Environment.GetEnvironmentVariable("MYDMSVN_GUI_SAL_SCHEMA9_PACKAGE");
        Assert.False(string.IsNullOrWhiteSpace(historical), "The verified historical schema9 package is required.");
        ValidatePackageDirectory(historical!, 9);
        Assert.Throws<Xunit.Sdk.EqualException>(() => ValidatePackageDirectory(historical!, 10));
        Assert.Throws<Xunit.Sdk.EqualException>(() => ValidatePackageDirectory(current, 9));
        ValidatedPackage();
    }

    [GuiPackageTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [Trait("Category", "GuiSql")]
    public async Task Actual_sales_list_accepts_native_numeric_page_size(string theme)
    {
        var package = Candidate();
        await GuiSqlEnvironment.RunAsync(package, async fixture =>
        {
            await fixture.SeedSalesWriterAsync();
            using var probe = new GuiProcessProbeFixture(package: true);
            PreserveSalesProbe(probe);
            var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" sales-list-input " + theme);
            fixture.ConfigureRuntime(start);
            start.EnvironmentVariables["MYDMSVN_GUI_P5_ACTOR_PASSWORD"] = fixture.ActorPassword;
            var result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(90));
            _output.WriteLine(result.StandardOutput);
            Assert.True(result.CleanupVerified);
            Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
            Assert.Contains("SALES_NATIVE_PAGE_SIZE=PASS", result.StandardOutput);
            await fixture.AssertSalesInventoryAsync(20m, 300m, 0);
        });
    }

    [GuiPackageTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [Trait("Category", "GuiSql")]
    public async Task DirectSales_CostAndStockMatchIndependentOracle(string theme)
    {
        var package = Candidate();
        await GuiSqlEnvironment.RunAsync(package, async fixture =>
        {
            await fixture.SeedSalesWriterAsync();
            await fixture.SeedSalesListProductAsync();
            using var probe = new GuiProcessProbeFixture(package: true);
            PreserveSalesProbe(probe);
            var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" sales-audit " + theme);
            fixture.ConfigureRuntime(start);
            if (Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_TRACE_LOOKUPS") == "1")
                start.EnvironmentVariables["MYDMSVN_GUI_P5_TRACE_LOOKUPS"] = "1";
            start.EnvironmentVariables["MYDMSVN_GUI_P5_ACTOR_PASSWORD"] = fixture.ActorPassword;
            var result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromMinutes(4));
            _output.WriteLine(result.StandardOutput);
            Assert.True(result.CleanupVerified);
            Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
            foreach (var marker in new[] { "SALES_ACTUAL_LOGIN", "SALES_DUPLICATE_GIFT_RELOAD", "SALES_POSTED_IMMUTABLE", "SALES_KEYSET_PRODUCT_TOTALS", "APPLICATION_DRAINED" })
                Assert.Contains(marker + "=PASS", result.StandardOutput);
            await fixture.AssertSalesListFixtureAsync(result.StandardOutput);
            await fixture.AssertSalesGoldenAsync();
            await fixture.AddOwnedSalesStockAsync(true);
            await fixture.AssertSalesGoldenAsync(later: true);
            var inspect = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" sales-inspect " + theme);
            fixture.ConfigureRuntime(inspect);
            inspect.EnvironmentVariables["MYDMSVN_GUI_P5_ACTOR_PASSWORD"] = fixture.ActorPassword;
            var reread = await GuiOwnedProcess.RunAsync(inspect, TimeSpan.FromSeconds(90));
            _output.WriteLine(reread.StandardOutput);
            Assert.True(reread.CleanupVerified);
            Assert.True(reread.ExitCode == 0, reread.StandardOutput + reread.StandardError);
            Assert.Contains("SALES_LATER_RECEIPT_COST_IMMUTABLE=PASS", reread.StandardOutput);
            await fixture.VerifySeparationAsync();
        });
    }

    [GuiPackageTheory]
    [InlineData("Light", false)]
    [InlineData("Dark", false)]
    [InlineData("Light", true)]
    [InlineData("Dark", true)]
    [Trait("Category", "GuiSql")]
    public Task Two_actual_sales_clients_reject_stale_save_or_post_without_duplicate_effect(string theme, bool post) =>
        RunSalesCoordinatedAsync(theme, post ? "sales-stale-post" : "sales-stale", async (fixture, barrier, execution) =>
        {
            await WaitForSignalAsync(barrier, "sales-stale-winner-ready", execution, TimeSpan.FromSeconds(200));
            await fixture.CaptureSalesWinnerVersionAsync(post);
            File.WriteAllText(Path.Combine(barrier, "sales-allow-loser"), "release");
            await WaitForSignalAsync(barrier, "sales-stale-conflict-ready", execution, TimeSpan.FromSeconds(40));
            await fixture.AssertSalesWinnerUnchangedAsync(post);
            _output.WriteLine("SALES_STALE_WINNER_VERSION_AND_LINES_UNCHANGED=PASS");
            File.WriteAllText(Path.Combine(barrier, "sales-stale-checked"), "release");
        }, post ? "SALES_STALE_POST_ONE_EFFECT=PASS" : "SALES_STALE_SAVE_INPUT_RETAINED=PASS");

    [GuiPackageTheory]
    [InlineData("Light", "Read")]
    [InlineData("Dark", "Read")]
    [InlineData("Light", "Write")]
    [InlineData("Dark", "Write")]
    [InlineData("Light", "Post")]
    [InlineData("Dark", "Post")]
    [Trait("Category", "GuiSql")]
    public Task Actual_sales_direct_deny_clears_protected_presentation_without_mutation(string theme, string permission) =>
        RunSalesCoordinatedAsync(theme, "sales-deny-" + permission, async (fixture, barrier, execution) =>
        {
            await WaitForSignalAsync(barrier, "sales-deny-ready", execution, TimeSpan.FromSeconds(160));
            await fixture.DenySalesPermissionAsync(permission);
            File.WriteAllText(Path.Combine(barrier, "sales-allow-denied-operation"), "release");
            await WaitForSignalAsync(barrier, "sales-denied", execution, TimeSpan.FromSeconds(35));
            await fixture.AssertSalesDraftAsync("SAL golden", unchangedVersion: true);
            File.WriteAllText(Path.Combine(barrier, "sales-denial-checked"), "release");
        }, "SALES_" + permission.ToUpperInvariant() + "_DENIED_PROTECTED_STATE_CLEARED=PASS");

    [GuiPackageTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [Trait("Category", "GuiSql")]
    public Task Actual_sales_sql_post_timeout_requires_authoritative_reload_and_one_explicit_retry(string theme) =>
        RunSalesCoordinatedAsync(theme, "sales-fault", async (fixture, barrier, execution) =>
        {
            await WaitForSignalAsync(barrier, "sales-post-ready", execution, TimeSpan.FromSeconds(160));
            await fixture.InstallOwnedSalesPostTimeoutAsync();
            File.WriteAllText(Path.Combine(barrier, "sales-allow-post"), "release");
            var clock = Stopwatch.StartNew();
            var observed = false;
            while (!execution.IsCompleted && clock.Elapsed < TimeSpan.FromSeconds(35))
            {
                if (await fixture.OwnedRuntimeIsWaitingInsideSqlAsync()) { observed = true; break; }
                await Task.Delay(100);
            }
            Assert.True(observed, "Owned Sales runtime never reached actual SQL WAITFOR.");
            _output.WriteLine("SALES_RUNTIME_SQL_WAITFOR_OBSERVED=PASS");
            await WaitForSignalAsync(barrier, "sales-uncertain", execution, TimeSpan.FromSeconds(55));
            await fixture.AssertSalesTimeoutAndRemoveFaultAsync();
            File.WriteAllText(Path.Combine(barrier, "sales-allow-reload"), "release");
            await WaitForSignalAsync(barrier, "sales-reloaded", execution, TimeSpan.FromSeconds(30));
            await fixture.AssertSalesDraftAsync("SAL golden", unchangedVersion: true);
            File.WriteAllText(Path.Combine(barrier, "sales-allow-retry"), "release");
        }, "SALES_UNCERTAIN_RELOAD_EXPLICIT_RETRY=PASS");

    private async Task RunSalesCoordinatedAsync(string theme, string scenario,
        Func<GuiSqlEnvironment, string, Task, Task> coordinate, string marker)
    {
        var package = Candidate();
        await GuiSqlEnvironment.RunAsync(package, async fixture =>
        {
            await fixture.SeedSalesWriterAsync();
            using var probe = new GuiProcessProbeFixture(package: true);
            PreserveSalesProbe(probe);
            var barrier = Path.Combine(Path.GetTempPath(), "MyDmsVn.GuiSal-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(barrier);
            using var cancellation = new CancellationTokenSource();
            var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" " + scenario + " " + theme);
            fixture.ConfigureRuntime(start);
            if (Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_TRACE_LOOKUPS") == "1")
                start.EnvironmentVariables["MYDMSVN_GUI_P5_TRACE_LOOKUPS"] = "1";
            start.EnvironmentVariables["MYDMSVN_GUI_P5_ACTOR_PASSWORD"] = fixture.ActorPassword;
            start.EnvironmentVariables["MYDMSVN_GUI_P5_BARRIER"] = barrier;
            var execution = GuiOwnedProcess.RunAsync(start, TimeSpan.FromMinutes(5), cancellation.Token);
            GuiProcessResult? result = null;
            Exception? failure = null;
            try { await coordinate(fixture, barrier, execution); result = await execution; }
            catch (Exception error) { failure = error; }
            finally
            {
                if (!execution.IsCompleted) cancellation.Cancel();
                try { await execution; }
                catch (Exception error) { if (!ReferenceEquals(failure, error)) failure = failure == null ? error : new AggregateException(failure, error); }
                foreach (var name in new[] { "sales-deny-ready", "sales-allow-denied-operation", "sales-denied", "sales-denial-checked",
                    "sales-post-ready", "sales-allow-post", "sales-uncertain", "sales-allow-reload", "sales-reloaded", "sales-allow-retry",
                    "sales-stale-winner-ready", "sales-allow-loser", "sales-stale-conflict-ready", "sales-stale-checked" })
                    File.Delete(Path.Combine(barrier, name));
                Directory.Delete(barrier);
            }
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            _output.WriteLine(result!.StandardOutput);
            Assert.True(result.CleanupVerified);
            Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
            Assert.Contains(marker, result.StandardOutput);
            Assert.Contains("APPLICATION_DRAINED=PASS", result.StandardOutput);
            if (scenario == "sales-fault") await fixture.AssertSalesGoldenAsync();
        });
    }

    [GuiPackageTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [Trait("Category", "GuiSql")]
    public async Task Actual_packages_enforce_schema9_quiesced_admin_upgrade_and_schema10_runtime_boundary(string theme)
    {
        var package = Candidate();
        var historical = Environment.GetEnvironmentVariable("MYDMSVN_GUI_SAL_SCHEMA9_PACKAGE");
        Assert.False(string.IsNullOrWhiteSpace(historical), "The verified historical schema9 package is required.");
        var schema9 = ValidatePackageDirectory(historical!, 9);
        var oldManifestPath = Path.Combine(schema9, "release-manifest.json");
        var oldManifest = JObject.Parse(File.ReadAllText(oldManifestPath));
        Assert.Equal("95591071ea214be88813deb5227d216f1b0bca71", oldManifest.Value<string>("sourceCommit"));
        using (var hash = SHA256.Create())
            Assert.Equal("3376D54C49FAD98152CB5A8BC7F94EDFEF8F7E9D13B15A16A3A126D642347447", BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(oldManifestPath))).Replace("-", ""));
        try
        {
            await GuiSqlEnvironment.RunSchemaTransitionAsync(package, schema9, async fixture =>
            {
                await fixture.SeedSalesWriterAsync();
                await fixture.CaptureLegacySchema9Async();
                await AssertPackagedSchemaRefusalAsync(fixture, package, "Schema.TooOld", theme);
                await fixture.AssertLegacyPreservedAsync(upgraded: false);
                await fixture.AddPartial010Async();
                await AssertPackagedSchemaRefusalAsync(fixture, package, "Schema.PartialMigration", theme);
                var partial = await fixture.RunPackagedMigratorAsync(package);
                Assert.NotEqual(0, partial.ExitCode);
                Assert.Contains("Migration.PartialSchema", partial.StandardError + partial.StandardOutput);
                await fixture.AssertPartial010UnchangedAsync();
                await fixture.AssertLegacyPreservedAsync(upgraded: false);
                _output.WriteLine("PACKAGED_SCHEMA9_PARTIAL_REFUSAL_IMMUTABLE=PASS");
            });
            // A partial state is retained for diagnosis and owned cleanup. A second,
            // pristine exact9 allocation proves upgrade; never heuristically repair
            // partial DDL (dropping a column does not restore physical ordinals).
            await GuiSqlEnvironment.RunSchemaTransitionAsync(package, schema9, async fixture =>
            {
                await fixture.SeedSalesWriterAsync();
                await fixture.UpgradeOwnedSchema9Async(package);
                _output.WriteLine("PACKAGED_LEGACY_SOURCE_LEDGER_JOURNAL_PRESERVED=PASS");
                _output.WriteLine("PACKAGED_SCHEMA10_NOOP_ROWS_TOKENS_JOURNAL_ZERO_SCRIPTS=PASS");
                var beforeRuntimeRefusal = await fixture.ReadSchema10SnapshotAsync();
                var refused = await fixture.RunPackagedMigratorAsync(package, runtime: true);
                Assert.NotEqual(0, refused.ExitCode);
                Assert.Contains("Migration.InsufficientPrivileges", refused.StandardError + refused.StandardOutput);
                // This is the genuine historical schema9 executable, with its own
                // original validated bytes, observing current schema10 read-only.
                await AssertPackagedSchemaRefusalAsync(fixture, schema9, "Schema.TooNew", theme);
                Assert.Equal(beforeRuntimeRefusal, await fixture.ReadSchema10SnapshotAsync());
                await fixture.ApplySchema10RuntimeGrantsAsync();
                using var probe = new GuiProcessProbeFixture(package: true);
                PreserveSalesProbe(probe);
                var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" login-audit " + theme);
                fixture.ConfigureRuntime(start);
                var admitted = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(90));
                _output.WriteLine(admitted.StandardOutput);
                Assert.True(admitted.CleanupVerified);
                Assert.Equal(0, admitted.ExitCode);
                Assert.Contains("REAL_LOGIN=PASS", admitted.StandardOutput);
                await fixture.AssertSalesInventoryAsync(20m, 300m, 0);
                await fixture.VerifySeparationAsync();
            });
        }
        finally { ValidatePackageDirectory(schema9, 9); }
    }

    private async Task AssertPackagedSchemaRefusalAsync(GuiSqlEnvironment fixture, string package, string code, string theme)
    {
        using var probe = new GuiProcessProbeFixture(package: true);
        PreserveSalesProbe(probe);
        var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" startup-schema-" + code + " " + theme);
        fixture.ConfigureRuntime(start);
        var result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(45));
        _output.WriteLine(result.StandardOutput);
        Assert.True(result.CleanupVerified);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("PACKAGED_SCHEMA_REFUSED=" + code + ":PASS", result.StandardOutput);
    }

    private void PreserveSalesProbe(GuiProcessProbeFixture probe)
    {
        var executable = probe.Start("unused").FileName;
        var destination = Path.Combine(GuiSqlEnvironment.RepositoryRoot(), ".local", "t08", "probe-provenance", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(destination);
        foreach (var path in new[] { executable, Path.ChangeExtension(executable, ".cs") })
        {
            var copy = Path.Combine(destination, Path.GetFileName(path));
            File.Copy(path, copy);
            using var hash = SHA256.Create();
            var digest = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(copy))).Replace("-", "");
            File.AppendAllText(Path.Combine(destination, "before-launch.sha256"), Path.GetFileName(copy) + " " + digest + Environment.NewLine);
            _output.WriteLine("PROBE_BEFORE_LAUNCH_SHA256=" + Path.GetFileName(copy) + ":" + digest);
        }
    }
}
