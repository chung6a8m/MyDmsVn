using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

// Runtime-only native IO. Compilation does not call these APIs. O/G/A only, never SACL.
public static class Web6CheckpointNative
{
    private const AccessControlSections Sections = AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access;
    [StructLayout(LayoutKind.Sequential)] private struct SA { public int Length; public IntPtr Descriptor; public int Inherit; }
    [StructLayout(LayoutKind.Sequential)] private struct Info { public uint Attributes; public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written; public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] private static extern bool CreateDirectoryW(string path, ref SA attributes);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr sa, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError=true)] private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out Info info);
    [DllImport("advapi32.dll", SetLastError=true)] private static extern bool GetKernelObjectSecurity(SafeFileHandle handle, uint sections, byte[] descriptor, uint length, out uint needed);
    [DllImport("kernel32.dll", SetLastError=true)] private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int kind, ref byte info, uint length);
    private static Exception Denied() { return new IOException("Checkpoint native operation denied.", new Win32Exception(Marshal.GetLastWin32Error())); }
    public static string Hash(byte[] bytes) { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    public static void CreateRoot(string path)
    {
        using(var user=WindowsIdentity.GetCurrent()) {
            var acl=new DirectorySecurity(); acl.SetOwner(user.User); acl.SetAccessRuleProtection(true,false);
            acl.AddAccessRule(new FileSystemAccessRule(user.User,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
            byte[] bytes=acl.GetSecurityDescriptorBinaryForm(); IntPtr ptr=Marshal.AllocHGlobal(bytes.Length);
            try { Marshal.Copy(bytes,0,ptr,bytes.Length); var sa=new SA{Length=Marshal.SizeOf(typeof(SA)),Descriptor=ptr}; if(!CreateDirectoryW(path,ref sa)) throw Denied(); }
            finally { Marshal.FreeHGlobal(ptr); }
        }
    }
    public sealed class Lease : IDisposable
    {
        private SafeFileHandle handle;
        public string IdentityHash { get; private set; }
        public string AclHash { get; private set; }
        public string ContentHash { get; private set; }
        public string Text { get; private set; }
        public bool Directory { get; private set; }
        public long Length { get; private set; }
        public long CreationTimeTicks { get; private set; }
        public bool ProtectedAcl { get; private set; }
        private bool canDelete;
        public Lease(string path, bool directory, bool requireOwner, bool delete)
            : this(path,directory,requireOwner,delete,true) { }
        public Lease(string path, bool directory, bool requireOwner, bool delete, bool readText)
            : this(path,directory,requireOwner,delete,readText,false) { }
        public Lease(string path, bool directory, bool requireOwner, bool delete, bool readText, bool hashContent)
        {
            Directory=directory; canDelete=delete;
            // No write/delete sharing for records; directories remain enumerable/usable,
            // but cannot be renamed/replaced while their creation identity is pinned.
            handle=CreateFileW(path,0x20000u|0x80u|((directory||(!readText&&!hashContent))?0u:0x80000000u)|(delete?0x10000u:0u),directory?3u:1u,IntPtr.Zero,3,0x00200000u|(directory?0x02000000u:0u),IntPtr.Zero);
            try {
                if(handle.IsInvalid) throw Denied();
                Info i; if(!GetFileInformationByHandle(handle,out i)) throw Denied();
                if((i.Attributes&0x400u)!=0 || ((i.Attributes&0x10u)!=0)!=directory || (!directory && i.Links!=1)) throw new IOException("Checkpoint type/reparse/link denied.");
                IdentityHash=Hash(Encoding.UTF8.GetBytes(i.Volume+":"+i.IndexHigh+":"+i.IndexLow+":"+i.Created.dwHighDateTime+":"+i.Created.dwLowDateTime));
                uint needed; GetKernelObjectSecurity(handle,7,null,0,out needed);
                byte[] descriptor=new byte[needed]; if(!GetKernelObjectSecurity(handle,7,descriptor,needed,out needed)) throw Denied();
                FileSystemSecurity acl=directory ? (FileSystemSecurity)new DirectorySecurity() : new FileSecurity(); acl.SetSecurityDescriptorBinaryForm(descriptor,Sections);
                AclHash=Hash(acl.GetSecurityDescriptorBinaryForm());
                ProtectedAcl=acl.AreAccessRulesProtected;
                CreationTimeTicks=DateTime.FromFileTimeUtc(((long)(uint)i.Created.dwHighDateTime<<32)|(uint)i.Created.dwLowDateTime).Ticks;
                Length=directory ? 0 : ((long)i.SizeHigh<<32)|i.SizeLow;
                if(requireOwner) using(var user=WindowsIdentity.GetCurrent()) {
                    if(!acl.AreAccessRulesProtected || !acl.GetOwner(typeof(SecurityIdentifier)).Equals(user.User)) throw new IOException("Checkpoint ACL denied.");
                    var rules=acl.GetAccessRules(true,true,typeof(SecurityIdentifier));
                    if(rules.Count!=1) throw new IOException("Checkpoint ACL membership denied.");
                    var rule=(FileSystemAccessRule)rules[0];
                    if(rule.IsInherited || !rule.IdentityReference.Equals(user.User) || rule.AccessControlType!=AccessControlType.Allow || rule.FileSystemRights!=FileSystemRights.FullControl) throw new IOException("Checkpoint ACL grant denied.");
                }
                if(!directory && hashContent && !readText) {
                    using(var stream=new FileStream(new SafeFileHandle(handle.DangerousGetHandle(),false),FileAccess.Read))
                    using(var sha=SHA256.Create()) ContentHash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                }
                if(!directory && readText) {
                    // Keep the original handle pinned; duplicate handle wrapper does not own it.
                    using(var stream=new FileStream(new SafeFileHandle(handle.DangerousGetHandle(),false),FileAccess.Read)) {
                        if(stream.Length>4*1024*1024) throw new IOException("Checkpoint record size denied.");
                        byte[] bytes=new byte[(int)stream.Length]; int offset=0,read;
                        while(offset<bytes.Length && (read=stream.Read(bytes,offset,bytes.Length-offset))>0) offset+=read;
                        if(offset!=bytes.Length) throw new IOException("Checkpoint short read denied.");
                        Text=new UTF8Encoding(false,true).GetString(bytes); ContentHash=Hash(bytes);
                    }
                }
            } catch { Dispose(); throw; }
        }
        public void DeleteExact() { if(!canDelete) throw new IOException("Checkpoint delete authority absent."); byte value=1; if(!SetFileInformationByHandle(handle,4,ref value,1)) throw Denied(); }
        public void Dispose() { if(handle!=null) { handle.Dispose(); handle=null; } }
    }
    public sealed class Delivery
    {
        public string IdentityHash { get; internal set; }
        public string AclHash { get; internal set; }
        public string ContentHash { get; internal set; }
    }
    public static Delivery WriteNew(string path,string text)
    {
        byte[] bytes=new UTF8Encoding(false,true).GetBytes(text);
        using(var user=WindowsIdentity.GetCurrent()) {
            var acl=new FileSecurity(); acl.SetOwner(user.User); acl.SetAccessRuleProtection(true,false);
            acl.AddAccessRule(new FileSystemAccessRule(user.User,FileSystemRights.FullControl,AccessControlType.Allow));
            using(var stream=new FileStream(path,FileMode.CreateNew,FileSystemRights.ReadData|FileSystemRights.WriteData|FileSystemRights.ReadPermissions,FileShare.None,4096,FileOptions.WriteThrough,acl)) {
                stream.Write(bytes,0,bytes.Length); stream.Flush(true); stream.Position=0;
                byte[] read=new byte[bytes.Length]; int offset=0,n;
                while(offset<read.Length && (n=stream.Read(read,offset,read.Length-offset))>0) offset+=n;
                if(offset!=bytes.Length || Hash(read)!=Hash(bytes) || stream.Length!=bytes.Length) throw new IOException("Checkpoint delivery readback denied.");
                Info i; if(!GetFileInformationByHandle(stream.SafeFileHandle,out i)) throw Denied();
                uint needed; GetKernelObjectSecurity(stream.SafeFileHandle,7,null,0,out needed);
                byte[] descriptor=new byte[needed]; if(!GetKernelObjectSecurity(stream.SafeFileHandle,7,descriptor,needed,out needed)) throw Denied();
                var actual=new FileSecurity(); actual.SetSecurityDescriptorBinaryForm(descriptor,Sections);
                return new Delivery {
                    IdentityHash=Hash(Encoding.UTF8.GetBytes(i.Volume+":"+i.IndexHigh+":"+i.IndexLow+":"+i.Created.dwHighDateTime+":"+i.Created.dwLowDateTime)),
                    AclHash=Hash(actual.GetSecurityDescriptorBinaryForm()), ContentHash=Hash(read)
                };
            }
        }
    }
}
