using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal sealed class SqlTestDatabase : IDisposable
{
    public const string ConnectionStringEnvironmentVariable =
        "MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING";

    private const int CommandTimeoutSeconds = 30;
    private static readonly TimeSpan LifecycleTimeout = TimeSpan.FromMinutes(2);

    private readonly string _adminConnectionString;
    private readonly Func<string, SqlConnection> _connectionFactory;
    private readonly string _serverName;
    private readonly string _ownershipToken;
    private readonly (int Id, DateTime CreateDate) _identity;
    private readonly bool _guiAllocation;
    private int _disposeState;
    private int _manifestWriter;
    private SqlTestOwnershipManifest? _ownershipManifest;

    private SqlTestDatabase(
        string adminConnectionString,
        Func<string, SqlConnection> connectionFactory,
        string databaseName,
        string serverName,
        string ownershipToken,
        (int Id, DateTime CreateDate) identity,
        bool guiAllocation)
    {
        _adminConnectionString = adminConnectionString;
        _connectionFactory = connectionFactory;
        _serverName = serverName;
        _ownershipToken = ownershipToken;
        _identity = identity;
        _guiAllocation = guiAllocation;
        DatabaseName = databaseName;
        ConnectionString = new SqlConnectionStringBuilder(adminConnectionString)
        {
            InitialCatalog = databaseName,
            Pooling = false,
        }.ConnectionString;
    }

    public string ConnectionString { get; }

    public string DatabaseName { get; }

    // Explicit R2f route only. Never call CreateCoreAsync/afterCreate: that default
    // route can use ordinary cleanup after a callback failure. This route retains.
    internal static async Task<SqlTestDatabase> CreateSupervisedAsync(string adminConnectionString, SqlTestSupervision supervision, CancellationToken cancellationToken = default)
    {
        SqlTestDatabase? database = null;
        try
        {
            var normalized = NormalizeAdminConnectionString(adminConnectionString);
            var name = SqlTestDatabaseName.Create(); var ownership = Guid.NewGuid().ToString("N");
            string server = "";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(LifecycleTimeout);
            using var connection = new SqlConnection(normalized);
            await supervision.Coordinator.AdvanceAsync(SqlTestSupervisionPhase.MasterBinding, async () => {
                await connection.OpenAsync(timeout.Token).ConfigureAwait(false);
                server = await ReadInstanceAsync(connection, timeout.Token).ConfigureAwait(false);
                supervision.RequireServer(server);
            }, timeout.Token).ConfigureAwait(false);
            await supervision.Coordinator.AdvanceAsync(SqlTestSupervisionPhase.Intent, () => { supervision.Intent(name, server, ownership); return Task.CompletedTask; }, timeout.Token).ConfigureAwait(false);
            await supervision.Coordinator.AdvanceAsync(SqlTestSupervisionPhase.Create, async () => {
                using var command=CreateCommand(connection,$"CREATE DATABASE [{name}];");
                await command.ExecuteNonQueryAsync(timeout.Token).ConfigureAwait(false);
            }, timeout.Token).ConfigureAwait(false);
            await supervision.Coordinator.AdvanceAsync(SqlTestSupervisionPhase.Mark, async () => {
                using var command=CreateCommand(connection,$"EXEC [{name}].sys.sp_addextendedproperty @name=N'MyDmsVn.TestOwnership',@value=@token;");
                command.Parameters.Add("@token",SqlDbType.NVarChar,32).Value=ownership;
                await command.ExecuteNonQueryAsync(timeout.Token).ConfigureAwait(false);
            }, timeout.Token).ConfigureAwait(false);
            await supervision.Coordinator.AdvanceAsync(SqlTestSupervisionPhase.LiveVerification, async () => {
                var identity=await ReadDatabaseIdentityAsync(connection,name,timeout.Token).ConfigureAwait(false);
                if(identity==null) throw new InvalidOperationException();
                database=new SqlTestDatabase(normalized,value=>new SqlConnection(value),name,server,ownership,identity.Value,false);
                using var live=new SqlConnection(database.ConnectionString); await live.OpenAsync(timeout.Token).ConfigureAwait(false);
                await database.VerifyOwnershipAsync(live,timeout.Token).ConfigureAwait(false);
            }, timeout.Token).ConfigureAwait(false);
            await supervision.Coordinator.AdvanceAsync(SqlTestSupervisionPhase.OriginalWitness, () => {
                // Original tuple never leaves the owner through a public raw getter.
                supervision.Original(database!._identity.Id,database._identity.CreateDate); return Task.CompletedTask;
            }, timeout.Token).ConfigureAwait(false);
            return database!;
        }
        catch(Exception error) { throw supervision.Coordinator.Fail(error); }
        // No automatic cleanup, including when CREATE succeeded but delivery failed.
    }

    internal async Task VerifySupervisedQuiescenceAsync(CancellationToken token)
    {
        using(var live=new SqlConnection(ConnectionString)) { await live.OpenAsync(token).ConfigureAwait(false); await VerifyOwnershipAsync(live,token).ConfigureAwait(false); }
        using var master=new SqlConnection(_adminConnectionString); await master.OpenAsync(token).ConfigureAwait(false);
        if(!string.Equals(await ReadInstanceAsync(master,token).ConfigureAwait(false),_serverName,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
        using var check=CreateCommand(master,"SELECT (SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE database_id=DB_ID(@name)) + (SELECT COUNT(*) FROM sys.dm_tran_locks WHERE resource_database_id=DB_ID(@name));");
        check.Parameters.Add("@name",SqlDbType.NVarChar,128).Value=DatabaseName;
        if(Convert.ToInt32(await check.ExecuteScalarAsync(token).ConfigureAwait(false))!=0) throw new InvalidOperationException();
    }
    internal async Task VerifySupervisedAbsenceAsync(CancellationToken token)
    {
        if(Volatile.Read(ref _disposeState)!=2) throw new InvalidOperationException();
        using var master=new SqlConnection(_adminConnectionString); await master.OpenAsync(token).ConfigureAwait(false);
        if(!string.Equals(await ReadInstanceAsync(master,token).ConfigureAwait(false),_serverName,StringComparison.OrdinalIgnoreCase) || await ReadDatabaseIdentityAsync(master,DatabaseName,token).ConfigureAwait(false)!=null) throw new InvalidOperationException();
    }

    // Original private identity is written only after verifying the actual live allocation.
    // This is a private attended-review record, never a cleanup capability or log DTO.
    internal async Task WriteOwnershipManifestAsync(SqlTestOwnershipManifest control, CancellationToken token)
    {
        if (Interlocked.CompareExchange(ref _manifestWriter, 1, 0) != 0)
            throw new InvalidOperationException("Allocation manifest writer is already active.");
        try
        {
            if (_ownershipManifest != null) throw new InvalidOperationException();
            using var connection = _connectionFactory(ConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);
            await VerifyOwnershipAsync(connection, token).ConfigureAwait(false);
            SealOriginalOwnership(control);
        }
        catch { throw new InvalidOperationException("Live allocation ownership/manifest seal was not confirmed; retain private control."); }
        finally { Volatile.Write(ref _manifestWriter, 0); }
    }

    private void SealOriginalOwnership(SqlTestOwnershipManifest control)
    {
        control.WriteVerifiedRecord(DatabaseName, _serverName, _identity.Id, _identity.CreateDate,
            _ownershipToken, _guiAllocation, DateTimeOffset.UtcNow);
        _ownershipManifest = control;
    }

    internal IReadOnlyDictionary<string, object> OriginalCutoverOwnership(SqlTestOwnershipManifest control)
    {
        if (!ReferenceEquals(control, _ownershipManifest))
            throw new InvalidOperationException("Cutover ownership was not delivered to this control.");
        control.Verify();
        return new Dictionary<string, object>
        {
            ["databaseName"] = DatabaseName,
            ["serverName"] = _serverName,
            ["databaseId"] = _identity.Id,
            ["createdAt"] = _identity.CreateDate.ToString("O", CultureInfo.InvariantCulture),
            ["originalToken"] = _ownershipToken,
        };
    }

    internal async Task CompleteOwnershipManifestAsync(SqlTestOwnershipManifest control, CancellationToken token)
    {
        try
        {
            if (Volatile.Read(ref _disposeState) != 2 || !ReferenceEquals(control, _ownershipManifest)) throw new InvalidOperationException();
            control.Verify();
            using var connection = _connectionFactory(_adminConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);
            if (!string.Equals(await ReadInstanceAsync(connection, token).ConfigureAwait(false), _serverName, StringComparison.OrdinalIgnoreCase) ||
                await ReadDatabaseIdentityAsync(connection, DatabaseName, token).ConfigureAwait(false) != null) throw new InvalidOperationException();
            control.RemoveAfterVerifiedAbsence();
        }
        catch { throw new InvalidOperationException("Owned SQL absence/control removal was not proved; retain private manifest."); }
    }

    public static Task<SqlTestDatabase> CreateAsync(
        string adminConnectionString,
        CancellationToken cancellationToken = default) =>
        CreateAsync(
            adminConnectionString,
            cancellationToken,
            value => new SqlConnection(value),
            _ => { });

    public static Task<SqlTestDatabase> CreateGuiAsync(
        string adminConnectionString,
        CancellationToken cancellationToken = default) =>
        CreateCoreAsync(adminConnectionString, cancellationToken,
            value => new SqlConnection(value), _ => { }, guiAllocation: true);

    internal static Task<SqlTestDatabase> CreateAsync(
        string adminConnectionString,
        CancellationToken cancellationToken,
        Func<string, SqlConnection> connectionFactory,
        Action<string> afterCreate) =>
        CreateCoreAsync(adminConnectionString, cancellationToken, connectionFactory, afterCreate, guiAllocation: false);

    // Supervisor records the exact intended allocation before any SQL side effect.
    internal static Task<SqlTestDatabase> CreateForSupervisorAsync(string adminConnectionString, Action<string> beforeCreate, CancellationToken cancellationToken = default)
    {
        if (beforeCreate is null) throw new ArgumentNullException(nameof(beforeCreate));
        return CreateCoreAsync(adminConnectionString, cancellationToken, value => new SqlConnection(value), _ => { }, guiAllocation: false, beforeCreate: beforeCreate);
    }

    // The caller supplied a create-new private intent writer and retains uncertainty.
    // Deliver a database only after the original live tuple has been durably sealed.
    internal static Task<SqlTestDatabase> CreateWithRetainedOwnershipAsync(string adminConnectionString,
        SqlTestOwnershipManifest control, Action<string, string, string> sealIntent, CancellationToken cancellationToken = default) =>
        CreateCoreAsync(adminConnectionString, cancellationToken, value => new SqlConnection(value), _ => { },
            guiAllocation: false, retainedControl: control, sealIntent: sealIntent);

    private static async Task<SqlTestDatabase> CreateCoreAsync(
        string adminConnectionString,
        CancellationToken cancellationToken,
        Func<string, SqlConnection> connectionFactory,
        Action<string> afterCreate,
        bool guiAllocation, Action<string>? beforeCreate = null,
        SqlTestOwnershipManifest? retainedControl = null, Action<string, string, string>? sealIntent = null)
    {
        var normalizedConnectionString = NormalizeAdminConnectionString(adminConnectionString);
        if (connectionFactory is null)
        {
            throw new ArgumentNullException(nameof(connectionFactory));
        }

        if (afterCreate is null)
        {
            throw new ArgumentNullException(nameof(afterCreate));
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(LifecycleTimeout);
        var token = timeout.Token;
        var databaseName = guiAllocation ? SqlTestDatabaseName.CreateGui() : SqlTestDatabaseName.Create();
        EnsureName(databaseName, guiAllocation);
        beforeCreate?.Invoke(databaseName);
        var ownershipToken = Guid.NewGuid().ToString("N");
        SqlTestDatabase? database = null;
        var createAttempted = false;
        try
        {
            using var connection = connectionFactory(normalizedConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);
            var serverName = await ReadInstanceAsync(connection, token).ConfigureAwait(false);

            sealIntent?.Invoke(databaseName, serverName, ownershipToken);
            createAttempted = true;
            using (var create = CreateCommand(connection, $"CREATE DATABASE [{databaseName}];"))
            {
                await create.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            using (var mark = CreateCommand(
                connection,
                $"EXEC [{databaseName}].sys.sp_addextendedproperty " +
                "@name = N'MyDmsVn.TestOwnership', @value = @ownershipToken;"))
            {
                mark.Parameters.Add("@ownershipToken", SqlDbType.NVarChar, 32).Value = ownershipToken;
                await mark.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            var identity = await ReadDatabaseIdentityAsync(connection, databaseName, token)
                .ConfigureAwait(false);
            var marker = await ReadOwnershipMarkerAsync(connection, databaseName, token)
                .ConfigureAwait(false);
            if (identity is null || !string.Equals(marker, ownershipToken, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The created database ownership could not be confirmed.");
            }

            database = new SqlTestDatabase(
                normalizedConnectionString,
                connectionFactory,
                databaseName,
                serverName,
                ownershipToken,
                identity.Value,
                guiAllocation);
            if (retainedControl != null)
                database.SealOriginalOwnership(retainedControl);
            afterCreate(databaseName);
            return database;
        }
        catch (Exception provisioningError)
        {
            if (retainedControl != null)
                throw new InvalidOperationException("Cutover allocation or ownership delivery failed. Retain its control and any sealed intent.", provisioningError);
            if (database is not null)
            {
                try
                {
                    await database.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception cleanupError)
                {
                    throw new AggregateException(
                        $"Provisioning and guarded cleanup both failed for SQL test database '{databaseName}'.",
                        provisioningError,
                        cleanupError);
                }
            }
            else if (createAttempted)
            {
                throw new InvalidOperationException(
                    $"Provisioning did not confirm ownership of SQL test database '{databaseName}'. " +
                    "Inspect the allocation manually; no unverified database was dropped.",
                    provisioningError);
            }

            throw;
        }
    }

    // Validate the actual reset connection, not just a name or connection-string target.
    internal async Task VerifyOwnershipAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            throw new InvalidOperationException("The SQL test allocation is disposing or disposed.");
        }

        EnsureName(DatabaseName, _guiAllocation);
        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Ownership verification requires an open connection.");
        }

        using var command = CreateCommand(connection,
            "SELECT DB_NAME(), CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')), " +
            "database_id, create_date, " +
            "(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties " +
            "WHERE class=0 AND name=N'MyDmsVn.TestOwnership') " +
            "FROM sys.databases WHERE database_id=DB_ID();");
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ||
            reader.IsDBNull(0) || reader.IsDBNull(1) || reader.IsDBNull(4) ||
            !string.Equals(reader.GetString(0), DatabaseName, StringComparison.Ordinal) ||
            !string.Equals(reader.GetString(1), _serverName, StringComparison.OrdinalIgnoreCase) ||
            reader.GetInt32(2) != _identity.Id || reader.GetDateTime(3) != _identity.CreateDate ||
            !string.Equals(reader.GetString(4), _ownershipToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing reset: the live SQL allocation ownership does not match.");
        }
    }

    public void Dispose() => DisposeAsync().GetAwaiter().GetResult();

    public Task DisposeAsync() => DisposeCoreAsync(requireQuiescence: false);

    // Supervisor-only: never disconnect a late session or roll back its work.
    internal Task DisposeIfQuiescentAsync() => DisposeCoreAsync(requireQuiescence: true);

    private async Task DisposeCoreAsync(bool requireQuiescence)
    {
        var previousState = Interlocked.CompareExchange(ref _disposeState, 1, 0);
        if (previousState == 2)
        {
            return;
        }

        if (previousState == 1)
        {
            throw new InvalidOperationException("SQL test database cleanup is already in progress.");
        }

        try
        {
            EnsureName(DatabaseName, _guiAllocation);
            using var timeout = new CancellationTokenSource(LifecycleTimeout);
            var token = timeout.Token;
            using var connection = _connectionFactory(_adminConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);
            var serverName = await ReadInstanceAsync(connection, token).ConfigureAwait(false);
            if (!string.Equals(serverName, _serverName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Refusing cleanup of '{DatabaseName}' on a different SQL Server instance.");
            }

            var identity = await ReadDatabaseIdentityAsync(connection, DatabaseName, token)
                .ConfigureAwait(false);
            if (identity is null)
            {
                Volatile.Write(ref _disposeState, 2);
                return;
            }

            if (identity.Value.Id != _identity.Id || identity.Value.CreateDate != _identity.CreateDate)
            {
                throw new InvalidOperationException(
                    $"Refusing cleanup of '{DatabaseName}': the database identity has changed.");
            }

            var marker = await ReadOwnershipMarkerAsync(connection, DatabaseName, token)
                .ConfigureAwait(false);
            if (!string.Equals(marker, _ownershipToken, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Refusing cleanup of '{DatabaseName}': the database ownership marker has changed or is missing.");
            }

            if (requireQuiescence)
            {
                using var check = CreateCommand(connection,
                    "SELECT (SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE database_id=DB_ID(@name)) + " +
                    "(SELECT COUNT(*) FROM sys.dm_tran_locks WHERE resource_database_id=DB_ID(@name));");
                check.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = DatabaseName;
                if (Convert.ToInt32(await check.ExecuteScalarAsync(token).ConfigureAwait(false)) != 0)
                    throw new InvalidOperationException("Refusing cleanup: owned SQL sessions or locks remain.");
            }
            using var drop = CreateCommand(connection, requireQuiescence
                ? $"DROP DATABASE [{DatabaseName}];"
                : $"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}];");
            await drop.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            Volatile.Write(ref _disposeState, 2);
        }
        catch
        {
            Volatile.Write(ref _disposeState, 0);
            throw;
        }
    }

    private static void EnsureName(string name, bool guiAllocation)
    {
        if (guiAllocation) SqlTestDatabaseName.EnsureGuiDisposable(name);
        else SqlTestDatabaseName.EnsureDisposable(name);
    }

    private static string NormalizeAdminConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("A SQL Server connection string targeting master is required.");
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) ||
            !string.Equals(builder.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(builder.AttachDBFilename) ||
            builder.UserInstance)
        {
            throw new InvalidOperationException(
                "SQL test administration requires an explicit SQL Server instance and master database without attachment.");
        }

        builder.InitialCatalog = "master";
        builder.Pooling = false;
        builder.ConnectRetryCount = 0;
        builder.ConnectTimeout = builder.ConnectTimeout == 0
            ? CommandTimeoutSeconds
            : Math.Min(builder.ConnectTimeout, CommandTimeoutSeconds);
        return builder.ConnectionString;
    }

    private static async Task<string> ReadInstanceAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        using var command = CreateCommand(
            connection,
            "SELECT DB_NAME(), CONVERT(nvarchar(128), SERVERPROPERTY('ServerName'));");
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ||
            reader.IsDBNull(0) || reader.IsDBNull(1) ||
            !string.Equals(reader.GetString(0), "master", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(reader.GetString(1)))
        {
            throw new InvalidOperationException(
                "SQL test administration could not verify the live master database and server identity.");
        }

        return reader.GetString(1);
    }

    private static async Task<(int Id, DateTime CreateDate)?> ReadDatabaseIdentityAsync(
        SqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        using var command = CreateCommand(
            connection,
            "SELECT database_id, create_date FROM sys.databases WHERE name = @databaseName;");
        command.Parameters.Add("@databaseName", SqlDbType.NVarChar, 128).Value = databaseName;
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (reader.GetInt32(0), reader.GetDateTime(1));
    }

    private static async Task<string?> ReadOwnershipMarkerAsync(
        SqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        using var command = CreateCommand(
            connection,
            $"SELECT CONVERT(nvarchar(128), value) FROM [{databaseName}].sys.extended_properties " +
            "WHERE class = 0 AND name = N'MyDmsVn.TestOwnership';");
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    private static SqlCommand CreateCommand(SqlConnection connection, string commandText) =>
        new SqlCommand(commandText, connection) { CommandTimeout = CommandTimeoutSeconds };
}

internal sealed class SqlTestOwnershipManifest : IDisposable
{
    private readonly string _rootIdentity;
    private readonly string _rootAcl;
    private readonly byte[] _rootDescriptor;
    private readonly bool _evidencePolicyProbe;
    private SafeFileHandle? _root;
    private FileStream? _file;
    private string? _fileIdentity;
    private string? _fileAcl;
    private byte[]? _fileDescriptor;
    private byte[]? _sealed;
    private bool _removed;
    private bool _aclMutationAttempted;
    private AclMutationRecord? _aclMutationRecord;
    private OwnedAclParent? _dedicatedParent;
    internal string ControlPath { get; }
    private string ManifestPath => Path.Combine(ControlPath, "allocation.txt");

    private SqlTestOwnershipManifest(string path, SafeFileHandle root, bool evidencePolicyProbe)
    {
        ControlPath = path; _root = root; _rootIdentity = Identity(root);
        _evidencePolicyProbe = evidencePolicyProbe;
        var acl = Directory.GetAccessControl(path, SqlTestPrivateRecord.Sections);
        _rootAcl = acl.GetSecurityDescriptorSddlForm(SqlTestPrivateRecord.Sections);
        _rootDescriptor = acl.GetSecurityDescriptorBinaryForm();
    }

    internal static SqlTestOwnershipManifest Create(string parent) => CreateCore(parent, evidencePolicyProbe: false);

    // The existing evidence guard requires exactly operator+Administrators. This variant
    // keeps the same original-identity/outer-record protocol and a compatible read lease.
    internal static SqlTestOwnershipManifest CreateEvidencePolicyProbe(string parent) => CreateCore(parent, evidencePolicyProbe: true);

    private static SqlTestOwnershipManifest CreateCore(string parent, bool evidencePolicyProbe)
    {
        using var creation = new RootCreation(parent, evidencePolicyProbe);
        try
        {
            foreach (var phase in RootCreation.Phases) creation.Execute(phase);
            return creation.TakeResult();
        }
        catch { throw new IOException("Private allocation control root was refused."); }
    }

    // Shared by ordinary manifests and the independently sealed sibling. It owns handles,
    // never deletes on failure, and exposes one fallible native substage at a time.
    private sealed class RootCreation : IDisposable
    {
        internal static readonly SqlTestAclPhase[] Phases = {
            SqlTestAclPhase.SiblingPathAdmission, SqlTestAclPhase.SiblingAncestors, SqlTestAclPhase.SiblingDescriptor,
            SqlTestAclPhase.SiblingCreate, SqlTestAclPhase.SiblingAclNormalize, SqlTestAclPhase.SiblingRootOpen,
            SqlTestAclPhase.SiblingRootCapture };
        private readonly string _parent;
        private readonly bool _probe;
        private string _full = "", _path = "";
        private DirectorySecurity? _acl;
        private SafeFileHandle? _handle;
        private SqlTestOwnershipManifest? _result;
        internal RootCreation(string parent, bool probe) { _parent = parent; _probe = probe; }
        internal void Execute(SqlTestAclPhase phase)
        {
            switch (phase)
            {
                case SqlTestAclPhase.SiblingPathAdmission:
                    _full = Path.GetFullPath(_parent).TrimEnd('\\');
                    if (!Path.IsPathRooted(_parent) || !string.Equals(_full, _parent.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) || _full.Length + 57 > 240)
                        throw new IOException();
                    _path = Path.Combine(_full, "sql-owner-" + Guid.NewGuid().ToString("N"));
                    break;
                case SqlTestAclPhase.SiblingAncestors: CheckAncestors(_full); break;
                case SqlTestAclPhase.SiblingDescriptor:
                    using (var user = WindowsIdentity.GetCurrent()) _acl = SqlTestPrivateRecord.DirectoryAcl(user.User!);
                    if (_probe) _acl.AddAccessRule(new FileSystemAccessRule(
                        new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, AccessControlType.Allow));
                    break;
                case SqlTestAclPhase.SiblingCreate:
                    CreateProtectedDirectory(_path, _acl!);
                    break;
                case SqlTestAclPhase.SiblingAclNormalize: Directory.SetAccessControl(_path, _acl!); break;
                case SqlTestAclPhase.SiblingRootOpen: _handle = OpenRoot(_path, _probe, deleteAccess: !_probe); break;
                case SqlTestAclPhase.SiblingRootCapture: _result = new SqlTestOwnershipManifest(_path, _handle!, _probe); break;
                default: throw new InvalidOperationException();
            }
        }
        internal SqlTestOwnershipManifest TakeResult()
        { var result = _result ?? throw new InvalidOperationException(); _result = null; _handle = null; return result; }
        public void Dispose() { _result?.Dispose(); _handle?.Dispose(); }
    }

    private static void CreateProtectedDirectory(string path, DirectorySecurity acl)
    {
        var bytes = acl.GetSecurityDescriptorBinaryForm(); var descriptor = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, descriptor, bytes.Length);
            var attributes = new SecurityAttributes { Length = Marshal.SizeOf(typeof(SecurityAttributes)), Descriptor = descriptor };
            if (!CreateDirectory(path, ref attributes)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { Marshal.FreeHGlobal(descriptor); }
    }

    // Dedicated container, not an allocation-manifest object. Only this live owner can
    // bind a target or remove the original empty parent; Dispose never removes anything.
    internal sealed class OwnedAclParent : IDisposable
    {
        private readonly string _shared;
        internal string Path { get; }
        private readonly string _identity;
        private readonly byte[] _descriptor;
        private SafeFileHandle? _lease;
        private SqlTestOwnershipManifest? _target, _control;
        private readonly SqlTestAclParentLifecycle _lifecycle = new SqlTestAclParentLifecycle();
        private OwnedAclParent(string shared, string path, SafeFileHandle lease)
        {
            _shared = shared; Path = path; _lease = lease; _identity = Identity(lease);
            _descriptor = Directory.GetAccessControl(path, SqlTestPrivateRecord.Sections).GetSecurityDescriptorBinaryForm();
        }
        internal static OwnedAclParent Create(string shared)
        {
            SafeFileHandle? lease = null;
            try
            {
                var outer = System.IO.Path.GetFullPath(shared).TrimEnd('\\');
                if (!System.IO.Path.IsPathRooted(shared) || !string.Equals(outer, shared.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    throw new SqlTestAclParentPolicyException(SqlTestAclReason.InvalidParentBinding);
                var path = System.IO.Path.Combine(outer, "acl-scope-" + Guid.NewGuid().ToString("N"));
                SqlTestAclParentPolicy.ValidateLayout(outer, path, System.IO.Path.Combine(path, "target"), null);
                if (path.Length + 57 > 240) throw new IOException();
                CheckAncestors(outer);
                using var user = WindowsIdentity.GetCurrent();
                var acl = SqlTestPrivateRecord.DirectoryAcl(user.User!);
                CreateProtectedDirectory(path, acl);
                Directory.SetAccessControl(path, acl);
                // READ_ATTRIBUTES + share read/write (no DELETE). Compatible with the
                // independent authority's same read pin; neither lease authorizes removal.
                lease = OpenRoot(path, deleteAccess: false);
                var result = new OwnedAclParent(outer, path, lease);
                result.VerifyNative();
                SqlTestAclParentPolicy.RequireMembership(SqlTestAclMembership.Empty, path, "", null, Directory.GetFileSystemEntries);
                return result;
            }
            catch (Exception error) { lease?.Dispose(); throw new SqlTestAclPhaseException(SqlTestAclPhase.ParentAncestors, SqlTestAclReason.OperationFailed, error); }
        }
        internal SqlTestOwnershipManifest CreateTarget(bool evidencePolicyProbe = false)
        {
            try
            {
                _lifecycle.CreateTarget(() => {
                    VerifyNative();
                    SqlTestAclParentPolicy.RequireMembership(SqlTestAclMembership.Empty, Path, "", null, Directory.GetFileSystemEntries);
                }, () => {
                    _target = CreateCore(Path, evidencePolicyProbe);
                    _target._dedicatedParent = this;
                    SqlTestAclParentPolicy.ValidateLayout(_shared, Path, _target.ControlPath, null);
                });
                return _target!;
            }
            catch (Exception error) { throw new SqlTestAclPhaseException(SqlTestAclPhase.ParentAncestors, SqlTestAclReason.OperationFailed, error); }
        }
        internal void VerifyBoundTarget(SqlTestOwnershipManifest target)
        {
            _lifecycle.Verify(() => {
                if (!ReferenceEquals(_target, target) || !ReferenceEquals(target._dedicatedParent, this))
                    throw new SqlTestAclParentPolicyException(SqlTestAclReason.InvalidParentBinding);
                SqlTestAclParentPolicy.ValidateLayout(_shared, Path, target.ControlPath, _control?.ControlPath);
                VerifyNative();
            });
        }
        internal void BindSealedControl(SqlTestOwnershipManifest target, SqlTestOwnershipManifest control)
        {
            _lifecycle.SealControl(() => {
                VerifyBoundTarget(target);
                SqlTestAclParentPolicy.ValidateLayout(_shared, Path, target.ControlPath, control.ControlPath);
                control.VerifyCore();
                SqlTestAclParentPolicy.RequireMembership(SqlTestAclMembership.Prepared, Path, target.ControlPath, control.ControlPath, Directory.GetFileSystemEntries);
                _control = control;
            });
        }
        internal void CompleteChildren(SqlTestOwnershipManifest target, SqlTestOwnershipManifest control)
        {
            _lifecycle.ChildrenRemoved(() => {
                if (!ReferenceEquals(_target, target) || !ReferenceEquals(_control, control) || !target._removed || !control._removed)
                    throw new SqlTestAclParentPolicyException(SqlTestAclReason.InvalidParentBinding);
                VerifyNative();
                SqlTestAclParentPolicy.RequireMembership(SqlTestAclMembership.Empty, Path, target.ControlPath, control.ControlPath, Directory.GetFileSystemEntries);
            });
        }
        internal void RemoveAfterVerifiedChildren()
        {
            try
            {
                _lifecycle.Remove(() => {
                    VerifyNative();
                    SqlTestAclParentPolicy.RequireMembership(SqlTestAclMembership.Empty, Path, _target!.ControlPath, _control!.ControlPath, Directory.GetFileSystemEntries);
                }, () => {
                    // All child and authority pins have drained. Reopen DELETE only now,
                    // then freshly prove original native identity/O/G/A/ancestry/emptiness.
                    _lease!.Dispose(); _lease = null;
                    _lease = OpenRoot(Path, deleteAccess: true);
                    VerifyNative();
                    SqlTestAclParentPolicy.RequireMembership(SqlTestAclMembership.Empty, Path, _target!.ControlPath, _control!.ControlPath, Directory.GetFileSystemEntries);
                    DeleteExactHandle(_lease); _lease.Dispose(); _lease = null;
                    if (Directory.Exists(Path) || File.Exists(Path)) throw new IOException();
                });
            }
            catch (Exception error) { throw new SqlTestAclPhaseException(SqlTestAclPhase.Restoration, SqlTestAclReason.OperationFailed, error); }
        }
        private void VerifyNative()
        {
            if (_lease == null || _lease.IsClosed || _lease.IsInvalid) throw new SqlTestAclParentPolicyException(SqlTestAclReason.ParentIdentityMismatch);
            try { CheckAncestors(Path); }
            catch (Exception error) { throw new SqlTestAclParentPolicyException(SqlTestAclReason.ParentIdentityMismatch, error); }
            using var fresh = OpenDirectoryHandle(Path);
            var acl = Directory.GetAccessControl(Path, SqlTestPrivateRecord.Sections);
            var descriptor = acl.GetSecurityDescriptorBinaryForm();
            SqlTestAclParentPolicy.VerifyIdentity(_identity, _descriptor, Identity(_lease), descriptor, acl.AreAccessRulesProtected, false);
            SqlTestAclParentPolicy.VerifyIdentity(_identity, _descriptor, Identity(fresh), descriptor, acl.AreAccessRulesProtected, false);
        }
        public void Dispose() { _lifecycle.Abandon(); _lease?.Dispose(); _lease = null; }
    }

    internal void WriteVerifiedRecord(string database, string server, int id, DateTime created, string token, bool gui, DateTimeOffset utc)
    {
        if (_sealed != null || _removed) throw new InvalidOperationException("Allocation manifest is create-new only.");
        try
        {
            if (gui) SqlTestDatabaseName.EnsureGuiDisposable(database); else SqlTestDatabaseName.EnsureDisposable(database);
            if (string.IsNullOrWhiteSpace(server) || server.Length > 128 || id <= 4 || created == default ||
                !Guid.TryParseExact(token, "N", out var ownership) || ownership == Guid.Empty || utc == default || utc.Offset != TimeSpan.Zero)
                throw new IOException();
            BeginRecord();
            // Fixed field order, invariant scalars, base64 UTF-8 text, LF terminators.
            var record = "version=1\nallocationKind=" + (gui ? "gui" : "sql") +
                "\ndatabase=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(database)) +
                "\nserver=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(server)) +
                "\ndatabaseId=" + id.ToString(CultureInfo.InvariantCulture) +
                "\ncreateDateTicks=" + created.Ticks.ToString(CultureInfo.InvariantCulture) +
                "\nownershipToken=" + token + "\nutc=" + utc.ToString("O", CultureInfo.InvariantCulture) +
                "\ncontrolIdentity=" + _rootIdentity + "\ncontrolAcl=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(_rootAcl)) +
                "\nfileIdentity=" + _fileIdentity + "\nfileAcl=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(_fileAcl)) + "\n";
            FinishRecord(record);
        }
        catch { throw new IOException("Private allocation manifest could not be sealed; retain control root."); }
    }

    internal void Verify()
    {
        try { VerifyCore(); }
        catch { throw new IOException("Private allocation manifest identity or content changed; retain control root."); }
    }

    internal string VerifiedDigest()
    {
        Verify();
        return SqlTestPrivateRecord.Hash(_sealed!);
    }

    private void VerifyCore()
    {
        if (_removed || _sealed == null) throw new IOException();
        if (_aclMutationAttempted)
        {
            if (_aclMutationRecord == null) throw new IOException();
            _aclMutationRecord.VerifyIndependentRecord(casePresent: true);
        }
        VerifyContents();
    }

    private void VerifyContents()
    {
        if (_removed || _sealed == null) throw new IOException();
        VerifyRoot();
        var entries = Directory.GetFileSystemEntries(ControlPath);
        if (entries.Length != 1 || !string.Equals(entries[0], ManifestPath, StringComparison.OrdinalIgnoreCase) ||
            (File.GetAttributes(ManifestPath) & FileAttributes.ReparsePoint) != 0) throw new IOException();
        _file ??= OpenManifest(createNew: false);
        if (Identity(_file.SafeFileHandle) != _fileIdentity ||
            File.GetAccessControl(ManifestPath, SqlTestPrivateRecord.Sections).GetSecurityDescriptorSddlForm(SqlTestPrivateRecord.Sections) != _fileAcl || _file.Length != _sealed.Length)
            throw new IOException();
        _file.Position = 0;
        var actual = new byte[_sealed.Length]; var offset = 0;
        while (offset < actual.Length) { var read = _file.Read(actual, offset, actual.Length - offset); if (read == 0) throw new IOException(); offset += read; }
        if (!actual.SequenceEqual(_sealed)) throw new IOException();
        SqlTestPrivateRecord.Validate(actual);
    }

    private void VerifyRoot()
    {
        CheckAncestors(ControlPath);
        _root ??= OpenRoot(ControlPath, _evidencePolicyProbe, deleteAccess: !_evidencePolicyProbe);
        using var fresh = OpenDirectoryHandle(ControlPath);
        if (Identity(_root) != _rootIdentity || Identity(fresh) != _rootIdentity ||
            Directory.GetAccessControl(ControlPath, SqlTestPrivateRecord.Sections).GetSecurityDescriptorSddlForm(SqlTestPrivateRecord.Sections) != _rootAcl)
            throw new IOException("Private allocation control identity changed.");
    }

    // Only called by the original owner after same-server absence; Dispose never deletes.
    internal void RemoveAfterVerifiedAbsence()
    {
        try
        {
            Verify();
            DeleteExactHandle(_file!.SafeFileHandle);
            _file.Dispose(); _file = null;
            if (Directory.GetFileSystemEntries(ControlPath).Length != 0) throw new IOException();
            VerifyRoot();
            if (_evidencePolicyProbe)
            {
                // The evidence policy's read lease cannot share an existing DELETE handle.
                // Reopen with DELETE only after that probe drains, then revalidate the exact
                // original native identity/ACL before touching the newly opened object.
                _root!.Dispose(); _root = null;
                _root = OpenRoot(ControlPath, evidencePolicyProbe: true, deleteAccess: true);
                VerifyRoot();
            }
            DeleteExactHandle(_root!); _root!.Dispose(); _root = null;
            if (Directory.Exists(ControlPath) || File.Exists(ControlPath)) throw new IOException();
            _removed = true;
            _aclMutationRecord?.CompleteCaseRemoval();
        }
        catch { throw new IOException("Private allocation/control removal was not verified; retain remaining records."); }
    }

    public void Dispose() { _file?.Dispose(); _file = null; _root?.Dispose(); _root = null; _aclMutationRecord?.Dispose(); }

    private void BeginRecord() { CreateRecord(); CaptureRecordAcl(); }

    private void CreateRecord()
    {
        VerifyRoot();
        if (_sealed != null || _file != null || Directory.GetFileSystemEntries(ControlPath).Length != 0) throw new IOException();
        _file = OpenManifest(createNew: true);
    }

    private void CaptureRecordAcl()
    {
        using var user = WindowsIdentity.GetCurrent();
        File.SetAccessControl(ManifestPath, SqlTestPrivateRecord.FileAcl(user.User!));
        _fileIdentity = Identity(_file!.SafeFileHandle);
        var acl = File.GetAccessControl(ManifestPath, SqlTestPrivateRecord.Sections);
        _fileAcl = acl.GetSecurityDescriptorSddlForm(SqlTestPrivateRecord.Sections);
        _fileDescriptor = acl.GetSecurityDescriptorBinaryForm();
    }

    private void FinishRecord(string record)
    {
        var payload = SqlTestPrivateRecord.Seal(record);
        _file!.Write(payload, 0, payload.Length); _file.Flush(true);
        _sealed = payload;
        Verify(); // Readback through the exact still-open file, plus native identity/ACL/hash.
    }

    private FileStream OpenManifest(bool createNew)
    {
        // DELETE access is held from admission, while share-delete/write are denied.
        // Normal cleanup marks this exact verified object for deletion without a path race.
        var handle = CreateFile(ManifestPath, createNew ? 0xC0010000U : 0x80010000U, 1, IntPtr.Zero,
            createNew ? 1U : 3U, 0x80200000, IntPtr.Zero);
        if (handle.IsInvalid) { var error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new System.ComponentModel.Win32Exception(error); }
        try { RejectReparse((FileAttributes)ReadAttributes(handle)); return new FileStream(handle, createNew ? FileAccess.ReadWrite : FileAccess.Read); }
        catch { handle.Dispose(); throw; }
    }

    private static void DeleteExactHandle(SafeFileHandle handle)
    {
        var disposition = new FileDisposition { Delete = true };
        if (!SetFileInformationByHandle(handle, 4, ref disposition, (uint)Marshal.SizeOf(typeof(FileDisposition)))) throw new IOException();
    }

    private static uint ReadAttributes(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var information)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return information.Attributes;
    }

    // An original live owner may prepare one independent record. There is deliberately no
    // API that rehydrates ownership or cleans up an allocation from a persisted record.
    internal AclMutationRecord PreserveBeforeAclMutation(OwnedAclParent owner, Action<string>? diagnostic = null)
    {
        var preparation = new AclPreparation(this, owner);
        var coordinator = new SqlTestAclMutationCoordinator(preparation, diagnostic);
        try
        {
            coordinator.Prepare();
            var result = preparation.Result!;
            result._coordinator = coordinator;
            return result;
        }
        catch { preparation.Dispose(); throw; } // Handle drain only; uncertain cases remain retained.
    }

    private sealed class AclPreparation : ISqlTestAclPreparation, IDisposable
    {
        private readonly SqlTestOwnershipManifest _target;
        private readonly OwnedAclParent _owner;
        private string _parent = "";
        private DirectorySecurity? _parentAcl;
        private SafeFileHandle? _parentHandle;
        private RootCreation? _creation;
        private SqlTestOwnershipManifest? _outside;
        private byte[]? _payload;
        private bool _recordSealed;
        internal AclMutationRecord? Result;
        internal AclPreparation(SqlTestOwnershipManifest target, OwnedAclParent owner)
        { _target = target; _owner = owner ?? throw new SqlTestAclParentPolicyException(SqlTestAclReason.InvalidParentBinding); }
        public bool IsSealed => _recordSealed && _outside?._sealed != null &&
            Result != null && ReferenceEquals(_target._aclMutationRecord, Result);
        public void Execute(SqlTestAclPhase phase)
        {
            switch (phase)
            {
                case SqlTestAclPhase.CaseVerify: _target.VerifyCore(); break;
                case SqlTestAclPhase.AttemptAdmission:
                    if (_target._aclMutationAttempted) throw new IOException();
                    _target._aclMutationAttempted = true;
                    break;
                case SqlTestAclPhase.ParentAncestors:
                    _owner.VerifyBoundTarget(_target); _parent = _owner.Path; CheckAncestors(_parent); break;
                case SqlTestAclPhase.ParentAclRead: _parentAcl = Directory.GetAccessControl(_parent, SqlTestPrivateRecord.Sections); break;
                case SqlTestAclPhase.ParentAclPolicy:
                    using (var user = WindowsIdentity.GetCurrent())
                    {
                        if (!_parentAcl!.AreAccessRulesProtected || !user.User!.Equals(_parentAcl.GetOwner(typeof(SecurityIdentifier)))) throw new IOException();
                        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                        foreach (FileSystemAccessRule rule in _parentAcl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                            if (rule.AccessControlType != AccessControlType.Allow || (!user.User.Equals(rule.IdentityReference) && !administrators.Equals(rule.IdentityReference))) throw new IOException();
                    }
                    _owner.VerifyBoundTarget(_target);
                    _target.VerifyContents();
                    break;
                case SqlTestAclPhase.ParentMembership:
                    SqlTestAclParentPolicy.RequireMembership(SqlTestAclMembership.TargetOnly, _parent, _target.ControlPath, null, Directory.GetFileSystemEntries);
                    break;
                case SqlTestAclPhase.ParentPinOpen: _parentHandle = OpenDirectoryHandle(_parent, pinAgainstRename: true); break;
                case SqlTestAclPhase.SiblingPathAdmission:
                    _creation = new RootCreation(_parent, false); _creation.Execute(phase); break;
                case SqlTestAclPhase.SiblingAncestors:
                case SqlTestAclPhase.SiblingDescriptor:
                case SqlTestAclPhase.SiblingCreate:
                case SqlTestAclPhase.SiblingAclNormalize:
                case SqlTestAclPhase.SiblingRootOpen: _creation!.Execute(phase); break;
                case SqlTestAclPhase.SiblingRootCapture:
                    _creation!.Execute(phase); _outside = _creation.TakeResult();
                    Result = new AclMutationRecord(_target, _outside, _owner, _parent, _parentHandle!, _parentAcl!.GetSecurityDescriptorBinaryForm());
                    break;
                case SqlTestAclPhase.RecordCreate: _outside!.CreateRecord(); break;
                case SqlTestAclPhase.RecordAclCapture: _outside!.CaptureRecordAcl(); break;
                case SqlTestAclPhase.RecordSerialize: _payload = SqlTestPrivateRecord.Seal(Result!.Serialize()); break;
                case SqlTestAclPhase.RecordWrite: _outside!._file!.Write(_payload!, 0, _payload!.Length); break;
                case SqlTestAclPhase.RecordFlush: _outside!._file!.Flush(true); break;
                case SqlTestAclPhase.RecordReadback:
                    _outside!._sealed = _payload; _outside.VerifyCore(); break;
                case SqlTestAclPhase.RecordSeal: _owner.BindSealedControl(_target, _outside!); _recordSealed = true; break;
                case SqlTestAclPhase.IndependentVerify: Result!.VerifyIndependentRecord(casePresent: true); break;
                case SqlTestAclPhase.AuthorityRegister: _target._aclMutationRecord = Result; break;
                case SqlTestAclPhase.AuthorityReverify: Result!.VerifyIndependentRecord(casePresent: true); break;
                case SqlTestAclPhase.ReadyForMutation: if (!IsSealed) throw new IOException(); break;
                case SqlTestAclPhase.FreshVerify: Result!.VerifyIndependentRecord(casePresent: true); break;
                case SqlTestAclPhase.Restoration: Result!.RestoreOriginalAcl(); break;
                default: throw new InvalidOperationException();
            }
        }
        public void Dispose() { _creation?.Dispose(); _outside?.Dispose(); _parentHandle?.Dispose(); }
    }

    internal sealed class AclMutationRecord : IDisposable
    {
        private readonly SqlTestOwnershipManifest _case;
        private readonly SqlTestOwnershipManifest _outside;
        private readonly OwnedAclParent _owner;
        private readonly string _parent;
        private readonly string _parentIdentity;
        private readonly byte[] _parentDescriptor;
        private readonly SafeFileHandle _parentHandle;
        private bool _completed;
        internal SqlTestAclMutationCoordinator? _coordinator;

        internal AclMutationRecord(SqlTestOwnershipManifest target, SqlTestOwnershipManifest outside, OwnedAclParent owner,
            string parent, SafeFileHandle parentHandle, byte[] parentDescriptor)
        {
            _case = target; _outside = outside; _owner = owner; _parent = parent; _parentHandle = parentHandle;
            _parentIdentity = Identity(parentHandle); _parentDescriptor = parentDescriptor;
        }

        internal string Serialize()
        {
            // Recovery facts remain private in the independent sibling, never diagnostics.
            return "version=1\nkind=acl-mutation-control\nutc=" + DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) +
                    "\nrootPath=" + Text(_parent) + "\nrootIdentity=" + _parentIdentity + "\nrootDescriptor=" + Bytes(_parentDescriptor) +
                    "\nrootNonReparse=1\ncasePath=" + Text(_case.ControlPath) + "\ncaseIdentity=" + _case._rootIdentity +
                    "\ncaseDescriptor=" + Bytes(_case._rootDescriptor) + "\ncaseNonReparse=1\nfileIdentity=" + _case._fileIdentity +
                    "\nfileDescriptor=" + Bytes(_case._fileDescriptor!) + "\nfileNonReparse=1\nmanifestBytes=" + Bytes(_case._sealed!) +
                    "\nmanifestHash=" + SqlTestPrivateRecord.Hash(_case._sealed!) + "\nouterPath=" + Text(_outside.ControlPath) +
                    "\nouterIdentity=" + _outside._rootIdentity + "\nouterDescriptor=" + Bytes(_outside._rootDescriptor) +
                    "\nrecordIdentity=" + _outside._fileIdentity + "\nrecordDescriptor=" + Bytes(_outside._fileDescriptor!) +
                    "\nexpectedMembership=" + Text(Path.GetFileName(_case.ControlPath) + "\n" + Path.GetFileName(_outside.ControlPath)) + "\n";
        }

        internal void VerifyIndependentRecord(bool casePresent)
        {
            if (_completed) throw new IOException();
            _outside.VerifyCore();
            _owner.VerifyBoundTarget(_case);
            CheckAncestors(_parent);
            using var fresh = OpenDirectoryHandle(_parent);
            if (Identity(_parentHandle) != _parentIdentity || Identity(fresh) != _parentIdentity ||
                !Directory.GetAccessControl(_parent, SqlTestPrivateRecord.Sections).GetSecurityDescriptorBinaryForm().SequenceEqual(_parentDescriptor)) throw new IOException();
            SqlTestAclParentPolicy.RequireMembership(casePresent ? SqlTestAclMembership.Prepared : SqlTestAclMembership.TargetRemoved,
                _parent, _case.ControlPath, _outside.ControlPath, Directory.GetFileSystemEntries);
        }

        internal void RestoreOriginalAcl()
        {
            VerifyIndependentRecord(casePresent: true);
            var fields = SqlTestPrivateRecord.Validate(_outside._sealed!).TrimEnd('\n').Split('\n')
                .Select(line => line.Split(new[] { '=' }, 2)).ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
            CheckAncestors(_case.ControlPath);
            using var fresh = OpenDirectoryHandle(_case.ControlPath);
            if (Identity(fresh) != fields["caseIdentity"] || _case._root == null || Identity(_case._root) != fields["caseIdentity"] ||
                _case._file == null || Identity(_case._file.SafeFileHandle) != fields["fileIdentity"]) throw new IOException();
            var entries = Directory.GetFileSystemEntries(_case.ControlPath);
            if (entries.Length != 1 || !string.Equals(entries[0], _case.ManifestPath, StringComparison.OrdinalIgnoreCase)) throw new IOException();
            RejectReparse(File.GetAttributes(_case.ManifestPath));
            var original = Convert.FromBase64String(fields["manifestBytes"]);
            if (SqlTestPrivateRecord.Hash(original) != fields["manifestHash"] || _case._file.Length != original.Length) throw new IOException();
            _case._file.Position = 0;
            var actual = new byte[original.Length]; var offset = 0;
            while (offset < actual.Length) { var read = _case._file.Read(actual, offset, actual.Length - offset); if (read == 0) throw new IOException(); offset += read; }
            if (!actual.SequenceEqual(original)) throw new IOException();
            // Fresh objects explicitly mark only Owner/Group/Access modified; never SACL.
            Directory.SetAccessControl(_case.ControlPath, SqlTestPrivateRecord.RestoreDirectory(Convert.FromBase64String(fields["caseDescriptor"])));
            File.SetAccessControl(_case.ManifestPath, SqlTestPrivateRecord.RestoreFile(Convert.FromBase64String(fields["fileDescriptor"])));
            _case.VerifyCore();
        }

        internal void RunMutation(Action mutation, Action assertion)
        {
            if (_coordinator == null) throw new SqlTestAclPhaseException(SqlTestAclPhase.SealGate, SqlTestAclReason.NotReady);
            _coordinator.RunMutation(mutation, assertion);
        }

        internal void CompleteCaseRemoval()
        {
            VerifyIndependentRecord(casePresent: false);
            if (Directory.Exists(_case.ControlPath) || File.Exists(_case.ControlPath)) throw new IOException();
            _outside.RemoveAfterVerifiedAbsence();
            _parentHandle.Dispose(); // Release read pin before the dedicated owner may acquire DELETE.
            _owner.CompleteChildren(_case, _outside);
            _completed = true;
        }

        public void Dispose() { _outside.Dispose(); _parentHandle.Dispose(); }
        private static string Text(string text) => Bytes(Encoding.UTF8.GetBytes(text));
        private static string Bytes(byte[] bytes) => Convert.ToBase64String(bytes);
    }

    private static void CheckAncestors(string path)
    {
        for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            RejectReparse(File.GetAttributes(current));
    }

    internal static void RejectReparse(FileAttributes attributes)
    {
        if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse allocation control path refused.");
    }

    private static SafeFileHandle OpenRoot(string path, bool evidencePolicyProbe = false, bool deleteAccess = true)
    {
        using var user = WindowsIdentity.GetCurrent();
        var acl = Directory.GetAccessControl(path, SqlTestPrivateRecord.Sections);
        if (!acl.AreAccessRulesProtected || !user.User!.Equals(acl.GetOwner(typeof(SecurityIdentifier)))) throw new IOException();
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType != AccessControlType.Allow ||
                (!user.User.Equals(rule.IdentityReference) && !(evidencePolicyProbe &&
                    new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Equals(rule.IdentityReference)))) throw new IOException();
        return OpenDirectoryHandle(path, pinAgainstRename: true, deleteAccess: deleteAccess);
    }

    private static SafeFileHandle OpenDirectoryHandle(string path, bool pinAgainstRename = false, bool deleteAccess = false)
    {
        var handle = CreateFile(path, deleteAccess ? 0x10080U : 0x80U, pinAgainstRename ? 3U : 7U, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (handle.IsInvalid) { var error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new System.ComponentModel.Win32Exception(error); }
        return handle;
    }

    private static string Identity(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var info)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        if ((info.Attributes & 0x400) != 0) throw new IOException();
        return info.Volume.ToString("X8") + ":" + info.IndexHigh.ToString("X8") + info.IndexLow.ToString("X8") + ":" + info.CreatedHigh.ToString("X8") + info.CreatedLow.ToString("X8");
    }
    // Controller retains ownership of this already-provisioned independent root.
    // Fixed record slots are append-only. Dispose closes handles, never deletes.
    internal sealed class SupervisionStore : IDisposable
    {
        private static readonly string[] Slots={"intent.record","original.record","complete.record"};
        private readonly SqlTestSupervisionConfiguration _config;
        private readonly SafeFileHandle _root;
        private readonly System.Collections.Generic.List<FileStream> _files=new System.Collections.Generic.List<FileStream>();
        private readonly System.Collections.Generic.List<byte[]> _records=new System.Collections.Generic.List<byte[]>();
        internal SupervisionStore(SqlTestSupervisionConfiguration config)
        {
            _config=config;
            var temp=Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\'); var root=config.Root;
            if(string.Equals(root,temp,StringComparison.OrdinalIgnoreCase)||root.StartsWith(temp+"\\",StringComparison.OrdinalIgnoreCase)||temp.StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase)) throw new IOException();
            CheckAncestors(root);
            var drive=new DriveInfo(Path.GetPathRoot(root)!);
            if(drive.DriveType!=DriveType.Fixed || drive.DriveFormat!="NTFS") throw new IOException();
            _root=OpenRoot(root,deleteAccess:false);
            try { Verify(); } catch { _root.Dispose(); throw; }
        }
        internal void Verify()
        {
            CheckAncestors(_config.Root);
            using var fresh=OpenDirectoryHandle(_config.Root);
            var acl=Directory.GetAccessControl(_config.Root,SqlTestPrivateRecord.Sections);
            var actualIdentity=SqlTestSupervisionBinding.Hash(Identity(fresh));
            var actualAcl=SqlTestPrivateRecord.Hash(acl.GetSecurityDescriptorBinaryForm());
            new SqlTestSupervisionDelivery(_config.Binding.RootIdentity,_config.Binding.RootAcl).Verify(actualIdentity,actualAcl,acl.AreAccessRulesProtected,false);
            if(Identity(_root)!=Identity(fresh)) throw new IOException();
            using var user=WindowsIdentity.GetCurrent();
            var rules=acl.GetAccessRules(true,true,typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
            if(!user.User!.Equals(acl.GetOwner(typeof(SecurityIdentifier)))||rules.Length!=1||rules[0].IsInherited||!user.User.Equals(rules[0].IdentityReference)||rules[0].AccessControlType!=AccessControlType.Allow||rules[0].FileSystemRights!=FileSystemRights.FullControl) throw new IOException();
            var names=Directory.GetFileSystemEntries(_config.Root);
            if(_files.Count!=_records.Count||names.Length!=_records.Count||!names.OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(Slots.Take(_records.Count).Select(x=>Path.Combine(_config.Root,x)).OrderBy(x=>x,StringComparer.Ordinal),StringComparer.Ordinal)) throw new IOException();
            for(var i=0;i<_records.Count;i++)
            {
                var path=Path.Combine(_config.Root,Slots[i]); CheckAncestors(path);
                using var current=CreateFile(path,0x80U,3,IntPtr.Zero,3,0x00200000,IntPtr.Zero);
                if(current.IsInvalid||Identity(current)!=Identity(_files[i].SafeFileHandle)) throw new IOException();
                var fileAcl=File.GetAccessControl(path,SqlTestPrivateRecord.Sections);
                SqlTestSupervisionProtocol.Delivery(_records[i]).Verify(SqlTestSupervisionBinding.Hash(Identity(current)),SqlTestPrivateRecord.Hash(fileAcl.GetSecurityDescriptorBinaryForm()),fileAcl.AreAccessRulesProtected,false);
                var file=_files[i]; if(file.Length!=_records[i].Length) throw new IOException();
                file.Position=0; var bytes=new byte[file.Length]; var offset=0;
                while(offset<bytes.Length) { var count=file.Read(bytes,offset,bytes.Length-offset); if(count==0) throw new IOException(); offset+=count; }
                if(!bytes.SequenceEqual(_records[i])) throw new IOException();
            }
            SqlTestSupervisionProtocol.Verify(_records.ToArray(),_config.Binding,false);
        }
        internal void Intent(string database,string server,string token) => Write(0,d=>SqlTestSupervisionProtocol.Intent(_config.Binding,database,server,token,d));
        internal void Original(int id,DateTime created) => Write(1,d=>SqlTestSupervisionProtocol.Original(_config.Binding,_records[0],id,created,d));
        internal void Complete() => Write(2,d=>SqlTestSupervisionProtocol.Completion(_config.Binding,_records[0],_records[1],d));
        private void Write(int slot,Func<SqlTestSupervisionDelivery,byte[]> encode)
        {
            Verify(); if(_records.Count!=slot) throw new IOException();
            var path=Path.Combine(_config.Root,Slots[slot]); using var user=WindowsIdentity.GetCurrent();
            FileStream file=null!;
            SqlTestSupervisionRecordDelivery.Deliver(()=>{
                file=new FileStream(path,FileMode.CreateNew,FileSystemRights.ReadData|FileSystemRights.WriteData|FileSystemRights.ReadPermissions,FileShare.Read,4096,FileOptions.WriteThrough,SqlTestPrivateRecord.FileAcl(user.User!));
                _files.Add(file); // retain the exact partial file on any later failure
                var acl=File.GetAccessControl(path,SqlTestPrivateRecord.Sections);
                return new SqlTestSupervisionDelivery(SqlTestSupervisionBinding.Hash(Identity(file.SafeFileHandle)),SqlTestPrivateRecord.Hash(acl.GetSecurityDescriptorBinaryForm()));
            },encode,bytes=>file.Write(bytes,0,bytes.Length),()=>file.Flush(true),()=>{
                if(file.Length>65536) throw new IOException(); file.Position=0;
                var bytes=new byte[file.Length];var offset=0;
                while(offset<bytes.Length) { var count=file.Read(bytes,offset,bytes.Length-offset); if(count==0) throw new IOException();offset+=count; }
                return bytes;
            },bytes=>{_records.Add(bytes);Verify();});
        }
        public void Dispose() { foreach(var file in _files) file.Dispose(); _root.Dispose(); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { internal int Length; internal IntPtr Descriptor; internal int Inherit; }
    [StructLayout(LayoutKind.Sequential)] private struct FileDisposition { [MarshalAs(UnmanagedType.Bool)] internal bool Delete; }
    [StructLayout(LayoutKind.Sequential)] private struct FileInformation
    { internal uint Attributes, CreatedLow, CreatedHigh, AccessLow, AccessHigh, WriteLow, WriteHigh, Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [DllImport("kernel32.dll", EntryPoint="CreateDirectoryW", CharSet=CharSet.Unicode, ExactSpelling=true, SetLastError=true)]
    private static extern bool CreateDirectory(string path, ref SecurityAttributes attributes);
    [DllImport("kernel32.dll", EntryPoint="CreateFileW", CharSet=CharSet.Unicode, ExactSpelling=true, SetLastError=true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, ref FileDisposition information, uint size);
}

internal enum SqlTestAclMembership { TargetOnly, Prepared, TargetRemoved, Empty }
internal sealed class SqlTestAclParentPolicyException : IOException
{
    internal SqlTestAclReason Reason { get; }
    internal Exception? OriginalFailure { get; }
    internal SqlTestAclParentPolicyException(SqlTestAclReason reason, Exception? original = null)
        : base("WEB6_ACL_PARENT reason=" + reason) { Reason = reason; OriginalFailure = original; }
}
internal static class SqlTestAclParentPolicy
{
    internal static void ValidateLayout(string shared, string parent, string target, string? control)
    {
        bool Canonical(string path) => !string.IsNullOrEmpty(path) && Path.IsPathRooted(path) &&
            Path.GetPathRoot(path)?.Length == 3 && path.Length <= 240 &&
            string.Equals(path, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase) && !path.EndsWith("\\", StringComparison.Ordinal);
        bool Child(string child, string owner) => !string.Equals(child, owner, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Path.GetDirectoryName(child), owner, StringComparison.OrdinalIgnoreCase);
        try
        {
            if (!Canonical(shared) || !Canonical(parent) || !Canonical(target) || !Child(parent, shared) || !Child(target, parent) ||
                (control != null && (!Canonical(control) || !Child(control, parent) || string.Equals(control, target, StringComparison.OrdinalIgnoreCase))))
                throw new SqlTestAclParentPolicyException(SqlTestAclReason.InvalidParentBinding);
        }
        catch (SqlTestAclParentPolicyException) { throw; }
        catch (Exception error) { throw new SqlTestAclParentPolicyException(SqlTestAclReason.InvalidParentBinding, error); }
    }

    internal static void VerifyIdentity(string original, byte[] descriptor, string current, byte[] currentDescriptor, bool protectedAcl, bool reparse)
    {
        if (string.IsNullOrEmpty(original) || descriptor == null || descriptor.Length == 0 || original != current ||
            currentDescriptor == null || !descriptor.SequenceEqual(currentDescriptor) || !protectedAcl || reparse)
            throw new SqlTestAclParentPolicyException(SqlTestAclReason.ParentIdentityMismatch);
    }

    internal static void RequireMembership(SqlTestAclMembership stage, string parent, string target, string? control, Func<string, string[]> enumerate)
    {
        string[] expected;
        switch (stage)
        {
            case SqlTestAclMembership.TargetOnly: expected = new[] { target }; break;
            case SqlTestAclMembership.Prepared: expected = new[] { target, control! }; break;
            case SqlTestAclMembership.TargetRemoved: expected = new[] { control! }; break;
            case SqlTestAclMembership.Empty: expected = Array.Empty<string>(); break;
            default: throw new SqlTestAclParentPolicyException(SqlTestAclReason.InvalidParentBinding);
        }
        if (expected.Any(string.IsNullOrEmpty) || expected.Distinct(StringComparer.OrdinalIgnoreCase).Count() != expected.Length)
            throw new SqlTestAclParentPolicyException(SqlTestAclReason.InvalidParentBinding);
        string[] actual;
        try { actual = enumerate(parent); }
        catch (Exception error) { throw new SqlTestAclParentPolicyException(SqlTestAclReason.EnumerationFailed, error); }
        if (actual == null || !actual.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .SequenceEqual(expected.OrderBy(value => value, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase))
            throw new SqlTestAclParentPolicyException(SqlTestAclReason.UnexpectedMembership);
    }
}

internal sealed class SqlTestAclParentLifecycle
{
    private enum Stage { Empty, Target, Sealed, ChildrenRemoved, Removed, Failed }
    private readonly object _sync = new object();
    private Stage _stage;
    internal bool Removed { get { lock (_sync) return _stage == Stage.Removed; } }
    internal void CreateTarget(Action verifyEmpty, Action create) => Transition(Stage.Empty, Stage.Target, verifyEmpty, create);
    internal void SealControl(Action verify) => Transition(Stage.Target, Stage.Sealed, verify);
    internal void ChildrenRemoved(Action verify) => Transition(Stage.Sealed, Stage.ChildrenRemoved, verify);
    internal void Remove(Action verify, Action remove) => Transition(Stage.ChildrenRemoved, Stage.Removed, verify, remove);
    internal void Verify(Action verify)
    {
        lock (_sync)
        {
            try
            {
                var expected = _stage;
                Require(expected);
                verify();
                Require(expected);
            }
            catch { _stage = Stage.Failed; throw; }
        }
    }
    internal void Abandon() { lock (_sync) { if (_stage != Stage.Removed) _stage = Stage.Failed; } }
    private void Transition(Stage from, Stage to, Action verify, Action? mutate = null)
    {
        lock (_sync)
        {
            try
            {
                Require(from);
                verify();
                // Verification may catch a reentrant rejection. Check again while holding
                // the same lock, immediately before any create/delete callback can run.
                Require(from);
                mutate?.Invoke();
                Require(from);
                _stage = to;
            }
            catch { _stage = Stage.Failed; throw; }
        }
    }
    private void Require(Stage expected)
    {
        if (_stage == Stage.Failed || _stage == Stage.Removed || _stage != expected)
            throw new SqlTestAclParentPolicyException(SqlTestAclReason.TerminalFailure);
    }
}

internal enum SqlTestAclPhase
{
    CaseVerify, AttemptAdmission, ParentAncestors, ParentAclRead, ParentAclPolicy, ParentMembership, ParentPinOpen,
    SiblingPathAdmission, SiblingAncestors, SiblingDescriptor, SiblingCreate, SiblingAclNormalize, SiblingRootOpen,
    SiblingRootCapture, RecordCreate, RecordAclCapture, RecordSerialize, RecordWrite, RecordFlush, RecordReadback,
    RecordSeal, IndependentVerify, AuthorityRegister, AuthorityReverify, ReadyForMutation,
    SealGate, FreshVerify, Mutation, Assertion, Restoration
}
internal enum SqlTestAclReason { None, OperationFailed, Unsealed, NotReady, AlreadyAttempted, DiagnosticUnavailable, TerminalFailure, EnumerationFailed, UnexpectedMembership, ParentIdentityMismatch, InvalidParentBinding }
internal interface ISqlTestAclPreparation
{
    void Execute(SqlTestAclPhase phase);
    bool IsSealed { get; }
}
internal sealed class SqlTestAclPhaseException : IOException
{
    internal SqlTestAclPhase Phase { get; }
    internal SqlTestAclReason Reason { get; }
    // Deliberately not InnerException/Data: framework TRX formatting must not serialize private details.
    internal Exception? OriginalFailure { get; }
    internal Exception? DiagnosticFailure { get; set; }
    internal Exception? PriorFailure { get; set; }
    internal SqlTestAclPhaseException(SqlTestAclPhase phase, SqlTestAclReason reason, Exception? original = null)
        : base("WEB6_ACL phase=" + phase + " result=Failed reason=" + reason)
    { Phase = phase; Reason = reason; OriginalFailure = original; }
}
internal sealed class SqlTestAclMutationCoordinator
{
    private readonly ISqlTestAclPreparation _owner;
    private readonly Action<string>? _diagnostic;
    private bool _preparationStarted, _ready, _mutationStarted;
    private SqlTestAclPhaseException? _firstFailure;
    internal SqlTestAclMutationCoordinator(ISqlTestAclPreparation owner, Action<string>? diagnostic)
    { _owner = owner ?? throw new ArgumentNullException(nameof(owner)); _diagnostic = diagnostic; }

    internal void Prepare()
    {
        RefuseIfFailed();
        if (_preparationStarted) Fail(SqlTestAclPhase.AttemptAdmission, SqlTestAclReason.AlreadyAttempted);
        _preparationStarted = true;
        // This single ordered sequence drives both the native adapter and injected pure proof.
        foreach (var phase in new[] {
            SqlTestAclPhase.CaseVerify, SqlTestAclPhase.AttemptAdmission,
            SqlTestAclPhase.ParentAncestors, SqlTestAclPhase.ParentAclRead, SqlTestAclPhase.ParentAclPolicy,
            SqlTestAclPhase.ParentMembership, SqlTestAclPhase.ParentPinOpen,
            SqlTestAclPhase.SiblingPathAdmission, SqlTestAclPhase.SiblingAncestors, SqlTestAclPhase.SiblingDescriptor,
            SqlTestAclPhase.SiblingCreate, SqlTestAclPhase.SiblingAclNormalize, SqlTestAclPhase.SiblingRootOpen,
            SqlTestAclPhase.SiblingRootCapture, SqlTestAclPhase.RecordCreate, SqlTestAclPhase.RecordAclCapture,
            SqlTestAclPhase.RecordSerialize, SqlTestAclPhase.RecordWrite, SqlTestAclPhase.RecordFlush,
            SqlTestAclPhase.RecordReadback, SqlTestAclPhase.RecordSeal, SqlTestAclPhase.IndependentVerify,
            SqlTestAclPhase.AuthorityRegister, SqlTestAclPhase.AuthorityReverify, SqlTestAclPhase.ReadyForMutation })
            Step(phase, () => _owner.Execute(phase));
        RefuseIfFailed();
        _ready = true;
    }

    internal void RunMutation(Action mutation, Action assertion)
    {
        RefuseIfFailed();
        if (!_ready) Fail(SqlTestAclPhase.SealGate, SqlTestAclReason.NotReady);
        if (_mutationStarted) Fail(SqlTestAclPhase.SealGate, SqlTestAclReason.AlreadyAttempted);
        // Admission is one-shot even if a later verifier fails. No implicit mutation retry.
        _mutationStarted = true;
        Step(SqlTestAclPhase.SealGate, () => { if (!_owner.IsSealed) throw new SqlTestAclPhaseException(SqlTestAclPhase.SealGate, SqlTestAclReason.Unsealed); });
        Step(SqlTestAclPhase.FreshVerify, () => _owner.Execute(SqlTestAclPhase.FreshVerify));
        // A failed pre-mutation diagnostic cannot enter mutation or restoration.
        Emit(SqlTestAclPhase.Mutation, "Started", SqlTestAclReason.None);
        RefuseIfFailed();
        Exception? mutationFailure = null;
        try
        {
            InvokeAndComplete(SqlTestAclPhase.Mutation, mutation);
            Step(SqlTestAclPhase.Assertion, assertion);
        }
        catch (Exception error) { mutationFailure = error; throw; }
        finally
        {
            try { Restore(); }
            catch (SqlTestAclPhaseException failure) { failure.PriorFailure = mutationFailure; throw; }
        }
    }

    private void Restore()
    {
        // After mutation may have started, unavailable diagnostics cannot suppress the
        // authority's own proof/restore. The diagnostic failure still fails the run.
        SqlTestAclPhaseException? diagnosticFailure = null;
        try { Emit(SqlTestAclPhase.Restoration, "Started", SqlTestAclReason.None); }
        catch (SqlTestAclPhaseException error) { diagnosticFailure = error; }
        try { InvokeAndComplete(SqlTestAclPhase.Restoration, () => _owner.Execute(SqlTestAclPhase.Restoration)); }
        catch (SqlTestAclPhaseException error) { error.DiagnosticFailure ??= diagnosticFailure; throw; }
        if (diagnosticFailure != null) throw diagnosticFailure;
    }

    private void Step(SqlTestAclPhase phase, Action action)
    {
        RefuseIfFailed();
        Emit(phase, "Started", SqlTestAclReason.None);
        RefuseIfFailed(); // A diagnostic callback may itself reject a reentrant admission.
        InvokeAndComplete(phase, action);
        RefuseIfFailed();
    }

    private void InvokeAndComplete(SqlTestAclPhase phase, Action action)
    {
        var priorFailure = _firstFailure;
        try
        {
            action();
            if (priorFailure == null) RefuseIfFailed();
        }
        catch (Exception error)
        {
            var failure = error as SqlTestAclPhaseException ?? new SqlTestAclPhaseException(phase,
                error is SqlTestAclParentPolicyException policy ? policy.Reason : SqlTestAclReason.OperationFailed, error);
            ThrowFailure(failure);
        }
        Emit(phase, "Completed", SqlTestAclReason.None);
    }

    private void Fail(SqlTestAclPhase phase, SqlTestAclReason reason) =>
        ThrowFailure(new SqlTestAclPhaseException(phase, reason));

    private void LatchFailure(SqlTestAclPhaseException failure)
    {
        // Irreversible before diagnostics: even a reentrant admission sees terminal state.
        _ready = false;
        _firstFailure ??= failure;
    }

    private void RefuseIfFailed()
    {
        if (_firstFailure != null)
            ThrowFailure(new SqlTestAclPhaseException(_firstFailure.Phase, SqlTestAclReason.TerminalFailure, _firstFailure));
    }

    private void ThrowFailure(SqlTestAclPhaseException failure)
    {
        LatchFailure(failure);
        try { Emit(failure.Phase, "Failed", failure.Reason); }
        catch (SqlTestAclPhaseException diagnosticFailure) { failure.DiagnosticFailure = diagnosticFailure; }
        throw failure;
    }

    private void Emit(SqlTestAclPhase phase, string result, SqlTestAclReason reason)
    {
        // All public fields are closed enum values/literals. Private exceptions are not formatted.
        try { _diagnostic?.Invoke("WEB6_ACL phase=" + phase + " result=" + result + " reason=" + reason); }
        catch (Exception error)
        {
            var failure = new SqlTestAclPhaseException(phase, SqlTestAclReason.DiagnosticUnavailable, error);
            LatchFailure(failure);
            throw failure;
        }
    }
}

// The selected R2f Fact alone opts into this protocol. No persisted record can
// create an owner or authorize cleanup. Defaults elsewhere remain unchanged.
internal enum SqlTestSupervisionPhase
{
    MasterBinding, Intent, Create, Mark, LiveVerification, OriginalWitness,
    SubjectControl, ProbePreparation, ProbeMutation, ProbeRefusal, Restoration,
    SubjectManifest, Quiescence, Drop, Absence, SubjectRemoval, CompletionReceipt
}
internal sealed class SqlTestSupervisionException : InvalidOperationException
{
    internal Exception? OriginalFailure { get; }
    internal SqlTestSupervisionException(SqlTestSupervisionPhase phase, Exception? original = null)
        : base("WEB6_SQL_SUPERVISION phase=" + (Enum.IsDefined(typeof(SqlTestSupervisionPhase),phase) ? phase.ToString() : "Admission") + " status=Retained") { OriginalFailure = original; }
}
internal sealed class SqlTestSupervisedCoordinator
{
    private readonly object _sync = new object();
    private readonly Action _verify;
    private readonly Action<SqlTestSupervisionPhase>? _diagnostic;
    private int _next, _failureVersion;
    private bool _busy, _probeEntered, _restoreAttempted, _probeCallActive, _workAdmitted;
    private SqlTestSupervisionPhase _activePhase;
    private SqlTestSupervisionException? _failure;
    internal bool CreationMayHaveOccurred { get; private set; }
    internal bool Failed { get { lock (_sync) return _failure != null; } }
    internal bool Completed { get { lock (_sync) return _failure == null && _next == 17 && !_busy; } }
    internal SqlTestSupervisedCoordinator(Action verify, Action<SqlTestSupervisionPhase>? diagnostic = null)
    { _verify = verify ?? throw new ArgumentNullException(nameof(verify)); _diagnostic = diagnostic; }
    internal SqlTestSupervisionException Fail(Exception error) { lock (_sync) return Reject((SqlTestSupervisionPhase)Math.Min(_next, 16), error); }
    internal void RequireActiveWork(SqlTestSupervisionPhase phase)
    {
        lock(_sync) if(!_busy||!_workAdmitted||_failure!=null||_activePhase!=phase) throw Reject(phase);
    }
    private SqlTestSupervisionException Reject(SqlTestSupervisionPhase phase, Exception? error = null)
    { _failureVersion++; return _failure ??= new SqlTestSupervisionException(phase, error); }
    internal Task AdvanceAsync(SqlTestSupervisionPhase phase, Func<Task> work, CancellationToken token) => StepAsync(phase, work, token, false);
    private async Task StepAsync(SqlTestSupervisionPhase phase, Func<Task> work, CancellationToken token, bool restoration, bool probe = false)
    {
        Task pending; int version; var entered = false;
        lock (_sync)
        {
            try
            {
                if (_busy || (restoration ? (!_probeCallActive || !_probeEntered || _restoreAttempted || phase != SqlTestSupervisionPhase.Restoration) :
                    (_failure != null || _next != (int)phase || _next >= 17 || phase == SqlTestSupervisionPhase.Restoration ||
                     ((phase == SqlTestSupervisionPhase.ProbeMutation || phase == SqlTestSupervisionPhase.ProbeRefusal) && (!probe || !_probeCallActive))))) throw Reject(phase);
                if (restoration) _restoreAttempted = true;
                _busy = true; _workAdmitted=false; _activePhase=phase; entered = true; version = _failureVersion;
                token.ThrowIfCancellationRequested(); _diagnostic?.Invoke(phase);
                if (_failureVersion != version) throw Reject(phase);
                _verify();
                if (_failureVersion != version || !_busy || (!restoration && _failure != null)) throw Reject(phase);
                token.ThrowIfCancellationRequested();
                if (phase == SqlTestSupervisionPhase.Create) CreationMayHaveOccurred = true;
                if (phase == SqlTestSupervisionPhase.ProbeMutation) _probeEntered = true;
                _workAdmitted=true;
                pending = work() ?? throw new InvalidOperationException();
            }
            catch (Exception error) { if(entered) { _busy = false; _workAdmitted=false; } throw Reject(phase, error); }
        }
        try
        {
            await pending.ConfigureAwait(false);
            lock (_sync)
            {
                if (_failureVersion != version || !_busy) throw Reject(phase);
                _workAdmitted=false;
                _verify();
                if (_failureVersion != version || !_busy) throw Reject(phase);
                token.ThrowIfCancellationRequested();
                if (_failure == null) _next = (int)phase + 1;
                _busy = false;
            }
        }
        catch (Exception error) { lock (_sync) { _busy = false; _workAdmitted=false; throw Reject(phase, error); } }
    }
    internal async Task ProbeAsync(Func<Task> mutate, Func<Task> refusal, Func<Task> restore, CancellationToken token)
    {
        lock(_sync)
        {
            if(_probeCallActive || _busy || _failure != null || _next != (int)SqlTestSupervisionPhase.ProbeMutation)
                throw Reject(SqlTestSupervisionPhase.ProbeMutation);
            _probeCallActive=true;
        }
        try
        {
            await StepAsync(SqlTestSupervisionPhase.ProbeMutation, mutate, token, false, true).ConfigureAwait(false);
            await StepAsync(SqlTestSupervisionPhase.ProbeRefusal, refusal, token, false, true).ConfigureAwait(false);
        }
        finally
        {
            // Only the already-entered probe gets one restoration attempt. A failed
            // restoration/delivery cannot unlock disposal or any later phase.
            bool entered; lock (_sync) entered = _probeEntered && !_restoreAttempted;
            try { if (entered) await StepAsync(SqlTestSupervisionPhase.Restoration, restore, CancellationToken.None, true).ConfigureAwait(false); }
            finally { lock(_sync) _probeCallActive=false; }
        }
        lock (_sync) if (_failure != null) throw _failure;
    }
}
internal sealed class SqlTestSupervisionBinding
{
    internal const string Selection = "MyDmsVn.Server.Infrastructure.IntegrationTests.WebOperationMaintenanceOperatorTests.Candidate_allocation_manifest_binds_live_original_identity_and_requires_absence_before_removal";
    internal static string SelectionHash => Hash(Selection);
    internal readonly string Run, Source, SelectionValue, RootIdentity, RootAcl;
    internal SqlTestSupervisionBinding(string run, string source, string selection, string identity, string acl)
    {
        if (new[] { run, source, selection, identity, acl }.Any(value => !IsHash(value)) || selection != SelectionHash)
            throw new SqlTestSupervisionException(SqlTestSupervisionPhase.MasterBinding);
        Run=run; Source=source; SelectionValue=selection; RootIdentity=identity; RootAcl=acl;
    }
    internal static bool IsHash(string value) => value != null && value.Length == 64 && value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
    internal static string Hash(string text) => SqlTestPrivateRecord.Hash(new UTF8Encoding(false,true).GetBytes(text));
    internal string Fields => "runHash="+Run+"\nsourceHash="+Source+"\nselectionHash="+SelectionValue+"\nrootIdentityHash="+RootIdentity+"\nrootAclHash="+RootAcl+"\n";
}
internal static class SqlTestSupervisionProtocol
{
    private static string Header(SqlTestSupervisionBinding b, string kind, string previous, SqlTestSupervisionDelivery delivery) => "version=1\nkind="+kind+"\n"+b.Fields+"previousHash="+previous+"\nfileIdentityHash="+delivery.Identity+"\nfileAclHash="+delivery.Acl+"\n";
    internal static byte[] Intent(SqlTestSupervisionBinding b, string database, string server, string token, SqlTestSupervisionDelivery delivery)
    {
        try
        {
            SqlTestDatabaseName.EnsureDisposable(database);
            if (string.IsNullOrWhiteSpace(server) || server.Length>128 || server.Any(c=>char.IsControl(c)) || !Guid.TryParseExact(token,"N",out var id) || id==Guid.Empty || token!=id.ToString("N")) throw new InvalidDataException();
            return SqlTestPrivateRecord.Seal(Header(b,"Intent",new string('0',64),delivery)+"database="+Convert.ToBase64String(Encoding.UTF8.GetBytes(database))+"\nserver="+Convert.ToBase64String(Encoding.UTF8.GetBytes(server))+"\nownershipToken="+token+"\n");
        }
        catch(Exception error) { throw new SqlTestSupervisionException(SqlTestSupervisionPhase.Intent,error); }
    }
    internal static byte[] Original(SqlTestSupervisionBinding b, byte[] intent, int id, DateTime created, SqlTestSupervisionDelivery delivery)
    {
        Verify(new[]{intent},b,false);
        if(id<=4 || created==default) throw new SqlTestSupervisionException(SqlTestSupervisionPhase.OriginalWitness);
        var fields=Fields(intent);
        return SqlTestPrivateRecord.Seal(Header(b,"Original",SqlTestPrivateRecord.Hash(intent),delivery)+"database="+fields["database"]+"\nserver="+fields["server"]+"\nownershipToken="+fields["ownershipToken"]+"\ndatabaseId="+id.ToString(CultureInfo.InvariantCulture)+"\ncreateDateTicks="+created.Ticks.ToString(CultureInfo.InvariantCulture)+"\n");
    }
    internal static byte[] Completion(SqlTestSupervisionBinding b, byte[] intent, byte[] original, SqlTestSupervisionDelivery delivery)
    {
        Verify(new[]{intent,original},b,false);
        return SqlTestPrivateRecord.Seal(Header(b,"Complete",SqlTestPrivateRecord.Hash(original),delivery)+"databaseAbsent=1\nsubjectControlRemoved=1\n");
    }
    private static System.Collections.Generic.Dictionary<string,string> Fields(byte[] bytes) => SqlTestPrivateRecord.Validate(bytes).TrimEnd('\n').Split('\n').Select(x=>x.Split(new[]{'='},2)).ToDictionary(x=>x[0],x=>x[1],StringComparer.Ordinal);
    internal static SqlTestSupervisionDelivery Delivery(byte[] bytes) { var fields=Fields(bytes); return new SqlTestSupervisionDelivery(fields["fileIdentityHash"],fields["fileAclHash"]); }
    internal static void Verify(byte[][] records, SqlTestSupervisionBinding b, bool complete)
    {
        try
        {
            if(records.Length>3 || (complete && records.Length!=3)) throw new InvalidDataException();
            if(records.Length==0) return;
            var one=Fields(records[0]);
            var expected=Intent(b,new UTF8Encoding(false,true).GetString(Convert.FromBase64String(one["database"])),new UTF8Encoding(false,true).GetString(Convert.FromBase64String(one["server"])),one["ownershipToken"],Delivery(records[0]));
            if(!records[0].SequenceEqual(expected)) throw new InvalidDataException();
            if(records.Length>1)
            {
                var two=Fields(records[1]);
                expected=Original(b,records[0],int.Parse(two["databaseId"],CultureInfo.InvariantCulture),new DateTime(long.Parse(two["createDateTicks"],CultureInfo.InvariantCulture)),Delivery(records[1]));
                if(!records[1].SequenceEqual(expected)) throw new InvalidDataException();
            }
            if(records.Length>2 && !records[2].SequenceEqual(Completion(b,records[0],records[1],Delivery(records[2])))) throw new InvalidDataException();
        }
        catch(Exception error) { throw new SqlTestSupervisionException(SqlTestSupervisionPhase.OriginalWitness,error); }
    }
}
internal sealed class SqlTestSupervisionDelivery
{
    internal readonly string Identity, Acl;
    internal SqlTestSupervisionDelivery(string identity,string acl)
    {
        if(!SqlTestSupervisionBinding.IsHash(identity)||!SqlTestSupervisionBinding.IsHash(acl)) throw new SqlTestSupervisionException(SqlTestSupervisionPhase.Intent);
        Identity=identity; Acl=acl;
    }
    internal void Verify(string identity,string acl,bool protectedAcl,bool reparse)
    {
        if(Identity!=identity||Acl!=acl||!protectedAcl||reparse) throw new SqlTestSupervisionException(SqlTestSupervisionPhase.OriginalWitness);
    }
}
internal static class SqlTestSupervisionRecordDelivery
{
    internal static byte[] Deliver(Func<SqlTestSupervisionDelivery> create,Func<SqlTestSupervisionDelivery,byte[]> encode,
        Action<byte[]> write,Action flush,Func<byte[]> readback,Action<byte[]> verify)
    {
        try
        {
            var delivery=create(); var expected=(byte[])encode(delivery).Clone();
            write((byte[])expected.Clone()); flush();
            if(!expected.SequenceEqual(readback())) throw new IOException();
            verify((byte[])expected.Clone()); return expected;
        }
        catch(Exception error) { throw new SqlTestSupervisionException(SqlTestSupervisionPhase.OriginalWitness,error); }
    }
}
internal sealed class SqlTestSupervisionConfiguration
{
    private const string Prefix="MYDMSVN_WEB6_SQL_SUPERVISION_";
    internal readonly string Root, ServerHash;
    internal readonly SqlTestSupervisionBinding Binding;
    private SqlTestSupervisionConfiguration(string[] values)
    {
        Root=values[0]; ServerHash=values[6];
        if(string.IsNullOrWhiteSpace(Root)||Root.Length>180||!Path.IsPathRooted(Root)||Path.GetPathRoot(Root)?.Length!=3||Root!=Path.GetFullPath(Root)||Root.EndsWith("\\",StringComparison.Ordinal)||!SqlTestSupervisionBinding.IsHash(ServerHash)) throw new InvalidOperationException();
        Binding=new SqlTestSupervisionBinding(values[1],values[2],values[3],values[4],values[5]);
    }
    internal static SqlTestSupervisionConfiguration? Read(Func<string,string?> get)
    {
        try
        {
            var keys=new[]{"ROOT","RUN_HASH","SOURCE_HASH","SELECTION_HASH","ROOT_IDENTITY_HASH","ROOT_ACL_HASH","SERVER_HASH"};
            var values=keys.Select(key=>get(Prefix+key)).ToArray();
            if(values.All(value=>value==null)) return null;
            if(values.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException();
            return new SqlTestSupervisionConfiguration(values!);
        }
        catch(Exception error) { throw new SqlTestSupervisionException(SqlTestSupervisionPhase.MasterBinding,error); }
    }
}
internal sealed class SqlTestSupervision : IDisposable
{
    private readonly SqlTestSupervisionConfiguration _configuration;
    private readonly SqlTestOwnershipManifest.SupervisionStore _store;
    internal SqlTestSupervisedCoordinator Coordinator { get; }
    private SqlTestSupervision(SqlTestSupervisionConfiguration configuration)
    {
        _configuration=configuration; _store=new SqlTestOwnershipManifest.SupervisionStore(configuration);
        Coordinator=new SqlTestSupervisedCoordinator(_store.Verify);
    }
    internal static SqlTestSupervision? FromEnvironment()
    {
        try { var configuration=SqlTestSupervisionConfiguration.Read(Environment.GetEnvironmentVariable); return configuration==null?null:new SqlTestSupervision(configuration); }
        catch(Exception error) { throw new SqlTestSupervisionException(SqlTestSupervisionPhase.MasterBinding,error); }
    }
    internal void RequireServer(string server)
    { Coordinator.RequireActiveWork(SqlTestSupervisionPhase.MasterBinding); if(SqlTestSupervisionBinding.Hash(server)!=_configuration.ServerHash) throw new SqlTestSupervisionException(SqlTestSupervisionPhase.MasterBinding); }
    internal void Intent(string name,string server,string token) { Coordinator.RequireActiveWork(SqlTestSupervisionPhase.Intent); _store.Intent(name,server,token); }
    internal void Original(int id,DateTime created) { Coordinator.RequireActiveWork(SqlTestSupervisionPhase.OriginalWitness); _store.Original(id,created); }
    internal void Complete() { Coordinator.RequireActiveWork(SqlTestSupervisionPhase.CompletionReceipt); _store.Complete(); }
    public void Dispose() => _store.Dispose();
}

internal static class SqlTestPrivateRecord
{
    internal const AccessControlSections Sections = AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access;
    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
    internal static DirectorySecurity DirectoryAcl(SecurityIdentifier owner)
    {
        var acl = new DirectorySecurity(); acl.SetOwner(owner); acl.SetGroup(owner); acl.SetAccessRuleProtection(true, false);
        acl.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        return acl;
    }
    internal static FileSecurity FileAcl(SecurityIdentifier owner)
    {
        var acl = new FileSecurity(); acl.SetOwner(owner); acl.SetGroup(owner); acl.SetAccessRuleProtection(true, false);
        acl.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl, AccessControlType.Allow));
        return acl;
    }
    internal static DirectorySecurity RestoreDirectory(byte[] descriptor)
    {
        NoAudit(descriptor);
        var acl = new DirectorySecurity(); acl.SetSecurityDescriptorBinaryForm(descriptor, Sections); return acl;
    }
    internal static FileSecurity RestoreFile(byte[] descriptor)
    {
        NoAudit(descriptor);
        var acl = new FileSecurity(); acl.SetSecurityDescriptorBinaryForm(descriptor, Sections); return acl;
    }
    private static void NoAudit(byte[] descriptor)
    {
        try
        {
            var raw = new RawSecurityDescriptor(descriptor, 0);
            if (raw.SystemAcl != null || (raw.ControlFlags & (ControlFlags.SystemAclPresent | ControlFlags.SystemAclProtected |
                ControlFlags.SystemAclAutoInherited | ControlFlags.SystemAclAutoInheritRequired | ControlFlags.SystemAclDefaulted)) != 0)
                throw new InvalidDataException();
        }
        catch { throw new InvalidDataException("Private descriptor must contain Owner, Group and Access only."); }
    }
    internal static byte[] Seal(string record)
    {
        Canonical(record);
        return StrictUtf8.GetBytes(record + "sha256=" + Hash(StrictUtf8.GetBytes(record)) + "\n");
    }
    internal static string Validate(byte[] payload)
    {
        try
        {
            if (payload.Length > 65536 || payload.Length < 73) throw new InvalidDataException();
            var text = StrictUtf8.GetString(payload);
            var position = text.LastIndexOf("sha256=", StringComparison.Ordinal);
            if (position < 1 || text[position - 1] != '\n' || text.Length - position != 72 || text[text.Length - 1] != '\n') throw new InvalidDataException();
            var record = text.Substring(0, position); Canonical(record);
            if (text.Substring(position + 7, 64) != Hash(StrictUtf8.GetBytes(record))) throw new InvalidDataException();
            return record;
        }
        catch { throw new InvalidDataException("Private record canonical seal is invalid."); }
    }
    internal static string Hash(byte[] payload)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(payload)).Replace("-", "").ToLowerInvariant();
    }
    private static void Canonical(string record)
    {
        if (record == null || record.Length > 60000 || !record.StartsWith("version=1\n", StringComparison.Ordinal) ||
            !record.EndsWith("\n", StringComparison.Ordinal) || record.IndexOf('\r') >= 0 || record.IndexOf('\0') >= 0)
            throw new InvalidDataException("Private record is not canonical.");
        var keys = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var line in record.Substring(0, record.Length - 1).Split('\n'))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0 || line.Any(character => character < 32 || character > 126)) throw new InvalidDataException("Private record field refused.");
            var key = line.Substring(0, separator);
            if (key == "sha256" || !keys.Add(key) || key.Any(character => !char.IsLetterOrDigit(character))) throw new InvalidDataException("Private record key refused.");
        }
    }
}
