# Exact-handle cleanup. Loading these definitions performs no IO.
function Open-Web6SandboxLease([string]$Path,[bool]$Directory,[bool]$Delete) {
    [Web6CheckpointNative+Lease]::new($Path,$Directory,$false,$Delete,$false,$true)
}
function Get-Web6SandboxStamp($Lease,[string]$Relative) {
    [pscustomobject]@{relative=$Relative;nativeIdentity=$Lease.IdentityHash;directory=$Lease.Directory;
        createdTicks=$Lease.CreationTimeTicks.ToString();acl=$Lease.AclHash;length=$Lease.Length;
        hash=if($Lease.Directory){''}else{$Lease.ContentHash}}
}
function Assert-Web6SandboxStamp($Lease,$Expected) {
    $actual=Get-Web6SandboxStamp $Lease $Expected.relative
    foreach($field in @('relative','nativeIdentity','directory','createdTicks','acl','length','hash')) {
        if([string]$actual.$field -cne [string]$Expected.$field){throw 'Exact sandbox identity/ACL/content changed.'}
    }
}
function Get-Web6SandboxChildren([string]$Path) { @(Get-ChildItem -LiteralPath $Path -Force | ForEach-Object Name) }
function Assert-Web6SandboxDrain { Require-Drain }
function Remove-Web6SandboxExact($State) {
    if($State.schemaVersion -ne 3 -or $State.quarantine -or $State.phase -cne 'Ready' -or -not $State.toolsSealed){throw 'Sandbox cleanup authority absent.'}
    # Only a direct drive child is admitted, so the drive pin covers all outer ancestry.
    if($State.root -cnotmatch '\A[A-Z]:\\w6b-[0-9a-f]{32}\z'){throw 'Noncanonical sandbox root.'}
    $root=$State.root;$drive=$root.Substring(0,3)
    $entries=[Collections.Generic.List[object]]::new()
    $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $closed=@($State.rootIdentity,$State.toolIdentity,$State.evidenceIdentity,$State.controlIdentity,$State.controlStamp)
    $closedNames=@('','tool-scratch','evidence','control','control\owner.json')
    for($i=0;$i -lt $closed.Count;$i++) {
        if($closed[$i].relative -cne $closedNames[$i] -or $closed[$i].directory -ne ($i -lt 4)){throw 'Sandbox control manifest changed.'}
        $entries.Add([pscustomobject]@{relative=$closedNames[$i];record=$closed[$i];lease=$null;path=$null})
    }
    foreach($record in @($State.toolInventory)) {
        if(-not $record.relative){throw 'Empty tool member denied.'}
        $entries.Add([pscustomobject]@{relative=('tool-scratch\'+$record.relative);record=$record;lease=$null;path=$null})
    }
    foreach($entry in $entries) {
        if(-not $names.Add($entry.relative)){throw 'Duplicate sandbox manifest member.'}
        if($entry.relative -eq ''){$entry.path=$root;continue}
        foreach($part in ($entry.relative -split '\\')) {
            if(-not $part -or $part -in @('.','..') -or $part.EndsWith('.') -or $part.EndsWith(' ') -or
                $part.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0){throw 'Noncanonical sandbox member.'}
        }
        $entry.path=$root+'\'+$entry.relative
        if([IO.Path]::GetFullPath($entry.path) -cne $entry.path -or $entry.path.Length -gt 240){throw 'Sandbox member path denied.'}
    }
    $pins=[Collections.Generic.List[object]]::new()
    try {
        $parent=Open-Web6SandboxLease $drive $true $false;$pins.Add($parent)
        # Parent-first admission pins the entire exact set before any destructive call.
        foreach($entry in @($entries|Sort-Object {$_.relative.Length})) {
            if($entry.relative -ne '') {
                $parentName=if($entry.relative.Contains('\')){$entry.relative.Substring(0,$entry.relative.LastIndexOf('\'))}else{''}
                $ancestor=@($entries|Where-Object {$_.relative -ceq $parentName -and $_.record.directory -and $null -ne $_.lease})
                if($ancestor.Count -ne 1){throw 'Manifest ancestor not pinned.'}
            }
            $entry.lease=Open-Web6SandboxLease $entry.path $entry.record.directory $true;$pins.Add($entry.lease)
            Assert-Web6SandboxStamp $entry.lease $entry.record
            if($entry.relative -cin $closedNames[0..3] -and -not $entry.lease.ProtectedAcl){throw 'Sandbox root ACL is not protected.'}
        }
        foreach($entry in @($entries|Where-Object {$_.record.directory})) {
            $expected=@($entries|Where-Object {$_ -ne $entry -and [IO.Path]::GetDirectoryName($_.path) -ceq $entry.path}|ForEach-Object {[IO.Path]::GetFileName($_.path)}|Sort-Object)
            $actual=@(Get-Web6SandboxChildren $entry.path|Sort-Object)
            if($expected.Count -ne $actual.Count -or ($expected -join "`n") -cne ($actual -join "`n")){throw 'Sandbox membership changed.'}
        }
        Assert-Web6SandboxDrain
        foreach($entry in @($entries|Sort-Object {$_.relative.Length} -Descending)) {
            # Directories must be empty at deletion; the kernel also refuses nonempty
            # directory disposition. No unlisted child can be deleted by this helper.
            if($entry.record.directory -and @(Get-Web6SandboxChildren $entry.path).Count -ne 0){throw 'Exact directory is not empty.'}
            $entry.lease.DeleteExact()
            $entry.lease.Dispose()
        }
    } finally {
        for($i=$pins.Count-1;$i -ge 0;$i--){$pins[$i].Dispose()}
    }
}
