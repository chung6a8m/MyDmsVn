using System;
using MyDmsVn.Web6.CandidateBootstrap;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Testing.Sql;
using MyDmsVn.Desktop.Tests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Explicit packaged-host selector. Requires a published Release host and the operator's
// existing localhost:44321 IIS Express TLS binding. Never changes machine TLS/IIS config.
public sealed partial class WebHostIisExpressTests
{
    [WebHostSqlFact, Trait("Category", "WebHostIntegration")]
    public Task Published_SystemWeb_host_warms_up_with_restricted_schema11_and_serves_safe_HTTP() => Run(false);

    [WebHostSqlFact, Trait("Category", "Web5OwnedPublishedHost")]
    public Task Published_WEB5_host_is_admitted_before_HTTP_and_absent_before_SQL_cleanup() =>
        Run(false, web5Pilot: true, admissionOnly: true);

    [WebBrowserSqlFact, Trait("Category", "WebBrowserIntegration")]
    public Task Published_React_browser_uses_real_HTTPS_SQL_and_atomic_stock() => Run(true);

    [WebBrowserSqlFact, Trait("Category", "WebBrowserIntegration")]
    public Task Published_React_Receipt_browser_uses_real_HTTPS_SQL_and_independent_receipt_stock() => Run(true, receiptPilot: true);

    [WebBrowserSqlFact, Trait("Category", "WebBrowserIntegration")]
    public Task Published_React_integrated_browser_preserves_same_key_QV_COGS_and_warehouse_isolation() => Run(true, receiptPilot: true, integratedPilot: true);

    [WebBrowserSqlFact, Trait("Category", "WebBrowserIntegration")]
    public Task Published_React_WEB4_recovery_preserves_journal_and_physical_stock() => Run(true, receiptPilot: true, integratedPilot: true, recoveryPilot: true);

    [WebBrowserSqlFact, Trait("Category", "WebBrowserIntegration")]
    public Task Published_React_WEB5_compatible_recovery_preserves_journal_and_physical_stock() => Run(true, receiptPilot: true, integratedPilot: true, recoveryPilot: true, web5Pilot: true);

    [WebOriginalFirstSqlFact, Trait("Category", "WebBrowserIntegration")]
    public Task Published_React_WEB5_original_first_fence_races() => Run(true, receiptPilot: true, integratedPilot: true, recoveryPilot: true, web5Pilot: true, originalFirstPilot: true);

    [WebAttendedSqlFact, Trait("Category", "WebAttendedFixture")]
    public Task WEB5_attended_local_fixture() => Run(true, integratedPilot: true, web5Pilot: true, attended: true);

    [Web6CandidateSqlFact, Trait("Category", "Web6PairedCandidate")]
    public Task Web6_paired_candidate_runs_six_manual_resends_in_real_browsers_and_SQL() =>
        Run(true, receiptPilot: true, integratedPilot: true, recoveryPilot: true, web5Pilot: true, web6Candidate: true);

    [Web6CutoverSqlFact, Trait("Category", "Web6CutoverPreflight")]
    public Task Web6_pinned_cutover_runs_protected_IIS_and_browser_lifecycle() =>
        RunCutoverCandidate();

    [Web6WorkloadSqlFact, Trait("Category", "Web6BoundedWorkload")]
    public Task Web6_paired_candidate_runs_bounded_workload_profiles() =>
        Run(true, receiptPilot: true, integratedPilot: true, recoveryPilot: true, web5Pilot: true,
            web6Candidate: true, web6Workload: true);

