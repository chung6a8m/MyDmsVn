using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Desktop.Tests;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using MyDmsVn.WebOperation.Operator;
using Xunit;
using Context = MyDmsVn.Server.Infrastructure.IntegrationTests.WebOperationJournalTests.Context;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Test owner only: generated exact resources, no arbitrary SQL/control input and no retry.
// Workflow failure retains quarantine. TEST_CLEANUP_ONLY is separately verified test hygiene.
internal sealed class Web6MaintenanceRecoveryFixture
{
    internal enum Failure { None, Checksum, Restore, PrincipalProvisioning, EpochCas }
    internal sealed class Result
    {
        internal Failure ObservedFailure = Failure.None;
        internal bool ChecksumRejected = false;
        internal bool Succeeded, Quarantined, PhysicalOracleMatched, WriterAdmission, ResourcesAbsent, CopyOnlyChecksummed;
        internal int EpochRotations, StaleDenials, CurrentOperations, NormalCleanupCommands;
        internal string Phase = "created", QuarantineManifest = "";
    }
    internal sealed class Admission
    {
        private readonly string _manifest;
        private bool _oracle, _rotated, _principals, _drained, _quarantine;
        internal Admission(string manifest) { if (string.IsNullOrWhiteSpace(manifest)) throw new ArgumentException(); _manifest = manifest; }
        internal bool WriterAdmission { get; private set; }
        internal string? QuarantineManifest { get; private set; }
        private void Healthy() { if (_quarantine) throw new InvalidOperationException("Exact resources are quarantined."); }
        internal void OracleVerified() { Healthy(); _oracle = true; }
        internal void Rotated(int changed)
        {
            Healthy();
            if (!_oracle || _rotated || changed != 1) throw new InvalidOperationException("One owned epoch CAS after oracle is required.");
            _rotated = true;
        }
        internal void PrincipalsVerified() { Healthy(); if (!_rotated) throw new InvalidOperationException(); _principals = true; }
        internal void Admit() { Healthy(); if (!_oracle || !_rotated || !_principals) throw new InvalidOperationException("Recovery admission is closed."); WriterAdmission = true; _drained = false; }
        internal void RequireWriter()
        {
            Healthy();
            if (!WriterAdmission) { Quarantine("stale-writer"); throw new InvalidOperationException("Writer admission is closed."); }
        }
        // These checks also serve the independent TEST_CLEANUP_ONLY verifier. Passing
        // an observation never clears quarantine or enables writer/normal cleanup admission.
        internal void CheckOwnership(bool exactCreationIdentity, params FileAttributes[] paths)
        {
            if (!exactCreationIdentity || paths.Length == 0 || paths.Any(path => (path & FileAttributes.ReparsePoint) != 0))
            { Quarantine("ownership"); throw new InvalidOperationException("Resource creation identity or path is unsafe."); }
        }
        internal void CheckDrain(bool processesDrained, int sessionsAndLocks)
        {
            if (!processesDrained || sessionsAndLocks != 0)
            { Quarantine("drain"); throw new InvalidOperationException("Owned processes/sessions have not drained."); }
        }
        internal void Drained() { Healthy(); WriterAdmission = false; _drained = true; }
        internal void Quarantine(string reason) { _quarantine = true; WriterAdmission = false; QuarantineManifest = _manifest; }
        internal void Cleanup(string manifest, Action command)
        {
            Healthy();
            if (manifest != _manifest) { Quarantine("manifest-mismatch"); throw new InvalidOperationException("Exact manifest required."); }
            if (!_drained) throw new InvalidOperationException("Drain required.");
            command();
        }
    }
    private readonly string _admin, _root, _clone = SqlTestDatabaseName.Create();
    private readonly SecurityIdentifier _user, _service;
    private readonly DateTime _directoryCreated;
    private readonly string _marker = Guid.NewGuid().ToString("N");
    private readonly Guid _post = Guid.NewGuid(), _draft = Guid.NewGuid(), _oldSession = Guid.NewGuid(), _newSession = Guid.NewGuid();
    private readonly byte[] _fingerprint = Enumerable.Repeat((byte)7, 32).ToArray();
    private readonly List<string> _phases = new List<string>();
    private readonly List<Principal> _principals = new List<Principal>();
    private readonly Dictionary<string, (DateTime Created, string Hash)> _files = new Dictionary<string, (DateTime, string)>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _directories = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
    private readonly Admission _admission;
    private DateTime _manifestCreated, _dataCreated, _logCreated;
    private string? _manifestHash;
    private SqlTestDatabase? _source;
    private string _sourceName = "", _server = "", _adminLogin = "", _sourceToken = "", _sourceOracle = "";
    private string[] _sourcePaths = Array.Empty<string>();
    private readonly Guid _compactionCorrelation = Guid.NewGuid(), _currentOperation = Guid.NewGuid();
    private (int Id, DateTime Created) _sourceIdentity, _cloneIdentity;
    private bool _cloneCreated, _done, _started, _injected;
    private int _actor;
    private Guid _oldEpoch, _newEpoch;
    private Principal? _runtime, _maintenance;
    private readonly Result _result = new Result();
    private string Backup => Path.Combine(_root, "source.bak");
    private string Data => Path.Combine(_root, "clone.mdf");
    private string Log => Path.Combine(_root, "clone.ldf");
    internal string ManifestPath => Path.Combine(_root, "ownership.json");
    internal bool TestCleanupResourcesAbsent { get; private set; }
    private string CloneConnection => new SqlConnectionStringBuilder(_admin) { InitialCatalog = _clone }.ConnectionString;

