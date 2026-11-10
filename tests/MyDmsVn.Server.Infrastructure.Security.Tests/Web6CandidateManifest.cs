using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Infrastructure.Operations;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal sealed class Web6CandidateInputs
{
    internal Web6CandidateInputs(string candidateId, (string Sha, string Tree) backend, (string Sha, string Tree) frontend,
        string schema13Fingerprint, Uri origin, string certificateHash, string selector, int runSeconds, int shutdownSeconds,
        string browserSelector = "web6-manual-resend-live.spec.ts")
    {
        if (!Regex.IsMatch(candidateId ?? "", @"\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant) ||
            !IsSelector(selector) || !IsSelector(browserSelector) || runSeconds is < 1 or > 86400 || shutdownSeconds is < 1 or > 600 ||
            origin == null || !origin.IsAbsoluteUri || origin.Scheme != Uri.UriSchemeHttps || !origin.IsLoopback ||
            origin.AbsolutePath != "/" || origin.UserInfo.Length != 0 || origin.Query.Length != 0 || origin.Fragment.Length != 0)
            throw new ArgumentException("Candidate identity, selectors, budgets or loopback HTTPS origin refused.");
        CandidateId = candidateId!;
        Backend = (RequireHash(backend.Sha, 40), RequireHash(backend.Tree, 40));
        Frontend = (RequireHash(frontend.Sha, 40), RequireHash(frontend.Tree, 40));
        Schema13Fingerprint = RequireHash(schema13Fingerprint);
        Origin = origin;
        CertificateHash = RequireHash(certificateHash);
        Selector = selector;
        BrowserSelector = browserSelector;
        RunSeconds = runSeconds;
        ShutdownSeconds = shutdownSeconds;
    }

    internal string CandidateId { get; }
    internal (string Sha, string Tree) Backend { get; }
    internal (string Sha, string Tree) Frontend { get; }
    internal string Schema13Fingerprint { get; }
    internal Uri Origin { get; }
    internal string CertificateHash { get; }
    internal string Selector { get; }
    internal string BrowserSelector { get; }
    internal int RunSeconds { get; }
    internal int ShutdownSeconds { get; }

    internal static string RequireHash(string? hash, int length = 64)
    {
        if (hash == null || hash.Length != length || hash.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ArgumentException("Candidate requires a canonical lowercase hash.");
        return hash;
    }

    private static bool IsSelector(string? value) =>
        Regex.IsMatch(value ?? "", @"\A[A-Za-z0-9][A-Za-z0-9._-]{0,199}\z", RegexOptions.CultureInvariant);
}

internal sealed class Web6CutoverArchive
{
    internal Web6CutoverArchive(string sha, string tree, bool clean, string artifactHash)
    {
        Sha = Web6CandidateInputs.RequireHash(sha, 40);
        Tree = Web6CandidateInputs.RequireHash(tree, 40);
        ArtifactHash = Web6CandidateInputs.RequireHash(artifactHash);
        Clean = clean;
    }

    internal string Sha { get; }
    internal string Tree { get; }
    internal bool Clean { get; }
    internal string ArtifactHash { get; }
}

internal enum Web6CutoverPair { Current, Old, Restored }

internal sealed class Web6CandidateCutoverPlan
{
    internal const string FrontendArchiveSha = "ea104582a5aa4bcdf0d04999296a30a3c43e450e";
    internal const string BackendArchiveSha = "a3f3db16381a38bdf0b76e8ab36cb55bec02bbd2";

    internal Web6CandidateCutoverPlan(string currentPairId, string oldPairId, string restoredPairId,
        Uri currentOrigin, Uri oldOrigin, string frontendArchiveSha, string frontendArchiveTree,
        string frontendArtifactHash, string backendArchiveSha, string backendArchiveTree,
        string backendArtifactHash, string currentArtifactHash, int currentSchema, int oldSchema,
        string currentDatabaseReceipt, string oldDatabaseReceipt, string restoredDatabaseReceipt, Guid currentEpoch, Guid restoredEpoch)
    {
        CurrentPairId = RequireIdentity(currentPairId);
        OldPairId = RequireIdentity(oldPairId);
        RestoredPairId = RequireIdentity(restoredPairId);
        if (new[] { CurrentPairId, OldPairId, RestoredPairId }.Distinct(StringComparer.Ordinal).Count() != 3)
            throw new ArgumentException("Cutover pair identities must be distinct.");
        CurrentOrigin = RequireOrigin(currentOrigin);
        OldOrigin = RequireOrigin(oldOrigin);
        if (CurrentOrigin == OldOrigin) throw new ArgumentException("Cutover origins must be distinct.");
        if (frontendArchiveSha != FrontendArchiveSha || backendArchiveSha != BackendArchiveSha)
            throw new ArgumentException("Cutover archive source pins changed.");
        FrontendArchiveTree = Web6CandidateInputs.RequireHash(frontendArchiveTree, 40);
        BackendArchiveTree = Web6CandidateInputs.RequireHash(backendArchiveTree, 40);
        FrontendArtifactHash = Web6CandidateInputs.RequireHash(frontendArtifactHash);
        OldArtifactHash = Web6CandidateInputs.RequireHash(backendArtifactHash);
        CurrentArtifactHash = Web6CandidateInputs.RequireHash(currentArtifactHash);
        if (currentSchema != 13 || oldSchema != 12)
            throw new ArgumentException("Cutover requires current exact13 and old exact12 schemas.");
        CurrentSchema = currentSchema;
        OldSchema = oldSchema;
        CurrentDatabaseReceipt = Web6CandidateInputs.RequireHash(currentDatabaseReceipt);
        OldDatabaseReceipt = Web6CandidateInputs.RequireHash(oldDatabaseReceipt);
        RestoredDatabaseReceipt = Web6CandidateInputs.RequireHash(restoredDatabaseReceipt);
        if (new[] { CurrentDatabaseReceipt, OldDatabaseReceipt, RestoredDatabaseReceipt }.Distinct(StringComparer.Ordinal).Count() != 3)
            throw new ArgumentException("Cutover database allocations must be distinct.");
        if (currentEpoch == Guid.Empty || restoredEpoch == Guid.Empty || currentEpoch == restoredEpoch)
            throw new ArgumentException("Cutover restore must rotate the deployment epoch.");
        CurrentEpoch = currentEpoch;
        RestoredEpoch = restoredEpoch;
    }

    internal string CurrentPairId { get; }
    internal string OldPairId { get; }
    internal string RestoredPairId { get; }
    internal Uri CurrentOrigin { get; }
    internal Uri OldOrigin { get; }
    internal string FrontendArchiveTree { get; }
    internal string BackendArchiveTree { get; }
    internal string FrontendArtifactHash { get; }
    internal string OldArtifactHash { get; }
    internal string CurrentArtifactHash { get; }
    internal int CurrentSchema { get; }
    internal int OldSchema { get; }
    internal string CurrentDatabaseReceipt { get; }
    internal string OldDatabaseReceipt { get; }
    internal string RestoredDatabaseReceipt { get; }
    internal Guid CurrentEpoch { get; }
    internal Guid RestoredEpoch { get; }
    internal string CurrentCandidateId => CurrentPairId;
    internal string FrontendSourceSha => FrontendArchiveSha;
    internal string BackendSourceSha => BackendArchiveSha;

    internal void VerifyArchives(Web6CutoverArchive frontend, Web6CutoverArchive backend)
    {
        if (frontend == null || backend == null || !frontend.Clean || !backend.Clean ||
            frontend.Sha != FrontendArchiveSha || frontend.Tree != FrontendArchiveTree ||
            frontend.ArtifactHash != FrontendArtifactHash || backend.Sha != BackendArchiveSha ||
            backend.Tree != BackendArchiveTree || backend.ArtifactHash != OldArtifactHash)
            throw new IOException("Cutover archive identity, cleanliness or rebuilt artifact drifted.");
    }

    internal JObject FrontendHandles() => new JObject
    {
        ["version"] = 1,
        ["currentPairId"] = CurrentPairId,
        ["oldPairId"] = OldPairId,
        ["restoredPairId"] = RestoredPairId,
        ["currentOrigin"] = CurrentOrigin.GetLeftPart(UriPartial.Authority),
        ["oldOrigin"] = OldOrigin.GetLeftPart(UriPartial.Authority),
        ["frontendWeb4SourceSha"] = FrontendArchiveSha,
        ["backendWeb4SourceSha"] = BackendArchiveSha,
        ["frontendWeb4DistManifestSha256"] = FrontendArtifactHash,
        ["backendWeb4ArtifactSha256"] = OldArtifactHash,
        ["currentDatabaseOwnershipReceiptSha256"] = CurrentDatabaseReceipt,
        ["oldDatabaseOwnershipReceiptSha256"] = OldDatabaseReceipt,
        ["restoredDatabaseOwnershipReceiptSha256"] = RestoredDatabaseReceipt,
        ["currentSchema"] = CurrentSchema,
        ["oldSchema"] = OldSchema,
        ["currentDeploymentEpoch"] = CurrentEpoch.ToString("D"),
        ["restoredDeploymentEpoch"] = RestoredEpoch.ToString("D"),
    };

    private static string RequireIdentity(string value)
    {
        if (!Regex.IsMatch(value ?? "", @"\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant))
            throw new ArgumentException("Cutover pair identity is invalid.");
        return value!;
    }

    private static Uri RequireOrigin(Uri origin)
    {
        if (origin == null || !origin.IsAbsoluteUri || origin.Scheme != Uri.UriSchemeHttps || !origin.IsLoopback ||
            origin.AbsolutePath != "/" || origin.UserInfo.Length != 0 || origin.Query.Length != 0 || origin.Fragment.Length != 0 ||
            origin.Port < 1024 || origin.Port > 65535)
            throw new ArgumentException("Cutover origin must be owned loopback HTTPS.");
        return origin;
    }
}

internal sealed class Web6CutoverAdmission
{
    internal Web6CutoverAdmission(Uri origin, int schema, string databaseReceipt, string artifactHash, Guid epoch,
        int controllers, int workers, bool queueOwned)
    {
        Origin = origin;
        Schema = schema;
        DatabaseReceipt = databaseReceipt;
        ArtifactHash = artifactHash;
        Epoch = epoch;
        Controllers = controllers;
        Workers = workers;
        QueueOwned = queueOwned;
    }

    internal Uri Origin { get; }
    internal int Schema { get; }
    internal string DatabaseReceipt { get; }
    internal string ArtifactHash { get; }
    internal Guid Epoch { get; }
    internal int Controllers { get; }
    internal int Workers { get; }
    internal bool QueueOwned { get; }
    internal Web6CutoverAdmission WithArtifact(string value) => new Web6CutoverAdmission(Origin, Schema, DatabaseReceipt, value, Epoch, Controllers, Workers, QueueOwned);
    internal Web6CutoverAdmission WithSchema(int value) => new Web6CutoverAdmission(Origin, value, DatabaseReceipt, ArtifactHash, Epoch, Controllers, Workers, QueueOwned);
    internal Web6CutoverAdmission WithReceipt(string value) => new Web6CutoverAdmission(Origin, Schema, value, ArtifactHash, Epoch, Controllers, Workers, QueueOwned);
    internal Web6CutoverAdmission WithOrigin(Uri value) => new Web6CutoverAdmission(value, Schema, DatabaseReceipt, ArtifactHash, Epoch, Controllers, Workers, QueueOwned);
    internal Web6CutoverAdmission WithEpoch(Guid value) => new Web6CutoverAdmission(Origin, Schema, DatabaseReceipt, ArtifactHash, value, Controllers, Workers, QueueOwned);
}

internal sealed class Web6CandidateCutoverCoordinator
{
    private readonly Web6CandidateCutoverPlan _plan;
    private Guid _epoch;
    internal Web6CandidateCutoverCoordinator(Web6CandidateCutoverPlan plan)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _epoch = plan.CurrentEpoch;
    }

    internal Web6CutoverPair? ActivePair { get; private set; }
    internal bool HasActivePair => ActivePair.HasValue;

    internal void Admit(Web6CutoverPair pair, Web6CutoverAdmission observation)
    {
        if (ActivePair.HasValue) throw new InvalidOperationException("Cutover must drain the active pair before switching.");
        if (observation == null || observation.Controllers != 1 || observation.Workers != 1 || !observation.QueueOwned)
            throw new IOException("Cutover requires one owned controller, worker and request queue.");
        var current = pair != Web6CutoverPair.Old;
        var expectedOrigin = current ? _plan.CurrentOrigin : _plan.OldOrigin;
        var expectedSchema = current ? _plan.CurrentSchema : _plan.OldSchema;
        var expectedReceipt = pair == Web6CutoverPair.Restored ? _plan.RestoredDatabaseReceipt : current ? _plan.CurrentDatabaseReceipt : _plan.OldDatabaseReceipt;
        var expectedArtifact = current ? _plan.CurrentArtifactHash : _plan.OldArtifactHash;
        var expectedEpoch = pair == Web6CutoverPair.Restored ? _plan.RestoredEpoch : current ? _plan.CurrentEpoch : Guid.Empty;
        if (observation.Origin != expectedOrigin || observation.Schema != expectedSchema ||
            observation.DatabaseReceipt != expectedReceipt || observation.ArtifactHash != expectedArtifact ||
            observation.Epoch != expectedEpoch)
            throw new IOException("Cutover pair admission evidence does not match its pinned artifact, schema, database or epoch.");
        ActivePair = pair;
        _epoch = expectedEpoch;
    }

    internal void Drain(Web6CutoverPair pair, bool workerExited, bool queueAbsent)
    {
        if (ActivePair != pair) throw new InvalidOperationException("Cutover drain does not own the active pair.");
        if (!workerExited || !queueAbsent) throw new IOException("Cutover pair did not drain cleanly.");
        ActivePair = null;
    }

    internal void RestartSameEpoch(Guid observedEpoch)
    {
        if (ActivePair != Web6CutoverPair.Current || observedEpoch != _epoch)
            throw new IOException("Code restart must retain the admitted deployment epoch.");
    }

    internal void Restore(Guid restoredEpoch)
    {
        if (ActivePair != Web6CutoverPair.Restored || restoredEpoch != _plan.RestoredEpoch || restoredEpoch != _epoch)
            throw new IOException("Restore must rotate to the pinned fresh deployment epoch.");
        // Epoch belongs to the admitted allocation; never mutate current authority.
    }

    internal bool IsEpochCurrent(Guid epoch) => epoch == _epoch;
}

internal sealed class Web6CutoverBuildObservation
{
    internal Web6CutoverBuildObservation(string frontendSha, string frontendTree, bool frontendClean, string frontendBuildHash,
        string backendSha, string backendTree, bool backendClean, string backendBuildHash,
        string currentPublishedHash, string oldPublishedHash)
    {
        Frontend = new Web6CutoverArchive(frontendSha, frontendTree, frontendClean, frontendBuildHash);
        Backend = new Web6CutoverArchive(backendSha, backendTree, backendClean, backendBuildHash);
        CurrentPublishedHash = Web6CandidateInputs.RequireHash(currentPublishedHash);
        OldPublishedHash = Web6CandidateInputs.RequireHash(oldPublishedHash);
    }
    internal Web6CutoverArchive Frontend { get; }
    internal Web6CutoverArchive Backend { get; }
    internal string CurrentPublishedHash { get; }
    internal string OldPublishedHash { get; }
}

// Expected build hashes are sealed before copying into either published root. Verification
// consumes a fresh measurement and therefore cannot bless an arbitrary directory by measuring
// it and reusing that same value as its expectation.
internal sealed class Web6CutoverBuildAuthority
{
    private readonly Web6CutoverBuildObservation _expected;
    internal Web6CutoverBuildAuthority(string frontendSha, string frontendTree, string frontendBuildHash,
        string backendSha, string backendTree, string backendBuildHash, string currentPublishedHash, string oldPublishedHash)
    {
        _expected = new Web6CutoverBuildObservation(frontendSha, frontendTree, true, frontendBuildHash,
            backendSha, backendTree, true, backendBuildHash, currentPublishedHash, oldPublishedHash);
        if (_expected.Frontend.Sha != Web6CandidateCutoverPlan.FrontendArchiveSha ||
            _expected.Backend.Sha != Web6CandidateCutoverPlan.BackendArchiveSha)
            throw new ArgumentException("Cutover build authority source pin changed.");
    }

    internal static Web6CutoverBuildAuthority ReadProtected(string path, string expectedFileDigest)
    {
        path = Web6CandidateFileSystem.CanonicalPath(path);
        Web6CandidateFileSystem.RequireDirectories(Path.GetDirectoryName(path)!);
        Web6CandidateFileSystem.RequireRegularFile(path);
        using var identity = WindowsIdentity.GetCurrent();
        Web6CandidateManifest.RequirePrivateOwner(File.GetAccessControl(path, SqlTestPrivateRecord.Sections), identity.User!);
        byte[] bytes;
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Web6CandidateFileSystem.Identity(file.SafeFileHandle, directory: false);
            if (file.Length is < 2 or > 4096) throw new IOException("Cutover build authority size is invalid.");
            bytes = new byte[file.Length];
            var offset = 0;
            while (offset < bytes.Length) { var read = file.Read(bytes, offset, bytes.Length - offset); if (read == 0) throw new IOException(); offset += read; }
        }
        if (SqlTestPrivateRecord.Hash(bytes) != Web6CandidateInputs.RequireHash(expectedFileDigest))
            throw new IOException("Cutover build authority digest changed.");
        var json = JObject.Parse(new UTF8Encoding(false, true).GetString(bytes),
            new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        var keys = new[] { "version", "frontendSha", "frontendTree", "frontendBuildSha256", "backendSha", "backendTree", "backendBuildSha256", "currentPublishedSha256", "oldPublishedSha256" };
        if (!json.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal)
            .SequenceEqual(keys.OrderBy(name => name, StringComparer.Ordinal)) || (int?)json["version"] != 1)
            throw new IOException("Cutover build authority shape is invalid.");
        try
        {
            return new Web6CutoverBuildAuthority((string)json["frontendSha"]!, (string)json["frontendTree"]!,
                (string)json["frontendBuildSha256"]!, (string)json["backendSha"]!, (string)json["backendTree"]!,
                (string)json["backendBuildSha256"]!, (string)json["currentPublishedSha256"]!, (string)json["oldPublishedSha256"]!);
        }
        catch (Exception error) when (error is ArgumentException || error is InvalidCastException)
        { throw new IOException("Cutover build authority values are invalid.", error); }
    }

    internal void Verify(Web6CutoverBuildObservation actual)
    {
        if (actual == null || !actual.Frontend.Clean || !actual.Backend.Clean ||
            actual.Frontend.Sha != _expected.Frontend.Sha || actual.Frontend.Tree != _expected.Frontend.Tree ||
            actual.Frontend.ArtifactHash != _expected.Frontend.ArtifactHash ||
            actual.Backend.Sha != _expected.Backend.Sha || actual.Backend.Tree != _expected.Backend.Tree ||
            actual.Backend.ArtifactHash != _expected.Backend.ArtifactHash ||
            actual.CurrentPublishedHash != _expected.CurrentPublishedHash ||
            actual.OldPublishedHash != _expected.OldPublishedHash ||
            actual.Frontend.ArtifactHash != actual.CurrentPublishedHash ||
            actual.Backend.ArtifactHash != actual.OldPublishedHash)
            throw new IOException("Cutover sealed build authority or published artifact drifted.");
    }
}

internal sealed class Web6CutoverSqlOwnershipReceipt
{
    private Web6CutoverSqlOwnershipReceipt(string digest) => Digest = digest;
    internal string Digest { get; }

    internal static Web6CutoverSqlOwnershipReceipt FromProtectedRecord(SqlTestOwnershipManifest control,
        string sealedRecordDigest, string schemaFingerprint, string allocationIdentity)
    {
        if (control == null) throw new ArgumentNullException(nameof(control));
        var expected = Web6CandidateInputs.RequireHash(sealedRecordDigest);
        var actual = control.VerifiedDigest();
        if (actual != expected) throw new IOException("Cutover SQL ownership record is not the protected original.");
        var schema = Web6CandidateInputs.RequireHash(schemaFingerprint?.ToLowerInvariant());
        if (schema != SqlSchemaDefinitionFingerprint.Schema12.ToLowerInvariant() &&
            schema != SqlSchemaDefinitionFingerprint.Schema13.ToLowerInvariant())
            throw new IOException("Cutover SQL ownership schema is unsupported.");
        if (!Regex.IsMatch(allocationIdentity ?? "", @"\A[A-Za-z0-9][A-Za-z0-9._|:-]{0,255}\z", RegexOptions.CultureInvariant))
            throw new IOException("Cutover SQL allocation identity is invalid.");
        var body = Encoding.UTF8.GetBytes(actual + "\n" + schema + "\n" + allocationIdentity);
        return new Web6CutoverSqlOwnershipReceipt(SqlTestPrivateRecord.Hash(body));
    }
}

internal static class Web6CutoverSqlApproval
{
    internal static async Task VerifyFileAsync(string path, string adminConnectionString, CancellationToken token)
    {
        path = Web6CandidateFileSystem.CanonicalPath(path);
        Web6CandidateFileSystem.RequireDirectories(Path.GetDirectoryName(path)!);
        Web6CandidateFileSystem.RequireRegularFile(path);
        using var identity = WindowsIdentity.GetCurrent();
        Web6CandidateManifest.RequirePrivateOwner(File.GetAccessControl(path, SqlTestPrivateRecord.Sections), identity.User!);
        string text;
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Web6CandidateFileSystem.Identity(file.SafeFileHandle, directory: false);
            if (file.Length is < 2 or > 4096) throw new IOException("Cutover SQL approval size is invalid.");
            using var reader = new StreamReader(file, new UTF8Encoding(false, true), false, 1024, leaveOpen: false);
            text = await reader.ReadToEndAsync().ConfigureAwait(false);
        }
        using var connection = new SqlConnection(new SqlConnectionStringBuilder(adminConnectionString)
        { InitialCatalog = "master", Pooling = false }.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        using var command = connection.CreateCommand(); command.CommandText = "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'));";
        var server = (string?)await command.ExecuteScalarAsync(token).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(server)) throw new IOException("Cutover SQL approval server is unavailable.");
        ParseAndVerify(text, SqlTestPrivateRecord.Hash(Encoding.UTF8.GetBytes(server)), DateTimeOffset.UtcNow);
    }

    internal static void ParseAndVerify(string text, string expectedServerHash, DateTimeOffset now)
    {
        JObject json;
        try { json = JObject.Parse(text, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); }
        catch (Exception error) { throw new IOException("Cutover SQL approval is invalid.", error); }
        var keys = new[] { "version", "purpose", "serverSha256", "expiresUtc" };
        if (!json.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(keys.OrderBy(name => name, StringComparer.Ordinal)) || (int?)json["version"] != 1)
            throw new IOException("Cutover SQL approval shape is invalid.");
        if ((string?)json["purpose"] != "web6-cutover" ||
            (string?)json["serverSha256"] != Web6CandidateInputs.RequireHash(expectedServerHash))
            throw new IOException("Cutover SQL approval authority refused the server or purpose.");
        if (!DateTimeOffset.TryParse((string?)json["expiresUtc"], CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var expires) || expires <= now || expires > now.AddHours(24))
            throw new IOException("Cutover SQL approval expiry is invalid.");
    }

    internal static void RequireSchema(string actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new IOException("Cutover post-migration schema fingerprint is incompatible.");
    }
}

internal sealed class Web6CutoverBrowserResult
{
    internal Web6CutoverBrowserResult(int discovered, int passed, int failed, int skipped, int retries, IEnumerable<string> markers)
    { Discovered = discovered; Passed = passed; Failed = failed; Skipped = skipped; Retries = retries; Markers = markers.ToArray(); }
    internal int Discovered { get; }
    internal int Passed { get; }
    internal int Failed { get; }
    internal int Skipped { get; }
    internal int Retries { get; }
    internal IReadOnlyList<string> Markers { get; }
}

internal sealed class Web6CutoverDrain
{
    internal Web6CutoverDrain(bool workerExited, bool queueAbsent) { WorkerExited = workerExited; QueueAbsent = queueAbsent; }
    internal bool WorkerExited { get; }
    internal bool QueueAbsent { get; }
}

internal static class Web6CutoverControl
{
    private static readonly HashSet<string> PairActions = new HashSet<string>(new[]
    {
        "oldClientNewHostOn", "oldClientNewHostOff", "newClientOldHostOn", "newClientOldHostOff",
    }, StringComparer.Ordinal);
    private static readonly HashSet<string> OperationActions = new HashSet<string>(new[]
    {
        "sameEpochRestart", "revokeGrantOn", "revokeGrantOff", "restoreEpoch",
        "committedUnavailable", "verifyNoCutoverMutation",
    }, StringComparer.Ordinal);

    internal static string Validate(JObject frame, string token)
    {
        if (frame == null || string.IsNullOrEmpty(token) || frame["id"]?.Type != JTokenType.Integer ||
            frame["token"]?.Type != JTokenType.String || (string)frame["token"]! != token ||
            frame["action"]?.Type != JTokenType.String)
            throw new InvalidDataException("Invalid WEB6 cutover control frame.");
        var action = (string)frame["action"]!;
        var cleanupGrant = action == "revokeGrantOff" && frame["operationId"] == null;
        var expected = PairActions.Contains(action) || cleanupGrant
            ? new[] { "id", "token", "action" }
            : action == "committedUnavailable" ? new[] { "id", "token", "action", "operationId", "committedSetup" }
            : OperationActions.Contains(action) ? new[] { "id", "token", "action", "operationId" } : Array.Empty<string>();
        if (expected.Length == 0 || !frame.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal)
            .SequenceEqual(expected.OrderBy(name => name, StringComparer.Ordinal)))
            throw new InvalidDataException("Invalid WEB6 cutover control frame.");
        if (OperationActions.Contains(action) && !cleanupGrant && (frame["operationId"]?.Type != JTokenType.String ||
            !Guid.TryParseExact((string)frame["operationId"]!, "D", out var operation) || operation == Guid.Empty))
            throw new InvalidDataException("Invalid WEB6 cutover control frame.");
        if (action == "committedUnavailable") Web6CommittedSetup.Parse(frame["committedSetup"] as JObject, Guid.ParseExact((string)frame["operationId"]!, "D"));
        return action;
    }
}

