using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

public sealed class PackagedGuiAcceptanceTests
{
    private readonly ITestOutputHelper _output;
    public PackagedGuiAcceptanceTests(ITestOutputHelper output) { _output = output; }
    [GuiPackageFact]
    [Trait("Category", "GuiSql")]
    public async Task Disposable_gui_topology_separates_provisioning_migration_runtime_and_readonly_oracle()
    {
        await GuiSqlEnvironment.RunAsync(ValidatedPackage(), fixture => fixture.VerifySeparationAsync());
    }

    [GuiPackageFact]
    [Trait("Category", "GuiSql")]
    public async Task Actual_packaged_login_uses_native_uia_and_authenticates_against_the_owned_database()
    {
        var package = ValidatedPackage();
        await GuiSqlEnvironment.RunAsync(package, async fixture =>
        {
            using var probe = new GuiProcessProbeFixture(package: true);
            var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" login-audit");
            fixture.ConfigureRuntime(start);
            var result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(90));
            _output.WriteLine(result.StandardOutput);
            Assert.True(result.ExitCode == 0, result.StandardOutput + Environment.NewLine + result.StandardError);
            Assert.Contains("REAL_LOGIN=PASS", result.StandardOutput);
            Assert.Contains("LOGOUT_RELOGIN_RESTART=PASS", result.StandardOutput);
            Assert.Contains("APPLICATION_DRAINED=PASS", result.StandardOutput);
            await fixture.VerifySeparationAsync();
        });
    }

    [GuiPackageFact]
    [Trait("Category", "GuiSql")]
    public async Task Catalog_rows_support_semantic_uia_selection_and_persist_create_edit_active_changes()
    {
        var package = ValidatedPackage();
        await GuiSqlEnvironment.RunAsync(package, async fixture =>
        {
            using var probe = new GuiProcessProbeFixture(package: true);
            var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" catalog-audit");
            fixture.ConfigureRuntime(start);
            var result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(120));
            _output.WriteLine(result.StandardOutput);
            Assert.True(result.ExitCode == 0, result.StandardOutput + Environment.NewLine + result.StandardError);
            Assert.Contains("CATALOG_PRODUCT=PASS", result.StandardOutput);
            Assert.Contains("APPLICATION_DRAINED=PASS", result.StandardOutput);
            await fixture.AssertCatalogProductAsync();
        });
    }

    [GuiPackageFact]
    public async Task Unavailable_sql_startup_is_safe_owned_and_drains_under_native_uia()
    {
        var package = ValidatedPackage();
        using var probe = new GuiProcessProbeFixture(package: true);
        var start = probe.Start("\"" + Path.Combine(package, "desktop-net48-win-x86", "MyDmsVn.Desktop.App.exe") + "\" startup-unavailable");
        ClearSqlEnvironment(start);
        start.EnvironmentVariables["Security__SuperuserUsername"] = "gui.p5.owner";
        start.EnvironmentVariables["MYDMSVN_RUNTIME_SQLSERVER_CONNECTION_STRING"] =
            "Server=127.0.0.1,1;Database=Unavailable;Integrated Security=true;Encrypt=false;Connect Timeout=1;ConnectRetryCount=0;Pooling=false;";
        var result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(30));
        _output.WriteLine(result.StandardOutput);
        Assert.True(result.ExitCode == 0, result.StandardOutput + Environment.NewLine + result.StandardError);
        Assert.Contains("BITNESS=32", result.StandardOutput);
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
        return root;
    }
}
