using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace MyDmsVn.WebOperation.Operator;

public sealed class MaintenanceEvidencePolicy
{
    private static readonly Regex DeviceName = new Regex(
        @"\A(?:CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|(?:COM|LPT)[1-9¹²³])\z",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private readonly SecurityIdentifier _operator;
    private readonly string[] _primaryAncestry;
    private readonly string[] _backupAncestry;
    private readonly MaintenanceRecoveryRoot? _recovery;

    public MaintenanceEvidencePolicy(string primaryRoot, string backupRoot, TimeSpan retention)
        : this(primaryRoot, backupRoot, retention, null) { }

    internal MaintenanceEvidencePolicy(string primaryRoot, string backupRoot, TimeSpan retention, MaintenanceRecoveryRoot? recovery)
    {
        _recovery = recovery;
        if (retention != TimeSpan.FromDays(90))
            throw new ArgumentOutOfRangeException(nameof(retention), "Evidence retention must be exactly 90 days.");
        PrimaryRoot = RequireCanonicalRoot(primaryRoot);
        BackupRoot = RequireCanonicalRoot(backupRoot);
        if (Within(PrimaryRoot, BackupRoot) || Within(BackupRoot, PrimaryRoot))
            throw new ArgumentException("Evidence roots must be distinct and non-nested.");

        using var identity = WindowsIdentity.GetCurrent();
        _operator = identity.User ?? throw new IOException("Evidence operator identity is unavailable.");
        using var primary = EvidenceRoot.Open(PrimaryRoot, _operator, recovery: _recovery);
        using var backup = EvidenceRoot.Open(BackupRoot, _operator, recovery: _recovery);
        _primaryAncestry = primary.Identities;
        _backupAncestry = backup.Identities;
        if (_primaryAncestry.Contains(backup.Identity) || _backupAncestry.Contains(primary.Identity))
            throw new IOException("Evidence roots resolve to the same or nested directories.");
        Retention = retention;
    }

    public string PrimaryRoot { get; }
    public string BackupRoot { get; }
    public TimeSpan Retention { get; }

    internal EvidenceRoot OpenPrimary() => Open(PrimaryRoot, _primaryAncestry);
    internal EvidenceRoot OpenBackup() => Open(BackupRoot, _backupAncestry);

    private EvidenceRoot Open(string path, string[] ancestry)
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (!_operator.Equals(identity.User))
            throw new IOException("Evidence operator identity changed.");
        return EvidenceRoot.Open(path, _operator, ancestry, _recovery);
    }

