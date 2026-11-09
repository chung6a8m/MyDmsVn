using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Desktop.Tests;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed partial class WebHostIisExpressTests
{
    // Both allocation owners outlive this adapter, every browser and the final disposition.
    // No runtime endpoint, global environment mutation or unowned IIS process is used.
    private sealed class LiveCutoverOperations : IWeb6CandidateCutoverOperations, IWeb6CutoverRuntime
    {
        private readonly Web6CandidateCutoverPlan _plan;
        private readonly SqlTestDatabase _current, _old;
        private readonly SqlTestOwnershipManifest _currentControl, _oldControl;
        private readonly OwnedLogin _currentLogin, _oldLogin;
        private readonly Dictionary<string, Deployment> _deployments = new Dictionary<string, Deployment>();
        private readonly Dictionary<Guid, string> _operationIdentities = new Dictionary<Guid, string>();
        private readonly byte[] _jwt = new byte[32];
        private Web6CandidateCutoverCoordinator _coordinator;
        private Web6CutoverSession? _session;
        private WebRecoveryPilotControl? _control;
        private Process? _process;
        private Web5IisExpressIdentity? _identity;
        private Web6CutoverAdmission? _admission;
        private Web6CutoverPair _active;
        private bool _browserDrained = true, _hostDrained = true, _complete;
        private int _browsers;
        private string? _currentSnapshot, _oldSnapshot;
        private int _customer, _warehouse, _employee, _product, _receiptProduct;
        private IntegratedCatalog? _integrated;
        private string Root => Path.GetFullPath(Required("MYDMSVN_WEB6_CANDIDATE_ROOT"));
        private static string Required(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException("Missing " + name);

        internal LiveCutoverOperations(Web6CandidateCutoverPlan plan, SqlTestDatabase current, SqlTestDatabase old,
            SqlTestOwnershipManifest currentControl, SqlTestOwnershipManifest oldControl)
        {
            _plan = plan; _current = current; _old = old; _currentControl = currentControl; _oldControl = oldControl;
            _currentLogin = new OwnedLogin(Required(SqlTestDatabase.ConnectionStringEnvironmentVariable));
            _oldLogin = new OwnedLogin(Required(SqlTestDatabase.ConnectionStringEnvironmentVariable));
            _coordinator = new Web6CandidateCutoverCoordinator(plan);
            using var random = RandomNumberGenerator.Create(); random.GetBytes(_jwt);
        }

        public async Task<Web6CandidateCutoverPlan> VerifyAuthority(CancellationToken token)
        {
            GuiTestSafety.ThrowIfPoisoned();
            // Task 3's browser runner must consume the same immutable publication as this owner.
            if (Path.GetFullPath(Required("MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY")) != Path.GetFullPath(Required("MYDMSVN_WEB6_CUTOVER_CURRENT_PUBLISH_DIRECTORY")))
                throw new IOException("Current publication authorities differ.");
            await VerifyDatabase(_current, _currentControl, true, token);
            await VerifyDatabase(_old, _oldControl, false, token);
            await _currentLogin.CreateAsync(); await _oldLogin.CreateAsync();
            await Seed(_current, _currentLogin, true); await Seed(_old, _oldLogin, false);
            _currentSnapshot = await LifecycleSnapshot(_current.ConnectionString, true);
            _oldSnapshot = await LifecycleSnapshot(_old.ConnectionString, false);
            return _plan;
        }

        private async Task VerifyDatabase(SqlTestDatabase database, SqlTestOwnershipManifest control, bool current, CancellationToken token)
        {
            using var sql = new SqlConnection(database.ConnectionString); await sql.OpenAsync(token);
            await database.VerifyOwnershipAsync(sql, token);
            var fingerprint = await SqlSchemaDefinitionFingerprint.ReadAsync(sql, token);
            Web6CutoverSqlApproval.RequireSchema(fingerprint, current ? SqlSchemaDefinitionFingerprint.Schema13 : SqlSchemaDefinitionFingerprint.Schema12);
            var receipt = Web6CutoverSqlOwnershipReceipt.FromProtectedRecord(control, control.VerifiedDigest(), fingerprint,
                current ? "current-allocation" : "old-allocation").Digest;
            if (receipt != (current ? _plan.CurrentDatabaseReceipt : _plan.OldDatabaseReceipt)) throw new IOException("Cutover ownership receipt changed.");
        }

        private async Task Seed(SqlTestDatabase database, OwnedLogin login, bool current)
        {
            using var sql = new SqlConnection(database.ConnectionString); await sql.OpenAsync();
            await sql.ExecuteAsync("CREATE USER [" + login.Name + "] FOR LOGIN [" + login.Name + "]; CREATE USER WebSmokeMigration WITHOUT LOGIN;");
            await sql.ExecuteAsync(File.ReadAllText(FindRootFile("deployment/sql/Provision-MyDmsVnWebPrincipals.sql"))
                .Replace("$(WebRuntimeUser)", login.Name).Replace("$(MigrationUser)", "WebSmokeMigration"));
            await sql.ExecuteAsync("INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) VALUES(N'web.smoke',N'WEB.SMOKE',N'Web smoke',N'Local',@hash,N'',N'BCrypt'); " +
                "INSERT dbo.SystemSecuritySettings(Id,SuperuserUserId,BoundNormalizedUsername) SELECT 1,UserId,N'WEB.SMOKE' FROM dbo.Users WHERE NormalizedUsername=N'WEB.SMOKE';",
                new { hash = new BcryptPasswordHasher().Hash(BrowserPassword) });
            var customer = await sql.QuerySingleAsync<int>("INSERT dbo.Customers(Code,Name) OUTPUT INSERTED.CustomerId VALUES(N'PILOT-C',N'Pilot Customer');");
            var warehouse = await sql.QuerySingleAsync<int>("INSERT dbo.Warehouses(Code,Name) OUTPUT INSERTED.WarehouseId VALUES(N'PILOT-W',N'Pilot Warehouse');");
            var employee = await sql.QuerySingleAsync<int>("INSERT dbo.Employees(Code,Name) OUTPUT INSERTED.EmployeeId VALUES(N'PILOT-E',N'Pilot Employee');");
            var product = await sql.QuerySingleAsync<int>("INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'PILOT-P',N'Pilot Product',N'EA');");
            var receipt = await sql.QuerySingleAsync<int>("INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'WEB3-RP',N'Web3 Receipt Product',N'EA');");
            await sql.ExecuteAsync("INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@warehouse,@product,1000,15000);", new { warehouse, product });
            await SeedBrowserActors(sql); await SeedReceiptActors(sql);
            var integrated = await SeedIntegratedActorsAndCatalog(sql);
            if (current)
            {
                // Fresh allocation only, before the first writer admission.
                if (await sql.ExecuteAsync("UPDATE dbo.WebDeploymentState SET DeploymentEpoch=@epoch WHERE Id=1;", new { epoch = _plan.CurrentEpoch }) != 1)
                    throw new IOException("Fresh current allocation epoch was not initialized.");
                _customer = customer; _warehouse = warehouse; _employee = employee; _product = product; _receiptProduct = receipt; _integrated = integrated;
            }
        }

        public Task SealProtectedManifest(JObject handles, CancellationToken token)
        {
            if (!JToken.DeepEquals(handles, _plan.FrontendHandles())) throw new IOException("Cutover handles changed.");
            var currentPublish = Path.GetFullPath(Required("MYDMSVN_WEB6_CUTOVER_CURRENT_PUBLISH_DIRECTORY"));
            var oldPublish = Path.GetFullPath(Required("MYDMSVN_WEB6_CUTOVER_OLD_PUBLISH_DIRECTORY"));
            if (Web6CandidateFileSet.MeasureTree(currentPublish) != _plan.CurrentArtifactHash || Web6CandidateFileSet.MeasureTree(oldPublish) != _plan.OldArtifactHash)
                throw new IOException("Published cutover artifacts changed.");
            CreateDeployment("current", currentPublish, Path.Combine(Required("MYDMSVN_WEB_TEST_BROWSER_DIRECTORY"), "dist"), false, false);
            CreateDeployment("cached", currentPublish, Required("MYDMSVN_WEB6_CUTOVER_FRONTEND_DIST_DIRECTORY"), true, false);
            CreateDeployment("old", oldPublish, Path.Combine(Required("MYDMSVN_WEB_TEST_BROWSER_DIRECTORY"), "dist"), false, true);
            _deployments["current"].Handles = _deployments["current"].Manifest!.CreatePrivateInput("cutover-handles.json",
                System.Text.Encoding.UTF8.GetBytes(handles.ToString(Newtonsoft.Json.Formatting.None)));
            return Task.CompletedTask;
        }

        private void CreateDeployment(string key, string published, string dist, bool cached, bool old)
        {
            var backendRoot = old ? Required("MYDMSVN_WEB6_CUTOVER_BACKEND_ARCHIVE_DIRECTORY") : Path.GetDirectoryName(FindRootFile("CleanArchitecture.sln"))!;
            var frontendRoot = cached ? Required("MYDMSVN_WEB6_CUTOVER_FRONTEND_ARCHIVE_DIRECTORY") : Required("MYDMSVN_WEB_TEST_BROWSER_DIRECTORY");
            var backend = GitIdentity(backendRoot); var frontend = GitIdentity(frontendRoot);
            var root = key == "current" ? Root : Root + "-" + key;
            if (Directory.Exists(root) || File.Exists(root)) throw new IOException("Cutover deployment root must be create-new.");
            var inputs = new Web6CandidateInputs(_plan.CurrentCandidateId, backend, frontend, SqlSchemaDefinitionFingerprint.Schema13.ToLowerInvariant(),
                old ? _plan.OldOrigin : _plan.CurrentOrigin, CertificateHash(Required("MYDMSVN_WEB_TEST_TLS_THUMBPRINT")), "Web6CutoverPreflight", 600, 60, "web6-cutover-live.spec.ts");
            // Current-host variants use Task 2's protected file/audit admission. Exact12 uses PR95 plus its separately pinned schema/receipt.
            var deployment = new Deployment { Root = root, Inputs = inputs, Old = old, Source = published };
            _deployments.Add(key, deployment);
            if (!old) deployment.Manifest = Web6CandidateManifest.CreateNew(root, inputs);
            else { using var owner = WindowsIdentity.GetCurrent(); Directory.CreateDirectory(root, SqlTestPrivateRecord.DirectoryAcl(owner.User!)); }
            var host = Path.Combine(root, "host"); Directory.CreateDirectory(host);
            CopyTree(published, host, omitWwwroot: true);
            CopyTree(dist, Path.Combine(host, "wwwroot"), omitWwwroot: false);
            // Compare independently pinned source hashes after the copies, before sealing the composites.
            if (Web6CandidateFileSet.MeasureTree(published) != (old ? _plan.OldArtifactHash : _plan.CurrentArtifactHash) ||
                Web6CandidateFileSet.MeasureTree(dist) != Web6CandidateFileSet.MeasureTree(Path.Combine(host, "wwwroot")) ||
                (cached && Web6CandidateFileSet.MeasureTree(dist) != _plan.FrontendArtifactHash))
                throw new IOException("Cutover composition source changed while copying.");
            deployment.Host = host; deployment.Hash = Web6CandidateFileSet.MeasureTree(host);
            deployment.Configuration = CreateConfiguration(root, host);
            if (old)
            {
                var xml = new System.Xml.XmlDocument(); xml.Load(deployment.Configuration);
                foreach (System.Xml.XmlElement binding in xml.SelectNodes("/configuration/system.applicationHost/sites/site/bindings/binding")!)
                    binding.SetAttribute("bindingInformation", binding.GetAttribute("protocol") == "https" ? "*:44322:localhost" : "*:18082:localhost");
                xml.Save(deployment.Configuration);
            }
            deployment.ConfigHash = HashText(File.ReadAllText(deployment.Configuration));
            if (!old)
            {
                using var identity = WindowsIdentity.GetCurrent();
                using (new FileStream(deployment.Manifest!.AuditPath, FileMode.CreateNew, FileSystemRights.FullControl, FileShare.Read,
                    4096, FileOptions.WriteThrough, SqlTestPrivateRecord.FileAcl(identity.User!))) { }
                var locks = Directory.GetFiles(backendRoot, "packages.lock.json", SearchOption.AllDirectories)
                    .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or ".git"))
                    .Select(path => path.Substring(backendRoot.Length + 1)).ToArray();
                deployment.Manifest.SealArtifacts(new Web6CandidateFileSet(Path.Combine(host, "bin", "MyDmsVn.Server.HttpHost.dll"),
                    Path.Combine(host, "Web.config"), deployment.Configuration, backendRoot, locks, Path.Combine(frontendRoot, "package-lock.json"),
                    Path.Combine(backendRoot, "docs/contracts/web4-v1"), Path.Combine(backendRoot, "docs/contracts/web5-resolution-v1"),
                    Path.Combine(backendRoot, "docs/contracts/web6-resend-eligibility-v1"), dist, Path.Combine(host, "wwwroot")), _plan.CurrentDatabaseReceipt);
            }
        }

        private static void CopyTree(string source, string target, bool omitWwwroot)
        {
            _ = Web6CandidateFileSet.MeasureTree(source); // refuses links/reparse points before copying
            Directory.CreateDirectory(target);
            foreach (var path in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = path.Substring(source.TrimEnd(Path.DirectorySeparatorChar).Length + 1);
                if (omitWwwroot && relative.StartsWith("wwwroot" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
                var destination = Path.Combine(target, relative); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None); input.CopyTo(output);
            }
        }

        public async Task<Web6CutoverAdmission> StartAndObserve(string browser, Web6CutoverPair pair, CancellationToken token)
        {
            _session = new Web6CutoverSession(_plan, this);
            _coordinator = new Web6CandidateCutoverCoordinator(_plan);
            await _session.Begin();
            return _admission ?? throw new IOException("Cutover host admission is absent.");
        }

        public async Task Switch(Web6CutoverPair pair, bool cached, Guid epoch)
        {
            await DrainHost();
            if (pair == Web6CutoverPair.Current && epoch != _plan.CurrentEpoch)
                throw new InvalidOperationException("WEB6 cutover BLOCKED: restore requires a separately owned restored allocation, protected receipt and clone admission; current allocation must never be rewound or relabeled.");
            var current = pair == Web6CutoverPair.Current;
            var deployment = _deployments[current ? cached ? "cached" : "current" : "old"];
            var database = current ? _current : _old; var login = current ? _currentLogin : _oldLogin;
            await VerifyDatabase(database, current ? _currentControl : _oldControl, current, CancellationToken.None);
            var expectedSource = current ? _plan.CurrentArtifactHash : _plan.OldArtifactHash;
            if (Web6CandidateFileSet.MeasureTree(deployment.Source) != expectedSource || Web6CandidateFileSet.MeasureTree(deployment.Host) != deployment.Hash ||
                HashText(File.ReadAllText(deployment.Configuration)) != deployment.ConfigHash) throw new IOException("Cutover deployment/configuration drift.");
            using (var sql = new SqlConnection(database.ConnectionString))
                if (current && await sql.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;") != epoch)
                    throw new IOException("Cutover current epoch changed; refusing rewind.");
            var origin = (current ? _plan.CurrentOrigin : _plan.OldOrigin).GetLeftPart(UriPartial.Authority);
            var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IIS Express/iisexpress.exe"),
                "/config:\"" + deployment.Configuration + "\" /site:Web2Smoke /systray:false")
            { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var name in start.EnvironmentVariables.Keys.Cast<string>().Where(name => name.StartsWith("MYDMSVN_", StringComparison.OrdinalIgnoreCase)).ToArray()) start.EnvironmentVariables.Remove(name);
            start.EnvironmentVariables["MYDMSVN_WEB_ORIGIN"] = origin;
            start.EnvironmentVariables["MYDMSVN_WEB_OPERATION_MODE"] = current ? "web5" : "web4";
            start.EnvironmentVariables["MYDMSVN_WEB_JWT_ISSUER"] = origin;
            start.EnvironmentVariables["MYDMSVN_WEB_JWT_AUDIENCE"] = "web-smoke";
            start.EnvironmentVariables["MYDMSVN_WEB_JWT_ACTIVE_KID"] = "smoke";
            start.EnvironmentVariables["MYDMSVN_WEB_JWT_KEYS"] = "smoke:" + Convert.ToBase64String(_jwt);
            start.EnvironmentVariables["MYDMSVN_WEB_SQL_CONNECTION_STRING"] = login.ConnectionString(database.DatabaseName);
            start.EnvironmentVariables["MYDMSVN_WEB_SUPERUSER_USERNAME"] = "web.smoke";
            if (current)
            {
                start.EnvironmentVariables["MYDMSVN_WEB6_RESEND_ELIGIBILITY"] = "receipt-v1";
                start.EnvironmentVariables["MYDMSVN_WEB6_CANDIDATE_ID"] = _plan.CurrentCandidateId;
                start.EnvironmentVariables["MYDMSVN_WEB6_CANDIDATE_ROOT"] = deployment.Root;
                start.EnvironmentVariables["MYDMSVN_WEB_SECURITY_AUDIT_PATH"] = deployment.Manifest!.AuditPath;
            }
            _process = new Process { StartInfo = start }; _active = pair;
            _process.OutputDataReceived += (_, _) => { }; _process.ErrorDataReceived += (_, _) => { };
            _hostDrained = false;
            if (!_process.Start()) throw new IOException("Cutover IIS process did not start.");
            _process.BeginOutputReadLine(); _process.BeginErrorReadLine();
            _identity = Web5IisExpressHostAdmission.Capture(_process, deployment.Configuration, "Web2Smoke", new Uri(origin + "/"));
            await Web5IisExpressHostAdmission.WaitForAdmissionAsync(_identity, _process, TimeSpan.FromSeconds(45), CancellationToken.None);
            var queue = await Web5IisExpressHostAdmission.VerifyAsync(_identity, CancellationToken.None);
            if (current)
            {
                var inputs = deployment.Inputs;
                deployment.Manifest!.VerifyBeforeAdmission(_process.Id, _process.StartTime.ToUniversalTime(), queue.Name, () =>
                    new Web6CandidateObservation((inputs.Backend.Sha, inputs.Backend.Tree, true), (inputs.Frontend.Sha, inputs.Frontend.Tree, true),
                        inputs.Schema13Fingerprint, CertificateHash(Required("MYDMSVN_WEB_TEST_TLS_THUMBPRINT")), _plan.CurrentDatabaseReceipt, new[] { queue },
                        new Web6CandidateAuditLaunch(_process.Id, _process.StartTime.ToUniversalTime(), deployment.Root, deployment.Manifest.AuditPath, _plan.CurrentCandidateId)));
            }
            _admission = new Web6CutoverAdmission(new Uri(origin + "/"), current ? 13 : 12, current ? _plan.CurrentDatabaseReceipt : _plan.OldDatabaseReceipt,
                expectedSource, current ? epoch : Guid.Empty, 1, 1, true);
            _coordinator.Admit(pair, _admission);
            var deadline = DateTime.UtcNow.AddSeconds(45);
            while (DateTime.UtcNow < deadline && !_process.HasExited)
            {
                try { if ((await Request(origin + "/api/v1/health", Required("MYDMSVN_WEB_TEST_TLS_THUMBPRINT"))).Status == 200) return; }
                catch (WebException) { }
                await Task.Delay(250);
            }
            throw new IOException("Admitted cutover host did not become healthy.");
        }

        private async Task DrainHost()
        {
            if (_process == null) return;
            if (!_process.HasExited) { try { _process.StandardInput.WriteLine("q"); } catch (IOException) { } if (!_process.WaitForExit(10000)) _process.Kill(); }
            if (!_process.WaitForExit(10000) || _identity == null) throw new IOException("Cutover process identity/drain uncertain; quarantine allocations.");
            await Web5IisExpressHostAdmission.WaitForAbsenceAsync(_identity, TimeSpan.FromSeconds(20), CancellationToken.None);
            _process.Dispose(); _process = null; _identity = null; _admission = null; _hostDrained = true;
            if (_coordinator.HasActivePair) _coordinator.Drain(_active, true, true);
        }

        public async Task<Web6CutoverBrowserResult> RunAndValidate(string browser, Web6CutoverPair pair, CancellationToken token)
        {
            Web6CutoverBrowserResult? result = null;
            _control = WebRecoveryPilotControl.StartCutover(_plan.CurrentEpoch, _session!.Handle);
            try
            {
                _browserDrained = false;
                await RunBrowser(_current, Root, _customer, _warehouse, _employee, _product, _receiptProduct, _integrated,
                    true, true, false, true, _currentLogin.Name, null, _deployments["current"].Manifest, null, true, _plan, _control, browser,
                    (count, output) => result = new Web6CutoverBrowserResult((int)count["discovered"]!, (int)count["passed"]!, (int)count["failed"]!,
                        (int)count["skipped"]!, (int)count["retries"]!, ((JArray)output["checks"]!).Values<string>()!));
                _browserDrained = true;
                _session.VerifyComplete();
                _deployments["current"].Manifest!.RecordDisposition(browser == "chromium" ? Web6CandidateStage.Chromium : Web6CandidateStage.Edge, Web6CandidateOutcome.Passed);
                _browsers++;
                return result ?? throw new IOException("Cutover result missing.");
            }
            catch { _browserDrained = !GuiTestSafety.IsPoisoned; throw; }
            finally { await _control.Stop(_browserDrained); _control = null; }
        }

        public async Task VerifyOperation(Guid operation, bool committed)
        {
            using var sql = new SqlConnection(_current.ConnectionString); await sql.OpenAsync();
            var identity = await sql.QuerySingleAsync<string>("SELECT (SELECT ActorUserId,OperationId,DeploymentEpoch,ContractVersion,CodecVersion,OperationKind,RequestTargetId,RequestFingerprint,ResultDocumentId,Disposition FROM dbo.WebOperationJournal WHERE OperationId=@operation ORDER BY ActorUserId FOR JSON PATH,INCLUDE_NULL_VALUES);", new { operation });
            if (_operationIdentities.TryGetValue(operation, out var prior) && prior != identity) throw new IOException("Cutover operation identity changed across lifecycle.");
            if (committed)
            {
                if (await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal j JOIN dbo.Users u ON u.UserId=j.ActorUserId WHERE j.OperationId=@operation AND j.DeploymentEpoch=@epoch AND j.Disposition='COMMITTED' AND u.Username=N'integratedEditor';", new { operation, epoch = _plan.CurrentEpoch }) != 1)
                    throw new InvalidDataException("Cutover committed-unavailable requires an authentic committed operation; pre-dispatch absence is not COMMITTED.");
                await Web6CandidateOracle.VerifyOneEffectAsync(sql, operation, CancellationToken.None);
            }
            _operationIdentities[operation] = identity;
        }

        public async Task SetGrant(bool granted)
        {
            using var sql = new SqlConnection(_current.ConnectionString);
            if (await sql.ExecuteAsync("UPDATE p SET Granted=@granted FROM dbo.UserPermissions p JOIN dbo.Users u ON u.UserId=p.UserId WHERE u.Username=N'integratedEditor' AND p.PermissionKey=N'Sales.Receipts.Write';", new { granted }) != 1)
                throw new IOException("Cutover original write grant was not found.");
        }

        public async Task MakeCommittedUnavailable(Guid operation)
        {
            await VerifyOperation(operation, true);
            using var sql = new SqlConnection(_current.ConnectionString);
            // Expire only retained reply availability; do not manufacture business/journal outcomes.
            if (await sql.ExecuteAsync("UPDATE dbo.WebOperationJournal SET ResultExpiresAtUtc=CompletedAtUtc WHERE OperationId=@operation AND Disposition='COMMITTED';", new { operation }) != 1)
                throw new IOException("Committed cutover identity disappeared.");
        }

        private static async Task<string> LifecycleSnapshot(string connectionString, bool current)
        {
            var physical = await Web5NegativePilot.Snapshot(connectionString);
            using var sql = new SqlConnection(connectionString);
            var journal = await sql.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.WebOperationJournal ORDER BY ActorUserId,OperationId FOR JSON PATH,INCLUDE_NULL_VALUES);");
            var terminal = current ? await sql.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.WebOperationTerminalAudit ORDER BY ActorUserId,OperationId FOR JSON PATH,INCLUDE_NULL_VALUES);") : "";
            return physical + "|" + journal + "|" + terminal;
        }

        public async Task VerifyZeroMutation(Guid operation)
        {
            if (_currentSnapshot != await LifecycleSnapshot(_current.ConnectionString, true) || _oldSnapshot != await LifecycleSnapshot(_old.ConnectionString, false))
                throw new IOException("Cutover lifecycle changed business/stock/ledger/valuation/sequence state.");
            using var sql = new SqlConnection((_active == Web6CutoverPair.Current ? _current : _old).ConnectionString);
            if (await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE OperationId=@operation;", new { operation }) != 0)
                throw new IOException("Pre-dispatch cutover operation unexpectedly reached SQL.");
        }

        public async Task<Web6CutoverDrain> Drain(string browser, Web6CutoverPair pair, CancellationToken token)
        { await DrainHost(); return new Web6CutoverDrain(_hostDrained, _hostDrained); }
        public Task VerifyRestore(string browser, CancellationToken token) { _session!.VerifyComplete(); return Task.CompletedTask; }
        public Task Complete(CancellationToken token) { _complete = _browsers == 2; if (!_complete) throw new IOException("Both browsers are required."); return Task.CompletedTask; }

        internal async Task<bool> Cleanup()
        {
            var failures = new List<Exception>();
            try { if (_control != null) await _control.Stop(_browserDrained); } catch (Exception error) { failures.Add(error); }
            try { await DrainHost(); } catch (Exception error) { failures.Add(error); }
            if (_browserDrained && _hostDrained && failures.Count == 0 && !GuiTestSafety.IsPoisoned)
            {
                try { await _currentLogin.CleanupAsync(); } catch (Exception error) { failures.Add(error); }
                try { await _oldLogin.CleanupAsync(); } catch (Exception error) { failures.Add(error); }
            }
            if (failures.Count != 0) throw new AggregateException("Cutover owned host/pipe/login cleanup failed; allocations remain quarantined.", failures);
            return _browserDrained && _hostDrained && !GuiTestSafety.IsPoisoned;
        }

        internal void VerifyFinalDisposition()
        {
            Web6CutoverOracle.ValidateDisposition(_complete, _complete, _complete, _browserDrained, _hostDrained, true);
            _deployments["current"].Manifest!.RecordDisposition(Web6CandidateStage.Cutover, Web6CandidateOutcome.Passed);
            _deployments["current"].Manifest!.RecordDisposition(Web6CandidateStage.Cleanup, Web6CandidateOutcome.Passed);
        }
        internal void DisposeManifests()
        {
            foreach (var deployment in _deployments.Values) { deployment.Handles?.Dispose(); deployment.Manifest?.Dispose(); }
        }
        private sealed class Deployment
        {
            internal string Root = "", Host = "", Source = "", Configuration = "", Hash = "", ConfigHash = "";
            internal bool Old;
            internal Web6CandidateInputs Inputs = null!;
            internal Web6CandidateManifest? Manifest;
            internal Web6CandidateManifest.PrivateInput? Handles;
        }
    }
}
