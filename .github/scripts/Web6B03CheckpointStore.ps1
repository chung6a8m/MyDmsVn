# Runtime functions only. Dot sourcing does not allocate roots or invoke native IO.
. "$PSScriptRoot/Web6B03CheckpointChain.ps1"
function Import-Web6CheckpointNative {
    if(-not ('Web6CheckpointNative' -as [type])) { Add-Type -Path "$PSScriptRoot/Web6B03CheckpointNative.cs" }
}
function Get-Web6CheckpointContext {
    Get-Web6CheckpointHash ($env:GITHUB_REPOSITORY_ID+':'+$env:GITHUB_RUN_ID+':'+$env:GITHUB_RUN_ATTEMPT)
}
function Assert-Web6CheckpointLocation([string]$Root,[string]$Token,[string]$Candidate) {
    if($Token -cnotmatch '\A[0-9a-f]{32}\z' -or $env:SystemDrive -cnotmatch '\A[A-Z]:\z' -or $Root -cne ($env:SystemDrive+'\w6cp-'+$Token)) { throw 'Checkpoint canonical location denied.' }
    foreach($excluded in @($Candidate,$env:TEMP,$env:TMP)) {
        if($excluded -and ($Root -ieq $excluded.TrimEnd('\') -or $Root.StartsWith($excluded.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase))) { throw 'Checkpoint containment denied.' }
    }
    $drive=[IO.DriveInfo]::new($env:SystemDrive+'\')
    if($drive.DriveType -ne [IO.DriveType]::Fixed -or $drive.DriveFormat -ne 'NTFS') { throw 'Checkpoint fixed NTFS required.' }
}
function Get-Web6CheckpointStamp($Lease) { [pscustomobject][ordered]@{identity=$Lease.IdentityHash;acl=$Lease.AclHash;hash=$Lease.ContentHash} }
function Assert-Web6CheckpointStamp($Lease,$Stamp) {
    if($Lease.IdentityHash -cne $Stamp.identity -or $Lease.AclHash -cne $Stamp.acl -or $Lease.ContentHash -cne $Stamp.hash) { throw 'Checkpoint delivery identity changed.' }
}
function Open-Web6CheckpointLease([string]$Path,[bool]$Directory,[bool]$Owner=$true) {
    $lease=[Web6CheckpointNative+Lease]::new($Path,$Directory,$Owner,$false)
    [void]$script:checkpointLeases.Add($lease); $lease
}
function Close-Web6CheckpointLeases {
    if($script:checkpointLeases) { foreach($lease in $script:checkpointLeases) { $lease.Dispose() }; $script:checkpointLeases.Clear() }
}
function Get-Web6CheckpointFileName([int]$Sequence) { 'record-'+($Sequence+1).ToString('D2',[Globalization.CultureInfo]::InvariantCulture)+'.json' }
function Initialize-Web6CheckpointStore([string]$Root,[string]$Profile) {
    [void](Get-Web6CheckpointPhases $Profile)
    Assert-Web6CheckpointLocation $Root $RunToken $state.root
    Import-Web6CheckpointNative
    $script:checkpointLeases=New-Object Collections.ArrayList
    $parent=Open-Web6CheckpointLease ($env:SystemDrive+'\') $true $false
    if(Test-Path -LiteralPath $Root) { throw 'Checkpoint existing leaf denied.' }
    $state|Add-Member checkpoint ([pscustomobject]@{root=$Root;profile=$Profile;parent=(Get-Web6CheckpointStamp $parent);identity=$null;binding=$null;files=@();manifest=$null;materialized=$null;removalReceipt=$null;phase='Allocating';quarantine=$false})
    Save-State
    [Web6CheckpointNative]::CreateRoot($Root)
    $rootLease=Open-Web6CheckpointLease $Root $true
    $state.checkpoint.identity=Get-Web6CheckpointStamp $rootLease
    if(@(Get-ChildItem -LiteralPath $Root -Force).Count -ne 0) { throw 'Checkpoint initial membership denied.' }
    $state.checkpoint.phase='Ready'; Save-State
}
function Assert-Web6CheckpointPrefixStore([string]$Root,[string]$Profile,[string]$ExpectedPhase,[string]$ExpectedAction) {
    Assert-Web6CheckpointLocation $Root $RunToken $state.root
    Import-Web6CheckpointNative
    if(-not $script:checkpointLeases) { $script:checkpointLeases=New-Object Collections.ArrayList }
    $c=$state.checkpoint
    if(-not $c -or $c.quarantine -or $c.phase -ne 'Ready' -or $c.root -cne $Root -or $c.profile -cne $Profile) { throw 'Checkpoint context denied.' }
    $parent=Open-Web6CheckpointLease ($env:SystemDrive+'\') $true $false; Assert-Web6CheckpointStamp $parent $c.parent
    $rootLease=Open-Web6CheckpointLease $Root $true; Assert-Web6CheckpointStamp $rootLease $c.identity
    if($c.binding.tokenHash -cne (Get-Web6B03RunTokenHash $RunToken) -or $c.binding.contextHash -cne (Get-Web6CheckpointContext) -or $c.binding.sourceHash -cne (Get-Web6CheckpointHash $env:GITHUB_SHA) -or $c.binding.checkpointIdentityHash -cne $rootLease.IdentityHash -or $c.binding.rootIdentityHash -cne $state.rootIdentity.nativeIdentity -or $c.binding.controlIdentityHash -cne $state.controlIdentity.nativeIdentity) { throw 'Checkpoint current binding denied.' }
    $records=@(); $expected=@()
    for($i=0;$i -lt @($c.files).Count;$i++) {
        $name=Get-Web6CheckpointFileName $i; $expected+=$name
        $lease=Open-Web6CheckpointLease (Join-Path $Root $name) $false
        Assert-Web6CheckpointStamp $lease $c.files[$i]; $records+=,$lease.Text
    }
    if($c.manifest) {
        $expected+='manifest.json'; $lease=Open-Web6CheckpointLease (Join-Path $Root 'manifest.json') $false
        Assert-Web6CheckpointStamp $lease $c.manifest
        if($lease.Text -cne (New-Web6CheckpointManifest $Profile $c.binding $records)) { throw 'Checkpoint manifest denied.' }
    }
    $actual=@(Get-ChildItem -LiteralPath $Root -Force|ForEach-Object Name)
    if($actual.Count -ne $expected.Count -or @($actual|Where-Object{$_ -cnotin $expected}).Count -ne 0) { throw 'Checkpoint exact membership denied.' }
    Assert-Web6CheckpointPrefix $records $Profile $c.binding
    if($ExpectedPhase) {
        $phases=@(Get-Web6CheckpointPhases $Profile)
        if($records.Count -ge $phases.Count -or $ExpectedPhase -cne $phases[$records.Count] -or $ExpectedAction -cne (Get-Web6CheckpointAction $ExpectedPhase) -or $c.manifest) { throw 'Checkpoint next phase denied.' }
    }
    $records
}
function Write-Web6CheckpointDelivery([string]$Name,[string]$Text) {
    $path=Join-Path $CheckpointRoot $Name
    $created=[Web6CheckpointNative]::WriteNew($path,$Text)
    $lease=Open-Web6CheckpointLease $path $false
    Assert-Web6CheckpointStamp $lease (Get-Web6CheckpointStamp $created)
    if($lease.Text -cne $Text -or $lease.ContentHash -cne (Get-Web6CheckpointHash $Text)) { throw 'Checkpoint delivery denied.' }
    Get-Web6CheckpointStamp $lease
}
function Get-Web6CheckpointInventoryDigest($Entries) {
    # Only the digest leaves the private store. Relative names and ACLs are never delivered.
    if(@($Entries).Count -eq 0) { return Get-Web6CheckpointHash '' }
    Get-Web6CheckpointHash (ConvertTo-Web6CheckpointJson @($Entries|Sort-Object relative))
}
function Write-Web6Checkpoint([string]$Phase,[string[]]$Prior) {
    $c=$state.checkpoint
    if(-not $c.binding) {
        $rootLease=Open-Web6CheckpointLease $state.root $true
        $controlLease=Open-Web6CheckpointLease (Join-Path $state.root 'control') $true
        $c.binding=[ordered]@{tokenHash=(Get-Web6B03RunTokenHash $RunToken);contextHash=(Get-Web6CheckpointContext);sourceHash=(Get-Web6CheckpointHash $env:GITHUB_SHA);rootIdentityHash=$rootLease.IdentityHash;controlIdentityHash=$controlLease.IdentityHash;checkpointIdentityHash=$c.identity.identity}
    }
    Require-Drain
    if($Phase -eq 'Complete') {
        if(Test-Path -LiteralPath $state.root) { throw 'Checkpoint removal observation denied.' }
        $evidence=@(); $tools=@()
    } else {
        $evidence=@(Inventory (Join-Path $state.root 'evidence')); $tools=@(Inventory (Join-Path $state.root 'tool-scratch'))
    }
    $previous=if($Prior.Count){($Prior[-1]|ConvertFrom-Json).recordHash}else{'0'*64}
    $text=New-Web6CheckpointRecord $c.profile $Phase $c.binding ($Prior.Count+1) $previous $evidence.Count (Get-Web6CheckpointInventoryDigest $evidence) $tools.Count (Get-Web6CheckpointInventoryDigest $tools) $state.processes ([DateTime]::UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",[Globalization.CultureInfo]::InvariantCulture))
    $all=@($Prior)+@($text); Assert-Web6CheckpointPrefix $all $c.profile $c.binding
    $stamp=Write-Web6CheckpointDelivery (Get-Web6CheckpointFileName $Prior.Count) $text
    $c.files=@($c.files)+@($stamp); Save-State
    if($Phase -eq 'Complete') {
        $manifest=New-Web6CheckpointManifest $c.profile $c.binding $all
        $c.manifest=Write-Web6CheckpointDelivery 'manifest.json' $manifest; Save-State
    }
    # Recheck exact membership and delivery while all prior file/root leases remain
    # pinned. A phase is not successful merely because its last write returned.
    [void](Assert-Web6CheckpointPrefixStore $CheckpointRoot $c.profile '' '')
}
