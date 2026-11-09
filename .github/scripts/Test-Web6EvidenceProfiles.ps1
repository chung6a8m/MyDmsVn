# Runs only the shared synthetic-evidence guard and its static B03 profile.
$ErrorActionPreference='Stop'
function Guard([string]$Arguments) {
    $start=[Diagnostics.ProcessStartInfo]::new()
    $start.FileName=Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
    $start.Arguments='-NoProfile -ExecutionPolicy Bypass -File "'+(Join-Path $PSScriptRoot 'Test-Web5SqlEvidence.ps1')+'" '+$Arguments
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $process=[Diagnostics.Process]::Start($start)
    try{$output=$process.StandardOutput.ReadToEnd();$errorText=$process.StandardError.ReadToEnd();$process.WaitForExit();[pscustomobject]@{code=$process.ExitCode;text=$output+$errorText}}finally{$process.Dispose()}
}
$common=Guard ''
if($common.code -ne 0 -or -not $common.text.Contains('Evidence guard checks passed: 15')){throw 'Default common evidence guard must pass independently of B03 readiness.'}
$b03=Guard '-B03'
if($b03.code -eq 0 -or -not $b03.text.Contains('B03 short-path preflight gate missing.')){throw 'Incomplete B03 workflow must fail its explicit readiness profile.'}
$workflow=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../workflows/backend-web6-b03-sql-candidate.yml'))
if(-not $workflow.Contains('Test-Web5SqlEvidence.ps1 -B03')){throw 'B03 workflow must explicitly select B03 readiness.'}
foreach($name in @('backend-web5-sql-baseline.yml','backend-web5-sql-candidate.yml','backend-web6-b02a-sql-candidate.yml','backend-web6-b02b-sql-candidate.yml','backend-web6-b04-sql-candidate.yml')) {
    $path=Join-Path $PSScriptRoot ('../workflows/'+$name)
    if((Test-Path -LiteralPath $path) -and [IO.File]::ReadAllText($path).Contains('Test-Web5SqlEvidence.ps1 -B03')){throw 'Unrelated lane selected B03 readiness.'}
}
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Test-Web5SqlEvidence.ps1'),[ref]$tokens,[ref]$errors)
$gate=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.IfStatementAst] -and $n.Clauses[0].Item1.Extent.Text -ceq '$B03'},$true))
if($errors.Count -ne 0 -or $gate.Count -ne 1 -or
    @($gate[0].Clauses[0].Item2.Statements|Where-Object {$_ -is [Management.Automation.Language.ThrowStatementAst] -and $_.Extent.Text -ceq "throw 'B03 durable private witness admission is not implemented; protected candidate remains blocked.'"}).Count -ne 1){throw 'Unconditional B03 durability refusal must remain until reviewed integration.'}
'WEB6_EVIDENCE_PROFILES default=PASS commonChecks=15 b03=EXPECTED_REFUSAL workflowBinding=PASS sql=0 native=0'
