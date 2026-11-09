$ErrorActionPreference='Stop'
. "$PSScriptRoot/Web6B03CheckpointChain.ps1"
$script:checks=0
function Expect([scriptblock]$body,[bool]$accept=$true) {
    $ok=$true; try { & $body | Out-Null } catch { $ok=$false }
    if ($ok -ne $accept) { throw ('Checkpoint pure assertion failed at '+$script:checks) }
    $script:checks++
}
$binding=[ordered]@{ tokenHash=('a'*64); contextHash=('b'*64); sourceHash=('c'*64); rootIdentityHash=('d'*64); controlIdentityHash=('e'*64); checkpointIdentityHash=('f'*64) }
Expect { if(((Get-Web6CheckpointPhases 'NativeSafety') -join ',') -cne 'Initialize,AfterDiscovery,AfterTests,AfterEvidenceValidation,SealTools,PostSealCheck,Complete') { throw 'Closed native phases changed.' } }
$closedIds=@('WebOperationMaintenanceOperatorTests','WebOperationMaintenanceEvidenceTests','WebOperationReconciliationOperatorTests','Web6MaintenanceRecoveryTests','WebOperationOperatorDecisionTests','WebOperationOperatorInspectTests','WebOperationResolutionTests','WebOperationTerminalCrashTests','Web5BackupSafetyTests','WebRecoveryPilotResultTests','Web5BackupRestoreTests','WebOperationHttpSqlTests')
$closedCandidate=@('Initialize')+@(foreach($id in $closedIds){'Before:'+$id;'After:'+$id})+@('SealTools','PostSealCheck','Complete')
Expect { if(((Get-Web6CheckpointPhases 'B03Candidate') -join ',') -cne ($closedCandidate -join ',')) { throw 'Closed candidate phases changed.' } }
foreach ($profile in @('NativeSafety','B03Candidate')) {
    $phases=@(Get-Web6CheckpointPhases $profile); $records=@(); $previous='0'*64
    foreach ($phase in $phases) {
        $record=New-Web6CheckpointRecord $profile $phase $binding ($records.Count+1) $previous 0 (Get-Web6CheckpointHash '') 0 (Get-Web6CheckpointHash '') 0 '2026-11-09T00:00:00.0000000Z'
        $records+=,$record; $previous=($record|ConvertFrom-Json).recordHash
    }
    $manifest=New-Web6CheckpointManifest $profile $binding $records
    $bundle=New-Web6CheckpointBundle $records $manifest
    Expect { Assert-Web6CheckpointBundle $bundle $profile $binding }
    $rehydrated=ConvertFrom-Json (ConvertTo-Web6CheckpointJson $binding)
    Expect { Assert-Web6CheckpointBundle $bundle $profile $rehydrated }
    $receipt=New-Web6CheckpointRemovalReceipt $bundle $profile $binding
    Expect { Assert-Web6CheckpointRemovalReceipt $receipt $bundle $profile $binding }
    Expect { Assert-Web6CheckpointRemovalReceipt ($receipt.Replace('VerifiedRemoval','Unknown')) $bundle $profile $binding } $false
    Expect { Assert-Web6CheckpointRemovalReceipt ($receipt.Replace('true','false')) $bundle $profile $binding } $false
    Expect { Assert-Web6CheckpointRemovalReceipt ($receipt+$receipt) $bundle $profile $binding } $false
    for ($i=0;$i -lt $records.Count;$i++) {
        $bad=@($records); $bad=@($bad | Select-Object -SkipLast ($bad.Count-$i))+@($records | Select-Object -Skip ($i+1))
        Expect { Assert-Web6CheckpointPrefix $bad $profile $binding -Complete } $false
        $bad=@($records[0..$i])+@($records[$i..($records.Count-1)])
        Expect { Assert-Web6CheckpointPrefix $bad $profile $binding -Complete } $false
        if ($i -gt 0) {
            $bad=@($records); $bad[$i]=$records[$i-1]; $bad[$i-1]=$records[$i]
            Expect { Assert-Web6CheckpointPrefix $bad $profile $binding -Complete } $false
        }
    }
    foreach ($key in $binding.Keys) {
        $wrong=[ordered]@{}; foreach ($k in $binding.Keys) { $wrong[$k]=$binding[$k] }; $wrong[$key]='1'*64
        Expect { Assert-Web6CheckpointBundle $bundle $profile $wrong } $false
    }
    foreach ($field in @('previousHash','evidenceDigest','toolDigest','recordHash','rootIdentityHash','phaseListHash')) {
        $row=$records[1]|ConvertFrom-Json; $row.$field='2'*64
        $bad=@($records); $bad[1]=$row|ConvertTo-Json -Compress
        Expect { Assert-Web6CheckpointPrefix $bad $profile $binding } $false
    }
    foreach ($field in @('phase','status','delivery')) {
        foreach ($value in @('Unknown','C:\private\secret','token=confidential')) {
            $row=$records[1]|ConvertFrom-Json; $row.$field=$value
            $bad=@($records); $bad[1]=$row|ConvertTo-Json -Compress
            Expect { Assert-Web6CheckpointPrefix $bad $profile $binding } $false
        }
    }
    Expect { Assert-Web6CheckpointBundle ($bundle+$bundle) $profile $binding } $false
    Expect { Assert-Web6CheckpointBundle ('{"global":'+$bundle+'}') $profile $binding } $false
    Expect { Assert-Web6CheckpointBundle ($bundle.Replace('CreateNewFlushReadback','Unknown')) $profile $binding } $false
    Expect { Assert-Web6CheckpointBundle $bundle $(if($profile -eq 'NativeSafety'){'B03Candidate'}else{'NativeSafety'}) $binding } $false
    Expect { Assert-Web6CheckpointBundle ($bundle.Replace('"manifestHash":"','"manifestHash":"0')) $profile $binding } $false
    Expect { New-Web6CheckpointRecord $profile 'Before:Unknown' $binding 1 ('0'*64) 0 ('0'*64) 0 ('0'*64) 0 '2026-11-09T00:00:00.0000000Z' } $false
    # Rehashed observations must still obey seal->check invariants. This is not
    # merely a digest-tamper test: each altered row has a valid canonical hash.
    $different=@(); $previous='0'*64
    for($i=0;$i -lt $phases.Count;$i++) {
        $count=if($phases[$i] -eq 'SealTools'){1}else{0}
        $digest=if($count){'3'*64}else{Get-Web6CheckpointHash ''}
        $row=New-Web6CheckpointRecord $profile $phases[$i] $binding ($i+1) $previous 0 (Get-Web6CheckpointHash '') $count $digest 0 '2026-11-09T00:00:00.0000000Z'
        $different+=,$row; $previous=($row|ConvertFrom-Json).recordHash
    }
    Expect { Assert-Web6CheckpointPrefix $different $profile $binding -Complete } $false
    foreach($badValue in @(-1,1)) {
        Expect { New-Web6CheckpointRecord $profile 'Initialize' $binding 1 ('0'*64) $badValue (Get-Web6CheckpointHash '') 0 (Get-Web6CheckpointHash '') 0 '2026-11-09T00:00:00.0000000Z' } $false
        Expect { New-Web6CheckpointRecord $profile 'Initialize' $binding 1 ('0'*64) 0 (Get-Web6CheckpointHash '') 0 (Get-Web6CheckpointHash '') $badValue '2026-11-09T00:00:00.0000000Z' } $false
    }
    Expect { Assert-Web6CheckpointBundle ($bundle.Replace('"version":1,','"version":1,"version":1,')) $profile $binding } $false
    Expect { Assert-Web6CheckpointBundle (' '+$bundle) $profile $binding } $false
    $other=$bundle.Replace(('a'*64),('1'*64))
    Expect { Assert-Web6CheckpointBundle $other $profile $binding } $false
    $otherBinding=[ordered]@{}; foreach($key in $binding.Keys){$otherBinding[$key]=$binding[$key]}; $otherBinding.tokenHash='1'*64
    $otherRecords=@(); $previous='0'*64
    for($i=0;$i -lt $phases.Count;$i++) {
        $count=if($phases[$i] -cin @('Initialize','Complete')){0}else{2}
        $digest=if($count){'3'*64}else{Get-Web6CheckpointHash ''}
        $row=New-Web6CheckpointRecord $profile $phases[$i] $otherBinding ($i+1) $previous 0 (Get-Web6CheckpointHash '') $count $digest 0 '2026-11-09T00:00:00.0000000Z'
        $otherRecords+=,$row; $previous=($row|ConvertFrom-Json).recordHash
    }
    $otherBundle=New-Web6CheckpointBundle $otherRecords (New-Web6CheckpointManifest $profile $otherBinding $otherRecords)
    Expect { Assert-Web6CheckpointBundle $otherBundle $profile $otherBinding }
    Expect { Assert-Web6CheckpointBundle $otherBundle $profile $binding } $false
    Expect { Assert-Web6CheckpointBundle ('['+$bundle+','+$otherBundle+']') $profile $binding } $false
}
Write-Output "WEB6_CHECKPOINT_PURE checks=$script:checks passed=$script:checks filesystem=0 sql=0"
