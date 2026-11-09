# Windows PowerShell 5.1 pure test of the actual tracked checkpoint adapter descriptor codec.
# Extract only decoding statements; never compile/invoke native IO or the controller.
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if($PSVersionTable.PSEdition -cne 'Desktop' -or $PSVersionTable.PSVersion.Major -ne 5){throw 'PS5.1 required.'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source=[IO.File]::ReadAllText((Join-Path $root '.github/scripts/Web6B03CheckpointNative.cs'))
$sections=[regex]::Match($source,'private const AccessControlSections Sections = ([^;]+);')
if(-not $sections.Success -or $sections.Groups[1].Value -cne 'AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access'){throw 'Exact O/G/A sections changed.'}
$codec=[regex]::Matches($source,'(?m)^\s*(?<body>(?:var|FileSystemSecurity) acl=[^\r\n]+SetSecurityDescriptorBinaryForm\(descriptor,Sections\);)\r?\n\s*AclHash=Hash\(acl.GetSecurityDescriptorBinaryForm\(\)\);')
if($codec.Count -ne 1){throw 'Exact Lease decoder absent.'}
$fragment=$codec[0].Groups['body'].Value
if($fragment -notmatch '^(var|FileSystemSecurity) acl=' -or $fragment -match 'DllImport|FileStream|GetAccessControl|SetAccessControl|CreateFile|GetKernelObjectSecurity'){throw 'Pure codec extraction refused.'}
$fileCodec=[regex]::Matches($source,'var actual=new FileSecurity\(\); actual.SetSecurityDescriptorBinaryForm\(descriptor,Sections\);')
if($fileCodec.Count -ne 1){throw 'Exact WriteNew file decoder absent.'}
$pure=@"
using System.Security.AccessControl;
public static class CheckpointDescriptorProbe {
    private const AccessControlSections Sections = $($sections.Groups[1].Value);
    public static FileSystemSecurity Decode(byte[] descriptor, bool directory) {
        $fragment
        return acl;
    }
    public static FileSystemSecurity DecodeDelivery(byte[] descriptor) {
        $($fileCodec[0].Value)
        return actual;
    }
}
"@
Add-Type -TypeDefinition $pure
function Hash([byte[]]$Bytes){$sha=[Security.Cryptography.SHA256]::Create();try{[BitConverter]::ToString($sha.ComputeHash($Bytes)).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}}
function EqualBytes([byte[]]$Left,[byte[]]$Right){[Convert]::ToBase64String($Left) -ceq [Convert]::ToBase64String($Right)}
$sid=[Security.Principal.SecurityIdentifier]::new('S-1-5-21-1-2-3-1001')
$group=[Security.Principal.SecurityIdentifier]::new('S-1-5-21-1-2-3-513')
$directory=[Security.AccessControl.DirectorySecurity]::new()
$directory.SetOwner($sid);$directory.SetGroup($group);$directory.SetAccessRuleProtection($true,$false)
$directory.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,[Security.AccessControl.FileSystemRights]::FullControl,[Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit',[Security.AccessControl.PropagationFlags]::None,[Security.AccessControl.AccessControlType]::Allow))
$file=[Security.AccessControl.FileSecurity]::new()
$file.SetOwner($sid);$file.SetGroup($group);$file.SetAccessRuleProtection($true,$false)
$file.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,[Security.AccessControl.FileSystemRights]::FullControl,[Security.AccessControl.AccessControlType]::Allow))
$dirBytes=$directory.GetSecurityDescriptorBinaryForm();$fileBytes=$file.GetSecurityDescriptorBinaryForm()
$decodedDirectory=[CheckpointDescriptorProbe]::Decode($dirBytes,$true)
$decodedFile=[CheckpointDescriptorProbe]::Decode($fileBytes,$false)
$delivery=[CheckpointDescriptorProbe]::DecodeDelivery($fileBytes)
# Same O/G/A representation used by the seam; no live Directory.GetAccessControl.
$seam=[Security.AccessControl.DirectorySecurity]::new()
$seam.SetSecurityDescriptorBinaryForm($dirBytes,[Security.AccessControl.AccessControlSections]'Owner,Group,Access')
function Contract($Actual,[bool]$IsDirectory,[byte[]]$Expected){
    (($IsDirectory -and $Actual -is [Security.AccessControl.DirectorySecurity]) -or
        (-not $IsDirectory -and $Actual -is [Security.AccessControl.FileSecurity])) -and
        (EqualBytes $Actual.GetSecurityDescriptorBinaryForm() $Expected)
}
$cases=[ordered]@{
    'directory-kind'={ $decodedDirectory -is [Security.AccessControl.DirectorySecurity] }
    'directory-roundtrip'={ EqualBytes $dirBytes $decodedDirectory.GetSecurityDescriptorBinaryForm() }
    'directory-seam-hash'={ (Hash $decodedDirectory.GetSecurityDescriptorBinaryForm()) -ceq (Hash $seam.GetSecurityDescriptorBinaryForm()) }
    'directory-protected-inheritance'={ $rules=$decodedDirectory.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier]);$decodedDirectory.AreAccessRulesProtected -and $rules.Count -eq 1 -and [int]$rules[0].InheritanceFlags -eq 3 -and [int]$rules[0].PropagationFlags -eq 0 -and -not $rules[0].IsInherited -and $rules[0].FileSystemRights -eq [Security.AccessControl.FileSystemRights]::FullControl }
    'directory-owner-group'={ $decodedDirectory.GetOwner([Security.Principal.SecurityIdentifier]).Equals($sid) -and $decodedDirectory.GetGroup([Security.Principal.SecurityIdentifier]).Equals($group) }
    'file-kind-roundtrip'={ Contract $decodedFile $false $fileBytes }
    'file-protection-owner-group'={ $decodedFile.AreAccessRulesProtected -and $decodedFile.GetOwner([Security.Principal.SecurityIdentifier]).Equals($sid) -and $decodedFile.GetGroup([Security.Principal.SecurityIdentifier]).Equals($group) }
    'file-delivery-hash'={ (Contract $delivery $false $fileBytes) -and (Hash $delivery.GetSecurityDescriptorBinaryForm()) -ceq (Hash $decodedFile.GetSecurityDescriptorBinaryForm()) }
    'reject-file-as-directory'={ -not (Contract ([CheckpointDescriptorProbe]::Decode($dirBytes,$false)) $true $dirBytes) }
    'reject-directory-as-file'={ -not (Contract ([CheckpointDescriptorProbe]::Decode($fileBytes,$true)) $false $fileBytes) }
}
$passed=0;$failed=0
foreach($case in $cases.GetEnumerator()){
    try{if(-not (& $case.Value)){throw 'Descriptor contract mismatch.'};$passed++;'DESCRIPTOR '+$case.Key+' PASS'}catch{$failed++;'DESCRIPTOR '+$case.Key+' FAIL'}
}
"WEB6_CHECKPOINT_DESCRIPTOR_MEMORY passed=$passed failed=$failed native=0 sql=0 filesystemMutation=0 controllerInvoked=0"
if($failed){throw 'Checkpoint descriptor regression failed.'}
