using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using MyDmsVn.Server.Application.Operations;

namespace MyDmsVn.WebOperation.Operator;

public sealed class RecordNotExecutedOptions : IOperatorTargetOptions
{
    private static readonly Regex Label = new Regex(@"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,127}\z", RegexOptions.CultureInvariant);
    private static readonly Regex Sha256 = new Regex(@"\A[0-9a-fA-F]{64}\z", RegexOptions.CultureInvariant);
    private static readonly Regex Case = new Regex(@"\A[A-Za-z0-9_-]{1,128}\z", RegexOptions.CultureInvariant);
    private static readonly Regex Reason = new Regex(@"\A[A-Za-z0-9_-]{1,64}\z", RegexOptions.CultureInvariant);
    private static readonly Regex Evidence = new Regex(@"\A[A-Za-z0-9_.:/-]{1,256}\z", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> SystemDatabases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "master", "model", "msdb", "tempdb"
    };

    private RecordNotExecutedOptions(bool helpRequested, bool dryRun = false, bool execute = false,
        string environmentName = "", string databaseName = "", Guid deploymentEpoch = default,
        int actorUserId = 0, Guid operationId = default, WebOperationKind kind = default,
        long? requestTargetId = null, string fingerprintHex = "", Guid decisionId = default,
        string caseId = "", string reasonCode = "", string evidenceReference = "", int timeoutSeconds = 15)
    {
        HelpRequested = helpRequested; DryRun = dryRun; Execute = execute; EnvironmentName = environmentName;
        DatabaseName = databaseName; DeploymentEpoch = deploymentEpoch; ActorUserId = actorUserId; OperationId = operationId;
        Kind = kind; RequestTargetId = requestTargetId; FingerprintHex = fingerprintHex; DecisionId = decisionId;
        CaseId = caseId; ReasonCode = reasonCode; EvidenceReference = evidenceReference; TimeoutSeconds = timeoutSeconds;
    }

    public bool HelpRequested { get; }
    public bool DryRun { get; }
    public bool Execute { get; }
    public string EnvironmentName { get; }
    public string DatabaseName { get; }
    public Guid DeploymentEpoch { get; }
    public int ActorUserId { get; }
    public Guid OperationId { get; }
    public WebOperationKind Kind { get; }
    public long? RequestTargetId { get; }
    public string FingerprintHex { get; }
    public Guid DecisionId { get; }
    public string CaseId { get; }
    public string ReasonCode { get; }
    public string EvidenceReference { get; }
    public int TimeoutSeconds { get; }

    public string ConfirmationText => "record-not-executed|environment=" + EnvironmentName + "|database=" + DatabaseName +
        "|epoch=" + DeploymentEpoch.ToString("D") + "|actor=" + ActorUserId.ToString(CultureInfo.InvariantCulture) +
        "|operation=" + OperationId.ToString("D") + "|kind=" + ((byte)Kind).ToString(CultureInfo.InvariantCulture) +
        "|target=" + (RequestTargetId?.ToString(CultureInfo.InvariantCulture) ?? "NONE") + "|fingerprint=" + FingerprintHex +
        "|decision=" + DecisionId.ToString("D") + "|case=" + CaseId + "|reason=" + ReasonCode + "|evidence=" + EvidenceReference;

