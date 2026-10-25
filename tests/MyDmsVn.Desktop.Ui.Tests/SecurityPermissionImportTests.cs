using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Application.Security;
using MyDmsVn.Desktop.UiLab.Scenarios;
using MyDmsVn.Desktop.WinForms.Features.Security;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class SecurityPermissionImportTests
{
    [Fact]
    public async Task Apply_uses_the_original_json_target_revision_and_catalog_digest()
    {
        const string originalJson = "{\n  \"schemaVersion\": 1, \"targetType\": \"Role\"\n}";
        var client = new ImportClient();
        var import = new SecurityPermissionImportCoordinator(client, () => true);

        var preview = await import.PreviewAsync(PermissionImportTargetTypeDto.Role, 201,
            originalJson, 4, "digest-1", CancellationToken.None);
        var applied = await import.ApplyPreviewAsync(PermissionImportTargetTypeDto.Role, 201,
            originalJson, 4, "digest-1", true, CancellationToken.None);

        Assert.True(preview.IsSuccess);
        Assert.True(applied!.IsSuccess);
        Assert.Equal(originalJson, client.AppliedRequest!.Json);
        Assert.Equal(21, client.AppliedRequest.ExpectedTargetRevision);
        Assert.Equal("digest-1", client.AppliedRequest.ExpectedCatalogDigest);
        Assert.Equal(201, client.AppliedTargetId);
    }

    [Fact]
    public async Task Target_or_catalog_change_invalidates_preview_without_retry()
    {
        var client = new ImportClient();
        var import = new SecurityPermissionImportCoordinator(client, () => true);
        await import.PreviewAsync(PermissionImportTargetTypeDto.User, 101, "json", 2, "digest-1", CancellationToken.None);

        Assert.Null(await import.ApplyPreviewAsync(PermissionImportTargetTypeDto.User, 102,
            "json", 2, "digest-1", true, CancellationToken.None));
        Assert.Null(await import.ApplyPreviewAsync(PermissionImportTargetTypeDto.User, 101,
            "json", 2, "digest-2", true, CancellationToken.None));
        Assert.Equal(0, client.ApplyCount);
    }

    [Fact]
    public async Task Cancel_and_changed_owner_send_no_apply()
    {
        var client = new ImportClient();
        var current = true;
        var import = new SecurityPermissionImportCoordinator(client, () => current);
        await import.PreviewAsync(PermissionImportTargetTypeDto.User, 101, "json", 2, "digest-1", CancellationToken.None);

        Assert.Null(await import.ApplyPreviewAsync(PermissionImportTargetTypeDto.User, 101,
            "json", 2, "digest-1", false, CancellationToken.None));
        current = false;
        Assert.Null(await import.ApplyPreviewAsync(PermissionImportTargetTypeDto.User, 101,
            "json", 2, "digest-1", true, CancellationToken.None));
        Assert.Equal(0, client.ApplyCount);
    }

    [Fact]
    public async Task Cancellation_after_apply_dispatch_retains_the_received_response()
    {
        var client = new ImportClient();
        var import = new SecurityPermissionImportCoordinator(client, () => true);
        await import.PreviewAsync(PermissionImportTargetTypeDto.Role, 201,
            "json", 2, "digest-1", CancellationToken.None);
        using var source = new CancellationTokenSource();
        client.ApplyEntered = source.Cancel;

        var response = await import.ApplyPreviewAsync(PermissionImportTargetTypeDto.Role, 201,
            "json", 2, "digest-1", true, source.Token);

        Assert.Equal(1, client.ApplyCount);
        Assert.True(response!.IsSuccess);
    }

    [Fact]
    public async Task Workspace_import_is_contextual_and_reload_confirms_addition()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L09);
        using var viewModel = new SecurityWorkspaceViewModel(scene.Administration, scene.Catalog,
            scene.Session, new ImmediateUiDispatcher(), scene.Session.Version);
        await viewModel.LoadAsync(default);
        viewModel.SelectRole(202);
        const string json = "{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[{\"permissionKey\":\"Sales.Receipts.Read\",\"decision\":\"Allow\"}]}";

        Assert.True(await viewModel.PreviewImportAsync(PermissionImportTargetTypeDto.Role, json, default));
        Assert.Equal(22, viewModel.ImportPreview!.TargetRevision);
        Assert.True(await viewModel.ApplyImportPreviewAsync(PermissionImportTargetTypeDto.Role, json, true, default));
        Assert.Contains("Sales.Receipts.Read", viewModel.SelectedRole!.PermissionKeys);
        Assert.Equal(2, scene.Journal.Entries.Count(entry => entry.Operation.Contains("Import")));
    }

    [Fact]
    public async Task Workspace_target_switch_and_cancel_do_not_apply()
    {
        using var scene = SecurityScenarioCatalog.Create(SecurityScenarioId.SEC_L09);
        using var viewModel = new SecurityWorkspaceViewModel(scene.Administration, scene.Catalog,
            scene.Session, new ImmediateUiDispatcher(), scene.Session.Version);
        await viewModel.LoadAsync(default);
        viewModel.SelectRole(202);
        const string json = "{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[]}";

        Assert.True(await viewModel.PreviewImportAsync(PermissionImportTargetTypeDto.Role, json, default));
        Assert.False(await viewModel.ApplyImportPreviewAsync(PermissionImportTargetTypeDto.Role, json, false, default));
        Assert.Null(viewModel.ImportPreview);
        Assert.True(await viewModel.PreviewImportAsync(PermissionImportTargetTypeDto.Role, json, default));
        viewModel.SelectRole(201);
        Assert.Null(viewModel.ImportPreview);
        Assert.False(await viewModel.ApplyImportPreviewAsync(PermissionImportTargetTypeDto.Role, json, true, default));
        Assert.DoesNotContain(scene.Journal.Entries, entry => entry.Operation.Contains("ApplyImport"));
    }

    [Fact]
    public void Control_import_confirms_the_visible_role_and_sends_one_apply()
    {
        SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (control, scene) =>
        {
            control.ViewModel.SelectRole(202);
            control.Tabs.SelectedIndex = 1;
            const string json = "{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":202,\"entries\":[{\"permissionKey\":\"Sales.Receipts.Read\",\"decision\":\"Allow\"}]}";
            var confirmations = 0;
            control.SetImportDialogs(() => json, summary =>
            {
                confirmations++;
                Assert.Contains("#202", summary);
                Assert.Contains("Sales.Receipts.Read", summary);
                return true;
            });

            control.RoleImportButton.PerformClick();
            Pump(control.LastOperation);

            Assert.Equal(1, confirmations);
            Assert.Single(scene.Journal.Entries, entry => entry.Operation == "Security.ImportApply");
            Assert.Contains("Sales.Receipts.Read", control.ViewModel.SelectedRole!.PermissionKeys);
        });
    }

    private static void Pump(Task task)
    {
        var elapsed = Stopwatch.StartNew();
        while (!task.IsCompleted && elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Yield();
        }
        Assert.True(task.IsCompleted);
        task.GetAwaiter().GetResult();
    }

    private sealed class ImportClient : ISecurityAdministrationApiClient
    {
        public int ApplyCount { get; private set; }
        public int AppliedTargetId { get; private set; }
        public PermissionAssignmentImportRequest? AppliedRequest { get; private set; }
        public Action? ApplyEntered { get; set; }
        public Task<ApiResponse<PermissionAssignmentImportPreviewDto>> PreviewPermissionImportAsync(
            PermissionImportTargetTypeDto targetType, int targetId, string json, CancellationToken token)
        {
            var parsed = new ParsedPermissionAssignmentImportDto(1, targetType, targetId,
                Array.Empty<PermissionAssignmentImportEntryDto>());
            return Task.FromResult(ApiResponse<PermissionAssignmentImportPreviewDto>.Success(
                new PermissionAssignmentImportPreviewDto(parsed, "target", 21, "digest-1",
                    Array.Empty<PermissionAssignmentImportEntryDto>())));
        }

        public Task<ApiResponse<PermissionAssignmentImportResultDto>> ApplyPermissionImportAsync(
            PermissionImportTargetTypeDto targetType, int targetId, PermissionAssignmentImportRequest request, CancellationToken token)
        {
            ApplyCount++;
            AppliedTargetId = targetId;
            AppliedRequest = request;
            ApplyEntered?.Invoke();
            return Task.FromResult(ApiResponse<PermissionAssignmentImportResultDto>.Success(
                new PermissionAssignmentImportResultDto(22, 0, false)));
        }

        public Task<ApiResponse<IReadOnlyList<SecurityUserDto>>> ListUsersAsync(CancellationToken token) => throw new NotSupportedException();
        public Task<ApiResponse<IReadOnlyList<SecurityRoleDto>>> ListRolesAsync(CancellationToken token) => throw new NotSupportedException();
        public Task<ApiResponse<SecurityAdministrationResultDto>> MutateAsync(SecurityAdministrationMutationRequest request, CancellationToken token) => throw new NotSupportedException();
    }
}
