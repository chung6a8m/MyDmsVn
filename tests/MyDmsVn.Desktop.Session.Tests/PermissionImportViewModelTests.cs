using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Application.Security;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class PermissionImportViewModelTests
{
    [Fact]
    public async Task Apply_uses_original_json_selected_target_and_preview_revision()
    {
        var api = new FakeAdministrationClient();
        api.Users = new[] { new SecurityUserDto(17, "operator", "Operator", null, true, 6,
            Array.Empty<int>(), Array.Empty<SecurityDecisionDto>()) };
        api.Preview = new PermissionAssignmentImportPreviewDto(
            new ParsedPermissionAssignmentImportDto(1, PermissionImportTargetTypeDto.User, 17,
                new[] { new PermissionAssignmentImportEntryDto("Inventory.Read", true) }),
            "Operator", 6, "catalog-sha256",
            new[] { new PermissionAssignmentImportEntryDto("Inventory.Read", true) });
        using var viewModel = Create(api);
        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.SelectUser(17);
        const string json = "{\"schemaVersion\":1,\"targetType\":\"User\",\"targetId\":17,\"entries\":[{\"permissionKey\":\"Inventory.Read\",\"decision\":\"Allow\"}]}";

        await viewModel.PreviewPermissionImportAsync(PermissionImportTargetTypeDto.User, 17, json, CancellationToken.None);
        await viewModel.ApplyPermissionImportAsync(json, CancellationToken.None);

        Assert.Equal(json, api.PreviewJson);
        Assert.Equal(json, api.ApplyRequest!.Json);
        Assert.Equal(6, api.ApplyRequest.ExpectedTargetRevision);
        Assert.Equal("catalog-sha256", api.ApplyRequest.ExpectedCatalogDigest);
        Assert.Equal(PermissionImportTargetTypeDto.User, api.ApplyTargetType);
        Assert.Equal(17, api.ApplyTargetId);
    }

    [Fact]
    public async Task Selection_change_invalidates_preview_before_apply()
    {
        var api = new FakeAdministrationClient
        {
            Users = new[] { new SecurityUserDto(17, "operator", "Operator", null, true, 6,
                Array.Empty<int>(), Array.Empty<SecurityDecisionDto>()) },
            Preview = new PermissionAssignmentImportPreviewDto(
                new ParsedPermissionAssignmentImportDto(1, PermissionImportTargetTypeDto.User, 17,
                    Array.Empty<PermissionAssignmentImportEntryDto>()),
                "Operator", 6, "catalog-sha256", Array.Empty<PermissionAssignmentImportEntryDto>()),
        };
        using var viewModel = Create(api);
        await viewModel.LoadAsync(CancellationToken.None);
        viewModel.SelectUser(17);
        await viewModel.PreviewPermissionImportAsync(PermissionImportTargetTypeDto.User, 17, "{}", CancellationToken.None);
        viewModel.SelectUser(null);
        await viewModel.ApplyPermissionImportAsync("{}", CancellationToken.None);

        Assert.Null(viewModel.ImportPreview);
        Assert.Null(api.ApplyRequest);
    }

    private static SecurityAdministrationViewModel Create(FakeAdministrationClient api) =>
        new(api, new FakeCatalogClient(), new TestSession(), new DesktopNotificationCenter());

    private sealed class FakeAdministrationClient : ISecurityAdministrationApiClient
    {
        public IReadOnlyList<SecurityUserDto> Users { get; set; } = Array.Empty<SecurityUserDto>();
        public PermissionAssignmentImportPreviewDto? Preview { get; set; }
        public string? PreviewJson { get; private set; }
        public PermissionAssignmentImportRequest? ApplyRequest { get; private set; }
        public PermissionImportTargetTypeDto ApplyTargetType { get; private set; }
        public int ApplyTargetId { get; private set; }

        public Task<ApiResponse<IReadOnlyList<SecurityUserDto>>> ListUsersAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ApiResponse<IReadOnlyList<SecurityUserDto>>.Success(Users));
        public Task<ApiResponse<IReadOnlyList<SecurityRoleDto>>> ListRolesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ApiResponse<IReadOnlyList<SecurityRoleDto>>.Success(Array.Empty<SecurityRoleDto>()));
        public Task<ApiResponse<SecurityAdministrationResultDto>> MutateAsync(
            SecurityAdministrationMutationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(ApiResponse<SecurityAdministrationResultDto>.Success(
                new SecurityAdministrationResultDto(request.ExpectedRevision ?? 0, true)));
        public Task<ApiResponse<PermissionAssignmentImportPreviewDto>> PreviewPermissionImportAsync(
            PermissionImportTargetTypeDto targetType, int targetId, string json, CancellationToken cancellationToken)
        {
            PreviewJson = json;
            return Task.FromResult(ApiResponse<PermissionAssignmentImportPreviewDto>.Success(Preview!));
        }
        public Task<ApiResponse<PermissionAssignmentImportResultDto>> ApplyPermissionImportAsync(
            PermissionImportTargetTypeDto targetType, int targetId, PermissionAssignmentImportRequest request,
            CancellationToken cancellationToken)
        {
            ApplyRequest = request;
            ApplyTargetType = targetType;
            ApplyTargetId = targetId;
            return Task.FromResult(ApiResponse<PermissionAssignmentImportResultDto>.Success(
                new PermissionAssignmentImportResultDto(request.ExpectedTargetRevision + 1, 1, true)));
        }
    }

    private sealed class FakeCatalogClient : IPermissionCatalogApiClient
    {
        public Task<ApiResponse<PermissionCatalogDto>> GetCatalogAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ApiResponse<PermissionCatalogDto>.Success(new PermissionCatalogDto(
                "1.0", "catalog-sha256",
                new[] { new PermissionCatalogItemDto("Inventory.Read", "Inventory", "Read inventory", "Active") })));
    }

    private sealed class TestSession : IDesktopSession
    {
        public event EventHandler? SessionChanged { add { } remove { } }
        public bool IsAuthenticated => true;
        public DesktopSessionState State => DesktopSessionState.Authenticated;
        public CurrentUserDto? CurrentUser { get; } = new(7, "admin", "Admin");
        public long Version => 1;
        public void SignOut() { }
    }
}
