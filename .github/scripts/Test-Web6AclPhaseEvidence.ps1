$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Read-Web6AclPhaseEvidence.ps1"
$script:checks = 0
$exact = 'MyDmsVn.Server.Infrastructure.IntegrationTests.Web6CandidateSafetyTests.Manifest_refuses_acl_drift_and_retains_until_original_acl_is_restored'
$class = 'MyDmsVn.Server.Infrastructure.IntegrationTests.Web6CandidateSafetyTests'
$method = 'Manifest_refuses_acl_drift_and_retains_until_original_acl_is_restored'
$marker = 'WEB6_ACL phase=ParentPinOpen result=Failed reason=OperationFailed'
function New-MemoryTrx([string]$output, [string]$globalOutput='', [string]$name=$exact, [string]$definitionClass=$class, [string]$execution='e1') {
    return "<TestRun xmlns='http://microsoft.com/schemas/VisualStudio/TeamTest/2010'><Results><UnitTestResult testId='t1' executionId='$execution' testName='$name'><Output><StdOut>$output</StdOut></Output></UnitTestResult></Results><TestDefinitions><UnitTest id='t1'><Execution id='e1'/><TestMethod className='$definitionClass' name='$method'/></UnitTest></TestDefinitions><ResultSummary><Output><StdOut>$globalOutput</StdOut></Output></ResultSummary></TestRun>"
}
function Refuse([string]$xml) {
    $failed=$false
    try { Read-Web6AclPhaseEvidence -TrxXml $xml | Out-Null } catch { $failed=$true }
    if(-not $failed){throw 'Memory parser accepted ambiguous evidence.'}
    $script:checks++
}
$accepted = @(Read-Web6AclPhaseEvidence -TrxXml (New-MemoryTrx $marker $marker))
if($accepted.Count -ne 1 -or $accepted[0] -cne $marker){throw 'Exact result-scoped marker was not selected.'}
$script:checks++
$terminal = $marker -replace 'OperationFailed','TerminalFailure'
$acceptedTerminal = @(Read-Web6AclPhaseEvidence -TrxXml (New-MemoryTrx $terminal))
if($acceptedTerminal.Count -ne 1 -or $acceptedTerminal[0] -cne $terminal){throw 'Closed terminal reason was not selected.'}
$script:checks++
foreach($reason in @('EnumerationFailed','UnexpectedMembership','ParentIdentityMismatch','InvalidParentBinding')) {
    $closed = 'WEB6_ACL phase=ParentMembership result=Failed reason='+$reason
    $acceptedClosed = @(Read-Web6AclPhaseEvidence -TrxXml (New-MemoryTrx $closed $closed))
    if($acceptedClosed.Count -ne 1 -or $acceptedClosed[0] -cne $closed){throw 'Closed parent reason was not selected.'}
    $script:checks++
    Refuse (New-MemoryTrx '' $closed)
    Refuse (New-MemoryTrx $closed '' 'Other.Class.Test')
    Refuse (New-MemoryTrx ($closed+"`n"+$closed))
}
Refuse (New-MemoryTrx ('WEB6_ACL phase=ParentMembership result=Failed reason=UnknownMembership'))
Refuse (New-MemoryTrx '' $marker)
Refuse (New-MemoryTrx $marker '' 'Other.Class.Test')
Refuse (New-MemoryTrx $marker '' $exact 'Other.Class')
Refuse (New-MemoryTrx $marker '' $exact $class 'foreign-execution')
Refuse (New-MemoryTrx ($marker+"`n"+$marker))
Refuse (New-MemoryTrx ($marker -replace 'ParentPinOpen','SecretPath'))
Refuse (New-MemoryTrx ($marker+' rawPath=private-canary'))
Refuse (New-MemoryTrx ($marker -replace 'OperationFailed','None'))
Refuse ((New-MemoryTrx $marker) -replace '</Results>',("<UnitTestResult testId='t1' executionId='e1' testName='$exact'><Output><StdOut>$marker</StdOut></Output></UnitTestResult></Results>"))
Refuse ((New-MemoryTrx '') -replace '</Results>',("<UnitTestResult testId='t2' executionId='e2' testName='Other.Class.Test'><Output><StdOut>$marker</StdOut></Output></UnitTestResult></Results>"))
Refuse ((New-MemoryTrx $marker) -replace '</Results>',("<UnitTestResult testId='t1' executionId='e2' testName='Other.Class.Test'/></Results>"))
Refuse ((New-MemoryTrx $marker) -replace '</Results>',("<UnitTestResult testId='t2' executionId='e1' testName='Other.Class.Test'/></Results>"))
Refuse (New-MemoryTrx 'unrelated output')
"WEB6_ACL_PARSER memoryOnly=1 checks=$script:checks passed=$script:checks filesystem=0 sql=0"
