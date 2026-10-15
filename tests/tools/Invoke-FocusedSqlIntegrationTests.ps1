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
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$previousApproval = $env:MYDMSVN_SQL_TEST_APPROVAL_FILE
$env:MYDMSVN_SQL_TEST_APPROVAL_FILE = $approval
try {
    Write-Warning 'Focused SQL categories are for development iteration only; they do not replace the unfiltered release gate.'
    & dotnet run --project $runnerProject -c Release --no-build -- focused-run `
        --repo $repository `
        --project $project `
        --artifacts $artifacts `
        --approval $approval `
        --category $Category `
        --frameworks ($Frameworks -join ',')
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    $env:MYDMSVN_SQL_TEST_APPROVAL_FILE = $previousApproval
}
