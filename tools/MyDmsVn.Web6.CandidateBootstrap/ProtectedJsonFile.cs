using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal sealed class ProtectedJsonFile : IDisposable
{
    private const int MaximumBytes = 16 * 1024;
    private readonly FileStream _stream;
    private readonly PathLease _parents;
    private readonly SecurityIdentifier _owner;
    private readonly (uint Volume, uint IndexHigh, uint IndexLow, long CreationTime) _identity;
    private readonly string _acl;

    private ProtectedJsonFile(string path, string? sha256, PathLease parents, FileStream stream)
    {
        Path = path;
        _parents = parents;
        _stream = stream;
        _owner = CurrentOwner();
        _identity = Inspect(stream.SafeFileHandle, path, directory: false);
        _acl = PrivateAcl(stream, _owner);
        Bytes = ReadBytes(stream);
        Sha256 = Digest(Bytes);
        if (sha256 != null && Sha256 != sha256)
            throw new IOException("Protected bootstrap file digest differs.");
        Verify();
    }

    internal string Path { get; }
    internal string Sha256 { get; }
    internal byte[] Bytes { get; }

    internal static ProtectedJsonFile Open(string path, string sha256)
    {
        if (!BootstrapJson.IsLowerHex(sha256, 64))
            throw new IOException("Protected bootstrap file digest is invalid.");
        return OpenCore(path, sha256);
    }

    internal static ProtectedJsonFile OpenSqlApproval(string path) => OpenCore(path, expectedSha256: null);

    private static ProtectedJsonFile OpenCore(string path, string? expectedSha256)
    {
        path = BootstrapJson.AbsolutePath(path);

        var parents = new PathLease(path);
        try
        {
            var stream = parents.OpenRead();
            try
            {
                return new ProtectedJsonFile(path, expectedSha256, parents, stream);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }
        catch
        {
            parents.Dispose();
            throw;
        }
    }

    internal static byte[] ReadInput(string path)
    {
        path = BootstrapJson.AbsolutePath(path);
        using var parents = new PathLease(path);
        using var stream = parents.OpenRead();
        var identity = Inspect(stream.SafeFileHandle, path, directory: false);
        var bytes = ReadBytes(stream);
        if (identity != Inspect(stream.SafeFileHandle, path, directory: false))
            throw new IOException("Bootstrap input file identity changed.");
        return bytes;
    }

    internal static string WriteNew(string path, byte[] bytes)
    {
        path = BootstrapJson.AbsolutePath(path);
        RequireSize(bytes.Length);
        using var parents = new PathLease(path);
        var owner = CurrentOwner();
        var acl = new FileSecurity();
        acl.SetOwner(owner);
        acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        acl.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl, AccessControlType.Allow));

        using var handle = parents.CreateNewFile(acl);
        using var stream = new FileStream(handle, FileAccess.ReadWrite, bufferSize: 4096);
        var identity = Inspect(stream.SafeFileHandle, path, directory: false);
        var initialAcl = PrivateAcl(stream, owner);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(flushToDisk: true);
        if (identity != Inspect(stream.SafeFileHandle, path, directory: false) ||
            PrivateAcl(stream, owner) != initialAcl || !ReadBytes(stream).SequenceEqual(bytes))
            throw new IOException("New protected bootstrap file could not be verified.");
        return Digest(bytes);
    }

    internal void Verify()
    {
        if (_identity != Inspect(_stream.SafeFileHandle, Path, directory: false) ||
            PrivateAcl(_stream, _owner) != _acl || !ReadBytes(_stream).SequenceEqual(Bytes) ||
            PrivateAcl(_stream, _owner) != _acl || _identity != Inspect(_stream.SafeFileHandle, Path, directory: false))
            throw new IOException("Protected bootstrap file changed.");
    }

    public void Dispose()
    {
        _stream.Dispose();
        _parents.Dispose();
    }

    internal static SecurityIdentifier CurrentOwner()
    {
        using var current = WindowsIdentity.GetCurrent();
        return current.User ?? throw new IOException("Bootstrap Owner identity is unavailable.");
    }

    private static string PrivateAcl(FileStream stream, SecurityIdentifier owner)
    {
        var acl = stream.GetAccessControl();
        var descriptor = new RawSecurityDescriptor(acl.GetSecurityDescriptorBinaryForm(), 0);
        var rules = descriptor.DiscretionaryAcl;
        if (!owner.Equals(descriptor.Owner) || !descriptor.ControlFlags.HasFlag(ControlFlags.DiscretionaryAclProtected) ||
            rules == null || rules.Count != 1 || rules[0] is not CommonAce ace || ace.AceType != AceType.AccessAllowed ||
            ace.AceFlags != AceFlags.None || ace.AccessMask != (int)FileSystemRights.FullControl || !owner.Equals(ace.SecurityIdentifier))
            throw new IOException("Bootstrap file is outside the protected Owner ACL.");
        return acl.GetSecurityDescriptorSddlForm(AccessControlSections.Owner | AccessControlSections.Access);
    }

    private static byte[] ReadBytes(FileStream stream)
    {
        var length = stream.Length;
        RequireSize(length);
        var bytes = new byte[(int)length];
        stream.Position = 0;
        for (var offset = 0; offset < bytes.Length;)
        {
            var count = stream.Read(bytes, offset, bytes.Length - offset);
            if (count == 0)
                throw new IOException("Bootstrap file ended while being read.");
            offset += count;
        }
        if (stream.ReadByte() != -1)
            throw new IOException("Bootstrap file grew while being read.");
        return bytes;
    }

    private static void RequireSize(long length)
    {
        if (length is < 1 or > MaximumBytes)
            throw new IOException("Bootstrap JSON file exceeds the accepted size boundary.");
    }

    internal static string Digest(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    private static SafeFileHandle OpenDrive(string path)
    {
        const uint fileTraverse = 0x20;
        const uint openReparsePoint = 0x00200000;
        const uint backupSemantics = 0x02000000;
        var handle = CreateFile(path, fileTraverse, FileShare.Read, IntPtr.Zero, FileMode.Open,
            openReparsePoint | backupSemantics, IntPtr.Zero);
        try
        {
            Inspect(handle, path, directory: true);
            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static SafeFileHandle OpenRelative(SafeFileHandle parent, string path, bool directory, byte[]? acl = null, bool installedTool = false)
    {
        const uint fileTraverse = 0x20;
        const uint readAttributes = 0x80;
        const uint synchronize = 0x00100000;
        const uint caseInsensitive = 0x40;
        const uint dontReparse = 0x1000;
        const uint fileOpen = 1;
        const uint fileCreate = 2;
        const uint fileDirectoryFile = 1;
        const uint writeThrough = 2;
        const uint synchronousIoNonAlert = 0x20;
        const uint fileNonDirectoryFile = 0x40;
        const uint openReparsePoint = 0x00200000;

        var component = System.IO.Path.GetFileName(path);
        var nameBuffer = IntPtr.Zero;
        var unicodeString = IntPtr.Zero;
        var securityDescriptor = IntPtr.Zero;
        var parentRef = false;
        try
        {
            parent.DangerousAddRef(ref parentRef);
            nameBuffer = Marshal.StringToHGlobalUni(component);
            var nameLength = checked((ushort)(component.Length * sizeof(char)));
            unicodeString = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
            Marshal.StructureToPtr(new UnicodeString
            {
                Length = nameLength,
                MaximumLength = checked((ushort)(nameLength + sizeof(char))),
                Buffer = nameBuffer,
            }, unicodeString, false);

            if (acl != null)
            {
                securityDescriptor = Marshal.AllocHGlobal(acl.Length);
                Marshal.Copy(acl, 0, securityDescriptor, acl.Length);
            }
            var attributes = new ObjectAttributes
            {
                Length = Marshal.SizeOf<ObjectAttributes>(),
                RootDirectory = parent.DangerousGetHandle(),
                ObjectName = unicodeString,
                Attributes = caseInsensitive | dontReparse,
                SecurityDescriptor = securityDescriptor,
            };
            var access = directory ? fileTraverse | readAttributes | synchronize
                : (uint)(acl == null ? FileSystemRights.Read | FileSystemRights.Synchronize : FileSystemRights.FullControl);
            var options = synchronousIoNonAlert | openReparsePoint | (directory ? fileDirectoryFile : fileNonDirectoryFile);
            if (acl != null)
                options |= writeThrough;

            // Resolve exactly one component beneath the admitted handle. Absolute
            // path resolution could follow a reparse mutation made via attributes-only access.
            var status = NtCreateFile(out var handle, access, ref attributes, out _, IntPtr.Zero,
                (uint)FileAttributes.Normal, (uint)(acl == null || directory ? FileShare.Read : FileShare.None),
                acl == null ? fileOpen : fileCreate, options, IntPtr.Zero, 0);
            try
            {
                if (status != 0 || handle.IsInvalid)
                    throw new IOException($"Bootstrap entry could not be opened or created beneath its pinned directory (NTSTATUS 0x{status:x8}).");
                Inspect(handle, path, directory, installedTool);
                return handle;
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }
        finally
        {
            if (parentRef)
                parent.DangerousRelease();
            Marshal.FreeHGlobal(securityDescriptor);
            Marshal.FreeHGlobal(unicodeString);
            Marshal.FreeHGlobal(nameBuffer);
        }
    }

    // Use the existing owner handle directly. A second open of a directory held
    // with DELETE access would need to share DELETE and is intentionally avoided.
    internal static SafeFileHandle CreateChildDirectory(SafeFileHandle parent, string path, DirectorySecurity acl)
    {
        path = BootstrapJson.AbsolutePath(path);
        Inspect(parent, System.IO.Path.GetDirectoryName(path)!, directory: true);
        return OpenRelative(parent, path, directory: true, acl.GetSecurityDescriptorBinaryForm());
    }

    internal static SafeFileHandle CreateChildFile(SafeFileHandle parent, string path, FileSecurity acl)
    {
        path = BootstrapJson.AbsolutePath(path);
        Inspect(parent, System.IO.Path.GetDirectoryName(path)!, directory: true);
        return OpenRelative(parent, path, directory: false, acl.GetSecurityDescriptorBinaryForm());
    }

    internal static (uint Volume, uint IndexHigh, uint IndexLow, long CreationTime) Inspect(SafeFileHandle handle,
        string expectedPath, bool directory, bool installedTool = false)
    {
        if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info) ||
            (info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0 ||
            ((info.Attributes & FileAttributes.Directory) != 0) != directory ||
            (!directory && !installedTool && info.NumberOfLinks != 1))
            throw new IOException("Bootstrap file or directory identity is unsafe.");

        var finalPath = new StringBuilder(512);
        var length = GetFinalPathNameByHandle(handle, finalPath, (uint)finalPath.Capacity, 0);
        if (length == 0 || length >= finalPath.Capacity ||
            !string.Equals(finalPath.ToString(), @"\\?\" + expectedPath, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Bootstrap file or directory uses an aliased path.");

        return (info.VolumeSerialNumber, info.FileIndexHigh, info.FileIndexLow, info.CreationTime);
    }

    internal sealed class PathLease : IDisposable
    {
        private readonly string _path;
        private readonly List<SafeFileHandle> _ancestors = new List<SafeFileHandle>();

        internal PathLease(string path)
        {
            _path = BootstrapJson.AbsolutePath(path);
            var drive = System.IO.Path.GetPathRoot(_path);
            var volume = new DriveInfo(drive);
            if (volume.DriveType != DriveType.Fixed || volume.DriveFormat != "NTFS")
                throw new IOException("Bootstrap files require a fixed local NTFS drive.");

            var ancestors = new Stack<string>();
            for (var parent = System.IO.Path.GetDirectoryName(_path); parent != null; parent = System.IO.Path.GetDirectoryName(parent))
                ancestors.Push(parent);

            try
            {
                // Hold every directory with read-only sharing through the child operation.
                _ancestors.Add(OpenDrive(ancestors.Pop()));
                foreach (var ancestor in ancestors)
                    _ancestors.Add(OpenRelative(Parent, ancestor, directory: true));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private SafeFileHandle Parent => _ancestors[_ancestors.Count - 1];

        internal FileStream OpenRead(bool installedTool = false)
        {
            var handle = OpenRelative(Parent, _path, directory: false, installedTool: installedTool);
            try
            {
                return new FileStream(handle, FileAccess.Read, bufferSize: 1);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        internal SafeFileHandle CreateNewFile(FileSecurity acl) =>
            OpenRelative(Parent, _path, directory: false, acl.GetSecurityDescriptorBinaryForm());

        internal SafeFileHandle OpenDirectory(DirectorySecurity? createAcl = null) =>
            OpenRelative(Parent, _path, directory: true, createAcl?.GetSecurityDescriptorBinaryForm());

        public void Dispose()
        {
            for (var index = _ancestors.Count - 1; index >= 0; index--)
                _ancestors[index].Dispose();
        }
    }

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

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct FileInformation
    {
        internal FileAttributes Attributes;
        internal long CreationTime;
        internal long LastAccessTime;
        internal long LastWriteTime;
        internal uint VolumeSerialNumber;
        internal uint FileSizeHigh;
        internal uint FileSizeLow;
        internal uint NumberOfLinks;
        internal uint FileIndexHigh;
        internal uint FileIndexLow;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, FileShare share, IntPtr security,
        FileMode mode, uint flags, IntPtr template);

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtCreateFile(out SafeFileHandle handle, uint access, ref ObjectAttributes attributes,
        out IoStatusBlock ioStatusBlock, IntPtr allocationSize, uint fileAttributes, uint shareAccess,
        uint disposition, uint options, IntPtr eaBuffer, uint eaLength);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint capacity, uint flags);
}

internal static class BootstrapJson
{
    private const string White = @"[ \t\r\n]*";
    private const string StringToken = """
        "(?:[^"\\\x00-\x1f]|\\(?:["\\/bfnrt]|u(?:[0-9a-ce-fA-CE-F][0-9a-fA-F]{3}|[dD][0-7][0-9a-fA-F]{2}|[dD][89abAB][0-9a-fA-F]{2}\\u[dD][c-fC-F][0-9a-fA-F]{2})))*"
        """;
    private const string StringArray = @"\[" + White + "(?:" + StringToken + "(?:" + White + "," + White + StringToken + ")*)?" + White + @"\]";
    private const string Value = "(?:" + StringToken + "|" + StringArray + "|-?(?:0|[1-9][0-9]*)|true|false)";
    private const string Property = StringToken + White + ":" + White + Value;
    // Json.NET also accepts syntax such as comments, trailing commas and single
    // quotes. Both v1 bootstrap schemas use only flat objects and string arrays.
    private static readonly Regex Grammar = new Regex(
        @"\A" + White + "{" + White + "(?:" + Property + "(?:" + White + "," + White + Property + ")*)?" + White + "}" + White + @"\z",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Regex Candidate = new Regex(@"\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant);
    private static readonly Regex ReservedPathName = new Regex(
        @"\A(?:CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|(?:COM|LPT)[1-9¹²³])(?:\.|\z)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly string[] QuarantineHandles =
    {
        "WEB5-HISTORICAL", "WEB6-B04-LIFECYCLE", "WEB6-B04-OVERLAP", "WEB6-B04-HANG-BOUND",
    };
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

    internal static JObject Parse(byte[] bytes, params string[] requiredKeys)
    {
        try
        {
            var text = Utf8.GetString(bytes);
            if (!Grammar.IsMatch(text))
                throw new IOException("Bootstrap JSON syntax is invalid.");
            using var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None, MaxDepth = 2 };
            var obj = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (obj.Count != requiredKeys.Length || requiredKeys.Any(key => obj.Property(key, StringComparison.Ordinal) == null))
                throw new IOException("Bootstrap JSON keys differ from the exact schema.");
            if (obj["version"] is not JValue { Type: JTokenType.Integer, Value: 1L })
                throw new IOException("Bootstrap JSON version is invalid.");
            return obj;
        }
        catch (Exception error) when (error is JsonException or DecoderFallbackException or RegexMatchTimeoutException)
        {
            throw new IOException("Bootstrap JSON syntax is invalid.");
        }
    }

    internal static byte[] Serialize(JObject obj) => Utf8.GetBytes(obj.ToString(Formatting.None));

    internal static string Text(JObject obj, string key) =>
        obj[key] is JValue { Type: JTokenType.String, Value: string text } ? text :
            throw new IOException("Bootstrap JSON field requires a string.");

    internal static string[] Strings(JObject obj, string key)
    {
        if (obj[key] is not JArray array || array.Any(value => value.Type != JTokenType.String))
            throw new IOException("Bootstrap JSON field requires an array of strings.");
        return array.Values<string>().Select(value => value!).ToArray();
    }

    internal static string CandidateId(JObject obj) => RequireCandidateId(Text(obj, "candidateId"));

    internal static string RequireCandidateId(string candidateId)
    {
        if (!Candidate.IsMatch(candidateId) || IsQuarantineHandle(candidateId))
            throw new IOException("Bootstrap candidate ID is invalid.");
        return candidateId;
    }

    internal static string Commit(JObject obj, string key)
    {
        var commit = Text(obj, key);
        if (!IsLowerHex(commit, 40) || commit.All(c => c == '0'))
            throw new IOException("Bootstrap requires an exact lowercase Git commit SHA.");
        return commit;
    }

    internal static bool IsLowerHex(string value, int length) =>
        value.Length == length && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static string AbsolutePath(string path)
    {
        if (path.Length <= 3 || path[0] is not (>= 'A' and <= 'Z' or >= 'a' and <= 'z') ||
            path[1] != ':' || path[2] != '\\')
            throw new IOException("Bootstrap requires an absolute local Windows path.");

        var invalid = System.IO.Path.GetInvalidFileNameChars();
        foreach (var part in path.Substring(3).Split('\\'))
            if (part.Length == 0 || part.EndsWith(".", StringComparison.Ordinal) || part.EndsWith(" ", StringComparison.Ordinal) ||
                part.IndexOfAny(invalid) >= 0 || ReservedPathName.IsMatch(part) || IsQuarantineHandle(part))
                throw new IOException("Bootstrap path contains an unsafe component.");

        var fullPath = System.IO.Path.GetFullPath(path);
        if (!SamePath(path, fullPath))
            throw new IOException("Bootstrap path is not canonical.");
        return fullPath;
    }

    internal static bool SamePath(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    internal static bool IsChild(string path, string parent) => path.StartsWith(parent + "\\", StringComparison.OrdinalIgnoreCase);

    internal static void RequireSeparateRoots(params string[] paths)
    {
        for (var index = 0; index < paths.Length; index++)
        {
            for (var other = index + 1; other < paths.Length; other++)
            {
                var left = paths[index];
                var right = paths[other];
                if (SamePath(left, right) || IsChild(left, right) || IsChild(right, left))
                    throw new IOException("Bootstrap roots must be distinct and nonoverlapping.");
            }
        }
    }

    private static bool IsQuarantineHandle(string value) => QuarantineHandles.Contains(value, StringComparer.OrdinalIgnoreCase);
}