internal interface IWeb6CandidateCutoverOperations
{
    Task<Web6CandidateCutoverPlan> VerifyAuthority(CancellationToken token);
    Task SealProtectedManifest(JObject handles, CancellationToken token);
    Task<Web6CutoverAdmission> StartAndObserve(string browser, Web6CutoverPair pair, CancellationToken token);
    Task<Web6CutoverBrowserResult> RunAndValidate(string browser, Web6CutoverPair pair, CancellationToken token);
    Task<Web6CutoverDrain> Drain(string browser, Web6CutoverPair pair, CancellationToken token);
    Task VerifyRestore(string browser, CancellationToken token);
    Task Complete(CancellationToken token);
}

internal sealed class Web6CandidateCutoverOrchestrator
{
    private static readonly string[] RequiredMarkers =
    {
        "web6-cutover-old-client-new-host", "web6-cutover-new-client-old-host",
        "web6-cutover-revoked-grant", "web6-cutover-committed-unavailable-protected",
        "web6-cutover-same-epoch-restart", "web6-cutover-restored-epoch-rejected",
    };
    private readonly IWeb6CandidateCutoverOperations _operations;
    internal Web6CandidateCutoverOrchestrator(IWeb6CandidateCutoverOperations operations) =>
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));

    internal async Task Execute(CancellationToken token)
    {
        var plan = await _operations.VerifyAuthority(token).ConfigureAwait(false);
        await _operations.SealProtectedManifest(plan.FrontendHandles(), token).ConfigureAwait(false);
        foreach (var browser in new[] { "chromium", "edge" })
        {
            var coordinator = new Web6CandidateCutoverCoordinator(plan);
            // The frontend pack switches hosts over the owned IPC channel. Run it once per browser.
            foreach (var pair in new[] { Web6CutoverPair.Current })
            {
                Exception? failure = null;
                var started = false;
                var admitted = false;
                try
                {
                    started = true; // Start can fail after creating a process: cleanup is already armed.
                    var admission = await _operations.StartAndObserve(browser, pair, token).ConfigureAwait(false);
                    coordinator.Admit(pair, admission);
                    admitted = true;
                    var result = await _operations.RunAndValidate(browser, pair, token).ConfigureAwait(false);
                    if (result.Discovered != 3 || result.Passed != 3 || result.Failed != 0 || result.Skipped != 0 || result.Retries != 0 ||
                        !new HashSet<string>(result.Markers, StringComparer.Ordinal).SetEquals(RequiredMarkers))
                        throw new IOException("Cutover browser result did not prove every mandatory zero-skip/retry marker.");
                }
                catch (Exception error) { failure = error; throw; }
                finally
                {
                    if (started) try
                    {
                        var drain = await _operations.Drain(browser, pair, token).ConfigureAwait(false);
                        if (admitted) coordinator.Drain(pair, drain.WorkerExited, drain.QueueAbsent);
                        else if (!drain.WorkerExited || !drain.QueueAbsent)
                            throw new IOException("Cutover failed admission and did not drain its armed host/queue.");
                    }
                    catch (Exception cleanup) when (failure != null)
                    { throw new AggregateException("Cutover browser result and pair drain both failed.", failure, cleanup); }
                }
            }
            await _operations.VerifyRestore(browser, token).ConfigureAwait(false);
        }
        await _operations.Complete(token).ConfigureAwait(false);
    }
}

