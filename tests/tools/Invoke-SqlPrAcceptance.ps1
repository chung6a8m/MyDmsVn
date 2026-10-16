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
    [ValidateNotNullOrEmpty()]
    [string[]] $Frameworks
)

$workingTree = & git -C $RepositoryRoot status --porcelain
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect the repository working tree.'
}

if ($workingTree) {
    throw 'PR acceptance requires a clean, committed working tree.'
}

[string] $revision = & git -C $RepositoryRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $revision -notmatch '^[0-9a-fA-F]{40}$') {
    throw 'Unable to resolve the exact commit under test.'
}

$runnerProject = Join-Path $PSScriptRoot 'MyDmsVn.SqlTestRunner\MyDmsVn.SqlTestRunner.csproj'
& dotnet run --project $runnerProject -c Release --no-build -- `
    pr-acceptance `
    --repo $RepositoryRoot `
    --project $TestProject `
    --artifacts $ArtifactRoot `
    --approval $ApprovalFile `
    --revision $revision `
    --frameworks ($Frameworks -join ',')
exit $LASTEXITCODE