    private Web6MaintenanceRecoveryFixture(string admin, string root, SecurityIdentifier user, SecurityIdentifier service)
    {
        _admin = admin; _root = root; _user = user; _service = service;
        _directoryCreated = Directory.GetCreationTimeUtc(root);
        _admission = new Admission(ManifestPath);
    }
    internal static void EnsureDistinctNames(string source, string clone)
    {
        Web5BackupFixture.EnsureCloneName(source); Web5BackupFixture.EnsureCloneName(clone);
        if (source.Equals(clone, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Source and clone must differ.");
    }
    internal static void EnsureNewPath(string role, string path)
    {
        if (role != "backup" && role != "data" && role != "log") throw new InvalidOperationException();
        Web5BackupFixture.EnsureNewDevice(path);
    }
    internal static async Task<Web6MaintenanceRecoveryFixture> CreateAsync()
    {
        var options = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable))
        { Pooling = false, ConnectRetryCount = 0, ConnectTimeout = 15 };
        if (!options.InitialCatalog.Equals("master", StringComparison.OrdinalIgnoreCase) || options.AttachDBFilename.Length != 0 || options.UserInstance)
            throw new InvalidOperationException("Designated nonproduction master required.");
        using var sql = new SqlConnection(options.ConnectionString); await sql.OpenAsync();
        if (!Environment.MachineName.Equals(await sql.QuerySingleAsync<string>("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'));"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Local SQL required.");
        var account = await sql.QuerySingleAsync<string>("SELECT service_account FROM sys.dm_server_services WHERE servicename=N'SQL Server ('+CONVERT(nvarchar(128),@@SERVICENAME)+N')';");
        var service = (SecurityIdentifier)new NTAccount(account).Translate(typeof(SecurityIdentifier));
        using var identity = WindowsIdentity.GetCurrent();
        var root = Path.Combine(Path.GetTempPath(), "web6-recovery-" + Guid.NewGuid().ToString("N"));
        Web5BackupFixture.EnsureNewDevice(root);
        CreatePrivateDirectory(root, identity.User!, service);
        var fixture = new Web6MaintenanceRecoveryFixture(options.ConnectionString, root, identity.User!, service);
        fixture.Record("allocated-private-directory");
        return fixture;
    }
    internal async Task<Result> RunAsync(Failure failure)
    {
        if (_started) throw new InvalidOperationException("One recovery attempt only.");
        _started = true;
        try
        {
            _source = await SqlTestDatabase.CreateForSupervisorAsync(_admin, name => { _sourceName = name; EnsureDistinctNames(name, _clone); Record("source-create-intent"); });
            using (var source = new SqlConnection(_source.ConnectionString))
            {
                await source.OpenAsync(); await _source.VerifyOwnershipAsync(source, default);
                _server = await source.QuerySingleAsync<string>("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'));");
                _adminLogin = await source.QuerySingleAsync<string>("SELECT ORIGINAL_LOGIN();");
                _sourceIdentity = await ReadIdentity(source, _sourceName);
                _sourcePaths = (await source.QueryAsync<string>("SELECT physical_name FROM sys.database_files ORDER BY file_id;")).ToArray();
                _sourceToken = await source.QuerySingleAsync<string>("SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'MyDmsVn.TestOwnership';");
            }
            Record("source-owned");
            if (!new DatabaseMigrationRunner().MigrateWeb5(_source.ConnectionString).Successful) throw new InvalidOperationException("Schema13 migration failed.");
            using (var source = new SqlConnection(_source.ConnectionString))
            {
                await source.OpenAsync(); await Seed(source);
                using (var context = new Context(new SqlConnection(_source.ConnectionString)))
                {
                    await context.Sql.OpenAsync(); context.Tx = context.Sql.BeginTransaction();
                    Assert.Equal(1, await new SqlWebOperationMaintenance(context).CompactAsync(_oldEpoch, _oldEpoch, _actor, _draft, _compactionCorrelation,
                        (login, current, stored, actor, operation, correlation) => { Record("source-compaction-attempt"); return Task.CompletedTask; }, default));
                    context.Tx.Commit();
                }
                Record("source-compaction-committed"); _sourceOracle = await Snapshot(source);
            }
            await VerifyDrain(); Record("source-drained");
            await BackupAndRestore(failure);
            using (var clone = new SqlConnection(CloneConnection))
            {
                await clone.OpenAsync(); await VerifyClone(clone);
                Assert.True((await SqlWeb5SchemaCompatibilityChecker.CheckConnectionAsync(clone, default)).IsCompatible);
                Assert.Equal(_sourceOracle, await Snapshot(clone));
                _result.PhysicalOracleMatched = true; _admission.OracleVerified(); Record("clone-oracle-verified");
                _newEpoch = Guid.NewGuid();
                _injected = failure == Failure.EpochCas;
                var changed = await clone.ExecuteAsync("UPDATE dbo.WebDeploymentState SET DeploymentEpoch=@next,RotatedAtUtc=SYSUTCDATETIME() WHERE Id=1 AND DeploymentEpoch=@previous;",
                    new { next = _newEpoch, previous = failure == Failure.EpochCas ? Guid.NewGuid() : _oldEpoch });
                _admission.Rotated(changed); _result.EpochRotations++; Record("clone-epoch-rotated");
                await clone.ExecuteAsync("UPDATE dbo.WebAuthSessions SET RevokedAtUtc=SYSUTCDATETIME() WHERE SessionId=@oldSession; DELETE dbo.RolePermissions WHERE RoleId IN(SELECT RoleId FROM dbo.UserRoles WHERE UserId=@actor); DECLARE @n datetime2(7)=SYSUTCDATETIME(); INSERT dbo.WebAuthSessions VALUES(@session,@actor,@n,DATEADD(day,1,@n),DATEADD(day,1,@n),NULL);",
                    new { oldSession = _oldSession, actor = _actor, session = _newSession });
                await Provision(clone, failure);
            }
            _admission.PrincipalsVerified(); Record("fresh-principals-verified");
            await InspectRecovered();
            _admission.Admit(); _result.WriterAdmission = true; Record("writer-admitted");
            await CheckWriterBoundaries(); await InspectRecovered();
            await VerifyDrain(); _admission.Drained(); _result.WriterAdmission = false; Record("all-workers-sessions-drained");
            _admission.Cleanup(ManifestPath, () => _result.NormalCleanupCommands++);
            await CleanupOwned(); _result.ResourcesAbsent = true; _result.Succeeded = true; _result.Phase = "cleaned";
        }
        catch (Exception error)
        {
            _admission.Quarantine(error.GetType().Name); _result.Quarantined = true; _result.WriterAdmission = false;
            if (_injected)
            {
                _result.ObservedFailure = failure;
                if (error is SqlException injectedSql) Console.WriteLine("WEB6_RECOVERY injectedSqlErrors=" + string.Join(",", injectedSql.Errors.Cast<SqlError>().Select(item => item.Number)));
                _result.ChecksumRejected = failure == Failure.Checksum && _result.CopyOnlyChecksummed && error is SqlException checksumError &&
                    checksumError.Errors.Cast<SqlError>().Any(item => item.Number == 3189 || item.Number == 3183);
            }
            _result.QuarantineManifest = ManifestPath;
            Record("QUARANTINED-" + _result.Phase + "-" + error.GetType().Name);
            Console.WriteLine("WEB6_RECOVERY quarantine=1 phase=" + _result.Phase);
            if (failure == Failure.None || !_injected) throw;
        }
        return _result;
    }
    private async Task BackupAndRestore(Failure failure)
    {
        VerifyDirectory(); EnsureNewPath("backup", Backup); EnsureNewPath("data", Data); EnsureNewPath("log", Log);
        using (new FileStream(Backup, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        Seal(Backup); Record("backup-start");
        using var master = new SqlConnection(_admin); await master.OpenAsync();
        await VerifyMaster(master);
        await VerifySource(); VerifySealed(Backup);
        await master.ExecuteAsync(new SqlTestCommand("BACKUP DATABASE [" + _sourceName + "] TO DISK=@backup WITH FORMAT,INIT,COPY_ONLY,CHECKSUM;", new { backup = Backup }, commandTimeout: 120));
        Seal(Backup); Record("backup-complete");
        using (var header = master.CreateCommand())
        {
            header.CommandText = "RESTORE HEADERONLY FROM DISK=@backup;"; header.CommandTimeout = 120; header.Parameters.AddWithValue("@backup", Backup);
            using var reader = await header.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetBoolean(reader.GetOrdinal("IsCopyOnly")));
            Assert.True(reader.GetBoolean(reader.GetOrdinal("HasBackupChecksums")));
        }
        await master.ExecuteAsync(new SqlTestCommand("RESTORE VERIFYONLY FROM DISK=@backup WITH CHECKSUM;", new { backup = Backup }, commandTimeout: 120));
        _result.CopyOnlyChecksummed = true;
        if (failure == Failure.Checksum)
        {
            // Corrupt an owned backup data block while keeping the media header intact.
            // Resealing supports TEST_CLEANUP_ONLY, never a successful recovery claim.
            _injected = true; VerifySealed(Backup);
            using (var file = new FileStream(Backup, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            { file.Position = file.Length / 2; var position = file.Position; var value = file.ReadByte(); file.Position = position; file.WriteByte((byte)(value ^ 0xff)); file.Flush(true); }
            Seal(Backup); Record("injected-checksum-data-corruption");
        }
        if (failure == Failure.Checksum)
            await master.ExecuteAsync(new SqlTestCommand("RESTORE VERIFYONLY FROM DISK=@backup WITH CHECKSUM;", new { backup = Backup }, commandTimeout: 120));
        var files = new List<(string Name, string Type)>();
        using (var list = master.CreateCommand())
        {
            list.CommandText = "RESTORE FILELISTONLY FROM DISK=@backup;"; list.CommandTimeout = 120; list.Parameters.AddWithValue("@backup", Backup);
            using var reader = await list.ExecuteReaderAsync();
            while (await reader.ReadAsync()) files.Add((reader.GetString(reader.GetOrdinal("LogicalName")), reader.GetString(reader.GetOrdinal("Type"))));
        }
        if (files.Count != 2 || files.Count(x => x.Type == "D") != 1 || files.Count(x => x.Type == "L") != 1) throw new InvalidOperationException("Unsupported owned backup layout.");
        if (await master.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.databases WHERE name=@name;", new { name = _clone }) != 0) throw new InvalidOperationException("Clone already exists.");
        VerifyDirectory(); VerifySealed(Backup); EnsureNewPath("data", Data); EnsureNewPath("log", Log); Record("restore-start");
        if (failure == Failure.Restore)
        {
            _injected = true;
            // A nonexistent backup-set position fails before allocation. Any unexpected
            // partial allocation is unverified and cannot enter TEST_CLEANUP_ONLY deletion.
            await master.ExecuteAsync(new SqlTestCommand("RESTORE DATABASE [" + _clone + "] FROM DISK=@backup WITH FILE=2147483647,CHECKSUM;",
                new { backup = Backup }, commandTimeout: 120));
        }
        await master.ExecuteAsync(new SqlTestCommand("RESTORE DATABASE [" + _clone + "] FROM DISK=@backup WITH CHECKSUM,MOVE @dataName TO @data,MOVE @logName TO @log;",
            new { backup = Backup, dataName = files.Single(x => x.Type == "D").Name, data = Data, logName = files.Single(x => x.Type == "L").Name, log = Log }, commandTimeout: 120));
        _cloneIdentity = await ReadIdentity(master, _clone);
        _dataCreated = File.GetCreationTimeUtc(Data); _logCreated = File.GetCreationTimeUtc(Log);
        _cloneCreated = true;
        Record("restore-complete");
    }
    private async Task Seed(SqlConnection sql)
    {
        await sql.ExecuteAsync("EXEC sys.sp_addextendedproperty @name=N'Web6RecoveryOwner',@value=@marker;", new { marker = _marker });
        _oldEpoch = await sql.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;");
        _actor = await sql.QuerySingleAsync<int>("INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(N'recovery',N'RECOVERY',N'Recovery',N'Local',N'hash',N'',N'BCrypt');");
        await sql.ExecuteAsync(@"
INSERT dbo.Roles(RoleName,NormalizedRoleName) VALUES(N'recovery',N'RECOVERY'); DECLARE @r int=SCOPE_IDENTITY();
INSERT dbo.UserRoles(UserId,RoleId) VALUES(@actor,@r); INSERT dbo.RolePermissions(RoleId,PermissionKey) VALUES(@r,N'Sales.Receipts.Write');
DECLARE @now datetime2(7)=SYSUTCDATETIME(); INSERT dbo.WebAuthSessions VALUES(@session,@actor,@now,DATEADD(day,1,@now),DATEADD(day,1,@now),NULL);
INSERT dbo.Products(Code,Name,Unit) VALUES(N'R5',N'Recovery',N'ea'); DECLARE @p int=SCOPE_IDENTITY();
INSERT dbo.Warehouses(Code,Name) VALUES(N'R5',N'Recovery'); DECLARE @w int=SCOPE_IDENTITY();
INSERT dbo.Employees(Code,Name) VALUES(N'R5',N'Recovery'); DECLARE @e int=SCOPE_IDENTITY();
INSERT dbo.Customers(Code,Name) VALUES(N'R5',N'Recovery'); DECLARE @c int=SCOPE_IDENTITY();
DECLARE @t datetime2(7)='20250101';
SET IDENTITY_INSERT dbo.GoodsReceipts ON;
INSERT dbo.GoodsReceipts(ReceiptId,ReceiptNo,ReceiptDate,WarehouseId,EmployeeId,Status,CreatedByUserId,PostedAtUtc,PostedByUserId) VALUES(41,N'R5','20250101',@w,@e,'Posted',@actor,@t,@actor);
SET IDENTITY_INSERT dbo.GoodsReceipts OFF;
INSERT dbo.GoodsReceiptLines(ReceiptId,[LineNo],ProductId,Quantity,UnitCost,CreatedByUserId) VALUES(41,1,@p,10,2,@actor); DECLARE @line bigint=SCOPE_IDENTITY();
INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId) VALUES(@w,@p,'GoodsReceipt',41,@line,10,@t,@actor);
DECLARE @ledger bigint=SCOPE_IDENTITY(); INSERT dbo.StockValuationEntries VALUES(@ledger,20);
INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@w,@p,10,20);
SET IDENTITY_INSERT dbo.SalesReceipts ON;
INSERT dbo.SalesReceipts(SaleId,SaleNo,SaleDate,WarehouseId,EmployeeId,CustomerId,Status,CreatedByUserId) VALUES(42,N'R5-D','20250101',@w,@e,@c,'Draft',@actor);
SET IDENTITY_INSERT dbo.SalesReceipts OFF;
INSERT dbo.SalesReceiptLines(SaleId,[LineNo],ProductId,Qty,Unit,IsGift,LineAmount,UnitPrice,CreatedByUserId) VALUES(42,1,@p,1,N'ea',0,3,3,@actor);
INSERT dbo.WebOperationJournal VALUES(@actor,@post,@epoch,1,1,6,41,@hash,41,'EXPIRED',NULL,@t,@t,DATEADD(day,7,@t));
INSERT dbo.WebOperationJournal VALUES(@actor,@draft,@epoch,1,1,1,NULL,@hash,42,'COMMITTED',0x7b7d,@t,@t,DATEADD(day,7,@t));",
            new { actor = _actor, session = _oldSession, epoch = _oldEpoch, post = _post, draft = _draft, hash = _fingerprint });
        Record("source-seeded");
    }
    private async Task Provision(SqlConnection sql, Failure failure)
    {
        _runtime = new Principal(); var op = new Principal(); _maintenance = new Principal();
        _principals.AddRange(new[] { _runtime, op, _maintenance }); Record("principals-create-intent");
        foreach (var principal in _principals)
        {
            await principal.Create(sql); Record("principal-created");
            if (failure == Failure.PrincipalProvisioning)
            { _injected = true; await sql.ExecuteAsync("THROW 51997,'Injected provisioning refusal after owned login.',1;"); }
        }
        // Inert database-only migration user has no login or usable runtime credential.
        await sql.ExecuteAsync("CREATE USER [Web6RecoveryMigration] WITHOUT LOGIN;");
        string Script(string name) => ReadScript(name).Replace("$(WebRuntimeUser)", _runtime.Name).Replace("$(MigrationUser)", "Web6RecoveryMigration")
            .Replace("$(OperatorUser)", op.Name).Replace("$(MaintenanceUser)", _maintenance.Name);
        await sql.ExecuteAsync(Script("Provision-MyDmsVnWebPrincipals.sql"));
        await sql.ExecuteAsync(Script("Provision-MyDmsVnWebOperationOperator.sql"));
        await sql.ExecuteAsync(Script("Provision-MyDmsVnWebOperationMaintenance.sql"));
        foreach (var principal in _principals)
        {
            using var probe = new SqlConnection(principal.Connection(CloneConnection)); await probe.OpenAsync();
            Assert.Equal(principal.Name, await probe.QuerySingleAsync<string>("SELECT ORIGINAL_LOGIN();"));
            Assert.Equal(principal == _maintenance ? 1 : 0, await probe.QuerySingleAsync<int>("SELECT HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL');"));
        }
    }
    private async Task InspectRecovered()
    {
        using var sql = new SqlConnection(CloneConnection); await sql.OpenAsync(); await VerifyClone(sql);
        var before = await Snapshot(sql);
        var factory = new MaintenanceSqlConnectionFactory(_maintenance!.Connection(CloneConnection), _maintenance.Name, _maintenance.Name,
            TargetDigest(_clone, _cloneIdentity, _sourceToken));
        var roots = new[] { Path.Combine(_root, "evidence-" + Guid.NewGuid().ToString("N")), Path.Combine(_root, "evidence-" + Guid.NewGuid().ToString("N")) };
        foreach (var root in roots)
        {
            CreatePrivateDirectory(root, _user, new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
            _directories.Add(root, Directory.GetCreationTimeUtc(root));
        }
        var evidence = new MaintenanceEvidenceStore(new MaintenanceEvidencePolicy(roots[0], roots[1], TimeSpan.FromDays(90)));
        Assert.True(CompactOptions.TryParse(Arguments("compact-preview", _draft), out var compact, out _));
        Assert.Equal(OperatorCompactionOutcome.NoChange, (await new SqlOperatorCompactionClient(factory).PreviewAsync(compact!, default)).Outcome);
        foreach (var operation in new[] { _post, _draft })
        {
            Assert.True(ReconcileCommittedOptions.TryParse(Arguments("reconcile-committed", operation), out var options, out _));
            var outcome = await new SqlOperatorReconciliationClient(factory, evidence).ReconcileAsync(options!, default);
            Assert.Equal(operation == _post ? WebOperationReconciliationOutcome.Committed : WebOperationReconciliationOutcome.Unknown, outcome.Outcome);
            foreach (var root in roots)
            {
                var casePath = Path.Combine(root, options!.CaseId);
                _directories.Add(casePath, Directory.GetCreationTimeUtc(casePath));
                foreach (var name in new[] { "01-attempt.json", "03-readback.json", "manifest.json" }) Seal(Path.Combine(casePath, name));
            }
        }
        Assert.Equal(before, await Snapshot(sql));
        Record("restricted-compact-reconcile-verified");
    }
    private string[] Arguments(string command, Guid operation) => new[] { command, "--environment", "owned-test", "--database", _clone,
        "--current-epoch", _newEpoch.ToString("D"), "--stored-epoch", _oldEpoch.ToString("D"), "--actor", _actor.ToString(CultureInfo.InvariantCulture),
        "--operation", operation.ToString("D"), "--fingerprint", Hex(_fingerprint), "--correlation", Guid.NewGuid().ToString("D"),
        "--case", "recovery-" + Guid.NewGuid().ToString("N"), "--timeout-seconds", "15" };
    private async Task CheckWriterBoundaries()
    {
        Assert.True(_admission.WriterAdmission);
        await Writer(_oldEpoch, _oldSession, "Deployment.EpochMismatch");
        await Writer(_newEpoch, _oldSession, "Auth.Unauthorized");
        await Writer(_newEpoch, _newSession, "Auth.Forbidden");
        using (var sql = new SqlConnection(CloneConnection))
            await sql.ExecuteAsync("INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) VALUES(@actor,N'Sales.Receipts.Write',1);", new { actor = _actor });
        await Writer(_newEpoch, _newSession, null);
    }
    private async Task Writer(Guid epoch, Guid session, string? denied)
    {
        _admission.RequireWriter();
        using var observer = new SqlConnection(CloneConnection); await observer.OpenAsync();
        var before = await Snapshot(observer, includeJournal: denied != null);
        using var context = new Context(new SqlConnection(_runtime!.Connection(CloneConnection))); await context.Sql.OpenAsync(); context.Tx = context.Sql.BeginTransaction();
        var journal = new SqlWebOperationJournal(context, new WebOperationJournalTests.ActorAccessor(_actor),
            PermissionRegistry.Create(new[] { new PermissionDefinition(PermissionKeys.SalesReceiptsWrite, "Sales", "Write") }));
        var operation = new WebOperationContext(denied == null ? _currentOperation : Guid.NewGuid(), epoch, session, WebOperationKind.SalesCreate, null, _fingerprint, new Codec());
        if (denied != null)
        {
            Assert.Equal(denied, (await Assert.ThrowsAsync<WebOperationException>(() => journal.AdmitAsync(operation, _actor, default))).Code);
            context.Tx.Rollback();
            Assert.Equal(before, await Snapshot(observer)); _result.StaleDenials++; return;
        }
        Assert.Null(await journal.AdmitAsync(operation, _actor, default));
        // Restricted-runtime journal admission probe. Full HTTP/Sales/Receipt handlers NOT RUN.
        await journal.CompleteAsync(operation, _actor, 43, Encoding.UTF8.GetBytes("{}"), default);
        context.Tx.Commit();
        Assert.Equal(before, await Snapshot(observer, includeJournal: false));
        _result.CurrentOperations = await observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@operation AND DeploymentEpoch=@epoch AND Disposition='COMMITTED' AND ResultDocumentId=43 AND RequestFingerprint=@hash;",
            new { actor = _actor, operation = _currentOperation, epoch = _newEpoch, hash = _fingerprint });
        Assert.Equal(3, await observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
    }
    private async Task VerifySource()
    {
        if (_source == null) throw new InvalidOperationException("Source ownership unavailable.");
        using var sql = new SqlConnection(_source.ConnectionString); await sql.OpenAsync(); await _source.VerifyOwnershipAsync(sql, default);
        Assert.Equal(_sourcePaths, (await sql.QueryAsync<string>("SELECT physical_name FROM sys.database_files ORDER BY file_id;")).ToArray());
    }
    private async Task VerifyMaster(SqlConnection sql)
    {
        if (await sql.QuerySingleAsync<string>("SELECT DB_NAME();") != "master" ||
            await sql.QuerySingleAsync<string>("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'));") != _server ||
            await sql.QuerySingleAsync<string>("SELECT ORIGINAL_LOGIN();") != _adminLogin)
            throw new InvalidOperationException("Master server/login identity changed.");
    }
    private async Task VerifyClone(SqlConnection sql)
    {
        var identityMatches = !(!_cloneCreated || await ReadIdentity(sql, _clone) != _cloneIdentity ||
            await sql.QuerySingleAsync<string>("SELECT DB_NAME();") != _clone ||
            await sql.QuerySingleAsync<string>("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'));") != _server ||
            await sql.QuerySingleAsync<string>("SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'Web6RecoveryOwner';") != _marker ||
            await sql.QuerySingleAsync<string>("SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'MyDmsVn.TestOwnership';") != _sourceToken);
        var physical = await sql.QueryAsync<string>("SELECT physical_name FROM sys.database_files ORDER BY file_id;");
        _admission.CheckOwnership(identityMatches && physical.SequenceEqual(new[] { Data, Log }, StringComparer.OrdinalIgnoreCase) &&
            File.GetCreationTimeUtc(Data) == _dataCreated && File.GetCreationTimeUtc(Log) == _logCreated,
            File.GetAttributes(Backup), File.GetAttributes(Data), File.GetAttributes(Log));
    }
    private async Task VerifyDrain()
    {
        _admission.CheckDrain(!GuiTestSafety.IsPoisoned && !WebOperationCrashProcess.IsPoisoned, 0);
        using var master = new SqlConnection(_admin); await master.OpenAsync();
        if (_server.Length > 0) await VerifyMaster(master);
        foreach (var name in new[] { _sourceName, _clone }.Where(x => x.Length > 0))
            _admission.CheckDrain(true, await master.QuerySingleAsync<int>("SELECT (SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE database_id=DB_ID(@name))+(SELECT COUNT(*) FROM sys.dm_tran_locks WHERE resource_database_id=DB_ID(@name));", new { name }));
        foreach (var principal in _principals) await principal.VerifyNoSessions(master);
    }
    internal async Task TestCleanupOnlyAsync()
    {
        if (_done) { TestCleanupResourcesAbsent = true; return; }
        // Quarantine is not success; fresh independent verification can fail and retain it.
        Record("TEST_CLEANUP_ONLY-start");
        await CleanupOwned(); TestCleanupResourcesAbsent = true;
        Console.WriteLine("WEB6_RECOVERY TEST_CLEANUP_ONLY resourcesAbsent=1 workflowSuccess=0");
    }
    private async Task CleanupOwned()
    {
        VerifyDirectory(); VerifyManifest(); await VerifyDrain();
        if (_source != null) await VerifySource();
        using var master = new SqlConnection(_admin); await master.OpenAsync();
        if (_server.Length > 0) await VerifyMaster(master);
        if (_source == null && _sourceName.Length > 0 &&
            await master.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.databases WHERE name=@name;", new { name = _sourceName }) != 0)
            throw new InvalidOperationException("Unverified source allocation retained.");
        var exists = await master.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.databases WHERE name=@name;", new { name = _clone });
        if (!_cloneCreated && exists != 0) throw new InvalidOperationException("Unverified partial clone retained.");
        if (_cloneCreated)
        {
            using var clone = new SqlConnection(CloneConnection);
            await clone.OpenAsync(); await VerifyClone(clone);
            foreach (var principal in _principals) await principal.Verify(clone);
        }
        foreach (var path in _files.Keys) VerifySealed(path);
        foreach (var directory in _directories)
            if (!Directory.Exists(directory.Key) || Directory.GetCreationTimeUtc(directory.Key) != directory.Value ||
                (File.GetAttributes(directory.Key) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Owned evidence directory changed.");
        if (!Directory.GetDirectories(_root, "*", SearchOption.AllDirectories).OrderBy(x => x).SequenceEqual(_directories.Keys.OrderBy(x => x)))
            throw new InvalidOperationException("Foreign directory retained.");
        // Verify the intended deletion set first. Foreign children prevent cleanup.
        var actualFiles = Directory.GetFiles(_root, "*", SearchOption.AllDirectories);
        if (actualFiles.Any(path => path != ManifestPath && !_files.ContainsKey(path) && !(_cloneCreated && (path == Data || path == Log))))
            throw new InvalidOperationException("Unowned child retained.");
        if (_cloneCreated)
        {
            await master.ExecuteAsync("DROP DATABASE [" + _clone + "];");
            _cloneCreated = false;
        }
        foreach (var principal in _principals) await principal.DropLogin(master);
        if (_source != null) await _source.DisposeIfQuiescentAsync();
        foreach (var name in new[] { _sourceName, _clone }.Where(x => x.Length > 0))
            if (await master.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.databases WHERE name=@name;", new { name }) != 0) throw new InvalidOperationException("Database remains.");
        if (File.Exists(Data) || File.Exists(Log)) throw new InvalidOperationException("Restored physical file remains.");
        foreach (var path in _files.Keys) { VerifySealed(path); File.Delete(path); }
        VerifyManifest(); File.Delete(ManifestPath);
        foreach (var directory in _directories.Keys.OrderByDescending(x => x.Length))
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Reparse child retained.");
            Directory.Delete(directory, false);
        }
        VerifyDirectory(); Directory.Delete(_root, false); _done = true;
        Console.WriteLine("WEB6_RECOVERY exactResourcesAbsent=1");
    }
    private void VerifyDirectory()
    {
        Web5BackupFixture.VerifyPrivateDirectory(_root, _user, _service);
        _admission.CheckOwnership(Directory.GetCreationTimeUtc(_root) == _directoryCreated, File.GetAttributes(_root));
    }
    private void Seal(string path)
    {
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        var info = new FileInfo(path);
        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("File identity uncertain.");
        if (_files.TryGetValue(path, out var previous) && previous.Created != info.CreationTimeUtc) throw new InvalidOperationException("File replaced during owned write.");
        _files[path] = (info.CreationTimeUtc, HashFile(path));
    }
    private void VerifySealed(string path)
    {
        VerifyDirectory();
        var info = new FileInfo(path);
        if (!_files.TryGetValue(path, out var seal) || !info.Exists || info.CreationTimeUtc != seal.Created ||
            (info.Attributes & FileAttributes.ReparsePoint) != 0 || HashFile(path) != seal.Hash) throw new InvalidOperationException("Owned file identity changed.");
        for (var cursor = info.Directory; cursor != null; cursor = cursor.Parent)
            if ((cursor.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Reparse ancestry.");
    }
    private void VerifyManifest()
    {
        if (_manifestHash == null) return;
        if (!File.Exists(ManifestPath) || File.GetCreationTimeUtc(ManifestPath) != _manifestCreated ||
            (File.GetAttributes(ManifestPath) & FileAttributes.ReparsePoint) != 0 || HashFile(ManifestPath) != _manifestHash)
            throw new InvalidOperationException("Exact ownership manifest changed.");
    }
    private void Record(string phase)
    {
        VerifyDirectory(); VerifyManifest(); _result.Phase = phase;
        var phaseUtc = DateTime.UtcNow.ToString("O");
        _phases.Add(phaseUtc + " " + phase);
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(new { version = 1, marker = _marker, server = _server, source = _sourceName,
            sourceId = _sourceIdentity.Id, sourceCreated = _sourceIdentity.Created, sourceToken = _sourceToken, clone = _clone,
            sourcePaths = _sourcePaths, actor = _actor, storedEpoch = _oldEpoch, currentEpoch = _newEpoch, compactOperation = _draft,
            reconcileOperation = _post, compactionCorrelation = _compactionCorrelation, currentJournalAdmissionProbe = _currentOperation,
            cloneId = _cloneIdentity.Id, cloneCreated = _cloneIdentity.Created, backup = Backup, data = Data, log = Log,
            dataCreated = _dataCreated, logCreated = _logCreated, files = _files.Select(p => new { path = p.Key, created = p.Value.Created, hash = p.Value.Hash }),
            directories = _directories.Select(p => new { path = p.Key, created = p.Value }),
            principals = _principals.Select(p => new { p.Name, sid = Hex(p.Sid) }), phases = _phases.ToArray(), writerAdmission = _admission.WriterAdmission });
        using (var file = new FileStream(ManifestPath, _manifestHash == null ? FileMode.CreateNew : FileMode.Truncate, FileAccess.Write, FileShare.None))
        { var bytes = new UTF8Encoding(false).GetBytes(json); file.Write(bytes, 0, bytes.Length); file.Flush(true); }
        _manifestCreated = File.GetCreationTimeUtc(ManifestPath); _manifestHash = HashFile(ManifestPath);
        // Retained TRX contains UTC phases only; the private resource manifest is removed
        // after verified cleanup. These timestamps are not service RPO/RTO.
        Console.WriteLine("WEB6_RECOVERY phaseUtc=" + phaseUtc + " phase=" + phase);
    }
    private string TargetDigest(string database, (int Id, DateTime Created) identity, string token)
    {
        var fields = new[] { "mydmsvn-maintenance-target-v1", "owned-test", database, _server, identity.Id.ToString(CultureInfo.InvariantCulture), identity.Created.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture), token };
        var bytes = fields.SelectMany(field => { var data = Encoding.UTF8.GetBytes(field); var length = data.Length; return new[] { (byte)(length >> 24), (byte)(length >> 16), (byte)(length >> 8), (byte)length }.Concat(data); }).ToArray();
        using var sha = SHA256.Create(); return Hex(sha.ComputeHash(bytes));
    }
    private static Task<(int Id, DateTime Created)> ReadIdentity(SqlConnection sql, string name) => sql.QuerySingleAsync<(int, DateTime)>("SELECT database_id,create_date FROM sys.databases WHERE name=@name;", new { name });
    private static async Task<string> Snapshot(SqlConnection sql, bool includeJournal = true)
    {
        var rows = new List<string>();
        foreach (var table in new[] { "GoodsReceipts ORDER BY ReceiptId", "GoodsReceiptLines ORDER BY ReceiptLineId", "SalesReceipts ORDER BY SaleId", "SalesReceiptLines ORDER BY SaleLineId", "StockBalances ORDER BY WarehouseId,ProductId", "StockLedger ORDER BY LedgerId", "StockValuationEntries ORDER BY LedgerId", "WebOperationJournal ORDER BY ActorUserId,OperationId", "WebOperationTerminalAudit ORDER BY ActorUserId,OperationId", "WebDeploymentState ORDER BY Id" })
            if (includeJournal || !table.StartsWith("WebOperationJournal", StringComparison.Ordinal))
                rows.Add(await sql.QuerySingleAsync<string>("SELECT (SELECT * FROM dbo." + table + " FOR JSON PATH,INCLUDE_NULL_VALUES);"));
        return string.Join("|", rows);
    }
    private static string HashFile(string path) { using var stream = File.OpenRead(path); using var sha = SHA256.Create(); return Hex(sha.ComputeHash(stream)); }
    private static string Hex(byte[] bytes) => string.Concat(bytes.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
    private static void CreatePrivateDirectory(string path, SecurityIdentifier user, SecurityIdentifier service)
    {
        var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false); acl.SetOwner(user);
        foreach (var sid in new[] { user, service }.Distinct()) acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        Directory.CreateDirectory(path, acl);
    }
    private static string ReadScript(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        { var path = Path.Combine(directory.FullName, "deployment", "sql", name); if (File.Exists(path)) return File.ReadAllText(path); }
        throw new FileNotFoundException(name);
    }
    private sealed class Codec : IWebOperationResultCodec
    {
        public byte[] Encode<T>(WebOperationKind kind, T result) => throw new NotSupportedException();
        public T Decode<T>(WebOperationKind kind, byte[] bytes) => throw new NotSupportedException();
    }
    private sealed class Principal
    {
        internal readonly string Name = "WEB6_Recovery_" + Guid.NewGuid().ToString("N");
        internal readonly byte[] Sid = Guid.NewGuid().ToByteArray();
        private readonly string _password = "Web6!" + Guid.NewGuid().ToString("N");
        private bool _attempted;
        internal async Task Create(SqlConnection sql)
        {
            _attempted = true;
            await sql.ExecuteAsync("CREATE LOGIN [" + Name + "] WITH PASSWORD=N'" + _password + "',SID=0x" + Hex(Sid) + ",CHECK_POLICY=OFF; CREATE USER [" + Name + "] FOR LOGIN [" + Name + "];");
        }
        internal Task VerifyNoSessions(SqlConnection sql) => sql.ExecuteAsync("IF EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE original_security_id=@sid OR security_id=@sid) THROW 51998,'Principal sessions remain.',1;", new { sid = Sid });
        internal Task Verify(SqlConnection sql) => sql.ExecuteAsync("IF EXISTS(SELECT 1 FROM sys.server_principals WHERE name=@name AND (sid<>@sid OR type<>'S')) OR EXISTS(SELECT 1 FROM sys.database_principals WHERE name=@name AND (sid<>@sid OR type<>'S')) THROW 51998,'Principal identity mismatch.',1;", new { name = Name, sid = Sid });
        internal async Task DropLogin(SqlConnection sql)
        {
            if (!_attempted) return;
            await VerifyNoSessions(sql);
            await sql.ExecuteAsync("IF EXISTS(SELECT 1 FROM sys.server_principals WHERE name=@name AND (sid<>@sid OR type<>'S')) THROW 51998,'Principal identity mismatch.',1; IF EXISTS(SELECT 1 FROM sys.server_principals WHERE name=@name AND sid=@sid) DROP LOGIN [" + Name + "]; IF SUSER_ID(@name) IS NOT NULL THROW 51998,'Principal remains.',1;", new { name = Name, sid = Sid });
        }
        internal string Connection(string connection) => new SqlConnectionStringBuilder(connection) { IntegratedSecurity = false, Authentication = SqlAuthenticationMethod.NotSpecified, UserID = Name, Password = _password, Pooling = false, ConnectRetryCount = 0 }.ConnectionString;
    }
}
