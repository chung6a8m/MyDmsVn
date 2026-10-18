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

# Use the policy shipped in this admin package, rather than a second schema version pin.
$schemaPolicyAssemblyPath = Join-Path $adminDirectory 'MyDmsVn.Server.Infrastructure.dll'
if (-not (Test-Path -LiteralPath $schemaPolicyAssemblyPath -PathType Leaf)) {
    throw 'Release.SchemaPolicyMissing: admin package must contain the runtime schema policy assembly.'
}
# Published assemblies require x86, including when the publishing shell is 64-bit.
$schemaReaderExecutable = if ([Environment]::Is64BitOperatingSystem) {
    Join-Path $env:WINDIR 'SysWOW64/WindowsPowerShell/v1.0/powershell.exe'
} else {
    Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe'
}
if (-not (Test-Path -LiteralPath $schemaReaderExecutable -PathType Leaf)) {
    throw 'Release.SchemaPolicyReaderMissing: x86 Windows PowerShell is required for the net48 package.'
}
$schemaReaderScript = @'
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$assembly = [System.Reflection.Assembly]::LoadFrom('__POLICY_PATH__')
$policy = $assembly.GetType('MyDmsVn.Server.Infrastructure.Operations.SchemaCompatibilityPolicy', $true)
[pscustomobject]@{ bitness = [IntPtr]::Size * 8; scripts = @($policy.GetField('CurrentScripts').GetValue($null)) } | ConvertTo-Json -Compress
'@
$schemaReaderScript = $schemaReaderScript.Replace('__POLICY_PATH__', $schemaPolicyAssemblyPath.Replace("'", "''"))
$schemaReaderEncoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($schemaReaderScript))
$schemaReader = New-Object System.Diagnostics.Process
try {
    $schemaReader.StartInfo.FileName = $schemaReaderExecutable
    $schemaReader.StartInfo.Arguments = '-NoProfile -NonInteractive -EncodedCommand ' + $schemaReaderEncoded
    $schemaReader.StartInfo.UseShellExecute = $false
    $schemaReader.StartInfo.CreateNoWindow = $true
    $schemaReader.StartInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    $schemaReader.StartInfo.RedirectStandardOutput = $true
    $schemaReader.StartInfo.RedirectStandardError = $true
    [void] $schemaReader.Start()
    $schemaOutput = $schemaReader.StandardOutput.ReadToEndAsync()
    $schemaError = $schemaReader.StandardError.ReadToEndAsync()
    if (-not $schemaReader.WaitForExit(30000)) {
        $schemaReader.Kill()
        [void] $schemaReader.WaitForExit(10000)
        throw 'Release.SchemaPolicyReadFailed: policy reader exceeded its time bound.'
    }
    $schemaOutputText = $schemaOutput.GetAwaiter().GetResult()
    $schemaErrorText = $schemaError.GetAwaiter().GetResult()
    if ($schemaReader.ExitCode -ne 0 -or -not [string]::IsNullOrWhiteSpace($schemaErrorText)) {
        throw 'Release.SchemaPolicyReadFailed: cannot read the policy from the shipped admin assembly.'
    }
    $schemaPolicy = $schemaOutputText | ConvertFrom-Json
    if ($schemaPolicy.bitness -ne 32) {
        throw 'Release.SchemaPolicyReadFailed: policy reader must run as x86.'
    }
    $requiredSchemaScripts = @($schemaPolicy.scripts)
}
finally {
    $schemaReader.Dispose()
}
if ($requiredSchemaScripts.Count -eq 0) {
    throw 'Release.SchemaPolicyMissing: runtime schema policy has no required scripts.'
}

$manifest = [ordered]@{
    product = 'MyDmsVn Local v1'
    productVersion = $Version
    sourceCommit = $commit
    sourceTreeDirty = $sourceTreeDirty
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    schemaSupport = [ordered]@{
        minimum = $requiredSchemaScripts.Count
        maximum = $requiredSchemaScripts.Count
        requiredScripts = $requiredSchemaScripts
        compatibilityPolicy = 'Exact runtime schema from SchemaCompatibilityPolicy.CurrentScripts'
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