    private static async Task Run(bool browser, bool receiptPilot = false, bool integratedPilot = false, bool recoveryPilot = false,
        bool web5Pilot = false, bool attended = false, bool originalFirstPilot = false, bool admissionOnly = false,
        bool web6Candidate = false, bool web6Cutover = false, Web6CandidateCutoverPlan? cutoverPlan = null,
        bool web6Workload = false)
    {
        GuiTestSafety.ThrowIfPoisoned();
        Web6WorkloadTask4Receipt? workloadReceipt = null;
        if (web6Workload)
        {
            var workloadValues = Web6WorkloadAcceptanceGate.EnvironmentValues();
            Web6WorkloadAcceptanceGate.RequireLive(workloadValues);
            workloadReceipt = Web6WorkloadTask4Receipt.ReadProtected(workloadValues["MYDMSVN_WEB6_TASK4_ACCEPTANCE_PATH"]!,
                workloadValues["MYDMSVN_WEB6_TASK4_ACCEPTANCE_SHA256"]!);
        }
        if (web6Candidate) Web6CandidateAcceptanceGate.RequireLive(Web6CandidateAcceptanceGate.EnvironmentValues());
        using var bootstrap = web6Candidate ? Web6SelectorOutput.Open(web6Workload ? "workload" :
            "pair-" + Environment.GetEnvironmentVariable("MYDMSVN_WEB_PILOT_PROJECT")) : null;
        var bootstrapResources = new JArray();
        JArray? bootstrapProfiles = null;
        if (web6Cutover) Web6CutoverAcceptanceGate.RequireLive(Web6CutoverAcceptanceGate.EnvironmentValues());
        if (web6Cutover && (!web6Candidate || cutoverPlan == null)) throw new InvalidOperationException("WEB6 cutover requires the protected paired candidate fixture and exact handles.");
        if (web6Candidate && (!browser || !receiptPilot || !integratedPilot || !recoveryPilot || !web5Pilot || attended || originalFirstPilot || admissionOnly))
            throw new InvalidOperationException("WEB6 candidate requires the exact paired browser fixture.");
        if (browser && !attended && !web6Candidate) Assert.Contains(Environment.GetEnvironmentVariable("MYDMSVN_WEB_PILOT_PROJECT"), new[] { "chromium", "edge" });
        var published = Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY");
        var thumbprint = Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_TLS_THUMBPRINT");
        Assert.False(string.IsNullOrWhiteSpace(published));
        Assert.False(string.IsNullOrWhiteSpace(thumbprint));
        Assert.True(File.Exists(Path.Combine(published!, "Web.config")));
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IIS Express", "iisexpress.exe");
        Assert.True(File.Exists(executable));
        Assert.DoesNotContain(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners(), endpoint => endpoint.Port == 44321 || endpoint.Port == 18081);
        var attendedDirectory = attended ? Environment.GetEnvironmentVariable("MYDMSVN_WEB_ATTENDED_DIRECTORY") : null;
        if (attended && (string.IsNullOrWhiteSpace(attendedDirectory) || !Path.IsPathRooted(attendedDirectory) || !Directory.Exists(attendedDirectory)))
            throw new InvalidOperationException("Provide a private existing absolute attended parent directory.");
        var attendedPassword = attended ? "Pilot!" + Guid.NewGuid().ToString("N") : null;
        var admin = Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!;
        using var bootstrapSqlControl = bootstrap == null ? null : SqlTestOwnershipManifest.Create(bootstrap.Plan.Text("sqlControlParent"));
        var database = await SqlTestDatabase.CreateAsync(admin);
        string? bootstrapSqlReceipt = null;
        var login = new OwnedLogin(admin);
        Process? process = null;
        Web5IisExpressIdentity? hostIdentity = null;
        Web6CandidateManifest? candidateManifest = null;
        Web6CandidateInputs? candidateInputs = null;
        Web6CandidateHostMismatch? candidateMismatch = null;
        Web6PrivateDirectoryLease? runDirectoryLease = null;
        Web6PrivateDirectoryLease? workloadRootLease = null;
        string? admittedQueueName = null;
        string? candidateSqlReceipt = null;
        var failures = new List<Exception>();
        var drained = !web6Candidate;
        var runtimeRootDeleted = false;
        var runtimeCleanup = new Web6RuntimeRootCleanupState();
        var candidateBrowserValidated = false;
        var workloadValidated = false;
        int customer = 0, warehouse = 0, employee = 0, product = 0, receiptProduct = 0;
        IntegratedCatalog? integrated = null;
        async Task CleanupOwnedSql()
        {
            if (bootstrap != null)
            {
                using var owned = new SqlConnection(database.ConnectionString); await owned.OpenAsync();
                await database.VerifyOwnershipAsync(owned, CancellationToken.None);
                bootstrapSqlControl!.Verify();
            }
            await login.CleanupAsync();
            if (bootstrap != null) await database.DisposeIfQuiescentAsync(); else await database.DisposeAsync();
            if (bootstrap != null) await database.CompleteOwnershipManifestAsync(bootstrapSqlControl!, CancellationToken.None);
            using var verification = new SqlConnection(new SqlConnectionStringBuilder(admin) { InitialCatalog = "master", Pooling = false }.ConnectionString);
            await verification.OpenAsync();
            var remaining = await verification.QuerySingleAsync<int>("SELECT (CASE WHEN DB_ID(@database) IS NULL THEN 0 ELSE 1 END) + (CASE WHEN SUSER_ID(@login) IS NULL THEN 0 ELSE 1 END);", new { database = database.DatabaseName, login = login.Name });
            if (remaining != 0) throw new InvalidOperationException("Owned fixture resources remain after cleanup.");
            Console.WriteLine("WEB5_OWNED_SQL_CLEANUP verified=1 remaining=0");
        }
        try
        {
            if (bootstrap != null)
            {
                await database.WriteOwnershipManifestAsync(bootstrapSqlControl!, CancellationToken.None);
                var allocation = await Web6BootstrapSelectorEvidence.Sql(bootstrap, database, bootstrapSqlControl!);
                bootstrapSqlReceipt = allocation.Hash;
                bootstrapResources.Add(Web6BootstrapSelectorEvidence.Resource("database", database.DatabaseName, allocation.Hash, allocation.Path));
                bootstrapResources.Add(Web6BootstrapSelectorEvidence.Resource("login", login.Name, allocation.Hash, allocation.Path));
            }
            var migration = web5Pilot ? new DatabaseMigrationRunner().MigrateWeb5(database.ConnectionString) : recoveryPilot ? new DatabaseMigrationRunner().MigrateWeb(database.ConnectionString) : FrozenWeb11Migration.Install(database.ConnectionString);
            Assert.True(migration.Successful, "Fresh web migration must succeed.");
            await login.CreateAsync();
            using (var connection = new SqlConnection(database.ConnectionString))
            {
                await connection.OpenAsync();
                await connection.ExecuteAsync("CREATE USER [" + login.Name + "] FOR LOGIN [" + login.Name + "]; CREATE USER WebSmokeMigration WITHOUT LOGIN;");
                await connection.ExecuteAsync(File.ReadAllText(FindRootFile("deployment/sql/Provision-MyDmsVnWebPrincipals.sql"))
                    .Replace("$(WebRuntimeUser)", login.Name).Replace("$(MigrationUser)", "WebSmokeMigration"));
                await connection.ExecuteAsync("INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) VALUES(N'web.smoke',N'WEB.SMOKE',N'Web smoke',N'Local',@hash,N'',N'BCrypt'); " +
                    "INSERT dbo.SystemSecuritySettings(Id,SuperuserUserId,BoundNormalizedUsername) SELECT 1,UserId,N'WEB.SMOKE' FROM dbo.Users WHERE NormalizedUsername=N'WEB.SMOKE';",
                    new { hash = new BcryptPasswordHasher().Hash(attended ? "Unused!" + Guid.NewGuid().ToString("N") : "IisSmoke_test-only-password_2026!") });
                customer = await connection.QuerySingleAsync<int>("INSERT dbo.Customers(Code,Name) OUTPUT INSERTED.CustomerId VALUES(N'PILOT-C',N'Pilot Customer');");
                warehouse = await connection.QuerySingleAsync<int>("INSERT dbo.Warehouses(Code,Name) OUTPUT INSERTED.WarehouseId VALUES(N'PILOT-W',N'Pilot Warehouse');");
                employee = await connection.QuerySingleAsync<int>("INSERT dbo.Employees(Code,Name) OUTPUT INSERTED.EmployeeId VALUES(N'PILOT-E',N'Pilot Employee');");
                product = await connection.QuerySingleAsync<int>("INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'PILOT-P',N'Pilot Product',N'EA');");
                await connection.ExecuteAsync("INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@warehouse,@product,@quantity,@value);", new { warehouse, product, quantity = browser ? 1000 : 20, value = browser ? 15000 : 300 });
                if (browser && !attended) await SeedBrowserActors(connection);
                if (receiptPilot)
                {
                    receiptProduct = await connection.QuerySingleAsync<int>("INSERT dbo.Products(Code,Name,Unit) OUTPUT INSERTED.ProductId VALUES(N'WEB3-RP',N'Web3 Receipt Product',N'EA');");
                    await SeedReceiptActors(connection);
                }
                if (integratedPilot) integrated = await SeedIntegratedActorsAndCatalog(connection, attendedPassword);
            }
            var runDirectory = web6Candidate
                ? Path.GetFullPath(Environment.GetEnvironmentVariable("MYDMSVN_WEB6_CANDIDATE_ROOT")!) + "-host-runtime"
                : Path.Combine(Path.GetDirectoryName(published!)!, "iis-" + Guid.NewGuid().ToString("N"));
            if (web6Candidate) runDirectoryLease = Web6PrivateDirectoryLease.CreateNew(runDirectory);
            else Directory.CreateDirectory(runDirectory);
            var configuration = CreateConfiguration(runDirectory, published!);
            if (web6Candidate)
            {
                var backendRoot = Path.GetDirectoryName(FindRootFile("CleanArchitecture.sln"))!;
                var frontendRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_BROWSER_DIRECTORY")!);
                var backendIdentity = GitIdentity(backendRoot);
                var frontendIdentity = GitIdentity(frontendRoot);
                if (web6Workload && (workloadReceipt!.CandidateId != Environment.GetEnvironmentVariable("MYDMSVN_WEB6_CANDIDATE_ID") ||
                    workloadReceipt.BackendSha != backendIdentity.Sha || workloadReceipt.FrontendSha != frontendIdentity.Sha ||
                    workloadReceipt.FrontendDistHash != Web6CandidateFileSet.MeasureTree(Path.Combine(frontendRoot, "dist"))))
                    throw new InvalidOperationException("Accepted Task4 receipt does not bind this workload source/artifact candidate.");
                var certificateHash = CertificateHash(Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_TLS_THUMBPRINT")!);
                candidateInputs = new Web6CandidateInputs(Environment.GetEnvironmentVariable("MYDMSVN_WEB6_CANDIDATE_ID")!,
                    backendIdentity, frontendIdentity, SqlSchemaDefinitionFingerprint.Schema13.ToLowerInvariant(),
                    new Uri("https://localhost:44321/"), certificateHash, web6Workload ? "Web6BoundedWorkload" : "Web6PairedCandidate", 600, 60,
                    browsers: bootstrap != null && !web6Workload ? bootstrap.Gate.Substring(5) : "chromium,edge");
                var candidateRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("MYDMSVN_WEB6_CANDIDATE_ROOT")!);
                if (Directory.Exists(candidateRoot) || File.Exists(candidateRoot)) throw new InvalidOperationException("WEB6 candidate root must be create-new.");
                candidateManifest = Web6CandidateManifest.CreateNew(candidateRoot, candidateInputs);
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                using (new FileStream(candidateManifest.AuditPath, FileMode.CreateNew, FileSystemRights.FullControl, FileShare.Read,
                    4096, FileOptions.WriteThrough, SqlTestPrivateRecord.FileAcl(identity.User!))) { }
                using (var ownership = new SqlConnection(database.ConnectionString))
                {
                    await ownership.OpenAsync();
                    await database.VerifyOwnershipAsync(ownership, CancellationToken.None);
                    Assert.Equal(SqlSchemaDefinitionFingerprint.Schema13,
                        await SqlSchemaDefinitionFingerprint.ReadAsync(ownership, CancellationToken.None));
                    var server = await ownership.QuerySingleAsync<string>("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'));");
                    candidateSqlReceipt = HashText(database.DatabaseName + "\n" + server + "\n" + SqlSchemaDefinitionFingerprint.Schema13);
                }
                var backendLocks = Directory.GetFiles(backendRoot, "packages.lock.json", SearchOption.AllDirectories)
                    .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or ".git"))
                    .Select(path => path.Substring(backendRoot.Length + 1)).ToArray();
                var files = new Web6CandidateFileSet(Path.Combine(published!, "bin", "MyDmsVn.Server.HttpHost.dll"),
                    Path.Combine(published!, "Web.config"), configuration, backendRoot, backendLocks,
                    Path.Combine(frontendRoot, "package-lock.json"), Path.Combine(backendRoot, "docs", "contracts", "web4-v1"),
                    Path.Combine(backendRoot, "docs", "contracts", "web5-resolution-v1"),
                    Path.Combine(backendRoot, "docs", "contracts", "web6-resend-eligibility-v1"),
                    Path.Combine(frontendRoot, "dist"), Path.Combine(published!, "wwwroot"));
                candidateManifest.SealArtifacts(files, candidateSqlReceipt);
            }
            var start = new ProcessStartInfo(executable, "/config:\"" + configuration + "\" /site:Web2Smoke /systray:false")
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            };
            var key = new byte[32]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(key);
            var inherited = new List<string>();
            foreach (string name in start.EnvironmentVariables.Keys) if (name.StartsWith("MYDMSVN_", StringComparison.OrdinalIgnoreCase)) inherited.Add(name);
            foreach (var name in inherited) start.EnvironmentVariables.Remove(name);
            start.EnvironmentVariables["MYDMSVN_WEB_ORIGIN"] = "https://localhost:44321";
            start.EnvironmentVariables["MYDMSVN_WEB_OPERATION_MODE"] = web5Pilot ? "web5" : recoveryPilot ? "web4" : "legacy-web11";
            start.EnvironmentVariables["MYDMSVN_WEB_JWT_ISSUER"] = "https://localhost:44321";
            start.EnvironmentVariables["MYDMSVN_WEB_JWT_AUDIENCE"] = "web-smoke";
            start.EnvironmentVariables["MYDMSVN_WEB_JWT_ACTIVE_KID"] = "smoke";
            start.EnvironmentVariables["MYDMSVN_WEB_JWT_KEYS"] = "smoke:" + Convert.ToBase64String(key);
            start.EnvironmentVariables["MYDMSVN_WEB_SQL_CONNECTION_STRING"] = login.ConnectionString(database.DatabaseName);
            start.EnvironmentVariables["MYDMSVN_WEB_SUPERUSER_USERNAME"] = "web.smoke";
            if (web6Candidate)
            {
                var candidateRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("MYDMSVN_WEB6_CANDIDATE_ROOT")!);
                var auditPath = Path.Combine(candidateRoot, "security-audit.log");
                Assert.True(Directory.Exists(candidateRoot) && File.Exists(auditPath), "WEB6 candidate audit storage must be pre-provisioned.");
                start.EnvironmentVariables["MYDMSVN_WEB6_RESEND_ELIGIBILITY"] = "receipt-v1";
                start.EnvironmentVariables["MYDMSVN_WEB6_CANDIDATE_ID"] = Environment.GetEnvironmentVariable("MYDMSVN_WEB6_CANDIDATE_ID")!;
                start.EnvironmentVariables["MYDMSVN_WEB6_CANDIDATE_ROOT"] = candidateRoot;
                start.EnvironmentVariables["MYDMSVN_WEB_SECURITY_AUDIT_PATH"] = auditPath;
            }
            async Task SwitchOwnedHost(string candidateId)
            {
                if (process != null)
                {
                    var prior = process; var priorIdentity = hostIdentity;
                    if (!prior.HasExited) { try { prior.StandardInput.WriteLine("q"); } catch (IOException) { } if (!prior.WaitForExit(10000)) prior.Kill(); }
                    if (!prior.WaitForExit(10000) || priorIdentity == null) throw new InvalidOperationException("Owned candidate host did not stop for configuration switch.");
                    await Web5IisExpressHostAdmission.WaitForAbsenceAsync(priorIdentity, TimeSpan.FromSeconds(20), CancellationToken.None);
                    prior.Dispose(); process = null; hostIdentity = null;
                }
                if (web6Candidate) start.EnvironmentVariables["MYDMSVN_WEB6_CANDIDATE_ID"] = candidateId;
                process = new Process { StartInfo = start };
                process.OutputDataReceived += (_, _) => { }; process.ErrorDataReceived += (_, _) => { };
                runtimeCleanup.MarkStartAttempted();
                Assert.True(process.Start()); drained = false;
                process.BeginOutputReadLine(); process.BeginErrorReadLine();
                hostIdentity = Web5IisExpressHostAdmission.Capture(process, configuration, "Web2Smoke", new Uri("https://localhost:44321/"));
                await Web5IisExpressHostAdmission.WaitForAdmissionAsync(hostIdentity, process, TimeSpan.FromSeconds(45), CancellationToken.None);
                var queue = await Web5IisExpressHostAdmission.VerifyAsync(hostIdentity, CancellationToken.None);
                admittedQueueName = queue.Name;
                if (bootstrap != null) Web6BootstrapSelectorEvidence.Host(bootstrapResources, hostIdentity, queue.Name);
                if (web6Candidate && candidateId == candidateInputs!.CandidateId)
                    candidateManifest!.VerifyBeforeAdmission(process.Id, process.StartTime.ToUniversalTime(), queue.Name, () =>
                        new Web6CandidateObservation((candidateInputs.Backend.Sha, candidateInputs.Backend.Tree, true),
                            (candidateInputs.Frontend.Sha, candidateInputs.Frontend.Tree, true), SqlSchemaDefinitionFingerprint.Schema13.ToLowerInvariant(),
                            candidateInputs.CertificateHash, candidateSqlReceipt, new[] { queue }, new Web6CandidateAuditLaunch(process.Id,
                                process.StartTime.ToUniversalTime(), Path.GetFullPath(Environment.GetEnvironmentVariable("MYDMSVN_WEB6_CANDIDATE_ROOT")!),
                                candidateManifest.AuditPath, candidateInputs.CandidateId)));
            }
            if (web6Candidate) candidateMismatch = new Web6CandidateHostMismatch(candidateInputs!.CandidateId, SwitchOwnedHost);
            await SwitchOwnedHost(web6Candidate ? candidateInputs!.CandidateId : "");
            var admittedProcess = process ?? throw new InvalidOperationException("Owned host process was not captured.");
            Reply? health = null;
            var deadline = DateTime.UtcNow.AddSeconds(45);
            while (DateTime.UtcNow < deadline && !admittedProcess.HasExited)
            {
                try { health = await Request("https://localhost:44321/api/v1/health", thumbprint!); if (health.Status == 200) break; }
                catch (WebException) { }
                await Task.Delay(250);
            }
            Assert.NotNull(health);
            Assert.False(admittedProcess.HasExited);
            Assert.Equal(200, health!.Status);
            Assert.Contains("application/json", health.ContentType);
            Assert.Equal("{\"data\":{\"status\":\"ok\"}}", health.Body);
            Assert.Equal("iis-smoke", health.Correlation);
            if (admissionOnly)
                Assert.True(web5Pilot && !browser, "Admission-only packaged proof is restricted to the SQL-backed WEB5 host.");
            else if (browser)
            {
                foreach (var type in new[] { (Extension: "*.js", Mime: "text/javascript"), (Extension: "*.css", Mime: "text/css"), (Extension: "*.woff2", Mime: "font/woff2") })
                {
                    var file = Directory.GetFiles(Path.Combine(published!, "wwwroot", "assets"), type.Extension).First();
                    var asset = await Request("https://localhost:44321/assets/" + Path.GetFileName(file), thumbprint!);
                    Assert.True(asset.Status == 200, "Published asset failed: extension=" + type.Extension + "; status=" + asset.Status + "; contentType=" + asset.ContentType);
                    Assert.Contains(type.Mime, asset.ContentType);
                    Assert.Equal(File.ReadAllBytes(file), asset.Bytes);
                }
                foreach (var deniedPath in new[] { "/assets/missing.js", "/Web.config", "/bin/MyDmsVn.Server.HttpHost.dll" })
                    Assert.Equal(404, (await Request("https://localhost:44321" + deniedPath, thumbprint!)).Status);
                Assert.Equal(401, (await Request("https://localhost:44321/api/v1/missing", thumbprint!)).Status);
                if (web6Workload)
                {
                    workloadRootLease = Web6PrivateDirectoryLease.CreateNew(
                        Path.GetFullPath(Environment.GetEnvironmentVariable("MYDMSVN_WEB6_WORKLOAD_ROOT")!));
                    await RunCandidateWorkload(database, candidateManifest!, thumbprint!, customer, warehouse, employee,
                        product, receiptProduct, workloadRootLease,
                        () => process ?? throw new InvalidOperationException("Workload host is absent."),
                        () => hostIdentity ?? throw new InvalidOperationException("Workload host identity is absent."),
                        admittedQueueName ?? throw new InvalidOperationException("Workload queue identity is absent."),
                        value => bootstrapProfiles = value);
                    workloadValidated = true;
                }
                else if (attended)
                    await Web5AttendedLease.Wait(process, attendedDirectory!, attendedPassword!, TimeSpan.FromMinutes(30), CancellationToken.None);
                else
                    await RunBrowser(database, runDirectory, customer, warehouse, employee, product, receiptProduct, integrated,
                        recoveryPilot, web5Pilot, originalFirstPilot, web6Candidate, login.Name,
                        web6Candidate ? () => candidateBrowserValidated = true : null, candidateManifest, candidateMismatch, web6Cutover, cutoverPlan);
            }
            else
            {
                var denied = await Request("https://localhost:44321/api/v1/private", thumbprint!);
                Assert.Equal(401, denied.Status); Assert.Null(denied.Location);
                Assert.Contains("application/json", denied.ContentType);
                Assert.Contains("Auth.Unauthorized", denied.Body);
                var invalid = await Request("https://localhost:44321/api/v1/health", thumbprint!, "Bearer invalid-token");
                Assert.Equal(401, invalid.Status); Assert.DoesNotContain("invalid-token", invalid.Body);
                var credentials = JsonConvert.SerializeObject(new { username = "web.smoke", password = "IisSmoke_test-only-password_2026!" });
                var missingOrigin = await Request("https://localhost:44321/api/v1/auth/login", thumbprint!, method: "POST", body: credentials, csrf: true);
                Assert.Equal(403, missingOrigin.Status);
                var signedIn = await Request("https://localhost:44321/api/v1/auth/login", thumbprint!, origin: "https://localhost:44321", method: "POST", body: credentials, csrf: true);
                Assert.Equal(200, signedIn.Status);
                AssertRefreshCookie(signedIn, cleared: false);
                var bearer = "Bearer " + (string)JObject.Parse(signedIn.Body)["data"]!["accessToken"]!;
                var me = await Request("https://localhost:44321/api/v1/auth/me", thumbprint!, bearer);
                Assert.Equal(200, me.Status);
                Assert.Equal("web.smoke", (string?)JObject.Parse(me.Body)["data"]!["username"]);
                var lookup = await Request("https://localhost:44321/api/v1/catalog/products/lookup?limit=25", thumbprint!, bearer);
                Assert.Equal(200, lookup.Status);
                Assert.Contains(JObject.Parse(lookup.Body)["data"]!, row => (int)row["id"]! == product);
                var saleBody = new JObject
                {
                    ["saleDate"] = "2024-02-29", ["customerId"] = customer, ["warehouseId"] = warehouse, ["employeeId"] = employee,
                    ["lines"] = new JArray(
                        new JObject { ["saleLineId"] = "0", ["productId"] = product, ["qty"] = "3.0000", ["unitPrice"] = "25.0000", ["isGift"] = false },
                        new JObject { ["saleLineId"] = "0", ["productId"] = product, ["qty"] = "2.0000", ["unitPrice"] = "25.0000", ["isGift"] = true }),
                };
                var created = await Request("https://localhost:44321/api/v1/sales-receipts", thumbprint!, bearer, "https://localhost:44321", "POST", saleBody.ToString(Formatting.None), csrf: true);
                Assert.Equal(200, created.Status);
                var sale = JObject.Parse(created.Body)["data"]!;
                Assert.Equal(JTokenType.String, sale["saleId"]!.Type);
                Assert.Equal(JTokenType.String, sale["lines"]![0]!["saleLineId"]!.Type);
                Assert.Equal(JTokenType.String, sale["totalAmount"]!.Type);
                var saleUrl = "https://localhost:44321/api/v1/sales-receipts/" + (string)sale["saleId"]!;
                var fetched = await Request(saleUrl, thumbprint!, bearer);
                Assert.Equal(200, fetched.Status);
                Assert.True(JToken.DeepEquals(NormalizeDraftDecimals(sale), NormalizeDraftDecimals(JObject.Parse(fetched.Body)["data"]!)));
                saleBody["expectedVersion"] = sale["version"]!.DeepClone();
                saleBody["note"] = "System.Web smoke update";
                for (var index = 0; index < 2; index++) saleBody["lines"]![index]!["saleLineId"] = sale["lines"]![index]!["saleLineId"]!.DeepClone();
                var updated = await Request(saleUrl + "/draft", thumbprint!, bearer, "https://localhost:44321", "PUT", saleBody.ToString(Formatting.None), csrf: true);
                Assert.True(updated.Status == 200, "Sales PUT failed: status=" + updated.Status + "; contentType=" + updated.ContentType + "; correlation=" + updated.Correlation + "; error=" + (updated.Status >= 400 ? updated.Body : "<omitted>"));
                var posted = await Request(saleUrl + "/post", thumbprint!, bearer, "https://localhost:44321", "POST",
                    new JObject { ["expectedVersion"] = JObject.Parse(updated.Body)["data"]!["version"]!.DeepClone() }.ToString(Formatting.None), csrf: true);
                Assert.Equal(200, posted.Status);
                var ack = JObject.Parse(posted.Body)["data"]!;
                Assert.Equal("Posted", (string?)ack["status"]);
                Assert.Null(ack["lines"]);
                var postedDetail = await Request(saleUrl, thumbprint!, bearer);
                Assert.Equal(200, postedDetail.Status);
                var postedJson = JObject.Parse(postedDetail.Body)["data"]!;
                Assert.Equal(JTokenType.String, postedJson["totalCost"]!.Type);
                Assert.Equal(JTokenType.String, postedJson["lines"]![0]!["postedUnitCost"]!.Type);
                using (var oracle = new SqlConnection(database.ConnectionString))
                {
                    await oracle.OpenAsync();
                    Assert.Equal(15m, await oracle.QuerySingleAsync<decimal>("SELECT Quantity FROM dbo.StockBalances;"));
                    Assert.Equal(225m, await oracle.QuerySingleAsync<decimal>("SELECT InventoryValue FROM dbo.StockBalances;"));
                    Assert.Equal(2, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger WHERE DocumentType='SalesReceipt';"));
                }
                var crossRefresh = await Request("https://localhost:44321/api/v1/auth/refresh", thumbprint!, origin: "https://outside.invalid", method: "POST", cookie: signedIn.Cookie, csrf: true);
                Assert.Equal(403, crossRefresh.Status);
                var rotated = await Request("https://localhost:44321/api/v1/auth/refresh", thumbprint!, origin: "https://localhost:44321", method: "POST", cookie: signedIn.Cookie, csrf: true);
                Assert.Equal(200, rotated.Status);
                AssertRefreshCookie(rotated, cleared: false);
                Assert.NotEqual(signedIn.Cookie, rotated.Cookie);
                var rotatedBearer = "Bearer " + (string)JObject.Parse(rotated.Body)["data"]!["accessToken"]!;
                Assert.Equal(200, (await Request("https://localhost:44321/api/v1/auth/me", thumbprint!, rotatedBearer)).Status);
                var admitted = await Request("https://localhost:44321/api/v1/private", thumbprint!, bearer);
                Assert.Equal(404, admitted.Status); Assert.Contains("application/json", admitted.ContentType);
                Assert.Null(admitted.Location); Assert.DoesNotContain("<html", admitted.Body);
                var signedOut = await Request("https://localhost:44321/api/v1/auth/logout", thumbprint!, origin: "https://localhost:44321", method: "POST", cookie: rotated.Cookie, csrf: true);
                Assert.Equal(200, signedOut.Status);
                AssertRefreshCookie(signedOut, cleared: true);
                Assert.Equal(401, (await Request("https://localhost:44321/api/v1/private", thumbprint!, bearer)).Status);
                Assert.Equal(401, (await Request("https://localhost:44321/api/v1/auth/me", thumbprint!, rotatedBearer)).Status);
                Assert.Equal(401, (await Request("https://localhost:44321/api/v1/auth/refresh", thumbprint!, origin: "https://localhost:44321", method: "POST", cookie: rotated.Cookie, csrf: true)).Status);
                var root = await Request("https://localhost:44321/", thumbprint!);
                var fallback = await Request("https://localhost:44321/sales/receipt/42", thumbprint!);
                Assert.Equal(200, root.Status); Assert.Equal(200, fallback.Status);
                Assert.NotEqual(root.Body, fallback.Body);
                string WithoutNonce(string html) => Regex.Replace(html, "<meta name=\"mydmsvn-style-nonce\" content=\"[^\"]+\" />", "");
                Assert.Equal(WithoutNonce(root.Body), WithoutNonce(fallback.Body));
                Assert.Contains("text/html", fallback.ContentType);
                Assert.Equal(404, (await Request("https://localhost:44321/missing.js", thumbprint!)).Status);
                Assert.Equal(400, (await Request("http://localhost:18081/api/v1/health", thumbprint!, insecureRejectionProbe: true)).Status);
                var crossOrigin = await Request("https://localhost:44321/api/v1/health", thumbprint!, origin: "https://outside.invalid");
                Assert.Equal(403, crossOrigin.Status);
            }
            await Web5IisExpressHostAdmission.VerifyAsync(hostIdentity!, CancellationToken.None);
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            if (process != null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        // A closed stdin pipe does not prove the worker exited. Always run the
                        // bounded wait/kill/drain below before touching its SQL resources.
                        try { process.StandardInput.WriteLine("q"); }
                        catch (IOException) { }
                        catch (InvalidOperationException) { }
                        if (!process.WaitForExit(10000)) process.Kill();
                        if (!process.WaitForExit(10000)) throw new InvalidOperationException("Owned IIS Express worker did not drain; retain SQL resources.");
                    }
                    runtimeCleanup.MarkWorkerExitVerified();
                    if (hostIdentity == null)
                        throw new InvalidOperationException("Owned IIS Express identity was not captured; retain SQL resources.");
                    await Web5IisExpressHostAdmission.WaitForAbsenceAsync(
                        hostIdentity, TimeSpan.FromSeconds(20), CancellationToken.None);
                    runtimeCleanup.MarkQueueAbsenceVerified();
                    drained = true;
                }
                catch (Exception error) { failures.Add(error); }
                finally { process.Dispose(); }
            }
            if (web6Candidate)
            {
                try
                {
                    var admittedCleanup = failures.Count == 0 && drained && !GuiTestSafety.IsPoisoned &&
                        runtimeCleanup.CanDelete(false) && runDirectoryLease != null && (!web6Workload || workloadRootLease != null);
                    if (admittedCleanup && bootstrap != null)
                    {
                        await Web6BootstrapSelectorEvidence.PreflightSqlCleanup(admin, new[] { (database, bootstrapSqlControl!) });
                        bootstrapResources.Add(Web6BootstrapSelectorEvidence.Resource("deletedDirectory", bootstrap.Plan.GateRoot(bootstrap.Gate) + "-host-runtime"));
                        if (web6Workload) bootstrapResources.Add(Web6BootstrapSelectorEvidence.Resource("deletedDirectory", bootstrap.Plan.GateRoot(bootstrap.Gate) + "-measurements"));
                        bootstrap.SealCleanupInventory(bootstrapResources);
                    }
                    runtimeRootDeleted = await Web6OuterCleanupCoordinator.Execute(admittedCleanup,
                        () => { runDirectoryLease!.DeleteTreeVerified(); return Task.CompletedTask; },
                        CleanupOwnedSql,
                        () => { workloadRootLease?.DeleteTreeVerified(); return Task.CompletedTask; },
                        () => runDirectoryLease?.Dispose(), () => workloadRootLease?.Dispose());
                    if (!runtimeRootDeleted && failures.Count == 0)
                        failures.Add(new InvalidOperationException("Candidate cleanup was not fully admitted; workload/runtime/SQL retained."));
                }
                catch (Exception error) { failures.Add(error); }
            }
            else if (failures.Count == 0 && drained && !GuiTestSafety.IsPoisoned)
            {
                try { await CleanupOwnedSql(); }
                catch (Exception error) { failures.Add(error); }
            }
            else if (failures.Count == 0)
                failures.Add(new InvalidOperationException("Fixture cleanup was not admitted; resources retained."));
            if (candidateManifest != null)
            {
                try
                {
                    if (web6Workload)
                        candidateManifest.RecordDisposition(Web6CandidateStage.Workload,
                            failures.Count == 0 && workloadValidated ? Web6CandidateOutcome.Passed : Web6CandidateOutcome.Failed);
                    else
                    {
                        foreach (var stage in bootstrap == null ? new[] { Web6CandidateStage.Chromium, Web6CandidateStage.Edge } :
                            new[] { bootstrap.Gate == "pair-edge" ? Web6CandidateStage.Edge : Web6CandidateStage.Chromium })
                            candidateManifest.RecordDisposition(stage,
                                failures.Count == 0 && candidateBrowserValidated ? Web6CandidateOutcome.Passed : Web6CandidateOutcome.Failed);
                    }
                    candidateManifest.RecordDisposition(Web6CandidateStage.Cleanup,
                        failures.Count == 0 && drained && runtimeRootDeleted ? Web6CandidateOutcome.Passed : Web6CandidateOutcome.Quarantined);
                }
                catch (Exception error) { failures.Add(error); }
                finally { candidateManifest.Dispose(); }
            }
        }
        if (web6Candidate && !web6Workload && failures.Count == 0)
            Web6CandidateOracle.ValidateDisposition(candidateBrowserValidated, browserDrained: true, hostDrained: drained, cleanupVerified: runtimeRootDeleted);
        if (failures.Count > 0) throw new AggregateException("Packaged host scenario or owned cleanup failed.", failures);
        if (bootstrap != null)
        {
            Web6BootstrapSelectorEvidence.Complete(bootstrap, bootstrapResources, bootstrapSqlReceipt!, bootstrapProfiles);
        }
    }

    private static string FindRootFile(string relative)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(relative);
    }
    private static JToken NormalizeDraftDecimals(JToken detail)
    {
        var result = detail.DeepClone();
        void Normalize(JToken owner, string name)
        {
            Assert.Equal(JTokenType.String, owner[name]!.Type);
            var value = decimal.Parse((string)owner[name]!, CultureInfo.InvariantCulture);
            Assert.Equal(value, decimal.Round(value, 4));
            owner[name] = value.ToString("F4", CultureInfo.InvariantCulture);
        }
        // The contract permits shorter exact decimal forms. Gift revenue is initially "0",
        // while SQL decimal(28,4) materializes "0.0000"; IDs/versions remain untouched.
        Normalize(result, "totalAmount");
        foreach (var line in result["lines"]!)
            foreach (var name in new[] { "qty", "unitPrice", "lineAmount" }) Normalize(line, name);
        return result;
    }
    private class WebHostSqlFactAttribute : FactAttribute
    {
        public WebHostSqlFactAttribute()
        {
            foreach (var variable in new[] { SqlTestDatabase.ConnectionStringEnvironmentVariable, "MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY", "MYDMSVN_WEB_TEST_TLS_THUMBPRINT" })
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable)))
                    Skip = "Packaged IIS Express SQL test requires explicit publish, TLS pin and nonproduction SQL configuration.";
        }
    }
    private sealed class WebAttendedSqlFactAttribute : WebHostSqlFactAttribute
    {
        public WebAttendedSqlFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("MYDMSVN_WEB_ATTENDED_ENABLE") != "1" ||
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYDMSVN_WEB_ATTENDED_DIRECTORY")))
                Skip = "Attended fixture requires explicit opt-in and a private parent directory.";
        }
    }
    private sealed class WebBrowserSqlFactAttribute : WebHostSqlFactAttribute
    {
        public WebBrowserSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_BROWSER_DIRECTORY")))
                Skip = "Real browser pilot requires an explicit frontend checkout with built artifacts and Playwright.";
        }
    }
    private sealed class WebOriginalFirstSqlFactAttribute : WebHostSqlFactAttribute
    {
        public WebOriginalFirstSqlFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("MYDMSVN_WEB5_ORIGINAL_FIRST") != "1" ||
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYDMSVN_WEB5_RACE_PARENT_DIRECTORY")) ||
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_BROWSER_DIRECTORY")))
                Skip = "Original-first fixture requires explicit opt-in, private parent directory and browser checkout.";
        }
    }
    private sealed class Web6CandidateSqlFactAttribute : WebHostSqlFactAttribute
    {
        public Web6CandidateSqlFactAttribute()
        {
            Skip = Web6CandidateAcceptanceGate.OrdinarySkipReason(Web6CandidateAcceptanceGate.EnvironmentValues());
        }
    }
    private sealed class Web6CutoverSqlFactAttribute : WebHostSqlFactAttribute
    {
        public Web6CutoverSqlFactAttribute()
        {
            Skip = Web6CutoverAcceptanceGate.OrdinarySkipReason(Web6CutoverAcceptanceGate.EnvironmentValues());
        }
    }
    private sealed class Web6WorkloadSqlFactAttribute : WebHostSqlFactAttribute
    {
        public Web6WorkloadSqlFactAttribute()
        {
            Skip = Web6WorkloadAcceptanceGate.OrdinarySkipReason(Web6WorkloadAcceptanceGate.EnvironmentValues());
        }
    }

    private const string BrowserPassword = "BrowserPilot_test-only-password_2026!";

    private static async Task RunCutoverCandidate()
    {
        var values = Web6CutoverAcceptanceGate.EnvironmentValues();
        Web6CutoverAcceptanceGate.RequireLive(values);
        using var bootstrap = Web6SelectorOutput.Open("cutover");
        string Required(string name) => values[name] ?? throw new InvalidOperationException("Cutover prerequisite disappeared.");
        var frontendRoot = Path.GetFullPath(Required("MYDMSVN_WEB6_CUTOVER_FRONTEND_ARCHIVE_DIRECTORY"));
        var backendRoot = Path.GetFullPath(Required("MYDMSVN_WEB6_CUTOVER_BACKEND_ARCHIVE_DIRECTORY"));
        var frontendIdentity = GitIdentity(frontendRoot);
        var backendIdentity = GitIdentity(backendRoot);
        var frontendArtifact = Web6CandidateFileSet.MeasureTree(Path.GetFullPath(Required("MYDMSVN_WEB6_CUTOVER_FRONTEND_DIST_DIRECTORY")));
        var oldArtifact = Web6CandidateFileSet.MeasureTree(Path.GetFullPath(Required("MYDMSVN_WEB6_CUTOVER_OLD_PUBLISH_DIRECTORY")));
        var currentArtifact = Web6CandidateFileSet.MeasureTree(Path.GetFullPath(Required("MYDMSVN_WEB6_CUTOVER_CURRENT_PUBLISH_DIRECTORY")));
        var buildAuthority = Web6CutoverBuildAuthority.ReadProtected(
            Path.GetFullPath(Required("MYDMSVN_WEB6_CUTOVER_BUILD_AUTHORITY_PATH")),
            Required("MYDMSVN_WEB6_CUTOVER_BUILD_AUTHORITY_SHA256"));
        buildAuthority.Verify(new Web6CutoverBuildObservation(frontendIdentity.Sha, frontendIdentity.Tree, true, frontendArtifact,
            backendIdentity.Sha, backendIdentity.Tree, true, oldArtifact, currentArtifact, oldArtifact));
        if (!Guid.TryParseExact(Required("MYDMSVN_WEB6_CUTOVER_CURRENT_EPOCH"), "D", out var currentEpoch) ||
            !Guid.TryParseExact(Required("MYDMSVN_WEB6_CUTOVER_RESTORED_EPOCH"), "D", out var restoredEpoch))
            throw new InvalidOperationException("Cutover deployment epochs are invalid.");
        var admin = Required("MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING");
        var controlParent = Path.GetFullPath(Required("MYDMSVN_WEB6_CUTOVER_SQL_CONTROL_PARENT"));
        await Web6CutoverSqlApproval.VerifyFileAsync(Path.GetFullPath(Required("MYDMSVN_SQL_TEST_APPROVAL_FILE")),
            admin, CancellationToken.None).ConfigureAwait(false);
        SqlTestDatabase? currentDatabase = null, oldDatabase = null, restoredDatabase = null;
        SqlTestOwnershipManifest? currentControl = null, oldControl = null, restoredControl = null;
        var complete = false;
        var cleanupVerified = false;
        LiveCutoverOperations? operations = null;
        var currentRecorded = false; var oldRecorded = false; var restoredRecorded = false;
        try
        {
            await Web6CutoverLifetime.Run(async () =>
            {
            currentControl = SqlTestOwnershipManifest.Create(controlParent);
            oldControl = SqlTestOwnershipManifest.Create(controlParent);
            restoredControl = SqlTestOwnershipManifest.Create(controlParent);
            currentDatabase = await SqlTestDatabase.CreateAsync(admin).ConfigureAwait(false);
            oldDatabase = await SqlTestDatabase.CreateAsync(admin).ConfigureAwait(false);
            restoredDatabase = await SqlTestDatabase.CreateAsync(admin).ConfigureAwait(false);
            if (new[] { currentDatabase.DatabaseName, oldDatabase.DatabaseName, restoredDatabase.DatabaseName }.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3) throw new InvalidOperationException("Cutover SQL allocations collided.");
            if (!new DatabaseMigrationRunner().MigrateWeb5(currentDatabase.ConnectionString).Successful ||
                !new DatabaseMigrationRunner().MigrateWeb(oldDatabase.ConnectionString).Successful ||
                !new DatabaseMigrationRunner().MigrateWeb5(restoredDatabase.ConnectionString).Successful)
                throw new InvalidOperationException("Cutover SQL migrations failed.");
            using (var currentSql = new SqlConnection(currentDatabase.ConnectionString))
            using (var oldSql = new SqlConnection(oldDatabase.ConnectionString))
            using (var restoredSql = new SqlConnection(restoredDatabase.ConnectionString))
            {
                await restoredSql.OpenAsync().ConfigureAwait(false);
                await restoredDatabase.VerifyOwnershipAsync(restoredSql, CancellationToken.None).ConfigureAwait(false);
                Web6CutoverSqlApproval.RequireSchema(await SqlSchemaDefinitionFingerprint.ReadAsync(restoredSql, CancellationToken.None).ConfigureAwait(false), SqlSchemaDefinitionFingerprint.Schema13);
                await currentSql.OpenAsync().ConfigureAwait(false); await oldSql.OpenAsync().ConfigureAwait(false);
                await currentDatabase.VerifyOwnershipAsync(currentSql, CancellationToken.None).ConfigureAwait(false);
                await oldDatabase.VerifyOwnershipAsync(oldSql, CancellationToken.None).ConfigureAwait(false);
                Web6CutoverSqlApproval.RequireSchema(
                    await SqlSchemaDefinitionFingerprint.ReadAsync(currentSql, CancellationToken.None).ConfigureAwait(false),
                    SqlSchemaDefinitionFingerprint.Schema13);
                Web6CutoverSqlApproval.RequireSchema(
                    await SqlSchemaDefinitionFingerprint.ReadAsync(oldSql, CancellationToken.None).ConfigureAwait(false),
                    SqlSchemaDefinitionFingerprint.Schema12);
            }
            await currentDatabase.WriteOwnershipManifestAsync(currentControl, CancellationToken.None).ConfigureAwait(false);
            currentRecorded = true;
            await oldDatabase.WriteOwnershipManifestAsync(oldControl, CancellationToken.None).ConfigureAwait(false);
            oldRecorded = true;
            await restoredDatabase.WriteOwnershipManifestAsync(restoredControl, CancellationToken.None).ConfigureAwait(false);
            restoredRecorded = true;
            var restoredReceipt = Web6CutoverSqlOwnershipReceipt.FromProtectedRecord(restoredControl, restoredControl.VerifiedDigest(),
                SqlSchemaDefinitionFingerprint.Schema13, "restored-allocation").Digest;
            var currentReceipt = Web6CutoverSqlOwnershipReceipt.FromProtectedRecord(currentControl, currentControl.VerifiedDigest(),
                SqlSchemaDefinitionFingerprint.Schema13, "current-allocation").Digest;
            var oldReceipt = Web6CutoverSqlOwnershipReceipt.FromProtectedRecord(oldControl, oldControl.VerifiedDigest(),
                SqlSchemaDefinitionFingerprint.Schema12, "old-allocation").Digest;
            var plan = new Web6CandidateCutoverPlan(Required("MYDMSVN_WEB6_CANDIDATE_ID"), "web4-old", "web6-restored",
                new Uri("https://localhost:44321/"), new Uri("https://localhost:44322/"),
                frontendIdentity.Sha, frontendIdentity.Tree, frontendArtifact,
                backendIdentity.Sha, backendIdentity.Tree, oldArtifact, currentArtifact,
                13, 12, currentReceipt, oldReceipt, restoredReceipt, currentEpoch, restoredEpoch);
            plan.VerifyArchives(new Web6CutoverArchive(frontendIdentity.Sha, frontendIdentity.Tree, true, frontendArtifact),
                new Web6CutoverArchive(backendIdentity.Sha, backendIdentity.Tree, true, oldArtifact));
            operations = new LiveCutoverOperations(plan, currentDatabase, oldDatabase, restoredDatabase, currentControl, oldControl, restoredControl, bootstrap);
            await new Web6CandidateCutoverOrchestrator(operations).Execute(CancellationToken.None).ConfigureAwait(false);
            complete = true;
            }, async () => operations == null || await operations.Cleanup().ConfigureAwait(false), new Func<Task>[]
            {
                async () =>
                {
                    if (currentDatabase == null) return;
                    await currentDatabase.DisposeIfQuiescentAsync().ConfigureAwait(false);
                    if (currentRecorded) await currentDatabase.CompleteOwnershipManifestAsync(currentControl!, CancellationToken.None).ConfigureAwait(false);
                },
                async () =>
                {
                    if (oldDatabase == null) return;
                    await oldDatabase.DisposeIfQuiescentAsync().ConfigureAwait(false);
                    if (oldRecorded) await oldDatabase.CompleteOwnershipManifestAsync(oldControl!, CancellationToken.None).ConfigureAwait(false);
                },
                async () =>
                {
                    if (restoredDatabase == null) return;
                    await restoredDatabase.DisposeIfQuiescentAsync().ConfigureAwait(false);
                    if (restoredRecorded) await restoredDatabase.CompleteOwnershipManifestAsync(restoredControl!, CancellationToken.None).ConfigureAwait(false);
                },
            }, stopOnCleanupFailure: bootstrap != null).ConfigureAwait(false);
            if (complete) { operations!.VerifyFinalDisposition(); cleanupVerified = true; }
        }
        finally { currentControl?.Dispose(); oldControl?.Dispose(); restoredControl?.Dispose(); operations?.DisposeManifests(cleanupVerified); }
        if (bootstrap != null)
            Web6BootstrapSelectorEvidence.Complete(bootstrap, operations!.BootstrapResources, operations.BootstrapSqlReceipt!,
                manifestPath: Path.Combine(bootstrap.Plan.EvidenceRoot, "candidate.manifest"), manifestHash: operations.BootstrapManifestHash);
    }
    private static async Task SeedBrowserActors(SqlConnection connection)
    {
        var hash = new BcryptPasswordHasher().Hash(BrowserPassword);
        foreach (var name in new[] { "alice", "bob", "poster" })
        {
            var id = await connection.QuerySingleAsync<int>("INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(@name,@normalized,@name,N'Local',@hash,N'',N'BCrypt');", new { name, normalized = name.ToUpperInvariant(), hash });
            foreach (var key in new[] { "Catalog.Products.Read", "Catalog.Customers.Read", "Catalog.Warehouses.Read", "Catalog.Employees.Read", "Sales.Receipts.Read", "Sales.Receipts.Write", "Sales.Receipts.Post" })
            {
                var grant = key.StartsWith("Catalog.", StringComparison.Ordinal) || name == "alice" || (name == "bob" ? key == "Sales.Receipts.Read" : key != "Sales.Receipts.Read");
                await connection.ExecuteAsync("INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) VALUES(@id,@key,@grant);", new { id, key, grant });
            }
        }
    }

    private static async Task SeedReceiptActors(SqlConnection connection)
    {
        var hash = new BcryptPasswordHasher().Hash(BrowserPassword);
        foreach (var name in new[] { "receiptEditor", "receiptPoster", "receiptReader" })
        {
            var id = await connection.QuerySingleAsync<int>("INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(@name,@normalized,@name,N'Local',@hash,N'',N'BCrypt');", new { name, normalized = name.ToUpperInvariant(), hash });
            var permissions = new List<string> { "Catalog.Products.Read", "Catalog.Customers.Read", "Catalog.Warehouses.Read", "Catalog.Employees.Read" };
            if (name == "receiptEditor") permissions.AddRange(new[] { "Sales.Receipts.Read", "Sales.Receipts.Write", "Sales.Receipts.Post", "Inventory.GoodsReceipts.Read", "Inventory.GoodsReceipts.Write", "Inventory.GoodsReceipts.Post" });
            if (name == "receiptPoster") permissions.AddRange(new[] { "Inventory.GoodsReceipts.Write", "Inventory.GoodsReceipts.Post" });
            if (name == "receiptReader") permissions.Add("Inventory.GoodsReceipts.Read");
            foreach (var key in permissions)
                await connection.ExecuteAsync("INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) VALUES(@id,@key,1);", new { id, key });
        }
    }

    private static async Task RunBrowser(SqlTestDatabase database, string runDirectory, int customer, int warehouse, int employee, int product, int receiptProduct, IntegratedCatalog? integrated, bool recoveryPilot, bool web5Pilot, bool originalFirstPilot,
        bool web6Candidate = false, string? runtimeUser = null, Action? candidateValidated = null,
        Web6CandidateManifest? candidateManifest = null, Web6CandidateHostMismatch? candidateMismatch = null,
        bool web6Cutover = false, Web6CandidateCutoverPlan? cutoverPlan = null,
        WebRecoveryPilotControl? cutoverControl = null, string? cutoverBrowser = null, Action<JObject, JObject>? cutoverResult = null)
    {
        var connectionString = database.ConnectionString;
        var checkout = Path.GetFullPath(Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_BROWSER_DIRECTORY")!);
        var node = Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_NODE") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe");
        var cli = Path.Combine(checkout, "node_modules", "@playwright", "test", "cli.js");
        Assert.True(File.Exists(node)); Assert.True(File.Exists(cli));
        Assert.True(File.Exists(Path.Combine(checkout, "playwright.pilot.config.ts")));
        var manifestPath = Path.Combine(runDirectory, "browser-manifest.json");
        var resultPath = Path.Combine(runDirectory, "browser-result.json");
        var countPath = Path.Combine(runDirectory, "browser-count.json");
        var networkPath = Path.Combine(runDirectory, "browser-network.json");
        var certificatePath = Path.Combine(runDirectory, "localhost-public.pem");
        object Actor(string username) => new { username, password = BrowserPassword };
        var manifest = new { origin = "https://localhost:44321", actors = new { alice = Actor("alice"), bob = Actor("bob"), poster = Actor("poster") }, catalog = new { customerId = customer, warehouseId = warehouse, employeeId = employee, productId = product }, resultPath };
        var manifestJson = JObject.FromObject(manifest);
        Web6CandidateExpected? web6Expected = null;
        if (receiptProduct != 0)
        {
            Assert.True(File.Exists(Path.Combine(checkout, "tests", "pilot", "web3-receipt-live.spec.ts")));
            manifestJson["mode"] = "web3-receipt";
            foreach (var name in new[] { "receiptEditor", "receiptPoster", "receiptReader" }) manifestJson["actors"]![name] = JObject.FromObject(Actor(name));
            manifestJson["receiptCatalog"] = JObject.FromObject(new { warehouseId = warehouse, employeeId = employee, productId = receiptProduct });
        }
        if (integrated != null)
        {
            Assert.True(File.Exists(Path.Combine(checkout, "tests", "pilot", "web3-integrated-live.spec.ts")));
            manifestJson["mode"] = "web3-integrated";
            foreach (var name in new[] { "integratedEditor", "balanceReader", "cardReader" }) manifestJson["actors"]![name] = JObject.FromObject(Actor(name));
            manifestJson["integratedCatalog"] = JObject.FromObject(new { warehouseId = integrated.Warehouse, otherWarehouseId = integrated.OtherWarehouse, employeeId = employee, customerId = customer, productId = integrated.Product });
        }
        WebRecoveryPilotControl? recovery = null;
        if (recoveryPilot)
        {
            Assert.True(File.Exists(Path.Combine(checkout, "tests", "pilot", "web4-recovery-live.spec.ts")));
            if (web5Pilot) Assert.True(File.Exists(Path.Combine(checkout, "tests", "pilot", "web5-resolution-live.spec.ts")));
            recovery = cutoverControl ?? (web6Candidate ? await WebRecoveryPilotControl.StartCandidate(connectionString, runtimeUser!, candidateMismatch!) :
                originalFirstPilot ? await WebRecoveryPilotControl.StartOriginalFirst(database, Environment.GetEnvironmentVariable("MYDMSVN_WEB5_RACE_PARENT_DIRECTORY")!) :
                await WebRecoveryPilotControl.Start(connectionString, web5Pilot));
            manifestJson["mode"] = web5Pilot ? "web5-recovery" : "web4-recovery";
            manifestJson["recovery"] = recovery.Manifest;
            if (originalFirstPilot) manifestJson["recovery"]!["originalFirst"] = true;
        }
        if (web6Candidate)
        {
            Assert.True(File.Exists(Path.Combine(checkout, "tests", "pilot", "web6-manual-resend-live.spec.ts")));
            var frontendSha = GitHead(checkout);
            var backendSha = GitHead(FindRootFile("CleanArchitecture.sln"), fileIsRootMarker: true);
            var sourceDist = Path.Combine(checkout, "dist");
            var publishedDist = Path.Combine(Path.GetFullPath(Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY")!), "wwwroot");
            var sourceHash = Web6CandidateFileSet.MeasureTree(sourceDist);
            var publishedHash = Web6CandidateFileSet.MeasureTree(publishedDist);
            Assert.Equal(sourceHash, publishedHash);
            web6Expected = new Web6CandidateExpected(Environment.GetEnvironmentVariable("MYDMSVN_WEB6_CANDIDATE_ID")!,
                frontendSha, backendSha, sourceHash, new Web6StockExpectation(warehouse, product, receiptProduct));
            manifestJson["version"] = 2;
            manifestJson["mode"] = "web6-candidate";
            manifestJson["candidateId"] = web6Expected.CandidateId;
            manifestJson["frontendSourceSha"] = web6Expected.FrontendSourceSha;
            manifestJson["backendSourceSha"] = web6Expected.BackendSourceSha;
            manifestJson["frontendDistManifestSha256"] = web6Expected.FrontendDistManifestSha256;
            if (web6Cutover) manifestJson["cutover"] = cutoverPlan!.FrontendHandles();
        }
        Exception? browserFailure = null;
        Web6CandidateManifest.PrivateInput? privateInput = null;
        Web6CandidateManifest.PrivateOutput? privateResult = null, privateCount = null, privateNetwork = null;
        var browserDrained = true;
        async Task<GuiProcessResult> ExecuteBrowser(string project, string outputName)
        {
            var start = new ProcessStartInfo(node, "\"" + cli + "\" test --config playwright.pilot.config.ts --workers=1 --max-failures=1 --project=" + project)
            { WorkingDirectory = checkout, UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            foreach (var name in start.EnvironmentVariables.Keys.Cast<string>().Where(name => name.StartsWith("MYDMSVN_", StringComparison.OrdinalIgnoreCase)).ToArray())
                start.EnvironmentVariables.Remove(name);
            start.EnvironmentVariables["MYDMSVN_WEB_PILOT_MANIFEST"] = manifestPath;
            start.EnvironmentVariables["NODE_EXTRA_CA_CERTS"] = certificatePath;
            if (web6Cutover) start.EnvironmentVariables["MYDMSVN_WEB6_CUTOVER_REQUIRED"] = "1";
            try
            {
                browserDrained = false;
                var executed = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromMinutes(5));
                browserDrained = true;
                File.WriteAllText(Path.Combine(runDirectory, outputName), Sanitize(recovery?.Redact(executed.StandardOutput + executed.StandardError) ?? executed.StandardOutput + executed.StandardError), new UTF8Encoding(false));
                Assert.True(executed.ExitCode == 0, "Browser pilot failed; inspect the sanitized local browser output (redact before sharing).");
                return executed;
            }
            catch (GuiProcessException error)
            {
                browserDrained = error.CleanupVerified;
                File.WriteAllText(Path.Combine(runDirectory, outputName), Sanitize(recovery?.Redact(error.StandardOutput + error.StandardError) ?? error.StandardOutput + error.StandardError), new UTF8Encoding(false));
                throw new InvalidOperationException("Browser process failed; cleanup verified=" + error.CleanupVerified + ". Inspect the sanitized local browser output.");
            }
        }
        try
        {
            if (web6Candidate)
            {
                privateResult = candidateManifest!.CreatePrivateOutput("browser-result.json");
                privateCount = candidateManifest.CreatePrivateOutput("browser-count.json");
                privateNetwork = candidateManifest.CreatePrivateOutput("browser-network.json");
                resultPath = privateResult.Path; countPath = privateCount.Path; networkPath = privateNetwork.Path;
                manifestJson["resultPath"] = resultPath;
                privateInput = candidateManifest!.CreatePrivateInput("browser-input.json", new UTF8Encoding(false).GetBytes(manifestJson.ToString(Formatting.None)));
                manifestPath = privateInput.Path;
            }
            else File.WriteAllText(manifestPath, manifestJson.ToString(Formatting.None), new UTF8Encoding(false));
            using (var certificates = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                certificates.Open(OpenFlags.ReadOnly);
                using var certificate = certificates.Certificates.Find(X509FindType.FindByThumbprint, Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_TLS_THUMBPRINT"), false).Cast<X509Certificate2>().Single();
                using var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                Assert.True(chain.Build(certificate), "The explicitly pinned local certificate must already be OS-trusted.");
                File.WriteAllText(certificatePath, "-----BEGIN CERTIFICATE-----\n" + Convert.ToBase64String(certificate.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks) + "\n-----END CERTIFICATE-----\n", new UTF8Encoding(false));
            }
            if (web6Candidate)
            {
                using var bootstrapPair = web6Cutover ? null : Web6SelectorOutput.Open("pair-" + Environment.GetEnvironmentVariable("MYDMSVN_WEB_PILOT_PROJECT"));
                foreach (var project in cutoverBrowser != null ? new[] { cutoverBrowser } : bootstrapPair != null ?
                    new[] { Environment.GetEnvironmentVariable("MYDMSVN_WEB_PILOT_PROJECT")! } : new[] { "chromium", "edge" })
                {
                    var initialResult = new JObject
                    {
                        ["version"] = 2, ["mode"] = "web6-candidate", ["candidateId"] = web6Expected!.CandidateId,
                        ["frontendSourceSha"] = web6Expected.FrontendSourceSha, ["backendSourceSha"] = web6Expected.BackendSourceSha,
                        ["frontendDistManifestSha256"] = web6Expected.FrontendDistManifestSha256,
                        ["operations"] = new JArray(), ["checks"] = new JArray(),
                    };
                    privateResult!.Replace(new UTF8Encoding(false).GetBytes(initialResult.ToString(Formatting.None)));
                    privateCount!.Replace(new UTF8Encoding(false).GetBytes("{}"));
                    privateNetwork!.Replace(new UTF8Encoding(false).GetBytes("[]"));
                    await ExecuteBrowser(project, "browser-output-" + project + ".log");
                    var count = JObject.Parse(privateCount.ReadAllText(1024), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    var candidateResult = JObject.Parse(privateResult.ReadAllText(1048576), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (web6Cutover)
                    {
                        Web6CutoverOracle.Validate(count, candidateResult,
                            cutoverPlan ?? throw new InvalidOperationException("Cutover plan disappeared."), web6Expected!, (JObject)manifestJson["cutover"]!);
                        cutoverResult?.Invoke(count, candidateResult);
                    }
                    else
                    {
                        Web6CandidateOracle.ValidateBrowserCount(count);
                        await Web6CandidateOracle.VerifySqlAsync(connectionString, candidateResult, web6Expected!, CancellationToken.None);
                        Web6CandidateOracle.ValidateNetwork(JArray.Parse(privateNetwork.ReadAllText(16384), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }));
                    }
                    Console.WriteLine("WEB6_BROWSER_COUNT browser=" + project + " " + count.ToString(Formatting.None));
                }
                await recovery!.Stop(browserDrained);
                var health = await Request("https://localhost:44321/api/v1/health", Environment.GetEnvironmentVariable("MYDMSVN_WEB_TEST_TLS_THUMBPRINT")!);
                if (health.Status != 200 || !browserDrained) throw new InvalidDataException("WEB6 audit readiness or browser drain failed.");
                candidateValidated?.Invoke();
                return;
            }
            var selectedProject = Environment.GetEnvironmentVariable("MYDMSVN_WEB_PILOT_PROJECT");
            Assert.Contains(selectedProject, new[] { "chromium", "edge" });
            await ExecuteBrowser(selectedProject!, "browser-output.log");
            if (recovery != null)
            {
                await recovery.Stop(browserDrained);
                if (web5Pilot)
                {
                    Assert.True(recovery.NegativeVerified);
                    if (originalFirstPilot) Assert.True(recovery.OriginalFirstVerified);
                    if (!File.Exists(countPath) || new FileInfo(countPath).Length > 1024) throw new InvalidDataException("Missing or oversized browser count.");
                    var count = JObject.Parse(File.ReadAllText(countPath), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    ValidateWeb5BrowserCount(count);
                    Console.WriteLine("WEB5_BROWSER_COUNT " + count.ToString(Formatting.None));
                }
                await VerifyRecoveryStock(connectionString, runDirectory, JObject.Parse(File.ReadAllText(resultPath)), recovery.Epochs, warehouse, product, receiptProduct, integrated!, web5Pilot, recovery.NegativeIdentities, originalFirstPilot, recovery.OriginalFirstKeys);
                return;
            }
            var posted = JArray.Parse(File.ReadAllText(resultPath));
            using var oracle = new SqlConnection(connectionString); await oracle.OpenAsync();
            VerifyPilotResultIdentities(posted,
                (await oracle.QueryAsync<long>("SELECT ReceiptId FROM dbo.GoodsReceipts WHERE Status='Posted';")).ToArray(),
                (await oracle.QueryAsync<long>("SELECT SaleId FROM dbo.SalesReceipts WHERE Status='Posted';")).ToArray(), integrated != null);
            var integratedRows = posted.Where(row => (string?)row["scenario"] == "integrated").ToArray();
            posted = new JArray(posted.Where(row => (string?)row["scenario"] == null));
            var receipts = posted.Where(row => (string?)row["documentType"] == "GoodsReceipt").ToArray();
            if (receiptProduct == 0) Assert.Empty(receipts);
            else Assert.NotEmpty(receipts);
            Assert.All(posted, row => Assert.True(row["documentType"] == null || (string?)row["documentType"] == "GoodsReceipt", "Unknown browser result document type."));
            posted = new JArray(posted.Where(row => row["documentType"] == null));
            Assert.NotEmpty(posted);
            Assert.Equal(posted.Count, posted.Select(row => (string)row["saleId"]!).Distinct().Count());
            decimal quantity = 0, cost = 0;
            foreach (var row in posted)
            {
                var id = long.Parse((string)row["saleId"]!, CultureInfo.InvariantCulture);
                var qty = decimal.Parse((string)row["quantity"]!, CultureInfo.InvariantCulture);
                var amount = decimal.Parse((string)row["cost"]!, CultureInfo.InvariantCulture);
                Assert.Equal("Posted", await oracle.QuerySingleAsync<string>("SELECT Status FROM dbo.SalesReceipts WHERE SaleId=@id;", new { id }));
                Assert.Equal(qty, await oracle.QuerySingleAsync<decimal>("SELECT SUM(Qty) FROM dbo.SalesReceiptLines WHERE SaleId=@id;", new { id }));
                Assert.Equal(amount, await oracle.QuerySingleAsync<decimal>("SELECT SUM(CostAmount) FROM dbo.SalesReceiptLines WHERE SaleId=@id;", new { id }));
                quantity += qty; cost += amount;
            }
            Assert.Equal(posted.Count, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SalesReceipts s WHERE s.Status='Posted' AND EXISTS(SELECT 1 FROM dbo.SalesReceiptLines l WHERE l.SaleId=s.SaleId AND l.ProductId=@product);", new { product }));
            var salesKeyFilter = receiptProduct == 0 ? "" : " WHERE WarehouseId=@warehouse AND ProductId=@product";
            Assert.Equal(1000m - quantity, await oracle.QuerySingleAsync<decimal>("SELECT Quantity FROM dbo.StockBalances" + salesKeyFilter + ";", new { warehouse, product }));
            Assert.Equal(15000m - cost, await oracle.QuerySingleAsync<decimal>("SELECT InventoryValue FROM dbo.StockBalances" + salesKeyFilter + ";", new { warehouse, product }));
            var salesLedgerKeyFilter = receiptProduct == 0 ? "" : " AND l.WarehouseId=@warehouse AND l.ProductId=@product";
            Assert.Equal(quantity, -(await oracle.QuerySingleAsync<decimal>("SELECT SUM(l.QuantityChange) FROM dbo.StockLedger l WHERE l.DocumentType='SalesReceipt'" + salesLedgerKeyFilter + ";", new { warehouse, product })));
            Assert.Equal(cost, -(await oracle.QuerySingleAsync<decimal>("SELECT SUM(v.ValueChange) FROM dbo.StockValuationEntries v JOIN dbo.StockLedger l ON l.LedgerId=v.LedgerId WHERE l.DocumentType='SalesReceipt'" + salesLedgerKeyFilter + ";", new { warehouse, product })));
            Assert.Equal(await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SalesReceiptLines l JOIN dbo.SalesReceipts s ON s.SaleId=l.SaleId WHERE s.Status='Posted';"),
                await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId JOIN dbo.SalesReceiptLines s ON s.SaleLineId=l.DocumentLineId AND s.SaleId=l.DocumentId WHERE l.DocumentType='SalesReceipt' AND l.QuantityChange=-s.Qty AND v.ValueChange=-s.CostAmount;"));
            if (receiptProduct != 0) await VerifyReceiptStock(oracle, receipts, warehouse, receiptProduct);
            if (integrated != null) await VerifyIntegratedStock(oracle, integratedRows, integrated);
            await VerifyAllPilotMovements(oracle, warehouse, product, receiptProduct, integrated);
        }
        catch (Exception error) { browserFailure = error; throw; }
        finally
        {
            try { if (recovery != null) await recovery.Stop(browserDrained); }
            catch (Exception cleanup) when (browserFailure != null) { throw new AggregateException("Browser scenario and recovery control drain failed.", browserFailure, cleanup); }
            finally
            {
                try { if (privateInput != null) privateInput.Dispose(); else File.Delete(manifestPath); }
                finally
                {
                    try { privateResult?.Dispose(); }
                    finally { try { privateCount?.Dispose(); } finally { privateNetwork?.Dispose(); File.Delete(certificatePath); } }
                }
            }
        }
    }
    private static async Task VerifyReceiptStock(SqlConnection oracle, JToken[] receipts, int warehouse, int product)
    {
        var ids = VerifyReceiptResultIdentities(receipts, (await oracle.QueryAsync<long>("SELECT r.ReceiptId FROM dbo.GoodsReceipts r WHERE r.Status='Posted' AND EXISTS(SELECT 1 FROM dbo.GoodsReceiptLines l WHERE l.ReceiptId=r.ReceiptId AND l.ProductId=@product);", new { product })).ToArray());
        decimal totalQuantity = 0, totalValue = 0;
        var totalLines = 0;
        for (var index = 0; index < receipts.Length; index++)
        {
            var row = receipts[index];
            var id = ids[index];
            var expectedQuantity = decimal.Parse((string)row["quantity"]!, CultureInfo.InvariantCulture);
            var expectedValue = decimal.Parse((string)row["value"]!, CultureInfo.InvariantCulture);
            var actor = (string)row["actor"]!;
            Assert.Contains(actor, new[] { "receiptEditor", "receiptPoster" });
            var header = await oracle.QuerySingleAsync<(string Status, int Warehouse, int Actor, string Username)>(
                "SELECT r.Status,r.WarehouseId,r.PostedByUserId,u.Username FROM dbo.GoodsReceipts r JOIN dbo.Users u ON u.UserId=r.PostedByUserId WHERE r.ReceiptId=@id AND r.PostedAtUtc IS NOT NULL;", new { id });
            Assert.Equal("Posted", header.Status);
            Assert.Equal(warehouse, header.Warehouse);
            Assert.Equal(actor, header.Username);
            // Read physical source numbers and calculate independently, without Application valuation/projection helpers.
            var lines = (await oracle.QueryAsync<(long Id, int Product, decimal Quantity, decimal UnitCost)>(
                "SELECT ReceiptLineId,ProductId,Quantity,UnitCost FROM dbo.GoodsReceiptLines WHERE ReceiptId=@id ORDER BY [LineNo];", new { id })).ToArray();
            Assert.NotEmpty(lines);
            decimal quantity = 0, value = 0;
            foreach (var line in lines)
            {
                Assert.Equal(product, line.Product);
                Assert.True(line.Quantity > 0 && line.UnitCost >= 0);
                var amount = decimal.Round(line.Quantity * line.UnitCost, 4, MidpointRounding.AwayFromZero);
                var movement = await oracle.QuerySingleAsync<(int Warehouse, int Product, decimal Quantity, decimal Value, int Actor)>(
                    "SELECT l.WarehouseId,l.ProductId,l.QuantityChange,v.ValueChange,l.PostedByUserId FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId JOIN dbo.GoodsReceipts r ON r.ReceiptId=l.DocumentId AND r.PostedAtUtc=l.PostedAtUtc WHERE l.DocumentType='GoodsReceipt' AND l.DocumentId=@id AND l.DocumentLineId=@line;", new { id, line = line.Id });
                Assert.Equal((warehouse, product, line.Quantity, amount, header.Actor), movement);
                quantity += line.Quantity; value += amount;
            }
            Assert.Equal(lines.Length, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger WHERE DocumentType='GoodsReceipt' AND DocumentId=@id;", new { id }));
            Assert.Equal(expectedQuantity, quantity); Assert.Equal(expectedValue, value);
            totalQuantity += quantity; totalValue += value; totalLines += lines.Length;
        }
        Assert.Equal(totalLines, await oracle.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.StockLedger WHERE DocumentType='GoodsReceipt' AND WarehouseId=@warehouse AND ProductId=@product;", new { warehouse, product }));
        Assert.Equal(totalQuantity, await oracle.QuerySingleAsync<decimal>("SELECT Quantity FROM dbo.StockBalances WHERE WarehouseId=@warehouse AND ProductId=@product;", new { warehouse, product }));
        Assert.Equal(totalValue, await oracle.QuerySingleAsync<decimal>("SELECT InventoryValue FROM dbo.StockBalances WHERE WarehouseId=@warehouse AND ProductId=@product;", new { warehouse, product }));
        Assert.Equal(totalQuantity, await oracle.QuerySingleAsync<decimal>("SELECT SUM(QuantityChange) FROM dbo.StockLedger WHERE DocumentType='GoodsReceipt' AND WarehouseId=@warehouse AND ProductId=@product;", new { warehouse, product }));
        Assert.Equal(totalValue, await oracle.QuerySingleAsync<decimal>("SELECT SUM(v.ValueChange) FROM dbo.StockLedger l JOIN dbo.StockValuationEntries v ON v.LedgerId=l.LedgerId WHERE l.DocumentType='GoodsReceipt' AND l.WarehouseId=@warehouse AND l.ProductId=@product;", new { warehouse, product }));
    }
    internal static long[] VerifyReceiptResultIdentities(JToken[] receipts, long[] postedIds)
    {
        var ids = new long[receipts.Length];
        var distinct = new HashSet<long>();
        for (var index = 0; index < receipts.Length; index++)
        {
            var token = receipts[index]["receiptId"];
            var text = token?.Type == JTokenType.String ? token.Value<string>() : null;
            if (text == null || !Regex.IsMatch(text, "\\A[1-9][0-9]{0,18}\\z", RegexOptions.CultureInvariant) ||
                !long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                throw new InvalidDataException("Receipt result ID must be a canonical positive int64 string.");
            if (!distinct.Add(id)) throw new InvalidDataException("Duplicate numeric receipt result ID.");
            ids[index] = id;
        }
        if (!distinct.SetEquals(postedIds)) throw new InvalidDataException("Receipt result IDs must exactly match SQL Posted receipt IDs.");
        return ids;
    }
    private static string Sanitize(string output) => Regex.Replace(
        Regex.Replace(output.Replace(BrowserPassword, "<redacted>"), "(?i)(authorization: Bearer )[^\\s]+", "$1<redacted>"),
        "(?i)(__Host-mydmsvn-rt=)[^;\\s]+", "$1<redacted>");
    private static string GitHead(string path, bool fileIsRootMarker = false)
    {
        var root = fileIsRootMarker ? Path.GetDirectoryName(path)! : path;
        if (Git(root, "status --porcelain").Length != 0) throw new InvalidOperationException("Candidate source tree must be clean.");
        var sha = Git(root, "rev-parse HEAD");
        return Web6CandidateInputs.RequireHash(sha, 40);
    }
    private static (string Sha, string Tree) GitIdentity(string root) =>
        (GitHead(root), Web6CandidateInputs.RequireHash(Git(root, "rev-parse HEAD^{tree}"), 40));
    private static string Git(string root, string arguments)
    {
        var bootstrap = Environment.GetEnvironmentVariable("MYDMSVN_WEB6_BOOTSTRAP_RUN_ID") != null;
        using var bootstrapOutput = bootstrap ? Web6SelectorOutput.Open(Path.GetFileNameWithoutExtension(
            Environment.GetEnvironmentVariable("MYDMSVN_WEB6_BOOTSTRAP_OUTPUT_PATH") ?? "")) : null;
        var executable = bootstrap ? BootstrapJson.AbsolutePath(Environment.GetEnvironmentVariable("MYDMSVN_WEB6_BOOTSTRAP_GIT_PATH") ?? "") : "git";
        if (bootstrap && (bootstrapOutput == null || executable != bootstrapOutput.GitPath ||
            Environment.GetEnvironmentVariable("MYDMSVN_WEB6_BOOTSTRAP_GIT_SHA256") != bootstrapOutput.Plan.Tool("git", "sha256")))
            throw new IOException("Bootstrap Git adapter differs from the protected prepared tool.");
        using var gitParents = bootstrap ? new ProtectedJsonFile.PathLease(executable) : null;
        using var pinnedGit = gitParents?.OpenRead(installedTool: true);
        if (bootstrap && BootstrapPathPolicy.HashStream(pinnedGit!) != Environment.GetEnvironmentVariable("MYDMSVN_WEB6_BOOTSTRAP_GIT_SHA256"))
            throw new IOException("Bootstrap Git executable digest differs.");
        var start = new ProcessStartInfo(executable, "--no-optional-locks -c core.hooksPath=NUL -c core.fsmonitor=false -c core.ignoreStat=false -c core.excludesFile=NUL -C \"" + root + "\" " + arguments)
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        if (bootstrap)
        {
            start.EnvironmentVariables["GIT_CONFIG_NOSYSTEM"] = "1";
            start.EnvironmentVariables["GIT_CONFIG_GLOBAL"] = "NUL";
            start.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Git source proof could not start.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(10000) || process.ExitCode != 0 || error.Length != 0)
            throw new InvalidOperationException("Git source proof failed.");
        return output.Trim();
    }
    private static string CertificateHash(string thumbprint)
    {
        using var certificates = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        certificates.Open(OpenFlags.ReadOnly);
        using var certificate = certificates.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false).Cast<X509Certificate2>().Single();
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(certificate.Export(X509ContentType.Cert))).Replace("-", "").ToLowerInvariant();
    }
    private static string HashText(string value)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(new UTF8Encoding(false, true).GetBytes(value))).Replace("-", "").ToLowerInvariant();
    }
    private static void AssertRefreshCookie(Reply reply, bool cleared)
    {
        Assert.NotNull(reply.SetCookie);
        var cookie = reply.SetCookie!.ToLowerInvariant();
        Assert.StartsWith("__host-mydmsvn-rt=", cookie);
        Assert.Contains("httponly", cookie); Assert.Contains("secure", cookie);
        Assert.Contains("samesite=strict", cookie); Assert.Contains("path=/", cookie);
        Assert.DoesNotContain("domain=", cookie);
        if (cleared) Assert.Contains("max-age=0", cookie);
        else Assert.True(Guid.TryParseExact(reply.Cookie, "D", out _));
    }
    private static string CreateConfiguration(string runDirectory, string published)
    {
        var source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IISExpress/config/applicationhost.config");
        var xml = new XmlDocument(); xml.Load(source);
        var sites = xml.SelectSingleNode("/configuration/system.applicationHost/sites")!;
        foreach (XmlNode site in sites.SelectNodes("site")!) sites.RemoveChild(site);
        var current = xml.CreateElement("site"); current.SetAttribute("name", "Web2Smoke"); current.SetAttribute("id", "1");
        var app = xml.CreateElement("application"); app.SetAttribute("path", "/"); app.SetAttribute("applicationPool", "Clr4IntegratedAppPool");
        var directory = xml.CreateElement("virtualDirectory"); directory.SetAttribute("path", "/"); directory.SetAttribute("physicalPath", Path.GetFullPath(published));
        app.AppendChild(directory); current.AppendChild(app);
        var bindings = xml.CreateElement("bindings");
        foreach (var endpoint in new[] { (Protocol: "https", Binding: "*:44321:localhost"), (Protocol: "http", Binding: "*:18081:localhost") })
        {
            var binding = xml.CreateElement("binding"); binding.SetAttribute("protocol", endpoint.Protocol); binding.SetAttribute("bindingInformation", endpoint.Binding); bindings.AppendChild(binding);
        }
        current.AppendChild(bindings); sites.AppendChild(current);
        var path = Path.Combine(runDirectory, "applicationhost.config"); xml.Save(path); return path;
    }
    private static async Task<Reply> Request(string url, string thumbprint, string? authorization = null, string? origin = null,
        string method = "GET", string? body = null, string? cookie = null, bool csrf = false, bool insecureRejectionProbe = false,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        var uri = new Uri(url);
        var request = insecureRejectionProbe
            ? Web5IisExpressHostAdmission.CreateDirectHttpRejectionRequest(uri)
            : Web5IisExpressHostAdmission.CreateDirectRequest(uri);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var abort = timeout.Token.Register(request.Abort);
        request.Method = method;
        request.Headers["X-Correlation-ID"] = "iis-smoke";
        if (authorization != null) request.Headers["Authorization"] = authorization;
        if (origin != null) request.Headers["Origin"] = origin;
        if (cookie != null) request.Headers["Cookie"] = "__Host-mydmsvn-rt=" + cookie;
        if (headers != null) foreach (var header in headers) request.Headers[header.Key] = header.Value;
        if (csrf) { request.Headers["X-Requested-With"] = "XMLHttpRequest"; request.Headers["Sec-Fetch-Site"] = "same-origin"; }
        // Test-only pin for the explicit existing localhost TLS certificate; no runtime callback.
        request.ServerCertificateValidationCallback = (_, certificate, _, _) => certificate != null &&
            string.Equals(certificate.GetCertHashString(), thumbprint, StringComparison.OrdinalIgnoreCase);
        if (method == "POST" || method == "PUT")
        {
            var bytes = body == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(body);
            request.ContentType = "application/json; charset=utf-8"; request.ContentLength = bytes.Length;
            if (bytes.Length > 0) using (var stream = await request.GetRequestStreamAsync()) await stream.WriteAsync(bytes, 0, bytes.Length);
        }
        HttpWebResponse response;
        try { response = (HttpWebResponse)await request.GetResponseAsync(); }
        catch (WebException exception) when (exception.Response is HttpWebResponse) { response = (HttpWebResponse)exception.Response; }
        using (response)
        using (var bytes = new MemoryStream())
        {
            await response.GetResponseStream().CopyToAsync(bytes);
            return new Reply((int)response.StatusCode, response.ContentType, bytes.ToArray(), response.Headers["Location"], response.Headers["X-Correlation-ID"], response.Headers["Set-Cookie"]);
        }
    }
    private sealed class Reply
    {
        public Reply(int status, string contentType, byte[] bytes, string? location, string? correlation, string? setCookie) { Status = status; ContentType = contentType; Bytes = bytes; Body = Encoding.UTF8.GetString(bytes); Location = location; Correlation = correlation; SetCookie = setCookie; }
        public byte[] Bytes { get; }
        public int Status { get; } public string ContentType { get; } public string Body { get; } public string? Location { get; } public string? Correlation { get; }
        public string? SetCookie { get; }
        public string Cookie => SetCookie!.Split(';')[0].Substring("__Host-mydmsvn-rt=".Length);
    }
    private sealed class OwnedLogin
    {
        private readonly string _admin;
        private readonly byte[] _sid = Guid.NewGuid().ToByteArray();
        private readonly string _password = "Web2!" + Guid.NewGuid().ToString("N");
        private string? _instance;
        private bool _attempted;
        public OwnedLogin(string admin) { _admin = admin; }
        public string Name { get; } = "WEB2_Smoke_" + Guid.NewGuid().ToString("N");
        public string ConnectionString(string database) => new SqlConnectionStringBuilder(_admin) { InitialCatalog = database, IntegratedSecurity = false, Authentication = SqlAuthenticationMethod.NotSpecified, UserID = Name, Password = _password, Pooling = false }.ConnectionString;
        public async Task CreateAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var connection = new SqlConnection(_admin); await connection.OpenAsync(timeout.Token);
            using var command = connection.CreateCommand(); command.CommandTimeout = 30;
            command.CommandText = "SELECT CONVERT(nvarchar(256),SERVERPROPERTY(N'ServerName'));";
            _instance = (string)await command.ExecuteScalarAsync(timeout.Token);
            if (string.IsNullOrWhiteSpace(_instance)) throw new InvalidOperationException("Test login instance identity is missing.");
            command.CommandText = "CREATE LOGIN [" + Name + "] WITH PASSWORD=N'" + _password + "', SID=0x" + BitConverter.ToString(_sid).Replace("-", "") + ", CHECK_POLICY=OFF;";
            _attempted = true; await command.ExecuteNonQueryAsync(timeout.Token);
        }
        public async Task CleanupAsync()
        {
            if (!_attempted) return;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var connection = new SqlConnection(_admin); await connection.OpenAsync(timeout.Token);
            using var command = connection.CreateCommand(); command.CommandTimeout = 30;
            command.CommandText = "SELECT CONVERT(nvarchar(256),SERVERPROPERTY(N'ServerName'));";
            if (!string.Equals(_instance, (string)await command.ExecuteScalarAsync(timeout.Token), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test login instance changed; retain SQL resources.");
            command.Parameters.AddWithValue("@name", Name); command.Parameters.AddWithValue("@sid", _sid);
            command.CommandText = "IF EXISTS(SELECT 1 FROM sys.server_principals WHERE name=@name) BEGIN " +
                "IF NOT EXISTS(SELECT 1 FROM sys.server_principals WHERE name=@name AND sid=@sid AND type='S') THROW 51998,'Test login identity changed.',1; " +
                "IF EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE original_security_id=@sid OR security_id=@sid) THROW 51998,'Owned login sessions have not drained.',1; " +
                "DROP LOGIN [" + Name + "]; END;";
            await command.ExecuteNonQueryAsync(timeout.Token);
        }
    }
}
