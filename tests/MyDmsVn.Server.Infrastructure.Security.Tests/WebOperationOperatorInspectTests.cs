using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.WebOperation.Operator;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebOperationOperatorInspectTests
{
    private const string Epoch = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
    private const string Operation = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
    private const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData("--help")]
    [InlineData("inspect", "--help")]
    public void Help_is_an_explicit_sql_free_parse_result(params string[] args)
    {
        Assert.True(InspectOptions.TryParse(args, out var options, out var error));
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.True(options!.HelpRequested);
    }

    [Fact]
    public void Exact_inspect_arguments_are_strictly_parsed()
    {
        Assert.True(InspectOptions.TryParse(Valid(), out var options, out var error));
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.False(options!.HelpRequested);
        Assert.Equal("owned-test", options.EnvironmentName);
        Assert.Equal("MyDmsVn_Test_Operator", options.DatabaseName);
        Assert.Equal(Guid.Parse(Epoch), options.DeploymentEpoch);
        Assert.Equal(42, options.ActorUserId);
        Assert.Equal(Guid.Parse(Operation), options.OperationId);
        Assert.Equal(Fingerprint, options.FingerprintHex);
        Assert.Equal(15, options.TimeoutSeconds);
    }

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public void Invalid_or_ambiguous_arguments_fail_before_sql(string[] args)
    {
        Assert.False(InspectOptions.TryParse(args, out var options, out var error));
        Assert.Null(options);
        Assert.False(string.IsNullOrWhiteSpace(error));
        Assert.DoesNotContain("Server=", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Exit_codes_are_stable_and_distinct()
    {
        Assert.Equal(0, (int)OperatorExitCode.Completed);
        Assert.Equal(2, (int)OperatorExitCode.InvalidArguments);
        Assert.Equal(3, (int)OperatorExitCode.Denied);
        Assert.Equal(4, (int)OperatorExitCode.Indeterminate);
        Assert.Equal(5, (int)OperatorExitCode.EnvironmentFailure);
        Assert.Equal(6, (int)OperatorExitCode.Timeout);
        Assert.Equal(130, (int)OperatorExitCode.Cancelled);
    }

    [Fact]
    public async Task Help_does_not_construct_a_SQL_client()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await OperatorInspectApplication.RunAsync(new[] { "--help" },
            _ => throw new InvalidOperationException("client must not be created"), stdout, stderr, CancellationToken.None);
        Assert.Equal((int)OperatorExitCode.Completed, exit);
        Assert.Contains("inspect --environment", stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Theory]
    [InlineData("COMMITTED", 0)]
    [InlineData("EXPIRED", 0)]
    [InlineData("CANCELLED", 0)]
    [InlineData("INDETERMINATE", 4)]
    public async Task Status_output_is_bounded_and_exit_code_is_stable(string disposition, int expectedExit)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var result = Inspection(disposition);
        var exit = await OperatorInspectApplication.RunAsync(Valid(), _ => new FakeClient(result), stdout, stderr, CancellationToken.None);
        Assert.Equal(expectedExit, exit);
        Assert.Contains("status=" + disposition, stdout.ToString());
        Assert.Contains("environment=owned-test", stdout.ToString());
        Assert.Contains("database=MyDmsVn_Test_Operator", stdout.ToString());
        Assert.DoesNotContain(Fingerprint, stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-payload", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Theory]
    [InlineData("Auth.Forbidden", 3)]
    [InlineData("Deployment.EpochMismatch", 3)]
    [InlineData("Idempotency.FingerprintMismatch", 3)]
    [InlineData("Operation.Unavailable", 5)]
    public async Task Operation_failures_are_sanitized(string code, int expectedExit)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await OperatorInspectApplication.RunAsync(Valid(), _ => new FakeClient(new WebOperationException(code)), stdout, stderr, CancellationToken.None);
        Assert.Equal(expectedExit, exit);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.DoesNotContain(Fingerprint, stderr.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-payload", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unexpected_environment_failure_does_not_echo_exception_or_secrets()
    {
        var stderr = new StringWriter();
        var exit = await OperatorInspectApplication.RunAsync(Valid(),
            _ => new FakeClient(new InvalidOperationException("Server=secret;Password=private-payload")),
            new StringWriter(), stderr, CancellationToken.None);
        Assert.Equal((int)OperatorExitCode.EnvironmentFailure, exit);
        Assert.DoesNotContain("Server=", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-payload", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Caller_cancellation_is_distinct_from_timeout()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var callerExit = await OperatorInspectApplication.RunAsync(Valid(), _ => new FakeClient(waitForCancellation: true),
            new StringWriter(), new StringWriter(), cancelled.Token);
        Assert.Equal((int)OperatorExitCode.Cancelled, callerExit);

        var timeoutArgs = Replace("--timeout-seconds", "1");
        var timeoutExit = await OperatorInspectApplication.RunAsync(timeoutArgs, _ => new FakeClient(waitForCancellation: true),
            new StringWriter(), new StringWriter(), CancellationToken.None);
        Assert.Equal((int)OperatorExitCode.Timeout, timeoutExit);
    }

    public static IEnumerable<object[]> InvalidArguments()
    {
        yield return new object[] { Array.Empty<string>() };
        yield return new object[] { new[] { "unknown" } };
        yield return new object[] { Replace("--actor", "0") };
        yield return new object[] { Replace("--actor", "01") };
        yield return new object[] { Replace("--epoch", Guid.Empty.ToString("D")) };
        yield return new object[] { Replace("--operation", Guid.NewGuid().ToString("B")) };
        yield return new object[] { Replace("--operation", "11111111-1111-1111-1111-111111111111") };
        yield return new object[] { Replace("--fingerprint", "abc") };
        yield return new object[] { Replace("--environment", "unsafe value") };
        yield return new object[] { Replace("--database", "master") };
        yield return new object[] { Replace("--timeout-seconds", "0") };
        yield return new object[] { Replace("--timeout-seconds", "61") };
        yield return new object[] { Append(Valid(), "--actor", "42") };
        yield return new object[] { Append(Valid(), "--unknown", "value") };
        yield return new object[] { Remove(Valid(), "--database") };
    }

    private static string[] Valid() => new[]
    {
        "inspect", "--environment", "owned-test", "--database", "MyDmsVn_Test_Operator",
        "--epoch", Epoch, "--actor", "42", "--operation", Operation,
        "--fingerprint", Fingerprint, "--timeout-seconds", "15"
    };

    private static string[] Replace(string name, string value)
    {
        var args = Valid();
        args[Array.IndexOf(args, name) + 1] = value;
        return args;
    }

    private static string[] Append(string[] args, params string[] values)
    {
        var result = new string[args.Length + values.Length];
        Array.Copy(args, result, args.Length);
        Array.Copy(values, 0, result, args.Length, values.Length);
        return result;
    }

    private static string[] Remove(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        var result = new List<string>(args);
        result.RemoveRange(index, 2);
        return result.ToArray();
    }

    private static WebOperationInspection Inspection(string disposition) => new WebOperationInspection(
        Guid.Parse(Epoch), 42, Guid.Parse(Operation),
        disposition == "INDETERMINATE" ? (WebOperationKind?)null : WebOperationKind.SalesCreate,
        null, disposition, disposition == "COMMITTED", disposition == "COMMITTED", disposition == "COMMITTED", null);

    private sealed class FakeClient : IOperatorInspectionClient
    {
        private readonly WebOperationInspection? _result;
        private readonly Exception? _error;
        private readonly bool _waitForCancellation;
        public FakeClient(WebOperationInspection result) { _result = result; }
        public FakeClient(Exception error) { _error = error; }
        public FakeClient(bool waitForCancellation) { _waitForCancellation = waitForCancellation; }
        public async Task<WebOperationInspection> InspectAsync(InspectOptions options, CancellationToken cancellationToken)
        {
            if (_waitForCancellation) await Task.Delay(Timeout.Infinite, cancellationToken);
            if (_error != null) throw _error;
            return _result!;
        }
    }
}
