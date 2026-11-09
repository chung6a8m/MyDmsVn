$ErrorActionPreference='Stop'
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Web6B03EvidenceSandbox.ps1'))
if($source -match '\[IO\.(File|Directory)\]::Delete\('){throw 'Sandbox cleanup still deletes by path after releasing its snapshot.'}
. "$PSScriptRoot/Web6B03SandboxCleanup.ps1"
$script:checks=0
function Check([bool]$value){if(-not $value){throw 'Sandbox memory contract failed.'};$script:checks++}
# Replace only IO boundaries. No native call, real path access, or filesystem mutation.
function RunCase([string]$Fault='') {
    $script:opened=[Collections.Generic.List[object]]::new();$script:deleted=[Collections.Generic.List[string]]::new()
    $script:expected=@{}
    $script:fault=$Fault;$script:drainCalls=0
    function Record([string]$relative,[bool]$directory){[pscustomobject]@{relative=$relative;nativeIdentity=$relative+'-id';directory=$directory;createdTicks='123';acl='acl';length=0;hash=if($directory){''}else{'hash'}}}
    $s=[pscustomobject]@{schemaVersion=3;root='C:\w6b-0123456789abcdef0123456789abcdef';quarantine=$false;phase='Ready';toolsSealed=$true
        rootIdentity=(Record '' $true);toolIdentity=(Record 'tool-scratch' $true);evidenceIdentity=(Record 'evidence' $true)
        controlIdentity=(Record 'control' $true);controlStamp=(Record 'control\owner.json' $false)
        toolInventory=@((Record 'nested' $true),(Record 'nested\one.bin' $false))}
    foreach($r in @($s.rootIdentity,$s.toolIdentity,$s.evidenceIdentity,$s.controlIdentity,$s.controlStamp)){$script:expected[$r.relative]=$r}
    foreach($r in $s.toolInventory){$script:expected['tool-scratch\'+$r.relative]=$r}
    function Open-Web6SandboxLease([string]$Path,[bool]$Directory,[bool]$Delete){
        $relative=if($Path -ceq 'C:\'){'DRIVE'}elseif($Path -ceq $s.root){''}else{$Path.Substring($s.root.Length+1)}
        if($relative -ne 'DRIVE' -and $relative -ne ''){
            $parent=if($relative.Contains('\')){$relative.Substring(0,$relative.LastIndexOf('\'))}else{''}
            if(@($script:opened|Where-Object {$_.relative -ceq $parent -and -not $_.disposed}).Count -ne 1){throw 'Ancestor not pinned before child open.'}
        }
        if($script:fault -ceq 'open-failure' -and $relative -ceq 'tool-scratch\nested\one.bin'){throw 'Injected open refusal.'}
        $record=if($relative -ceq 'DRIVE'){Record 'DRIVE' $true}else{$script:expected[$relative]}
        $lease=[pscustomobject]@{relative=$relative;Directory=$Directory;IdentityHash=$record.nativeIdentity;AclHash=$record.acl;CreationTimeTicks=$record.createdTicks;Length=$record.length;ContentHash=$record.hash;ProtectedAcl=$true;disposed=$false;canDelete=$Delete}
        if($script:fault -ceq 'replacement' -and $relative -ceq 'tool-scratch\nested\one.bin'){$lease.IdentityHash='replacement-id'}
        if($script:fault -ceq 'ancestor-replacement' -and $relative -ceq 'tool-scratch\nested'){$lease.IdentityHash='replacement-id'}
        if($script:fault -ceq 'acl-drift' -and $relative -ceq 'control'){$lease.AclHash='changed'}
        if($script:fault -ceq 'content-drift' -and $relative -ceq 'control\owner.json'){$lease.ContentHash='changed'}
        $lease|Add-Member ScriptMethod DeleteExact {
            if($this.disposed -or -not $this.canDelete){throw 'Unpinned deletion.'}
            if(@($script:opened|Where-Object {($_.relative -ceq 'DRIVE' -or $_.relative -ceq '' -or $this.relative.StartsWith($_.relative+'\')) -and $_.disposed}).Count){throw 'Ancestor released before exact deletion.'}
            # The entire set must have been admitted before the first deletion.
            if($script:opened.Count -ne 8){throw 'Partial admission permitted deletion.'}
            if($this.Directory -and @($script:opened|Where-Object {$_.relative.StartsWith($this.relative+'\') -and -not $_.disposed}).Count){throw 'Child still pinned during parent deletion.'}
            if($script:fault -ceq 'delete-failure'){throw 'Injected exact delete refusal.'}
            $script:deleted.Add($this.relative)
        }
        $lease|Add-Member ScriptMethod Dispose {$this.disposed=$true}
        $script:opened.Add($lease);return $lease
    }
    function Get-Web6SandboxChildren([string]$Path){
        $parent=if($Path -ceq $s.root){''}else{$Path.Substring($s.root.Length+1)}
        if($script:fault -ceq 'foreign-child' -and $parent -ceq 'tool-scratch\nested'){return @('foreign.bin','one.bin')}
        @($script:expected.Keys|Where-Object {$_ -ne '' -and -not $script:deleted.Contains($_)}|ForEach-Object {
            $p=if($_.Contains('\')){$_.Substring(0,$_.LastIndexOf('\'))}else{''}
            if($p -ceq $parent){($_ -split '\\')[-1]}
        })
    }
    function Assert-Web6SandboxDrain {$script:drainCalls++}
    if($Fault -ceq 'old-schema'){$s.schemaVersion=2}
    if($Fault -ceq 'quarantine'){$s.quarantine=$true}
    if($Fault -ceq 'traversal'){$s.toolInventory[1].relative='..\foreign.bin'}
    if($Fault -ceq 'duplicate'){$s.toolInventory+=,$s.toolInventory[0]}
    $accepted=$true;try{Remove-Web6SandboxExact $s}catch{$accepted=$false}
    Check ($accepted -eq ($Fault -ceq ''))
    Check (@($script:opened|Where-Object {-not $_.disposed}).Count -eq 0)
    if($Fault -ceq ''){Check ($script:deleted.Count -eq 7);Check ($script:deleted[6] -ceq '');Check ($script:drainCalls -gt 0)}
    else{Check ($script:deleted.Count -eq 0)}
}
RunCase
foreach($fault in @('old-schema','replacement','ancestor-replacement','acl-drift','content-drift','foreign-child','open-failure','quarantine','traversal','duplicate','delete-failure')){RunCase $fault}
if(-not $source.Contains('Remove-Web6SandboxExact $state')){throw 'Sandbox lifecycle does not use exact cleanup.'}
Check $true
"WEB6_SANDBOX_CLEANUP_MEMORY checks=$script:checks passed=$script:checks native=0 sql=0 filesystemMutation=0"
