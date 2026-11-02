using System;
using System.IO;
using System.Data;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebRecoveryPilotResultTests
{
    private static readonly Guid Epoch = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static JObject Operation() => new JObject
    {
        ["actor"] = "integratedEditor", ["operationId"] = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee",
        ["operationKind"] = "Sales.Create", ["requestTargetId"] = null, ["resultDocumentId"] = "1",
        ["requestFingerprint"] = new string('a', 64), ["deploymentEpoch"] = Epoch.ToString("D"),
    };
    private static JObject Result() => new JObject
    {
        ["version"] = 1, ["mode"] = "web4-recovery", ["posted"] = new JArray(),
        ["operations"] = new JArray(Operation()), ["checks"] = new JArray(WebHostIisExpressTests.RecoveryChecks),
    };
    private static void Verify(JObject result, JArray? physical = null) => WebHostIisExpressTests.VerifyRecoveryResult(result,
        physical ?? new JArray(Operation()), Array.Empty<long>(), Array.Empty<long>(), new[] { Epoch });

    [Fact]
    public void Committed_draft_create_matches_original_null_target_and_physical_journal() => Verify(Result());

    [Theory]
    [InlineData(1, "Sales.Create", false)]
    [InlineData(2, "Sales.Update", true)]
    [InlineData(3, "Sales.Post", true)]
    [InlineData(4, "Receipt.Create", false)]
    [InlineData(5, "Receipt.Update", true)]
    [InlineData(6, "Receipt.Post", true)]
    public void SQL_projection_preserves_command_kind_and_JSON_null_or_string_target_before_exact_comparison(int kind, string wireKind, bool target)
    {
        using var table = new DataTable();
        table.Columns.Add("Actor", typeof(string)); table.Columns.Add("OperationId", typeof(Guid));
        table.Columns.Add("OperationKind", typeof(byte)); table.Columns.Add("RequestTargetId", typeof(long));
        table.Columns.Add("ResultDocumentId", typeof(long)); table.Columns.Add("RequestFingerprint", typeof(byte[]));
        table.Columns.Add("DeploymentEpoch", typeof(Guid)); table.Columns.Add("Disposition", typeof(string));
        table.Rows.Add("integratedEditor", Guid.Parse((string)Operation()["operationId"]!), (byte)kind, target ? (object)1L : DBNull.Value,
            1L, Enumerable.Repeat((byte)0xaa, 32).ToArray(), Epoch, "COMMITTED");
        using var record = table.CreateDataReader(); Assert.True(record.Read());
        var actual = WebHostIisExpressTests.RecoveryJournalFromRecord(record);
        Assert.Equal(target ? JTokenType.String : JTokenType.Null, actual["requestTargetId"]!.Type);
        var result = Result();
        result["operations"]![0]!["operationKind"] = wireKind;
        if (target) result["operations"]![0]!["requestTargetId"] = "1";
        Verify(result, new JArray(actual));
    }

    [Fact]
    public void Omitted_committed_operation_is_rejected()
    {
        var physical = new JArray(Operation(), Operation()); physical[1]["operationId"] = Guid.NewGuid().ToString("D");
        Assert.Throws<InvalidDataException>(() => Verify(Result(), physical));
    }

    [Fact]
    public void Duplicate_claim_cannot_hide_missing_physical_key()
    {
        var result = Result(); ((JArray)result["operations"]!).Add(Operation());
        Assert.Throws<InvalidDataException>(() => Verify(result));
    }

    [Theory]
    [InlineData("actor", "poster")]
    [InlineData("operationId", "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeef")]
    [InlineData("operationKind", "Receipt.Create")]
    [InlineData("resultDocumentId", "2")]
    [InlineData("requestFingerprint", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    [InlineData("deploymentEpoch", "11111111-2222-4333-8444-555555555556")]
    public void Plausible_but_wrong_operation_identity_is_rejected(string property, string value)
    {
        var result = Result(); result["operations"]![0]![property] = value;
        Assert.Throws<InvalidDataException>(() => Verify(result));
    }

    [Theory]
    [InlineData("requestTargetId", "1")]
    [InlineData("resultDocumentId", "01")]
    [InlineData("operationKind", "Sales.Delete")]
    [InlineData("requestFingerprint", "not-a-sha256")]
    [InlineData("actor", "web.smoke")]
    public void Malformed_claims_are_rejected_before_SQL_comparison(string property, string value)
    {
        var result = Result(); result["operations"]![0]![property] = value;
        Assert.Throws<InvalidDataException>(() => Verify(result));
    }

    [Fact]
    public void Missing_or_duplicate_completion_check_is_not_a_success()
    {
        var result = Result(); result["checks"] = new JArray();
        Assert.Throws<InvalidDataException>(() => Verify(result));
        result["checks"] = new JArray("security", "security");
        Assert.Throws<InvalidDataException>(() => Verify(result));
        result["checks"] = new JArray("security");
        Assert.Throws<InvalidDataException>(() => Verify(result));
    }

    [Fact]
    public void Unreported_posted_source_and_extra_result_fields_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() => WebHostIisExpressTests.VerifyRecoveryResult(Result(), new JArray(Operation()), Array.Empty<long>(), new long[] { 1 }, new[] { Epoch }));
        var result = Result(); result["credentials"] = "not-permitted";
        Assert.Throws<InvalidDataException>(() => Verify(result));
    }

    [Theory]
    [InlineData("rotateEpoch")]
    [InlineData("setWritePermission")]
    public void Control_allows_only_fixed_owned_operations(string action)
    {
        var frame = new JObject { ["id"] = 1, ["action"] = action, ["token"] = "token" };
        if (action == "setWritePermission") frame["granted"] = false;
        Assert.Equal(action, WebRecoveryPilotControl.Validate(frame, "token"));
    }

    [Theory]
    [InlineData("sql", "DELETE dbo.Users")]
    [InlineData("actor", "web.smoke")]
    [InlineData("token", "wrong")]
    [InlineData("action", "dropDatabase")]
    public void Control_refuses_arbitrary_commands_actors_or_wrong_token(string property, string value)
    {
        var frame = new JObject { ["id"] = 1, ["action"] = "rotateEpoch", ["token"] = "token", [property] = value };
        Assert.Throws<InvalidDataException>(() => WebRecoveryPilotControl.Validate(frame, "token"));
    }
}
