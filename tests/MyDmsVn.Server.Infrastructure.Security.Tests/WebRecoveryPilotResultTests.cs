using System;
using System.IO;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
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

    private static readonly Web6CandidateExpected Web6Expected = new Web6CandidateExpected(
        "web6-candidate-plan", new string('a', 40), new string('b', 40), new string('c', 64));

    private static JObject Web6Result()
    {
        var kinds = new[] { "Sales.Create", "Sales.Update", "Sales.Post", "Receipt.Create", "Receipt.Update", "Receipt.Post" };
        return new JObject
        {
            ["version"] = 2, ["mode"] = "web6-candidate", ["candidateId"] = Web6Expected.CandidateId,
            ["frontendSourceSha"] = Web6Expected.FrontendSourceSha, ["backendSourceSha"] = Web6Expected.BackendSourceSha,
            ["frontendDistManifestSha256"] = Web6Expected.FrontendDistManifestSha256,
            ["operations"] = new JArray(kinds.Select((kind, index) => new JObject
            {
                ["operationKind"] = kind, ["operationId"] = $"0000000{index + 1}-0000-4000-8000-00000000000{index + 1}",
                ["requestFingerprint"] = new string((char)('a' + index), 64), ["consentDispatches"] = 1,
                ["candidateId"] = Web6Expected.CandidateId, ["checkId"] = "web6-" + kind.Replace('.', '-').ToLowerInvariant(),
            })),
            ["checks"] = new JArray("web6-six-kinds-one-consent", "web6-committed-loss-no-resend",
                "web6-two-tab-one-claim", "web6-fail-closed-boundaries", "web6-ime-double-click"),
        };
    }

    [Fact]
    public void Web6_live_acceptance_gate_fails_enabled_runs_with_missing_prerequisites_without_affecting_ordinary_discovery()
    {
        var disabled = new Dictionary<string, string?> { ["MYDMSVN_WEB6_CANDIDATE_ENABLE"] = null };
        Assert.NotNull(Web6CandidateAcceptanceGate.OrdinarySkipReason(disabled));

        var enabled = new Dictionary<string, string?> { ["MYDMSVN_WEB6_CANDIDATE_ENABLE"] = "1" };
        Assert.Null(Web6CandidateAcceptanceGate.OrdinarySkipReason(enabled));
        var failure = Assert.Throws<InvalidOperationException>(() => Web6CandidateAcceptanceGate.RequireLive(enabled));
        Assert.Contains("MYDMSVN_WEB6_CANDIDATE_ROOT", failure.Message);
    }

    [Fact]
    public async Task Candidate_mismatch_lifecycle_switches_the_owned_host_candidate_and_restores_the_exact_pair()
    {
        var switched = new List<string>();
        var lifecycle = new Web6CandidateHostMismatch("candidate-a", id => { switched.Add(id); return Task.CompletedTask; });
        await lifecycle.ArmAsync();
        Assert.Equal(new[] { lifecycle.MismatchCandidateId, "candidate-a" }, new[] { switched[0], lifecycle.ExpectedCandidateId });
        await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.ArmAsync());
        await lifecycle.RestoreAsync();
        Assert.Equal(new[] { lifecycle.MismatchCandidateId, "candidate-a" }, switched);
        await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.RestoreAsync());

        await lifecycle.ArmAsync();
        await lifecycle.RestoreIfNeededAsync();
        Assert.Equal(new[] { lifecycle.MismatchCandidateId, "candidate-a", lifecycle.MismatchCandidateId, "candidate-a" }, switched);
    }

    [Fact]
    public async Task Candidate_mismatch_lifecycle_restores_after_an_ambiguous_switch_failure()
    {
        var switched = new List<string>();
        var lifecycle = new Web6CandidateHostMismatch("candidate-a", id =>
        {
            switched.Add(id);
            if (id.EndsWith("-mismatch", StringComparison.Ordinal)) throw new IOException("ambiguous restart");
            return Task.CompletedTask;
        });
        await Assert.ThrowsAsync<IOException>(() => lifecycle.ArmAsync());
        Assert.True(lifecycle.IsArmed);
        await lifecycle.RestoreIfNeededAsync();
        Assert.Equal(new[] { lifecycle.MismatchCandidateId, "candidate-a" }, switched);
    }

    [Fact]
    public void Candidate_mismatch_identity_remains_a_distinct_valid_bounded_candidate()
    {
        var expected = "a" + new string('b', 63);
        var lifecycle = new Web6CandidateHostMismatch(expected, _ => Task.CompletedTask);
        Assert.NotEqual(expected, lifecycle.MismatchCandidateId);
        Assert.Matches("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", lifecycle.MismatchCandidateId);
    }

    [Fact]
    public void Candidate_mismatch_identity_never_collides_with_a_maximum_length_suffix_candidate()
    {
        var expected = new string('a', 55) + "-mismatch";
        Assert.Equal(64, expected.Length);
        var lifecycle = new Web6CandidateHostMismatch(expected, _ => Task.CompletedTask);
        Assert.NotEqual(expected, lifecycle.MismatchCandidateId);
        Assert.Matches("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", lifecycle.MismatchCandidateId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Candidate_oracle_rejects_noncanonical_sales_result_bytes_and_changed_draft_projection(int operationKind)
    {
        var envelope = JObject.Parse("{\"data\":{\"saleId\":\"1\",\"saleNumber\":\"SA0000000001\",\"saleDate\":\"2026-11-10\",\"customerId\":1,\"warehouseId\":2,\"employeeId\":3,\"status\":\"Draft\",\"version\":\"AAAAAAAAAAA=\",\"lines\":[{\"saleLineId\":\"4\",\"lineNo\":1,\"productId\":5,\"qty\":\"2.0000\",\"unitPrice\":\"3.0000\",\"isGift\":false,\"lineAmount\":\"6.0000\",\"productIsActive\":true,\"unit\":\"EA\",\"productDisplayName\":\"P\"}],\"totalAmount\":\"6.0000\",\"customerIsActive\":true,\"warehouseIsActive\":true,\"employeeIsActive\":true,\"customerDisplayName\":\"C\",\"warehouseDisplayName\":\"W\",\"employeeDisplayName\":\"E\"}}" );
        var bytes = Encoding.UTF8.GetBytes(envelope.ToString(Newtonsoft.Json.Formatting.None));
        Web6CandidateOracle.ValidateCanonicalResultBytes(operationKind, bytes);
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateCanonicalResultBytes(operationKind, Encoding.UTF8.GetBytes(" " + Encoding.UTF8.GetString(bytes))));

        var header = JObject.Parse("{\"saleId\":\"1\",\"saleNumber\":\"SA0000000001\",\"saleDate\":\"2026-11-10\",\"customerId\":1,\"warehouseId\":2,\"employeeId\":3,\"status\":\"Draft\",\"note\":null,\"version\":\"AAAAAAAAAAA=\",\"postedAtUtc\":null,\"postedByUserId\":null}");
        var lines = JArray.Parse("[{\"saleLineId\":\"4\",\"lineNo\":1,\"productId\":5,\"qty\":\"2.0000\",\"unitPrice\":\"3.0000\",\"isGift\":false,\"lineAmount\":\"6.0000\"}]");
        Web6CandidateOracle.ValidateDraftProjection(operationKind, envelope, header, lines);
        header["warehouseId"] = 9;
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateDraftProjection(operationKind, envelope, header, lines));
        header["warehouseId"] = 2; header["postedAtUtc"] = "2026-11-10T00:00:00Z"; header["postedByUserId"] = 7;
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateDraftProjection(operationKind, envelope, header, lines));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Candidate_oracle_compares_exact_receipt_header_lines_and_cardinality(int operationKind)
    {
        var envelope = JObject.Parse("{\"data\":{\"receiptId\":\"1\",\"receiptNumber\":\"GR0000000001\",\"receiptDate\":\"2026-11-10\",\"warehouseId\":2,\"warehouseDisplayName\":null,\"warehouseIsActive\":true,\"employeeId\":3,\"employeeDisplayName\":null,\"employeeIsActive\":true,\"status\":\"Draft\",\"note\":null,\"postedAtUtc\":null,\"version\":\"AAAAAAAAAAA=\",\"lines\":[{\"receiptLineId\":\"4\",\"lineNumber\":1,\"productId\":5,\"quantity\":\"2.0000\",\"unitCost\":\"3.0000\",\"productDisplayName\":null,\"productIsActive\":true}]}}" );
        var bytes = Encoding.UTF8.GetBytes(envelope.ToString(Newtonsoft.Json.Formatting.None));
        Web6CandidateOracle.ValidateCanonicalResultBytes(operationKind, bytes);
        var header = JObject.Parse("{\"receiptId\":\"1\",\"receiptNumber\":\"GR0000000001\",\"receiptDate\":\"2026-11-10\",\"warehouseId\":2,\"employeeId\":3,\"status\":\"Draft\",\"note\":null,\"version\":\"AAAAAAAAAAA=\",\"postedAtUtc\":null,\"postedByUserId\":null}");
        var lines = JArray.Parse("[{\"receiptLineId\":\"4\",\"lineNumber\":1,\"productId\":5,\"quantity\":\"2.0000\",\"unitCost\":\"3.0000\"}]");
        Web6CandidateOracle.ValidateDraftProjection(operationKind, envelope, header, lines);
        lines.Add(lines[0]!.DeepClone());
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateDraftProjection(operationKind, envelope, header, lines));
    }

    [Theory]
    [InlineData("1000.0000", "15000.0000", "-2.0000", "-30.0000", "998.0000", "14970.0000")]
    [InlineData("0.0000", "0.0000", "2.0000", "6.0000", "2.0000", "6.0000")]
    public void Candidate_oracle_requires_the_exact_stock_balance_after_one_post(string initialQuantity, string initialValue,
        string quantityChange, string valueChange, string actualQuantity, string actualValue)
    {
        Web6CandidateOracle.ValidateStockBalance(initialQuantity, initialValue, quantityChange, valueChange, actualQuantity, actualValue);
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateStockBalance(initialQuantity, initialValue,
            quantityChange, valueChange, (decimal.Parse(actualQuantity, System.Globalization.CultureInfo.InvariantCulture) + 1).ToString("F4", System.Globalization.CultureInfo.InvariantCulture), actualValue));
    }

    [Theory]
    [InlineData(3, "{\"data\":{\"saleId\":\"1\",\"status\":\"Posted\",\"postedAtUtc\":\"2026-11-10T00:00:00Z\",\"version\":\"AAAAAAAAAAA=\"}}")]
    [InlineData(6, "{\"data\":{\"receiptId\":\"1\",\"status\":\"Posted\",\"postedAtUtc\":\"2026-11-10T00:00:00Z\"}}")]
    public void Candidate_oracle_decodes_canonical_post_result_bytes_for_both_modules(int operationKind, string json) =>
        Web6CandidateOracle.ValidateCanonicalResultBytes(operationKind, Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Web6_candidate_result_requires_exact_six_unique_kinds_and_one_consent()
    {
        Web6CandidateOracle.ValidateResult(Web6Result(), Web6Expected);
        var missing = Web6Result(); ((JArray)missing["operations"]!).RemoveAt(0);
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateResult(missing, Web6Expected));
        var duplicate = Web6Result(); duplicate["operations"]![5]!["operationKind"] = "Sales.Create";
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateResult(duplicate, Web6Expected));
        var dispatch = Web6Result(); dispatch["operations"]![0]!["consentDispatches"] = 2;
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateResult(dispatch, Web6Expected));
    }

    [Theory]
    [InlineData("candidateId", "other-candidate")]
    [InlineData("frontendSourceSha", "dddddddddddddddddddddddddddddddddddddddd")]
    [InlineData("backendSourceSha", "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee")]
    [InlineData("frontendDistManifestSha256", "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")]
    public void Web6_candidate_result_refuses_wrong_pins(string property, string value)
    {
        var result = Web6Result(); result[property] = value;
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateResult(result, Web6Expected));
    }

    [Theory]
    [InlineData("token")]
    [InlineData("cookie")]
    [InlineData("connectionString")]
    [InlineData("receipt")]
    [InlineData("payload")]
    public void Web6_candidate_result_refuses_extra_secret_or_raw_fields(string property)
    {
        var result = Web6Result(); result[property] = "secret";
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateResult(result, Web6Expected));
    }

    [Fact]
    public void Web6_browser_count_requires_three_real_passes_without_skip_retry_or_extra_fields()
    {
        var count = JObject.Parse("{\"version\":2,\"discovered\":3,\"passed\":3,\"failed\":0,\"skipped\":0,\"interrupted\":0,\"retries\":0}");
        Web6CandidateOracle.ValidateBrowserCount(count);
        foreach (var name in new[] { "failed", "skipped", "interrupted", "retries" })
        {
            var changed = (JObject)count.DeepClone(); changed[name] = 1;
            Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateBrowserCount(changed));
        }
        var secret = (JObject)count.DeepClone(); secret["token"] = "secret";
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateBrowserCount(secret));
    }

    [Fact]
    public void Web6_network_summary_is_strict_and_contains_no_raw_request_material()
    {
        var summary = JArray.Parse("[{\"method\":\"GET\",\"routeClass\":\"resend-eligibility\",\"status\":200,\"count\":1},{\"method\":\"POST\",\"routeClass\":\"sales-write\",\"status\":0,\"count\":1}]");
        Web6CandidateOracle.ValidateNetwork(summary);
        var secret = (JArray)summary.DeepClone(); secret[0]!["headers"] = "Bearer secret";
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateNetwork(secret));
        var raw = (JArray)summary.DeepClone(); raw[0]!["routeClass"] = "/api/v3/operations/secret-id/resend-eligibility";
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateNetwork(raw));
    }

    [Fact]
    public void Web6_candidate_disposition_requires_healthy_audit_and_unambiguous_cleanup()
    {
        Web6CandidateOracle.ValidateDisposition(auditHealthy: true, browserDrained: true, hostDrained: true, cleanupVerified: true);
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateDisposition(false, true, true, true));
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateDisposition(true, false, true, true));
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateDisposition(true, true, false, true));
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateDisposition(true, true, true, false));
    }

    [Theory]
    [InlineData("verifyNotObserved", true)]
    [InlineData("verifyCommitted", true)]
    [InlineData("verifyOneEffect", true)]
    [InlineData("candidateMismatchOn", true)]
    [InlineData("candidateMismatchOff", true)]
    [InlineData("eligibility401", true)]
    [InlineData("eligibility500", true)]
    [InlineData("eligibilityNormal", true)]
    public void Web6_control_contract_accepts_only_fixed_candidate_actions(string action, bool requiresOperation)
    {
        var frame = new JObject { ["id"] = 1, ["token"] = "token", ["action"] = action };
        if (requiresOperation)
        {
            frame["operation"] = new JObject { ["schemaVersion"] = 1 };
            frame["operationId"] = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
        }
        Assert.Equal(action, WebRecoveryPilotControl.Validate(frame, "token", web6: true));
        frame["extra"] = true;
        Assert.Throws<InvalidDataException>(() => WebRecoveryPilotControl.Validate(frame, "token", web6: true));
    }

    [Fact]
    public void Committed_draft_create_matches_original_null_target_and_physical_journal() => Verify(Result());

    [Fact]
    public void Web5_requires_its_mode_and_both_completion_markers()
    {
        var result = Result();
        void VerifyWeb5() => WebHostIisExpressTests.VerifyRecoveryResult(result, new JArray(Operation()), Array.Empty<long>(), Array.Empty<long>(), new[] { Epoch }, true);
        Assert.Throws<InvalidDataException>(VerifyWeb5);
        result["mode"] = "web5-recovery";
        Assert.Throws<InvalidDataException>(VerifyWeb5);
        ((JArray)result["checks"]!).Add("web5-v2-committed-sales");
        Assert.Throws<InvalidDataException>(VerifyWeb5);
        ((JArray)result["checks"]!).Add("web5-v2-committed-receipt");
        Assert.Throws<InvalidDataException>(VerifyWeb5);
        foreach (var name in new[] { "web5-negative-late-sales-create", "web5-negative-late-sales-update", "web5-negative-late-sales-post", "web5-negative-late-receipt-create", "web5-negative-late-receipt-update", "web5-negative-late-receipt-post" }) ((JArray)result["checks"]!).Add(name);
        VerifyWeb5();
        Assert.Throws<InvalidDataException>(() => Verify(result));
    }

    [Fact]
    public void Original_first_requires_all_six_distinct_markers_only_in_opted_in_run()
    {
        var result = Result(); result["mode"] = "web5-recovery";
        var checks = (JArray)result["checks"]!;
        foreach (var name in new[] { "web5-v2-committed-sales", "web5-v2-committed-receipt", "web5-negative-late-sales-create", "web5-negative-late-sales-update", "web5-negative-late-sales-post", "web5-negative-late-receipt-create", "web5-negative-late-receipt-update", "web5-negative-late-receipt-post" }) checks.Add(name);
        void VerifyOriginal() => WebHostIisExpressTests.VerifyRecoveryResult(result, new JArray(Operation()), Array.Empty<long>(), Array.Empty<long>(), new[] { Epoch }, true, true);
        foreach (var name in new[] { "web5-original-first-sales-create", "web5-original-first-sales-update", "web5-original-first-sales-post", "web5-original-first-receipt-create", "web5-original-first-receipt-update", "web5-original-first-receipt-post" })
        { Assert.Throws<InvalidDataException>(VerifyOriginal); checks.Add(name); }
        VerifyOriginal();
        Assert.Throws<InvalidDataException>(() => WebHostIisExpressTests.VerifyRecoveryResult(result, new JArray(Operation()), Array.Empty<long>(), Array.Empty<long>(), new[] { Epoch }, true));
    }

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

    [Fact]
    public void Web5_browser_count_requires_exact_nonzero_success_and_no_extra_fields()
    {
        var summary = JObject.Parse("{\"version\":1,\"discovered\":14,\"passed\":14,\"failed\":0,\"skipped\":0,\"interrupted\":0,\"retries\":0}");
        WebHostIisExpressTests.ValidateWeb5BrowserCount(summary);
        summary["skipped"] = 1;
        Assert.Throws<InvalidDataException>(() => WebHostIisExpressTests.ValidateWeb5BrowserCount(summary));
        summary["skipped"] = 0; summary["secret"] = "x";
        Assert.Throws<InvalidDataException>(() => WebHostIisExpressTests.ValidateWeb5BrowserCount(summary));
    }

    [Fact]
    public void Recovery_dispositions_allow_only_the_expected_web5_negative()
    {
        WebHostIisExpressTests.ValidateRecoveryDispositions(new[] { "COMMITTED", "CANCELLED", "CANCELLED", "CANCELLED", "CANCELLED", "CANCELLED", "CANCELLED" }, true);
        WebHostIisExpressTests.ValidateRecoveryDispositions(new[] { "COMMITTED" }, false);
        Assert.Throws<InvalidDataException>(() => WebHostIisExpressTests.ValidateRecoveryDispositions(new[] { "COMMITTED", "CANCELLED" }, false));
        Assert.Throws<InvalidDataException>(() => WebHostIisExpressTests.ValidateRecoveryDispositions(new[] { "COMMITTED", "EXPIRED" }, true));
        Assert.Throws<InvalidDataException>(() => WebHostIisExpressTests.ValidateRecoveryDispositions(new[] { "COMMITTED" }, true));
    }

    [Fact]
    public void Negative_control_rejects_missing_operation_and_wrong_actor()
    {
        var frame = new JObject { ["id"] = 1, ["token"] = "token", ["action"] = "resolveNegative" };
        Assert.Throws<InvalidDataException>(() => WebRecoveryPilotControl.Validate(frame, "token"));
        frame["operation"] = new JObject { ["actorKey"] = "other" };
        Assert.Throws<InvalidDataException>(() => Web5NegativePilot.Validate((JObject)frame["operation"]!, 1, Epoch));
    }

    [Fact]
    public void Negative_verification_requires_one_exact_canonical_operation_id()
    {
        var frame = new JObject { ["id"] = 1, ["token"] = "token", ["action"] = "verifyNegative" };
        Assert.Throws<InvalidDataException>(() => WebRecoveryPilotControl.Validate(frame, "token"));
        var key = Guid.NewGuid().ToString("D"); frame["operationId"] = key;
        Assert.Equal("verifyNegative", WebRecoveryPilotControl.Validate(frame, "token"));
        frame["operationId"] = key.ToUpperInvariant();
        Assert.Throws<InvalidDataException>(() => WebRecoveryPilotControl.Validate(frame, "token"));
        frame["operationId"] = key; frame["extra"] = 1;
        Assert.Throws<InvalidDataException>(() => WebRecoveryPilotControl.Validate(frame, "token"));
    }

    [Fact]
    public void Negative_identity_recomputes_fingerprint_and_fences_actor_epoch_and_route()
    {
        var body = new JObject { ["note"] = "fixture" };
        var intent = new WebOperationHttpSqlTests.Intent("Sales.Create", body, null, Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"));
        var frozen = new JObject { ["schemaVersion"] = 1, ["actorKey"] = "1", ["deploymentEpoch"] = Epoch.ToString("D"),
            ["operationId"] = intent.Key.ToString("D"), ["requestFingerprint"] = intent.Fingerprint,
            ["request"] = new JObject { ["operationKind"] = "Sales.Create", ["method"] = "POST", ["routeScope"] = "/api/v1/sales-receipts", ["requestTargetId"] = null, ["body"] = body } };
        Web5NegativePilot.Validate(frozen, 1, Epoch);
        Assert.Throws<InvalidDataException>(() => Web5NegativePilot.Validate(frozen, 2, Epoch));
        Assert.Throws<InvalidDataException>(() => Web5NegativePilot.Validate(frozen, 1, Guid.NewGuid()));
        frozen["request"]!["body"]!["note"] = "changed";
        Assert.Throws<InvalidDataException>(() => Web5NegativePilot.Validate(frozen, 1, Epoch));
        frozen["request"]!["routeScope"] = "/api/v1/goods-receipts";
        Assert.Throws<InvalidDataException>(() => Web5NegativePilot.Validate(frozen, 1, Epoch));
    }

    [Theory]
    [InlineData("sales-create")][InlineData("sales-update")][InlineData("sales-post")]
    [InlineData("receipt-create")][InlineData("receipt-update")][InlineData("receipt-post")]
    public void Negative_control_accepts_only_exact_corpus_six_command_identities(string stem)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "docs", "contracts", "web4-v1", stem + "-intent.json"))) root = root.Parent;
        Assert.NotNull(root); var corpus = Path.Combine(root!.FullName, "docs", "contracts", "web4-v1");
        var request = JObject.Parse(File.ReadAllText(Path.Combine(corpus, stem + "-intent.json")));
        var status = JObject.Parse(File.ReadAllText(Path.Combine(corpus, stem + "-status.json")));
        var frozen = new JObject { ["schemaVersion"] = 1, ["actorKey"] = "1", ["deploymentEpoch"] = Epoch.ToString("D"), ["operationId"] = Guid.NewGuid().ToString("D"),
            ["requestFingerprint"] = status["data"]!["requestFingerprint"]!.DeepClone(), ["request"] = request };
        var intent = Web5NegativePilot.Validate(frozen, 1, Epoch);
        Assert.Equal((string?)request["operationKind"], intent.Kind);
        frozen["request"]!["method"] = "DELETE";
        Assert.Throws<InvalidDataException>(() => Web5NegativePilot.Validate(frozen, 1, Epoch));
    }

    [Fact]
    public async System.Threading.Tasks.Task Attended_lease_times_out_and_deletes_private_credentials()
    {
        var parent = Path.Combine(Path.GetTempPath(), "mydmsvn-attended-test-" + Guid.NewGuid().ToString("N"));
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var acl = new System.Security.AccessControl.DirectorySecurity();
        acl.SetAccessRuleProtection(true, false); acl.SetOwner(identity.User!);
        acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(identity.User!, System.Security.AccessControl.FileSystemRights.FullControl,
            System.Security.AccessControl.InheritanceFlags.ContainerInherit | System.Security.AccessControl.InheritanceFlags.ObjectInherit,
            System.Security.AccessControl.PropagationFlags.None, System.Security.AccessControl.AccessControlType.Allow));
        Directory.CreateDirectory(parent, acl);
        try
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            await Assert.ThrowsAsync<TimeoutException>(() => Web5AttendedLease.Wait(process, parent, "ephemeral", TimeSpan.FromMilliseconds(1), System.Threading.CancellationToken.None));
            Assert.Empty(Directory.GetFileSystemEntries(parent));
        }
        finally { Directory.Delete(parent); }
    }

    [Fact]
    public async System.Threading.Tasks.Task Attended_lease_refuses_insecure_parent_and_invalid_budget()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Web5AttendedLease.Wait(process, Path.GetTempPath(), "ephemeral", TimeSpan.FromMinutes(1), System.Threading.CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Web5AttendedLease.Wait(process, Path.GetTempPath(), "ephemeral", TimeSpan.FromMinutes(31), System.Threading.CancellationToken.None));
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
