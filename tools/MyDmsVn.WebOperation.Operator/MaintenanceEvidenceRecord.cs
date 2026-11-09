using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MyDmsVn.WebOperation.Operator;

public enum MaintenanceEvidencePhase { Attempt, Transaction, Readback }
public enum MaintenanceTransactionCertainty { NotStarted, RolledBack, Unknown, Committed }
public enum MaintenanceReadback { NotRun, Verified, Unknown, BusinessCommittedResultUnavailable, BusinessCommittedResultUnavailableSuperseded }
public enum MaintenanceEvidenceDelivery { PrimaryOnly, Delivered, Escalate }
public enum MaintenanceEvidencePurge { Denied, Purged, BackupUncertain, PrimaryUncertain }

public sealed class MaintenanceEvidenceRecord
{
    private static readonly Regex Label = new Regex(@"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,127}\z", RegexOptions.CultureInvariant);
    private static readonly Regex Case = new Regex(@"\A[A-Za-z0-9_-]{1,128}\z", RegexOptions.CultureInvariant);
    private static readonly Regex Digest = new Regex(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant);
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

    private readonly string _command;
    private readonly string _environmentHash;
    private readonly string _databaseHash;
    private readonly string _loginHash;
    private readonly int _actor;
    private readonly Guid _operation;
    private readonly Guid _correlation;
    private readonly Guid _currentEpoch;
    private readonly Guid _storedEpoch;
    private readonly string _fingerprintHash;
    private readonly MaintenanceTransactionCertainty _certainty;
    private readonly MaintenanceReadback _readback;

    private MaintenanceEvidenceRecord(string command, string environment, string database, string effectiveLogin,
        int actor, Guid operation, Guid correlation, Guid currentEpoch, Guid storedEpoch, byte[] fingerprint,
        string caseId, MaintenanceEvidencePhase phase, MaintenanceTransactionCertainty certainty, MaintenanceReadback readback)
    {
        if (!IsCaseId(caseId) || !Label.IsMatch(environment) || environment.Length > 64 || !Label.IsMatch(database) ||
            actor <= 0 || !IsV4(operation) || !IsV4(correlation) || operation == correlation ||
            currentEpoch == Guid.Empty || storedEpoch == Guid.Empty || fingerprint.Length != 32 ||
            string.IsNullOrWhiteSpace(effectiveLogin) || effectiveLogin.Length > 128 ||
            !Enum.IsDefined(typeof(MaintenanceEvidencePhase), phase) ||
            !Enum.IsDefined(typeof(MaintenanceTransactionCertainty), certainty) ||
            !Enum.IsDefined(typeof(MaintenanceReadback), readback))
            throw new ArgumentException("The evidence record is outside the supported boundary.");

        _command = command;
        _environmentHash = Hash(Utf8.GetBytes(environment));
        _databaseHash = Hash(Utf8.GetBytes(database));
        _loginHash = Hash(Utf8.GetBytes(effectiveLogin));
        _actor = actor;
        _operation = operation;
        _correlation = correlation;
        _currentEpoch = currentEpoch;
        _storedEpoch = storedEpoch;
        _fingerprintHash = Hash(fingerprint);
        _certainty = certainty;
        _readback = readback;
        CaseId = caseId;
        Phase = phase;
    }

    public string CaseId { get; }
    public MaintenanceEvidencePhase Phase { get; }

    public static MaintenanceEvidenceRecord FromCompaction(CompactOptions options, string effectiveLogin,
        MaintenanceEvidencePhase phase, MaintenanceTransactionCertainty certainty, MaintenanceReadback readback)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (options.HelpRequested) throw new ArgumentException("Help is not an evidence target.", nameof(options));
        if ((!options.Execute && certainty == MaintenanceTransactionCertainty.Committed) ||
            (phase == MaintenanceEvidencePhase.Attempt && certainty != MaintenanceTransactionCertainty.NotStarted) ||
            (phase != MaintenanceEvidencePhase.Readback && readback != MaintenanceReadback.NotRun) ||
            (phase == MaintenanceEvidencePhase.Readback &&
                (certainty != MaintenanceTransactionCertainty.Committed ||
                    (readback != MaintenanceReadback.Verified && readback != MaintenanceReadback.Unknown))))
            throw new ArgumentException("The compaction evidence phase and outcome are inconsistent.");

