[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$')]
    [string] $Version,

    [string] $OutputRoot,

    [switch] $SkipPublish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot (Join-Path 'artifacts\release' $Version)
}

$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$net48Directory = Join-Path $OutputRoot 'desktop-net48-win-x86'
$adminDirectory = Join-Path $OutputRoot 'admin-migrator-net48-win-x86'
$manifestPath = Join-Path $OutputRoot 'release-manifest.json'

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]] $Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Release.BuildFailed: dotnet $($Arguments -join ' ') exited with $LASTEXITCODE."
    }
}

function Assert-SafeOutputRoot {
    $root = [System.IO.Path]::GetPathRoot($OutputRoot)
    if ($OutputRoot -eq $root -or $OutputRoot -eq $repositoryRoot) {
        throw 'Release.OutputRootRejected: output must be a dedicated release directory.'
    }
}

function Assert-DesktopArtifactSafe {
    param([Parameter(Mandatory = $true)][string] $Directory)

    $adminArtifacts = Get-ChildItem -LiteralPath $Directory -Recurse -File | Where-Object {
        $_.Name -match '(?i)DbMigrator' -or $_.Extension -eq '.sql'
    }
    if ($adminArtifacts) {
        throw "Release.ArtifactRejected: desktop package contains migration/admin artifact '$($adminArtifacts[0].Name)'."
    }

    $textExtensions = @('.config', '.json', '.xml', '.txt', '.ini', '.yml', '.yaml', '.ps1', '.cmd', '.bat')
    foreach ($file in Get-ChildItem -LiteralPath $Directory -Recurse -File) {
        if ($textExtensions -notcontains $file.Extension.ToLowerInvariant()) {
            continue
        }

        $content = [System.IO.File]::ReadAllText($file.FullName)
        if ($content -match '(?i)(password|pwd|user\s+id|uid|auth(?:entication)?token|access\s+token|authorization)\s*[:=]' -or
            $content -match '(?i)(server|data\s+source)\s*=.+;(database|initial\s+catalog)\s*=') {
            throw "Release.ArtifactRejected: desktop package contains a connection string or credential marker in '$($file.Name)'."
        }
    }
}

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string] $Path)

    $stream = [System.IO.File]::OpenRead($Path)
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $algorithm.ComputeHash($stream)
        return ([System.BitConverter]::ToString($hash)).Replace('-', '')
    }
    finally {
        $algorithm.Dispose()
        $stream.Dispose()
    }
}

function New-PackageManifest {
    param(
        [Parameter(Mandatory = $true)][string] $Name,
        [Parameter(Mandatory = $true)][string] $TargetFramework,
        [Parameter(Mandatory = $true)][string] $Architecture,
        [Parameter(Mandatory = $true)][string] $DeploymentMode,
        [Parameter(Mandatory = $true)][string] $RequiredRuntime,
        [Parameter(Mandatory = $true)][string] $Directory
    )

    $files = @(
        Get-ChildItem -LiteralPath $Directory -Recurse -File |
            Sort-Object FullName |
            ForEach-Object {
                $relativePath = $_.FullName.Substring($Directory.Length).TrimStart('\', '/')
                [ordered]@{
                    path = $relativePath.Replace('\', '/')
                    length = $_.Length
                    sha256 = Get-Sha256 $_.FullName
                }
            }
    )

    return [ordered]@{
        name = $Name
        targetFramework = $TargetFramework
        architecture = $Architecture
        deploymentMode = $DeploymentMode
        requiredRuntime = $RequiredRuntime
        path = [System.IO.Path]::GetFileName($Directory)
        files = $files
    }
}

Assert-SafeOutputRoot
$commit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Release.GitIdentityUnavailable: cannot resolve the source commit.'
}
$sourceStatus = @(& git -C $repositoryRoot status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) {
    throw 'Release.GitStatusUnavailable: cannot determine whether the source tree is clean.'
}
$sourceTreeDirty = $sourceStatus.Count -gt 0

if (-not $SkipPublish) {
    if ($sourceTreeDirty) {
        throw 'Release.SourceTreeDirty: commit or remove all source changes before publishing release artifacts.'
    }

    if (Test-Path -LiteralPath $OutputRoot) {
        $existingOutput = @(Get-ChildItem -LiteralPath $OutputRoot -Force)
        if ($existingOutput.Count -gt 0) {
            throw "Release.OutputNotEmpty: refusing to overwrite '$OutputRoot'. Choose a new empty release directory."
        }
    }

    New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
    Push-Location $repositoryRoot
    try {
        Invoke-DotNet @('restore', 'MyDmsVn.sln', '--locked-mode', '--nologo', '-p:RestoreDisableParallel=true')
        Invoke-DotNet @(
            'publish', 'src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj',
            '-c', 'Release', '-f', 'net48', '--no-restore', '--nologo',
            '-p:PlatformTarget=x86', "-p:Version=$Version", '-o', $net48Directory)
        Invoke-DotNet @(
            'publish', 'src/MyDmsVn.Server.DbMigrator/MyDmsVn.Server.DbMigrator.csproj',
            '-c', 'Release', '-f', 'net48', '--no-restore', '--nologo',
            '-p:PlatformTarget=x86', "-p:Version=$Version", '-o', $adminDirectory)
    }
    finally {
        Pop-Location
    }
}
elseif (Test-Path -LiteralPath $manifestPath) {
    Remove-Item -LiteralPath $manifestPath -Force
}

foreach ($directory in @($net48Directory, $adminDirectory)) {
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        throw "Release.PackageMissing: expected directory '$directory'."
    }
}

Assert-DesktopArtifactSafe $net48Directory

$manifest = [ordered]@{
    product = 'MyDmsVn Local v1'
    productVersion = $Version
    sourceCommit = $commit
    sourceTreeDirty = $sourceTreeDirty
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    schemaSupport = [ordered]@{
        minimum = 8
        maximum = 8
        compatibilityPolicy = 'Exact schema 8 for the P3.1 release baseline'
    }
    sqlCompatibility = [ordered]@{
        validatedVersions = @()
        status = 'Pending P6-T06 operational validation'
    }
    signing = [ordered]@{
        required = $false
        status = 'Unsigned release acceptance requires owner approval'
    }
    packages = @(
        (New-PackageManifest 'Desktop .NET Framework 4.8' 'net48' 'win-x86' 'publish-directory' '.NET Framework 4.8' $net48Directory),
        (New-PackageManifest 'Admin migrator' 'net48' 'win-x86' 'publish-directory-admin-only' '.NET Framework 4.8' $adminDirectory)
    )
}

$json = $manifest | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText(
    $manifestPath,
    $json + [Environment]::NewLine,
    (New-Object System.Text.UTF8Encoding($false)))
Write-Output "Release manifest: $manifestPath"
