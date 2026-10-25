using System;
using System.Collections.Generic;
using System.Linq;

namespace MyDmsVn.Desktop.Application.Security;

public sealed class SecurityPermissionChange
{
    public SecurityPermissionChange(string permissionKey, bool granted)
    {
        PermissionKey = permissionKey;
        Granted = granted;
    }
    public string PermissionKey { get; }
    public bool Granted { get; }
}
/// <summary>Immutable intent captured for one serial save; server and editor revisions have separate meanings.</summary>
public sealed class SecurityRolePermissionSubmission
{
    internal SecurityRolePermissionSubmission(SecurityRolePermissionDraft draft)
    {
        RoleId = draft.RoleId;
        ServerRevision = draft.ServerRevision;
        SessionVersion = draft.SessionVersion;
        EditorId = draft.EditorId;
        LocalEditRevision = draft.LocalEditRevision;
        OriginalKeys = Array.AsReadOnly(draft.OriginalKeys.ToArray());
        DesiredKeys = Array.AsReadOnly(draft.DesiredKeys.ToArray());
        Changes = Array.AsReadOnly(draft.Changes.ToArray());
    }
    public int RoleId { get; }
    public long ServerRevision { get; }
    public long SessionVersion { get; }
    public Guid EditorId { get; }
    public long LocalEditRevision { get; }
    public IReadOnlyList<string> OriginalKeys { get; }
    public IReadOnlyList<string> DesiredKeys { get; }
    public IReadOnlyList<SecurityPermissionChange> Changes { get; }
}
/// <summary>Owns all role edits independently of the visible filter. Non-Active historical keys remain unchanged.</summary>
public sealed class SecurityRolePermissionDraft
{
    private readonly HashSet<string> _original;
    private readonly HashSet<string> _desired;
    private readonly HashSet<string> _active;
    public SecurityRolePermissionDraft(int roleId, long serverRevision, IEnumerable<string> originalKeys, IEnumerable<string> activeKeys, long sessionVersion, Guid? editorId = null)
    {
        if (roleId <= 0) throw new ArgumentOutOfRangeException(nameof(roleId));
        RoleId = roleId;
        ServerRevision = serverRevision;
        SessionVersion = sessionVersion;
        EditorId = editorId ?? Guid.NewGuid();
        _original = new HashSet<string>(originalKeys, StringComparer.Ordinal);
        _desired = new HashSet<string>(_original, StringComparer.Ordinal);
        _active = new HashSet<string>(activeKeys, StringComparer.Ordinal);
    }
    public int RoleId { get; }
    public long ServerRevision { get; }
    public long SessionVersion { get; }
    public Guid EditorId { get; }
    public long LocalEditRevision { get; private set; }
    public IReadOnlyList<string> OriginalKeys => Array.AsReadOnly(_original.OrderBy(k => k, StringComparer.Ordinal).ToArray());
    public IReadOnlyList<string> DesiredKeys => Array.AsReadOnly(_desired.OrderBy(k => k, StringComparer.Ordinal).ToArray());
    public IReadOnlyList<SecurityPermissionChange> Changes => Array.AsReadOnly(_active.Where(k => _original.Contains(k) != _desired.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).Select(k => new SecurityPermissionChange(k, _desired.Contains(k))).ToArray());
    public bool IsDirty => Changes.Count != 0;
    public void SetGranted(string permissionKey, bool granted)
    {
        if (!_active.Contains(permissionKey)) throw new InvalidOperationException("Only loaded Active keys are editable.");
        if (granted ? _desired.Add(permissionKey) : _desired.Remove(permissionKey)) LocalEditRevision++;
    }
    public SecurityRolePermissionSubmission FreezeSubmission() => new(this);
}