        return new MaintenanceEvidenceRecord(options.Command, options.EnvironmentName, options.DatabaseName, effectiveLogin,
            options.ActorUserId, options.OperationId, options.CorrelationId, options.CurrentEpoch, options.StoredEpoch,
            options.FingerprintBytes(), options.CaseId, phase, certainty, readback);
    }

    public static MaintenanceEvidenceRecord FromReconciliation(string environment, string database, string effectiveLogin,
        int actor, Guid operation, Guid correlation, Guid currentEpoch, Guid storedEpoch, byte[] fingerprint, string caseId,
        MaintenanceEvidencePhase phase, MaintenanceReadback readback)
    {
        if (fingerprint == null) throw new ArgumentNullException(nameof(fingerprint));
        if ((phase != MaintenanceEvidencePhase.Attempt || readback != MaintenanceReadback.NotRun) &&
            (phase != MaintenanceEvidencePhase.Readback ||
                (readback != MaintenanceReadback.BusinessCommittedResultUnavailable &&
                    readback != MaintenanceReadback.BusinessCommittedResultUnavailableSuperseded &&
                    readback != MaintenanceReadback.Unknown)))
            throw new ArgumentException("The reconciliation evidence phase and outcome are inconsistent.");

        return new MaintenanceEvidenceRecord("reconcile-committed", environment, database, effectiveLogin,
            actor, operation, correlation, currentEpoch, storedEpoch, fingerprint, caseId, phase,
            MaintenanceTransactionCertainty.NotStarted, readback);
    }

    internal bool Follows(MaintenanceEvidenceRecord previous) =>
        CaseId == previous.CaseId && _command == previous._command && _environmentHash == previous._environmentHash &&
        _databaseHash == previous._databaseHash && _loginHash == previous._loginHash && _actor == previous._actor &&
        _operation == previous._operation && _correlation == previous._correlation && _currentEpoch == previous._currentEpoch &&
        _storedEpoch == previous._storedEpoch && _fingerprintHash == previous._fingerprintHash &&
        (_command == "reconcile-committed"
            ? previous.Phase == MaintenanceEvidencePhase.Attempt && Phase == MaintenanceEvidencePhase.Readback
            : (previous.Phase == MaintenanceEvidencePhase.Attempt && Phase == MaintenanceEvidencePhase.Transaction) ||
                (previous.Phase == MaintenanceEvidencePhase.Transaction &&
                    previous._certainty == MaintenanceTransactionCertainty.Committed && Phase == MaintenanceEvidencePhase.Readback));

    internal byte[] Serialize(DateTimeOffset utc) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        version = 1,
        utc,
        command = _command,
        environmentHash = _environmentHash,
        databaseHash = _databaseHash,
        loginHash = _loginHash,
        actor = _actor,
        operation = _operation.ToString("N").Substring(0, 8),
        correlation = _correlation.ToString("N").Substring(0, 8),
        currentEpoch = _currentEpoch,
        storedEpoch = _storedEpoch,
        fingerprintHash = _fingerprintHash,
        phase = Phase.ToString(),
        transactionCertainty = _certainty.ToString(),
        readback = _readback.ToString(),
        delivery = MaintenanceEvidenceDelivery.PrimaryOnly.ToString()
    });

    internal static bool IsCaseId(string? value) =>
        value != null && Case.IsMatch(value) && !MaintenanceEvidencePolicy.IsDeviceName(value);
    internal static bool IsHash(string? value) => value != null && Digest.IsMatch(value);

    private static bool IsV4(Guid value)
    {
        var text = value.ToString("N");
        return text[12] == '4' && "89ab".IndexOf(text[16]) >= 0;
    }

    internal static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
}

public sealed class MaintenanceEvidenceReceipt
{
    internal MaintenanceEvidenceReceipt(string caseId, IEnumerable<string> eventHashes, MaintenanceEvidenceDelivery delivery,
        string? primaryManifestHash = null, string? backupManifestHash = null)
    {
        CaseId = caseId;
        EventHashes = Array.AsReadOnly(eventHashes.ToArray());
        Delivery = delivery;
        PrimaryManifestHash = primaryManifestHash;
        BackupManifestHash = backupManifestHash;
    }

    public string CaseId { get; }
    public IReadOnlyList<string> EventHashes { get; }
    public string? EventHash => EventHashes.LastOrDefault();
    public MaintenanceEvidenceDelivery Delivery { get; }
    public string? PrimaryManifestHash { get; }
    public string? BackupManifestHash { get; }
}
