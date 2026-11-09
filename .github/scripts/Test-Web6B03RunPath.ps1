$ErrorActionPreference='Stop'
. "$PSScriptRoot/Web6B03RunPath.ps1"
$passed=0
$token='0123456789abcdef0123456789abcdef'
$expected='C:\w6b-'+$token
if ((Get-Web6B03RunRoot 'C:' $token) -cne $expected) { throw 'Canonical root construction failed.' }
Assert-Web6B03RunRoot $expected 'C:' $token
$passed++
function Refused([scriptblock]$action) {
    $refused=$false
    try { & $action | Out-Null } catch { $refused=$true }
    if (-not $refused) { throw 'Noncanonical run identity was accepted.' }
    $script:passed++
}
foreach ($invalid in @($token.ToUpperInvariant(),$token.Substring(1),($token+'0'),('g'+$token.Substring(1)),'',$null,($token+"`n"),($token+' '),('../'+$token))) {
    Refused { Get-Web6B03RunRoot 'C:' $invalid }
}
foreach ($drive in @('c:','C:\','C:.','C: ','\\server\share','\\?\C:','C:/','relative','')) {
    Refused { Get-Web6B03RunRoot $drive $token }
}
foreach ($path in @(
    ($expected+'\'),($expected+'.'),($expected+' '),($expected+"`n"),
    ('C:\w6b-'+$token.ToUpperInvariant()),('C:\w6b-'+$token.Substring(1)),('C:\w6b-'+$token+'0'),
    ('C:\W6B-'+$token),('c:\w6b-'+$token),('D:\w6b-'+$token),('C:/w6b-'+$token),
    ('w6b-'+$token),('C:w6b-'+$token),('C:\parent\..\w6b-'+$token),($expected+'\..'),
    ('\\server\share\w6b-'+$token),('\\?\'+$expected),('\\.\'+$expected),''
)) { Refused { Assert-Web6B03RunRoot $path 'C:' $token } }
$hash=Get-Web6B03RunTokenHash $token
if ($hash -cnotmatch '\A[0-9a-f]{64}\z' -or
    $hash -cne '3eb1bd439947eb762998e566ccc2e099c791118b2f40579cc4f7da2b5061b7f9' -or
    $hash -cne (Get-Web6B03RunTokenHash $token) -or
    $hash -ceq (Get-Web6B03RunTokenHash '1123456789abcdef0123456789abcdef') -or $hash.Contains($token)) { throw 'Sanitized token hash contract failed.' }
$passed++
Refused { Get-Web6B03RunTokenHash $token.ToUpperInvariant() }
Write-Output "WEB6_RUN_PATH checks=$passed filesystem=0 sql=0"
