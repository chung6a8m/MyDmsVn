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
            "authority", "manifest", "chromium:current:start", "chromium:current:validate", "chromium:current:drain",
            "chromium:old:start", "chromium:old:validate", "chromium:old:drain", "chromium:restore",
            "edge:current:start", "edge:current:validate", "edge:current:drain",
            "edge:old:start", "edge:old:validate", "edge:old:drain", "edge:restore", "complete"
        }, operations.Calls);
        await Assert.ThrowsAsync<IOException>(() => new Web6CandidateCutoverOrchestrator(
            new RecordingCutoverOperations(Plan()) { SkipChromium = true }).Execute(CancellationToken.None));
    }

    [Fact]
    public void Independently_sealed_build_authority_rejects_arbitrary_published_artifact()
    {
        var authority = new Web6CutoverBuildAuthority(FrontendArchive, FrontendTree, FrontendArtifact,
            BackendArchive, BackendTree, BackendArtifact, FrontendArtifact, BackendArtifact);
        authority.Verify(new Web6CutoverBuildObservation(FrontendArchive, FrontendTree, true, FrontendArtifact,
            BackendArchive, BackendTree, true, BackendArtifact, FrontendArtifact, BackendArtifact));
        Assert.Throws<IOException>(() => authority.Verify(new Web6CutoverBuildObservation(FrontendArchive, FrontendTree, true,
            new string('7', 64), BackendArchive, BackendTree, true, BackendArtifact, FrontendArtifact, BackendArtifact)));
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

        coordinator.Restore(RestoredEpoch);
        Assert.False(coordinator.IsEpochCurrent(CurrentEpoch));
        Assert.True(coordinator.IsEpochCurrent(RestoredEpoch));
        Assert.Throws<IOException>(() => coordinator.Restore(RestoredEpoch));
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

    private static Web6CandidateCutoverPlan Plan(int currentSchema = 13, int oldSchema = 12, string? oldReceipt = null) =>
        new Web6CandidateCutoverPlan(
            "web6-current", "web4-old", "web6-restored",
            new Uri("https://localhost:44321/"), new Uri("https://localhost:44322/"),
            FrontendArchive, FrontendTree, FrontendArtifact,
            BackendArchive, BackendTree, BackendArtifact, new string('9', 64),
            currentSchema, oldSchema, CurrentReceipt, oldReceipt ?? OldReceipt,
            CurrentEpoch, RestoredEpoch);

    private static Web6CutoverAdmission Admission(Web6CandidateCutoverPlan plan, Web6CutoverPair pair,
        int controllers = 1, int workers = 1) => pair == Web6CutoverPair.Current
            ? new Web6CutoverAdmission(plan.CurrentOrigin, plan.CurrentSchema, plan.CurrentDatabaseReceipt,
                plan.CurrentArtifactHash, CurrentEpoch, controllers, workers, queueOwned: true)
            : new Web6CutoverAdmission(plan.OldOrigin, plan.OldSchema, plan.OldDatabaseReceipt,
                plan.OldArtifactHash, Guid.Empty, controllers, workers, queueOwned: true);

    private sealed class RecordingCutoverOperations : IWeb6CandidateCutoverOperations
    {
        private readonly Web6CandidateCutoverPlan _plan;
        internal RecordingCutoverOperations(Web6CandidateCutoverPlan plan) => _plan = plan;
        internal List<string> Calls { get; } = new List<string>();
        internal bool SkipChromium { get; set; }
        public Task<Web6CandidateCutoverPlan> VerifyAuthority(CancellationToken token) { Calls.Add("authority"); return Task.FromResult(_plan); }
        public Task SealProtectedManifest(JObject handles, CancellationToken token) { Calls.Add("manifest"); return Task.CompletedTask; }
        public Task<Web6CutoverAdmission> StartAndObserve(string browser, Web6CutoverPair pair, CancellationToken token)
        {
            Calls.Add(browser + ":" + pair.ToString().ToLowerInvariant() + ":start");
            return Task.FromResult(Admission(_plan, pair));
        }
        public Task<Web6CutoverBrowserResult> RunAndValidate(string browser, Web6CutoverPair pair, CancellationToken token)
        {
            Calls.Add(browser + ":" + pair.ToString().ToLowerInvariant() + ":validate");
            return Task.FromResult(new Web6CutoverBrowserResult(3, SkipChromium && browser == "chromium" ? 2 : 3,
                0, SkipChromium && browser == "chromium" ? 1 : 0, 0, new[] { "old-client-new-host", "new-client-old-host", "restored-epoch-rejected" }));
        }
        public Task<Web6CutoverDrain> Drain(string browser, Web6CutoverPair pair, CancellationToken token) { Calls.Add(browser + ":" + pair.ToString().ToLowerInvariant() + ":drain"); return Task.FromResult(new Web6CutoverDrain(true, true)); }
        public Task VerifyRestore(string browser, CancellationToken token) { Calls.Add(browser + ":restore"); return Task.CompletedTask; }
        public Task Complete(CancellationToken token) { Calls.Add("complete"); return Task.CompletedTask; }
    }
}
