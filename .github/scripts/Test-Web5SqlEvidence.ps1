param([switch]$B03)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Assert-Web5SqlEvidence.ps1"
$root=Join-Path ([IO.Path]::GetTempPath()) ('web5-evidence-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$passed=0
try {
    $name='MyDmsVn.Server.Infrastructure.IntegrationTests.WebOperationCommandTests.Probe'
    function Case([string]$label,[string]$counts,[string]$results,[string[]]$discovery,[bool]$accept,[string]$outcome='Completed',[bool]$red=$false,[bool]$observed=$false) {
        [xml]$rowsXml='<Results>'+$results+'</Results>'
        $definitions=''
        foreach($row in @($rowsXml.Results.UnitTestResult)) {
            if (-not $row) { continue }
            if (-not $row.HasAttribute('testId')) { $row.SetAttribute('testId',[guid]::NewGuid().ToString('D')) }
            $row.SetAttribute('executionId',[guid]::NewGuid().ToString('D'))
            $definitions+='<UnitTest id="'+$row.testId+'"><Execution id="'+$row.executionId+'"/></UnitTest>'
        }
        [IO.File]::WriteAllText((Join-Path $root 'result.trx'), '<TestRun>'+ $rowsXml.OuterXml+'<TestDefinitions>'+$definitions+'</TestDefinitions><ResultSummary outcome="'+$outcome+'"><Counters '+$counts+'/></ResultSummary></TestRun>')
        [IO.File]::WriteAllLines((Join-Path $root 'discovery.log'), $discovery)
        $accepted=$true
        try { Assert-Web5SqlEvidence -TrxPath (Join-Path $root 'result.trx') -DiscoveryPath (Join-Path $root 'discovery.log') -RequiredClasses @('WebOperationCommandTests') -SummaryPath (Join-Path $root 'summary.json') -Label $label -RequireOracleRed:$red | Out-Null } catch { $accepted=$false }
        if ($accepted -ne $accept) { throw "Incorrect evidence decision: $label" }
        $summary=Get-Content (Join-Path $root 'summary.json') -Raw | ConvertFrom-Json
        if ($summary.oracleRedObserved -ne $observed) { throw "Incorrect RED recognition: $label" }
        $script:passed++
    }
    $good='<UnitTestResult testName="'+$name+'" outcome="Passed"/>'
    Case 'complete' 'total="1" executed="1" passed="1" failed="0" notExecuted="0"' $good @($name) $true
    Case 'zero' 'total="0" executed="0" passed="0" failed="0" notExecuted="0"' '' @() $false
    Case 'skip' 'total="1" executed="0" passed="0" failed="0" notExecuted="1"' ('<UnitTestResult testName="'+$name+'" outcome="NotExecuted"/>') @($name) $false
    Case 'false-counters' 'total="1" executed="1" passed="1" failed="0" notExecuted="0"' ('<UnitTestResult testName="'+$name+'" outcome="Failed"/>') @($name) $false
    Case 'missing-row' 'total="2" executed="2" passed="2" failed="0" notExecuted="0"' $good @($name) $false
    Case 'discovery-mismatch' 'total="1" executed="1" passed="1" failed="0" notExecuted="0"' $good @($name+'Other') $false
    Case 'truncated-theory-names' 'total="2" executed="2" passed="2" failed="0" notExecuted="0"' ($good+$good) @($name,$name) $true
    $duplicate='<UnitTestResult testId="11111111-1111-1111-1111-111111111111" testName="'+$name+'" outcome="Passed"/>'
    Case 'duplicate-case-id' 'total="2" executed="2" passed="2" failed="0" notExecuted="0"' ($duplicate+$duplicate) @($name,$name) $false
    Case 'missing-class' 'total="1" executed="1" passed="1" failed="0" notExecuted="0"' ('<UnitTestResult testName="MyDmsVn.Server.Infrastructure.IntegrationTests.WebMissingTests.Probe" outcome="Passed"/>') @('MyDmsVn.Server.Infrastructure.IntegrationTests.WebMissingTests.Probe') $false
    Case 'aborted' 'total="1" executed="1" passed="1" failed="0" notExecuted="0"' $good @($name) $false 'Aborted'
    Case 'nonzero-error-counter' 'total="1" executed="1" passed="1" failed="0" notExecuted="0" error="1"' $good @($name) $false
    $oracle='MyDmsVn.Server.Infrastructure.IntegrationTests.WebOperationCommandTests.Web_exact12_all_six_capture_original_results_and_replay_without_dynamic_work_or_Read'
    $failure='<UnitTestResult testName="'+$oracle+'" outcome="Failed"><Output><ErrorInfo><Message>Assert.Equal() Failure&#10;Expected: 25&#10;Actual:   24</Message></ErrorInfo></Output></UnitTestResult>'
    $failedCounts='total="1" executed="1" passed="0" failed="1" notExecuted="0"'
    Case 'known-oracle-red' $failedCounts $failure @($oracle) $false 'Failed' $true $true
    Case 'unrelated-red' $failedCounts ($failure.Replace('Assert.Equal() Failure','Connection failed')) @($oracle) $false 'Failed' $true $false
    Case 'wrong-value-red' $failedCounts ($failure.Replace('Expected: 25','Expected: 250')) @($oracle) $false 'Failed' $true $false
    $b02bWorkflow=Get-Content (Join-Path (Split-Path $PSScriptRoot -Parent) 'workflows/backend-web6-b02b-sql-candidate.yml') -Raw
    $safeLifecycleCheck='powershell -NoProfile -File ../control/.github/scripts/Web5SqlInstance.ps1 -Action Check'
    if (-not $b02bWorkflow.Contains($safeLifecycleCheck)) { throw 'B02b lifecycle Check must preserve JSON identity strings through Windows PowerShell.' }
    $passed++
    if ($B03) {
        $b03Path=Join-Path (Split-Path $PSScriptRoot -Parent) 'workflows/backend-web6-b03-sql-candidate.yml'
        if (-not (Test-Path -LiteralPath $b03Path)) { throw 'B03 protected workflow is required.' }
        $b03Workflow=Get-Content -LiteralPath $b03Path -Raw
        foreach ($required in @(
            "tags: ['web6-b03-sql-candidate-*']", "github.repository_id == '1409766698'", "github.actor == 'chung6a8m'",
            "github.triggering_actor == 'chung6a8m'", "github.event_name == 'push'", "environment: web5-sql-candidate",
            "startsWith(github.ref, 'refs/tags/web6-b03-sql-candidate-')", 'ref: ${{ github.sha }}',
            'dotnet restore $project --locked-mode', 'dotnet build $project -c Release -f net48 --no-restore',
            '--list-tests --filter $filter', '--no-build --no-restore --filter $filter',
            'WebOperationMaintenanceOperatorTests', 'WebOperationMaintenanceEvidenceTests', 'WebOperationReconciliationOperatorTests',
            'Web6MaintenanceRecoveryTests', 'WebOperationOperatorDecisionTests', 'WebOperationOperatorInspectTests',
            'Web5BackupSafetyTests', 'Web5BackupRestoreTests', 'WebRecoveryPilotResultTests',
            'Compact_execute_release_changes_only_result_storage_and_second_attempt_is_no_change',
            'Assert-Web5SqlEvidence', '$summary.discovered -ne $expected', '$summary.executed -ne $expected',
            '$summary.passed -ne $expected', '$summary.failed -ne 0', '$summary.skipped -ne 0',
            '-NotePropertyName retry -NotePropertyValue 0', 'git -C candidate status --porcelain --untracked-files=normal',
            $safeLifecycleCheck, '-Action Complete', '-Action Check', 'Web6B03EvidenceSandbox.ps1',
            'TEST_CLEANUP_ONLY', 'workflowSuccess=0', 'Publish sanitized evidence only', 'path: ${{ env.WEB6_SAFE }}/*.json',
            'if: ${{ always() }}', 'IsIntegratedSecurityOnly', 'primaryBackupPairsOwnedByFixtures', 'unexpectedChildren'
        )) {
            if (-not $b03Workflow.Contains($required)) { throw ('B03 workflow missing required guard: '+$required) }
            $passed++
        }
        if ([regex]::Matches($b03Workflow,'ref: \$\{\{ github.sha \}\}').Count -ne 2) { throw 'Both B03 checkouts must pin exact HEAD.' }
        if ($b03Workflow -match 'workflow_dispatch|continue-on-error|retry-action|upload[^\r\n]*trx') { throw 'B03 workflow permits an unreviewed entry, retry, or raw artifact.' }
        $passed++
        $sandbox=Get-Content (Join-Path $PSScriptRoot 'Web6B03EvidenceSandbox.ps1') -Raw
        foreach ($required in @('createdTicks','SetAccessRuleProtection($true,$false)','ReparsePoint','unexpectedChildren','primaryBackupPairsOwnedByFixtures','Remove-Web6SandboxExact $state','$state.phase=''Quarantined''')) {
            if (-not $sandbox.Contains($required)) { throw 'B03 sandbox ownership/cleanup guard missing.' }
            $passed++
        }
        if ($sandbox -match 'Remove-Item|Delete\([^\r\n]*\$true|Stop-Process') { throw 'B03 sandbox must not recursively delete or terminate owners.' }
        $passed++
        $expectedCounts=@{ WebOperationMaintenanceOperatorTests=89; WebOperationMaintenanceEvidenceTests=33;
            WebOperationReconciliationOperatorTests=21; Web6MaintenanceRecoveryTests=28; WebOperationOperatorDecisionTests=59;
            WebOperationOperatorInspectTests=40; WebOperationResolutionTests=24; WebOperationTerminalCrashTests=2;
            Web5BackupSafetyTests=2; WebRecoveryPilotResultTests=43; Web5BackupRestoreTests=1; WebOperationHttpSqlTests=2 }
        foreach ($class in $expectedCounts.Keys) {
            if ($b03Workflow -notmatch ('(?m)^            '+[regex]::Escape($class)+'='+$expectedCounts[$class]+'\r?$')) { throw 'B03 pinned discovery count changed.' }
            $passed++
        }
        foreach ($required in @('evidence.runsettings','MYDMSVN_WEB6_EXPECTED_EVIDENCE_TEMP','WEB6_CANDIDATE_ENV testhost=1 releaseEnvironment=1','-Action CheckEvidence','-Action SealTools','--settings "$env:WEB6_RAW/evidence.runsettings"')) {
            if (-not $b03Workflow.Contains($required)) { throw 'B03 testhost containment proof missing.' }
            $passed++
        }
        foreach ($required in @('tool-scratch','toolInventory','Verify-SealedTools','Open-Web6SandboxLease','Require-Drain','CheckEvidence')) {
            if (-not $sandbox.Contains($required)) { throw 'B03 exact tool inventory or evidence boundary missing.' }
            $passed++
        }
        foreach ($required in @('SQL-free exact net48 dependency preflight','Web6CandidateSafetyTests','projectedFullPathMaximum=240')) {
            if (-not $b03Workflow.Contains($required)) { throw 'B03 short-path preflight gate missing.' }
            $passed++
        }
        if ($b03Workflow.IndexOf('SQL-free exact net48 dependency preflight') -gt $b03Workflow.IndexOf('Allocate private SID-owned SQL instance')) { throw 'Preflight must precede SQL allocation.' }
        foreach ($required in @('Assert-Web6B03RunRoot','Get-Web6B03RunRoot','Fixed NTFS required','control\owner.json','Exact run root already exists','runTokenSha256','schemaVersion=3')) {
            if (-not $sandbox.Contains($required)) { throw 'B03 fixed-NTFS short root ownership missing.' }
            $passed++
        }
        $generator='$runToken=[guid]::NewGuid().ToString(''N'')'
        if ([regex]::Matches($b03Workflow,[regex]::Escape($generator)).Count -ne 1 -or
            -not $b03Workflow.Contains('"WEB6_RUN_TOKEN=$runToken" >> $env:GITHUB_ENV')) { throw 'B03 run token must be generated exactly once by the workflow.' }
        foreach ($line in @($b03Workflow -split "`n" | Where-Object { $_.Contains('Web6B03EvidenceSandbox.ps1 -Action') })) {
            if (-not $line.Contains('-RunToken $env:WEB6_RUN_TOKEN')) { throw 'B03 lifecycle must reuse the exact workflow run token.' }
        }
        if ($sandbox -match 'NewGuid|w6b0[0-9]|Verify-Parent|\$state\.parent') { throw 'B03 helper must not regenerate a token or adopt a shared parent.' }
        & "$PSScriptRoot/Test-Web6B03RunPath.ps1"
        & "$PSScriptRoot/Test-Web6B03CheckpointWorkflow.ps1" -MutationTests
        & "$PSScriptRoot/Test-Web6B03CheckpointChain.ps1"
        $passed++
        # No reviewed durable private Intent/Original destination and pre-SQL acknowledgment
        # exists yet. A reviewed integration must replace this blocker; markers cannot waive it.
        throw 'B03 durable private witness admission is not implemented; protected candidate remains blocked.'
    }
    Write-Output "Evidence guard checks passed: $passed"
}
finally {
    # One exact test-created temporary directory; no prefix enumeration/deletion.
    $resolved=[IO.Path]::GetFullPath($root)
    if ((Split-Path $resolved -Parent) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')) { throw 'Unexpected temporary path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
