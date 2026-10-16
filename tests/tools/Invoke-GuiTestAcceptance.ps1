[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Get-Location).Path,

    [Parameter(Mandatory = $true)]
    [string] $TestProject,

    [Parameter(Mandatory = $true)]
    [string] $ArtifactRoot,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string] $Revision,

    [ValidateRange(1, 900)]
    [int] $TimeoutSeconds = 900
)

$workingTree = & git -C $RepositoryRoot status --porcelain --untracked-files=all
if ($LASTEXITCODE -ne 0 -or $workingTree) {
    throw 'GUI D2 acceptance requires a clean, committed working tree.'
}

[string] $head = & git -C $RepositoryRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $head -ne $Revision) {
    throw 'GUI D2 acceptance revision does not match HEAD.'
}

$runnerProject = Join-Path $PSScriptRoot 'MyDmsVn.SqlTestRunner\MyDmsVn.SqlTestRunner.csproj'
& dotnet run --project $runnerProject -c Release -f net8.0 --no-build -- `
    gui-acceptance `
    --repo $RepositoryRoot `
    --project $TestProject `
    --artifacts $ArtifactRoot `
    --framework net48 `
    --revision $Revision `
    --timeout-seconds $TimeoutSeconds
exit $LASTEXITCODE
