[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputPath, [switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot 'MyDmsVn.Server.HttpHost.csproj'
if (-not $SkipBuild) {
    & dotnet restore $projectPath --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Host locked restore failed.' }
    & dotnet build $projectPath -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Host build failed.' }
}
$compiled = Join-Path $PSScriptRoot 'bin/Release/net48'
$assemblyPath = Join-Path $compiled 'MyDmsVn.Server.HttpHost.dll'
$redirectPath = Join-Path $compiled 'MyDmsVn.Server.HttpHost.dll.config'
if (-not (Test-Path -LiteralPath $assemblyPath) -or -not (Test-Path -LiteralPath $redirectPath)) { throw 'Build the current host before publishing.' }
$destination = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $destination) {
    if ((Get-ChildItem -LiteralPath $destination -Force | Measure-Object).Count -ne 0) { throw 'Publish requires an empty output directory.' }
}
New-Item -ItemType Directory -Path (Join-Path $destination 'bin') -Force | Out-Null
Get-ChildItem -LiteralPath $compiled -File | Where-Object { $_.Extension -eq '.dll' -or $_.Extension -eq '.config' } | Copy-Item -Destination (Join-Path $destination 'bin')
foreach ($architecture in @('x86','x64','runtimes')) {
    $nativeSource = Join-Path $compiled $architecture
    if (Test-Path -LiteralPath $nativeSource) { Copy-Item -LiteralPath $nativeSource -Destination (Join-Path $destination 'bin') -Recurse }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'wwwroot') -Destination $destination -Recurse
[xml]$web = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Web.config') -Raw
[xml]$redirects = Get-Content -LiteralPath $redirectPath -Raw
$generatedRuntime = $redirects.SelectSingleNode('/configuration/runtime')
if ($generatedRuntime) {
    $runtime = $web.ImportNode($generatedRuntime, $true)
    [void]$web.DocumentElement.ReplaceChild($runtime, $web.SelectSingleNode('/configuration/runtime'))
}
$settings = [Xml.XmlWriterSettings]::new()
$settings.Encoding = [Text.UTF8Encoding]::new($false)
$settings.Indent = $true
$settings.NewLineChars = "`r`n"
$writer = [Xml.XmlWriter]::Create((Join-Path $destination 'Web.config'), $settings)
try { $web.Save($writer) } finally { $writer.Dispose() }
Write-Output ('Published host: ' + $destination)
