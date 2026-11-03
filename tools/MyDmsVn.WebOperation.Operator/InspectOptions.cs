using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MyDmsVn.WebOperation.Operator;

public sealed class InspectOptions
{
    private static readonly Regex Label = new Regex(@"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,127}\z", RegexOptions.CultureInvariant);
    private static readonly Regex Sha256 = new Regex(@"\A[0-9a-fA-F]{64}\z", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> SystemDatabases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "master", "model", "msdb", "tempdb"
    };

    private InspectOptions(bool helpRequested, string environmentName = "", string databaseName = "",
        Guid deploymentEpoch = default, int actorUserId = 0, Guid operationId = default,
        string fingerprintHex = "", int timeoutSeconds = 15)
    {
        HelpRequested = helpRequested;
        EnvironmentName = environmentName;
        DatabaseName = databaseName;
        DeploymentEpoch = deploymentEpoch;
        ActorUserId = actorUserId;
        OperationId = operationId;
        FingerprintHex = fingerprintHex;
        TimeoutSeconds = timeoutSeconds;
    }

    public bool HelpRequested { get; }
    public string EnvironmentName { get; }
    public string DatabaseName { get; }
    public Guid DeploymentEpoch { get; }
    public int ActorUserId { get; }
    public Guid OperationId { get; }
    public string FingerprintHex { get; }
    public int TimeoutSeconds { get; }

    public static bool TryParse(string[]? args, out InspectOptions? options, out string? error)
    {
        options = null;
        error = null;
        args ??= Array.Empty<string>();
        if ((args.Length == 1 && args[0] == "--help") ||
            (args.Length == 2 && args[0] == "inspect" && args[1] == "--help"))
        {
            options = new InspectOptions(true);
            return true;
        }
        if (args.Length == 0 || !string.Equals(args[0], "inspect", StringComparison.Ordinal))
            return Fail("Operator.InvalidArguments: expected the inspect command.", out options, out error);
        if ((args.Length - 1) % 2 != 0)
            return Fail("Operator.InvalidArguments: every option requires one value.", out options, out error);

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
        {
            var name = args[index];
            if (name != "--environment" && name != "--database" && name != "--epoch" && name != "--actor" &&
                name != "--operation" && name != "--fingerprint" && name != "--timeout-seconds")
                return Fail("Operator.InvalidArguments: unknown option.", out options, out error);
            if (values.ContainsKey(name))
                return Fail("Operator.InvalidArguments: duplicate option.", out options, out error);
            values.Add(name, args[index + 1]);
        }

        foreach (var required in new[] { "--environment", "--database", "--epoch", "--actor", "--operation", "--fingerprint" })
            if (!values.ContainsKey(required))
                return Fail("Operator.InvalidArguments: a required option is missing.", out options, out error);

        if (!Label.IsMatch(values["--environment"]) || values["--environment"].Length > 64)
            return Fail("Operator.InvalidArguments: invalid environment label.", out options, out error);
        if (!Label.IsMatch(values["--database"]) || SystemDatabases.Contains(values["--database"]))
            return Fail("Operator.InvalidArguments: invalid application database target.", out options, out error);
        if (!Guid.TryParseExact(values["--epoch"], "D", out var epoch) || epoch == Guid.Empty)
            return Fail("Operator.InvalidArguments: invalid deployment epoch.", out options, out error);
        if (!int.TryParse(values["--actor"], NumberStyles.None, CultureInfo.InvariantCulture, out var actor) || actor <= 0 ||
            actor.ToString(CultureInfo.InvariantCulture) != values["--actor"])
            return Fail("Operator.InvalidArguments: invalid actor ID.", out options, out error);
        if (!Guid.TryParseExact(values["--operation"], "D", out var operation) || !IsVersion4(operation))
            return Fail("Operator.InvalidArguments: invalid original operation ID.", out options, out error);
        if (!Sha256.IsMatch(values["--fingerprint"]))
            return Fail("Operator.InvalidArguments: invalid SHA-256 fingerprint.", out options, out error);
        var timeout = 15;
        if (values.TryGetValue("--timeout-seconds", out var timeoutText) &&
            (!int.TryParse(timeoutText, NumberStyles.None, CultureInfo.InvariantCulture, out timeout) || timeout < 1 || timeout > 60 ||
             timeout.ToString(CultureInfo.InvariantCulture) != timeoutText))
            return Fail("Operator.InvalidArguments: timeout must be 1 through 60 seconds.", out options, out error);

        options = new InspectOptions(false, values["--environment"], values["--database"], epoch, actor, operation,
            values["--fingerprint"].ToLowerInvariant(), timeout);
        return true;
    }

    public byte[] FingerprintBytes()
    {
        var bytes = new byte[32];
        for (var index = 0; index < bytes.Length; index++)
            bytes[index] = byte.Parse(FingerprintHex.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return bytes;
    }

    private static bool IsVersion4(Guid value)
    {
        var text = value.ToString("D");
        return text[14] == '4' && "89ab".IndexOf(text[19]) >= 0;
    }

    private static bool Fail(string message, out InspectOptions? options, out string? error)
    {
        options = null;
        error = message;
        return false;
    }
}
