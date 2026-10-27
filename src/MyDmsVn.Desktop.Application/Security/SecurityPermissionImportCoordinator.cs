using System;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;

namespace MyDmsVn.Desktop.Application.Security;

/// <summary>Retains one contextual import preview until its owner or inputs change.</summary>
public sealed class SecurityPermissionImportCoordinator
{
    private readonly ISecurityAdministrationApiClient _administration;
    private readonly Func<bool> _isCurrentOwner;
    private PreviewContext? _preview;
    private int _generation;

    public SecurityPermissionImportCoordinator(ISecurityAdministrationApiClient administration, Func<bool> isCurrentOwner)
    {
        _administration = administration ?? throw new ArgumentNullException(nameof(administration));
        _isCurrentOwner = isCurrentOwner ?? throw new ArgumentNullException(nameof(isCurrentOwner));
    }

    public void InvalidatePreview()
    {
        _generation++;
        _preview = null;
    }

    public async Task<ApiResponse<PermissionAssignmentImportPreviewDto>> PreviewAsync(
        PermissionImportTargetTypeDto targetType, int targetId, string json,
        long localEditRevision, string catalogDigest, CancellationToken token)
    {
        InvalidatePreview();
        var generation = _generation;
        if (!_isCurrentOwner() || token.IsCancellationRequested)
            return SessionChanged<PermissionAssignmentImportPreviewDto>();

        var response = await _administration.PreviewPermissionImportAsync(targetType, targetId, json, token)
            .ConfigureAwait(false);
        if (generation != _generation || !_isCurrentOwner() || token.IsCancellationRequested)
            return SessionChanged<PermissionAssignmentImportPreviewDto>();

        var data = response.Data;
        if (response.IsSuccess && data != null && data.Import.TargetType == targetType &&
            data.Import.TargetId == targetId && string.Equals(data.CatalogDigest, catalogDigest, StringComparison.Ordinal))
        {
            _preview = new PreviewContext(targetType, targetId, json, localEditRevision,
                catalogDigest, data.TargetRevision);
        }

        return response;
    }

    /// <summary>Returns null without dispatch when confirmation or the reviewed context is absent.</summary>
    public async Task<ApiResponse<PermissionAssignmentImportResultDto>?> ApplyPreviewAsync(
        PermissionImportTargetTypeDto targetType, int targetId, string json,
        long localEditRevision, string catalogDigest, bool confirmed, CancellationToken token)
    {
        if (!confirmed || token.IsCancellationRequested || !_isCurrentOwner() ||
            _preview == null || !_preview.Matches(targetType, targetId, json, localEditRevision, catalogDigest))
            return null;

        var preview = _preview;
        InvalidatePreview(); // A lost response must never replay this preview automatically.
        var response = await _administration.ApplyPermissionImportAsync(targetType, targetId,
            new PermissionAssignmentImportRequest(preview.Json, preview.TargetRevision, preview.CatalogDigest), token)
            .ConfigureAwait(false);
        // A cancellation after dispatch cannot erase a received authoritative response.
        return _isCurrentOwner() ? response : null;
    }

    private static ApiResponse<T> SessionChanged<T>() => ApiResponse<T>.Failure(
        new ApiError(ApiStatusCode.Unauthorized, "Auth.SessionChanged", "The workspace session changed."));

    private sealed class PreviewContext
    {
        public PreviewContext(PermissionImportTargetTypeDto targetType, int targetId, string json,
            long localEditRevision, string catalogDigest, long targetRevision)
        {
            TargetType = targetType;
            TargetId = targetId;
            Json = json;
            LocalEditRevision = localEditRevision;
            CatalogDigest = catalogDigest;
            TargetRevision = targetRevision;
        }

        public PermissionImportTargetTypeDto TargetType { get; }
        public int TargetId { get; }
        public string Json { get; }
        public long LocalEditRevision { get; }
        public string CatalogDigest { get; }
        public long TargetRevision { get; }

        public bool Matches(PermissionImportTargetTypeDto targetType, int targetId, string json,
            long localEditRevision, string catalogDigest) =>
            TargetType == targetType && TargetId == targetId &&
            LocalEditRevision == localEditRevision &&
            string.Equals(Json, json, StringComparison.Ordinal) &&
            string.Equals(CatalogDigest, catalogDigest, StringComparison.Ordinal);
    }
}