internal static class Web6CutoverAcceptanceGate
{
    private static readonly string[] Required =
    {
        "MYDMSVN_WEB6_CANDIDATE_ID",
        "MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY",
        "MYDMSVN_WEB6_CUTOVER_FRONTEND_ARCHIVE_DIRECTORY",
        "MYDMSVN_WEB6_CUTOVER_BACKEND_ARCHIVE_DIRECTORY",
        "MYDMSVN_WEB6_CUTOVER_FRONTEND_DIST_DIRECTORY",
        "MYDMSVN_WEB6_CUTOVER_OLD_PUBLISH_DIRECTORY",
        "MYDMSVN_WEB6_CUTOVER_CURRENT_PUBLISH_DIRECTORY",
        "MYDMSVN_WEB6_CUTOVER_BUILD_AUTHORITY_PATH",
        "MYDMSVN_WEB6_CUTOVER_BUILD_AUTHORITY_SHA256",
        "MYDMSVN_WEB6_CUTOVER_SQL_CONTROL_PARENT",
        "MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING",
        "MYDMSVN_SQL_TEST_APPROVAL_FILE",
        "MYDMSVN_WEB6_CUTOVER_CURRENT_EPOCH",
        "MYDMSVN_WEB6_CUTOVER_RESTORED_EPOCH",
        "MYDMSVN_WEB6_CANDIDATE_ROOT",
        "MYDMSVN_WEB_TEST_BROWSER_DIRECTORY",
        "MYDMSVN_WEB_TEST_TLS_THUMBPRINT",
    };

