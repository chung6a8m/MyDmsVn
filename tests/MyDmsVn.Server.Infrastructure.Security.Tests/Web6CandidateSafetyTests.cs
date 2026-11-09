using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CandidateSafetyTests
{
    private readonly ITestOutputHelper _output;
    public Web6CandidateSafetyTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Manifest_is_create_new_sealed_and_removed_only_by_exact_owner()
    {
        using var control = SqlTestOwnershipManifest.Create(Path.GetTempPath());
        Write(control);
        control.Verify();
        Assert.Throws<InvalidOperationException>(() => Write(control));
        control.RemoveAfterVerifiedAbsence();
        Assert.False(Directory.Exists(control.ControlPath));
    }

    [Fact]
    public void Uncertain_scope_disposal_retains_the_original_manifest()
    {
        var control = SqlTestOwnershipManifest.Create(Path.GetTempPath());
        Write(control);
        control.Dispose();
        Assert.True(File.Exists(Path.Combine(control.ControlPath, "allocation.txt")));
        // Original in-memory identity is still held by this pure test; no SQL exists.
        control.RemoveAfterVerifiedAbsence();
    }

    [Fact]
    public void Manifest_handle_refuses_replacement_and_data_tamper()
    {
        using var control = SqlTestOwnershipManifest.Create(Path.GetTempPath());
        Write(control);
        var file = Path.Combine(control.ControlPath, "allocation.txt");
        Assert.Throws<IOException>(() => File.Move(file, file + ".replacement"));
        Assert.Throws<IOException>(() => File.WriteAllText(file, "tampered"));
        control.Verify();
        control.RemoveAfterVerifiedAbsence();
    }

    [Fact]
    public void Manifest_refuses_acl_drift_and_retains_until_original_acl_is_restored()
    {
        using var parent = SqlTestOwnershipManifest.OwnedAclParent.Create(Path.GetTempPath());
        using var control = parent.CreateTarget();
        Write(control);
        // Independent create-new/flush/readback record must exist outside this case first.
        using var authority = control.PreserveBeforeAclMutation(parent, marker => _output.WriteLine(marker));
        var changed = Directory.GetAccessControl(control.ControlPath, SqlTestPrivateRecord.Sections);
        changed.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.Read, AccessControlType.Allow));
        authority.RunMutation(() => Directory.SetAccessControl(control.ControlPath, changed),
            () => Assert.Throws<IOException>(() => control.RemoveAfterVerifiedAbsence()));
        control.Verify();
        control.RemoveAfterVerifiedAbsence();
        parent.RemoveAfterVerifiedChildren();
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("traversal")]
    [InlineData("long")]
    public void Manifest_refuses_noncanonical_or_overbudget_parent(string kind)
    {
        var parent = kind == "relative" ? "." : kind == "traversal" ? Path.GetTempPath() + "child\\.." : "C:\\" + new string('a', 230);
        Assert.Throws<IOException>(() => SqlTestOwnershipManifest.Create(parent));
        Assert.Throws<IOException>(() => SqlTestOwnershipManifest.RejectReparse(FileAttributes.ReparsePoint | FileAttributes.Directory));
    }

    [Fact]
    public void Manifest_refuses_unexpected_children_without_removing_any_entry()
    {
        using var control = SqlTestOwnershipManifest.Create(Path.GetTempPath());
        Write(control);
        var extra = Path.Combine(control.ControlPath, "unexpected.txt");
        File.WriteAllText(extra, "test-owned");
        try { Assert.Throws<IOException>(() => control.RemoveAfterVerifiedAbsence()); Assert.True(File.Exists(extra)); }
        finally { File.Delete(extra); }
        control.RemoveAfterVerifiedAbsence();
    }

    [Fact]
    public async Task Exact_net48_http_and_crash_graph_loads_without_SQL()
    {
        var expected = Environment.GetEnvironmentVariable("MYDMSVN_WEB6_EXPECTED_EVIDENCE_TEMP");
        Assert.False(string.IsNullOrWhiteSpace(expected));
        RequireMatch(string.Equals(Path.GetFullPath(expected!).TrimEnd('\\'), Path.GetTempPath().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        RequireMatch(string.Equals(Path.GetFullPath(expected!).TrimEnd('\\'), Environment.GetEnvironmentVariable("TEMP")!.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        RequireMatch(string.Equals(Path.GetFullPath(expected!).TrimEnd('\\'), Environment.GetEnvironmentVariable("TMP")!.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        var graph = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "CrashHost"), "*", SearchOption.AllDirectories);
        var maximum = graph.Max(path => path.Length);
        Assert.True(maximum <= 240, "Crash-host dependency path budget exceeded.");
        Assert.True(Path.GetTempPath().TrimEnd('\\').Length + 180 <= 240, "Projected transient path budget exceeded.");
        WebAuthHttpSqlTests.Scenario.LoadWithoutSql();
        var child = await WebOperationCrashProcess.StartAsync(new JObject {
            ["connectionString"]="WEB5-no-SQL-may-open", ["phase"]="none", ["method"]="GET",
            ["path"]="operations/context", ["body"]=JValue.CreateNull(), ["access"]=JValue.CreateNull(), ["headers"]=new JObject()
        });
        await child.CloseAsync();
        Assert.False(WebOperationCrashProcess.IsPoisoned);
        _output.WriteLine("WEB6_PATH_PREFLIGHT sql=0 graphLoaded=1 childrenDrained=1 projectedFullPathMaximum=240 actualGraphMaximum={0}", maximum);
    }

    private static void RequireMatch(bool match) => Assert.True(match, "Evidence environment mismatch.");

    private static void Write(SqlTestOwnershipManifest control) => control.WriteVerifiedRecord(
        "MyDmsVn_Test_11111111111141118111111111111111", "test-server", 17,
        new DateTime(2026, 11, 9, 1, 2, 3), "22222222222242228222222222222222", false, DateTimeOffset.UtcNow);
}
