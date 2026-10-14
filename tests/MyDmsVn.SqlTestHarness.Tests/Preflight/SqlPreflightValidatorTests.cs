using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestHarness.Approval;
using MyDmsVn.SqlTestHarness.Preflight;
using MyDmsVn.SqlTestHarness.Ownership;
using Xunit;

namespace MyDmsVn.SqlTestHarness.Tests.Preflight;

public sealed class SqlPreflightValidatorTests
{
    private const string MasterConnectionString =
        "Server=sql-alias;Database=master;Integrated Security=true;Encrypt=false";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-connection-string")]
    [InlineData("Server=sql-alias;Database=MyDmsVn;Integrated Security=true")]
    public async Task Invalid_connection_configuration_fails_before_opening_or_ddl(string? connectionString)
    {
        var session = ValidSession();
        var validator = CreateValidator(session);

        var result = await validator.ValidateAsync(
            connectionString,
            Approval(),
            SqlPreflightRequirements.Mutation,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SqlPreflightFailureKind.InvalidConfiguration, result.Failure!.Kind);
        Assert.Equal(0, session.OpenCount);
        AssertNoDestructiveSql(session);
    }

    [Fact]
    public async Task Connection_failure_has_a_distinct_failure_category_and_no_ddl()
    {
        var session = ValidSession();
        session.OpenError = new InvalidOperationException("offline");

        var result = await CreateValidator(session).ValidateAsync(
            MasterConnectionString,
            Approval(),
            SqlPreflightRequirements.Mutation,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(SqlPreflightFailureKind.ConnectionFailure, result.Failure!.Kind);
        AssertNoDestructiveSql(session);
    }

    [Fact]
    public async Task Effective_database_other_than_master_fails_closed()
    {
        var session = ValidSession();
        session.EffectiveDatabase = "tempdb";

        var result = await ValidateAsync(session);

        AssertFailure(result, SqlPreflightFailureKind.WrongEffectiveDatabase);
        AssertNoDestructiveSql(session);
    }

    [Fact]
    public async Task Unreadable_instance_identity_fails_closed()
    {
        var session = ValidSession();
        session.Identity = null;

        var result = await ValidateAsync(session);

        AssertFailure(result, SqlPreflightFailureKind.IdentityUnavailable);
        AssertNoDestructiveSql(session);
    }

    [Fact]
    public async Task Server_identity_mismatch_fails_closed()
    {
        var session = ValidSession();
        session.Identity = new SqlInstanceIdentity(
            "production-sql",
            "TESTBOX",
            "SQLEXPRESS",
            "tcp:10.0.0.5,1433");

        var result = await ValidateAsync(session);

        AssertFailure(result, SqlPreflightFailureKind.ApprovalMismatch);
        AssertNoDestructiveSql(session);
    }

    [Fact]
    public async Task Endpoint_mismatch_rejects_an_unintended_alias_target()
    {
        var session = ValidSession();
        session.Identity = new SqlInstanceIdentity(
            "vsql01",
            "TESTBOX",
            "SQLEXPRESS",
            "tcp:10.0.0.99,1433");

        var result = await ValidateAsync(session);

        AssertFailure(result, SqlPreflightFailureKind.ApprovalMismatch);
        AssertNoDestructiveSql(session);
    }

    [Theory]
    [InlineData(14, "VIEW SERVER STATE")]
    [InlineData(15, "VIEW SERVER STATE")]
    [InlineData(16, "VIEW SERVER PERFORMANCE STATE")]
    public async Task Missing_version_specific_dmv_permission_is_reported(
        int majorVersion,
        string expectedPermission)
    {
        var session = ValidSession();
        session.MajorVersion = majorVersion;
        session.HasPermission = false;

        var result = await ValidateAsync(session);

        AssertFailure(result, SqlPreflightFailureKind.MissingPermission);
        Assert.Equal(expectedPermission, Assert.Single(session.RequestedPermissions));
        AssertNoDestructiveSql(session);
    }

    [Fact]
    public async Task Expired_approval_is_rechecked_at_validation_time()
    {
        var session = ValidSession();
        var approval = Approval(new DateTimeOffset(2026, 10, 13, 23, 59, 59, TimeSpan.Zero));

        var result = await CreateValidator(session).ValidateAsync(
            MasterConnectionString,
            approval,
            SqlPreflightRequirements.Mutation,
            CancellationToken.None);

        AssertFailure(result, SqlPreflightFailureKind.ApprovalExpired);
        Assert.Equal(0, session.OpenCount);
        AssertNoDestructiveSql(session);
    }

    [Fact]
    public async Task Changed_live_identity_is_not_accepted_from_a_previous_validation()
    {
        var session = ValidSession();
        var validator = CreateValidator(session);

        var first = await validator.ValidateAsync(
            MasterConnectionString,
            Approval(),
            SqlPreflightRequirements.Mutation,
            CancellationToken.None);
        session.Identity = new SqlInstanceIdentity(
            "production-sql",
            "PROD",
            null,
            "tcp:10.0.0.200,1433");
        var second = await validator.ValidateAsync(
            MasterConnectionString,
            Approval(),
            SqlPreflightRequirements.Mutation,
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.NotNull(first.Authorization);
        AssertFailure(second, SqlPreflightFailureKind.ApprovalMismatch);
        Assert.Equal(2, session.OpenCount);
        AssertNoDestructiveSql(session);
    }

    [Fact]
    public async Task Valid_live_identity_and_permission_issue_a_scoped_authorization()
    {
        var session = ValidSession();

        var result = await ValidateAsync(session);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Failure);
        Assert.Equal("local-sql-test", result.Authorization!.ApprovalId);
        Assert.Equal(
            "3e68cd38f51b859ba18373c96bf81945b44cbc94ebe8e83435dcc1720892f7e7",
            result.Authorization.InstanceFingerprintSha256);
        Assert.Equal(UtcNow(), result.Authorization.ValidatedAtUtc);
        AssertNoDestructiveSql(session);
    }

    private static Task<SqlPreflightResult> ValidateAsync(RecordingSession session) =>
        CreateValidator(session).ValidateAsync(
            MasterConnectionString,
            Approval(),
            SqlPreflightRequirements.Mutation,
            CancellationToken.None);

    private static SqlPreflightValidator CreateValidator(RecordingSession session) =>
        new SqlPreflightValidator(_ => session, UtcNow);

    private static RecordingSession ValidSession() =>
        new RecordingSession
        {
            EffectiveDatabase = "master",
            Identity = new SqlInstanceIdentity(
                "vsql01",
                "TESTBOX",
                "SQLEXPRESS",
                "tcp:10.0.0.5,1433"),
            MajorVersion = 16,
            HasPermission = true,
        };

    private static SqlInstanceApproval Approval(DateTimeOffset? validUntilUtc = null)
    {
        var identity = new SqlInstanceIdentity(
            "vsql01",
            "TESTBOX",
            "SQLEXPRESS",
            "tcp:10.0.0.5,1433");
        return new SqlInstanceApproval(
            "local-sql-test",
            identity,
            "3e68cd38f51b859ba18373c96bf81945b44cbc94ebe8e83435dcc1720892f7e7",
            validUntilUtc ?? new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero));
    }

