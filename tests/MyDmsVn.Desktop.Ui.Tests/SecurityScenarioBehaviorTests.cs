using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Application.Security;
using MyDmsVn.Desktop.UiLab.Scenarios;
using Xunit;

namespace MyDmsVn.Desktop.Tests;
public sealed class SecurityScenarioBehaviorTests
{
    [Fact]
    public async Task Held_import_preview_cannot_publish_after_session_replacement()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L09);
        using var viewModel = new SecurityWorkspaceViewModel(scene.Administration, scene.Catalog,
            scene.Session, new ImmediateUiDispatcher(), scene.Session.Version);
        await viewModel.LoadAsync(default);
        viewModel.SelectRole(202);
        const string json = "{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[{\"permissionKey\":\"Sales.Receipts.Read\",\"decision\":\"Allow\"}]}";
        var gate = scene.Administration.HoldNextImportPreview();

        var pending = viewModel.PreviewImportAsync(PermissionImportTargetTypeDto.Role, json, default);
        Assert.False(pending.IsCompleted);
        scene.Session.Replace();
        gate.Complete(true);

        Assert.False(await pending);
        Assert.Null(viewModel.ImportPreview);
        Assert.Null(viewModel.SelectedRoleId);
        Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation == "Security.ImportApply");
    }

    [Fact]
    public void Review_fixture_aliases_have_required_assignments_and_catalog_states()
    {
        using var s = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        var active = Assert.Single(s.Data.Users, user => user.UserId == 101);
        var inactive = Assert.Single(s.Data.Users, user => user.UserId == 102);
        var protectedUser = Assert.Single(s.Data.Users, user => user.UserId == 103);
        var activeRole = Assert.Single(s.Data.Roles, role => role.RoleId == 201);
        var otherRole = Assert.Single(s.Data.Roles, role => role.RoleId == 202);
        var inactiveRole = Assert.Single(s.Data.Roles, role => role.RoleId == 203);

        Assert.True(active.IsActive);
        Assert.Equal(2, active.RoleIds.Count);
        Assert.Contains(active.DirectDecisions, decision => decision.Decision == true);
        Assert.Contains(active.DirectDecisions, decision => decision.Decision == false);
        Assert.DoesNotContain(active.DirectDecisions, decision => decision.PermissionKey == "Sales.Receipts.Read");
        Assert.False(inactive.IsActive);
        Assert.NotEmpty(inactive.RoleIds);
        Assert.True(protectedUser.IsProtectedAccount);

        Assert.True(activeRole.IsActive);
        Assert.True(activeRole.MemberUserIds.Count >= 2);
        Assert.True(activeRole.PermissionKeys.Count >= 2);
        Assert.True(otherRole.IsActive);
        Assert.False(inactiveRole.IsActive);
        Assert.Contains("Sales.Receipts.Read", inactiveRole.PermissionKeys);
        Assert.Contains("Inventory.Stock.Audit", inactiveRole.PermissionKeys);
        Assert.DoesNotContain("Inventory.Stock.Audit", activeRole.PermissionKeys);

        Assert.True(s.Data.Catalog.Items.Select(item => item.Module).Distinct().Count() >= 2);
        Assert.Contains(s.Data.Catalog.Items, item => item.Key == "Legacy.Deprecated" && item.Status == "Deprecated");
        Assert.Contains(s.Data.Catalog.Items, item => item.Key == "Inventory.Stock.Export" && item.Status == "Active" && s.Data.Roles.All(role => !role.PermissionKeys.Contains(item.Key)) && s.Data.Users.All(user => user.DirectDecisions.All(decision => decision.PermissionKey != item.Key)));
    }

    [Fact]
    public async Task Independent_reads_preserve_authorized_panes()
    {
        using var s = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        s.Administration.UsersError = new ApiError(ApiStatusCode.Forbidden, "Forbidden", "Denied");
        Assert.False((await s.Administration.ListUsersAsync(default)).IsSuccess);
        Assert.True((await s.Administration.ListRolesAsync(default)).IsSuccess);
        Assert.True((await s.Catalog.GetCatalogAsync(default)).IsSuccess);
    }

    [Fact]
    public async Task Membership_returns_both_revisions()
    {
        using var s = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        var r = await s.Administration.MutateAsync(new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.User, SecurityAdministrationOperationDto.SetMembership, 101, 202, isActive: true, expectedRevision: 11, expectedRelatedRevision: 22), default);
        Assert.True(r.IsSuccess);
        Assert.Equal(12, r.Data!.AssignmentRevision);
        Assert.Equal(23, r.Data.RelatedAssignmentRevision);
    }

    [Fact]
    public async Task Removing_membership_preserves_user_activity()
    {
        using var s = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L04);
        var r = await s.Administration.MutateAsync(new SecurityAdministrationMutationRequest(
            SecurityAdministrationTargetDto.User, SecurityAdministrationOperationDto.SetMembership,
            101, 203, isActive: false, expectedRevision: 11, expectedRelatedRevision: 23), default);

        Assert.True(r.IsSuccess);
        Assert.True(Assert.Single(s.Data.Users, user => user.UserId == 101).IsActive);
        Assert.DoesNotContain(203, Assert.Single(s.Data.Users, user => user.UserId == 101).RoleIds);

        var assignInactive = await s.Administration.MutateAsync(new SecurityAdministrationMutationRequest(
            SecurityAdministrationTargetDto.User, SecurityAdministrationOperationDto.SetMembership,
            102, 202, isActive: true, expectedRevision: 12, expectedRelatedRevision: 22), default);
        Assert.True(assignInactive.IsSuccess);
        Assert.False(Assert.Single(s.Data.Users, user => user.UserId == 102).IsActive);
        Assert.Contains(202, Assert.Single(s.Data.Users, user => user.UserId == 102).RoleIds);
    }

    [Fact]
    public async Task Held_mutations_can_complete_after_cancellation_without_retargeting()
    {
        using var s = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        var g = s.Administration.HoldNextMutation(true);
        using var c = new CancellationTokenSource();
        var t = s.Administration.MutateAsync(new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.Role, SecurityAdministrationOperationDto.SetPermission, 201, permissionKey: "Sales.Receipts.Read", decision: false, expectedRevision: 21), c.Token);
        c.Cancel();
        s.Session.Replace();
        g.Complete(ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(22, true)));
        Assert.Equal("Auth.SessionChanged", (await t).Error!.Code);
        Assert.Equal("Auth.SessionChanged", (await s.Administration.ListUsersAsync(default)).Error!.Code);
        Assert.True(await s.DrainAsync(1000));
    }

    [Fact]
    public async Task Password_requests_are_redacted_before_journaling()
    {
        using var s = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        await s.Administration.MutateAsync(new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.User, SecurityAdministrationOperationDto.SetPassword, 101, password: "sensitive-password", expectedRevision: 11), default);
        Assert.All(s.Journal.Entries, e => Assert.IsNotType<SecurityAdministrationMutationRequest>(e.Request));
        Assert.DoesNotContain("sensitive-password", string.Join(" ", s.Journal.Entries));
    }

    [Fact]
    public async Task Import_revalidates_revision_and_digest_without_mutating_on_conflict()
    {
        using var s = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L09);
        const string json = "{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[{\"permissionKey\":\"Sales.Receipts.Read\",\"decision\":\"Allow\"}]}";
        var p = await s.Administration.PreviewPermissionImportAsync(PermissionImportTargetTypeDto.Role, 202, json, default);
        Assert.True(p.IsSuccess);
        var bad = await s.Administration.ApplyPermissionImportAsync(PermissionImportTargetTypeDto.Role, 202, new PermissionAssignmentImportRequest(json, 22, "stale"), default);
        Assert.Equal(ApiStatusCode.Conflict, bad.Error!.Status);
        Assert.Equal(22, s.Data.Roles[1].AssignmentRevision);
        var good = await s.Administration.ApplyPermissionImportAsync(PermissionImportTargetTypeDto.Role, 202, new PermissionAssignmentImportRequest(json, 22, p.Data!.CatalogDigest), default);
        Assert.True(good.IsSuccess);
        Assert.Equal(23, good.Data!.TargetRevision);
    }

    [Fact]
    public async Task Role_noop_preserves_revision_and_partial_failure_preserves_first_commit()
    {
        using var s = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L08);
        var first = await s.Administration.MutateAsync(new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.Role, SecurityAdministrationOperationDto.SetPermission, 201, permissionKey: "Sales.Receipts.Post", decision: true, expectedRevision: 21), default);
        Assert.True(first.IsSuccess);
        Assert.Equal(22, first.Data!.AssignmentRevision);
        var second = await s.Administration.MutateAsync(new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.Role, SecurityAdministrationOperationDto.SetPermission, 201, permissionKey: "Sales.Receipts.Read", decision: false, expectedRevision: 22), default);
        Assert.Equal(ApiStatusCode.Conflict, second.Error!.Status);
        Assert.Contains("Sales.Receipts.Post", s.Data.Roles[0].PermissionKeys);
        var noop = await s.Administration.MutateAsync(new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.Role, SecurityAdministrationOperationDto.SetPermission, 201, permissionKey: "Sales.Receipts.Post", decision: true, expectedRevision: 22), default);
        Assert.False(noop.Data!.Changed);
        Assert.Equal(22, noop.Data.AssignmentRevision);
    }

    [Fact]
    public async Task Drain_times_out_then_cancels_all_rearmed_held_reads()
    {
        using var s = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L12);
        s.Administration.UsersGate = new ScenarioGate<ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>>
        {
            IgnoreCancellation = true
        };
        var old = s.Administration.ListUsersAsync(default);
        s.Administration.UsersGate = new ScenarioGate<ApiResponse<System.Collections.Generic.IReadOnlyList<SecurityUserDto>>>
        {
            IgnoreCancellation = true
        };
        var current = s.Administration.ListUsersAsync(default);
        Assert.False(await s.DrainAsync(30));
        s.CancelPending();
        Assert.True(await s.DrainAsync(1000));
        Assert.True(old.IsCanceled);
        Assert.True(current.IsCanceled);
    }

    [Fact]
    public async Task Held_success_commits_fixture_before_fencing_the_old_session()
    {
        using var s = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L01);
        var gate = s.Administration.HoldNextMutation(true);
        var pending = s.Administration.MutateAsync(new SecurityAdministrationMutationRequest(SecurityAdministrationTargetDto.Role, SecurityAdministrationOperationDto.SetPermission, 201, permissionKey: "Sales.Receipts.Post", decision: true, expectedRevision: 21), default);
        Assert.DoesNotContain("Sales.Receipts.Post", s.Data.Roles[0].PermissionKeys);
        s.Session.Replace();
        gate.Complete(ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(22, true)));
        Assert.Equal("Auth.SessionChanged", (await pending).Error!.Code);
        Assert.Contains("Sales.Receipts.Post", s.Data.Roles[0].PermissionKeys);
        Assert.Equal(22, s.Data.Roles[0].AssignmentRevision);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[],\"extra\":true}")]
    [InlineData("{\"schemaVersion\":\"1\",\"targetType\":\"Role\",\"targetId\":202,\"entries\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"targetType\":\"role\",\"targetId\":202,\"entries\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":\"202\",\"entries\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[{\"permissionKey\":\"Sales.Receipts.Read\",\"decision\":\"Allow\",\"extra\":0}]}")]
    [InlineData("{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[{\"permissionKey\":\"Sales.Receipts.Read\",\"decision\":\"Allow\"},{\"permissionKey\":\"Sales.Receipts.Read\",\"decision\":\"Allow\"}]}")]
    [InlineData("/* comment */{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[]}")]
    public async Task Import_rejects_noncanonical_schema_v1_documents(string json)
    {
        using var s = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L09);
        var response = await s.Administration.PreviewPermissionImportAsync(PermissionImportTargetTypeDto.Role, 202, json, default);
        Assert.Equal(ApiStatusCode.BadRequest, response.Error!.Status);
        Assert.Equal("ValidationError", response.Error.Code);
        Assert.Contains(response.Error.Details, x => x.Code == "Security.ImportInvalid");
    }

    [Theory]
    [InlineData(102)]
    [InlineData(103)]
    public async Task Import_unavailable_target_has_application_conflict_envelope(int id)
    {
        using var s = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L09);
        var json = "{\"schemaVersion\":1,\"targetType\":\"User\",\"targetId\":" + id + ",\"entries\":[]}";
        var r = await s.Administration.PreviewPermissionImportAsync(PermissionImportTargetTypeDto.User, id, json, default);
        Assert.Equal(ApiStatusCode.Conflict, r.Error!.Status);
        Assert.Equal("Security.ImportTargetUnavailable", r.Error.Code);
    }

    [Fact]
    public async Task Import_existing_opposite_decision_has_application_conflict_envelope()
    {
        using var s = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L09);
        const string json = "{\"schemaVersion\":1,\"targetType\":\"User\",\"targetId\":101,\"entries\":[{\"permissionKey\":\"Sales.Receipts.Post\",\"decision\":\"Allow\"}]}";
        var r = await s.Administration.PreviewPermissionImportAsync(PermissionImportTargetTypeDto.User, 101, json, default);
        Assert.Equal(ApiStatusCode.Conflict, r.Error!.Status);
        Assert.Equal("Security.ImportDecisionConflict", r.Error.Code);
    }

    [Theory]
    [InlineData(22, "stale", ApiStatusCode.Conflict, "Security.ImportCatalogChanged", null)]
    [InlineData(21, "security-fixture-v1", ApiStatusCode.Conflict, "Security.RevisionConflict", null)]
    [InlineData(-1, "security-fixture-v1", ApiStatusCode.BadRequest, "ValidationError", "Security.ImportRevisionInvalid")]
    public async Task Apply_import_preserves_application_stale_envelopes(long revision, string digest, ApiStatusCode status, string code, string? detail)
    {
        using var s = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L09);
        const string json = "{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[{\"permissionKey\":\"Sales.Receipts.Read\",\"decision\":\"Allow\"}]}";
        var result = await s.Administration.ApplyPermissionImportAsync(PermissionImportTargetTypeDto.Role, 202, new PermissionAssignmentImportRequest(json, revision, digest), default);
        Assert.Equal(status, result.Error!.Status);
        Assert.Equal(code, result.Error.Code);
        if (detail != null)
            Assert.Contains(result.Error.Details, x => x.Code == detail);
        Assert.Equal(22, s.Data.Roles[1].AssignmentRevision);
        Assert.DoesNotContain("Sales.Receipts.Read", s.Data.Roles[1].PermissionKeys);
    }
}