    internal static string? OrdinarySkipReason(IReadOnlyDictionary<string, string?> values) =>
        Value(values, "MYDMSVN_WEB6_CUTOVER_ENABLE") == "1" ? null :
            "WEB6 cutover requires exact explicit nonproduction opt-in.";

    internal static void RequireLive(IReadOnlyDictionary<string, string?> values)
    {
        if (Value(values, "MYDMSVN_WEB6_CUTOVER_ENABLE") != "1")
            throw new InvalidOperationException("MYDMSVN_WEB6_CUTOVER_ENABLE must equal 1.");
        var missing = Required.Where(name => string.IsNullOrWhiteSpace(Value(values, name))).ToArray();
        if (missing.Length != 0)
            throw new InvalidOperationException("WEB6 cutover BLOCKED: missing " + string.Join(",", missing));
    }

    internal static IReadOnlyDictionary<string, string?> EnvironmentValues() => Environment.GetEnvironmentVariables().Keys.Cast<object>()
        .Select(key => key.ToString()!).Where(key => key.StartsWith("MYDMSVN_", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(key => key, Environment.GetEnvironmentVariable, StringComparer.OrdinalIgnoreCase);

    private static string? Value(IReadOnlyDictionary<string, string?> values, string name) =>
        values.TryGetValue(name, out var value) ? value : null;
}

internal sealed class Web6CandidateFileSet
{
    private readonly string _backendDll, _webConfig, _iisConfig, _backendRoot, _frontendLock;
    private readonly string _web4Contract, _web5Contract, _web6Contract, _dist, _published;
    private readonly string[] _backendLocks;

    internal Web6CandidateFileSet(string backendDll, string webConfig, string iisConfig, string backendRoot,
        IEnumerable<string> backendLocks, string frontendLock, string web4Contract, string web5Contract, string web6Contract,
        string dist, string published)
    {
        _backendDll = backendDll;
        _webConfig = webConfig;
        _iisConfig = iisConfig;
        _backendRoot = backendRoot;
        _backendLocks = backendLocks.Select(path => path.Replace('/', Path.DirectorySeparatorChar)).ToArray();
        if (_backendLocks.Length == 0 || _backendLocks.Distinct(StringComparer.OrdinalIgnoreCase).Count() != _backendLocks.Length ||
            _backendLocks.Any(path => Path.IsPathRooted(path) || Path.GetFileName(path) != "packages.lock.json"))
            throw new ArgumentException("Candidate requires distinct, relative backend dependency locks.", nameof(backendLocks));
        _frontendLock = frontendLock;
        _web4Contract = web4Contract;
        _web5Contract = web5Contract;
        _web6Contract = web6Contract;
        _dist = Web6CandidateFileSystem.CanonicalPath(dist);
        _published = Web6CandidateFileSystem.CanonicalPath(published);
        if (string.Equals(_dist, _published, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Candidate requires a separate published frontend tree.", nameof(published));
    }

    internal string Measure()
    {
        var distHash = HashTree(_dist);
        var publishedHash = HashTree(_published);
        if (distHash != publishedHash) throw new IOException("Candidate frontend dist and published files differ.");
        var lockRoot = Web6CandidateFileSystem.CanonicalPath(_backendRoot);
        var locks = HashFiles(lockRoot, _backendLocks.Select(path => Path.Combine(lockRoot, path)));
        return "backendLocksSha256=" + locks + "\nfrontendLockSha256=" + HashFile(_frontendLock) +
            "\nweb4ContractSha256=" + HashTree(_web4Contract) + "\nweb5ContractSha256=" + HashTree(_web5Contract) +
            "\nweb6ContractSha256=" + HashTree(_web6Contract) + "\nbackendDllSha256=" + HashFile(_backendDll) +
            "\nwebConfigSha256=" + HashFile(_webConfig) + "\niisConfigSha256=" + HashFile(_iisConfig) +
            "\ndistRootSha256=" + distHash + "\npublishedRootSha256=" + publishedHash + "\n";
    }

    internal static string MeasureTree(string root) => HashTree(root);

    private static string HashTree(string root)
    {
        root = Web6CandidateFileSystem.CanonicalPath(root);
        Web6CandidateFileSystem.RequireDirectories(root);
        return HashFiles(root, EnumerateFiles(root));
    }

    private static IEnumerable<string> EnumerateFiles(string directory)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = Web6CandidateFileSystem.Attributes(path);
            if ((attributes & FileAttributes.Directory) == 0) yield return path;
            else
            {
                foreach (var child in EnumerateFiles(path)) yield return child;
            }
        }
    }

    private static string HashFiles(string root, IEnumerable<string> files)
    {
        var prefix = root + Path.DirectorySeparatorChar;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inventory = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in files)
        {
            var canonical = Web6CandidateFileSystem.CanonicalPath(path);
            if (!canonical.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Candidate artifact escaped its expected root.");
            var relative = canonical.Substring(prefix.Length).Replace(Path.DirectorySeparatorChar, '/');
            if (!names.Add(relative)) throw new IOException("Candidate artifact contains an ambiguous relative path.");
            inventory.Add(relative, HashFile(canonical));
        }
        if (inventory.Count == 0) throw new IOException("Candidate artifact inventory is empty.");
        var manifest = string.Concat(inventory.Select(entry =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(entry.Key)) + "=" + entry.Value + "\n"));
        return SqlTestPrivateRecord.Hash(Encoding.ASCII.GetBytes(manifest));
    }

    private static string HashFile(string path)
    {
        path = Web6CandidateFileSystem.CanonicalPath(path);
        Web6CandidateFileSystem.RequireDirectories(Path.GetDirectoryName(path)!);
        Web6CandidateFileSystem.RequireRegularFile(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Web6CandidateFileSystem.Identity(file.SafeFileHandle, directory: false);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
    }
}

internal static class Web6CandidateFileSystem
{
    internal static string CanonicalPath(string path)
    {
        if (path == null || path.Length < 4 || path.Length > 240 || path[1] != ':' || path[2] != '\\' ||
            path.IndexOf(':', 2) >= 0 || !string.Equals(Path.GetFullPath(path), path, StringComparison.Ordinal) ||
            path.Substring(3).Split('\\').Any(component => component.Length == 0 || component.EndsWith(".", StringComparison.Ordinal) ||
                component.EndsWith(" ", StringComparison.Ordinal)))
            throw new IOException("Candidate requires a canonical local path within budget.");
        return path;
    }

    internal static void RequireDirectories(string path)
    {
        var parents = new Stack<string>();
        for (var current = path; current != null; current = Path.GetDirectoryName(current)) parents.Push(current);
        foreach (var directory in parents)
            if ((Attributes(directory) & FileAttributes.Directory) == 0)
                throw new IOException("Candidate ancestor is not a directory.");
    }

    internal static FileAttributes Attributes(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
            throw new IOException("Candidate reparse point or device refused.");
        return attributes;
    }

    internal static void RequireRegularFile(string path)
    {
        if ((Attributes(path) & FileAttributes.Directory) != 0)
            throw new IOException("Candidate requires a regular file.");
    }

    internal sealed class ParentChain : IDisposable
    {
        private readonly List<SafeFileHandle> _leases = new List<SafeFileHandle>();

        internal ParentChain(string path)
        {
            var volume = Path.GetPathRoot(path)!;
            try
            {
                Pin(OpenDirectory(volume, pin: true));
                if (path.Length > volume.Length)
                    foreach (var component in path.Substring(volume.Length).Split(Path.DirectorySeparatorChar))
                        Pin(OpenRelativeDirectory(_leases[_leases.Count - 1], component, acl: null));
            }
            catch { Dispose(); throw; }
        }

        internal SafeFileHandle CreateRoot(string name, DirectorySecurity acl) =>
            OpenRelativeDirectory(_leases[_leases.Count - 1], name, acl);

        internal SafeFileHandle CreateDeletableRoot(string name, DirectorySecurity acl) =>
            OpenRelativeDirectory(_leases[_leases.Count - 1], name, acl, deleteAuthority: true);

        private void Pin(SafeFileHandle handle)
        {
            _leases.Add(handle);
            Identity(handle, directory: true);
        }

        public void Dispose()
        {
            for (var index = _leases.Count - 1; index >= 0; index--) _leases[index].Dispose();
        }
    }

    // Open each child relative to the already pinned parent. Create-new returns the original root handle
    // with the private descriptor already applied, without another lookup through the absolute path.
    private static SafeFileHandle OpenRelativeDirectory(SafeFileHandle parent, string name, DirectorySecurity? acl,
        bool deleteAuthority = false)
    {
        const uint caseInsensitive = 0x40, dontReparse = 0x1000, readAndListAttributes = 0x81, delete = 0x10000, shareRead = 1;
        const uint openExisting = 1, createNew = 2, directoryWithoutFollowing = 0x00200001;
        var buffer = IntPtr.Zero;
        var objectName = IntPtr.Zero;
        var descriptor = default(GCHandle);
        var retained = false;
        try
        {
            parent.DangerousAddRef(ref retained);
            buffer = Marshal.StringToHGlobalUni(name);
            objectName = Marshal.AllocHGlobal(Marshal.SizeOf<NativeUnicodeString>());
            Marshal.StructureToPtr(new NativeUnicodeString
            {
                Length = checked((ushort)(name.Length * sizeof(char))),
                MaximumLength = checked((ushort)((name.Length + 1) * sizeof(char))),
                Buffer = buffer
            }, objectName, fDeleteOld: false);
            if (acl != null) descriptor = GCHandle.Alloc(acl.GetSecurityDescriptorBinaryForm(), GCHandleType.Pinned);
            var attributes = new NativeObjectAttributes
            {
                Size = Marshal.SizeOf<NativeObjectAttributes>(),
                RootDirectory = parent.DangerousGetHandle(),
                ObjectName = objectName,
                Attributes = caseInsensitive | dontReparse,
                SecurityDescriptor = descriptor.IsAllocated ? descriptor.AddrOfPinnedObject() : IntPtr.Zero
            };
            var status = NtCreateFile(out var handle, readAndListAttributes | (deleteAuthority ? delete : 0), ref attributes, out _, IntPtr.Zero,
                FileAttributes.Normal, shareRead, acl == null ? openExisting : createNew, directoryWithoutFollowing, IntPtr.Zero, 0);
            if (status >= 0 && handle != null && !handle.IsInvalid) return handle;
            handle?.Dispose();
            throw new IOException("Candidate directory could not be pinned or created through its owned parent.");
        }
        finally
        {
            if (descriptor.IsAllocated) descriptor.Free();
            Marshal.FreeHGlobal(objectName);
            Marshal.FreeHGlobal(buffer);
            if (retained) parent.DangerousRelease();
        }
    }

    internal static SafeFileHandle OpenDirectory(string path, bool pin = false)
    {
        const uint readAttributes = 0x80, listDirectory = 1, shareRead = 1, shareReadWrite = 3;
        const uint openExisting = 3, openDirectoryWithoutFollowing = 0x02200000;
        var handle = CreateFile(path, pin ? readAttributes | listDirectory : readAttributes, pin ? shareRead : shareReadWrite,
            IntPtr.Zero, openExisting, openDirectoryWithoutFollowing, IntPtr.Zero);
        if (!handle.IsInvalid) return handle;
        handle.Dispose();
        throw new IOException("Candidate control directory could not be pinned.");
    }

    internal static string Identity(SafeFileHandle handle, bool directory)
    {
        if (!GetFileInformationByHandle(handle, out var info) ||
            (info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0 ||
            ((info.Attributes & FileAttributes.Directory) != 0) != directory ||
            (!directory && info.Links != 1))
            throw new IOException("Candidate filesystem identity could not be verified.");
        return FormattableString.Invariant($"{info.Volume:X8}:{info.IndexHigh:X8}{info.IndexLow:X8}:{info.CreatedHigh:X8}{info.CreatedLow:X8}");
    }

    internal static void DeleteChildrenRelative(SafeFileHandle parent, string path, Action<string>? beforeOpen = null)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(path).ToArray())
        {
            var name = System.IO.Path.GetFileName(entry);
            beforeOpen?.Invoke(entry);
            using var child = OpenRelativeForDelete(parent, name);
            var directory = IsDirectory(child);
            _ = Identity(child, directory);
            if (directory) DeleteChildrenRelative(child, entry, beforeOpen);
            MarkDirectoryForDeletion(child);
            child.Dispose();
            if (Directory.Exists(entry) || File.Exists(entry))
                throw new IOException("Private child deletion was not observed.");
        }
    }

    private static SafeFileHandle OpenRelativeForDelete(SafeFileHandle parent, string name)
    {
        const uint caseInsensitive = 0x40, dontReparse = 0x1000, readAttributes = 0x80, listDirectory = 1,
            delete = 0x10000, shareRead = 1, openExisting = 1, openReparsePoint = 0x00200000;
        var buffer = IntPtr.Zero; var objectName = IntPtr.Zero; var retained = false;
        try
        {
            parent.DangerousAddRef(ref retained);
            buffer = Marshal.StringToHGlobalUni(name);
            objectName = Marshal.AllocHGlobal(Marshal.SizeOf<NativeUnicodeString>());
            Marshal.StructureToPtr(new NativeUnicodeString
            {
                Length = checked((ushort)(name.Length * sizeof(char))),
                MaximumLength = checked((ushort)((name.Length + 1) * sizeof(char))), Buffer = buffer
            }, objectName, false);
            var attributes = new NativeObjectAttributes
            {
                Size = Marshal.SizeOf<NativeObjectAttributes>(), RootDirectory = parent.DangerousGetHandle(), ObjectName = objectName,
                Attributes = caseInsensitive | dontReparse
            };
            var status = NtCreateFile(out var handle, readAttributes | listDirectory | delete, ref attributes, out _, IntPtr.Zero,
                FileAttributes.Normal, shareRead, openExisting, openReparsePoint, IntPtr.Zero, 0);
            if (status >= 0 && handle != null && !handle.IsInvalid) return handle;
            handle?.Dispose();
            throw new IOException("Private child could not be pinned for cleanup.");
        }
        finally
        {
            Marshal.FreeHGlobal(objectName); Marshal.FreeHGlobal(buffer);
            if (retained) parent.DangerousRelease();
        }
    }

    private static bool IsDirectory(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var info) ||
            (info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0 ||
            (info.Attributes & FileAttributes.Directory) == 0 && info.Links != 1)
            throw new IOException("Private child identity could not be verified.");
        return (info.Attributes & FileAttributes.Directory) != 0;
    }