    private static string RequireCanonicalRoot(string path)
    {
        if (string.IsNullOrEmpty(path) || path.Length < 4 ||
            !((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z')) ||
            path[1] != ':' || path[2] != '\\' || path.IndexOf(':', 2) >= 0 || path.IndexOf('/') >= 0 ||
            !string.Equals(path, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase) ||
            path.Substring(3).Split('\\').Any(part => part.Length == 0 || part.EndsWith(".", StringComparison.Ordinal) ||
                part.EndsWith(" ", StringComparison.Ordinal) || IsDeviceName(part)))
            throw new ArgumentException("Evidence roots must be canonical absolute local directory paths.");
        return path;
    }

    private static bool Within(string path, string parent) => string.Equals(path, parent, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(parent + "\\", StringComparison.OrdinalIgnoreCase);

    internal static bool IsDeviceName(string name)
    {
        var extension = name.IndexOf('.');
        var stem = extension < 0 ? name : name.Substring(0, extension).TrimEnd(' ');
        return DeviceName.IsMatch(stem);
    }
}

// Internal recovery-only bridge. The caller retains a create-new DELETE lease that
// denies competing rename/delete and pins ancestry; every use revalidates its ACL.
// Sharing is granted only to the exact native directory represented by that lease.
internal sealed class MaintenanceRecoveryRoot
{
    internal MaintenanceRecoveryRoot(string path, SafeFileHandle handle, Action verify)
    { Path = path; Handle = handle; Verify = verify; }
    internal string Path { get; }
    internal SafeFileHandle Handle { get; }
    internal Action Verify { get; }
}

internal sealed class EvidenceRoot : IDisposable
{
    private readonly SecurityIdentifier _operator;
    private readonly List<EvidenceEntry> _ancestry = new List<EvidenceEntry>();
    private MaintenanceRecoveryRoot? _recovery;
    private bool _retained;

    private EvidenceRoot(SecurityIdentifier @operator) => _operator = @operator;

    internal string Identity => Directory.Identity;
    internal string[] Identities => _ancestry.Select(entry => entry.Identity).ToArray();
    private EvidenceEntry Directory => _ancestry[_ancestry.Count - 1];

    internal static EvidenceRoot Open(string path, SecurityIdentifier @operator, string[]? expected = null, MaintenanceRecoveryRoot? recovery = null)
    {
        var result = new EvidenceRoot(@operator);
        try
        {
            if (recovery != null)
            {
                recovery.Verify();
                if (!path.StartsWith(recovery.Path + "\\", StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Evidence is outside the retained recovery root.");
                result._recovery = recovery;
                recovery.Handle.DangerousAddRef(ref result._retained);
            }
            var drive = path.Substring(0, 3);
            var driveEntry = EvidenceEntry.OpenDrive(drive);
            result._ancestry.Add(driveEntry);
            var components = path.Substring(3).Split('\\');
            var current = drive.TrimEnd('\\');
            var matched = recovery == null;
            for (var index = 0; index < components.Length; index++)
            {
                current += "\\" + components[index];
                var isRoot = index == components.Length - 1;
                var leased = recovery != null && string.Equals(current, recovery.Path, StringComparison.OrdinalIgnoreCase);
                var entry = result.Directory.OpenDirectory(components[index], isRoot ? @operator : null, recoveryHandle: leased ? recovery!.Handle : null);
                result._ancestry.Add(entry);
                if (leased) matched = true;
            }

            if (!matched) throw new IOException("Retained recovery root was not verified.");

            result.Directory.VerifyFinalPath(path);
            if (expected != null && !result.Identities.SequenceEqual(expected, StringComparer.Ordinal))
                throw new IOException("Evidence root or ancestry identity changed.");
            result.Verify();
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    internal EvidenceEntry OpenCase(string caseId, bool create = false, bool delete = false)
    {
        if (!MaintenanceEvidenceRecord.IsCaseId(caseId))
            throw new IOException("Evidence case ID is invalid.");
        Verify();
        return Directory.OpenDirectory(caseId, _operator, create, delete);
    }

    internal void Verify()
    {
        _recovery?.Verify();
        foreach (var entry in _ancestry)
            entry.Verify();
    }

    internal void VerifyCaseRemoved(string caseId)
    {
        Verify();
        try
        {
            using var remaining = OpenCase(caseId);
        }
        catch (EvidenceEntryMissingException)
        {
            return;
        }
        throw new IOException("Exact evidence directory is still present.");
    }

    public void Dispose()
    {
        for (var index = _ancestry.Count - 1; index >= 0; index--)
            _ancestry[index].Dispose();
        if (_retained) { _recovery!.Handle.DangerousRelease(); _retained = false; }
    }
}

internal sealed class EvidenceEntryMissingException : IOException
{
    internal EvidenceEntryMissingException() : base("Exact evidence entry is absent.") { }
}

// Every child operation starts from an already-open directory and a single name.
// Opening each component without following a reparse point prevents a path swap
// from redirecting creation, reads or deletion to a foreign directory.
internal sealed class EvidenceEntry : IDisposable
{
    private const uint FileReadData = 0x0001;
    private const uint FileWriteData = 0x0002;
    private const uint FileTraverse = 0x0020;
    private const uint FileReadAttributes = 0x0080;
    private const uint DeleteAccess = 0x00010000;
    private const uint ReadControl = 0x00020000;
    private const uint Synchronize = 0x00100000;
    private const uint ShareRead = 1;
    private const uint OpenExisting = 1;
    private const uint CreateNew = 2;
    private const uint DirectoryFile = 0x00000001;
    private const uint NonDirectoryFile = 0x00000040;
    private const uint SynchronousIo = 0x00000020;
    private const uint OpenReparsePoint = 0x00200000;
    private const uint CaseInsensitive = 0x00000040;
    private const uint VolumeNameGuid = 1;
    private const uint FixedDrive = 3;
    private const int FileDispositionInfo = 4;
    private const int FileStreamInfo = 7;
    private const int FileFullDirectoryInfo = 14;
    private const int FileFullDirectoryRestartInfo = 15;
    private const int NoMoreFiles = 18;
    private const int HandleEof = 38;
    private const int ObjectNameNotFound = unchecked((int)0xC0000034);

    private static readonly SecurityIdentifier Administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
    private readonly SafeFileHandle _handle;
    private readonly bool _directory;
    private readonly string _volume;
    private readonly SecurityIdentifier? _operator;

    private EvidenceEntry(SafeFileHandle handle, bool directory, string? volume, SecurityIdentifier? @operator)
    {
        _handle = handle;
        _directory = directory;
        _operator = @operator;
        _volume = volume ?? VolumeIdentity(handle);
        Identity = Inspect();
    }

    internal string Identity { get; }

    internal static EvidenceEntry OpenDrive(string drive)
    {
        if (GetDriveType(drive) != FixedDrive)
            throw new IOException("Evidence requires a fixed local drive.");
        var entry = Open(null, @"\??\" + drive, true, null, false, false);
        try
        {
            entry.VerifyFinalPath(drive);
            var fileSystem = new StringBuilder(16);
            if (!GetVolumeInformationByHandle(entry._handle, IntPtr.Zero, 0, out _, out _, out _, fileSystem, (uint)fileSystem.Capacity) ||
                !string.Equals(fileSystem.ToString(), "NTFS", StringComparison.Ordinal))
                throw new IOException("Evidence requires an NTFS volume.");
            return entry;
        }
        catch
        {
            entry.Dispose();
            throw;
        }
    }

    internal EvidenceEntry OpenDirectory(string name, SecurityIdentifier? @operator, bool create = false, bool delete = false, SafeFileHandle? recoveryHandle = null) =>
        Open(this, name, true, @operator, create, delete, recoveryHandle);

    internal EvidenceEntry OpenFile(string name, bool create = false, bool delete = false) =>
        Open(this, name, false, _operator, create, delete);

    private static EvidenceEntry Open(EvidenceEntry? parent, string name, bool directory, SecurityIdentifier? @operator,
        bool create, bool delete, SafeFileHandle? recoveryHandle = null)
    {
        if (parent != null && (name.Length == 0 || name == "." || name == ".." || name.IndexOfAny(new[] { '\\', '/', ':', '\0' }) >= 0))
            throw new IOException("Evidence entry is not a single path component.");

        var nameBuffer = IntPtr.Zero;
        var unicodeString = IntPtr.Zero;
        var securityDescriptor = IntPtr.Zero;
        try
        {
            nameBuffer = Marshal.StringToHGlobalUni(name);
            var byteLength = checked((ushort)(name.Length * 2));
            unicodeString = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
            Marshal.StructureToPtr(new UnicodeString
            {
                Length = byteLength,
                MaximumLength = checked((ushort)(byteLength + 2)),
                Buffer = nameBuffer
            }, unicodeString, false);

            if (create)
            {
                var descriptor = CreateAcl(@operator!);
                securityDescriptor = Marshal.AllocHGlobal(descriptor.Length);
                Marshal.Copy(descriptor, 0, securityDescriptor, descriptor.Length);
            }
            var attributes = new ObjectAttributes
            {
                Length = Marshal.SizeOf<ObjectAttributes>(),
                RootDirectory = parent?._handle.DangerousGetHandle() ?? IntPtr.Zero,
                ObjectName = unicodeString,
                Attributes = CaseInsensitive,
                SecurityDescriptor = securityDescriptor
            };
            var access = FileReadAttributes | Synchronize;
            if (directory) access |= FileTraverse;
            if (!directory || @operator != null) access |= FileReadData | ReadControl;
            if (create && !directory) access |= FileWriteData;
            if (delete) access |= DeleteAccess;
            var options = SynchronousIo | OpenReparsePoint | (directory ? DirectoryFile : NonDirectoryFile);
            // Ancestors and root leases deny rename/delete. Case/file delete handles
            // also exclude competing delete handles; files deny writers throughout.
            const uint shareDelete = 4;
            var sharing = directory && !delete ? ShareRead | (recoveryHandle != null ? shareDelete : 0u) : 0u;
            var status = NtCreateFile(out var handle, access, ref attributes, out _, IntPtr.Zero,
                (uint)FileAttributes.Normal, sharing, create ? CreateNew : OpenExisting, options, IntPtr.Zero, 0);
            GC.KeepAlive(parent);
            if (status < 0 || handle.IsInvalid)
            {
                handle.Dispose();
                if (!create && status == ObjectNameNotFound)
                    throw new EvidenceEntryMissingException();
                throw new IOException("Evidence entry could not be opened or created.");
            }
            try
            {
                if (recoveryHandle != null && (recoveryHandle.IsClosed || recoveryHandle.IsInvalid ||
                    !GetFileInformationByHandle(recoveryHandle, out var leased) ||
                    !GetFileInformationByHandle(handle, out var opened) ||
                    leased.VolumeSerialNumber != opened.VolumeSerialNumber || leased.FileIndexHigh != opened.FileIndexHigh ||
                    leased.FileIndexLow != opened.FileIndexLow || leased.CreationTimeHigh != opened.CreationTimeHigh ||
                    leased.CreationTimeLow != opened.CreationTimeLow))
                    throw new IOException("Recovery root native identity differs from its retained lease.");
                return new EvidenceEntry(handle, directory, parent?._volume, @operator);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(securityDescriptor);
            Marshal.FreeHGlobal(unicodeString);
            Marshal.FreeHGlobal(nameBuffer);
        }
    }

    internal void Verify()
    {
        if (Inspect() != Identity)
            throw new IOException("Evidence entry identity changed.");
    }

    private string Inspect()
    {
        if (!GetFileInformationByHandle(_handle, out var info) ||
            (info.Attributes & FileAttributes.ReparsePoint) != 0 ||
            ((info.Attributes & FileAttributes.Directory) != 0) != _directory ||
            (!_directory && info.NumberOfLinks != 1))
            throw new IOException("Evidence entry is a reparse point, an aliased file, or an unexpected entry type.");
        if (_operator != null)
            VerifyAcl();
        return FormattableString.Invariant($"{_volume}:{info.VolumeSerialNumber:x8}{info.FileIndexHigh:x8}{info.FileIndexLow:x8}{info.CreationTimeHigh:x8}{info.CreationTimeLow:x8}");
    }

    private void VerifyAcl()
    {
        const int seFileObject = 1;
        const int ownerAndDacl = 5;
        var result = GetSecurityInfo(_handle, seFileObject, ownerAndDacl,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out var descriptor);
        try
        {
            if (result != 0 || descriptor == IntPtr.Zero)
                throw new IOException("Evidence ACL is unavailable.");
            var length = GetSecurityDescriptorLength(descriptor);
            if (length <= 0 || length > ushort.MaxValue)
                throw new IOException("Evidence ACL is invalid.");
            var bytes = new byte[length];
            Marshal.Copy(descriptor, bytes, 0, bytes.Length);
            var acl = new RawSecurityDescriptor(bytes, 0);
            var rules = acl.DiscretionaryAcl;
            if (!_operator!.Equals(acl.Owner) || !acl.ControlFlags.HasFlag(ControlFlags.DiscretionaryAclProtected) ||
                rules == null || rules.Count != 2)
                throw new IOException("Evidence ACL is outside the private boundary.");

            var allowed = new HashSet<SecurityIdentifier> { _operator, Administrators };
            foreach (GenericAce rule in rules)
            {
                const AceFlags inheritanceFlags = AceFlags.ObjectInherit | AceFlags.ContainerInherit;
                if (rule is not CommonAce ace || ace.AceType != AceType.AccessAllowed ||
                    (ace.AceFlags & ~inheritanceFlags) != 0 ||
                    ace.AccessMask != (int)FileSystemRights.FullControl || !allowed.Remove(ace.SecurityIdentifier))
                    throw new IOException("Evidence ACL is outside the private boundary.");
            }
        }
        finally
        {
            if (descriptor != IntPtr.Zero)
                LocalFree(descriptor);
        }
    }

    private static byte[] CreateAcl(SecurityIdentifier @operator)
    {
        var acl = new RawAcl(GenericAcl.AclRevision, 2);
        acl.InsertAce(0, new CommonAce(AceFlags.None, AceQualifier.AccessAllowed, (int)FileSystemRights.FullControl,
            @operator, false, null));
        acl.InsertAce(1, new CommonAce(AceFlags.None, AceQualifier.AccessAllowed, (int)FileSystemRights.FullControl,
            Administrators, false, null));
        var descriptor = new RawSecurityDescriptor(ControlFlags.DiscretionaryAclPresent | ControlFlags.DiscretionaryAclProtected,
            @operator, null, null, acl);
        var bytes = new byte[descriptor.BinaryLength];
        descriptor.GetBinaryForm(bytes, 0);
        return bytes;
    }

    internal void VerifyFinalPath(string expected)
    {
        if (!string.Equals(FinalPath(_handle, 0), @"\\?\" + expected, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Evidence path uses an alias.");
    }

    private static string VolumeIdentity(SafeFileHandle handle)
    {
        const string prefix = @"\\?\Volume";
        var path = FinalPath(handle, VolumeNameGuid);
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !path.EndsWith("\\", StringComparison.Ordinal) ||
            !Guid.TryParseExact(path.Substring(prefix.Length, path.Length - prefix.Length - 1), "B", out var volume))
            throw new IOException("Evidence drive does not resolve to a volume root.");
        return volume.ToString("N");
    }

    private static string FinalPath(SafeFileHandle handle, uint flags)
    {
        var path = new StringBuilder(32768);
        var length = GetFinalPathNameByHandle(handle, path, (uint)path.Capacity, flags);
        if (length == 0 || length >= path.Capacity)
            throw new IOException("Evidence path identity is unavailable.");
        return path.ToString();
    }

    internal byte[] Read(int maximumBytes)
    {
        Verify();
        VerifyDefaultStreamOnly();
        using var stream = Stream(FileAccess.Read);
        var size = stream.Length;
        if (size <= 0 || size > maximumBytes)
            throw new IOException("Evidence file size is outside the supported boundary.");
        var bytes = new byte[(int)size];
        stream.Position = 0;
        for (var offset = 0; offset < bytes.Length;)
        {
            var read = stream.Read(bytes, offset, bytes.Length - offset);
            if (read == 0) throw new IOException("Evidence file is incomplete.");
            offset += read;
        }
        return bytes;
    }

    internal void Write(byte[] bytes)
    {
        Verify();
        VerifyDefaultStreamOnly();
        using var stream = Stream(FileAccess.Write);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(true);
    }

    private FileStream Stream(FileAccess access) => new FileStream(new SafeFileHandle(_handle.DangerousGetHandle(), false), access);

    internal void VerifyContents(IEnumerable<string> names)
    {
        Verify();
        VerifyDefaultStreamOnly();
        var expected = new HashSet<string>(names, StringComparer.Ordinal);
        const int capacity = 4096;
        const int nameLengthOffset = 60;
        const int nameOffset = 68;
        var buffer = Marshal.AllocHGlobal(capacity);
        try
        {
            var infoClass = FileFullDirectoryRestartInfo;
            while (GetFileInformationByHandleEx(_handle, infoClass, buffer, capacity))
            {
                infoClass = FileFullDirectoryInfo;
                var offset = 0;
                while (true)
                {
                    var next = Marshal.ReadInt32(buffer, offset);
                    var length = Marshal.ReadInt32(buffer, offset + nameLengthOffset);
                    if (length <= 0 || length % 2 != 0 || length > capacity - offset - nameOffset)
                        throw new IOException("Evidence directory enumeration is invalid.");
                    var name = Marshal.PtrToStringUni(IntPtr.Add(buffer, offset + nameOffset), length / 2);
                    if (name != "." && name != ".." && !expected.Remove(name!))
                        throw new IOException("Evidence directory contains an unexpected entry.");
                    if (next == 0) break;
                    if (next < nameOffset + length || next > capacity - offset - nameOffset)
                        throw new IOException("Evidence directory enumeration is invalid.");
                    offset += next;
                }
            }
            if (Marshal.GetLastWin32Error() != NoMoreFiles || expected.Count != 0)
                throw new IOException("Evidence directory contents could not be verified.");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal void Delete()
    {
        Verify();
        VerifyDefaultStreamOnly();
        byte delete = 1;
        if (!SetFileInformationByHandle(_handle, FileDispositionInfo, ref delete, 1))
            throw new IOException("Exact evidence entry could not be deleted.");
        _handle.Dispose();
    }

    private void VerifyDefaultStreamOnly()
    {
        const int capacity = 1024;
        const int nameOffset = 24;
        const string defaultStream = "::$DATA";
        var buffer = Marshal.AllocHGlobal(capacity);
        try
        {
            if (!GetFileInformationByHandleEx(_handle, FileStreamInfo, buffer, capacity))
            {
                if (_directory && Marshal.GetLastWin32Error() == HandleEof)
                    return;
                throw new IOException("Evidence streams could not be verified.");
            }
            if (Marshal.ReadInt32(buffer) != 0 || Marshal.ReadInt32(buffer, 4) != defaultStream.Length * 2 ||
                Marshal.PtrToStringUni(IntPtr.Add(buffer, nameOffset), defaultStream.Length) != defaultStream)
                throw new IOException("Evidence entry contains an unexpected data stream.");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose() => _handle.Dispose();

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        internal ushort Length;
        internal ushort MaximumLength;
        internal IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        internal int Length;
        internal IntPtr RootDirectory;
        internal IntPtr ObjectName;
        internal uint Attributes;
        internal IntPtr SecurityDescriptor;
        internal IntPtr SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        internal IntPtr Status;
        internal IntPtr Information;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        internal FileAttributes Attributes;
        internal uint CreationTimeLow;
        internal uint CreationTimeHigh;
        internal uint LastAccessTimeLow;
        internal uint LastAccessTimeHigh;
        internal uint LastWriteTimeLow;
        internal uint LastWriteTimeHigh;
        internal uint VolumeSerialNumber;
        internal uint FileSizeHigh;
        internal uint FileSizeLow;
        internal uint NumberOfLinks;
        internal uint FileIndexHigh;
        internal uint FileIndexLow;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtCreateFile(out SafeFileHandle handle, uint desiredAccess, ref ObjectAttributes objectAttributes,
        out IoStatusBlock ioStatusBlock, IntPtr allocationSize, uint fileAttributes, uint shareAccess, uint createDisposition,
        uint createOptions, IntPtr eaBuffer, uint eaLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int infoClass, IntPtr buffer, int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int infoClass, ref byte buffer, int size);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetDriveTypeW", ExactSpelling = true)]
    private static extern uint GetDriveType(string path);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetVolumeInformationByHandleW", ExactSpelling = true)]
    private static extern bool GetVolumeInformationByHandle(SafeFileHandle handle, IntPtr volumeName, uint volumeNameSize,
        out uint serialNumber, out uint maximumComponentLength, out uint fileSystemFlags, StringBuilder fileSystemName, uint fileSystemNameSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetFinalPathNameByHandleW", ExactSpelling = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint capacity, uint flags);

    [DllImport("advapi32.dll")]
    private static extern uint GetSecurityInfo(SafeFileHandle handle, int objectType, int securityInfo,
        IntPtr owner, IntPtr group, IntPtr dacl, IntPtr sacl, out IntPtr descriptor);

    [DllImport("advapi32.dll")]
    private static extern int GetSecurityDescriptorLength(IntPtr descriptor);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
