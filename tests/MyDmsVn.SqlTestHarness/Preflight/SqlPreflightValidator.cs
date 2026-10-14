using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.SqlTestHarness.Approval;

namespace MyDmsVn.SqlTestHarness.Preflight;

public sealed class SqlPreflightValidator
{
    private readonly Func<string, ISqlAdministrativeSession> _sessionFactory;
    private readonly Func<DateTimeOffset> _utcNow;

    public SqlPreflightValidator()
        : this(
            connectionString => new SqlClientAdministrativeSession(connectionString),
            () => DateTimeOffset.UtcNow)
    {
    }

    public SqlPreflightValidator(
        Func<string, ISqlAdministrativeSession> sessionFactory,
        Func<DateTimeOffset> utcNow)
    {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    public async Task<SqlPreflightResult> ValidateAsync(
        string? connectionString,
        SqlInstanceApproval approval,
        SqlPreflightRequirements requirements,
        CancellationToken cancellationToken)
    {
        if (approval is null)
        {
            throw new ArgumentNullException(nameof(approval));
        }

        if (requirements is null)
        {
            throw new ArgumentNullException(nameof(requirements));
        }

        if (!TryValidateConnectionString(connectionString, out var normalizedConnectionString))
        {
            return Failed(
                SqlPreflightFailureKind.InvalidConfiguration,
                "The SQL test connection string must be valid and explicitly target master.");
        }

        var now = _utcNow();
        if (approval.ValidUntilUtc is not null && approval.ValidUntilUtc <= now)
        {
            return Failed(
                SqlPreflightFailureKind.ApprovalExpired,
                "The SQL instance approval has expired.");
        }

        using var session = _sessionFactory(normalizedConnectionString!);
        try
        {
            await session.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return Failed(
                SqlPreflightFailureKind.ConnectionFailure,
                "The approved SQL instance could not be reached.",
                exception);
        }

        string? effectiveDatabase;
        try
        {
            effectiveDatabase = await session.ReadEffectiveDatabaseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return Failed(
                SqlPreflightFailureKind.WrongEffectiveDatabase,
                "The effective SQL database could not be verified as master.",
                exception);
        }

        if (!string.Equals(effectiveDatabase, "master", StringComparison.OrdinalIgnoreCase))
        {
            return Failed(
                SqlPreflightFailureKind.WrongEffectiveDatabase,
                "The effective SQL database is not master.");
        }

        SqlInstanceIdentity? identity;
        try
        {
            identity = await session.ReadIdentityAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return Failed(
                SqlPreflightFailureKind.IdentityUnavailable,
                "The SQL instance identity could not be read.",
                exception);
        }

        if (identity is null)
        {
            return Failed(
                SqlPreflightFailureKind.IdentityUnavailable,
                "The SQL instance identity is incomplete.");
        }

        var actualFingerprint = SqlInstanceFingerprint.Create(identity);
        if (!IdentitiesMatch(identity, approval.Identity) ||
            !string.Equals(actualFingerprint, approval.FingerprintSha256, StringComparison.Ordinal))
        {
            return Failed(
                SqlPreflightFailureKind.ApprovalMismatch,
                "The connected SQL instance does not match the operator approval.");
        }

        if (requirements.RequireCrossSessionDmvPermission)
        {
            int? majorVersion;
            try
            {
                majorVersion = await session.ReadMajorVersionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                return Failed(
                    SqlPreflightFailureKind.VersionUnavailable,
                    "The SQL Server major version could not be read.",
                    exception);
            }

            if (majorVersion is null)
            {
                return Failed(
                    SqlPreflightFailureKind.VersionUnavailable,
                    "The SQL Server major version is unavailable.");
            }

            var permission = majorVersion >= 16
                ? "VIEW SERVER PERFORMANCE STATE"
                : "VIEW SERVER STATE";
            bool? hasPermission;
            try
            {
                hasPermission = await session.HasServerPermissionAsync(permission, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                return Failed(
                    SqlPreflightFailureKind.MissingPermission,
                    $"The required SQL permission '{permission}' could not be verified.",
                    exception);
            }

            if (hasPermission != true)
            {
                return Failed(
                    SqlPreflightFailureKind.MissingPermission,
                    $"The approved SQL test identity requires '{permission}'.");
            }
        }

        return SqlPreflightResult.Success(
            new SqlMutationAuthorization(
                approval.ApprovalId,
                actualFingerprint,
                approval.ConfigurationSha256,
                identity,
                now));
    }

    private static bool TryValidateConnectionString(
        string? connectionString,
        out string? normalizedConnectionString)
    {
        normalizedConnectionString = null;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.DataSource) ||
                !string.Equals(builder.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            normalizedConnectionString = builder.ConnectionString;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IdentitiesMatch(SqlInstanceIdentity actual, SqlInstanceIdentity approved) =>
        string.Equals(actual.ServerName, approved.ServerName, StringComparison.Ordinal) &&
        string.Equals(actual.MachineName, approved.MachineName, StringComparison.Ordinal) &&
        string.Equals(actual.InstanceName, approved.InstanceName, StringComparison.Ordinal) &&
        string.Equals(actual.Endpoint, approved.Endpoint, StringComparison.Ordinal);

    private static SqlPreflightResult Failed(
        SqlPreflightFailureKind kind,
        string message,
        Exception? exception = null) =>
        SqlPreflightResult.Failed(new SqlPreflightFailure(kind, message, exception));
}
