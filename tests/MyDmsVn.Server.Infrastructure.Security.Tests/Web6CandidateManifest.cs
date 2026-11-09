using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

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
    private static SafeFileHandle OpenRelativeDirectory(SafeFileHandle parent, string name, DirectorySecurity? acl)
    {
        const uint caseInsensitive = 0x40, dontReparse = 0x1000, readAndListAttributes = 0x81, shareRead = 1;
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
            var status = NtCreateFile(out var handle, readAndListAttributes, ref attributes, out _, IntPtr.Zero,
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

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtCreateFile(out SafeFileHandle handle, uint access, ref NativeObjectAttributes attributes,
        out NativeIoStatus status, IntPtr allocationSize, FileAttributes fileAttributes, uint share, uint disposition,
        uint options, IntPtr extendedAttributes, uint extendedAttributesLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation info);
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
            _state = State.Disposed;
            foreach (var record in _records.Values) record.Reader.Dispose();
            _rootLease.Dispose();
            _parents.Dispose();
        }
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
