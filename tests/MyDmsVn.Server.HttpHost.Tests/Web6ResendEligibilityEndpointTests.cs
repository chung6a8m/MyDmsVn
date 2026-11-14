using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Application.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.HttpHost.Tests;

public sealed class Web6ResendEligibilityEndpointTests
{
    private static readonly Guid Epoch = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly Guid Operation = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
    private const string Fingerprint = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";

    public static IEnumerable<object[]> Kinds()
    {
        yield return new object[] { "sales-create", "Sales.Create", "" };
        yield return new object[] { "sales-update", "Sales.Update", "9007199254740993" };
        yield return new object[] { "sales-post", "Sales.Post", "9007199254740993" };
        yield return new object[] { "receipt-create", "Receipt.Create", "" };
        yield return new object[] { "receipt-update", "Receipt.Update", "9007199254740993" };
        yield return new object[] { "receipt-post", "Receipt.Post", "9007199254740993" };
    }

    [Fact]
    public async Task Enabled_candidate_context_matches_additive_corpus_and_is_not_cached()
    {
        using var probe = new Probe();
        using var response = await probe.Get("resend-context");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(Compact(Corpus("context.json")), Compact(await response.Content.ReadAsStringAsync()));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task Disabled_and_old_modes_do_not_expose_v3(bool web5, bool web6)
    {
        using var host = new AuthFixture(web5Enabled: web5, web6ResendEligibilityEnabled: web6,
            web6CandidateId: web6 ? "candidate-1" : null);
        using var login = await host.Login();
        var token = (string)JObject.Parse(await login.Content.ReadAsStringAsync())["data"]!["accessToken"]!;
        using var request = new HttpRequestMessage(HttpMethod.Get, AuthFixture.Origin + "/api/v3/operations/resend-context");
        request.Headers.Add("Authorization", "Bearer " + token);
        using var response = await host.Server.HttpClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(JObject.Parse(await response.Content.ReadAsStringAsync())["data"]);
    }

    [Theory, MemberData(nameof(Kinds))]
    public async Task All_six_receipts_match_corpus_and_authorize_the_original_kind(string fixture, string kind, string target)
    {
        using var probe = new Probe();
        using var response = await probe.Get(Operation.ToString("D") + "/resend-receipt", request =>
        {
            request.Headers.Add("X-MyDmsVn-Deployment-Epoch", Epoch.ToString("D"));
            request.Headers.Add("X-MyDmsVn-Operation-Fingerprint", Fingerprint);
            request.Headers.Add("X-MyDmsVn-Operation-Kind", kind);
            if (target.Length != 0) request.Headers.Add("X-MyDmsVn-Operation-Target-Id", target);
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = JObject.Parse(await response.Content.ReadAsStringAsync());
        var receipt = (string)actual["data"]!["receipt"]!;
        Assert.InRange(receipt.Length, 1, 4096);
        actual["data"]!["receipt"] = "<receipt>";
        Assert.Equal(Compact(Corpus(fixture + "-receipt.json")), actual.ToString(Formatting.None));
        Assert.Contains(WebOperationWireInput.ParseKind(kind), probe.AuthorizedKinds);
        Assert.DoesNotContain("private", actual.ToString(Formatting.None), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("missing-epoch")]
    [InlineData("missing-kind")]
    [InlineData("target-on-create")]
    [InlineData("body")]
    [InlineData("query")]
    public async Task Malformed_receipt_request_is_rejected_before_repository_admission(string defect)
    {
        using var probe = new Probe();
        var route = Operation.ToString("D") + "/resend-receipt" + (defect == "query" ? "?x=1" : "");
        using var response = await probe.Get(route, request =>
        {
            if (defect != "missing-epoch") request.Headers.Add("X-MyDmsVn-Deployment-Epoch", Epoch.ToString("D"));
            request.Headers.Add("X-MyDmsVn-Operation-Fingerprint", Fingerprint);
            if (defect != "missing-kind") request.Headers.Add("X-MyDmsVn-Operation-Kind", "Sales.Create");
            if (defect == "target-on-create") request.Headers.Add("X-MyDmsVn-Operation-Target-Id", "1");
            if (defect == "body") request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(probe.AuthorizedKinds);
    }

    public static IEnumerable<object[]> Decisions()
    {
        yield return new object[] { "allow", null!, "ALLOW", "ORIGINAL_NOT_OBSERVED_RECEIPT_VALID" };
        yield return new object[] { "committed-result-available", Resolution("COMMITTED", true), "DENY", "COMMITTED_RESULT_AVAILABLE" };
        yield return new object[] { "committed-result-unavailable", Resolution("RESULT_UNAVAILABLE"), "DENY", "COMMITTED_RESULT_UNAVAILABLE" };
        yield return new object[] { "result-expired", Resolution("RESULT_EXPIRED"), "DENY", "RESULT_EXPIRED" };
        yield return new object[] { "not-executed", Resolution("NOT_EXECUTED", terminal: true), "DENY", "NOT_EXECUTED" };
        yield return new object[] { "terminal-audit-incomplete", Resolution("INDETERMINATE"), "INDETERMINATE", "TERMINAL_AUDIT_INCOMPLETE" };
    }

    [Theory, MemberData(nameof(Decisions))]
    public async Task Eligibility_decisions_match_additive_corpus_without_outcome_or_terminal(string fixture,
        WebOperationResolution? resolution, string decision, string reason)
    {
        using var probe = new Probe { Resolution = resolution };
        var receipt = await probe.Receipt();
        using var response = await probe.Eligibility(receipt);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = Compact(await response.Content.ReadAsStringAsync());
        Assert.Equal(Compact(Corpus("eligibility-" + fixture + ".json")), body);
        Assert.Equal(decision, (string)JObject.Parse(body)["data"]!["decision"]!);
        Assert.Equal(reason, (string)JObject.Parse(body)["data"]!["reason"]!);
        var data = (JObject)JObject.Parse(body)["data"]!;
        Assert.Null(data.Property("outcome"));
        Assert.Null(data.Property("terminal"));
        Assert.Equal(1, probe.EligibilityReads);
    }

    [Fact]
    public async Task Eligibility_repository_failure_maps_to_unavailable_503_without_private_details()
    {
        using var probe = new Probe { FailEligibilityRead = true };
        var receipt = await probe.Receipt();
        using var context = await probe.Get("resend-context");
        Assert.Equal(HttpStatusCode.OK, context.StatusCode);
        using var response = await probe.Eligibility(receipt);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var envelope = JObject.Parse(body);
        Assert.Equal("Operation.Unavailable", (string?)envelope["error"]!["code"]);
        Assert.Empty((JArray)envelope["error"]!["details"]!);
        Assert.Null(envelope["data"]);
        Assert.DoesNotContain("private-journal-read-denied", body);
        Assert.Equal(1, probe.EligibilityReads);
    }

    [Fact]
    public async Task Correctly_bound_expired_receipt_denies_without_treating_absence_as_rollback_proof()
    {
        using var probe = new Probe();
        probe.Clock.Now = probe.Clock.Now.AddMinutes(-1);
        var receipt = await probe.Receipt();
        probe.Clock.Now = probe.Clock.Now.AddSeconds(Web6ResendReceiptCodec.LifetimeSeconds);
        probe.ResetAccess();
        using var response = await probe.Eligibility(receipt);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Compact(Corpus("eligibility-receipt-expired.json")), Compact(await response.Content.ReadAsStringAsync()));
        Assert.Empty(probe.EligibilityKinds);
        Assert.Contains(WebOperationKind.SalesCreate, probe.AuthorizedKinds);
    }

    [Fact]
    public async Task Receipt_expiring_while_waiting_on_protected_state_cannot_return_allow()
    {
        using var probe = new Probe { ExpireDuringEligibilityRead = true };
        var receipt = await probe.Receipt();
        using var response = await probe.Eligibility(receipt);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Compact(Corpus("eligibility-expired-during-read.json")),
            Compact(await response.Content.ReadAsStringAsync()));
        Assert.Equal(1, probe.EligibilityReads);
    }

    [Fact]
    public async Task Receipt_expiring_during_protected_read_denies_even_when_resolution_is_returned()
    {
        using var probe = new Probe
        {
            ExpireDuringEligibilityRead = true,
            Resolution = Resolution("COMMITTED", result: true)
        };
        var receipt = await probe.Receipt();
        using var response = await probe.Eligibility(receipt);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Compact(Corpus("eligibility-expired-during-read.json")),
            Compact(await response.Content.ReadAsStringAsync()));
        Assert.Equal(1, probe.EligibilityReads);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("malformed")]
    [InlineData("wrong-signature")]
    public async Task Invalid_receipt_never_reaches_current_authority_or_protected_state(string defect)
    {
        using var probe = new Probe();
        var receipt = await probe.Receipt();
        using var response = await probe.Eligibility(receipt, request =>
        {
            request.Headers.Remove("X-MyDmsVn-Resend-Receipt");
            if (defect == "duplicate") request.Headers.TryAddWithoutValidation("X-MyDmsVn-Resend-Receipt", new[] { receipt, receipt });
            else if (defect == "malformed") request.Headers.Add("X-MyDmsVn-Resend-Receipt", "not-a-jws");
            else if (defect == "wrong-signature") request.Headers.Add("X-MyDmsVn-Resend-Receipt", receipt.Substring(0, receipt.Length - 1) + (receipt.EndsWith("A", StringComparison.Ordinal) ? "B" : "A"));
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(probe.AuthorizedKinds);
        Assert.Empty(probe.EligibilityKinds);
    }

    [Fact]
    public async Task Signed_receipt_from_another_candidate_conflicts_without_protected_state_disclosure()
    {
        using var source = new Probe("candidate-2");
        var receipt = await source.Receipt();
        using var target = new Probe();
        using var response = await target.Eligibility(receipt);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Idempotency.KeyConflict", (string)JObject.Parse(await response.Content.ReadAsStringAsync())["error"]!["code"]!);
        Assert.Empty(target.AuthorizedKinds);
        Assert.Empty(target.EligibilityKinds);
    }

    private static WebOperationResolution Resolution(string state, bool result = false, bool terminal = false)
    {
        var completed = new DateTime(2026, 10, 31, 8, 0, 0, DateTimeKind.Utc);
        var status = new WebOperationStatus(Operation, Epoch, WebOperationKind.SalesCreate, null,
            Enumerable.Range(0, 32).Select(value => (byte)value).ToArray(), result ? 42 : (long?)null,
            completed, state, result ? Encoding.UTF8.GetBytes("{}") : null);
        return new WebOperationResolution(status, terminal
            ? new WebOperationTerminalDecision(Guid.Parse("12345678-1234-4123-8123-123456789abc"), "CASE-1", completed)
            : null);
    }

    private static string Corpus(string name)
    {
        for (var root = new DirectoryInfo(AppContext.BaseDirectory); root != null; root = root.Parent)
        {
            var path = Path.Combine(root.FullName, "docs", "contracts", "web6-resend-eligibility-v1", name);
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(name);
    }

    private static string Compact(string json) => JToken.Parse(json).ToString(Formatting.None);

    private sealed class Probe : IDisposable, IUnitOfWorkFactory, IWebOperationReadRepository, IWebOperationResendEligibilityRepository
    {
        private readonly AuthFixture _host;
        private string? _token;
        internal readonly List<WebOperationKind> AuthorizedKinds = new List<WebOperationKind>();
        internal readonly List<WebOperationKind> EligibilityKinds = new List<WebOperationKind>();
        internal WebOperationResolution? Resolution;
        internal int EligibilityReads;
        internal bool ExpireDuringEligibilityRead;
        internal bool FailEligibilityRead;
        internal AuthClock Clock => _host.Clock;
        internal void ResetAccess() => _token = null;

        internal Probe(string candidate = "candidate-1")
        {
            _host = new AuthFixture(services => services.AddSingleton<IUnitOfWorkFactory>(this), web5Enabled: true,
                web6ResendEligibilityEnabled: true, web6CandidateId: candidate);
        }

        internal async Task<HttpResponseMessage> Get(string route, Action<HttpRequestMessage>? configure = null)
        {
            if (_token == null)
            {
                using var login = await _host.Login();
                _token = (string)JObject.Parse(await login.Content.ReadAsStringAsync())["data"]!["accessToken"]!;
            }
            using var request = new HttpRequestMessage(HttpMethod.Get, AuthFixture.Origin + "/api/v3/operations/" + route);
            request.Headers.Add("Authorization", "Bearer " + _token);
            configure?.Invoke(request);
            return await _host.Server.HttpClient.SendAsync(request);
        }

        internal async Task<string> Receipt()
        {
            using var response = await Get(Operation.ToString("D") + "/resend-receipt", IdentityHeaders);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AuthorizedKinds.Clear();
            return (string)JObject.Parse(await response.Content.ReadAsStringAsync())["data"]!["receipt"]!;
        }

        internal Task<HttpResponseMessage> Eligibility(string receipt, Action<HttpRequestMessage>? configure = null) =>
            Get(Operation.ToString("D") + "/resend-eligibility", request =>
            {
                IdentityHeaders(request);
                request.Headers.Add("X-MyDmsVn-Resend-Receipt", receipt);
                configure?.Invoke(request);
            });

        private static void IdentityHeaders(HttpRequestMessage request)
        {
            request.Headers.Add("X-MyDmsVn-Deployment-Epoch", Epoch.ToString("D"));
            request.Headers.Add("X-MyDmsVn-Operation-Fingerprint", Fingerprint);
            request.Headers.Add("X-MyDmsVn-Operation-Kind", "Sales.Create");
        }

        public IUnitOfWork Create() => new Uow(this);
        public Task<IUnitOfWork> CreateAsync(CancellationToken cancellationToken) => Task.FromResult<IUnitOfWork>(Create());
        public Task<Guid> ReadContextAsync(int actorUserId, Guid sessionId, CancellationToken cancellationToken) => Task.FromResult(Epoch);
        public Task<WebOperationStatus?> ReadStatusAsync(int actorUserId, Guid sessionId, Guid deploymentEpoch,
            Guid operationId, byte[] expectedFingerprint, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AuthorizeReceiptAsync(int actorUserId, Guid sessionId, Guid deploymentEpoch, WebOperationKind kind, CancellationToken cancellationToken)
        { AuthorizedKinds.Add(kind); return Task.CompletedTask; }
        public Task<WebOperationResolution?> ReadEligibilityAsync(int actorUserId, Guid sessionId, Guid deploymentEpoch,
            Guid operationId, WebOperationKind expectedKind, long? expectedTargetId, byte[] expectedFingerprint, CancellationToken cancellationToken)
        {
            EligibilityReads++;
            EligibilityKinds.Add(expectedKind);
            if (FailEligibilityRead) throw new InvalidOperationException("private-journal-read-denied");
            if (ExpireDuringEligibilityRead) Clock.Now = Clock.Now.AddSeconds(Web6ResendReceiptCodec.LifetimeSeconds);
            return Task.FromResult(Resolution);
        }
        public void Dispose() => _host.Dispose();

        private sealed class Uow : IUnitOfWork
        {
            private readonly Probe _probe;
            internal Uow(Probe probe) { _probe = probe; }
            public UnitOfWorkState State { get; private set; } = UnitOfWorkState.Created;
            public void BeginTransaction() => State = UnitOfWorkState.ActiveTransaction;
            public TRepository Repository<TRepository>() where TRepository : class =>
                _probe as TRepository ?? throw new InvalidOperationException(typeof(TRepository).Name);
            public void Commit() => State = UnitOfWorkState.Committed;
            public void Rollback() => State = UnitOfWorkState.RolledBack;
            public void Dispose() { if (State == UnitOfWorkState.ActiveTransaction) Rollback(); State = UnitOfWorkState.Disposed; }
        }
    }
}
