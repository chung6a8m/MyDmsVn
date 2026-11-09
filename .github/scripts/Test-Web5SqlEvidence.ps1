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
    Write-Output "Evidence guard checks passed: $passed"
}
finally {
    # One exact test-created temporary directory; no prefix enumeration/deletion.
    $resolved=[IO.Path]::GetFullPath($root)
    if ((Split-Path $resolved -Parent) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')) { throw 'Unexpected temporary path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
