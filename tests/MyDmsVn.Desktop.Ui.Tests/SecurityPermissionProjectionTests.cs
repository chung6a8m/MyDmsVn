using System;
using System.Linq;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application.Security;
using Xunit;

namespace MyDmsVn.Desktop.Ui.Tests;

public sealed class SecurityPermissionProjectionTests
{
    private const string Key = "Sales.Receipts.Post";
    private static SecurityProjectionSnapshot Snapshot(bool? direct = null, bool active = true, bool protectedUser = false, bool roleActive = true, SecurityDatasetState rolesState = SecurityDatasetState.Ready, SecurityDatasetState catalogState = SecurityDatasetState.Ready, string status = "Active", bool missingRole = false, long roleSession = 7)
    {
        var user = new SecurityUserDto(1, "user", "User", null, active, 20, new[] { 2 }, new[] { new SecurityDecisionDto(Key, direct) }, protectedUser);
        return new SecurityProjectionSnapshot(user,
            new SecurityDataset<SecurityUserDto>(SecurityDatasetState.Ready, 7, true, new[] { user }),
            new SecurityDataset<SecurityRoleDto>(rolesState, roleSession, true, missingRole ? Array.Empty<SecurityRoleDto>() : new[] { new SecurityRoleDto(2, "Sales", roleActive, 30, new[] { 1 }, new[] { Key }) }),
            new SecurityDataset<PermissionCatalogItemDto>(catalogState, 7, true, new[] { new PermissionCatalogItemDto(Key, "Sales", "Post", status) }), 7);
    }

    [Fact]
    public void Direct_deny_explains_precedence_over_active_role_grant()
    {
        var result = SecurityPermissionProjection.Evaluate(Snapshot(false), Key);
        Assert.Equal(SecurityPermissionResult.Denied, result.Result);
        Assert.Equal(SecurityPermissionReason.DirectDeny, result.Reason);
        Assert.Single(result.RoleSources);
    }

    [Fact]
    public void Missing_role_data_does_not_fabricate_default_deny()
    {
        Assert.Equal(SecurityPermissionResult.Unavailable, SecurityPermissionProjection.Evaluate(Snapshot(missingRole: true), Key).Result);
        Assert.Equal(SecurityPermissionResult.Unavailable, SecurityPermissionProjection.Evaluate(Snapshot(rolesState: SecurityDatasetState.Forbidden), Key).Result);
    }

    [Fact]
    public void Unavailable_catalog_does_not_classify_every_key_as_unknown()
    {
        var result = SecurityPermissionProjection.Evaluate(Snapshot(catalogState: SecurityDatasetState.Failed), "Other.Key");
        Assert.Equal(SecurityPermissionReason.CatalogUnavailable, result.Reason);
        Assert.Equal(SecurityPermissionResult.Unavailable, result.Result);
    }

    [Fact]
    public void Protected_marker_does_not_claim_superuser_bypass() => Assert.Equal(SecurityPermissionResult.Unavailable, SecurityPermissionProjection.Evaluate(Snapshot(true, protectedUser: true), Key).Result);
    [Theory]
    [InlineData(true, true, true, "Active", SecurityPermissionResult.Allowed)]
    [InlineData(false, true, true, "Active", SecurityPermissionResult.Denied)]
    [InlineData(null, true, true, "Active", SecurityPermissionResult.Allowed)]
    [InlineData(null, true, false, "Active", SecurityPermissionResult.Unassigned)]
    [InlineData(true, false, true, "Active", SecurityPermissionResult.Denied)]
    [InlineData(true, true, true, "Deprecated", SecurityPermissionResult.Denied)]
    public void Projection_obeys_activity_and_decision_precedence(bool? direct, bool active, bool roleActive, string status, SecurityPermissionResult expected) => Assert.Equal(expected, SecurityPermissionProjection.Evaluate(Snapshot(direct, active, roleActive: roleActive, status: status), Key).Result);

    [Fact]
    public void Direct_decision_retains_incomplete_provenance() => Assert.False(SecurityPermissionProjection.Evaluate(Snapshot(true, missingRole: true), Key).IsProvenanceComplete);

    [Fact]
    public void Stale_session_roles_cannot_supply_grants() => Assert.Equal(SecurityPermissionResult.Unavailable, SecurityPermissionProjection.Evaluate(Snapshot(roleSession: 6), Key).Result);

    [Fact]
    public void Revoked_user_data_cannot_expose_cached_decisions_or_role_sources()
    {
        var loaded = Snapshot(false);
        var revoked = new SecurityProjectionSnapshot(loaded.User, new SecurityDataset<SecurityUserDto>(SecurityDatasetState.Forbidden, 7, false), loaded.Roles, loaded.Catalog, 7);
        var result = SecurityPermissionProjection.Evaluate(revoked, Key);
        Assert.Equal(SecurityPermissionResult.Unavailable, result.Result);
        Assert.Empty(result.RoleSources);
        Assert.Null(result.DirectDecision);
    }

    [Fact]
    public void Stale_catalog_cannot_supply_permission_classification()
    {
        var loaded = Snapshot();
        var stale = new SecurityProjectionSnapshot(loaded.User, loaded.Users, loaded.Roles, new SecurityDataset<PermissionCatalogItemDto>(SecurityDatasetState.Ready, 7, false, loaded.Catalog.Items), 7);
        Assert.Equal(SecurityPermissionReason.CatalogUnavailable, SecurityPermissionProjection.Evaluate(stale, Key).Reason);
    }

    [Fact]
    public void Known_missing_key_is_denied() => Assert.Equal(SecurityPermissionReason.UnknownPermission, SecurityPermissionProjection.Evaluate(Snapshot(), "Other.Key").Reason);

    [Fact]
    public void Observed_grant_with_missing_other_role_preserves_incomplete_provenance()
    {
        var original = Snapshot();
        var user = new SecurityUserDto(1, "u", "u", null, true, 20, new[] { 2, 3 }, Array.Empty<SecurityDecisionDto>());
        var result = SecurityPermissionProjection.Evaluate(new SecurityProjectionSnapshot(user, original.Users, original.Roles, original.Catalog, 7), Key);
        Assert.Equal(SecurityPermissionResult.Allowed, result.Result);
        Assert.False(result.IsProvenanceComplete);
    }
}
