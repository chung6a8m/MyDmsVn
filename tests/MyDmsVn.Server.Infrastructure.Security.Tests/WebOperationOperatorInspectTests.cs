using System;
using System.Collections.Generic;
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
}
