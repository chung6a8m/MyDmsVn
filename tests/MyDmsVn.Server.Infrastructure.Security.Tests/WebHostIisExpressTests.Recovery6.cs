using System;
using MyDmsVn.Web6.CandidateBootstrap;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed partial class WebHostIisExpressTests
{
    internal static async Task RunCandidateRecovery(IReadOnlyDictionary<string, string?> values,
        Web6WorkloadTask4Receipt task4, string publish, string thumbprint)
    {
        using var bootstrap = Web6SelectorOutput.Open("recovery");
        var currentBackend = AdmitCurrentBackend(bootstrap);
        var candidateRoot = Web6CandidateFileSystem.CanonicalPath(values["MYDMSVN_WEB6_CANDIDATE_ROOT"]!);
        var recoveryRoot = Path.GetFullPath(values["MYDMSVN_WEB6_RECOVERY_ROOT"]!);
        var frontendRoot = Web6CandidateFileSystem.CanonicalPath(values["MYDMSVN_WEB_TEST_BROWSER_DIRECTORY"]!);
        var candidateManifest = Path.Combine(candidateRoot, "candidate.manifest");
        Web6CandidateFileSystem.RequireRegularFile(candidateManifest);
        using (var identity = WindowsIdentity.GetCurrent())
            Web6CandidateManifest.RequirePrivateOwner(File.GetAccessControl(candidateManifest, SqlTestPrivateRecord.Sections), identity.User!);
        var manifestHash = SqlTestPrivateRecord.Hash(File.ReadAllBytes(candidateManifest));
        if (task4.BackendSha != currentBackend.Verify().Sha || task4.FrontendSha != GitIdentity(frontendRoot).Sha ||
            task4.FrontendDistHash != Web6CandidateFileSet.MeasureTree(Path.Combine(frontendRoot, "dist")) ||
            task4.FrontendDistHash != Web6CandidateFileSet.MeasureTree(Path.Combine(publish, "wwwroot")) ||
            task4.ManifestHash != manifestHash)
            throw new InvalidDataException("Protected Task4 receipt does not bind the measured recovery publication and candidate root.");

        var fixture = await currentBackend.AllocateAsync(() => Web6MaintenanceRecoveryFixture.CreateAsync(recoveryRoot));
        try
        {
            await currentBackend.AllocateAsync(fixture.PrepareSourceForLiveServiceAsync);
            var plan = new Web6CandidateRecoveryPlan(fixture.SourceDatabaseIdentity, fixture.DestinationDatabaseIdentity,
                fixture.SourceEpoch, fixture.RestoredEpoch, fixture.SourceRuntimePrincipal, fixture.RestoredRuntimePrincipal);
            var operations = new LiveCandidateRecoveryOperations(fixture, publish, thumbprint, task4.CandidateId,
                recoveryRoot, frontendRoot, currentBackend, bootstrap);
            var result = await new Web6CandidateRecoveryCoordinator(operations).Execute(plan, CancellationToken.None);
            if (!result.TrustedService || result.RpoLostBusinessRows != 0 || result.RpoLostJournalRows != 0)
                throw new InvalidDataException("WEB6 service recovery result was not trusted and zero-loss.");
            if (bootstrap != null)
                Web6BootstrapSelectorEvidence.Complete(bootstrap, operations.BootstrapResources, operations.BootstrapSqlReceipt!,
                    rpo: checked(result.RpoLostBusinessRows + result.RpoLostJournalRows),
                    rto: checked((long)Math.Ceiling(result.RtoTicks * 1000d / Stopwatch.Frequency)));
        }
        catch
        {
            // The coordinator/adapter owns verified cleanup after host start. Before start,
            // any uncertain SQL/backup state remains in the protected recovery root.
            fixture.RetainLiveEvidence();
            throw;
        }
    }

    private sealed class LiveCandidateRecoveryOperations : IWeb6CandidateRecoveryOperations
    {
        private const string Origin = "https://localhost:44321";
        private readonly Web6MaintenanceRecoveryFixture _fixture;
        private readonly string _publish, _publishHash, _thumbprint, _candidateId, _recoveryRoot;
        private readonly string _frontendRoot, _backendRoot;
        private readonly Web6CandidateBackendSource _backendSource;
        private readonly byte[] _jwt = new byte[32];
        private Process? _process;
        private Web5IisExpressIdentity? _identity;
        private readonly List<Web6PrivateDirectoryLease> _runtimeRoots = new List<Web6PrivateDirectoryLease>();
        private Web6CandidateManifest? _activeManifest;
        private string? _configuration, _access;
        private bool _sourceDrained, _restoredDrained, _retained;
        private string? _activeRole, _activeSqlReceipt;
        private Guid _saleCreate, _saleUpdate, _salePost, _staleWriter;
        private readonly Web6SelectorOutput? _bootstrap;
        internal JArray BootstrapResources { get; } = new JArray();
        internal string? BootstrapSqlReceipt { get; private set; }

        internal LiveCandidateRecoveryOperations(Web6MaintenanceRecoveryFixture fixture, string publish, string thumbprint,
            string candidateId, string recoveryRoot, string frontendRoot, Web6CandidateBackendSource backendSource, Web6SelectorOutput? bootstrap)
        {
            _fixture = fixture; _publish = publish; _thumbprint = thumbprint; _candidateId = candidateId; _recoveryRoot = recoveryRoot;
            _frontendRoot = frontendRoot; _backendRoot = backendSource.Root; _backendSource = backendSource;
            _bootstrap = bootstrap;
            _publishHash = Web6CandidateFileSet.MeasureTree(publish);
            using var random = RandomNumberGenerator.Create(); random.GetBytes(_jwt);
        }

        public long Timestamp() => Stopwatch.GetTimestamp();

        public Task VerifySource(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (plan.SourceDatabaseIdentity != _fixture.SourceDatabaseIdentity || plan.SourceEpoch != _fixture.SourceEpoch ||
                plan.SourceRuntimePrincipal != _fixture.SourceRuntimePrincipal || Web6CandidateFileSet.MeasureTree(_publish) != _publishHash)
                throw new InvalidDataException("Recovery source authority changed.");
            return Task.CompletedTask;
        }

        public Task StartSourceHost(Web6CandidateRecoveryPlan plan, CancellationToken token) =>
            StartHost("source", _fixture.LiveSourceRuntimeConnectionString, token);

        public Task VerifySourceAdmission(Web6CandidateRecoveryPlan plan, CancellationToken token) => VerifyActiveAdmission(token);

        public Task DetectFailure(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_activeRole != "source" || _process == null || _process.HasExited)
                throw new IOException("Source candidate outage cannot be observed from an admitted live process.");
            _process.Kill();
            if (!_process.WaitForExit(10000)) throw new IOException("Source candidate interruption was not observed.");
            return Task.CompletedTask;
        }

        public async Task DrainSource(CancellationToken token)
        {
            await DrainActive(token);
            _sourceDrained = true;
        }

        public async Task<Web6VerifiedBackup> Backup(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!_sourceDrained) throw new InvalidOperationException("Verified source drain is required before backup.");
            await _fixture.BackupLiveSourceAsync();
            var expected = _fixture.BackupSha256;
            var observed = _fixture.BackupSha256;
            return new Web6VerifiedBackup(expected, observed);
        }

        public async Task RestoreDistinct(Web6CandidateRecoveryPlan plan, Web6VerifiedBackup backup, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); backup.Validate();
            if (plan.DestinationDatabaseIdentity != _fixture.DestinationDatabaseIdentity ||
                plan.SourceDatabaseIdentity == plan.DestinationDatabaseIdentity || backup.ObservedSha256 != _fixture.BackupSha256)
                throw new InvalidDataException("Distinct restored allocation or backup seal changed.");
            await _fixture.RestoreLiveDestinationAsync();
        }

        public async Task VerifyRestoreOracle(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); await _fixture.VerifyLiveRestoreOracleAsync();
            _ = Web6CandidateOracle.CompareRecovery(_fixture.SourceRecoverySnapshot!, _fixture.RestoredRecoverySnapshot!);
        }

        public async Task RotateEpoch(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); await _fixture.RotateLiveEpochAsync();
            if (plan.RestoredEpoch != _fixture.RestoredEpoch || plan.RestoredEpoch == plan.SourceEpoch)
                throw new InvalidDataException("Restored epoch rotation changed.");
        }

        public async Task ReprovisionPrincipals(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); await _fixture.ProvisionLiveDestinationAsync();
            if (plan.RestoredRuntimePrincipal != _fixture.RestoredRuntimePrincipal || plan.RestoredRuntimePrincipal == plan.SourceRuntimePrincipal)
                throw new InvalidDataException("Restricted restored principal changed.");
        }

        public Task StartRestoredHost(Web6CandidateRecoveryPlan plan, CancellationToken token) =>
            StartHost("restored", _fixture.LiveRuntimeConnectionString, token);

        private Task StartHost(string role, string connectionString, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_process != null) throw new InvalidOperationException("Recovery host is already started.");
            var runtimeRoot = Web6PrivateDirectoryLease.CreateNew(_recoveryRoot + "-" + role + "-runtime");
            _runtimeRoots.Add(runtimeRoot);
            _configuration = CreateConfiguration(runtimeRoot.Path, _publish);
            var candidateControl = Path.Combine(runtimeRoot.Path, "candidate-control");
            _activeSqlReceipt = _fixture.LiveOwnershipReceiptSha256;
            _activeManifest = CreateAdmissionManifest(candidateControl, _configuration, _activeSqlReceipt);
            var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IIS Express", "iisexpress.exe");
            var start = new ProcessStartInfo(executable, "/config:\"" + _configuration + "\" /site:Web2Smoke /systray:false")
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            };
            foreach (var name in start.EnvironmentVariables.Keys.Cast<string>().Where(name => name.StartsWith("MYDMSVN_", StringComparison.OrdinalIgnoreCase)).ToArray())
                start.EnvironmentVariables.Remove(name);
            start.EnvironmentVariables["MYDMSVN_WEB_ORIGIN"] = Origin;
            start.EnvironmentVariables["MYDMSVN_WEB_OPERATION_MODE"] = "web5";
            start.EnvironmentVariables["MYDMSVN_WEB_JWT_ISSUER"] = Origin;
            start.EnvironmentVariables["MYDMSVN_WEB_JWT_AUDIENCE"] = "web-smoke";
            start.EnvironmentVariables["MYDMSVN_WEB_JWT_ACTIVE_KID"] = "smoke";
            start.EnvironmentVariables["MYDMSVN_WEB_JWT_KEYS"] = "smoke:" + Convert.ToBase64String(_jwt);
            start.EnvironmentVariables["MYDMSVN_WEB_SQL_CONNECTION_STRING"] = connectionString;
            start.EnvironmentVariables["MYDMSVN_WEB_SUPERUSER_USERNAME"] = "web.smoke";
            start.EnvironmentVariables["MYDMSVN_WEB6_RESEND_ELIGIBILITY"] = "receipt-v1";
            start.EnvironmentVariables["MYDMSVN_WEB6_CANDIDATE_ID"] = _candidateId;
            start.EnvironmentVariables["MYDMSVN_WEB6_CANDIDATE_ROOT"] = candidateControl;
            start.EnvironmentVariables["MYDMSVN_WEB_SECURITY_AUDIT_PATH"] = _activeManifest.AuditPath;
            _process = new Process { StartInfo = start }; _activeRole = role; _access = null;
            _process.OutputDataReceived += (_, _) => { }; _process.ErrorDataReceived += (_, _) => { };
            if (!_process.Start()) throw new IOException("Recovery IIS Express process did not start.");
            _process.BeginOutputReadLine(); _process.BeginErrorReadLine();
            _identity = Web5IisExpressHostAdmission.Capture(_process, _configuration, "Web2Smoke", new Uri(Origin + "/"));
            return Task.CompletedTask;
        }

        public async Task VerifyRestoredAdmission(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            await VerifyActiveAdmission(token);
            _fixture.AdmitLiveService();
        }

        private Web6CandidateManifest CreateAdmissionManifest(string root, string configuration, string sqlReceipt)
        {
            var backend = _backendSource.Verify(); var frontend = GitIdentity(_frontendRoot);
            var inputs = new Web6CandidateInputs(_candidateId, backend, frontend,
                SqlSchemaDefinitionFingerprint.Schema13.ToLowerInvariant(), new Uri(Origin + "/"),
                CertificateHash(_thumbprint), "Web6CandidateRecovery", 600, 60);
            var manifest = Web6CandidateManifest.CreateNew(root, inputs);
            using (var identity = WindowsIdentity.GetCurrent())
            using (new FileStream(manifest.AuditPath, FileMode.CreateNew, FileSystemRights.FullControl, FileShare.Read,
                4096, FileOptions.WriteThrough, SqlTestPrivateRecord.FileAcl(identity.User!))) { }
            var locks = Directory.GetFiles(_backendRoot, "packages.lock.json", SearchOption.AllDirectories)
                .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or ".git"))
                .Select(path => path.Substring(_backendRoot.Length + 1)).ToArray();
            manifest.SealArtifacts(Web6CandidateFileSet.ForPublishedHost(_publish, configuration, _backendRoot, locks,
                _frontendRoot, Path.Combine(_frontendRoot, "dist")), sqlReceipt);
            return manifest;
        }

        private async Task VerifyActiveAdmission(CancellationToken token)
        {
            if (_process == null || _identity == null || _activeManifest == null || _activeSqlReceipt == null)
                throw new InvalidOperationException("Recovery host start was not armed.");
            await Web5IisExpressHostAdmission.WaitForAdmissionAsync(_identity, _process, TimeSpan.FromSeconds(45), token);
            var queue = await Web5IisExpressHostAdmission.VerifyAsync(_identity, token);
            if (_bootstrap != null) Web6BootstrapSelectorEvidence.Host(BootstrapResources, _identity, queue.Name);
            if (_process.HasExited || queue.AttachedProcessCount != 1) throw new IOException("Recovery host admission is not exclusive.");
            var backend = _backendSource.Verify(); var frontend = GitIdentity(_frontendRoot);
            _activeManifest.VerifyBeforeAdmission(_process.Id, _process.StartTime.ToUniversalTime(), queue.Name, () =>
                new Web6CandidateObservation((backend.Sha, backend.Tree, true), (frontend.Sha, frontend.Tree, true),
                    SqlSchemaDefinitionFingerprint.Schema13.ToLowerInvariant(), CertificateHash(_thumbprint), _activeSqlReceipt,
                    new[] { queue }, new Web6CandidateAuditLaunch(_process.Id, _process.StartTime.ToUniversalTime(),
                        Path.GetDirectoryName(_activeManifest.AuditPath)!, _activeManifest.AuditPath, _candidateId)));
            var health = await Request(Origin + "/api/v1/health", _thumbprint);
            if (health.Status != 200 || health.Body != "{\"data\":{\"status\":\"ok\"}}")
                throw new IOException("Candidate was admitted before healthy readiness.");
            _activeManifest.VerifyForWorkload(_candidateId);
        }

        private async Task DrainActive(CancellationToken token)
        {
            if (_process != null)
            {
                if (!_process.HasExited)
                {
                    try { _process.StandardInput.WriteLine("q"); } catch (IOException) { }
                    if (!_process.WaitForExit(10000)) _process.Kill();
                }
                if (!_process.WaitForExit(10000) || _identity == null) throw new IOException("Recovery worker exit identity is uncertain.");
                await Web5IisExpressHostAdmission.WaitForAbsenceAsync(_identity, TimeSpan.FromSeconds(20), token);
                _process.Dispose(); _process = null; _identity = null;
            }
            _activeManifest?.Dispose(); _activeManifest = null; _activeRole = null; _activeSqlReceipt = null; _access = null;
        }

        public async Task RejectStaleWriterReceiptAndStatus(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            await Login();
            var before = await _fixture.LiveEffectSnapshotAsync(token);
            var body = new JObject
            {
                ["saleDate"] = "2026-11-10", ["customerId"] = _fixture.CustomerId, ["warehouseId"] = _fixture.WarehouseId,
                ["employeeId"] = _fixture.EmployeeId, ["lines"] = new JArray(new JObject { ["saleLineId"] = "0",
                    ["productId"] = _fixture.ProductId, ["qty"] = "1.0000", ["unitPrice"] = "25.0000", ["isGift"] = false })
            };
            _staleWriter = Guid.NewGuid();
            var staleWriter = await Mutation("/api/v1/sales-receipts", "POST", body, _staleWriter, _fixture.SourceEpoch);
            var fingerprint = string.Concat(_fixture.OldFingerprint.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            var staleHeaders = new Dictionary<string, string> { ["X-MyDmsVn-Deployment-Epoch"] = _fixture.SourceEpoch.ToString("D"),
                ["X-MyDmsVn-Operation-Fingerprint"] = fingerprint };
            var staleStatus = await Request(Origin + "/api/v1/operations/" + _fixture.OldCommittedOperation.ToString("D"), _thumbprint, _access, headers: staleHeaders);
            staleHeaders["X-MyDmsVn-Operation-Kind"] = "Sales.Create";
            var staleReceipt = await Request(Origin + "/api/v3/operations/" + _fixture.OldCommittedOperation.ToString("D") + "/resend-receipt", _thumbprint, _access, headers: staleHeaders);
            var after = await _fixture.LiveEffectSnapshotAsync(token);
            Web6RecoveryHttpDenial.RequireNoEffect(before, after);
            Web6RecoveryHttpDenial.Require(staleWriter.Status, staleWriter.Body, "stale writer");
            Web6RecoveryHttpDenial.Require(staleStatus.Status, staleStatus.Body, "stale status");
            Web6RecoveryHttpDenial.Require(staleReceipt.Status, staleReceipt.Body, "stale receipt");
        }

        public async Task AuthenticatedHealth(CancellationToken token)
        {
            await Login();
            var health = await Request(Origin + "/api/v1/health", _thumbprint, _access);
            var me = await Request(Origin + "/api/v1/auth/me", _thumbprint, _access);
            if (health.Status != 200 || me.Status != 200 || (string?)JObject.Parse(me.Body)["data"]?["username"] != "web.smoke")
                throw new IOException("Authenticated recovery health failed.");
            _activeManifest?.VerifyForWorkload(_candidateId);
        }

        public async Task FreshWrite(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            await Login();
            _saleCreate = Guid.NewGuid(); _saleUpdate = Guid.NewGuid(); _salePost = Guid.NewGuid();
            var body = new JObject
            {
                ["saleDate"] = "2026-11-10", ["customerId"] = _fixture.CustomerId, ["warehouseId"] = _fixture.WarehouseId,
                ["employeeId"] = _fixture.EmployeeId,
                ["lines"] = new JArray(
                    new JObject { ["saleLineId"] = "0", ["productId"] = _fixture.ProductId, ["qty"] = "2.0000", ["unitPrice"] = "25.0000", ["isGift"] = false },
                    new JObject { ["saleLineId"] = "0", ["productId"] = _fixture.ProductId, ["qty"] = "1.0000", ["unitPrice"] = "25.0000", ["isGift"] = true })
            };
            var created = RequireData(await Mutation("/api/v1/sales-receipts", "POST", body, _saleCreate, plan.RestoredEpoch), "recovery Sales.Create");
            var id = (string)created["saleId"]!;
            body["expectedVersion"] = created["version"]!.DeepClone(); body["note"] = "WEB6 recovered current epoch";
            for (var index = 0; index < 2; index++) body["lines"]![index]!["saleLineId"] = created["lines"]![index]!["saleLineId"]!.DeepClone();
            var updated = RequireData(await Mutation("/api/v1/sales-receipts/" + id + "/draft", "PUT", body, _saleUpdate, plan.RestoredEpoch), "recovery Sales.Update");
            RequireData(await Mutation("/api/v1/sales-receipts/" + id + "/post", "POST",
                new JObject { ["expectedVersion"] = updated["version"]!.DeepClone() }, _salePost, plan.RestoredEpoch), "recovery Sales.Post");
            using var sql = new SqlConnection(_fixture.LiveObserverConnectionString); await sql.OpenAsync(token);
            await Web6CandidateOracle.VerifyOneEffectAsync(sql, _saleCreate, token);
            await Web6CandidateOracle.VerifyOneEffectAsync(sql, _saleUpdate, token);
            await Web6CandidateOracle.VerifyRecoveryPostAsync(sql, _salePost,
                new Web6StockExpectation(_fixture.WarehouseId, _fixture.ProductId, _fixture.ReceiptProductId), token);
        }

        public Task<(Web6RecoveryOracleSnapshot Before, Web6RecoveryOracleSnapshot After)> RecoverySnapshots(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult((_fixture.SourceRecoverySnapshot!, _fixture.RestoredRecoverySnapshot!));
        }

        public async Task DrainRestored(CancellationToken token)
        {
            await DrainActive(token);
            _restoredDrained = true;
        }

        public async Task CleanupVerified(CancellationToken token)
        {
            if (!_sourceDrained || !_restoredDrained || _retained) throw new InvalidOperationException("Verified source/restored drain is required before cleanup.");
            token.ThrowIfCancellationRequested();
            if (_bootstrap != null)
            {
                var digest = _fixture.LiveOwnershipReceiptSha256;
                var bytes = File.ReadAllBytes(_fixture.ManifestPath);
                if (ProtectedJsonFile.Digest(bytes) != digest) throw new IOException("Recovery original ownership record changed.");
                var allocationPath = Path.Combine(_bootstrap.Plan.EvidenceRoot, "recovery-allocation.json");
                BootstrapSqlReceipt = ProtectedJsonFile.WriteNew(allocationPath, BootstrapJson.Serialize(new JObject
                { ["version"] = 1, ["runId"] = _bootstrap.RunId, ["gate"] = "recovery", ["originalSha256"] = digest,
                    ["originalAllocationBase64"] = Convert.ToBase64String(bytes) }));
                foreach (var name in new[] { _fixture.SourceDatabaseIdentity, _fixture.DestinationDatabaseIdentity })
                    BootstrapResources.Add(Web6BootstrapSelectorEvidence.Resource("database", name, BootstrapSqlReceipt, allocationPath));
                foreach (var name in _fixture.LivePrincipalNames)
                    BootstrapResources.Add(Web6BootstrapSelectorEvidence.Resource("login", name, BootstrapSqlReceipt, allocationPath));
                foreach (var root in _runtimeRoots)
                    BootstrapResources.Add(Web6BootstrapSelectorEvidence.Resource("deletedDirectory", root.Path));
                BootstrapResources.Add(Web6BootstrapSelectorEvidence.Resource("deletedDirectory", _recoveryRoot));
                _bootstrap.SealCleanupInventory(BootstrapResources);
            }
            await Web6RecoveryCleanupDisposition.Execute(_runtimeRoots, _fixture.PrepareLiveCleanupAsync);
            _runtimeRoots.Clear();
        }

        public void RetainEvidence()
        {
            _retained = true;
            _activeManifest?.Dispose(); _activeManifest = null;
            foreach (var root in _runtimeRoots) root.Dispose();
            _fixture.RetainLiveEvidence();
            Console.WriteLine("WEB6_RECOVERY retain=1 activeHostUncertain=" + (_process == null ? "0" : "1") +
                " recoveryRoot=" + _recoveryRoot);
        }

        private async Task Login()
        {
            if (_access != null) return;
            var credentials = JsonConvert.SerializeObject(new { username = "web.smoke", password = "IisSmoke_test-only-password_2026!" });
            var reply = await Request(Origin + "/api/v1/auth/login", _thumbprint, origin: Origin, method: "POST", body: credentials, csrf: true);
            _access = "Bearer " + (string)RequireData(reply, "recovery login")["accessToken"]!;
        }

        private Task<Reply> Mutation(string path, string method, JObject body, Guid operation, Guid epoch) => Request(Origin + path,
            _thumbprint, _access, Origin, method, body.ToString(Formatting.None), csrf: true,
            headers: new Dictionary<string, string> { ["Idempotency-Key"] = operation.ToString("D"),
                ["X-MyDmsVn-Deployment-Epoch"] = epoch.ToString("D") });

        private static JToken RequireData(Reply reply, string operation)
        {
            if (reply.Status != 200) throw new IOException(operation + " failed with HTTP " + reply.Status.ToString(CultureInfo.InvariantCulture));
            return JObject.Parse(reply.Body)["data"]!;
        }
    }
}
