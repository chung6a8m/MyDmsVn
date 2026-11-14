using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Infrastructure.Operations;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CandidateCutoverTests
{
    [Theory]
    [InlineData((int)Web6CutoverPair.Current, false)]
    [InlineData((int)Web6CutoverPair.Current, true)]
    [InlineData((int)Web6CutoverPair.Old, false)]
    [InlineData((int)Web6CutoverPair.Restored, false)]
    public async Task Stable_readmission_observes_exact_pair_queue_and_health_then_rechecks_identity(int pairValue, bool cached)
    {
        var pair = (Web6CutoverPair)pairValue;
        var plan = Plan();
        var admission = Admission(plan, pair);
        var calls = new List<string>();
        await WebHostIisExpressTests.VerifyCutoverStableEvidence(plan, pair, cached, admission.Epoch, pair, cached, admission,
            () => { calls.Add("identity-queue"); return Task.CompletedTask; },
            () => { calls.Add("pinned-health"); return Task.CompletedTask; },
            new Web6CutoverDeadline(TimeSpan.FromSeconds(1)), default);
        Assert.Equal(new[] { "identity-queue", "pinned-health", "identity-queue" }, calls);
    }

    [Theory]
    [InlineData("pair")]
    [InlineData("cached")]
    [InlineData("epoch")]
    [InlineData("schema")]
    [InlineData("receipt")]
    [InlineData("artifact")]
    [InlineData("queue")]
    [InlineData("health")]
    public async Task Stable_readmission_refuses_mismatch_or_failed_observation(string fault)
    {
        var plan = Plan();
        var admission = Admission(plan, Web6CutoverPair.Current);
        if (fault == "schema") admission = admission.WithSchema(12);
        if (fault == "receipt") admission = admission.WithReceipt(new string('0', 64));
        if (fault == "artifact") admission = admission.WithArtifact(new string('0', 64));
        var calls = new List<string>();
        await Assert.ThrowsAsync<IOException>(() => WebHostIisExpressTests.VerifyCutoverStableEvidence(plan,
            Web6CutoverPair.Current, false, fault == "epoch" ? Guid.Empty : plan.CurrentEpoch,
            fault == "pair" ? Web6CutoverPair.Old : Web6CutoverPair.Current, fault == "cached", admission,
            () => { calls.Add("queue"); if (fault == "queue") throw new IOException(); return Task.CompletedTask; },
            () => { calls.Add("health"); if (fault == "health") throw new IOException(); return Task.CompletedTask; },
            new Web6CutoverDeadline(TimeSpan.FromSeconds(1)), default));
        if (fault != "health") Assert.DoesNotContain("health", calls);
    }

    [Fact]
    public async Task Stable_health_returning_after_deadline_does_not_acknowledge_success()
    {
        var plan = Plan();
        var now = TimeSpan.Zero;
        var calls = 0;
        await Assert.ThrowsAsync<TimeoutException>(() => WebHostIisExpressTests.VerifyCutoverStableEvidence(plan,
            Web6CutoverPair.Current, false, plan.CurrentEpoch, Web6CutoverPair.Current, false, Admission(plan, Web6CutoverPair.Current),
            () => { calls++; return Task.CompletedTask; },
            () => { now = TimeSpan.FromSeconds(2); return Task.CompletedTask; },
            new Web6CutoverDeadline(TimeSpan.FromSeconds(1), () => now), default));
        Assert.Equal(1, calls);
    }

    private const string FrontendArchive = "ea104582a5aa4bcdf0d04999296a30a3c43e450e";
    private const string BackendArchive = "a3f3db16381a38bdf0b76e8ab36cb55bec02bbd2";
    private static readonly string FrontendTree = new string('a', 40);
    private static readonly string BackendTree = new string('b', 40);
    private static readonly string FrontendArtifact = new string('c', 64);
    private static readonly string BackendArtifact = new string('d', 64);
    private static readonly string CurrentReceipt = new string('e', 64);
    private static readonly string OldReceipt = new string('f', 64);
    private static readonly Guid CurrentEpoch = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly Guid RestoredEpoch = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");

    [Fact]
    public void Committed_control_requires_exact_frontend_proof_and_matching_identity()
    {
        var id = Guid.NewGuid();
        var frame = ControlFrame("committedUnavailable");
        frame["operationId"] = id.ToString("D");
        frame.Remove("committedSetup");
        Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(frame, "secret"));
        frame["committedSetup"] = Proof(id);
        var request = Web6CutoverControl.Validate(frame, "secret");
        Assert.Equal("committedUnavailable", request.Action);
        Assert.Equal(id, request.Operation);
        Assert.True(JToken.DeepEquals(Proof(id), request.CommittedSetup));
        foreach (var field in new[] { "operationId", "operationKind", "requestFingerprint", "authorizedBusinessEffects", "checkId", "extra" })
        {
            var bad = (JObject)frame.DeepClone();
            bad["committedSetup"]![field] = field == "authorizedBusinessEffects" ? (JToken)2 : "wrong";
            Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(bad, "secret"));
        }
        foreach (var field in ((JObject)frame["committedSetup"]!).Properties())
        {
            var bad = (JObject)frame.DeepClone(); ((JObject)bad["committedSetup"]!).Remove(field.Name);
            Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(bad, "secret"));
        }
        var oversized = (JObject)frame.DeepClone();
        oversized["committedSetup"]!["authorizedBusinessEffects"] = long.MaxValue;
        Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(oversized, "secret"));
        frame["action"] = "sameEpochRestart";
        Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(frame, "secret"));
    }

    [Fact]
    public void Validated_committed_proof_is_immutable_after_validation()
    {
        var id = Guid.NewGuid();
        var frame = ControlFrame("committedUnavailable");
        frame["operationId"] = id.ToString("D");
        frame["committedSetup"] = Proof(id);

        var request = Web6CutoverControl.Validate(frame, "secret");
        frame["committedSetup"]!["requestFingerprint"] = "changed";
        request.CommittedSetup!["authorizedBusinessEffects"] = 2;

        Assert.True(JToken.DeepEquals(Proof(id), request.CommittedSetup));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("\"5001\"")]
    [InlineData("5001.0")]
    [InlineData("5e3")]
    [InlineData("-5001")]
    [InlineData("5000")]
    [InlineData("180001")]
    [InlineData("9223372036854775808")]
    public void Cutover_control_refuses_missing_non_integer_or_out_of_range_allowance(string? allowanceJson)
    {
        var frame = ControlFrame("oldClientNewHostOn");
        if (allowanceJson == null) frame.Remove("requestAllowanceMs");
        else frame["requestAllowanceMs"] = JToken.Parse(allowanceJson);

        var failure = Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(frame, "secret"));
        Assert.Equal("Invalid WEB6 cutover control frame.", failure.Message);
    }

    [Theory]
    [InlineData("resetCutover", 5001)]
    [InlineData("resetCutover", 180000)]
    [InlineData("sameEpochRestart", 210000)]
    [InlineData("revokeGrantOn", 45000)]
    [InlineData("committedUnavailable", 60000)]
    public void Cutover_control_reserves_exactly_five_seconds_of_each_admitted_allowance(string action, int allowanceMs)
    {
        var request = Web6CutoverControl.Validate(ControlFrame(action, allowanceMs), "secret");

        Assert.Equal(TimeSpan.FromMilliseconds(allowanceMs), request.RequestAllowance);
        Assert.Equal(TimeSpan.FromMilliseconds(allowanceMs - 5000), request.WorkAllowance);
    }

    [Theory]
    [InlineData("revokeGrantOn", 45001)]
    [InlineData("revokeGrantOff", 45001)]
    [InlineData("verifyNoCutoverMutation", 45001)]
    [InlineData("committedUnavailable", 60001)]
    [InlineData("restoreEpoch", 180001)]
    [InlineData("sameEpochRestart", 210001)]
    public void Cutover_control_refuses_an_allowance_above_the_action_cap(string action, int allowanceMs)
    {
        Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(ControlFrame(action, allowanceMs), "secret"));
    }

    [Theory]
    [InlineData("operationId")]
    [InlineData("committedSetup")]
    public void Reset_cutover_refuses_operation_and_proof_fields(string field)
    {
        var frame = ControlFrame("resetCutover");
        frame[field] = JValue.CreateNull();

        Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(frame, "secret"));
    }

    [Theory]
    [InlineData("sameEpochRestart")]
    [InlineData("revokeGrantOn")]
    [InlineData("restoreEpoch")]
    [InlineData("committedUnavailable")]
    [InlineData("verifyNoCutoverMutation")]
    public void Cutover_operation_actions_require_the_operation_field(string action)
    {
        var frame = ControlFrame(action);
        frame.Remove("operationId");
        Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(frame, "secret"));

        frame["operationId"] = Guid.Empty.ToString("D");
        Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(frame, "secret"));
    }

    [Fact]
    public void Cutover_control_refuses_wrong_token_correlation_type_and_unknown_action()
    {
        var frame = ControlFrame("resetCutover");
        Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(frame, "wrong"));
        frame["id"] = "7";
        Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(frame, "secret"));
        Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(ControlFrame("unknown"), "secret"));
    }

    [Fact]
    public void Restored_handle_is_a_third_distinct_owned_allocation()
    {
        var handles = Plan().FrontendHandles();
        Assert.Matches("^[0-9a-f]{64}$", (string?)handles["restoredDatabaseOwnershipReceiptSha256"] ?? "");
        Assert.NotEqual(CurrentReceipt, (string?)handles["restoredDatabaseOwnershipReceiptSha256"]);
        Assert.NotEqual(OldReceipt, (string?)handles["restoredDatabaseOwnershipReceiptSha256"]);
    }

    [Theory]
    [InlineData("fingerprint")]
    [InlineData("kind")]
    [InlineData("absent")]
    [InlineData("disposition")]
    [InlineData("epoch")]
    public void Committed_SQL_binding_refuses_unproven_or_mismatched_journal(string drift)
    {
        var id = Guid.NewGuid(); var proof = Web6CommittedSetup.Parse(Proof(id), id);
        Assert.Throws<InvalidDataException>(() => proof.VerifyJournal(drift == "absent" ? Guid.Empty : id,
            drift == "kind" ? 4 : 1, drift == "fingerprint" ? new string('b',64) : new string('a',64),
            drift == "epoch" ? RestoredEpoch : CurrentEpoch, CurrentEpoch,
            drift == "disposition" ? "CANCELLED" : "COMMITTED", "integratedEditor", null, 1));
    }

    [Fact]
    public void Setup_delta_allows_only_one_named_create_and_subsequent_delta_is_zero()
    {
        var id = Guid.NewGuid(); var before = Snapshot(); var after = Snapshot();
        ((JArray)after["sales"]!).Add(new JObject { ["SaleId"] = 1 });
        ((JArray)after["saleLines"]!).Add(new JObject { ["SaleId"] = 1, ["SaleLineId"] = 1 });
        ((JArray)after["journal"]!).Add(new JObject { ["OperationId"] = id.ToString("D"), ["ResultDocumentId"] = 1 });
        after["sequences"]![0]!["value"] = 2; after["sequences"]![0]!["lastUsed"] = 2;
        Web6CutoverDelta.VerifySetup(before, after, id, 1);
        Web6CutoverDelta.VerifyZero(after, after);
        foreach (var key in new[] { "sales", "saleLines", "receipts", "balances", "ledger", "valuation", "journal", "terminal" })
        {
            var drift = (JObject)after.DeepClone(); ((JArray)drift[key]!).Add(new JObject { ["unexpected"] = true });
            Assert.Throws<InvalidDataException>(() => Web6CutoverDelta.VerifySetup(before, drift, id, 1));
            Assert.Throws<InvalidDataException>(() => Web6CutoverDelta.VerifyZero(after, drift));
        }
        Assert.Throws<InvalidDataException>(() => Web6CutoverDelta.VerifySetup(before, before, id, 1));
    }

    [Fact]
    public void First_setup_uses_start_value_and_records_first_NEXT_VALUE_consumption()
    {
        var id = Guid.NewGuid(); var before = Snapshot(); var after = Snapshot();
        before["sequences"]![0]!["lastUsed"] = JValue.CreateNull();
        after["sequences"]![0]!["lastUsed"] = 1;
        ((JArray)after["sales"]!).Add(new JObject { ["SaleId"] = 1 });
        ((JArray)after["saleLines"]!).Add(new JObject { ["SaleId"] = 1, ["SaleLineId"] = 1 });
        ((JArray)after["journal"]!).Add(new JObject { ["OperationId"] = id.ToString("D"), ["ResultDocumentId"] = 1 });
        Web6CutoverDelta.VerifySetup(before, after, id, 1);
        var overconsumed = (JObject)after.DeepClone(); overconsumed["sequences"]![0]!["value"] = 2;
        Assert.Throws<InvalidDataException>(() => Web6CutoverDelta.VerifySetup(before, overconsumed, id, 1));
    }

    private static JObject Snapshot() => new JObject
    {
        ["sales"] = new JArray(), ["saleLines"] = new JArray(), ["receipts"] = new JArray(), ["receiptLines"] = new JArray(),
        ["balances"] = new JArray(), ["ledger"] = new JArray(), ["valuation"] = new JArray(), ["journal"] = new JArray(), ["terminal"] = new JArray(),
        ["sequences"] = new JArray(new JObject { ["name"] = "SalesReceiptNumberSequence", ["value"] = 1, ["start"] = 1, ["lastUsed"] = 1 }),
    };

    [Fact]
    public void Restored_admission_requires_its_own_receipt_and_current_can_return_without_rewind()
    {
        var plan = Plan(); var coordinator = new Web6CandidateCutoverCoordinator(plan);
        coordinator.Admit(Web6CutoverPair.Current, Admission(plan, Web6CutoverPair.Current));
        Assert.Throws<InvalidOperationException>(() => coordinator.Admit(Web6CutoverPair.Restored, Admission(plan, Web6CutoverPair.Restored)));
        coordinator.Drain(Web6CutoverPair.Current, true, true);
        Assert.Throws<IOException>(() => coordinator.Admit(Web6CutoverPair.Restored, Admission(plan, Web6CutoverPair.Current)));
        coordinator.Admit(Web6CutoverPair.Restored, Admission(plan, Web6CutoverPair.Restored));
        Assert.Throws<IOException>(() => coordinator.Drain(Web6CutoverPair.Restored, true, false));
        Assert.Throws<InvalidOperationException>(() => coordinator.Admit(Web6CutoverPair.Current, Admission(plan, Web6CutoverPair.Current)));
        coordinator.Drain(Web6CutoverPair.Restored, true, true);
        Assert.Throws<IOException>(() => coordinator.Admit(Web6CutoverPair.Current, Admission(plan, Web6CutoverPair.Current).WithEpoch(RestoredEpoch)));
        coordinator.Admit(Web6CutoverPair.Current, Admission(plan, Web6CutoverPair.Current));
        Assert.Equal(CurrentEpoch, plan.CurrentEpoch);
    }

    [Fact]
    public async Task Third_allocation_cleanup_failure_remains_failure_and_other_owned_cleanups_are_attempted()
    {
        var calls = new List<string>();
        await Assert.ThrowsAsync<IOException>(() => Web6CutoverLifetime.Run(() => Task.CompletedTask, () => Task.FromResult(true), new Func<Task>[]
        {
            () => { calls.Add("restored"); throw new IOException("restored cleanup failed"); },
            () => { calls.Add("current"); return Task.CompletedTask; },
            () => { calls.Add("old"); return Task.CompletedTask; },
        }));
        Assert.Equal(new[] { "restored", "current", "old" }, calls);
    }

    [Fact]
    public void Disposition_requires_independent_setup_proof_restored_admissions_and_current_readmission()
    {
        var firstId = Guid.NewGuid(); var secondId = Guid.NewGuid();
        var first = Web6CommittedSetup.Parse(Proof(firstId), firstId); var second = Web6CommittedSetup.Parse(Proof(secondId), secondId);
        Web6CutoverOracle.ValidateSetupEvidence(first, first, true);
        Assert.Throws<InvalidDataException>(() => Web6CutoverOracle.ValidateSetupEvidence(first, null, true));
        Assert.Throws<InvalidDataException>(() => Web6CutoverOracle.ValidateSetupEvidence(first, second, true));
        Assert.Throws<InvalidDataException>(() => Web6CutoverOracle.ValidateSetupEvidence(first, first, false));
        Web6CutoverOracle.ValidateAllocationDisposition(Plan(), new[] { first, second }, 2, 1);
        Assert.Throws<InvalidDataException>(() => Web6CutoverOracle.ValidateAllocationDisposition(Plan(), new[] { first, first }, 2, 1));
        Assert.Throws<InvalidDataException>(() => Web6CutoverOracle.ValidateAllocationDisposition(Plan(), new[] { first, second }, 1, 1));
        Assert.Throws<InvalidDataException>(() => Web6CutoverOracle.ValidateAllocationDisposition(Plan(), new[] { first, second }, 2, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Result_corruption_always_reenables_and_verifies_trigger_after_update_failure(bool enableFails)
    {
        var calls = new List<string>();
        Func<Task> disable = () => { calls.Add("disable"); return Task.CompletedTask; };
        Func<Task> mutate = () => { calls.Add("mutate"); throw new IOException("mutation failed"); };
        Func<Task> enable = () => { calls.Add("enable"); if (enableFails) throw new IOException("enable failed"); return Task.CompletedTask; };
        Func<Task> verify = () => { calls.Add("verify"); return Task.CompletedTask; };
        if (enableFails) await Assert.ThrowsAsync<AggregateException>(() => Web6CutoverCorruption.Run(disable, mutate, enable, verify));
        else await Assert.ThrowsAsync<IOException>(() => Web6CutoverCorruption.Run(disable, mutate, enable, verify));
        Assert.Equal(new[] { "disable", "mutate", "enable", "verify" }, calls);
    }

    [Fact]
    public void Unavailable_fixture_changes_only_result_bytes_and_preserves_every_identity_time_and_effect()
    {
        var id = Guid.NewGuid(); var before = Snapshot();
        ((JArray)before["journal"]!).Add(new JObject { ["OperationId"] = id.ToString("D"), ["ResultBytes"] = "Y2Fub25pY2Fs", ["Disposition"] = "COMMITTED", ["CompletedAtUtc"] = "original", ["ResultExpiresAtUtc"] = "seven-days-later" });
        var after = (JObject)before.DeepClone(); after["journal"]![0]!["ResultBytes"] = "e30=";
        Web6CutoverDelta.VerifyUnavailable(before, after, id);
        after["journal"]![0]!["ResultExpiresAtUtc"] = "original";
        Assert.Throws<InvalidDataException>(() => Web6CutoverDelta.VerifyUnavailable(before, after, id));
    }

    [Fact]
    public void Restored_session_copy_requires_same_actor_and_exact_existing_metadata()
    {
        var now = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);
        var source = new JObject { ["SessionId"] = Guid.NewGuid().ToString("D"), ["UserId"] = 7,
            ["CreatedAtUtc"] = now.AddMinutes(-1), ["AbsoluteExpiresAtUtc"] = now.AddDays(1),
            ["IdleExpiresAtUtc"] = now.AddHours(1), ["RevokedAtUtc"] = JValue.CreateNull() };
        Web6CutoverSessionCopy.Verify(source, null, 7, now);
        Web6CutoverSessionCopy.Verify(source, (JObject)source.DeepClone(), 7, now);
        Assert.Throws<InvalidDataException>(() => Web6CutoverSessionCopy.Verify(source, null, 8, now));
        var changed = (JObject)source.DeepClone(); changed["IdleExpiresAtUtc"] = now.AddMinutes(10);
        Assert.Throws<InvalidDataException>(() => Web6CutoverSessionCopy.Verify(source, changed, 7, now));
        Assert.Throws<InvalidDataException>(() => Web6CutoverSessionCopy.Verify(source, null, 7, now.AddDays(2)));
    }

    [Fact]
    public async Task Edge_current_readmission_failure_drains_and_never_completes()
    {
        var operations = new RecordingCutoverOperations(Plan()) { FailEdgeStart = true };
        await Assert.ThrowsAsync<IOException>(() => new Web6CandidateCutoverOrchestrator(operations).Execute(CancellationToken.None));
        Assert.Contains("chromium:current:drain", operations.Calls);
        Assert.Contains("edge:current:drain", operations.Calls);
        Assert.DoesNotContain("complete", operations.Calls);
    }

    [Fact]
    public void Restored_authority_refuses_either_existing_allocation_receipt()
    {
        Assert.Throws<ArgumentException>(() => Plan(restoredReceipt: CurrentReceipt));
        Assert.Throws<ArgumentException>(() => Plan(restoredReceipt: OldReceipt));
        Assert.Throws<ArgumentException>(() => Plan(restoredReceipt: "invalid"));
    }

    [Fact]
    public async Task Restored_admission_failure_cannot_complete_lifecycle()
    {
        var runtime = new RecordingRuntime { FailRestored = true };
        var session = new Web6CutoverSession(Plan(), runtime); await session.Begin(new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), CancellationToken.None);
        var committed = Guid.NewGuid(); var unknown = Guid.NewGuid();
        await Handle(session, "committedUnavailable", committed, Proof(committed));
        await Handle(session, "sameEpochRestart", unknown);
        await Handle(session, "revokeGrantOn", unknown);
        await Handle(session, "revokeGrantOff", unknown);
        await Assert.ThrowsAsync<IOException>(() => Handle(session, "restoreEpoch", unknown));
        Assert.Throws<InvalidDataException>(session.VerifyComplete);
    }

    private static JObject Proof(Guid id) => new JObject
    {
        ["operationId"] = id.ToString("D"), ["operationKind"] = "Sales.Create", ["requestFingerprint"] = new string('a', 64),
        ["authorizedBusinessEffects"] = 1, ["checkId"] = "web6-cutover-authentic-committed-setup",
    };

    private static Task<JObject> Handle(Web6CutoverSession session, string action, Guid? operation, JObject? proof = null) =>
        session.Handle(new Web6CutoverRequest(action, operation, proof, TimeSpan.FromSeconds(10)), new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), CancellationToken.None);

    [Fact]
    public void Cutover_acceptance_gate_cannot_turn_missing_prerequisites_into_a_pass()
    {
        var ordinary = new System.Collections.Generic.Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        Assert.NotNull(Web6CutoverAcceptanceGate.OrdinarySkipReason(ordinary));
        ordinary["MYDMSVN_WEB6_CUTOVER_ENABLE"] = "1";
        Assert.Null(Web6CutoverAcceptanceGate.OrdinarySkipReason(ordinary));
        var failure = Assert.Throws<InvalidOperationException>(() => Web6CutoverAcceptanceGate.RequireLive(ordinary));
        Assert.Contains("BLOCKED", failure.Message);
        Assert.Contains("MYDMSVN_WEB6_CUTOVER_FRONTEND_ARCHIVE_DIRECTORY", failure.Message);
        Assert.Contains("MYDMSVN_WEB6_CUTOVER_SQL_CONTROL_PARENT", failure.Message);
    }

    [Fact]
    public async Task Owning_cutover_orchestrator_requires_protected_manifest_serial_pairs_and_both_browser_results()
    {
        var operations = new RecordingCutoverOperations(Plan());
        await new Web6CandidateCutoverOrchestrator(operations).Execute(CancellationToken.None);
        Assert.Equal(new[]
        {
            "authority", "manifest", "chromium:current:start", "chromium:current:validate", "chromium:current:drain", "chromium:restore",
            "edge:current:start", "edge:current:validate", "edge:current:drain",
            "edge:restore", "complete"
        }, operations.Calls);
        await Assert.ThrowsAsync<IOException>(() => new Web6CandidateCutoverOrchestrator(
            new RecordingCutoverOperations(Plan()) { SkipChromium = true }).Execute(CancellationToken.None));
        var badAdmission = new RecordingCutoverOperations(Plan()) { DriftAdmission = true };
        await Assert.ThrowsAsync<IOException>(() => new Web6CandidateCutoverOrchestrator(badAdmission).Execute(CancellationToken.None));
        Assert.Contains("chromium:current:drain", badAdmission.Calls);
    }

    [Fact]
    public void Cutover_result_is_validated_without_the_six_operation_candidate_oracle()
    {
        var plan = Plan();
        var count = JObject.Parse("{\"version\":2,\"discovered\":3,\"passed\":3,\"failed\":0,\"skipped\":0,\"interrupted\":0,\"retries\":0}");
        var result = CutoverResult(plan);

        Web6CutoverOracle.Validate(count, result, plan, CurrentExpected(), plan.FrontendHandles());
        Assert.Empty((JArray)result["operations"]!);

        var sixKind = new Web6CandidateExpected(plan.CurrentCandidateId, plan.FrontendSourceSha,
            plan.BackendSourceSha, plan.FrontendArtifactHash);
        Assert.Throws<InvalidDataException>(() => Web6CandidateOracle.ValidateResult(result, sixKind));
    }

    [Fact]
    public void Cutover_result_refuses_mutation_wrong_pin_or_incomplete_lifecycle_markers()
    {
        var plan = Plan();
        var count = JObject.Parse("{\"version\":2,\"discovered\":3,\"passed\":3,\"failed\":0,\"skipped\":0,\"interrupted\":0,\"retries\":0}");
        var operation = CutoverResult(plan); ((JArray)operation["operations"]!).Add(new JObject());
        Assert.Throws<InvalidDataException>(() => Web6CutoverOracle.Validate(count, operation, plan, CurrentExpected(), plan.FrontendHandles()));
        var pin = CutoverResult(plan); pin["backendSourceSha"] = new string('1', 40);
        Assert.Throws<InvalidDataException>(() => Web6CutoverOracle.Validate(count, pin, plan, CurrentExpected(), plan.FrontendHandles()));
        var marker = CutoverResult(plan); ((JArray)marker["checks"]!).RemoveAt(0);
        Assert.Throws<InvalidDataException>(() => Web6CutoverOracle.Validate(count, marker, plan, CurrentExpected(), plan.FrontendHandles()));
        var retry = (JObject)count.DeepClone(); retry["retries"] = 1;
        Assert.Throws<InvalidDataException>(() => Web6CutoverOracle.Validate(retry, CutoverResult(plan), plan, CurrentExpected(), plan.FrontendHandles()));
    }

    [Theory]
    [InlineData("oldClientNewHostOn")]
    [InlineData("oldClientNewHostOff")]
    [InlineData("newClientOldHostOn")]
    [InlineData("newClientOldHostOff")]
    [InlineData("sameEpochRestart")]
    [InlineData("revokeGrantOn")]
    [InlineData("revokeGrantOff")]
    [InlineData("restoreEpoch")]
    [InlineData("committedUnavailable")]
    [InlineData("verifyNoCutoverMutation")]
    [InlineData("resetCutover")]
    public void Cutover_control_maps_only_the_frontend_lifecycle_actions(string action)
    {
        var frame = ControlFrame(action);
        var request = Web6CutoverControl.Validate(frame, "secret");
        Assert.Equal(action, request.Action);
        Assert.Equal((string?)frame["operationId"], request.Operation?.ToString("D"));
        Assert.True(JToken.DeepEquals(frame["committedSetup"], request.CommittedSetup));
        frame["extra"] = true;
        Assert.Throws<InvalidDataException>(() => Web6CutoverControl.Validate(frame, "secret"));
    }

    private static JObject ControlFrame(string action, int allowanceMs = 5001)
    {
        var frame = new JObject { ["id"] = 7, ["token"] = "secret", ["action"] = action, ["requestAllowanceMs"] = allowanceMs };
        if (action is "sameEpochRestart" or "revokeGrantOn" or "revokeGrantOff" or "restoreEpoch" or "committedUnavailable" or "verifyNoCutoverMutation")
            frame["operationId"] = RestoredEpoch.ToString("D");
        if (action == "committedUnavailable") frame["committedSetup"] = Proof(RestoredEpoch);
        return frame;
    }

    [Fact]
    public void Independently_sealed_build_authority_rejects_arbitrary_published_artifact()
    {
        var currentPublish = new string('9', 64);
        var authority = new Web6CutoverBuildAuthority(FrontendArchive, FrontendTree, FrontendArtifact,
            BackendArchive, BackendTree, BackendArtifact, currentPublish, BackendArtifact);
        authority.Verify(new Web6CutoverBuildObservation(FrontendArchive, FrontendTree, true, FrontendArtifact,
            BackendArchive, BackendTree, true, BackendArtifact, currentPublish, BackendArtifact));
        Assert.Throws<IOException>(() => authority.Verify(new Web6CutoverBuildObservation(FrontendArchive, FrontendTree, true,
            new string('7', 64), BackendArchive, BackendTree, true, BackendArtifact, currentPublish, BackendArtifact)));
    }

    [Fact]
    public void SQL_receipt_requires_the_original_verified_protected_allocation_record()
    {
        using var control = SqlTestOwnershipManifest.Create(Path.GetTempPath());
        control.WriteVerifiedRecord("MyDmsVn_Test_11111111111141118111111111111111", "test-server", 17,
            new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc), "22222222222242228222222222222222", false,
            new DateTimeOffset(2026, 11, 10, 12, 0, 1, TimeSpan.Zero));
        var digest = control.VerifiedDigest();
        var receipt = Web6CutoverSqlOwnershipReceipt.FromProtectedRecord(control, digest,
            SqlSchemaDefinitionFingerprint.Schema13, "current-allocation");
        Assert.Matches("^[0-9a-f]{64}$", receipt.Digest);
        Assert.Throws<IOException>(() => Web6CutoverSqlOwnershipReceipt.FromProtectedRecord(control,
            new string('0', 64), SqlSchemaDefinitionFingerprint.Schema13, "current-allocation"));
    }

    [Fact]
    public void SQL_approval_and_post_migration_schema_are_fail_closed()
    {
        var now = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        var serverHash = new string('a', 64);
        var valid = new JObject
        {
            ["version"] = 1, ["purpose"] = "web6-cutover", ["serverSha256"] = serverHash,
            ["expiresUtc"] = now.AddHours(1).ToString("O"),
        };
        Web6CutoverSqlApproval.ParseAndVerify(valid.ToString(Newtonsoft.Json.Formatting.None), serverHash, now);
        valid["purpose"] = "other";
        Assert.Throws<IOException>(() => Web6CutoverSqlApproval.ParseAndVerify(valid.ToString(Newtonsoft.Json.Formatting.None), serverHash, now));
        valid["purpose"] = "web6-cutover"; valid["expiresUtc"] = now.AddSeconds(-1).ToString("O");
        Assert.Throws<IOException>(() => Web6CutoverSqlApproval.ParseAndVerify(valid.ToString(Newtonsoft.Json.Formatting.None), serverHash, now));
        Assert.Throws<IOException>(() => Web6CutoverSqlApproval.RequireSchema(SqlSchemaDefinitionFingerprint.Schema12,
            SqlSchemaDefinitionFingerprint.Schema13));
    }

    [Fact]
    public void SQL_approval_preserves_Z_suffix_expiry_instant()
    {
        const string serverHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string approval =
            "{\"version\":1,\"purpose\":\"web6-cutover\",\"serverSha256\":\"" + serverHash +
            "\",\"expiresUtc\":\"2026-11-14T08:55:19.1536282Z\"}";
        var observedAtUtc = new DateTimeOffset(2026, 11, 14, 8, 55, 19, 100, TimeSpan.Zero);

        Web6CutoverSqlApproval.ParseAndVerify(approval, serverHash, observedAtUtc);
    }

    [Fact]
    public void Cutover_plan_pins_archives_artifacts_and_separate_database_ownership()
    {
        var plan = Plan();
        plan.VerifyArchives(
            new Web6CutoverArchive(FrontendArchive, FrontendTree, clean: true, FrontendArtifact),
            new Web6CutoverArchive(BackendArchive, BackendTree, clean: true, BackendArtifact));

        var json = plan.FrontendHandles();
        Assert.Equal(1, (int)json["version"]!);
        Assert.Equal(FrontendArchive, (string)json["frontendWeb4SourceSha"]!);
        Assert.Equal(BackendArchive, (string)json["backendWeb4SourceSha"]!);
        Assert.Equal(12, (int)json["oldSchema"]!);
        Assert.Equal(13, (int)json["currentSchema"]!);
        Assert.Equal(CurrentReceipt, (string)json["currentDatabaseOwnershipReceiptSha256"]!);
        Assert.Equal(OldReceipt, (string)json["oldDatabaseOwnershipReceiptSha256"]!);
        Assert.NotEqual((string)json["currentOrigin"]!, (string)json["oldOrigin"]!);
    }

    [Theory]
    [InlineData("frontend-sha")]
    [InlineData("backend-sha")]
    [InlineData("frontend-tree")]
    [InlineData("backend-tree")]
    [InlineData("dirty-frontend")]
    [InlineData("dirty-backend")]
    [InlineData("frontend-artifact")]
    [InlineData("backend-artifact")]
    public void Cutover_plan_refuses_archive_identity_or_artifact_drift(string drift)
    {
        var frontend = new Web6CutoverArchive(
            drift == "frontend-sha" ? new string('1', 40) : FrontendArchive,
            drift == "frontend-tree" ? new string('2', 40) : FrontendTree,
            drift != "dirty-frontend",
            drift == "frontend-artifact" ? new string('3', 64) : FrontendArtifact);
        var backend = new Web6CutoverArchive(
            drift == "backend-sha" ? new string('4', 40) : BackendArchive,
            drift == "backend-tree" ? new string('5', 40) : BackendTree,
            drift != "dirty-backend",
            drift == "backend-artifact" ? new string('6', 64) : BackendArtifact);
        Assert.Throws<IOException>(() => Plan().VerifyArchives(frontend, backend));
    }

    [Theory]
    [InlineData(13, 13, false)]
    [InlineData(13, 12, true)]
    public void Cutover_plan_refuses_old_binary_on_schema13_or_reused_database(int currentSchema, int oldSchema, bool sameReceipt)
    {
        Assert.Throws<ArgumentException>(() => Plan(currentSchema, oldSchema,
            sameReceipt ? CurrentReceipt : OldReceipt));
    }

    [Fact]
    public void Cutover_coordinator_admits_one_listener_and_requires_verified_drain_before_switch()
    {
        var plan = Plan();
        var coordinator = new Web6CandidateCutoverCoordinator(plan);
        coordinator.Admit(Web6CutoverPair.Current, Admission(plan, Web6CutoverPair.Current));
        Assert.Throws<InvalidOperationException>(() => coordinator.Admit(Web6CutoverPair.Old, Admission(plan, Web6CutoverPair.Old)));
        Assert.Throws<IOException>(() => coordinator.Drain(Web6CutoverPair.Current, workerExited: false, queueAbsent: true));
        Assert.True(coordinator.HasActivePair);
        coordinator.Drain(Web6CutoverPair.Current, workerExited: true, queueAbsent: true);
        coordinator.Admit(Web6CutoverPair.Old, Admission(plan, Web6CutoverPair.Old));
        Assert.Equal(Web6CutoverPair.Old, coordinator.ActivePair);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    [InlineData(0, 1)]
    public void Cutover_coordinator_refuses_second_or_missing_controller_worker(int controllers, int workers)
    {
        var plan = Plan();
        var admission = Admission(plan, Web6CutoverPair.Current, controllers, workers);
        Assert.Throws<IOException>(() => new Web6CandidateCutoverCoordinator(plan).Admit(Web6CutoverPair.Current, admission));
    }

    [Fact]
    public void Code_restart_preserves_epoch_but_restore_rotates_and_rejects_stale_epoch()
    {
        var plan = Plan();
        var coordinator = new Web6CandidateCutoverCoordinator(plan);
        coordinator.Admit(Web6CutoverPair.Current, Admission(plan, Web6CutoverPair.Current));
        coordinator.RestartSameEpoch(CurrentEpoch);
        Assert.True(coordinator.IsEpochCurrent(CurrentEpoch));
        Assert.Throws<IOException>(() => coordinator.RestartSameEpoch(Guid.NewGuid()));

        coordinator.Drain(Web6CutoverPair.Current, true, true);
        coordinator.Admit(Web6CutoverPair.Restored, Admission(plan, Web6CutoverPair.Restored));
        coordinator.Restore(RestoredEpoch);
        Assert.False(coordinator.IsEpochCurrent(CurrentEpoch));
        Assert.True(coordinator.IsEpochCurrent(RestoredEpoch));
        Assert.Throws<IOException>(() => coordinator.Restore(CurrentEpoch));
    }

    [Fact]
    public void Cutover_admission_refuses_pair_artifact_schema_receipt_or_listener_drift()
    {
        var plan = Plan();
        foreach (var drift in new[] { "artifact", "schema", "receipt", "origin", "epoch" })
        {
            var admission = Admission(plan, Web6CutoverPair.Old);
            admission = drift switch
            {
                "artifact" => admission.WithArtifact(new string('7', 64)),
                "schema" => admission.WithSchema(13),
                "receipt" => admission.WithReceipt(CurrentReceipt),
                "origin" => admission.WithOrigin(new Uri("https://localhost:44323/")),
                _ => admission.WithEpoch(CurrentEpoch),
            };
            Assert.Throws<IOException>(() => new Web6CandidateCutoverCoordinator(plan).Admit(Web6CutoverPair.Old, admission));
        }
    }

    private static Web6CandidateCutoverPlan Plan(int currentSchema = 13, int oldSchema = 12, string? oldReceipt = null, string? restoredReceipt = null) =>
        new Web6CandidateCutoverPlan(
            "web6-current", "web4-old", "web6-restored",
            new Uri("https://localhost:44321/"), new Uri("https://localhost:44322/"),
            FrontendArchive, FrontendTree, FrontendArtifact,
            BackendArchive, BackendTree, BackendArtifact, new string('9', 64),
            currentSchema, oldSchema, CurrentReceipt, oldReceipt ?? OldReceipt, restoredReceipt ?? new string('7',64),
            CurrentEpoch, RestoredEpoch);

    private static Web6CutoverAdmission Admission(Web6CandidateCutoverPlan plan, Web6CutoverPair pair,
        int controllers = 1, int workers = 1) => pair == Web6CutoverPair.Restored
            ? new Web6CutoverAdmission(plan.CurrentOrigin, 13, plan.RestoredDatabaseReceipt, plan.CurrentArtifactHash, RestoredEpoch, controllers, workers, true)
            : pair == Web6CutoverPair.Current
            ? new Web6CutoverAdmission(plan.CurrentOrigin, plan.CurrentSchema, plan.CurrentDatabaseReceipt,
                plan.CurrentArtifactHash, CurrentEpoch, controllers, workers, queueOwned: true)
            : new Web6CutoverAdmission(plan.OldOrigin, plan.OldSchema, plan.OldDatabaseReceipt,
                plan.OldArtifactHash, Guid.Empty, controllers, workers, queueOwned: true);

    [Fact]
    public async Task IPC_session_maps_frontend_switches_and_requires_actual_lifecycle_observations()
    {
        var runtime = new RecordingRuntime();
        var session = new Web6CutoverSession(Plan(), runtime);
        await session.Begin(new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), CancellationToken.None);
        var id = Guid.NewGuid();
        await Handle(session, "oldClientNewHostOn", null);
        await Handle(session, "verifyNoCutoverMutation", Guid.NewGuid());
        await Handle(session, "oldClientNewHostOff", null);
        await Handle(session, "newClientOldHostOn", null);
        await Handle(session, "verifyNoCutoverMutation", Guid.NewGuid());
        await Handle(session, "newClientOldHostOff", null);
        var committed = Guid.NewGuid();
        await Handle(session, "committedUnavailable", committed, Proof(committed));
        await Handle(session, "verifyNoCutoverMutation", committed);
        await Handle(session, "sameEpochRestart", id);
        await Handle(session, "revokeGrantOn", id);
        await Handle(session, "revokeGrantOff", id);

        await Handle(session, "restoreEpoch", id);
        await Handle(session, "verifyNoCutoverMutation", id);
        session.VerifyComplete();
        Assert.Equal(new[] { "Current:current", "Current:cached", "zero", "Current:current", "Old:current", "zero",
            "Current:current", "committed", "unavailable", "zero", "identity", "Current:current", "identity", "grant:False", "grant:True",
            "Restored:current", "zero" }, runtime.Calls);
        var incomplete = new Web6CutoverSession(Plan(), new RecordingRuntime());
        await incomplete.Begin(new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), CancellationToken.None);
        Assert.Throws<InvalidDataException>(incomplete.VerifyComplete);
    }

    [Fact]
    public async Task Missing_committed_identity_never_arms_unavailable_mode()
    {
        var runtime = new RecordingRuntime { RejectCommitted = true };
        var session = new Web6CutoverSession(Plan(), runtime);
        await session.Begin(new Web6CutoverDeadline(TimeSpan.FromSeconds(210)), CancellationToken.None);
        var id = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidDataException>(() => Handle(session, "committedUnavailable", id, Proof(id)));
        Assert.DoesNotContain("unavailable", runtime.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Owned_allocations_outlive_browser_oracle_and_are_not_disposed_if_drain_is_uncertain(bool failed)
    {
        var calls = new List<string>();
        Func<Task> body = () => { calls.Add("browser-oracle"); if (failed) throw new IOException("browser failed"); return Task.CompletedTask; };
        Func<Task<bool>> drain = () => { calls.Add("drain"); return Task.FromResult(true); };
        Func<Task>[] cleanup = { () => { calls.Add("current-dispose"); return Task.CompletedTask; }, () => { calls.Add("old-dispose"); return Task.CompletedTask; }, () => { calls.Add("restored-dispose"); return Task.CompletedTask; } };
        if (failed) await Assert.ThrowsAsync<IOException>(() => Web6CutoverLifetime.Run(body, drain, cleanup));
        else await Web6CutoverLifetime.Run(body, drain, cleanup);
        Assert.Equal(new[] { "browser-oracle", "drain", "current-dispose", "old-dispose", "restored-dispose" }, calls);
        calls.Clear();
        await Assert.ThrowsAsync<IOException>(() => Web6CutoverLifetime.Run(() => Task.CompletedTask, () => Task.FromResult(false), cleanup));
        Assert.Empty(calls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Bootstrap_failure_before_operations_creation_retains_every_allocation(int allocatedCount)
    {
        var allocated = new HashSet<int>();
        var evidence = new HashSet<int>();
        var cleanupCalls = 0;
        var beforeOperations = new IOException("Injected failure before operations creation.");
        var observed = await Assert.ThrowsAsync<IOException>(() => Web6CutoverLifetime.Run(() =>
        {
            for (var index = 1; index <= allocatedCount; index++) { allocated.Add(index); evidence.Add(index); }
            throw beforeOperations;
        }, () => Task.FromResult(true), new Func<Task>[]
        {
            () => { cleanupCalls++; allocated.Clear(); evidence.Clear(); return Task.CompletedTask; },
        }, stopOnCleanupFailure: true));
        Assert.Same(beforeOperations, observed);
        Assert.Equal(0, cleanupCalls);
        Assert.Equal(allocatedCount, allocated.Count);
        Assert.Equal(allocatedCount, evidence.Count);
    }

    private sealed class RecordingRuntime : IWeb6CutoverRuntime
    {
        internal List<string> Calls { get; } = new List<string>();
        internal bool RejectCommitted;
        internal bool FailRestored;
        public Task Switch(Web6CutoverPair pair, bool cached, Guid epoch, Web6CutoverDeadline deadline, CancellationToken token) { Calls.Add(pair + (cached ? ":cached" : ":current")); if (FailRestored && pair == Web6CutoverPair.Restored) throw new IOException("restored admission failed"); return Task.CompletedTask; }
        public Task VerifyStableAdmission(Web6CutoverPair pair, bool cached, Guid epoch, Web6CutoverDeadline deadline, CancellationToken token) => Task.CompletedTask;
        public Task VerifyOperation(Guid id, bool committed, Web6CutoverDeadline deadline, CancellationToken token) { Calls.Add(committed ? "committed" : "identity"); if (committed && RejectCommitted) throw new InvalidDataException(); return Task.CompletedTask; }
        public Task VerifyCommittedSetup(Web6CommittedSetup proof, Web6CutoverDeadline deadline, CancellationToken token) => VerifyOperation(proof.Operation, true, deadline, token);
        public Task VerifyLifecycle(Web6CutoverDeadline deadline, CancellationToken token) => Task.CompletedTask;
        public Task SetGrant(bool granted, Web6CutoverDeadline deadline, CancellationToken token) { Calls.Add("grant:" + granted); return Task.CompletedTask; }
        public Task MakeCommittedUnavailable(Guid id, Web6CutoverDeadline deadline, CancellationToken token) { Calls.Add("unavailable"); return Task.CompletedTask; }
        public Task VerifyZeroMutation(Guid id, Web6CutoverDeadline deadline, CancellationToken token) { Calls.Add("zero"); return Task.CompletedTask; }
    }

    private static Web6CandidateExpected CurrentExpected() => new Web6CandidateExpected("web6-current",
        "0e0330d5bfcf131f577f784ee6372f865b90e24e", "9cde71a6528615d369ededcbaf2f5b3490cb5d3a", new string('8', 64));

    [Fact]
    public void Frontend_shaped_result_binds_current_identity_and_all_historical_manifest_handles()
    {
        var count = JObject.Parse("{\"version\":2,\"discovered\":3,\"passed\":3,\"failed\":0,\"skipped\":0,\"interrupted\":0,\"retries\":0}");
        var plan = Plan();
        foreach (var property in plan.FrontendHandles().Properties())
        {
            var handles = plan.FrontendHandles(); handles[property.Name] = "wrong";
            Assert.Throws<InvalidDataException>(() => Web6CutoverOracle.Validate(count, CutoverResult(plan), plan, CurrentExpected(), handles));
        }
        var duplicate = CutoverResult(plan); ((JArray)duplicate["checks"]!).Add(duplicate["checks"]![0]!.DeepClone());
        Assert.Throws<InvalidDataException>(() => Web6CutoverOracle.Validate(count, duplicate, plan, CurrentExpected(), plan.FrontendHandles()));
        for (var missing = 0; missing < 6; missing++)
        {
            var flags = new[] { true, true, true, true, true, true }; flags[missing] = false;
            Assert.Throws<InvalidDataException>(() => Web6CutoverOracle.ValidateDisposition(flags[0], flags[1], flags[2], flags[3], flags[4], flags[5]));
        }
    }

    [Fact]
    public async Task Start_failure_still_drains_armed_host_before_releasing_allocations()
    {
        var operations = new RecordingCutoverOperations(Plan()) { FailStart = true };
        await Assert.ThrowsAsync<IOException>(() => new Web6CandidateCutoverOrchestrator(operations).Execute(CancellationToken.None));
        Assert.Contains("chromium:current:drain", operations.Calls);
        Assert.DoesNotContain("complete", operations.Calls);
    }

    [Fact]
    public void Legacy_grant_cleanup_frame_can_omit_the_operation_id()
    {
        var frame = ControlFrame("revokeGrantOff");
        frame.Remove("operationId");
        var request = Web6CutoverControl.Validate(frame, "secret");
        Assert.Equal("revokeGrantOff", request.Action);
        Assert.Null(request.Operation);
        Assert.Null(request.CommittedSetup);
    }

    // Literal shape emitted by frontend createWeb6PilotResult at reviewed 0e0330d5.
    // Historical pins are in the separately protected input manifest, never invented result fields.
    private static JObject CutoverResult(Web6CandidateCutoverPlan plan) => JObject.Parse(@"{
        ""version"":2,""mode"":""web6-candidate"",""candidateId"":""web6-current"",
        ""frontendSourceSha"":""0e0330d5bfcf131f577f784ee6372f865b90e24e"",
        ""backendSourceSha"":""9cde71a6528615d369ededcbaf2f5b3490cb5d3a"",
        ""frontendDistManifestSha256"":""8888888888888888888888888888888888888888888888888888888888888888"",
        ""operations"":[],""checks"": [
            ""web6-cutover-old-client-new-host"",""web6-cutover-new-client-old-host"",
            ""web6-cutover-revoked-grant"",""web6-cutover-committed-unavailable-protected"",
            ""web6-cutover-same-epoch-restart"",""web6-cutover-restored-epoch-rejected""]
    }");

    [Fact]
    public async Task Cancelled_aggregate_refuses_between_browser_handoff_and_completion()
    {
        using var parent = new CancellationTokenSource();
        var operations = new RecordingCutoverOperations(Plan()) { AfterRestore = parent.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new Web6CandidateCutoverOrchestrator(operations).Execute(parent.Token));
        Assert.DoesNotContain("edge:current:start", operations.Calls);
        Assert.DoesNotContain("complete", operations.Calls);
        Assert.True(operations.RetainForContainment);
    }

    [Fact]
    public async Task Aggregate_deadline_covers_handoff_after_first_controller_and_stops_second_browser()
    {
        var now = TimeSpan.Zero;
        var operations = new RecordingCutoverOperations(Plan()) { AfterRestore = () => now = TimeSpan.FromSeconds(3300) };
        await Assert.ThrowsAsync<TimeoutException>(() => new Web6CandidateCutoverOrchestrator(operations, () => now).Execute(CancellationToken.None));
        Assert.Contains("chromium:restore", operations.Calls);
        Assert.DoesNotContain("edge:current:start", operations.Calls);
        Assert.DoesNotContain("complete", operations.Calls);
        Assert.True(operations.RetainForContainment);
    }

    [Fact]
    public async Task Aggregate_expiry_after_final_observation_still_retains_allocations()
    {
        var now = TimeSpan.Zero;
        var operations = new RecordingCutoverOperations(Plan()) { AfterComplete = () => now = TimeSpan.FromSeconds(3300) };
        await Assert.ThrowsAsync<TimeoutException>(() => new Web6CandidateCutoverOrchestrator(operations, () => now).Execute(CancellationToken.None));
        Assert.True(operations.RetainForContainment);
    }

    [Fact]
    public async Task Unjoined_browser_failure_does_not_even_invoke_host_drain_or_handoff()
    {
        var failure = new IOException("unjoined controller");
        var operations = new RecordingCutoverOperations(Plan()) { RunFailure = failure, RetainForContainment = true };
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => new Web6CandidateCutoverOrchestrator(operations).Execute(CancellationToken.None)));
        Assert.DoesNotContain("chromium:current:drain", operations.Calls);
        Assert.DoesNotContain("chromium:restore", operations.Calls);
        Assert.DoesNotContain("edge:current:start", operations.Calls);
        Assert.DoesNotContain("complete", operations.Calls);
    }

    [Fact]
    public async Task Aggregate_expiry_immediately_after_browser_result_prevents_inner_drain()
    {
        var now = TimeSpan.Zero;
        var operations = new RecordingCutoverOperations(Plan()) { AfterRun = () => now = TimeSpan.FromSeconds(3300) };
        await Assert.ThrowsAsync<TimeoutException>(() => new Web6CandidateCutoverOrchestrator(operations, () => now).Execute(CancellationToken.None));
        Assert.DoesNotContain("chromium:current:drain", operations.Calls);
        Assert.True(operations.RetainForContainment);
    }

    private sealed class RecordingCutoverOperations : IWeb6CandidateCutoverOperations
    {
        public bool RetainForContainment { get; set; }
        public void RetainAfterDeadline() => RetainForContainment = true;
        public void BindDeadline(Web6CutoverDeadline deadline, CancellationToken token) { }
        private readonly Web6CandidateCutoverPlan _plan;
        internal RecordingCutoverOperations(Web6CandidateCutoverPlan plan) => _plan = plan;
        internal List<string> Calls { get; } = new List<string>();
        internal bool SkipChromium { get; set; }
        internal bool DriftAdmission { get; set; }
        internal bool FailStart { get; set; }
        internal bool FailEdgeStart { get; set; }
        internal Action? AfterRestore { get; set; }
        internal Action? AfterComplete { get; set; }
        internal Exception? RunFailure { get; set; }
        internal Action? AfterRun { get; set; }
        public Task<Web6CandidateCutoverPlan> VerifyAuthority(CancellationToken token) { Calls.Add("authority"); return Task.FromResult(_plan); }
        public Task SealProtectedManifest(JObject handles, CancellationToken token) { Calls.Add("manifest"); return Task.CompletedTask; }
        public Task<Web6CutoverAdmission> StartAndObserve(string browser, Web6CutoverPair pair, CancellationToken token)
        {
            Calls.Add(browser + ":" + pair.ToString().ToLowerInvariant() + ":start");
            if (FailStart || (FailEdgeStart && browser == "edge")) throw new IOException("start failed after process creation");
            var admission = Admission(_plan, pair);
            return Task.FromResult(DriftAdmission ? admission.WithReceipt(new string('0', 64)) : admission);
        }
        public Task<Web6CutoverBrowserResult> RunAndValidate(string browser, Web6CutoverPair pair, CancellationToken token)
        {
            Calls.Add(browser + ":" + pair.ToString().ToLowerInvariant() + ":validate");
            AfterRun?.Invoke();
            if (RunFailure != null) throw RunFailure;
            return Task.FromResult(new Web6CutoverBrowserResult(3, SkipChromium && browser == "chromium" ? 2 : 3,
                0, SkipChromium && browser == "chromium" ? 1 : 0, 0, new[]
                {
                    "web6-cutover-old-client-new-host", "web6-cutover-new-client-old-host",
                    "web6-cutover-revoked-grant", "web6-cutover-committed-unavailable-protected",
                    "web6-cutover-same-epoch-restart", "web6-cutover-restored-epoch-rejected",
                }));
        }
        public Task<Web6CutoverDrain> Drain(string browser, Web6CutoverPair pair, CancellationToken token) { Calls.Add(browser + ":" + pair.ToString().ToLowerInvariant() + ":drain"); return Task.FromResult(new Web6CutoverDrain(true, true)); }
        public Task VerifyRestore(string browser, CancellationToken token) { Calls.Add(browser + ":restore"); AfterRestore?.Invoke(); return Task.CompletedTask; }
        public Task Complete(CancellationToken token) { Calls.Add("complete"); AfterComplete?.Invoke(); return Task.CompletedTask; }
    }
}
