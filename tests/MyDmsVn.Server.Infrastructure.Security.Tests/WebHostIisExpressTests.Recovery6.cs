using System;
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
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed partial class WebHostIisExpressTests
{
    internal static async Task RunCandidateRecovery(IReadOnlyDictionary<string, string?> values,
        Web6WorkloadTask4Receipt task4, string publish, string thumbprint)
    {
        var candidateRoot = Web6CandidateFileSystem.CanonicalPath(values["MYDMSVN_WEB6_CANDIDATE_ROOT"]!);
        var recoveryRoot = Path.GetFullPath(values["MYDMSVN_WEB6_RECOVERY_ROOT"]!);
        var frontendRoot = Web6CandidateFileSystem.CanonicalPath(values["MYDMSVN_WEB_TEST_BROWSER_DIRECTORY"]!);
        var backendRoot = Path.GetDirectoryName(FindRootFile("CleanArchitecture.sln"))!;
        var candidateManifest = Path.Combine(candidateRoot, "candidate.manifest");
        Web6CandidateFileSystem.RequireRegularFile(candidateManifest);
        using (var identity = WindowsIdentity.GetCurrent())
            Web6CandidateManifest.RequirePrivateOwner(File.GetAccessControl(candidateManifest, SqlTestPrivateRecord.Sections), identity.User!);
        var manifestHash = SqlTestPrivateRecord.Hash(File.ReadAllBytes(candidateManifest));
        if (task4.BackendSha != GitIdentity(backendRoot).Sha || task4.FrontendSha != GitIdentity(frontendRoot).Sha ||
            task4.FrontendDistHash != Web6CandidateFileSet.MeasureTree(Path.Combine(frontendRoot, "dist")) ||
            task4.FrontendDistHash != Web6CandidateFileSet.MeasureTree(Path.Combine(publish, "wwwroot")) ||
            task4.ManifestHash != manifestHash)
            throw new InvalidDataException("Protected Task4 receipt does not bind the measured recovery publication and candidate root.");

        var fixture = await Web6MaintenanceRecoveryFixture.CreateAsync(recoveryRoot);
        try
        {
            await fixture.PrepareForLiveServiceAsync();
            var backupHash = fixture.BackupSha256;
            var plan = new Web6CandidateRecoveryPlan(fixture.SourceDatabaseIdentity, fixture.DestinationDatabaseIdentity,
                fixture.SourceEpoch, fixture.RestoredEpoch, fixture.SourceRuntimePrincipal, fixture.RestoredRuntimePrincipal,
                backupHash, backupHash);
            var operations = new LiveCandidateRecoveryOperations(fixture, publish, thumbprint, task4.CandidateId,
                candidateRoot, recoveryRoot);
            var result = await new Web6CandidateRecoveryCoordinator(operations).Execute(plan, CancellationToken.None);
            if (!result.TrustedService || result.RpoLostBusinessRows != 0 || result.RpoLostJournalRows != 0)
                throw new InvalidDataException("WEB6 service recovery result was not trusted and zero-loss.");
        }
        catch
        {
            // The coordinator/adapter owns verified cleanup after host start. Before start,
            // any uncertain SQL/backup state remains in the protected recovery root.
            throw;
        }
    }

    private sealed class LiveCandidateRecoveryOperations : IWeb6CandidateRecoveryOperations
    {
        private const string Origin = "https://localhost:44321";
        private readonly Web6MaintenanceRecoveryFixture _fixture;
        private readonly string _publish, _publishHash, _thumbprint, _candidateId, _candidateRoot, _recoveryRoot;
        private readonly byte[] _jwt = new byte[32];
        private Process? _process;
        private Web5IisExpressIdentity? _identity;
        private Web6PrivateDirectoryLease? _runtimeRoot;
        private string? _configuration, _access;
        private bool _drained, _retained;
        private int _timestampRead;
        private Guid _saleCreate, _saleUpdate, _salePost;

        internal LiveCandidateRecoveryOperations(Web6MaintenanceRecoveryFixture fixture, string publish, string thumbprint,
            string candidateId, string candidateRoot, string recoveryRoot)
        {
            _fixture = fixture; _publish = publish; _thumbprint = thumbprint; _candidateId = candidateId;
            _candidateRoot = candidateRoot; _recoveryRoot = recoveryRoot;
            _publishHash = Web6CandidateFileSet.MeasureTree(publish);
            using var random = RandomNumberGenerator.Create(); random.GetBytes(_jwt);
        }

        public long Timestamp()
        {
            _timestampRead++;
            return _timestampRead switch
            {
                1 => _fixture.LiveFailureDetectedTimestamp,
                2 => _fixture.LiveBackupCompleteTimestamp,
                3 => _fixture.LiveRestoreCompleteTimestamp,
                _ => Stopwatch.GetTimestamp(),
            };
        }

        public Task VerifySource(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (plan.SourceDatabaseIdentity != _fixture.SourceDatabaseIdentity || plan.SourceEpoch != _fixture.SourceEpoch ||
                plan.SourceRuntimePrincipal != _fixture.SourceRuntimePrincipal || Web6CandidateFileSet.MeasureTree(_publish) != _publishHash)
                throw new InvalidDataException("Recovery source authority changed.");
            return Task.CompletedTask;
        }

        public Task DetectFailure(CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        public Task DrainSource(CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.CompletedTask; }

        public Task Backup(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (plan.ExpectedBackupSha256 != _fixture.BackupSha256 || !File.Exists(_fixture.BackupPath))
                throw new InvalidDataException("Protected recovery backup changed.");
            return Task.CompletedTask;
        }

        public Task RestoreDistinct(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (plan.DestinationDatabaseIdentity != _fixture.DestinationDatabaseIdentity ||
                plan.SourceDatabaseIdentity == plan.DestinationDatabaseIdentity || plan.ObservedBackupSha256 != _fixture.BackupSha256)
                throw new InvalidDataException("Distinct restored allocation or backup seal changed.");
            return Task.CompletedTask;
        }

        public Task VerifyRestoreOracle(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _ = Web6CandidateOracle.CompareRecovery(_fixture.SourceRecoverySnapshot!, _fixture.RestoredRecoverySnapshot!);
            return Task.CompletedTask;
        }

        public Task RotateEpoch(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (plan.RestoredEpoch != _fixture.RestoredEpoch || plan.RestoredEpoch == plan.SourceEpoch)
                throw new InvalidDataException("Restored epoch rotation changed.");
            return Task.CompletedTask;
        }

        public Task ReprovisionPrincipals(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (plan.RestoredRuntimePrincipal != _fixture.RestoredRuntimePrincipal ||
                plan.RestoredRuntimePrincipal == plan.SourceRuntimePrincipal)
                throw new InvalidDataException("Restricted restored principal changed.");
            return Task.CompletedTask;
        }

        public Task StartRestoredHost(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_process != null) throw new InvalidOperationException("Recovery host is already started.");
            _runtimeRoot = Web6PrivateDirectoryLease.CreateNew(_recoveryRoot + "-runtime");
            _configuration = CreateConfiguration(_recoveryRoot + "-runtime", _publish);
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
            start.EnvironmentVariables["MYDMSVN_WEB_SQL_CONNECTION_STRING"] = _fixture.LiveRuntimeConnectionString;
            start.EnvironmentVariables["MYDMSVN_WEB_SUPERUSER_USERNAME"] = "web.smoke";
            start.EnvironmentVariables["MYDMSVN_WEB6_RESEND_ELIGIBILITY"] = "receipt-v1";
            start.EnvironmentVariables["MYDMSVN_WEB6_CANDIDATE_ID"] = _candidateId;
            start.EnvironmentVariables["MYDMSVN_WEB6_CANDIDATE_ROOT"] = _candidateRoot;
            start.EnvironmentVariables["MYDMSVN_WEB_SECURITY_AUDIT_PATH"] = Path.Combine(_candidateRoot, "security-audit.log");
            _process = new Process { StartInfo = start };
            _process.OutputDataReceived += (_, _) => { }; _process.ErrorDataReceived += (_, _) => { };
            if (!_process.Start()) throw new IOException("Recovery IIS Express process did not start.");
            _process.BeginOutputReadLine(); _process.BeginErrorReadLine();
            _identity = Web5IisExpressHostAdmission.Capture(_process, _configuration, "Web2Smoke", new Uri(Origin + "/"));
            return Task.CompletedTask;
        }

        public async Task VerifyRestoredAdmission(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            if (_process == null || _identity == null) throw new InvalidOperationException("Recovery host start was not armed.");
            await Web5IisExpressHostAdmission.WaitForAdmissionAsync(_identity, _process, TimeSpan.FromSeconds(45), token);
            var queue = await Web5IisExpressHostAdmission.VerifyAsync(_identity, token);
            if (_process.HasExited || queue.AttachedProcessCount != 1) throw new IOException("Recovery host admission is not exclusive.");
            _fixture.AdmitLiveService();
        }

        public async Task RejectStaleWriterReceiptAndStatus(Web6CandidateRecoveryPlan plan, CancellationToken token)
        {
            await Login();
            var body = new JObject
            {
                ["saleDate"] = "2026-11-10", ["customerId"] = _fixture.CustomerId, ["warehouseId"] = _fixture.WarehouseId,
                ["employeeId"] = _fixture.EmployeeId, ["lines"] = new JArray(new JObject { ["saleLineId"] = "0",
                    ["productId"] = _fixture.ProductId, ["qty"] = "1.0000", ["unitPrice"] = "25.0000", ["isGift"] = false })
            };
            var staleWriter = await Mutation("/api/v1/sales-receipts", "POST", body, Guid.NewGuid(), _fixture.SourceEpoch);
            var fingerprint = string.Concat(_fixture.OldFingerprint.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            var staleHeaders = new Dictionary<string, string> { ["X-MyDmsVn-Deployment-Epoch"] = _fixture.SourceEpoch.ToString("D"),
                ["X-MyDmsVn-Operation-Fingerprint"] = fingerprint };
            var staleStatus = await Request(Origin + "/api/v1/operations/" + _fixture.OldCommittedOperation.ToString("D"), _thumbprint, _access, headers: staleHeaders);
            staleHeaders["X-MyDmsVn-Operation-Kind"] = "Sales.Create";
            var staleReceipt = await Request(Origin + "/api/v3/operations/" + _fixture.OldCommittedOperation.ToString("D") + "/resend-receipt", _thumbprint, _access, headers: staleHeaders);
            if (staleWriter.Status == 200 || staleStatus.Status == 200 || staleReceipt.Status == 200)
                throw new InvalidDataException("Old epoch writer, status or receipt was accepted after recovery.");
        }

        public async Task AuthenticatedHealth(CancellationToken token)
        {
            await Login();
            var health = await Request(Origin + "/api/v1/health", _thumbprint, _access);
            var me = await Request(Origin + "/api/v1/auth/me", _thumbprint, _access);
            if (health.Status != 200 || me.Status != 200 || (string?)JObject.Parse(me.Body)["data"]?["username"] != "web.smoke")
                throw new IOException("Authenticated recovery health failed.");
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
            _drained = true;
        }

        public async Task CleanupVerified(CancellationToken token)
        {
            if (!_drained || _retained) throw new InvalidOperationException("Verified recovery drain is required before cleanup.");
            await _fixture.CleanupLiveServiceAsync();
            _runtimeRoot?.DeleteTreeVerified(); _runtimeRoot = null;
        }

        public void RetainEvidence()
        {
            _retained = true;
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