    private static DateTimeOffset UtcNow() =>
        new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);

    private static void AssertFailure(SqlPreflightResult result, SqlPreflightFailureKind kind)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Authorization);
        Assert.Equal(kind, result.Failure!.Kind);
    }

    private static void AssertNoDestructiveSql(RecordingSession session)
    {
        foreach (var command in session.Commands)
        {
            Assert.DoesNotContain("CREATE DATABASE", command, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ALTER DATABASE", command, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DROP DATABASE", command, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("KILL", command, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class RecordingSession : ISqlAdministrativeSession
    {
        public int OpenCount { get; private set; }

        public Exception? OpenError { get; set; }

        public string? EffectiveDatabase { get; set; }

        public SqlInstanceIdentity? Identity { get; set; }

        public int? MajorVersion { get; set; }

        public bool? HasPermission { get; set; }

        public List<string> Commands { get; } = new List<string>();

        public List<string> RequestedPermissions { get; } = new List<string>();

        public Task OpenAsync(CancellationToken cancellationToken)
        {
            OpenCount++;
            if (OpenError is not null)
            {
                throw OpenError;
            }

            return Task.CompletedTask;
        }

        public Task<string?> ReadEffectiveDatabaseAsync(CancellationToken cancellationToken) =>
            Task.FromResult(EffectiveDatabase);

        public Task<SqlInstanceIdentity?> ReadIdentityAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Identity);

        public Task<int?> ReadMajorVersionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(MajorVersion);

        public Task<bool?> HasServerPermissionAsync(string permissionName, CancellationToken cancellationToken)
        {
            RequestedPermissions.Add(permissionName);
            return Task.FromResult(HasPermission);
        }

        public Task<LiveDatabaseOwnership?> ReadDatabaseOwnershipAsync(
            string databaseName,
            CancellationToken cancellationToken) =>
            Task.FromResult<LiveDatabaseOwnership?>(null);

        public Task ExecuteAsync(
            string sql,
            IReadOnlyDictionary<string, object?> parameters,
            CancellationToken cancellationToken)
        {
            Commands.Add(sql);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
