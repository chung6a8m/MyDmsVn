[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string] $TestProject,

    [Parameter(Mandatory = $true)]
    [string] $ArtifactRoot,

    [Parameter(Mandatory = $true)]
    [string] $ApprovalFile,

    [Parameter(Mandatory = $true)]
    [ValidateSet('FastSQL', 'MigrationSchema', 'SecurityProvisioning', 'SqlConcurrency')]
    [string] $Category,

    [ValidateSet('net48', 'net8.0')]
    [string[]] $Frameworks = @('net48', 'net8.0')
)

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path $RepositoryRoot).Path
$project = (Resolve-Path $TestProject).Path
$artifacts = [IO.Path]::GetFullPath($ArtifactRoot)
$approval = (Resolve-Path $ApprovalFile).Path
$runnerProject = Join-Path $PSScriptRoot 'MyDmsVn.SqlTestRunner\MyDmsVn.SqlTestRunner.csproj'
$manifest = Join-Path $PSScriptRoot 'sql-test-nonsql-exemptions.json'
$assemblyName = [IO.Path]::GetFileNameWithoutExtension($project) + '.dll'

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$previousApproval = $env:MYDMSVN_SQL_TEST_APPROVAL_FILE
$env:MYDMSVN_SQL_TEST_APPROVAL_FILE = $approval
try {
    & dotnet run --project $runnerProject -c Release --no-build -- preflight
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    Write-Warning 'Focused SQL categories are for development iteration only; they do not replace the unfiltered release gate.'
    foreach ($tfm in $Frameworks) {
        $runId = 'focused-' + $Category.ToLowerInvariant() + '-' + $tfm.Replace('.', '-') + '-' + [Guid]::NewGuid().ToString('N')
        $runRoot = Join-Path $artifacts $runId
        $journal = Join-Path $runRoot 'ownership'
        $discovery = Join-Path $runRoot 'discovery.txt'
        $trxName = $runId + '.trx'
        $trx = Join-Path $runRoot $trxName
        $timing = Join-Path $runRoot 'timing.jsonl'
        $recovery = Join-Path $runRoot 'ownership-recovery.json'
        New-Item -ItemType Directory -Path $journal -Force | Out-Null

        & dotnet test $project -c Release -f $tfm --no-build --no-restore `
            --list-tests --filter "Category=$Category" 2>&1 | Tee-Object -FilePath $discovery
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }

        $env:MYDMSVN_SQL_TEST_RUN_ID = $runId
        $env:MYDMSVN_SQL_TEST_JOURNAL_DIRECTORY = $journal
        $env:MYDMSVN_SQL_TEST_TIMINGS = '1'
        $env:MYDMSVN_SQL_TEST_TIMING_OUTPUT = $timing
        & dotnet test $project -c Release -f $tfm --no-build --no-restore `
            --filter "Category=$Category" `
            --logger "trx;LogFileName=$trxName" `
            --results-directory $runRoot
        $testExit = $LASTEXITCODE

        $assembly = Join-Path (Join-Path (Join-Path ([IO.Path]::GetDirectoryName($project)) 'bin\Release') $tfm) $assemblyName
        & dotnet run --project $runnerProject -c Release --no-build -- focused-verify `
            --assembly $assembly `
            --manifest $manifest `
            --category $Category `
            --discovery $discovery `
            --trx $trx `
            --tfm $tfm `
            --exit-code $testExit `
            --run-id $runId `
            --journal $journal `
            --recovery $recovery `
            --approval $approval
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }
}
finally {
    $env:MYDMSVN_SQL_TEST_APPROVAL_FILE = $previousApproval
    Remove-Item Env:MYDMSVN_SQL_TEST_RUN_ID -ErrorAction SilentlyContinue
    Remove-Item Env:MYDMSVN_SQL_TEST_JOURNAL_DIRECTORY -ErrorAction SilentlyContinue
    Remove-Item Env:MYDMSVN_SQL_TEST_TIMINGS -ErrorAction SilentlyContinue
    Remove-Item Env:MYDMSVN_SQL_TEST_TIMING_OUTPUT -ErrorAction SilentlyContinue
}
