# Pure canonical path/token functions. No filesystem or environment mutation.
function Get-Web6B03RunRoot([string]$SystemDrive,[string]$RunToken) {
    if ($SystemDrive -cnotmatch '\A[A-Z]:\z' -or $RunToken -cnotmatch '\A[0-9a-f]{32}\z') {
        throw 'Canonical system drive and lowercase run token are required.'
    }
    $SystemDrive+'\w6b-'+$RunToken
}
function Assert-Web6B03RunRoot([string]$Root,[string]$SystemDrive,[string]$RunToken) {
    $expected=Get-Web6B03RunRoot $SystemDrive $RunToken
    # Exact text admission deliberately precedes any path normalization/filesystem call.
    if ($Root -cne $expected) { throw 'Run root does not match its exact canonical token.' }
}
function Get-Web6B03RunTokenHash([string]$RunToken) {
    if ($RunToken -cnotmatch '\A[0-9a-f]{32}\z') { throw 'Canonical run token is required.' }
    $sha=[Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($RunToken)))).Replace('-','').ToLowerInvariant() }
    finally { $sha.Dispose() }
}
