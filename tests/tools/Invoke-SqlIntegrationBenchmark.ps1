[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string] $TestProject,

    [Parameter(Mandatory = $true)]
    [string] $ArtifactRoot,

    [Parameter(Mandatory = $true)]
    [string] $ApprovalFile
)

$runnerProject = Join-Path $PSScriptRoot 'MyDmsVn.SqlTestRunner\MyDmsVn.SqlTestRunner.csproj'
& dotnet run --project $runnerProject -c Release --no-build -- `
    baseline `
    --repo $RepositoryRoot `
    --project $TestProject `
    --artifacts $ArtifactRoot `
    --approval $ApprovalFile
exit $LASTEXITCODE
