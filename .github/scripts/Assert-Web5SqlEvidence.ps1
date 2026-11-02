param([string]$TrxPath, [string]$DiscoveryPath, [string[]]$RequiredClasses, [string]$SummaryPath, [string]$Label, [switch]$RequireOracleRed)
$ErrorActionPreference = 'Stop'

function Assert-Web5SqlEvidence {
    param([string]$TrxPath, [string]$DiscoveryPath, [string[]]$RequiredClasses, [string]$SummaryPath, [string]$Label, [switch]$RequireOracleRed)
    $summary = [ordered]@{ label=$Label; discovered=0; uniqueCases=0; total=0; executed=0; passed=0; failed=0; skipped=0; requiredClasses=@{}; oracleRedObserved=$false; complete=$false }
    try {
        if (-not $RequiredClasses -or -not (Test-Path -LiteralPath $TrxPath) -or -not (Test-Path -LiteralPath $DiscoveryPath)) { throw 'Missing evidence.' }
        $discovered = @(Get-Content -LiteralPath $DiscoveryPath | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^MyDmsVn\.Server\.Infrastructure\.IntegrationTests\.' })
        if ($discovered.Count -eq 0) { throw 'Empty discovery.' }
        $summary.discovered = $discovered.Count
        $xml = [xml]::new(); $xml.XmlResolver = $null
        $xml.Load($TrxPath)
        $counts = $xml.TestRun.ResultSummary.Counters
        $rows = @($xml.TestRun.Results.UnitTestResult)
        foreach ($field in @('total','executed','passed','failed')) { $summary[$field] = [int]$counts.$field }
        $summary.skipped = [int]$counts.notExecuted
        $names = @($rows | ForEach-Object { [string]$_.testName })
        # xUnit truncates theory arguments: distinct cases may share a display name.
        # Reconcile the name multiset, but use TRX case/execution IDs for uniqueness.
        $caseIds=@($rows | ForEach-Object { [string]$_.testId })
        $executionIds=@($rows | ForEach-Object { [string]$_.executionId })
        $definitions=@($xml.TestRun.TestDefinitions.UnitTest)
        $definitionIds=@($definitions | ForEach-Object { [string]$_.id })
        $summary.uniqueCases=@($caseIds | Sort-Object -Unique).Count
        $guidPattern='^[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}$'
        $idsValid=@(($caseIds+$executionIds+$definitionIds) | Where-Object { $_ -notmatch $guidPattern }).Count -eq 0 -and
            $summary.uniqueCases -eq $rows.Count -and @($executionIds|Sort-Object -Unique).Count -eq $rows.Count -and
            $definitionIds.Count -eq $rows.Count -and @(Compare-Object $caseIds $definitionIds).Count -eq 0
        foreach($row in $rows) {
            $definition=@($definitions | Where-Object { $_.id -eq $row.testId })
            if($definition.Count -ne 1 -or $definition[0].Execution.id -ne $row.executionId) { $idsValid=$false }
        }
        $sameDiscovery = $names.Count -eq $discovered.Count -and @(Compare-Object $discovered $names).Count -eq 0
        foreach ($class in $RequiredClasses) {
            if ($class -notmatch '^Web[A-Za-z0-9]+Tests$') { throw 'Invalid required class.' }
            $summary.requiredClasses[$class] = @($names | Where-Object { $_ -match ('\.'+[regex]::Escape($class)+'\.') }).Count
        }
        $allClasses = @($summary.requiredClasses.Values | Where-Object { $_ -le 0 }).Count -eq 0
        $allowed = @($rows | Where-Object { $name=[string]$_.testName; @($RequiredClasses | Where-Object {$name -match ('\.'+[regex]::Escape($_)+'\.')}).Count -ne 1 }).Count -eq 0
        $badCounters = @('error','timeout','aborted','inconclusive','notRunnable','disconnected','warning','inProgress','pending') | Where-Object { [int]$counts.$_ -ne 0 }
        $reconciled = @($badCounters).Count -eq 0 -and $summary.total -gt 0 -and $summary.total -eq $summary.executed -and $summary.total -eq $rows.Count -and $summary.skipped -eq 0 -and $sameDiscovery -and $idsValid -and $allClasses -and $allowed
        if ($RequireOracleRed) {
            $failed = @($rows | Where-Object outcome -eq 'Failed')
            $message = if ($failed.Count -eq 1) { [string]$failed[0].Output.ErrorInfo.Message } else { '' }
            $summary.oracleRedObserved = $reconciled -and $summary.total -eq 1 -and $summary.failed -eq 1 -and $summary.passed -eq 0 -and
                $names[0] -eq 'MyDmsVn.Server.Infrastructure.IntegrationTests.WebOperationCommandTests.Web_exact12_all_six_capture_original_results_and_replay_without_dynamic_work_or_Read' -and
                $message -match 'Assert.Equal' -and $message -match '(?m)^\s*Expected:\s+25(?:\.0+)?\s*$' -and $message -match '(?m)^\s*Actual:\s+24(?:\.0+)?\s*$'
            # RED is always a failed gate, including the expected oracle failure.
            throw 'The deliberate RED challenge must block the gate.'
        }
        $summary.complete = $reconciled -and $summary.passed -eq $summary.total -and $summary.failed -eq 0 -and
            @($rows | Where-Object outcome -ne 'Passed').Count -eq 0 -and $xml.TestRun.ResultSummary.outcome -eq 'Completed'
        if (-not $summary.complete) { throw 'Incomplete SQL execution.' }
    }
    catch { $summary.complete=$false }
    finally {
        # Only bounded counts/class identifiers leave the runner. Never serialize error/body/TRX data.
        $json = $summary | ConvertTo-Json -Depth 5 -Compress
        [IO.File]::WriteAllText($SummaryPath, $json+[Environment]::NewLine, [Text.UTF8Encoding]::new($false))
        Write-Output $json
    }
    if (-not $summary.complete) { throw 'Required SQL evidence is absent, mismatched, skipped, failed or deliberately RED.' }
}

if ($MyInvocation.InvocationName -ne '.') { Assert-Web5SqlEvidence @PSBoundParameters }
