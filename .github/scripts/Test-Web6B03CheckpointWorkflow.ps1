# Static source guard only: never invokes the helper, compiler, filesystem fixture or SQL.
param([string]$WorkflowText,[switch]$MutationTests)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Web6B03CheckpointChain.ps1"
function Assert-Web6CheckpointWorkflowBytes([byte[]]$Bytes) {
    if($Bytes.Length -ge 3 -and $Bytes[0] -eq 239 -and $Bytes[1] -eq 187 -and $Bytes[2] -eq 191) { throw 'Workflow UTF-8 BOM denied.' }
    if($Bytes -contains [byte]13) { throw 'Workflow requires LF only; CR denied.' }
    $text=[Text.UTF8Encoding]::new($false,$true).GetString($Bytes)
    if(-not $text.EndsWith("`n",[StringComparison]::Ordinal) -or $text -match '\n[\t ]*\n\z' -or $text -eq "`n") { throw 'Workflow requires one final LF without a terminal blank line.' }
    $text
}
function Assert-Web6CheckpointWorkflow([string]$Text) {
    $calls=@($Text -split "`n"|Where-Object{$_ -match 'Web6B03EvidenceSandbox\.ps1 -Action'})
    if($calls.Count -ne 6) { throw 'Checkpoint helper call sites changed.' }
    $expected=@('Initialize','("Before:"+$class)','("After:"+$class)','SealTools','PostSealCheck','Complete')
    for($i=0;$i -lt $calls.Count;$i++) {
        foreach($required in @('-CheckpointRoot $env:WEB6_CHECKPOINT_ROOT','-CheckpointProfile B03Candidate',('-CheckpointPhase '+$expected[$i]))) {
            if(-not $calls[$i].Contains($required)) { throw 'Explicit checkpoint call binding absent.' }
        }
        if(-not $calls[$i].TrimEnd().EndsWith('-CheckpointPhase '+$expected[$i],[StringComparison]::Ordinal)) { throw 'Checkpoint phase tail changed.' }
    }
    $match=[regex]::Match($Text,'(?s)\$selections=\[ordered\]@\{(.*?)\}')
    $ids=@([regex]::Matches($match.Groups[1].Value,'(?m)^\s+([A-Za-z0-9]+)=\d+\r?$')|ForEach-Object{$_.Groups[1].Value})
    if(($ids -join ',') -cne ((Get-Web6CheckpointSelections) -join ',')) { throw 'Checkpoint closed selection order changed.' }
    foreach($required in @('"WEB6_CHECKPOINT_ROOT=$env:SystemDrive\w6cp-$runToken" >> $env:GITHUB_ENV','Web6B03CheckpointEvidence.ps1 -Action Materialize','Web6B03CheckpointEvidence.ps1 -Action Remove','Web6B03CheckpointEvidence.ps1 -Action VerifyMaterialized',"if ($"+'LASTEXITCODE -ne 0) { throw ''Full B03 checkpoint chain or separate cleanup not verified.'' }')) {
        if(-not $Text.Contains($required)) { throw 'Checkpoint final manifest gate absent.' }
    }
    foreach($line in @($Text -split "`n"|Where-Object{$_ -match 'Web6B03CheckpointEvidence\.ps1 -Action'})) {
        if(-not $line.Contains('-CheckpointRoot $env:WEB6_CHECKPOINT_ROOT -CheckpointProfile B03Candidate') -or -not $line.Contains('-RunToken $env:WEB6_RUN_TOKEN')) { throw 'Checkpoint final gate binding absent.' }
    }
    if($Text.IndexOf('-Action Materialize') -gt $Text.IndexOf('-Action Remove -StatePath $env:WEB6_SANDBOX_STATE') -or $Text.IndexOf('-Action Remove -StatePath $env:WEB6_SANDBOX_STATE') -gt $Text.IndexOf('-Action VerifyMaterialized')) { throw 'Checkpoint materialization/cleanup order changed.' }
}
$workflowBytes=[IO.File]::ReadAllBytes((Join-Path $PSScriptRoot '../workflows/backend-web6-b03-sql-candidate.yml'))
$decodedWorkflow=Assert-Web6CheckpointWorkflowBytes $workflowBytes
if(-not $WorkflowText) { $WorkflowText=$decodedWorkflow }
Assert-Web6CheckpointWorkflow $WorkflowText
$checks=2
$helper=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Web6B03EvidenceSandbox.ps1'))
$store=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Web6B03CheckpointStore.ps1'))
$native=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Web6B03CheckpointNative.cs'))
$delivery=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Web6B03CheckpointEvidence.ps1'))
foreach($pair in @(
    @($helper,'$prior=@(Assert-Web6CheckpointPrefixStore $CheckpointRoot $CheckpointProfile $CheckpointPhase $Action)'),
    @($helper,'Write-Web6Checkpoint $CheckpointPhase $prior'),
    @($store,'Assert-Web6CheckpointPrefix $records $Profile $c.binding'),
    @($store,'Assert-Web6CheckpointStamp $lease (Get-Web6CheckpointStamp $created)'),
    @($native,'FileMode.CreateNew'),@($native,'stream.Flush(true)'),
    @($native,'AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access'),
    @($native,'GetFileInformationByHandle'),@($native,'SetFileInformationByHandle'),
    @($delivery,'Assert-RemainingCheckpointMembers $i $records.Count'),
    @($delivery,'Assert-Web6CheckpointRemovalReceipt $receipt.Text'),
    @($delivery,"throw 'Checkpoint evidence uncertainty: retain exact remaining entries; candidate evidence NOT ACCEPTED.'")
)) { if(-not $pair[0].Contains($pair[1])) { throw 'Checkpoint source protocol missing.' }; $checks++ }
if($helper.IndexOf('$prior=@(Assert-Web6CheckpointPrefixStore') -gt $helper.IndexOf('Verify-Root; Require-Drain') -or $store -match 'Remove-Item|Directory\]::Delete' -or $native -match 'AccessControlSections\.Audit|SeSecurityPrivilege|0x1000000') { throw 'Checkpoint admission/cleanup/OGA protocol changed.' }
$checks++
if($MutationTests) {
    foreach($badBytes in @(
        [byte[]](@(239,187,191)+@($workflowBytes)),
        [Text.Encoding]::UTF8.GetBytes($decodedWorkflow.Replace("`n","`r`n")),
        [Text.Encoding]::UTF8.GetBytes($decodedWorkflow.Replace("`n","`r")),
        [byte[]](@(195,40)+@($workflowBytes)),
        [Text.Encoding]::UTF8.GetBytes($decodedWorkflow.TrimEnd([char]10)),
        [Text.Encoding]::UTF8.GetBytes($decodedWorkflow+"`n"),
        [Text.Encoding]::UTF8.GetBytes($decodedWorkflow+" `t`n")
    )) {
        $denied=$false; try { [void](Assert-Web6CheckpointWorkflowBytes $badBytes) } catch { $denied=$true }
        if(-not $denied) { throw 'Workflow encoding mutation escaped.' }; $checks++
    }
    foreach($bad in @(
        $WorkflowText.Replace('-CheckpointRoot $env:WEB6_CHECKPOINT_ROOT',''),
        $WorkflowText.Replace('-CheckpointProfile B03Candidate','-CheckpointProfile NativeSafety'),
        $WorkflowText.Replace('("After:"+$class)','("Before:"+$class)'),
        $WorkflowText.Replace('("Before:"+$class)','("Before:Unknown")'),
        $WorkflowText.Replace('WebOperationMaintenanceOperatorTests=89','Unknown=89'),
        $WorkflowText.Replace('-Action VerifyMaterialized','-Action Check'),
        $WorkflowText.Replace('-Action Materialize','-Action Check')
    )) {
        $denied=$false; try { Assert-Web6CheckpointWorkflow $bad } catch { $denied=$true }
        if(-not $denied) { throw 'Checkpoint static mutation escaped.' }; $checks++
    }
}
Write-Output "WEB6_CHECKPOINT_STATIC checks=$checks passed=$checks allocations=0"
