# Pure XML/string parser: never reads paths, creates files or grants cleanup authority.
function Read-Web6AclPhaseEvidence {
    param([Parameter(Mandatory=$true)][string]$TrxXml)
    try {
        $settings = [System.Xml.XmlReaderSettings]::new()
        $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
        $settings.XmlResolver = $null
        $settings.MaxCharactersInDocument = 16777216
        $inputText = [System.IO.StringReader]::new($TrxXml)
        $reader = [System.Xml.XmlReader]::Create($inputText,$settings)
        try { $document=[System.Xml.XmlDocument]::new(); $document.XmlResolver=$null; $document.Load($reader) }
        finally { $reader.Dispose(); $inputText.Dispose() }
        $ns=[System.Xml.XmlNamespaceManager]::new($document.NameTable)
        $ns.AddNamespace('t','http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
        $class='MyDmsVn.Server.Infrastructure.IntegrationTests.Web6CandidateSafetyTests'
        $method='Manifest_refuses_acl_drift_and_retains_until_original_acl_is_restored'
        $exact=$class+'.'+$method
        $rows=@($document.SelectNodes('/t:TestRun/t:Results/t:UnitTestResult',$ns) | Where-Object { $_.GetAttribute('testName') -ceq $exact })
        if($rows.Count -ne 1){throw 'scope'}
        $row=$rows[0]; $id=$row.GetAttribute('testId'); $execution=$row.GetAttribute('executionId')
        if(-not $id -or -not $execution){throw 'identity'}
        $allRows=@($document.SelectNodes('/t:TestRun/t:Results/t:UnitTestResult',$ns))
        if(@($allRows | Where-Object { $_.GetAttribute('testId') -ceq $id }).Count -ne 1 -or
            @($allRows | Where-Object { $_.GetAttribute('executionId') -ceq $execution }).Count -ne 1){throw 'duplicate identity'}
        $definitions=@($document.SelectNodes('/t:TestRun/t:TestDefinitions/t:UnitTest',$ns) | Where-Object { $_.GetAttribute('id') -ceq $id })
        if($definitions.Count -ne 1){throw 'definition'}
        $definition=$definitions[0]
        $testMethod=$definition.SelectSingleNode('t:TestMethod',$ns)
        if($testMethod.GetAttribute('className') -cne $class -or $testMethod.GetAttribute('name') -cne $method -or
            $definition.SelectSingleNode('t:Execution',$ns).GetAttribute('id') -cne $execution){throw 'binding'}
        # ResultSummary may repeat xUnit output. It is never a source of test evidence.
        $outputs=@($row.SelectNodes('t:Output/t:StdOut',$ns))
        if($outputs.Count -ne 1){throw 'output'}
        $phases=@('CaseVerify','AttemptAdmission','ParentAncestors','ParentAclRead','ParentAclPolicy','ParentMembership','ParentPinOpen',
            'SiblingPathAdmission','SiblingAncestors','SiblingDescriptor','SiblingCreate','SiblingAclNormalize','SiblingRootOpen','SiblingRootCapture',
            'RecordCreate','RecordAclCapture','RecordSerialize','RecordWrite','RecordFlush','RecordReadback','RecordSeal','IndependentVerify',
            'AuthorityRegister','AuthorityReverify','ReadyForMutation','SealGate','FreshVerify','Mutation','Assertion','Restoration')
        $seen=[System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        $markers=[System.Collections.Generic.List[string]]::new()
        foreach($line in ($outputs[0].InnerText -split '\r?\n')) {
            if($line -notmatch 'WEB6_ACL'){continue}
            if($line -cnotmatch '^WEB6_ACL phase=([A-Za-z]+) result=(Started|Completed|Failed) reason=(None|OperationFailed|Unsealed|NotReady|AlreadyAttempted|DiagnosticUnavailable|TerminalFailure|EnumerationFailed|UnexpectedMembership|ParentIdentityMismatch|InvalidParentBinding)$'){throw 'schema'}
            $phase=$Matches[1]; $result=$Matches[2]; $reason=$Matches[3]
            if($phases -cnotcontains $phase -or (($result -ceq 'Failed') -eq ($reason -ceq 'None')) -or -not $seen.Add($phase+':'+$result)){throw 'marker'}
            $markers.Add($line)
        }
        if($markers.Count -eq 0){throw 'missing'}
        return $markers.ToArray()
    }
    catch { throw 'Exact ACL test phase evidence is missing, malformed or ambiguous.' }
}
