param(
    [Parameter(Mandatory=$true)][ValidateSet('Materialize','Remove','VerifyMaterialized')][string]$Action,
    [Parameter(Mandatory=$true)][string]$StatePath,
    [Parameter(Mandatory=$true)][string]$CheckpointRoot,
    [Parameter(Mandatory=$true)][ValidateSet('NativeSafety','B03Candidate')][string]$CheckpointProfile,
    [Parameter(Mandatory=$true)][string]$ArtifactPath,
    [Parameter(Mandatory=$true)][string]$RunToken
)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Web6B03RunPath.ps1"
. "$PSScriptRoot/Web6B03CheckpointStore.ps1"
$state=$null
function Save-State { [IO.File]::WriteAllText($StatePath,($script:state|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false)) }
function Require-NoCheckpointOwners {
    $active=@(Get-Process|Where-Object{$_.ProcessName -match '^(dotnet|MSBuild|VBCSCompiler|testhost(\.net48|\.x86)?|MyDmsVn\.WebOperation\.(Operator|CrashHost)|MyDmsVn\.Server\.DbMigrator)$'}|ForEach-Object{$_.Id.ToString()+':'+$_.StartTime.ToUniversalTime().Ticks.ToString()}|Where-Object{$_ -notin @($state.baselineProcesses)})
    if($active.Count -ne 0) { throw 'Checkpoint owner drain denied.' }
}
function Assert-Materialized {
    $m=$state.checkpoint.materialized
    if(-not $m -or $m.path -cne $ArtifactPath) { throw 'Checkpoint materialization absent.' }
    $lease=Open-Web6CheckpointLease $ArtifactPath $false; Assert-Web6CheckpointStamp $lease $m.stamp
    Assert-Web6CheckpointBundle $lease.Text $CheckpointProfile $state.checkpoint.binding
    $script:materializedText=$lease.Text
}
function Assert-RemainingCheckpointMembers([int]$Start,[int]$Count) {
    $expected=@('manifest.json')+@(for($j=$Start;$j -lt $Count;$j++) { Get-Web6CheckpointFileName $j })
    $actual=@(Get-ChildItem -LiteralPath $CheckpointRoot -Force|ForEach-Object Name)
    if($actual.Count -ne $expected.Count -or @($actual|Where-Object{$_ -cnotin $expected}).Count -ne 0) { throw 'Checkpoint cleanup membership changed.' }
}
try {
    $state=Get-Content -LiteralPath $StatePath -Raw|ConvertFrom-Json
    $c=$state.checkpoint
    if($state.quarantine -or $state.phase -ne 'Removed' -or -not $c -or $c.quarantine -or $c.profile -cne $CheckpointProfile -or $c.root -cne $CheckpointRoot -or $state.token -cne $RunToken -or $c.binding.tokenHash -cne (Get-Web6B03RunTokenHash $RunToken) -or $c.binding.sourceHash -cne (Get-Web6CheckpointHash $env:GITHUB_SHA) -or $c.binding.contextHash -cne (Get-Web6CheckpointContext)) { throw 'Checkpoint final context denied.' }
    Assert-Web6CheckpointLocation $CheckpointRoot $RunToken $state.root
    if(Test-Path -LiteralPath $state.root) { throw 'Candidate removal not proven.' }
    Import-Web6CheckpointNative; $script:checkpointLeases=New-Object Collections.ArrayList
    Require-NoCheckpointOwners
    if($Action -eq 'VerifyMaterialized') {
        if($c.phase -ne 'Removed' -or (Test-Path -LiteralPath $CheckpointRoot)) { throw 'Checkpoint removal not proven.' }
        Assert-Materialized
        if(-not $c.removalReceipt) { throw 'Checkpoint removal delivery absent.' }
        $receipt=Open-Web6CheckpointLease ($ArtifactPath+'.removal.json') $false
        Assert-Web6CheckpointStamp $receipt $c.removalReceipt
        Assert-Web6CheckpointRemovalReceipt $receipt.Text $script:materializedText $CheckpointProfile $c.binding
        Write-Output 'WEB6_CHECKPOINT chainVerified=1 materialized=1 checkpointRemoved=1 activeProcesses=0'
    } else {
        $records=@(Assert-Web6CheckpointPrefixStore $CheckpointRoot $CheckpointProfile '' '')
        if(-not $c.manifest) { throw 'Checkpoint final manifest absent.' }
        Assert-Web6CheckpointPrefix $records $CheckpointProfile $c.binding -Complete
        if($Action -eq 'Materialize') {
            if($c.materialized -or (Test-Path -LiteralPath $ArtifactPath)) { throw 'Checkpoint materialization create-new required.' }
            $full=[IO.Path]::GetFullPath($ArtifactPath)
            if($full -cne $ArtifactPath -or $full.StartsWith($CheckpointRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or $full.StartsWith($state.root+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Checkpoint artifact containment denied.' }
            $manifest=New-Web6CheckpointManifest $CheckpointProfile $c.binding $records
            $bundle=New-Web6CheckpointBundle $records $manifest
            Assert-Web6CheckpointBundle $bundle $CheckpointProfile $c.binding
            $created=[Web6CheckpointNative]::WriteNew($ArtifactPath,$bundle)
            $lease=Open-Web6CheckpointLease $ArtifactPath $false
            Assert-Web6CheckpointStamp $lease (Get-Web6CheckpointStamp $created)
            if($lease.Text -cne $bundle) { throw 'Checkpoint materialization delivery denied.' }
            $c.materialized=[pscustomobject]@{path=$ArtifactPath;stamp=(Get-Web6CheckpointStamp $lease)}; Save-State
        } else {
            Assert-Materialized
            # Full prefix/manifest is verified first. Only derived, manifest-listed exact
            # record names are deletion targets; never enumerate siblings for cleanup.
            Close-Web6CheckpointLeases
            $script:checkpointLeases=New-Object Collections.ArrayList
            $rootLease=Open-Web6CheckpointLease $CheckpointRoot $true; Assert-Web6CheckpointStamp $rootLease $c.identity
            $parent=Open-Web6CheckpointLease ($env:SystemDrive+'\') $true $false; Assert-Web6CheckpointStamp $parent $c.parent
            Assert-Materialized
            Require-NoCheckpointOwners
            for($i=0;$i -lt $records.Count;$i++) {
                Require-NoCheckpointOwners
                Assert-RemainingCheckpointMembers $i $records.Count
                $path=Join-Path $CheckpointRoot (Get-Web6CheckpointFileName $i)
                $lease=[Web6CheckpointNative+Lease]::new($path,$false,$true,$true)
                try { Assert-Web6CheckpointStamp $lease $c.files[$i]; $lease.DeleteExact() } finally { $lease.Dispose() }
            }
            Assert-RemainingCheckpointMembers $records.Count $records.Count
            Require-NoCheckpointOwners
            $lease=[Web6CheckpointNative+Lease]::new((Join-Path $CheckpointRoot 'manifest.json'),$false,$true,$true)
            try { Assert-Web6CheckpointStamp $lease $c.manifest; $lease.DeleteExact() } finally { $lease.Dispose() }
            if(@(Get-ChildItem -LiteralPath $CheckpointRoot -Force).Count -ne 0) { throw 'Checkpoint unexpected remaining member.' }
            Close-Web6CheckpointLeases
            $parent=Open-Web6CheckpointLease ($env:SystemDrive+'\') $true $false; Assert-Web6CheckpointStamp $parent $c.parent
            Assert-Materialized
            $lease=[Web6CheckpointNative+Lease]::new($CheckpointRoot,$true,$true,$true)
            try { Assert-Web6CheckpointStamp $lease $c.identity; Require-NoCheckpointOwners; if(@(Get-ChildItem -LiteralPath $CheckpointRoot -Force).Count -ne 0) { throw 'Checkpoint root not empty.' }; $lease.DeleteExact() } finally { $lease.Dispose() }
            if(Test-Path -LiteralPath $CheckpointRoot) { throw 'Checkpoint exact root retained.' }
            Require-NoCheckpointOwners
            $receipt=New-Web6CheckpointRemovalReceipt $script:materializedText $CheckpointProfile $c.binding
            $created=[Web6CheckpointNative]::WriteNew(($ArtifactPath+'.removal.json'),$receipt)
            $delivered=Open-Web6CheckpointLease ($ArtifactPath+'.removal.json') $false
            Assert-Web6CheckpointStamp $delivered (Get-Web6CheckpointStamp $created)
            Assert-Web6CheckpointRemovalReceipt $delivered.Text $script:materializedText $CheckpointProfile $c.binding
            $c.removalReceipt=Get-Web6CheckpointStamp $delivered
            $c.phase='Removed'; Save-State
        }
    }
} catch {
    if($state -and $state.checkpoint) { $state.checkpoint.quarantine=$true; try { Save-State } catch { } }
    throw 'Checkpoint evidence uncertainty: retain exact remaining entries; candidate evidence NOT ACCEPTED.'
} finally { Close-Web6CheckpointLeases }
