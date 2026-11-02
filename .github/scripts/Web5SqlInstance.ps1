param(
    [Parameter(Mandatory=$true)][ValidateSet('Initialize','Check','Complete')][string]$Action,
    [Parameter(Mandatory=$true)][string]$StatePath,
    [Parameter(Mandatory=$true)][string]$SummaryPath
)
$ErrorActionPreference='Stop'
# CI-only instance lifecycle. Test execution remains direct dotnet test in YAML.
# Database allocation/drop continues to belong to the existing SqlTestDatabase helper.
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or $env:RUNNER_OS -ne 'Windows' -or
    $env:GITHUB_REPOSITORY_ID -ne '1409766698') { throw 'This lifecycle is restricted to the designated GitHub-hosted Windows job.' }
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
$sid=$identity.User.Value
if ($sid -in @('S-1-5-18','S-1-5-19','S-1-5-20')) { throw 'A normal runner user must own the private instance.' }
function Save-State($value) { [IO.File]::WriteAllText($StatePath, ($value|ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false)) }
function Write-SafeSummary($value) {
    $safe=[ordered]@{ runId=$env:GITHUB_RUN_ID; attempt=$env:GITHUB_RUN_ATTEMPT; image=$env:ImageVersion; runnerEnvironment=$env:RUNNER_ENVIRONMENT;
        controlSha=$env:GITHUB_SHA; baselineSha='fc36c879fdd2d46ee77d136bdb6f9e70f7191b64'; phase=$value.phase;
        engineVersion=$value.engineVersion; edition=$value.edition; compatibility=$value.compatibility; ownerSidSha256=$value.sidHash; instanceOwnershipVerified=$value.verified;
        userDatabases=$value.userDatabases; activeOwners=$value.activeOwners; quarantine=$value.quarantine }
    $json=$safe|ConvertTo-Json -Depth 5 -Compress
    [IO.File]::WriteAllText($SummaryPath,$json+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
    Write-Output $json
}
function Open-OwnedConnection($state) {
    if ($state.sid -ne $sid -or $state.runId -ne $env:GITHUB_RUN_ID -or $state.attempt -ne $env:GITHUB_RUN_ATTEMPT -or
        $state.name -notmatch '^Web5_[0-9]+_[0-9]+_[a-f0-9]{12}$' -or $state.controlSha -ne $env:GITHUB_SHA) { throw 'Instance ownership context mismatch.' }
    $builder=New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $builder['Data Source']='(localdb)\'+$state.name; $builder['Initial Catalog']='master'; $builder['Integrated Security']=$true
    $builder['Pooling']=$false; $builder['Connect Timeout']=5; $builder['ConnectRetryCount']=0; $builder['Encrypt']=$false
    $connection=New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
    try { $connection.Open(); return $connection } catch { $connection.Dispose(); throw }
}
function Query-Scalar($connection,[string]$sql) {
    $command=$connection.CreateCommand();$command.CommandTimeout=3;$command.CommandText=$sql
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}
function Verify-Identity($connection,$state) {
    if ([int](Query-Scalar $connection "SELECT CONVERT(int,SERVERPROPERTY('IsLocalDB'));") -ne 1) { throw 'LocalDB identity required.' }
    if ([string](Query-Scalar $connection "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'));") -ne $state.server) { throw 'Server identity changed.' }
    $sqlSid=[byte[]](Query-Scalar $connection 'SELECT SUSER_SID();')
    if ((New-Object Security.Principal.SecurityIdentifier($sqlSid,0)).Value -ne $sid) { throw 'SQL authenticated SID mismatch.' }
    if ([string](Query-Scalar $connection "SELECT CONVERT(nvarchar(36),value) FROM sys.extended_properties WHERE class=0 AND name=N'MyDmsVn.Web5CiOwnership';") -ne $state.token) { throw 'Ownership token mismatch.' }
    if ([string](Query-Scalar $connection "SELECT CONVERT(nvarchar(33),create_date,126) FROM sys.databases WHERE database_id=1;") -ne $state.masterCreated) { throw 'Master allocation identity changed.' }
}
function Verify-Quiescence($connection,$state) {
    $clock=[Diagnostics.Stopwatch]::StartNew()
    do {
        $state.userDatabases=[int](Query-Scalar $connection 'SELECT COUNT(*) FROM sys.databases WHERE database_id>4;')
        $sqlOwners=[int](Query-Scalar $connection 'SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE is_user_process=1 AND session_id<>@@SPID;')
        $transactions=[int](Query-Scalar $connection 'SELECT COUNT(*) FROM sys.dm_tran_session_transactions st JOIN sys.dm_exec_sessions s ON s.session_id=st.session_id WHERE s.is_user_process=1 AND s.session_id<>@@SPID;')
        $processes=@(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^(testhost(\.net48|\.x86)?|MyDmsVn\.Server\.DbMigrator)$' }).Count
        $state.activeOwners=$sqlOwners+$transactions+$processes
        if ($state.userDatabases -eq 0 -and $state.activeOwners -eq 0) { return }
        Start-Sleep -Milliseconds 200
    } while($clock.Elapsed.TotalSeconds -lt 20)
    throw 'Owned SQL or test workers did not drain; quarantine required.'
}
$stage='Preflight'
$state=$null
try {
    if ($Action -eq 'Initialize') {
        if (Test-Path -LiteralPath $StatePath) { throw 'Existing instance state cannot be adopted.' }
        $stage='DiscoverUtility'
        $utility=(Get-Command SqlLocalDB.exe -ErrorAction SilentlyContinue).Source
        if (-not $utility) {
            $utility=@('C:\Program Files\Microsoft SQL Server\160\Tools\Binn\SqlLocalDB.exe','C:\Program Files\Microsoft SQL Server\150\Tools\Binn\SqlLocalDB.exe')|Where-Object {Test-Path -LiteralPath $_}|Select-Object -First 1
        }
        if (-not $utility) { throw 'The runner image lacks the required LocalDB tool.' }
        $stage='DiscoverVersions'
        $versionOutput=@(& $utility versions 2>$null);if($LASTEXITCODE -ne 0){throw 'Cannot inspect LocalDB engines.'}
        # The utility prints product labels followed by parenthesized versions.
        $versions=@([regex]::Matches(($versionOutput -join [Environment]::NewLine),'\b\d+\.\d+(?:\.\d+){0,2}\b') | ForEach-Object Value)
        Write-Output ('Installed LocalDB versions: '+(($versions | Where-Object { $_ -match '^\d+(\.\d+){1,3}$' }) -join ','))
        $engine=@('15.0','16.0')|Where-Object { $candidate=$_; @($versions|Where-Object {$_.Trim() -like ($candidate+'*')}).Count -gt 0 }|Select-Object -First 1
        if (-not $engine) { throw 'No reviewed SQL2019/2022 LocalDB engine is installed.' }
        $stage='AllocateState'
        $state=[pscustomobject]@{ name=('Web5_'+$env:GITHUB_RUN_ID+'_'+$env:GITHUB_RUN_ATTEMPT+'_'+[guid]::NewGuid().ToString('N').Substring(0,12));
            sid=$sid; runId=$env:GITHUB_RUN_ID; attempt=$env:GITHUB_RUN_ATTEMPT; controlSha=$env:GITHUB_SHA; token=[guid]::NewGuid().ToString('D');
            sidHash=([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($sid))).Replace('-','').ToLowerInvariant()); utility=$utility; server='';masterCreated='';engineVersion='';edition='';compatibility=$null;phase='Allocating';verified=$false;quarantine=$false;userDatabases=-1;activeOwners=-1 }
        Save-State $state
        $stage='CreateInstance'
        $null=& $utility create $state.name $engine 2>&1;if($LASTEXITCODE -ne 0){throw 'Owned instance creation failed.'}
        $stage='StartInstance'
        $null=& $utility start $state.name 2>&1;if($LASTEXITCODE -ne 0){throw 'Owned instance startup failed.'}
        $stage='ConnectOwnedInstance'
        $connection=Open-OwnedConnection $state
        try {
            if ([int](Query-Scalar $connection "SELECT CONVERT(int,SERVERPROPERTY('IsLocalDB'));") -ne 1){throw 'Wrong engine boundary.'}
            $state.server=[string](Query-Scalar $connection "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'));")
            $state.masterCreated=[string](Query-Scalar $connection "SELECT CONVERT(nvarchar(33),create_date,126) FROM sys.databases WHERE database_id=1;")
            $state.engineVersion=[string](Query-Scalar $connection "SELECT CONVERT(nvarchar(50),SERVERPROPERTY('ProductVersion'));")
            $state.edition=[string](Query-Scalar $connection "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('Edition'));")
            $stage='StampOwnership'
            $command=$connection.CreateCommand();$command.CommandTimeout=10
            $command.CommandText="EXEC sys.sp_addextendedproperty @name=N'MyDmsVn.Web5CiOwnership',@value=@token; ALTER DATABASE model SET COMPATIBILITY_LEVEL=140;"
            $null=$command.Parameters.Add('@token',[Data.SqlDbType]::NVarChar,36);$command.Parameters['@token'].Value=$state.token
            try {$null=$command.ExecuteNonQuery()}finally{$command.Dispose()}
            $stage='VerifyInitializedInstance'
            Verify-Identity $connection $state; Verify-Quiescence $connection $state
            if ([int](Query-Scalar $connection "SELECT compatibility_level FROM sys.databases WHERE name=N'model';") -ne 140){throw 'Model compatibility is not140.'}
            $state.compatibility=140;$state.verified=$true;$state.phase='Ready';Save-State $state
            # The value identifies only this private ephemeral instance; never accept a supplied SQL endpoint.
            Write-Output ('::add-mask::'+$connection.ConnectionString)
            [IO.File]::AppendAllText($env:GITHUB_ENV,'MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING='+$connection.ConnectionString+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
        } finally {$connection.Dispose()}
    } else {
        if (-not(Test-Path -LiteralPath $StatePath)){throw 'No owned instance state exists.'}
        $state=Get-Content -LiteralPath $StatePath -Raw|ConvertFrom-Json
        if ((Test-Path -LiteralPath ($StatePath+'.active')) -or $state.quarantine -or $state.phase -ne 'Ready'){throw 'Uncertain or completed instance cannot be reused or deleted.'}
        $state.phase=if ($Action -eq 'Check') {'Checking'} else {'Completing'}; Save-State $state
        $stage='ConnectOwnedInstance'
        $connection=Open-OwnedConnection $state
        try {Verify-Identity $connection $state;Verify-Quiescence $connection $state}finally{$connection.Dispose()}
        $state.verified=$true
        if ($Action -eq 'Check') { $state.phase='Ready' }
        if ($Action -eq 'Complete') {
            # No user database/session/transaction/worker remains. Do not force-stop or drop databases.
            $null=& $state.utility stop $state.name 2>&1;if($LASTEXITCODE -ne0){throw 'Graceful instance stop failed.'}
            $null=& $state.utility delete $state.name 2>&1;if($LASTEXITCODE -ne0){throw 'Owned stopped instance removal failed.'}
            $state.phase='Removed'
        }
        Save-State $state
    }
    Write-SafeSummary $state
}
catch {
    # Bounded diagnostics, never exception bodies or provider arguments.
    Write-Output (([ordered]@{ action=$Action; stage=$stage; scriptLine=$_.InvocationInfo.ScriptLineNumber; exceptionType=$_.Exception.GetType().Name }) | ConvertTo-Json -Compress)
    if ($state) {$state.quarantine=$true;$state.phase='Quarantined';Save-State $state;Write-SafeSummary $state}
    # Do not echo provider errors, credentials, SQL text, instance token or exception bodies.
    throw 'Protected SQL instance could not prove safe ownership/lifecycle; fail and retain uncertain resources for the lifetime of this isolated VM.'
}
finally {$identity.Dispose()}
