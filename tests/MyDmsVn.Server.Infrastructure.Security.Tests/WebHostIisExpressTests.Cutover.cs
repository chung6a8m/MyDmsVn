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
    // All three allocation owners outlive this adapter, every browser and the final disposition.
    // No runtime endpoint, global environment mutation or unowned IIS process is used.
    private sealed class LiveCutoverOperations : IWeb6CandidateCutoverOperations, IWeb6CutoverRuntime
    {
        private readonly Web6CandidateCutoverPlan _plan;
        private readonly SqlTestDatabase _current, _old, _restored;
        private readonly SqlTestOwnershipManifest _currentControl, _oldControl, _restoredControl;
        private readonly OwnedLogin _currentLogin, _oldLogin, _restoredLogin;
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
        private JObject? _currentSnapshot, _oldSnapshot, _restoredSnapshot;
        private Web6CommittedSetup? _committedSetup;
        private byte[]? _committedCanonical;
        private string? _committedCanonicalHash;
        private bool _corruptionHealthy = true;
        private readonly List<Web6CommittedSetup> _verifiedSetups = new List<Web6CommittedSetup>();
        private int _restoredAdmissions, _currentReadmissions;
        private int _customer, _warehouse, _employee, _product, _receiptProduct;
        private IntegratedCatalog? _integrated;
        private Web6PrivateDirectoryLease? _browserLease;
        private string BrowserRoot => Root + "-browser";
        private string Root => Path.GetFullPath(Required("MYDMSVN_WEB6_CANDIDATE_ROOT"));
        private static string Required(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException("Missing " + name);

        internal LiveCutoverOperations(Web6CandidateCutoverPlan plan, SqlTestDatabase current, SqlTestDatabase old, SqlTestDatabase restored,
            SqlTestOwnershipManifest currentControl, SqlTestOwnershipManifest oldControl, SqlTestOwnershipManifest restoredControl)
        {
            _plan = plan; _current = current; _old = old; _currentControl = currentControl; _oldControl = oldControl; _restored = restored; _restoredControl = restoredControl;
            _currentLogin = new OwnedLogin(Required(SqlTestDatabase.ConnectionStringEnvironmentVariable));
            _oldLogin = new OwnedLogin(Required(SqlTestDatabase.ConnectionStringEnvironmentVariable));
            _restoredLogin = new OwnedLogin(Required(SqlTestDatabase.ConnectionStringEnvironmentVariable));
            _coordinator = new Web6CandidateCutoverCoordinator(plan);
            using var random = RandomNumberGenerator.Create(); random.GetBytes(_jwt);
        }

        public async Task<Web6CandidateCutoverPlan> VerifyAuthority(CancellationToken token)
        {
            GuiTestSafety.ThrowIfPoisoned();
            // Task 3's browser runner must consume the same immutable publication as this owner.
            if (Path.GetFullPath(Required("MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY")) != Path.GetFullPath(Required("MYDMSVN_WEB6_CUTOVER_CURRENT_PUBLISH_DIRECTORY")))
                throw new IOException("Current publication authorities differ.");
            if (GitIdentity(Required("MYDMSVN_WEB_TEST_BROWSER_DIRECTORY")).Sha != "9eb7d37e8fdb7ced5871ae9eb6be00f3e6e7163d")
                throw new IOException("Cutover frontend must use reviewed HEAD 9eb7d37.");
            await VerifyDatabase(_current, _currentControl, Web6CutoverPair.Current, token);
            await VerifyDatabase(_old, _oldControl, Web6CutoverPair.Old, token);
            await VerifyDatabase(_restored, _restoredControl, Web6CutoverPair.Restored, token);
            await _currentLogin.CreateAsync(); await _oldLogin.CreateAsync(); await _restoredLogin.CreateAsync();
            await Seed(_current, _currentLogin, Web6CutoverPair.Current); await Seed(_old, _oldLogin, Web6CutoverPair.Old);
            await Seed(_restored, _restoredLogin, Web6CutoverPair.Restored);
            _currentSnapshot = await LifecycleSnapshot(_current.ConnectionString, true);
            _oldSnapshot = await LifecycleSnapshot(_old.ConnectionString, false);
            _restoredSnapshot = await LifecycleSnapshot(_restored.ConnectionString, true);
            return _plan;
        }

        private async Task VerifyDatabase(SqlTestDatabase database, SqlTestOwnershipManifest control, Web6CutoverPair pair, CancellationToken token)
        {
            var current = pair != Web6CutoverPair.Old;
            using var sql = new SqlConnection(database.ConnectionString); await sql.OpenAsync(token);
            await database.VerifyOwnershipAsync(sql, token);
            var fingerprint = await SqlSchemaDefinitionFingerprint.ReadAsync(sql, token);
            Web6CutoverSqlApproval.RequireSchema(fingerprint, current ? SqlSchemaDefinitionFingerprint.Schema13 : SqlSchemaDefinitionFingerprint.Schema12);
            var receipt = Web6CutoverSqlOwnershipReceipt.FromProtectedRecord(control, control.VerifiedDigest(), fingerprint,
                pair == Web6CutoverPair.Restored ? "restored-allocation" : current ? "current-allocation" : "old-allocation").Digest;
            if (receipt != (pair == Web6CutoverPair.Restored ? _plan.RestoredDatabaseReceipt : current ? _plan.CurrentDatabaseReceipt : _plan.OldDatabaseReceipt)) throw new IOException("Cutover ownership receipt changed.");
        }

        private async Task Seed(SqlTestDatabase database, OwnedLogin login, Web6CutoverPair pair)
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
            if (pair != Web6CutoverPair.Old)
            {
                // Fresh allocation only, before the first writer admission.
                if (await sql.ExecuteAsync("UPDATE dbo.WebDeploymentState SET DeploymentEpoch=@epoch WHERE Id=1;", new { epoch = pair == Web6CutoverPair.Restored ? _plan.RestoredEpoch : _plan.CurrentEpoch }) != 1)
                    throw new IOException("Fresh current allocation epoch was not initialized.");
                if (pair == Web6CutoverPair.Current) { _customer = customer; _warehouse = warehouse; _employee = employee; _product = product; _receiptProduct = receipt; _integrated = integrated; }
            }
        }

        public Task SealProtectedManifest(JObject handles, CancellationToken token)
        {
            if (!JToken.DeepEquals(handles, _plan.FrontendHandles())) throw new IOException("Cutover handles changed.");
            var currentPublish = Path.GetFullPath(Required("MYDMSVN_WEB6_CUTOVER_CURRENT_PUBLISH_DIRECTORY"));
            var oldPublish = Path.GetFullPath(Required("MYDMSVN_WEB6_CUTOVER_OLD_PUBLISH_DIRECTORY"));
            if (Web6CandidateFileSet.MeasureTree(currentPublish) != _plan.CurrentArtifactHash || Web6CandidateFileSet.MeasureTree(oldPublish) != _plan.OldArtifactHash)
                throw new IOException("Published cutover artifacts changed.");
            _browserLease = Web6PrivateDirectoryLease.CreateNew(BrowserRoot);
            CreateDeployment("current", currentPublish, Path.Combine(Required("MYDMSVN_WEB_TEST_BROWSER_DIRECTORY"), "dist"), false, false);
            CreateDeployment("cached", currentPublish, Required("MYDMSVN_WEB6_CUTOVER_FRONTEND_DIST_DIRECTORY"), true, false);
            CreateDeployment("restored", currentPublish, Path.Combine(Required("MYDMSVN_WEB_TEST_BROWSER_DIRECTORY"), "dist"), false, false);
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
            else deployment.ControlLease = Web6PrivateDirectoryLease.CreateNew(root);
            // The sealed control root has an exact child allowlist; payload/config/logs live outside it.
            var runtimeRoot = root + "-runtime"; deployment.RuntimeLease = Web6PrivateDirectoryLease.CreateNew(runtimeRoot);
            var host = Path.Combine(runtimeRoot, "host"); Directory.CreateDirectory(host);
            CopyTree(published, host, omitWwwroot: true);
            CopyTree(dist, Path.Combine(host, "wwwroot"), omitWwwroot: false);
            // Compare independently pinned source hashes after the copies, before sealing the composites.
            if (Web6CandidateFileSet.MeasureTree(published) != (old ? _plan.OldArtifactHash : _plan.CurrentArtifactHash) ||
                Web6CandidateFileSet.MeasureTree(dist) != Web6CandidateFileSet.MeasureTree(Path.Combine(host, "wwwroot")) ||
                (cached && Web6CandidateFileSet.MeasureTree(dist) != _plan.FrontendArtifactHash))
                throw new IOException("Cutover composition source changed while copying.");
            deployment.Host = host; deployment.Hash = Web6CandidateFileSet.MeasureTree(host);
            deployment.Configuration = CreateConfiguration(runtimeRoot, host);
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
                    Path.Combine(backendRoot, "docs/contracts/web6-resend-eligibility-v1"), dist, Path.Combine(host, "wwwroot")), key == "restored" ? _plan.RestoredDatabaseReceipt : _plan.CurrentDatabaseReceipt);
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
            await VerifyLifecycle();
            _committedSetup = null; _committedCanonical = null; _committedCanonicalHash = null;
            _session = new Web6CutoverSession(_plan, this);
            await _session.Begin();
            if (browser == "edge") _currentReadmissions++;
            return _admission ?? throw new IOException("Cutover host admission is absent.");
        }

        public async Task Switch(Web6CutoverPair pair, bool cached, Guid epoch)
        {
            await DrainHost();
            var current = pair != Web6CutoverPair.Old;
            if ((pair == Web6CutoverPair.Current && epoch != _plan.CurrentEpoch) || (pair == Web6CutoverPair.Restored && epoch != _plan.RestoredEpoch))
                throw new IOException("Cutover allocation epoch cannot be relabeled or rewound.");
            var deployment = _deployments[pair == Web6CutoverPair.Restored ? "restored" : current ? cached ? "cached" : "current" : "old"];
            var database = pair == Web6CutoverPair.Restored ? _restored : current ? _current : _old;
            var login = pair == Web6CutoverPair.Restored ? _restoredLogin : current ? _currentLogin : _oldLogin;
            var receipt = pair == Web6CutoverPair.Restored ? _plan.RestoredDatabaseReceipt : current ? _plan.CurrentDatabaseReceipt : _plan.OldDatabaseReceipt;
            await VerifyDatabase(database, pair == Web6CutoverPair.Restored ? _restoredControl : current ? _currentControl : _oldControl, pair, CancellationToken.None);
            if (pair == Web6CutoverPair.Restored) await CopyRestoredSession();
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
                        inputs.Schema13Fingerprint, CertificateHash(Required("MYDMSVN_WEB_TEST_TLS_THUMBPRINT")), receipt, new[] { queue },
                        new Web6CandidateAuditLaunch(_process.Id, _process.StartTime.ToUniversalTime(), deployment.Root, deployment.Manifest.AuditPath, _plan.CurrentCandidateId)));
            }
            _admission = new Web6CutoverAdmission(new Uri(origin + "/"), current ? 13 : 12, receipt,
                expectedSource, current ? epoch : Guid.Empty, 1, 1, true);
            _coordinator.Admit(pair, _admission);
            if (pair == Web6CutoverPair.Restored) _restoredAdmissions++;
            var deadline = DateTime.UtcNow.AddSeconds(45);
            while (DateTime.UtcNow < deadline && !_process.HasExited)
            {
                try { if ((await Request(origin + "/api/v1/health", Required("MYDMSVN_WEB_TEST_TLS_THUMBPRINT"))).Status == 200) return; }
                catch (WebException) { }
                await Task.Delay(250);
            }
            throw new IOException("Admitted cutover host did not become healthy.");
        }

        private async Task CopyRestoredSession()
        {
            // Fixture metadata only: keep the browser's valid bearer session across the separate allocation.
            // No JWT, password or refresh-token/hash is copied, and the source allocation is read-only.
            if (!_hostDrained || _process != null) throw new IOException("Session copy requires verified host drain.");
            await VerifyDatabase(_current, _currentControl, Web6CutoverPair.Current, CancellationToken.None);
            await VerifyDatabase(_restored, _restoredControl, Web6CutoverPair.Restored, CancellationToken.None);
            using var source = new SqlConnection(_current.ConnectionString); await source.OpenAsync();
            using var target = new SqlConnection(_restored.ConnectionString); await target.OpenAsync();
            var actor = await source.QuerySingleAsync<int>("SELECT UserId FROM dbo.Users WHERE Username=N'integratedEditor' AND IsActive=1;");
            var targetActor = await target.QuerySingleAsync<int>("SELECT UserId FROM dbo.Users WHERE Username=N'integratedEditor' AND IsActive=1;");
            const string fields = "SessionId,UserId,CreatedAtUtc,AbsoluteExpiresAtUtc,IdleExpiresAtUtc,RevokedAtUtc";
            var rows = JArray.Parse(await source.QuerySingleAsync<string>("SELECT (SELECT TOP(1) " + fields + " FROM dbo.WebAuthSessions WHERE UserId=@actor AND RevokedAtUtc IS NULL AND IdleExpiresAtUtc>SYSUTCDATETIME() AND AbsoluteExpiresAtUtc>SYSUTCDATETIME() ORDER BY CreatedAtUtc DESC,SessionId FOR JSON PATH,INCLUDE_NULL_VALUES);", new { actor }));
            if (rows.Count != 1) throw new IOException("Restored fixture requires the current active actor session.");
            var row = (JObject)rows[0]; var session = Guid.Parse((string)row["SessionId"]!);
            async Task<JArray> ReadTarget() => JArray.Parse(await target.QuerySingleAsync<string>("SELECT (SELECT " + fields + " FROM dbo.WebAuthSessions WHERE SessionId=@session FOR JSON PATH,INCLUDE_NULL_VALUES);", new { session }));
            var existing = await ReadTarget();
            Web6CutoverSessionCopy.Verify(row, existing.Count == 0 ? null : (JObject)existing.Single(), targetActor, DateTime.UtcNow);
            if (existing.Count == 0)
            {
                if (await target.ExecuteAsync("INSERT dbo.WebAuthSessions(" + fields + ") VALUES(@session,@actor,@created,@absolute,@idle,NULL);",
                    new { session, actor, created = (DateTime)row["CreatedAtUtc"]!, absolute = (DateTime)row["AbsoluteExpiresAtUtc"]!, idle = (DateTime)row["IdleExpiresAtUtc"]! }) != 1)
                    throw new IOException("Restored fixture session copy failed.");
            }
            Web6CutoverSessionCopy.Verify(row, (JObject)(await ReadTarget()).Single(), targetActor, DateTime.UtcNow);
            await VerifyLifecycle();
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
                await RunBrowser(_current, BrowserRoot, _customer, _warehouse, _employee, _product, _receiptProduct, _integrated,
                    true, true, false, true, _currentLogin.Name, null, _deployments["current"].Manifest, null, true, _plan, _control, browser,
                    (count, output) => result = new Web6CutoverBrowserResult((int)count["discovered"]!, (int)count["passed"]!, (int)count["failed"]!,
                        (int)count["skipped"]!, (int)count["retries"]!, ((JArray)output["checks"]!).Values<string>()!));
                _browserDrained = true;
                _session.VerifyComplete();
                await VerifyLifecycle();
                Web6CutoverOracle.ValidateSetupEvidence(_session.CommittedSetup, _committedSetup, _restoredAdmissions == _browsers + 1);
                _verifiedSetups.Add(_committedSetup!);
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
                if (_committedSetup?.Operation == operation && _committedCanonical != null && HashBytes(_committedCanonical) != _committedCanonicalHash)
                    throw new IOException("Retained canonical cutover result changed.");
                await Web6CandidateOracle.VerifyOneEffectAsync(sql, operation, CancellationToken.None, _committedSetup?.Operation == operation ? _committedCanonical : null);
            }
            _operationIdentities[operation] = identity;
        }

        public async Task SetGrant(bool granted)
        {
            using var sql = new SqlConnection(_current.ConnectionString);
            if (await sql.ExecuteAsync("UPDATE p SET Granted=@granted FROM dbo.UserPermissions p JOIN dbo.Users u ON u.UserId=p.UserId WHERE u.Username=N'integratedEditor' AND p.PermissionKey=N'Sales.Receipts.Write';", new { granted }) != 1)
                throw new IOException("Cutover original write grant was not found.");
        }

        public async Task VerifyCommittedSetup(Web6CommittedSetup proof)
        {
            if (_committedSetup != null || _active != Web6CutoverPair.Current) throw new InvalidDataException("Unexpected cutover setup.");
            using var sql = new SqlConnection(_current.ConnectionString); await sql.OpenAsync();
            var document = await Web6CandidateOracle.VerifyCommittedSetupAsync(sql, proof, _plan.CurrentEpoch);
            var observed = await LifecycleSnapshot(_current.ConnectionString, true);
            Web6CutoverDelta.VerifySetup(_currentSnapshot!, observed, proof.Operation, document);
            Web6CutoverDelta.VerifyZero(_oldSnapshot!, await LifecycleSnapshot(_old.ConnectionString, false));
            Web6CutoverDelta.VerifyZero(_restoredSnapshot!, await LifecycleSnapshot(_restored.ConnectionString, true));
            _currentSnapshot = observed; _committedSetup = proof;
            _committedCanonical = Convert.FromBase64String((string)((JArray)observed["journal"]!).Single(row => string.Equals((string?)row["OperationId"], proof.Operation.ToString("D"), StringComparison.OrdinalIgnoreCase))["ResultBytes"]!);
            _committedCanonicalHash = HashBytes(_committedCanonical);
            await VerifyOperation(proof.Operation, true);
        }

        public async Task MakeCommittedUnavailable(Guid operation)
        {
            if (_committedSetup?.Operation != operation) throw new InvalidDataException("Committed setup proof is absent.");
            await VerifyLifecycle();
            await VerifyOperation(operation, true);
            await DrainHost();
            await VerifyDatabase(_current, _currentControl, Web6CutoverPair.Current, CancellationToken.None);
            _corruptionHealthy = false;
            Exception? corruptionFailure = null;
            try
            {
                using var sql = new SqlConnection(_current.ConnectionString); await sql.OpenAsync();
                using var transaction = sql.BeginTransaction();
                await Web6CutoverCorruption.Run(
                    () => sql.ExecuteAsync(new SqlTestCommand("DISABLE TRIGGER dbo.TR_WebOperationJournal_Immutable ON dbo.WebOperationJournal;", transaction: transaction)),
                    async () =>
                    {
                        var changed = await sql.ExecuteAsync(new SqlTestCommand("UPDATE dbo.WebOperationJournal SET ResultBytes=0x7B7D WHERE OperationId=@operation AND Disposition='COMMITTED' AND ResultBytes=@original;",
                            new { operation, original = _committedCanonical }, transaction: transaction));
                        if (changed != 1) throw new IOException("Authentic cutover result changed before fixture corruption.");
                    },
                    () => sql.ExecuteAsync(new SqlTestCommand("ENABLE TRIGGER dbo.TR_WebOperationJournal_Immutable ON dbo.WebOperationJournal;", transaction: transaction)),
                    async () =>
                    {
                        if (await sql.QuerySingleAsync<int>(new SqlTestCommand("SELECT COUNT(*) FROM sys.triggers WHERE object_id=OBJECT_ID(N'dbo.TR_WebOperationJournal_Immutable') AND is_disabled=0;", transaction: transaction)) != 1)
                            throw new IOException("Cutover journal trigger restoration failed.");
                    });
                transaction.Commit();
            }
            catch (Exception failure) { corruptionFailure = failure; throw; }
            finally
            {
                try
                {
                    // Transaction disposal rolls back any failed corruption; independently observe the enabled trigger afterward.
                    await VerifyDatabase(_current, _currentControl, Web6CutoverPair.Current, CancellationToken.None);
                    using var observedSql = new SqlConnection(_current.ConnectionString);
                    if (await observedSql.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.triggers WHERE object_id=OBJECT_ID(N'dbo.TR_WebOperationJournal_Immutable') AND is_disabled=0;") != 1)
                        throw new IOException("Cutover trigger state uncertain; quarantine allocation.");
                    _corruptionHealthy = true;
                }
                catch (Exception verification)
                {
                    if (corruptionFailure != null) throw new AggregateException("Cutover corruption and post-rollback verification failed.", corruptionFailure, verification);
                    throw;
                }
            }
            var observed = await LifecycleSnapshot(_current.ConnectionString, true);
            Web6CutoverDelta.VerifyUnavailable(_currentSnapshot!, observed, operation);
            _currentSnapshot = observed;
            await VerifyOperation(operation, true);
            Console.WriteLine("WEB6_CUTOVER_FIXTURE_CORRUPTION authenticated-committed-result-only");
            await Switch(Web6CutoverPair.Current, false, _plan.CurrentEpoch);
        }

        private static string HashBytes(byte[] value)
        { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(value)).Replace("-", "").ToLowerInvariant(); }

        private static async Task<JObject> LifecycleSnapshot(string connectionString, bool current)
        {
            using var sql = new SqlConnection(connectionString); await sql.OpenAsync();
            var snapshot = new JObject();
            foreach (var source in new[]
            {
                ("sales", "SalesReceipts ORDER BY SaleId"), ("saleLines", "SalesReceiptLines ORDER BY SaleLineId"),
                ("receipts", "GoodsReceipts ORDER BY ReceiptId"), ("receiptLines", "GoodsReceiptLines ORDER BY ReceiptLineId"),
                ("balances", "StockBalances ORDER BY WarehouseId,ProductId"), ("ledger", "StockLedger ORDER BY LedgerId"),
                ("valuation", "StockValuationEntries ORDER BY LedgerId"), ("journal", "WebOperationJournal ORDER BY ActorUserId,OperationId"),
            })
                snapshot[source.Item1] = JArray.Parse(await sql.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo." + source.Item2 + " FOR JSON PATH,INCLUDE_NULL_VALUES);"));
            snapshot["sequences"] = JArray.Parse(await sql.QuerySingleAsync<string>("SELECT (SELECT name,CONVERT(decimal(38,0),current_value) AS value,CONVERT(decimal(38,0),last_used_value) AS lastUsed,CONVERT(decimal(38,0),start_value) AS start FROM sys.sequences WHERE object_id IN (OBJECT_ID(N'dbo.SalesReceiptNumberSequence'),OBJECT_ID(N'dbo.GoodsReceiptNumberSequence')) ORDER BY name FOR JSON PATH,INCLUDE_NULL_VALUES);"));
            snapshot["terminal"] = current ? JArray.Parse(await sql.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo.WebOperationTerminalAudit ORDER BY ActorUserId,OperationId FOR JSON PATH,INCLUDE_NULL_VALUES);")) : new JArray();
            return snapshot;
        }

        public async Task VerifyLifecycle()
        {
            _browserLease?.Verify();
            foreach (var deployment in _deployments.Values) { deployment.ControlLease?.Verify(); deployment.RuntimeLease?.Verify(); }
            Web6CutoverDelta.VerifyZero(_currentSnapshot!, await LifecycleSnapshot(_current.ConnectionString, true));
            Web6CutoverDelta.VerifyZero(_oldSnapshot!, await LifecycleSnapshot(_old.ConnectionString, false));
            Web6CutoverDelta.VerifyZero(_restoredSnapshot!, await LifecycleSnapshot(_restored.ConnectionString, true));
            using var current = new SqlConnection(_current.ConnectionString);
            using var restored = new SqlConnection(_restored.ConnectionString);
            if (await current.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;") != _plan.CurrentEpoch ||
                await restored.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;") != _plan.RestoredEpoch)
                throw new IOException("Cutover allocation epoch changed.");
        }

        public async Task VerifyZeroMutation(Guid operation)
        {
            await VerifyLifecycle();
            if (_committedSetup?.Operation == operation) { await VerifyOperation(operation, true); return; }
            foreach (var database in new[] { _current, _old, _restored })
            {
                using var sql = new SqlConnection(database.ConnectionString);
                if (await sql.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE OperationId=@operation;", new { operation }) != 0)
                    throw new IOException("Pre-dispatch cutover operation unexpectedly reached SQL.");
            }
        }

        public async Task<Web6CutoverDrain> Drain(string browser, Web6CutoverPair pair, CancellationToken token)
        { await DrainHost(); await VerifyLifecycle(); return new Web6CutoverDrain(_hostDrained, _hostDrained); }
        public Task VerifyRestore(string browser, CancellationToken token) { _session!.VerifyComplete(); return Task.CompletedTask; }
        public async Task Complete(CancellationToken token)
        {
            await VerifyLifecycle();
            Web6CutoverOracle.ValidateAllocationDisposition(_plan, _verifiedSetups, _restoredAdmissions, _currentReadmissions);
            _complete = _browsers == 2; if (!_complete) throw new IOException("Both browsers are required.");
        }

        internal async Task<bool> Cleanup()
        {
            var failures = new List<Exception>();
            try { if (_control != null) await _control.Stop(_browserDrained); } catch (Exception error) { failures.Add(error); }
            try { await DrainHost(); } catch (Exception error) { failures.Add(error); }
            if (_corruptionHealthy && _browserDrained && _hostDrained && failures.Count == 0 && !GuiTestSafety.IsPoisoned)
            {
                try { await _currentLogin.CleanupAsync(); } catch (Exception error) { failures.Add(error); }
                try { await _oldLogin.CleanupAsync(); } catch (Exception error) { failures.Add(error); }
                try { await _restoredLogin.CleanupAsync(); } catch (Exception error) { failures.Add(error); }
            }
            if (failures.Count != 0) throw new AggregateException("Cutover owned host/pipe/login cleanup failed; allocations remain quarantined.", failures);
            return _corruptionHealthy && _browserDrained && _hostDrained && !GuiTestSafety.IsPoisoned;
        }

        internal void VerifyFinalDisposition()
        {
            Web6CutoverOracle.ValidateDisposition(_complete, _complete, _complete, _browserDrained, _hostDrained, true);
            Web6CutoverOracle.ValidateAllocationDisposition(_plan, _verifiedSetups, _restoredAdmissions, _currentReadmissions);
            _deployments["current"].Manifest!.RecordDisposition(Web6CandidateStage.Cutover, Web6CandidateOutcome.Passed);
            _deployments["current"].Manifest!.RecordDisposition(Web6CandidateStage.Cleanup, Web6CandidateOutcome.Passed);
        }
        internal void DisposeManifests()
        {
            var failures = new List<Exception>();
            void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
            foreach (var deployment in _deployments.Values)
            {
                Attempt(() => deployment.Handles?.Dispose());
                Attempt(() => deployment.Manifest?.Dispose());
            }
            var safe = _browserDrained && _hostDrained && _corruptionHealthy && !GuiTestSafety.IsPoisoned;
            foreach (var deployment in _deployments.Values)
            {
                if (safe)
                {
                    Attempt(() => deployment.RuntimeLease?.DeleteTreeVerified());
                    Attempt(() => deployment.ControlLease?.DeleteTreeVerified());
                }
                else { Attempt(() => deployment.RuntimeLease?.Dispose()); Attempt(() => deployment.ControlLease?.Dispose()); }
            }
            if (safe) Attempt(() => _browserLease?.DeleteTreeVerified()); else Attempt(() => _browserLease?.Dispose());
            if (failures.Count != 0) throw new AggregateException("Cutover private-root cleanup failed; uncertain roots are retained.", failures);
        }
        private sealed class Deployment
        {
            internal string Root = "", Host = "", Source = "", Configuration = "", Hash = "", ConfigHash = "";
            internal bool Old;
            internal Web6CandidateInputs Inputs = null!;
            internal Web6CandidateManifest? Manifest;
            internal Web6CandidateManifest.PrivateInput? Handles;
            internal Web6PrivateDirectoryLease? ControlLease;
            internal Web6PrivateDirectoryLease? RuntimeLease;
        }
    }
}
