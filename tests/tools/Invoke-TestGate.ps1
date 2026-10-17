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

    [string] $ApprovalFile,

    [switch] $PreflightOnly
)

$Revision = $null
if ($PreflightOnly -and $Mode -ne 'Final') {
    throw 'Manifest preflight is supported for reviewed Final SQL gates only.'
}

if ($Mode -eq 'Final' -and -not $PreflightOnly) {
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
$runnerProjectDirectory = Split-Path -Parent $runnerProject
$runnerOutput = Join-Path $runnerProjectDirectory 'bin\Release\net8.0'
$artifactRootFull = if ([IO.Path]::IsPathRooted($ArtifactRoot)) {
    [IO.Path]::GetFullPath($ArtifactRoot)
} else {
    [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $ArtifactRoot))
}
$shadowRoot = Join-Path ([IO.Path]::GetTempPath()) (Join-Path 'MyDmsVn\TestGate' ('runner-' + $PID.ToString('D10')))
$runnerDll = Join-Path $shadowRoot 'MyDmsVn.SqlTestRunner.dll'
if ($runnerDll.Length -gt 240) {
    throw 'The runner shadow path exceeds the Windows compatibility budget.'
}

if ($Mode -eq 'Final' -and -not $PreflightOnly) {
    $runnerTestsProject = Join-Path $PSScriptRoot 'MyDmsVn.SqlTestRunner.Tests\MyDmsVn.SqlTestRunner.Tests.csproj'
    & dotnet msbuild $runnerTestsProject -target:BuildRepositoryInventoryFixtures -property:Configuration=Release
    if ($LASTEXITCODE -ne 0) {
        exit 3
    }
}

& dotnet build $runnerProject -c Release -f net8.0 --no-restore
if ($LASTEXITCODE -ne 0) {
    exit 3
}

New-Item -ItemType Directory -Path $shadowRoot -Force | Out-Null
$overBudgetShadowFiles = Get-ChildItem -LiteralPath $runnerOutput -File -Recurse | Where-Object {
    $relativePath = $_.FullName.Substring($runnerOutput.Length).TrimStart('\')
    (Join-Path $shadowRoot $relativePath).Length -gt 240
}
if ($overBudgetShadowFiles) {
    throw 'A runner shadow dependency path exceeds the Windows compatibility budget.'
}

Get-ChildItem -LiteralPath $runnerOutput | Copy-Item -Destination $shadowRoot -Recurse -Force
if (-not (Test-Path -LiteralPath $runnerDll -PathType Leaf)) {
    exit 3
}

$arguments = @(
    $runnerDll,
    $(if ($PreflightOnly) { 'test-gate-preflight' } else { 'test-gate' }), '--repo', $RepositoryRoot, '--manifest', $ManifestPath,
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
