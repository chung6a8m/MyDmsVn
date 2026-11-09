using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed partial class WebHostIisExpressTests
{
    private static Task RunCandidateWorkload(SqlTestDatabase database, Web6CandidateManifest manifest, string thumbprint,
        int customer, int warehouse, int employee, int product, int receiptProduct, Web6PrivateDirectoryLease workloadRoot,
        Func<Process> process, Func<Web5IisExpressIdentity> hostIdentity, string expectedQueueName) =>
        new Web6CandidateWorkloadRunner(new LiveCandidateWorkload(database, manifest, thumbprint, customer, warehouse,
            employee, product, receiptProduct, workloadRoot, process, hostIdentity, expectedQueueName)).Execute(CancellationToken.None);

    private sealed class LiveCandidateWorkload : IWeb6CandidateWorkloadOperations
    {
        private const string Origin = "https://localhost:44321";
        private readonly SqlTestDatabase _database;
        private readonly Web6CandidateManifest _manifest;
        private readonly string _thumbprint;
        private readonly int _customer, _warehouse, _employee, _product, _receiptProduct;
        private readonly Func<Process> _process;
        private readonly Func<Web5IisExpressIdentity> _hostIdentity;
        private readonly Web6PrivateDirectoryLease _workloadRoot;
        private readonly string _expectedQueueName;
        private readonly object _gate = new object();
        private readonly List<Guid> _operations = new List<Guid>();
        private string? _access, _cookie;
        private Guid _epoch;
        private long _deadlocksAtAdmission;
        private TimeSpan _cpuAtAdmission;
        private long _clockAtAdmission;

        internal LiveCandidateWorkload(SqlTestDatabase database, Web6CandidateManifest manifest, string thumbprint,
            int customer, int warehouse, int employee, int product, int receiptProduct, Web6PrivateDirectoryLease workloadRoot,
            Func<Process> process, Func<Web5IisExpressIdentity> hostIdentity, string expectedQueueName)
        {
            _database = database; _manifest = manifest; _thumbprint = thumbprint;
            _customer = customer; _warehouse = warehouse; _employee = employee; _product = product;
            _receiptProduct = receiptProduct; _workloadRoot = workloadRoot; _process = process;
            _hostIdentity = hostIdentity; _expectedQueueName = expectedQueueName;
        }

        public async Task<Web6WorkloadAdmission> VerifyAdmission(CancellationToken token)
        {
            var cohort = Environment.GetEnvironmentVariable("MYDMSVN_WEB6_WORKLOAD_ACTOR_COHORT");
            _workloadRoot.Verify();
            _manifest.VerifyForWorkload(Environment.GetEnvironmentVariable("MYDMSVN_WEB6_CANDIDATE_ID")!);
            using (var proof = _manifest.CreatePrivateOutput("workload-admission.json"))
            {
                proof.Replace(new UTF8Encoding(false, true).GetBytes("{\"admittedWorkers\":1}"));
                proof.Verify();
            }
            using (var sql = new SqlConnection(_database.ConnectionString))
            {
                await sql.OpenAsync(token);
                await _database.VerifyOwnershipAsync(sql, token);
                _epoch = await sql.QuerySingleAsync<Guid>(new SqlTestCommand(
                    "SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;", cancellationToken: token));
                _deadlocksAtAdmission = await Deadlocks(sql, token);
            }
            var worker = _process(); worker.Refresh();
            _cpuAtAdmission = worker.TotalProcessorTime; _clockAtAdmission = Stopwatch.GetTimestamp();
            var credentials = JsonConvert.SerializeObject(new { username = "web.smoke", password = "IisSmoke_test-only-password_2026!" });
            var signedIn = await Request(Origin + "/api/v1/auth/login", _thumbprint, origin: Origin, method: "POST", body: credentials, csrf: true);
            Require200(signedIn, "workload login");
            _access = "Bearer " + (string)JObject.Parse(signedIn.Body)["data"]!["accessToken"]!;
            _cookie = signedIn.Cookie;
            return new Web6WorkloadAdmission(!worker.HasExited, 1, true, true,
                string.Equals(cohort, "web.smoke", StringComparison.Ordinal), true);
        }

        public async Task<Web6WorkloadHealth> Health(CancellationToken token)
        {
            var expectedCandidate = Environment.GetEnvironmentVariable("MYDMSVN_WEB6_CANDIDATE_ID")!;
            _manifest.VerifyForWorkload(expectedCandidate);
            var worker = _process(); worker.Refresh();
            var queue = await Web5IisExpressHostAdmission.VerifyAsync(_hostIdentity(), token);
            var reply = await Request(Origin + "/api/v1/health", _thumbprint);
            var healthy = reply.Status == 200 && reply.Body == "{\"data\":{\"status\":\"ok\"}}";
            return new Web6WorkloadHealth(healthy, string.Equals(queue.Name, _expectedQueueName, StringComparison.Ordinal),
                worker.HasExited ? 0 : queue.AttachedProcessCount, true);
        }

        public async Task<IReadOnlyDictionary<Web6MetricClass, Web6MetricSummary>> Smoke(
            IReadOnlyList<Web6MetricClass> requiredClasses, CancellationToken token)
        {
            if (!requiredClasses.SequenceEqual(Web6CandidateWorkloadPlan.RequiredClasses))
                throw new InvalidDataException("WEB6 workload smoke must cover all critical and supporting classes separately.");
            var samples = new List<Web6MetricSample>();
            await CriticalCycle(samples, token);
            await SupportingCycle(samples, token);
            var summary = Web6CandidateMetrics.Summarize(samples, Stopwatch.Frequency,
                Math.Max(1, samples.Max(sample => sample.EndedTimestamp) - samples.Min(sample => sample.StartedTimestamp)), 1);
            if (Web6CandidateWorkloadPlan.RequiredClasses.Any(metricClass => summary[metricClass].Count != 1))
                throw new InvalidDataException("WEB6 workload smoke omitted a required class.");
            return summary;
        }

        public async Task<IReadOnlyDictionary<Web6MetricClass, Web6MetricSummary>> Measure(
            int concurrency, int desiredSamples, CancellationToken token)
        {
            if (!Web6CandidateWorkloadPlan.ConcurrencyProfiles.Contains(concurrency))
                throw new InvalidDataException("WEB6 workload concurrency profile is not approved.");
            var started = Stopwatch.GetTimestamp();
            var samples = new List<Web6MetricSample>();
            await Web6BoundedSampleLoop.Execute(concurrency, desiredSamples, Stopwatch.Frequency,
                checked(Stopwatch.Frequency * Web6CandidateWorkloadPlan.ProfileBudgetSeconds), Stopwatch.GetTimestamp,
                async size =>
                {
                    await Task.WhenAll(Enumerable.Range(0, size).Select(_ => CriticalCycle(samples, token)));
                    for (var index = 0; index < size; index++) await SupportingCycle(samples, token);
                    return size;
                },
                async () => Web6CandidateWorkloadGuard.RequireContinue(await Observe(token)));
            var elapsed = Math.Max(1, Stopwatch.GetTimestamp() - started);
            return Web6CandidateMetrics.Summarize(samples, Stopwatch.Frequency, elapsed, desiredSamples);
        }

        public async Task<Web6WorkloadObservation> Observe(CancellationToken token)
        {
            Guid[] operations; lock (_gate) operations = _operations.ToArray();
            int journalRows, duplicateKeys, locks, sessions;
            long journalBytes, deadlocks;
            using (var sql = new SqlConnection(_database.ConnectionString))
            {
                await sql.OpenAsync(token);
                await _database.VerifyOwnershipAsync(sql, token);
                journalRows = operations.Length == 0 ? 0 : await sql.QuerySingleAsync<int>(new SqlTestCommand(
                    "SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE OperationId IN @operations;", new { operations }, cancellationToken: token));
                duplicateKeys = operations.Length == 0 ? 0 : await sql.QuerySingleAsync<int>(new SqlTestCommand(
                    "SELECT COUNT(*) FROM (SELECT OperationId FROM dbo.WebOperationJournal WHERE OperationId IN @operations GROUP BY OperationId HAVING COUNT(*)<>1) d;",
                    new { operations }, cancellationToken: token));
                journalBytes = await sql.QuerySingleAsync<long>(new SqlTestCommand(
                    "SELECT COALESCE(SUM(CONVERT(bigint,DATALENGTH(RequestFingerprint))+COALESCE(DATALENGTH(ResultBytes),0)),0) FROM dbo.WebOperationJournal;",
                    cancellationToken: token));
                locks = await sql.QuerySingleAsync<int>(new SqlTestCommand(
                    "SELECT COUNT(*) FROM sys.dm_tran_locks WHERE resource_database_id=DB_ID();", cancellationToken: token));
                sessions = await sql.QuerySingleAsync<int>(new SqlTestCommand(
                    "SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE database_id=DB_ID();", cancellationToken: token));
                deadlocks = await Deadlocks(sql, token) - _deadlocksAtAdmission;
            }
            var worker = _process(); worker.Refresh();
            var elapsedSeconds = Math.Max(1d / Stopwatch.Frequency,
                (Stopwatch.GetTimestamp() - _clockAtAdmission) / (double)Stopwatch.Frequency);
            var cpu = Math.Max(0, (worker.TotalProcessorTime - _cpuAtAdmission).TotalSeconds /
                elapsedSeconds / Math.Max(1, Environment.ProcessorCount) * 100d);
            var runaway = worker.PrivateMemorySize64 > 2L * 1024 * 1024 * 1024 || locks > 10000 || sessions > 500 || journalBytes > 512L * 1024 * 1024;
            return new Web6WorkloadObservation(journalRows != operations.Length || duplicateKeys != 0,
                checked((int)Math.Max(0, deadlocks)), runaway, cpu, worker.PrivateMemorySize64, locks, sessions, journalBytes);
        }

        public Task SealForOuterCleanup(bool completed, CancellationToken token)
        {
            _workloadRoot.Verify();
            return Task.CompletedTask;
        }

        public Task Complete(IReadOnlyList<Web6WorkloadProfileResult> profiles, CancellationToken token)
        {
            Console.WriteLine("WEB6_WORKLOAD_SUMMARY " + new JObject
            {
                ["scriptVersion"] = 1,
                ["profiles"] = new JArray(profiles.Select(ProfileJson)),
                ["criticalClasses"] = new JArray(Web6CandidateWorkloadPlan.CriticalClasses.Select(value => value.ToString())),
                ["supportingClasses"] = new JArray(Web6CandidateWorkloadPlan.SupportingClasses.Select(value => value.ToString())),
                ["desiredSamplesPerClass"] = Web6CandidateWorkloadPlan.DesiredSamplesPerCriticalClass,
                ["profileBudgetSeconds"] = Web6CandidateWorkloadPlan.ProfileBudgetSeconds,
                ["performanceClaim"] = "NONE; bounded observations only",
            }.ToString(Formatting.None));
            return Task.CompletedTask;
        }

        private async Task CriticalCycle(List<Web6MetricSample> samples, CancellationToken token)
        {
            var saleCreate = Guid.NewGuid();
            var saleBody = new JObject
            {
                ["saleDate"] = "2024-02-29", ["customerId"] = _customer, ["warehouseId"] = _warehouse, ["employeeId"] = _employee,
                ["lines"] = new JArray(new JObject { ["saleLineId"] = "0", ["productId"] = _product,
                    ["qty"] = "1.0000", ["unitPrice"] = "25.0000", ["isGift"] = false }),
            };
            var sale = Data(await Timed(Web6MetricClass.SalesCreate, samples, () => Mutation("/api/v1/sales-receipts", "POST", saleBody, saleCreate)));
            var saleId = (string)sale["saleId"]!;
            saleBody["expectedVersion"] = sale["version"]!.DeepClone();
            saleBody["note"] = "WEB6 bounded workload";
            saleBody["lines"]![0]!["saleLineId"] = sale["lines"]![0]!["saleLineId"]!.DeepClone();
            var saleUpdate = Guid.NewGuid();
            sale = Data(await Timed(Web6MetricClass.SalesUpdate, samples,
                () => Mutation("/api/v1/sales-receipts/" + saleId + "/draft", "PUT", saleBody, saleUpdate)));
            var salePost = Guid.NewGuid();
            await Timed(Web6MetricClass.SalesPost, samples, () => Mutation("/api/v1/sales-receipts/" + saleId + "/post", "POST",
                new JObject { ["expectedVersion"] = sale["version"]!.DeepClone() }, salePost));

            var receiptCreate = Guid.NewGuid();
            var receiptBody = new JObject
            {
                ["receiptDate"] = "2024-02-29", ["warehouseId"] = _warehouse, ["employeeId"] = _employee,
                ["lines"] = new JArray(new JObject { ["productId"] = _receiptProduct, ["quantity"] = "1.0000", ["unitCost"] = "12.0000" }),
            };
            var receipt = Data(await Timed(Web6MetricClass.ReceiptCreate, samples,
                () => Mutation("/api/v1/goods-receipts", "POST", receiptBody, receiptCreate)));
            var receiptId = (string)receipt["receiptId"]!;
            receiptBody["expectedVersion"] = receipt["version"]!.DeepClone();
            receiptBody["note"] = "WEB6 bounded workload";
            var receiptUpdate = Guid.NewGuid();
            receipt = Data(await Timed(Web6MetricClass.ReceiptUpdate, samples,
                () => Mutation("/api/v1/goods-receipts/" + receiptId + "/draft", "PUT", receiptBody, receiptUpdate)));
            var receiptPost = Guid.NewGuid();
            await Timed(Web6MetricClass.ReceiptPost, samples, () => Mutation("/api/v1/goods-receipts/" + receiptId + "/post", "POST",
                new JObject { ["expectedVersion"] = receipt["version"]!.DeepClone() }, receiptPost));
            lock (_gate) _operations.AddRange(new[] { saleCreate, saleUpdate, salePost, receiptCreate, receiptUpdate, receiptPost });
        }

        private async Task SupportingCycle(List<Web6MetricSample> samples, CancellationToken token)
        {
            var refresh = await Timed(Web6MetricClass.AuthRefresh, samples, () =>
                Request(Origin + "/api/v1/auth/refresh", _thumbprint, origin: Origin, method: "POST", cookie: _cookie, csrf: true));
            _cookie = refresh.Cookie;
            _access = "Bearer " + (string)JObject.Parse(refresh.Body)["data"]!["accessToken"]!;
            Guid operation; lock (_gate) operation = _operations.Last();
            string fingerprint;
            using (var sql = new SqlConnection(_database.ConnectionString))
            {
                await sql.OpenAsync(token);
                fingerprint = await sql.QuerySingleAsync<string>(new SqlTestCommand(
                    "SELECT CONVERT(varchar(64),RequestFingerprint,2) FROM dbo.WebOperationJournal WHERE OperationId=@operation;",
                    new { operation }, cancellationToken: token));
                fingerprint = fingerprint.ToLowerInvariant();
            }
            await Timed(Web6MetricClass.ProtectedStatus, samples, () => Request(
                Origin + "/api/v2/operations/" + operation.ToString("D") + "/resolution", _thumbprint, _access,
                headers: new Dictionary<string, string> { ["X-MyDmsVn-Deployment-Epoch"] = _epoch.ToString("D"), ["X-MyDmsVn-Operation-Fingerprint"] = fingerprint }));
            await Timed(Web6MetricClass.ProtectedEligibility, samples, () =>
                Request(Origin + "/api/v3/operations/resend-context", _thumbprint, _access));
            await Timed(Web6MetricClass.StockRead, samples, () => Request(
                Origin + "/api/v1/inventory/balances?warehouseId=" + _warehouse.ToString(CultureInfo.InvariantCulture) + "&pageNumber=1&pageSize=25",
                _thumbprint, _access));
        }

        private Task<Reply> Mutation(string path, string method, JObject body, Guid operation) => Request(Origin + path,
            _thumbprint, _access, Origin, method, body.ToString(Formatting.None), csrf: true,
            headers: new Dictionary<string, string> { ["Idempotency-Key"] = operation.ToString("D"),
                ["X-MyDmsVn-Deployment-Epoch"] = _epoch.ToString("D") });

        private static JToken Data(Reply reply) => JObject.Parse(reply.Body)["data"]!;

        private static async Task<Reply> Timed(Web6MetricClass metricClass, List<Web6MetricSample> samples, Func<Task<Reply>> action)
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                var reply = await action();
                var outcome = reply.Status == 200 ? Web6MetricOutcome.Success : Web6MetricOutcome.Error;
                lock (samples) samples.Add(new Web6MetricSample(metricClass, started, Stopwatch.GetTimestamp(), outcome));
                Require200(reply, metricClass.ToString());
                return reply;
            }
            catch (System.Net.WebException error)
            {
                lock (samples) samples.Add(new Web6MetricSample(metricClass, started, Stopwatch.GetTimestamp(),
                    error.Status == System.Net.WebExceptionStatus.Timeout ? Web6MetricOutcome.Timeout : Web6MetricOutcome.Error));
                throw;
            }
        }

        private static void Require200(Reply reply, string operation)
        {
            if (reply.Status != 200) throw new InvalidOperationException(operation + " failed with HTTP " + reply.Status.ToString(CultureInfo.InvariantCulture) + ".");
        }

        private static async Task<long> Deadlocks(SqlConnection sql, CancellationToken token) =>
            await sql.QuerySingleAsync<long>(new SqlTestCommand(
                "SELECT COALESCE(SUM(CONVERT(bigint,cntr_value)),0) FROM sys.dm_os_performance_counters WHERE counter_name LIKE N'Number of Deadlocks/sec%';",
                cancellationToken: token));

        private static JObject ProfileJson(Web6WorkloadProfileResult profile)
        {
            var classes = new JObject();
            foreach (var entry in profile.Metrics) classes[entry.Key.ToString()] = new JObject
            {
                ["count"] = entry.Value.Count, ["p50Ms"] = entry.Value.P50Ms, ["p95Ms"] = entry.Value.P95Ms,
                ["p99Ms"] = entry.Value.P99Ms, ["errors"] = entry.Value.Errors, ["timeouts"] = entry.Value.Timeouts,
                ["throughputPerSecond"] = entry.Value.ThroughputPerSecond, ["disposition"] = entry.Value.Disposition,
            };
            return new JObject
            {
                ["profile"] = profile.Concurrency == 0 ? "smoke" : "measurement",
                ["concurrency"] = profile.Concurrency,
                ["classes"] = classes,
                ["resources"] = new JObject
                {
                    ["cpuPercent"] = profile.Resources.CpuPercent, ["privateBytes"] = profile.Resources.PrivateBytes,
                    ["sqlLocks"] = profile.Resources.SqlLocks, ["deadlocks"] = profile.Resources.Deadlocks,
                    ["poolConnections"] = profile.Resources.PoolConnections, ["journalBytes"] = profile.Resources.JournalBytes,
                    ["duplicateEffect"] = profile.Resources.DuplicateEffect, ["resourceRunaway"] = profile.Resources.ResourceRunaway,
                },
            };
        }
    }
}