    public byte[] FingerprintBytes()
    {
        var bytes = new byte[32];
        for (var index = 0; index < bytes.Length; index++)
            bytes[index] = byte.Parse(FingerprintHex.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return bytes;
    }

    public static bool TryParse(string[]? args, out RecordNotExecutedOptions? options, out string? error)
    {
        options = null; error = null; args ??= Array.Empty<string>();
        if (args.Length == 2 && args[0] == "record-not-executed" && args[1] == "--help")
        {
            options = new RecordNotExecutedOptions(true);
            return true;
        }
        if (args.Length < 2 || args[0] != "record-not-executed")
            return Fail("Operator.InvalidArguments: expected the record-not-executed command.", out options, out error);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var dryRun = false; var execute = false;
        for (var index = 1; index < args.Length; index++)
        {
            var name = args[index];
            if (name == "--dry-run" || name == "--execute")
            {
                if ((name == "--dry-run" && dryRun) || (name == "--execute" && execute))
                    return Fail("Operator.InvalidArguments: duplicate mode.", out options, out error);
                dryRun |= name == "--dry-run"; execute |= name == "--execute";
                continue;
            }
            if (name != "--environment" && name != "--database" && name != "--epoch" && name != "--actor" &&
                name != "--operation" && name != "--kind" && name != "--target" && name != "--fingerprint" &&
                name != "--case" && name != "--reason" && name != "--evidence" && name != "--decision" &&
                name != "--timeout-seconds" && name != "--confirm")
                return Fail("Operator.InvalidArguments: unknown option.", out options, out error);
            if (values.ContainsKey(name)) return Fail("Operator.InvalidArguments: duplicate option.", out options, out error);
            if (++index >= args.Length) return Fail("Operator.InvalidArguments: every option requires one value.", out options, out error);
            values.Add(name, args[index]);
        }
        if (dryRun == execute) return Fail("Operator.InvalidArguments: select exactly one of --dry-run or --execute.", out options, out error);
        foreach (var required in new[] { "--environment", "--database", "--epoch", "--actor", "--operation", "--kind", "--target",
                     "--fingerprint", "--case", "--reason", "--evidence", "--decision" })
            if (!values.ContainsKey(required)) return Fail("Operator.InvalidArguments: a required option is missing.", out options, out error);
        if (dryRun && values.ContainsKey("--confirm")) return Fail("Operator.InvalidArguments: dry-run does not accept confirmation.", out options, out error);
        if (execute && !values.ContainsKey("--confirm")) return Fail("Operator.InvalidArguments: execute requires exact confirmation.", out options, out error);
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
        if (!byte.TryParse(values["--kind"], NumberStyles.None, CultureInfo.InvariantCulture, out var kindValue) || kindValue < 1 || kindValue > 6 ||
            kindValue.ToString(CultureInfo.InvariantCulture) != values["--kind"])
            return Fail("Operator.InvalidArguments: invalid operation kind.", out options, out error);
        var kind = (WebOperationKind)kindValue; long? target = null;
        if (kind == WebOperationKind.SalesCreate || kind == WebOperationKind.ReceiptCreate)
        {
            if (values["--target"] != "NONE") return Fail("Operator.InvalidArguments: Create target must be NONE.", out options, out error);
        }
        else if (!long.TryParse(values["--target"], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedTarget) || parsedTarget <= 0 ||
                 parsedTarget.ToString(CultureInfo.InvariantCulture) != values["--target"])
            return Fail("Operator.InvalidArguments: Update/Post target must be a positive canonical ID.", out options, out error);
        else target = parsedTarget;
        if (!Sha256.IsMatch(values["--fingerprint"])) return Fail("Operator.InvalidArguments: invalid SHA-256 fingerprint.", out options, out error);
        if (!Guid.TryParseExact(values["--decision"], "D", out var decision) || !IsVersion4(decision))
            return Fail("Operator.InvalidArguments: invalid decision ID.", out options, out error);
        if (decision == operation)
            return Fail("Operator.InvalidArguments: original operation and decision IDs must be distinct.", out options, out error);
        if (!Case.IsMatch(values["--case"])) return Fail("Operator.InvalidArguments: invalid case identity.", out options, out error);
        if (!Reason.IsMatch(values["--reason"])) return Fail("Operator.InvalidArguments: invalid reason code.", out options, out error);
        if (!Evidence.IsMatch(values["--evidence"])) return Fail("Operator.InvalidArguments: invalid evidence identity.", out options, out error);
        var timeout = 15;
        if (values.TryGetValue("--timeout-seconds", out var timeoutText) &&
            (!int.TryParse(timeoutText, NumberStyles.None, CultureInfo.InvariantCulture, out timeout) || timeout < 1 || timeout > 25 ||
             timeout.ToString(CultureInfo.InvariantCulture) != timeoutText))
            return Fail("Operator.InvalidArguments: timeout must be 1 through 25 seconds.", out options, out error);
        var parsed = new RecordNotExecutedOptions(false, dryRun, execute, values["--environment"], values["--database"], epoch,
            actor, operation, kind, target, values["--fingerprint"].ToLowerInvariant(), decision,
            values["--case"], values["--reason"], values["--evidence"], timeout);
        if (execute && !string.Equals(values["--confirm"], parsed.ConfirmationText, StringComparison.Ordinal))
            return Fail("Operator.InvalidArguments: exact confirmation does not match the decision.", out options, out error);
        options = parsed;
        return true;
    }

    private static bool IsVersion4(Guid value) { var text = value.ToString("D"); return text[14] == '4' && "89ab".IndexOf(text[19]) >= 0; }
    private static bool Fail(string message, out RecordNotExecutedOptions? options, out string? error) { options = null; error = message; return false; }
}