    internal static void MarkDirectoryForDeletion(SafeFileHandle handle)
    {
        var disposition = new NativeFileDispositionInfo { DeleteFile = true };
        if (!SetFileInformationByHandle(handle, 4, ref disposition,
            (uint)Marshal.SizeOf(typeof(NativeFileDispositionInfo))))
            throw new IOException("Candidate private directory could not be deleted through its retained identity.",
                new Win32Exception(Marshal.GetLastWin32Error()));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeUnicodeString
    {
        internal ushort Length, MaximumLength;
        internal IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeObjectAttributes
    {
        internal int Size;
        internal IntPtr RootDirectory, ObjectName;
        internal uint Attributes;
        internal IntPtr SecurityDescriptor, SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeIoStatus
    {
        internal IntPtr Status;
        internal UIntPtr Information;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        internal FileAttributes Attributes;
        internal uint CreatedLow, CreatedHigh, AccessedLow, AccessedHigh, WrittenLow, WrittenHigh;
        internal uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileDispositionInfo
    {
        [MarshalAs(UnmanagedType.Bool)] internal bool DeleteFile;
    }

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtCreateFile(out SafeFileHandle handle, uint access, ref NativeObjectAttributes attributes,
        out NativeIoStatus status, IntPtr allocationSize, FileAttributes fileAttributes, uint share, uint disposition,
        uint options, IntPtr extendedAttributes, uint extendedAttributesLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation info);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass,
        ref NativeFileDispositionInfo information, uint bufferSize);
}

// Create-new private directory whose parent chain and native identity remain pinned for its full use.
// Deletion is issued through the retained root handle only after exact ACL/identity and emptiness checks.
internal sealed class Web6PrivateDirectoryLease : IDisposable
{
    private readonly Web6CandidateFileSystem.ParentChain _parents;
    private readonly SecurityIdentifier _owner;
    private readonly SecurityIdentifier? _additionalFullControl;
    private readonly string _identity;
    private readonly string _acl;
    private SafeFileHandle? _handle;
    private bool _deleted;

    private Web6PrivateDirectoryLease(string path, Web6CandidateFileSystem.ParentChain parents,
        SafeFileHandle handle, SecurityIdentifier owner, SecurityIdentifier? additionalFullControl)
    {
        Path = path; _parents = parents; _handle = handle; _owner = owner; _additionalFullControl = additionalFullControl;
        _identity = Web6CandidateFileSystem.Identity(handle, directory: true);
        var security = Directory.GetAccessControl(path, SqlTestPrivateRecord.Sections);
        RequireLeaseAcl(security);
        _acl = security.GetSecurityDescriptorSddlForm(SqlTestPrivateRecord.Sections);
        Verify();
    }

    internal string Path { get; }

    internal static Web6PrivateDirectoryLease CreateNew(string path)
        => CreateNew(path, null);

    internal static Web6PrivateDirectoryLease CreateNew(string path, SecurityIdentifier? additionalFullControl)
    {
        path = Web6CandidateFileSystem.CanonicalPath(path);
        if (path.Length + 30 > 240 || System.IO.Path.GetDirectoryName(path) is not string parent)
            throw new IOException("Private directory exceeds its path budget.");
        using var current = WindowsIdentity.GetCurrent();
        var owner = current.User ?? throw new IOException("Private directory owner is unavailable.");
        var parents = new Web6CandidateFileSystem.ParentChain(parent);
        SafeFileHandle? handle = null;
        try
        {
            var acl = SqlTestPrivateRecord.DirectoryAcl(owner);
            if (additionalFullControl != null && additionalFullControl != owner)
                acl.AddAccessRule(new FileSystemAccessRule(additionalFullControl, FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            handle = parents.CreateDeletableRoot(System.IO.Path.GetFileName(path), acl);
            return new Web6PrivateDirectoryLease(path, parents, handle, owner, additionalFullControl);
        }
        catch { handle?.Dispose(); parents.Dispose(); throw; }
    }

    internal void Verify()
    {
        var handle = _handle ?? throw new ObjectDisposedException(nameof(Web6PrivateDirectoryLease));
        if (_deleted || Web6CandidateFileSystem.Identity(handle, directory: true) != _identity ||
            !Directory.Exists(Path) || (File.GetAttributes(Path) & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
            throw new IOException("Private directory identity changed.");
        var security = Directory.GetAccessControl(Path, SqlTestPrivateRecord.Sections);
        RequireLeaseAcl(security);
        if (security.GetSecurityDescriptorSddlForm(SqlTestPrivateRecord.Sections) != _acl)
            throw new IOException("Private directory ACL changed.");
    }

    private void RequireLeaseAcl(DirectorySecurity security)
    {
        if (_additionalFullControl == null) { Web6CandidateManifest.RequirePrivateOwner(security, _owner); return; }
        if (!security.AreAccessRulesProtected || !Equals(security.GetOwner(typeof(SecurityIdentifier)), _owner))
            throw new IOException("Private directory owner or ACL protection changed.");
        var allowed = new HashSet<SecurityIdentifier>(new[] { _owner, _additionalFullControl });
        var full = new HashSet<SecurityIdentifier>();
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, false, typeof(SecurityIdentifier)))
        {
            var sid = (SecurityIdentifier)rule.IdentityReference;
            if (rule.AccessControlType != AccessControlType.Allow || !allowed.Contains(sid))
                throw new IOException("Private directory ACL contains foreign authority.");
            if ((rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl) full.Add(sid);
        }
        if (!full.SetEquals(allowed)) throw new IOException("Private directory full-control authority changed.");
    }

    internal void DeleteVerified()
    {
        Verify();
        if (Directory.EnumerateFileSystemEntries(Path).Any())
            throw new IOException("Private directory cleanup requires an empty verified root.");
        var handle = _handle!;
        Web6CandidateFileSystem.MarkDirectoryForDeletion(handle);
        handle.Dispose(); _handle = null; _deleted = true;
        if (Directory.Exists(Path) || File.Exists(Path))
            throw new IOException("Private directory deletion was not observed.");
        _parents.Dispose();
    }

    internal void DeleteTreeVerified(Action<string>? beforeChildOpen = null)
    {
        Verify();
        Web6CandidateFileSystem.DeleteChildrenRelative(_handle!, Path, beforeChildOpen);
        DeleteVerified();
    }

    public void Dispose()
    {
        _handle?.Dispose(); _handle = null; _parents.Dispose();
    }
}

internal sealed class Web6CandidateObservation
{
    internal Web6CandidateObservation((string Sha, string Tree, bool Clean) backend, (string Sha, string Tree, bool Clean) frontend,
        string schema13Fingerprint, string certificateHash, string? verifiedSqlReceiptHash, IEnumerable<Web5HttpRequestQueue> queues,
        Web6CandidateAuditLaunch? auditLaunch)
    {
        Backend = backend;
        Frontend = frontend;
        Schema13Fingerprint = schema13Fingerprint;
        CertificateHash = certificateHash;
        VerifiedSqlReceiptHash = verifiedSqlReceiptHash;
        Queues = queues.ToArray();
        AuditLaunch = auditLaunch;
    }

    internal (string Sha, string Tree, bool Clean) Backend { get; }
    internal (string Sha, string Tree, bool Clean) Frontend { get; }
    internal string Schema13Fingerprint { get; }
    internal string CertificateHash { get; }
    internal string? VerifiedSqlReceiptHash { get; }
    internal IReadOnlyList<Web5HttpRequestQueue> Queues { get; }
    internal Web6CandidateAuditLaunch? AuditLaunch { get; }
}

// The owned launcher binds its effective candidate audit configuration to the verified process.
internal sealed class Web6CandidateAuditLaunch
{
    internal Web6CandidateAuditLaunch(int processId, DateTime startTimeUtc, string root, string auditPath, string candidateId)
    {
        ProcessId = processId;
        StartTimeUtc = startTimeUtc;
        Root = root;
        AuditPath = auditPath;
        CandidateId = candidateId;
    }

    internal int ProcessId { get; }
    internal DateTime StartTimeUtc { get; }
    internal string Root { get; }
    internal string AuditPath { get; }
    internal string CandidateId { get; }
}

internal enum Web6CandidateStage { Chromium, Edge, Cutover, Workload, Recovery, Cleanup }
internal enum Web6CandidateOutcome { Passed, Failed, Blocked, Quarantined }

// Live authority for one create-new candidate. It never adopts an existing record or owns SQL cleanup.
internal sealed class Web6CandidateManifest : IDisposable
{
    private readonly object _gate = new object();
    private readonly Web6CandidateInputs _inputs;
    private readonly SecurityIdentifier _owner;
    private readonly Web6CandidateFileSystem.ParentChain _parents;
    private readonly SafeFileHandle _rootLease;
    private readonly string _rootId;
    private readonly string _rootAcl;
    private readonly Dictionary<string, SealedRecord> _records = new Dictionary<string, SealedRecord>(StringComparer.Ordinal);
    private readonly Dictionary<string, PrivateInput> _privateInputs = new Dictionary<string, PrivateInput>(StringComparer.Ordinal);
    private readonly Dictionary<string, PrivateOutput> _privateOutputs = new Dictionary<string, PrivateOutput>(StringComparer.Ordinal);
    private Web6CandidateFileSet? _files;
    private string? _artifacts, _sqlReceipt, _manifestHash, _auditId, _auditAcl;
    private State _state;

    private enum State { Created, Sealing, Sealed, Disposed }

    private string Root { get; }
    internal string AuditPath => Path.Combine(Root, "security-audit.log");
    private string ManifestPath => Path.Combine(Root, "candidate.manifest");

    private Web6CandidateManifest(string root, Web6CandidateInputs inputs, SecurityIdentifier owner,
        Web6CandidateFileSystem.ParentChain parents, SafeFileHandle rootLease)
    {
        Root = root;
        _inputs = inputs;
        _owner = owner;
        _parents = parents;
        _rootLease = rootLease;
        _rootId = Web6CandidateFileSystem.Identity(rootLease, directory: true);
        _rootAcl = DirectoryAcl();
        VerifyRoot();
        RequireChildren(includeAudit: false);
    }

    internal static Web6CandidateManifest CreateNew(string root, Web6CandidateInputs inputs)
    {
        if (inputs == null) throw new ArgumentNullException(nameof(inputs));
        root = Web6CandidateFileSystem.CanonicalPath(root);
        if (root.Length + 30 > 240 || Path.GetDirectoryName(root) is not string parent)
            throw new IOException("Candidate control root exceeds its path budget.");
        using var current = WindowsIdentity.GetCurrent();
        var owner = current.User ?? throw new IOException("Candidate owner is unavailable.");
        var acl = SqlTestPrivateRecord.DirectoryAcl(owner);
        var parents = new Web6CandidateFileSystem.ParentChain(parent);
        SafeFileHandle? lease = null;
        try
        {
            lease = parents.CreateRoot(Path.GetFileName(root), acl);
            return new Web6CandidateManifest(root, inputs, owner, parents, lease);
        }
        catch { lease?.Dispose(); parents.Dispose(); throw; }
    }

    internal void SealArtifacts(Web6CandidateFileSet files, string sqlOwnershipReceiptHash)
    {
        lock (_gate)
        {
            RequireState(State.Created);
            if (files == null) throw new ArgumentNullException(nameof(files));
            var receipt = Web6CandidateInputs.RequireHash(sqlOwnershipReceiptHash);
            VerifyRoot();
            RequireChildren(includeAudit: true);
            var audit = ReadAudit();
            var artifacts = files.Measure();
            var body = "version=1\nmode=web6-candidate\ncandidateId=" + _inputs.CandidateId +
                "\nbackendSha=" + _inputs.Backend.Sha + "\nbackendTree=" + _inputs.Backend.Tree +
                "\nfrontendSha=" + _inputs.Frontend.Sha + "\nfrontendTree=" + _inputs.Frontend.Tree +
                "\nschema13=" + _inputs.Schema13Fingerprint + "\norigin=" + _inputs.Origin.GetLeftPart(UriPartial.Authority) +
                "\ncertificateSha256=" + _inputs.CertificateHash + "\nsqlReceiptSha256=" + receipt +
                "\nbrowsers=chromium,edge\nselector=" + _inputs.Selector + "\nbrowserSelector=" + _inputs.BrowserSelector +
                "\nrunSeconds=" + _inputs.RunSeconds.ToString(CultureInfo.InvariantCulture) +
                "\nshutdownSeconds=" + _inputs.ShutdownSeconds.ToString(CultureInfo.InvariantCulture) +
                "\nworkers=1\nstopRule=fail-closed\n" + artifacts;
            _state = State.Sealing;
            _manifestHash = WriteRecord(ManifestPath, body);
            _auditId = audit.Id;
            _auditAcl = audit.Acl;
            _files = files;
            _artifacts = artifacts;
            _sqlReceipt = receipt;
            VerifyRecords();
            _state = State.Sealed;
        }
    }

    internal void VerifyBeforeAdmission(int expectedProcessId, DateTime expectedProcessStartUtc, string expectedQueueName,
        Func<Web6CandidateObservation> observe)
    {
        lock (_gate)
        {
            RequireState(State.Sealed);
            if (expectedProcessId <= 0 || expectedProcessStartUtc.Kind != DateTimeKind.Utc || expectedProcessStartUtc == default ||
                string.IsNullOrWhiteSpace(expectedQueueName))
                throw new IOException("Candidate requires an expected IIS Express process identity and HTTP queue.");
            VerifyAdmissionFiles();

            var current = observe();
            if (current == null || current.Backend != (_inputs.Backend.Sha, _inputs.Backend.Tree, true) ||
                current.Frontend != (_inputs.Frontend.Sha, _inputs.Frontend.Tree, true) ||
                current.Schema13Fingerprint != _inputs.Schema13Fingerprint || current.CertificateHash != _inputs.CertificateHash ||
                current.VerifiedSqlReceiptHash != _sqlReceipt)
                throw new IOException("Candidate source, schema, certificate or SQL ownership could not be verified.");

            var launch = current.AuditLaunch;
            if (launch == null || launch.ProcessId != expectedProcessId || launch.StartTimeUtc.Kind != DateTimeKind.Utc ||
                launch.StartTimeUtc != expectedProcessStartUtc || launch.Root != Root || launch.AuditPath != AuditPath ||
                launch.CandidateId != _inputs.CandidateId)
                throw new IOException("Candidate audit configuration was not verified for the admitted process.");

            Web5HttpRequestQueue queue;
            try { queue = Web5HttpServiceState.RequireOwnedQueue(current.Queues, _inputs.Origin, expectedProcessId); }
            catch (InvalidDataException) { throw new IOException("Candidate does not have exactly one owned worker and controller."); }
            if (!string.Equals(queue.Name, expectedQueueName, StringComparison.Ordinal))
                throw new IOException("Candidate HTTP queue changed.");
            VerifyAdmissionFiles();
        }
    }

    private void VerifyAdmissionFiles()
    {
        VerifyRecords();
        if (_files!.Measure() != _artifacts || ReadAudit() != (_auditId, _auditAcl))
            throw new IOException("Candidate artifacts or protected audit destination changed.");
    }

    internal void VerifyForWorkload(string expectedCandidateId)
    {
        lock (_gate)
        {
            RequireState(State.Sealed);
            if (!string.Equals(_inputs.CandidateId, expectedCandidateId, StringComparison.Ordinal))
                throw new IOException("Candidate identity changed before workload observation.");
            VerifyAdmissionFiles();
        }
    }

    internal void RecordDisposition(Web6CandidateStage stage, Web6CandidateOutcome outcome)
    {
        lock (_gate)
        {
            RequireState(State.Sealed);
            if (!Enum.IsDefined(typeof(Web6CandidateStage), stage) || !Enum.IsDefined(typeof(Web6CandidateOutcome), outcome))
                throw new ArgumentOutOfRangeException(nameof(stage), "Candidate disposition is outside the allowlist.");
            VerifyRecords();
            var path = Path.Combine(Root, "disposition-" + stage.ToString().ToLowerInvariant() + ".record");
            if (_records.ContainsKey(path)) throw new InvalidOperationException("Candidate stage was already recorded.");
            WriteRecord(path, "version=1\ncandidateId=" + _inputs.CandidateId + "\nmanifestSha256=" + _manifestHash +
                "\nstage=" + stage + "\noutcome=" + outcome + "\n");
        }
    }

    internal PrivateInput CreatePrivateInput(string name, byte[] payload)
    {
        lock (_gate)
        {
            RequireState(State.Sealed);
            if (string.IsNullOrEmpty(name) || Path.GetFileName(name) != name || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                !name.EndsWith(".json", StringComparison.Ordinal) || payload == null || payload.Length == 0 || payload.Length > 1048576)
                throw new IOException("Candidate private input name or payload is invalid.");
            VerifyRecords();
            var path = Path.Combine(Root, name);
            if (_privateInputs.ContainsKey(path)) throw new IOException("Candidate private input already exists.");
            string id, acl;
            FileStream? reader = null;
            try
            {
                using (var writer = new FileStream(path, FileMode.CreateNew, FileSystemRights.ReadData | FileSystemRights.WriteData,
                    FileShare.Read, 4096, FileOptions.WriteThrough, SqlTestPrivateRecord.FileAcl(_owner)))
                {
                    File.SetAccessControl(path, SqlTestPrivateRecord.FileAcl(_owner));
                    id = Web6CandidateFileSystem.Identity(writer.SafeFileHandle, directory: false);
                    writer.Write(payload, 0, payload.Length);
                    writer.Flush(flushToDisk: true);
                }
                acl = FileAcl(path);
                reader = new FileStream(path, FileMode.Open, FileSystemRights.ReadData | FileSystemRights.Delete,
                    FileShare.Read, 4096, FileOptions.None);
                var input = new PrivateInput(this, path, reader, id, acl, (byte[])payload.Clone());
                _privateInputs.Add(path, input);
                input.Verify();
                RequireChildren(includeAudit: true);
                return input;
            }
            catch { reader?.Dispose(); if (File.Exists(path)) File.Delete(path); throw; }
        }
    }

    private void RemovePrivateInput(PrivateInput input)
    {
        lock (_gate)
        {
            if (!_privateInputs.TryGetValue(input.Path, out var current) || !ReferenceEquals(input, current)) return;
            input.VerifyCore();
            DeleteRetained(input.Reader, input.Path);
            if (File.Exists(input.Path)) throw new IOException("Candidate private input cleanup was ambiguous.");
            _privateInputs.Remove(input.Path);
            RequireChildren(includeAudit: true);
            input.MarkDisposed();
        }
    }

    internal PrivateOutput CreatePrivateOutput(string name)
    {
        lock (_gate)
        {
            RequireState(State.Sealed);
            if (string.IsNullOrEmpty(name) || Path.GetFileName(name) != name || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !name.EndsWith(".json", StringComparison.Ordinal))
                throw new IOException("Candidate private output name is invalid.");
            VerifyRecords();
            var path = Path.Combine(Root, name);
            if (_privateOutputs.ContainsKey(path)) throw new IOException("Candidate private output already exists.");
            FileStream? stream = null;
            try
            {
                stream = new FileStream(path, FileMode.CreateNew,
                    FileSystemRights.ReadData | FileSystemRights.WriteData | FileSystemRights.Delete,
                    FileShare.ReadWrite, 4096, FileOptions.WriteThrough, SqlTestPrivateRecord.FileAcl(_owner));
                File.SetAccessControl(path, SqlTestPrivateRecord.FileAcl(_owner));
                var output = new PrivateOutput(this, path, stream,
                    Web6CandidateFileSystem.Identity(stream.SafeFileHandle, directory: false), FileAcl(path));
                _privateOutputs.Add(path, output);
                output.VerifyCore();
                RequireChildren(includeAudit: true);
                return output;
            }
            catch { stream?.Dispose(); if (File.Exists(path)) File.Delete(path); throw; }
        }
    }

    private void RemovePrivateOutput(PrivateOutput output)
    {
        lock (_gate)
        {
            if (!_privateOutputs.TryGetValue(output.Path, out var current) || !ReferenceEquals(output, current)) return;
            output.VerifyCore();
            DeleteRetained(output.Stream, output.Path);
            if (File.Exists(output.Path)) throw new IOException("Candidate private output cleanup was ambiguous.");
            _privateOutputs.Remove(output.Path);
            RequireChildren(includeAudit: true);
            output.MarkDisposed();
        }
    }

    private static void DeleteRetained(FileStream stream, string path)
    {
        var disposition = new FileDispositionInfo { DeleteFile = true };
        if (!SetFileInformationByHandle(stream.SafeFileHandle, 4, ref disposition, (uint)Marshal.SizeOf(typeof(FileDispositionInfo))))
            throw new IOException("Candidate retained-handle deletion failed for " + Path.GetFileName(path) + ".", new Win32Exception(Marshal.GetLastWin32Error()));
        stream.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInfo { [MarshalAs(UnmanagedType.Bool)] internal bool DeleteFile; }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass,
        ref FileDispositionInfo information, uint bufferSize);

    private string WriteRecord(string path, string body)
    {
        var payload = SqlTestPrivateRecord.Seal(body);
        string fileId;
        using (var writer = new FileStream(path, FileMode.CreateNew, FileSystemRights.ReadData | FileSystemRights.WriteData,
            FileShare.None, 4096, FileOptions.WriteThrough, SqlTestPrivateRecord.FileAcl(_owner)))
        {
            // Normalize the protected descriptor before pinning it, as for the private root.
            File.SetAccessControl(path, SqlTestPrivateRecord.FileAcl(_owner));
            fileId = Web6CandidateFileSystem.Identity(writer.SafeFileHandle, directory: false);
            writer.Write(payload, 0, payload.Length);
            writer.Flush(flushToDisk: true);
        }

        var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            var record = new SealedRecord(reader, fileId, FileAcl(path), payload);
            record.Verify(path, FileAcl(path));
            _records.Add(path, record);
            return SqlTestPrivateRecord.Hash(payload);
        }
        catch { reader.Dispose(); throw; }
    }

    private void VerifyRecords()
    {
        VerifyRoot();
        RequireChildren(includeAudit: true);
        foreach (var entry in _records) entry.Value.Verify(entry.Key, FileAcl(entry.Key));
        foreach (var input in _privateInputs.Values) input.VerifyCore();
        foreach (var output in _privateOutputs.Values) output.VerifyCore();
    }

    private void VerifyRoot()
    {
        using var current = WindowsIdentity.GetCurrent();
        if (!_owner.Equals(current.User)) throw new IOException("Candidate requires the exact original owner.");
        Web6CandidateFileSystem.RequireDirectories(Root);
        using var fresh = Web6CandidateFileSystem.OpenDirectory(Root);
        if (Web6CandidateFileSystem.Identity(fresh, directory: true) != _rootId || DirectoryAcl() != _rootAcl)
            throw new IOException("Candidate control root identity or ACL changed.");
    }

    private void RequireChildren(bool includeAudit)
    {
        var expected = new HashSet<string>(_records.Keys, StringComparer.Ordinal);
        expected.UnionWith(_privateInputs.Keys);
        expected.UnionWith(_privateOutputs.Keys);
        if (includeAudit) expected.Add(AuditPath);
        if (!expected.SetEquals(Directory.EnumerateFileSystemEntries(Root)))
            throw new IOException("Candidate control root contains missing or unrecognized children.");
    }

    private (string Id, string Acl) ReadAudit()
    {
        var acl = FileAcl(AuditPath);
        using var audit = new FileStream(AuditPath, FileMode.Open, FileAccess.Write, FileShare.Read);
        var id = Web6CandidateFileSystem.Identity(audit.SafeFileHandle, directory: false);
        audit.Flush(flushToDisk: true);
        return (id, acl);
    }

    private string DirectoryAcl() => ReadAcl(Directory.GetAccessControl(Root, SqlTestPrivateRecord.Sections));

    private string FileAcl(string path)
    {
        Web6CandidateFileSystem.RequireRegularFile(path);
        return ReadAcl(File.GetAccessControl(path, SqlTestPrivateRecord.Sections));
    }

    private string ReadAcl(FileSystemSecurity acl)
    {
        RequirePrivateOwner(acl, _owner);
        return acl.GetSecurityDescriptorSddlForm(SqlTestPrivateRecord.Sections);
    }

    internal static void RequirePrivateOwner(FileSystemSecurity acl, SecurityIdentifier owner)
    {
        var rules = acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
        if (!acl.AreAccessRulesProtected || !owner.Equals(acl.GetOwner(typeof(SecurityIdentifier))) ||
            rules.Length != 1 || !owner.Equals(rules[0].IdentityReference) || rules[0].IsInherited ||
            rules[0].AccessControlType != AccessControlType.Allow || rules[0].FileSystemRights != FileSystemRights.FullControl)
            throw new IOException("Candidate filesystem owner or protected ACL refused.");
    }

    private void RequireState(State expected)
    {
        if (_state == State.Disposed) throw new ObjectDisposedException(nameof(Web6CandidateManifest));
        if (_state != expected) throw new InvalidOperationException("Candidate manifest lifecycle transition refused.");
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_state == State.Disposed) return;
            if (_privateInputs.Count != 0 || _privateOutputs.Count != 0) throw new IOException("Candidate private files must be verified and removed before manifest disposal.");
            _state = State.Disposed;
            foreach (var record in _records.Values) record.Reader.Dispose();
            _rootLease.Dispose();
            _parents.Dispose();
        }
    }

    internal sealed class PrivateInput : IDisposable
    {
        private readonly Web6CandidateManifest _owner;
        private readonly string _id, _acl;
        private readonly byte[] _payload;
        private bool _disposed;
        internal PrivateInput(Web6CandidateManifest owner, string path, FileStream reader, string id, string acl, byte[] payload)
        { _owner = owner; Path = path; Reader = reader; _id = id; _acl = acl; _payload = payload; }
        internal string Path { get; }
        internal FileStream Reader { get; }
        internal void Verify() { lock (_owner._gate) { if (_disposed) throw new ObjectDisposedException(nameof(PrivateInput)); _owner.VerifyRoot(); VerifyCore(); } }
        internal void VerifyCore()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PrivateInput));
            var acl = _owner.FileAcl(Path);
            using var fresh = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (Web6CandidateFileSystem.Identity(fresh.SafeFileHandle, directory: false) != _id || acl != _acl || fresh.Length != _payload.Length)
                throw new IOException("Candidate private input identity, ACL or length changed.");
            var actual = new byte[_payload.Length];
            using (var binary = new BinaryReader(fresh, Encoding.UTF8, leaveOpen: true)) actual = binary.ReadBytes(_payload.Length);
            if (!actual.SequenceEqual(_payload)) throw new IOException("Candidate private input contents changed.");
        }
        internal void MarkDisposed() => _disposed = true;
        public void Dispose() => _owner.RemovePrivateInput(this);
    }

