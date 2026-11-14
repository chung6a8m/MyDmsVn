using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Identity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.HttpHost;

internal sealed class ProtectedFileSecurityAuditSink : ISecurityAuditSink
{
    private static readonly SecurityIdentifier SystemAccount = new(WellKnownSidType.LocalSystemSid, null);
    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private readonly string _root;
    private readonly string _path;
    private readonly SecurityIdentifier _owner;
    private readonly object _writeLock = new();

    public ProtectedFileSecurityAuditSink(WebHostOptions options)
    {
        _root = options.Web6CandidateRoot ?? throw new ArgumentException("Candidate audit root is required.", nameof(options));
        _path = options.SecurityAuditPath ?? throw new ArgumentException("Candidate audit file is required.", nameof(options));
        using var identity = WindowsIdentity.GetCurrent();
        _owner = identity.User ?? throw new InvalidOperationException("Candidate audit owner is unavailable.");

        // Prove that the pre-provisioned destination can be opened and flushed before admitting the host.
        Append(null);
    }

    public Task WriteAsync(SecurityAuditEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var category = entry.DocumentType switch
        {
            "WebAuthSessionReplay" or "WebAuthRefresh" or "WebAuthLogout" or "WebAuthThrottled" => entry.DocumentType,
            _ => "Application",
        };
        var correlation = entry.CorrelationId != null && Regex.IsMatch(entry.CorrelationId, "\\A[A-Za-z0-9_-]{1,128}\\z")
            ? entry.CorrelationId
            : null;
        var record = new JObject
        {
            ["category"] = category,
            ["action"] = Enum.IsDefined(typeof(SecurityAuditAction), entry.Action) ? entry.Action.ToString() : "Unknown",
            ["outcome"] = Enum.IsDefined(typeof(SecurityAuditOutcome), entry.Outcome) ? entry.Outcome.ToString() : "Unknown",
            ["userId"] = entry.UserId > 0 ? entry.UserId : null,
            ["correlationId"] = correlation,
            ["utc"] = entry.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture),
        };
        var line = Encoding.UTF8.GetBytes(record.ToString(Formatting.None) + "\n");

        lock (_writeLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Append(line);
        }

        return Task.CompletedTask;
    }

    private void Append(byte[]? line)
    {
        try
        {
            ValidateDestination();
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Write, FileShare.Read);
            if (line != null)
            {
                stream.Seek(0, SeekOrigin.End);
                stream.Write(line, 0, line.Length);
            }

            stream.Flush(flushToDisk: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException)
        {
            throw new IOException("Protected security audit storage is unavailable.");
        }
    }

    private void ValidateDestination()
    {
        const FileAttributes excluded = FileAttributes.ReparsePoint | FileAttributes.Device;
        var rootPrefix = _root + Path.DirectorySeparatorChar;
        var parents = new Stack<string>();
        for (var directory = Path.GetDirectoryName(_path); directory != null; directory = Path.GetDirectoryName(directory))
            parents.Push(directory);

        // Validate each ancestor before any filesystem lookup through it.
        foreach (var directory in parents)
        {
            var attributes = File.GetAttributes(directory);
            if ((attributes & excluded) != 0 || (attributes & FileAttributes.Directory) == 0)
                throw new UnauthorizedAccessException();

            if (directory.Equals(_root, StringComparison.Ordinal) ||
                directory.StartsWith(rootPrefix, StringComparison.Ordinal))
                RequirePrivateAccess(Directory.GetAccessControl(directory, AccessControlSections.Owner | AccessControlSections.Access));
        }

        if ((File.GetAttributes(_path) & (excluded | FileAttributes.Directory)) != 0)
            throw new UnauthorizedAccessException();

        RequirePrivateAccess(File.GetAccessControl(_path, AccessControlSections.Owner | AccessControlSections.Access));
    }

    private void RequirePrivateAccess(FileSystemSecurity security)
    {
        var descriptor = new RawSecurityDescriptor(security.GetSecurityDescriptorBinaryForm(), 0);
        if (!_owner.Equals(descriptor.Owner) || descriptor.DiscretionaryAcl == null)
            throw new UnauthorizedAccessException();

        foreach (GenericAce rule in descriptor.DiscretionaryAcl)
        {
            if (rule is not CommonAce ace || ace.IsCallback ||
                ace.AceQualifier is not (AceQualifier.AccessDenied or AceQualifier.AccessAllowed))
                throw new UnauthorizedAccessException();

            var identity = ace.SecurityIdentifier;
            if (!identity.Equals(_owner) && !identity.Equals(SystemAccount) && !identity.Equals(Administrators))
                throw new UnauthorizedAccessException();
        }
    }
}
