# Pure codec. No filesystem, process or environment access. Reject noncanonical JSON,
# unknown fields and free text before returning any evidence to a controller.
function Get-Web6CheckpointHash([string]$Text) {
    $h=[Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($h.ComputeHash([Text.UTF8Encoding]::new($false,$true).GetBytes($Text)))).Replace('-','').ToLowerInvariant() } finally { $h.Dispose() }
}
function Get-Web6CheckpointSelections {
    @('WebOperationMaintenanceOperatorTests','WebOperationMaintenanceEvidenceTests','WebOperationReconciliationOperatorTests','Web6MaintenanceRecoveryTests','WebOperationOperatorDecisionTests','WebOperationOperatorInspectTests','WebOperationResolutionTests','WebOperationTerminalCrashTests','Web5BackupSafetyTests','WebRecoveryPilotResultTests','Web5BackupRestoreTests','WebOperationHttpSqlTests')
}
function Get-Web6CheckpointPhases([string]$Profile) {
    if ($Profile -ceq 'NativeSafety') { return @('Initialize','AfterDiscovery','AfterTests','AfterEvidenceValidation','SealTools','PostSealCheck','Complete') }
    if ($Profile -cne 'B03Candidate') { throw 'Checkpoint profile denied.' }
    @('Initialize')+@(foreach($id in (Get-Web6CheckpointSelections)) { 'Before:'+$id; 'After:'+$id })+@('SealTools','PostSealCheck','Complete')
}
function Get-Web6CheckpointAction([string]$Phase) {
    switch -CaseSensitive ($Phase) { 'Initialize' {'Initialize'} 'SealTools' {'SealTools'} 'PostSealCheck' {'Check'} 'Complete' {'Complete'} default {'CheckEvidence'} }
}
function Assert-Web6CheckpointBinding($Binding) {
    $keys=@('tokenHash','contextHash','sourceHash','rootIdentityHash','controlIdentityHash','checkpointIdentityHash')
    $actual=if($Binding -is [Collections.IDictionary]){@($Binding.Keys)}else{@($Binding.PSObject.Properties.Name)}
    if (($actual -join ',') -cne ($keys -join ',')) { throw 'Checkpoint binding schema denied.' }
    foreach ($key in $keys) { if ($Binding.$key -isnot [string] -or $Binding.$key -cnotmatch '\A[0-9a-f]{64}\z') { throw 'Checkpoint binding denied.' } }
}
function ConvertTo-Web6CheckpointJson($Value) { ConvertTo-Json -InputObject $Value -Depth 12 -Compress }
function New-Web6CheckpointRecord([string]$Profile,[string]$Phase,$Binding,[int]$Sequence,[string]$PreviousHash,[long]$EvidenceCount,[string]$EvidenceDigest,[long]$ToolCount,[string]$ToolDigest,[int]$Processes,[string]$Utc) {
    Assert-Web6CheckpointBinding $Binding
    $phases=@(Get-Web6CheckpointPhases $Profile)
    if ($Sequence -lt 1 -or $Sequence -gt $phases.Count -or $Phase -cne $phases[$Sequence-1]) { throw 'Checkpoint phase order denied.' }
    foreach($hash in @($PreviousHash,$EvidenceDigest,$ToolDigest)) { if($hash -cnotmatch '\A[0-9a-f]{64}\z') { throw 'Checkpoint digest denied.' } }
    if ($Utc -cnotmatch '\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{7}Z\z') { throw 'Checkpoint UTC denied.' }
    [void][DateTime]::ParseExact($Utc,"yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'",[Globalization.CultureInfo]::InvariantCulture)
    if($EvidenceCount -ne 0 -or $Processes -ne 0 -or $ToolCount -lt 0 -or ($Phase -cin @('Initialize','Complete') -and $ToolCount -ne 0)) { throw 'Checkpoint observation denied.' }
    if($EvidenceDigest -cne (Get-Web6CheckpointHash '') -or ($ToolCount -eq 0 -and $ToolDigest -cne (Get-Web6CheckpointHash ''))) { throw 'Empty observation digest denied.' }
    $r=[ordered]@{version=1;profile=$Profile;phaseListHash=(Get-Web6CheckpointHash ($phases -join "`n"));sequence=$Sequence;phase=$Phase;status=$(if($Phase -eq 'Complete'){'Removed'}else{'Verified'});utc=$Utc}
    foreach($k in $Binding.Keys) { $r[$k]=$Binding[$k] }
    if ($Binding -isnot [Collections.IDictionary]) { foreach($p in $Binding.PSObject.Properties) { $r[$p.Name]=$p.Value } }
    $r.evidenceCount=$EvidenceCount; $r.evidenceDigest=$EvidenceDigest; $r.toolCount=$ToolCount; $r.toolDigest=$ToolDigest; $r.activeProcessCount=$Processes
    $r.previousHash=$PreviousHash; $r.delivery='CreateNewFlushReadback'; $r.recordHash=Get-Web6CheckpointHash (ConvertTo-Web6CheckpointJson $r)
    ConvertTo-Web6CheckpointJson $r
}
function Assert-Web6CheckpointPrefix([string[]]$Records,[string]$Profile,$Binding,[switch]$Complete) {
    Assert-Web6CheckpointBinding $Binding
    $phases=@(Get-Web6CheckpointPhases $Profile); $previous='0'*64; $sealed=$null; $lastUtc=$null
    if($Records.Count -gt $phases.Count -or ($Complete -and $Records.Count -ne $phases.Count)) { throw 'Checkpoint prefix length denied.' }
    for($i=0;$i -lt $Records.Count;$i++) {
        $r=$Records[$i]|ConvertFrom-Json
        $canonical=New-Web6CheckpointRecord $Profile $phases[$i] $Binding ($i+1) $previous $r.evidenceCount $r.evidenceDigest $r.toolCount $r.toolDigest $r.activeProcessCount $r.utc
        if($canonical -cne $Records[$i]) { throw 'Checkpoint canonical record or binding denied.' }
        if($lastUtc -and [string]::CompareOrdinal($lastUtc,$r.utc) -gt 0) { throw 'Checkpoint UTC order denied.' }; $lastUtc=$r.utc
        if($r.phase -eq 'SealTools') { $sealed=$r }
        if($r.phase -eq 'PostSealCheck' -and ($r.toolCount -ne $sealed.toolCount -or $r.toolDigest -cne $sealed.toolDigest)) { throw 'Sealed observation changed.' }
        $previous=$r.recordHash
    }
}
function New-Web6CheckpointManifest([string]$Profile,$Binding,[string[]]$Records) {
    Assert-Web6CheckpointPrefix $Records $Profile $Binding -Complete
    $m=[ordered]@{version=1;profile=$Profile;phases=@(Get-Web6CheckpointPhases $Profile);phaseListHash=(Get-Web6CheckpointHash ((Get-Web6CheckpointPhases $Profile) -join "`n"));binding=$Binding;recordHashes=@($Records|ForEach-Object{($_|ConvertFrom-Json).recordHash});chainHead=($Records[-1]|ConvertFrom-Json).recordHash}
    $m.manifestHash=Get-Web6CheckpointHash (ConvertTo-Web6CheckpointJson $m)
    ConvertTo-Web6CheckpointJson $m
}
function New-Web6CheckpointBundle([string[]]$Records,[string]$Manifest) {
    ConvertTo-Web6CheckpointJson ([ordered]@{version=1;records=@($Records|ForEach-Object{ConvertFrom-Json $_});manifest=($Manifest|ConvertFrom-Json)})
}
function Assert-Web6CheckpointBundle([string]$Text,[string]$Profile,$Binding) {
    $b=$Text|ConvertFrom-Json
    $records=@($b.records|ForEach-Object{ConvertTo-Web6CheckpointJson $_})
    $manifest=New-Web6CheckpointManifest $Profile $Binding $records
    if((New-Web6CheckpointBundle $records $manifest) -cne $Text) { throw 'Checkpoint bundle/manifest denied.' }
}
function New-Web6CheckpointRemovalReceipt([string]$Bundle,[string]$Profile,$Binding) {
    Assert-Web6CheckpointBundle $Bundle $Profile $Binding
    $r=[ordered]@{version=1;profile=$Profile;binding=$Binding;bundleHash=(Get-Web6CheckpointHash $Bundle);checkpointRemoved=$true;activeProcessCount=0;status='VerifiedRemoval'}
    $r.receiptHash=Get-Web6CheckpointHash (ConvertTo-Web6CheckpointJson $r)
    ConvertTo-Web6CheckpointJson $r
}
function Assert-Web6CheckpointRemovalReceipt([string]$Text,[string]$Bundle,[string]$Profile,$Binding) {
    if($Text -cne (New-Web6CheckpointRemovalReceipt $Bundle $Profile $Binding)) { throw 'Checkpoint removal receipt denied.' }
}
