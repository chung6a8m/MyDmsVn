[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Get-Location).Path,

    [string] $ManifestPath = 'tests/test-gates.v1.json',

    [Parameter(Mandatory = $true)]
    [ValidateSet('Task', 'Final')]
    [string] $Mode,

    [Parameter(Mandatory = $true)]
    [string] $PlanId,

    [Parameter(Mandatory = $true)]
    [string] $TaskId,

    [Parameter(Mandatory = $true)]
    [string] $ArtifactRoot,

    [string] $ApprovalFile
)

$Revision = $null
if ($Mode -eq 'Final') {
    if (-not $ApprovalFile) {
        throw 'Final test gate requires an approved SQL environment file.'
    }

    $workingTree = & git -C $RepositoryRoot status --porcelain
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to inspect the repository working tree.'
    }

    if ($workingTree) {
        throw 'Final test gate requires a clean, committed working tree.'
    }

    [string] $Revision = & git -C $RepositoryRoot rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $Revision -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'Unable to resolve the exact commit under test.'
    }
}

$runnerProject = Join-Path $PSScriptRoot 'MyDmsVn.SqlTestRunner\MyDmsVn.SqlTestRunner.csproj'
$arguments = @(
    'run', '--project', $runnerProject, '-c', 'Release', '--no-build', '--',
    'test-gate', '--repo', $RepositoryRoot, '--manifest', $ManifestPath,
    '--mode', $Mode, '--plan', $PlanId, '--task', $TaskId, '--artifacts', $ArtifactRoot
)
if ($ApprovalFile) {
    $arguments += @('--approval', $ApprovalFile)
}
if ($Revision) {
    $arguments += @('--revision', $Revision)
}

& dotnet @arguments
exit $LASTEXITCODE
