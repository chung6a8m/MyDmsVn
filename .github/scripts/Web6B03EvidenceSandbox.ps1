param(
    [Parameter(Mandatory=$true)][ValidateSet('Initialize','CheckEvidence','SealTools','Check','Complete')][string]$Action,
    [Parameter(Mandatory=$true)][string]$StatePath,
    [Parameter(Mandatory=$true)][string]$SummaryPath,
    [Parameter(Mandatory=$true)][ValidateSet('NativeSafety','B03Candidate')][string]$CheckpointProfile,
    [Parameter(Mandatory=$true)][string]$CheckpointRoot,
    [Parameter(Mandatory=$true)][string]$CheckpointPhase,
    [string]$RunToken=$env:WEB6_RUN_TOKEN
)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Web6B03RunPath.ps1"
. "$PSScriptRoot/Web6B03CheckpointStore.ps1"
. "$PSScriptRoot/Web6B03SandboxCleanup.ps1"
# Containment owns the outer/subroot identities; fixtures own every evidence case.
# Only a previously sealed exact tool inventory may be deleted, never an unknown child.
$identity=[Security.Principal.WindowsIdentity]::GetCurrent(); $sid=$identity.User.Value; $state=$null
function Save-State { [IO.File]::WriteAllText($StatePath,($script:state|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false)) }
function Summary {
    $safe=[ordered]@{ phase=$script:state.phase; quarantine=$script:state.quarantine; sourceSha=$script:state.sourceSha;
        ownershipVerified=$script:state.verified; runTokenSha256=$script:state.runTokenSha256; baselineChildren=0; unexpectedChildren=$script:state.children;
        primaryBackupPairsOwnedByFixtures=$true; activeProcesses=$script:state.processes;
        toolInventorySealed=$script:state.toolsSealed; sealedToolEntries=@($script:state.toolInventory).Count }
    [IO.File]::WriteAllText($SummaryPath,($safe|ConvertTo-Json -Compress)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
}
function Processes {
    @(Get-Process | Where-Object { $_.ProcessName -match '^(dotnet|MSBuild|VBCSCompiler|testhost(\.net48|\.x86)?|MyDmsVn\.WebOperation\.(Operator|CrashHost)|MyDmsVn\.Server\.DbMigrator)$' } | ForEach-Object { $_.Id.ToString()+':'+$_.StartTime.ToUniversalTime().Ticks.ToString() })
}
function Require-Drain {
    $clock=[Diagnostics.Stopwatch]::StartNew()
    do {
        $script:state.processes=@(Processes | Where-Object { $_ -notin @($script:state.baselineProcesses) }).Count
        if ($script:state.processes -eq 0) { return }
        Start-Sleep -Milliseconds 200
    } while ($clock.Elapsed.TotalSeconds -lt 20)
    throw 'Sandbox tooling/test owners did not drain.'
}
function Snapshot([string]$path,[string]$relative) {
    $item=Get-Item -LiteralPath $path -Force
    if ($path.Length -gt 240 -or ($item.PSIsContainer -and $path.Length -gt 220)) { throw 'Owned path budget exceeded.' }
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Reparse entry denied.' }
    $isDirectory=$item.PSIsContainer
    $native=Open-Web6SandboxLease $path $isDirectory $false
    try { Get-Web6SandboxStamp $native $relative } finally { $native.Dispose() }
}
function Same($one,$two) {
    $one.nativeIdentity -ceq $two.nativeIdentity -and $one.relative -ceq $two.relative -and $one.directory -eq $two.directory -and $one.createdTicks -ceq $two.createdTicks -and
        $one.acl -ceq $two.acl -and $one.length -eq $two.length -and $one.hash -ceq $two.hash
}
function Inventory([string]$root) {
    $queue=New-Object 'System.Collections.Generic.Queue[string]'; $queue.Enqueue($root)
    while ($queue.Count -gt 0) {
        $parent=$queue.Dequeue()
        foreach ($item in @(Get-ChildItem -LiteralPath $parent -Force | Sort-Object Name)) {
            $path=[IO.Path]::GetFullPath($item.FullName)
            if (-not $path.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Inventory escaped owned root.' }
            $record=Snapshot $path $path.Substring($root.Length+1)
            $record
            if ($record.directory) { $queue.Enqueue($path) }
        }
    }
}
function Verify-Root {
    if ($script:state.schemaVersion -ne 3) { throw 'Unsupported sandbox ownership version.' }
    Assert-Web6B03RunRoot $script:state.root $env:SystemDrive $RunToken
    $root=$script:state.root
    $drive=[IO.DriveInfo]::new($env:SystemDrive+'\')
    if ($drive.DriveType -ne [IO.DriveType]::Fixed -or $drive.DriveFormat -ne 'NTFS') { throw 'Fixed NTFS required.' }
    if ($script:state.sid -ne $sid -or $script:state.sourceSha -ne $env:GITHUB_SHA -or
        $script:state.runId -ne $env:GITHUB_RUN_ID -or $script:state.attempt -ne $env:GITHUB_RUN_ATTEMPT -or
        $script:state.token -cne $RunToken -or $script:state.runTokenSha256 -cne (Get-Web6B03RunTokenHash $RunToken)) { throw 'Sandbox context mismatch.' }
    foreach ($record in @($script:state.rootIdentity,$script:state.toolIdentity,$script:state.evidenceIdentity,$script:state.controlIdentity)) {
        $path=if($record.relative -eq ''){$root}else{Join-Path $root $record.relative}
        if (-not (Same $record (Snapshot $path $record.relative)) -or -not (Get-Acl -LiteralPath $path).AreAccessRulesProtected) { throw 'Sandbox creation identity/ACL changed.' }
    }
    $children=@(Get-ChildItem -LiteralPath $root -Force | ForEach-Object Name | Sort-Object)
    if ($children.Count -ne 3 -or @(Compare-Object @('control','evidence','tool-scratch') $children).Count -ne 0) { throw 'Unexpected outer sandbox child.' }
    $control=Join-Path $root 'control'; $stamp=Join-Path $control 'owner.json'
    if (-not (Same $script:state.controlStamp (Snapshot $stamp 'control\owner.json'))) { throw 'Run ownership stamp changed.' }
    $controlChildren=@(Get-ChildItem -LiteralPath $control -Force | ForEach-Object Name)
    if ($controlChildren.Count -ne 1 -or $controlChildren[0] -cne 'owner.json') { throw 'Unexpected control child.' }
    $script:state.verified=$true
}
function Verify-SealedTools {
    if (-not $script:state.toolsSealed) { throw 'Tool inventory not sealed.' }
    $now=@(Inventory (Join-Path $script:state.root 'tool-scratch'))
    if ($now.Count -ne @($script:state.toolInventory).Count) { throw 'Tool inventory changed after seal.' }
    foreach ($record in @($script:state.toolInventory)) {
        $matches=@($now | Where-Object { $_.relative -ceq $record.relative })
        if ($matches.Count -ne 1 -or -not (Same $record $matches[0])) { throw 'Tool entry identity/hash changed after seal.' }
    }
}
function Set-Temp([string]$path) {
    foreach ($name in @('TEMP','TMP')) { [IO.File]::AppendAllText($env:GITHUB_ENV,$name+'='+$path+[Environment]::NewLine,[Text.UTF8Encoding]::new($false)) }
}
try {
    if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or $env:GITHUB_REPOSITORY_ID -ne '1409766698') { throw 'Hosted candidate only.' }
    $prior=@()
    if ($Action -eq 'Initialize') {
        if ($CheckpointPhase -cne 'Initialize') { throw 'Checkpoint initial phase denied.' }
        if (Test-Path -LiteralPath $StatePath) { throw 'Cannot adopt sandbox state.' }
        # Token is generated once by the controller/workflow, never by this helper.
        $root=Get-Web6B03RunRoot $env:SystemDrive $RunToken
        Assert-Web6B03RunRoot $root $env:SystemDrive $RunToken
        $drive=[IO.DriveInfo]::new($env:SystemDrive+'\')
        if ($drive.DriveType -ne [IO.DriveType]::Fixed -or $drive.DriveFormat -ne 'NTFS') { throw 'Fixed NTFS required.' }
        if ($root.Length -ne 39 -or (Join-Path $root 'evidence').Length -ne 48) { throw 'Short-root budget exceeded.' }
        if (Test-Path -LiteralPath $root) { throw 'Exact run root already exists; adoption is forbidden.' }
        $state=[pscustomobject]@{ schemaVersion=3; root=$root; token=$RunToken; runTokenSha256=(Get-Web6B03RunTokenHash $RunToken);
            sid=$sid; sourceSha=$env:GITHUB_SHA; runId=$env:GITHUB_RUN_ID; attempt=$env:GITHUB_RUN_ATTEMPT;
            rootIdentity=$null; toolIdentity=$null; evidenceIdentity=$null; controlIdentity=$null; controlStamp=$null;
            baselineProcesses=@(Processes); toolsSealed=$false; toolInventory=@(); phase='Allocating'; quarantine=$false; verified=$false; children=-1; processes=-1 }
        Save-State
        Initialize-Web6CheckpointStore $CheckpointRoot $CheckpointProfile
        $acl=New-Object Security.AccessControl.DirectorySecurity
        $acl.SetOwner($identity.User); $acl.SetAccessRuleProtection($true,$false)
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($identity.User,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
        # Atomic native create refuses an existing name, including a racing foreign directory.
        if (-not ('Web6B03OwnedDirectoryNative' -as [type])) { Add-Type -TypeDefinition 'using System; using System.IO; using System.ComponentModel; using System.Runtime.InteropServices; public static class Web6B03OwnedDirectoryNative { [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int Length; public IntPtr Descriptor; public int Inherit; } [DllImport("kernel32.dll", EntryPoint="CreateDirectoryW", CharSet=CharSet.Unicode, ExactSpelling=true, SetLastError=true)] private static extern bool CreateDirectory(string path, ref SecurityAttributes attributes); public static void CreateNew(string path, byte[] descriptor) { IntPtr pointer=Marshal.AllocHGlobal(descriptor.Length); try { Marshal.Copy(descriptor,0,pointer,descriptor.Length); var attributes=new SecurityAttributes { Length=Marshal.SizeOf(typeof(SecurityAttributes)), Descriptor=pointer, Inherit=0 }; if (!CreateDirectory(path,ref attributes)) throw new IOException("Owned directory create-new failed.",new Win32Exception(Marshal.GetLastWin32Error())); } finally { Marshal.FreeHGlobal(pointer); } } }' }
        [Web6B03OwnedDirectoryNative]::CreateNew($root,$acl.GetSecurityDescriptorBinaryForm())
        foreach ($name in @('tool-scratch','control')) {
            [Web6B03OwnedDirectoryNative]::CreateNew((Join-Path $root $name),$acl.GetSecurityDescriptorBinaryForm())
        }
        $stamp=Join-Path $root 'control\owner.json'
        $owner=[ordered]@{ schemaVersion=3; token=$RunToken; sid=$sid; sourceSha=$env:GITHUB_SHA; runId=$env:GITHUB_RUN_ID; attempt=$env:GITHUB_RUN_ATTEMPT }
        $ownerText=$owner|ConvertTo-Json -Compress
        $stream=[IO.File]::Open($stamp,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try { $bytes=[Text.Encoding]::UTF8.GetBytes($ownerText); $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
        if ([IO.File]::ReadAllText($stamp,[Text.Encoding]::UTF8) -cne $ownerText) { throw 'Run ownership stamp readback mismatch.' }
        $evidenceAcl=New-Object Security.AccessControl.DirectorySecurity
        $evidenceAcl.SetOwner($identity.User); $evidenceAcl.SetAccessRuleProtection($true,$false)
        foreach ($principal in @($identity.User,[Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))) {
            $evidenceAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($principal,'FullControl','None','None','Allow'))
        }
        [Web6B03OwnedDirectoryNative]::CreateNew((Join-Path $root 'evidence'),$evidenceAcl.GetSecurityDescriptorBinaryForm())
        $state.rootIdentity=Snapshot $root ''; $state.toolIdentity=Snapshot (Join-Path $root 'tool-scratch') 'tool-scratch'; $state.evidenceIdentity=Snapshot (Join-Path $root 'evidence') 'evidence'
        $state.controlIdentity=Snapshot (Join-Path $root 'control') 'control'; $state.controlStamp=Snapshot $stamp 'control\owner.json'
        Save-State; Verify-Root
        if (@(Inventory $root).Count -ne 4) { throw 'Sandbox baseline is not empty.' }
        $state.children=0; $state.processes=0; $state.phase='Ready'; Save-State
        Set-Temp (Join-Path $root 'tool-scratch')
        foreach ($pair in @(@('WEB6_SANDBOX',$root),@('WEB6_EVIDENCE_TEMP',(Join-Path $root 'evidence')),@('NUGET_SCRATCH',(Join-Path $root 'tool-scratch/nuget')))) {
            [IO.File]::AppendAllText($env:GITHUB_ENV,$pair[0]+'='+$pair[1]+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
        }
    } else {
        $state=Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
        if ($state.quarantine -or $state.phase -ne 'Ready') { throw 'Quarantine/completed sandbox cannot be reused.' }
        $prior=@(Assert-Web6CheckpointPrefixStore $CheckpointRoot $CheckpointProfile $CheckpointPhase $Action)
        Verify-Root; Require-Drain
        $evidence=Join-Path $state.root 'evidence'; $toolRoot=Join-Path $state.root 'tool-scratch'
        $state.children=@(Get-ChildItem -LiteralPath $evidence -Force).Count
        if ($state.children -ne 0) { throw 'Evidence children did not drain.' }
        if ($Action -eq 'CheckEvidence') {
            if ($state.toolsSealed) { throw 'No candidate execution admitted after tool seal.' }
        } elseif ($Action -eq 'SealTools') {
            if ($state.toolsSealed) { throw 'Cannot reseal tool inventory.' }
            $state.toolInventory=@(Inventory $toolRoot); $state.toolsSealed=$true
            Set-Temp $evidence
        } else {
            if (Test-Path -LiteralPath ($StatePath+'.active')) { throw 'Active candidate cannot be removed.' }
            Verify-SealedTools
            if ($Action -eq 'Complete') {
                # The cleanup helper pins every verified ancestor and object before
                # deleting any entry. It deletes only those same original handles.
                Remove-Web6SandboxExact $state
                if (Test-Path -LiteralPath $state.root) { throw 'Sandbox removal not proven.' }
                $state.phase='Removed'; Set-Temp $env:RUNNER_TEMP
                Write-Output 'WEB6_SANDBOX phase=Removed evidenceEmpty=1 toolInventoryRemoved=1 controlRemoved=1'
            }
        }
        Save-State
    }
    Write-Web6Checkpoint $CheckpointPhase $prior
    Summary
} catch {
    if ($state -and $state.checkpoint) { $state.checkpoint.quarantine=$true }
    if ($state) { $state.quarantine=$true; $state.phase='Quarantined'; try { Save-State; Summary } catch { } }
    throw 'B03 sandbox uncertainty: retain exact sandbox; no recursive or opportunistic cleanup.'
} finally { Close-Web6CheckpointLeases; $identity.Dispose() }
