using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MyDmsVn.WebOperation.Operator;

public sealed class ReconcileCommittedOptions : IOperatorTargetOptions
{
    public const string CommandName = "reconcile-committed";

    private static readonly Regex Label = new Regex(@"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,127}\z", RegexOptions.CultureInvariant);
    private static readonly Regex Sha256 = new Regex(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> SystemDatabases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "master", "model", "msdb", "tempdb"
    };
    private static readonly string[] Required =
    {
        "--environment", "--database", "--current-epoch", "--stored-epoch", "--actor",
        "--operation", "--fingerprint", "--correlation", "--case"
    };
    private static readonly HashSet<string> Allowed = new HashSet<string>(Required, StringComparer.Ordinal)
    {
        "--timeout-seconds"
    };

    private ReconcileCommittedOptions(string command, bool helpRequested, string environmentName = "", string databaseName = "",
        Guid currentEpoch = default, Guid storedEpoch = default, int actorUserId = 0, Guid operationId = default,
        string fingerprintHex = "", Guid correlationId = default, string caseId = "", int timeoutSeconds = 15)
    {
        Command = command;
        HelpRequested = helpRequested;
        EnvironmentName = environmentName;
        DatabaseName = databaseName;
        CurrentEpoch = currentEpoch;
        StoredEpoch = storedEpoch;
        ActorUserId = actorUserId;
        OperationId = operationId;
        FingerprintHex = fingerprintHex;
        CorrelationId = correlationId;
        CaseId = caseId;
        TimeoutSeconds = timeoutSeconds;
    }

    public string Command { get; }
    public bool HelpRequested { get; }
    public string EnvironmentName { get; }
    public string DatabaseName { get; }
    public Guid CurrentEpoch { get; }
    public Guid StoredEpoch { get; }
    public int ActorUserId { get; }
    public Guid OperationId { get; }
    public string FingerprintHex { get; }
    public Guid CorrelationId { get; }
    public string CaseId { get; }
    public int TimeoutSeconds { get; }

    public byte[] FingerprintBytes()
    {
        var bytes = new byte[32];
        for (var index = 0; index < bytes.Length; index++)
            bytes[index] = byte.Parse(FingerprintHex.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return bytes;
    }

    public static bool TryParse(string[]? args, out ReconcileCommittedOptions? options, out string? error)
    {
        options = null;
        error = "Operator.InvalidArguments: exact read-only reconciliation options are required.";
        if (args == null || args.Length < 2 || args[0] != CommandName)
            return false;
        if (args.Length == 2 && args[1] == "--help")
        {
            options = new ReconcileCommittedOptions(args[0], true);
            error = null;
            return true;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
        {
            var name = args[index];
            if (!Allowed.Contains(name) || values.ContainsKey(name) || index + 1 == args.Length || args[index + 1] == null)
                return false;
            values.Add(name, args[index + 1]);
        }
        foreach (var required in Required)
            if (!values.ContainsKey(required))
                return false;

        var environmentName = values["--environment"];
        var databaseName = values["--database"];
        var fingerprint = values["--fingerprint"];
        var caseId = values["--case"];
        if (environmentName.Length > 64 || !Label.IsMatch(environmentName) ||
            !Label.IsMatch(databaseName) || SystemDatabases.Contains(databaseName) ||
            !TryParseGuid(values["--current-epoch"], out var currentEpoch) ||
            !TryParseGuid(values["--stored-epoch"], out var storedEpoch) ||
            !TryParsePositiveInt(values["--actor"], out var actor) ||
            !TryParseV4(values["--operation"], out var operation) ||
            !TryParseV4(values["--correlation"], out var correlation) || correlation == operation ||
            !Sha256.IsMatch(fingerprint) || !MaintenanceEvidenceRecord.IsCaseId(caseId))
            return false;

        var timeout = 15;
        if (values.TryGetValue("--timeout-seconds", out var timeoutText) &&
            (!TryParsePositiveInt(timeoutText, out timeout) || timeout > 25))
            return false;

        var parsed = new ReconcileCommittedOptions(args[0], false, environmentName, databaseName, currentEpoch, storedEpoch,
            actor, operation, fingerprint, correlation, caseId, timeout);
        options = parsed;
        error = null;
        return true;
    }

    private static bool TryParsePositiveInt(string text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0 &&
        value.ToString(CultureInfo.InvariantCulture) == text;

    private static bool TryParseGuid(string text, out Guid value) =>
        Guid.TryParseExact(text, "D", out value) && value != Guid.Empty && value.ToString("D") == text;

    private static bool TryParseV4(string text, out Guid value) =>
        TryParseGuid(text, out value) && text[14] == '4' && "89ab".IndexOf(text[19]) >= 0;
}