    internal sealed class PrivateOutput : IDisposable
    {
        private readonly Web6CandidateManifest _owner;
        private readonly string _id, _acl;
        private bool _disposed;
        internal PrivateOutput(Web6CandidateManifest owner, string path, FileStream stream, string id, string acl)
        { _owner = owner; Path = path; Stream = stream; _id = id; _acl = acl; }
        internal string Path { get; }
        internal FileStream Stream { get; }
        internal void Clear()
        {
            Replace(Array.Empty<byte>());
        }
        internal void Replace(byte[] payload)
        {
            if (payload == null || payload.Length > 1048576) throw new IOException("Candidate private output payload is invalid.");
            lock (_owner._gate)
            {
                VerifyCore(); Stream.SetLength(0); Stream.Position = 0; Stream.Write(payload, 0, payload.Length); Stream.Flush(flushToDisk: true);
            }
        }
        internal string ReadAllText(int maximumBytes)
        {
            if (maximumBytes <= 0 || maximumBytes > 1048576) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            lock (_owner._gate)
            {
                VerifyCore();
                if (Stream.Length == 0 || Stream.Length > maximumBytes) throw new IOException("Candidate private output is missing or oversized.");
                Stream.Position = 0; var bytes = new byte[(int)Stream.Length];
                using (var reader = new BinaryReader(Stream, Encoding.UTF8, leaveOpen: true))
                    if (reader.Read(bytes, 0, bytes.Length) != bytes.Length) throw new IOException("Candidate private output read was incomplete.");
                return new UTF8Encoding(false, true).GetString(bytes);
            }
        }
        internal void Verify() { lock (_owner._gate) { if (_disposed) throw new ObjectDisposedException(nameof(PrivateOutput)); _owner.VerifyRoot(); VerifyCore(); } }
        internal void VerifyCore()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PrivateOutput));
            var acl = _owner.FileAcl(Path);
            using var fresh = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (Web6CandidateFileSystem.Identity(fresh.SafeFileHandle, directory: false) != _id || acl != _acl || fresh.Length > 1048576)
                throw new IOException("Candidate private output identity, ACL or size changed.");
        }
        internal void MarkDisposed() => _disposed = true;
        public void Dispose() => _owner.RemovePrivateOutput(this);
    }

    private sealed class SealedRecord
    {
        private readonly string _id, _acl;
        private readonly byte[] _payload;
        internal FileStream Reader { get; }

        internal SealedRecord(FileStream reader, string id, string acl, byte[] payload)
        {
            Reader = reader;
            _id = id;
            _acl = acl;
            _payload = payload;
        }

        internal void Verify(string path, string acl)
        {
            using var fresh = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (Web6CandidateFileSystem.Identity(fresh.SafeFileHandle, directory: false) != _id || acl != _acl ||
                fresh.Length != _payload.Length)
                throw new IOException("Candidate record identity, ACL or length changed.");
            var actual = new byte[_payload.Length];
            using (var reader = new BinaryReader(fresh, Encoding.UTF8, leaveOpen: true))
                actual = reader.ReadBytes(_payload.Length);
            if (!actual.SequenceEqual(_payload)) throw new IOException("Candidate record contents changed.");
            SqlTestPrivateRecord.Validate(actual);
        }
    }
}
