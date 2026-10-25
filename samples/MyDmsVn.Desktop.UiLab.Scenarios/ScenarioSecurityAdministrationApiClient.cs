using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Desktop.Application;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace MyDmsVn.Desktop.UiLab.Scenarios;
public sealed class SecurityMutationJournalEntry
{
    public SecurityMutationJournalEntry(SecurityAdministrationMutationRequest r)
    {
        Target = r.Target;
        Operation = r.Operation;
        TargetId = r.TargetId;
        RelatedId = r.RelatedId;
        PermissionKey = r.PermissionKey;
        Decision = r.Decision;
        ExpectedRevision = r.ExpectedRevision;
        ExpectedRelatedRevision = r.ExpectedRelatedRevision;
    }

    public SecurityAdministrationTargetDto Target { get; }
    public SecurityAdministrationOperationDto Operation { get; }
    public int TargetId { get; }
    public int RelatedId { get; }
    public string? PermissionKey { get; }
    public bool? Decision { get; }
    public long? ExpectedRevision { get; }
    public long? ExpectedRelatedRevision { get; }

    public override string ToString() => $"{Target}/{Operation} target={TargetId} related={RelatedId} key={PermissionKey ?? "—"} decision={(Operation == SecurityAdministrationOperationDto.SetPermission ? Decision?.ToString().ToLowerInvariant() ?? "null" : "—")} revision={ExpectedRevision?.ToString() ?? "—"} relatedRevision={ExpectedRelatedRevision?.ToString() ?? "—"}";
}

public sealed class SecurityResponseJournalEntry
{
    public SecurityResponseJournalEntry(string outcome, long? assignmentRevision = null, long? relatedRevision = null, int? count = null)
    {
        Outcome = outcome;
        AssignmentRevision = assignmentRevision;
        RelatedRevision = relatedRevision;
        Count = count;
    }

    public string Outcome { get; }
    public long? AssignmentRevision { get; }
    public long? RelatedRevision { get; }
    public int? Count { get; }
    public override string ToString() => $"outcome={Outcome} count={Count?.ToString() ?? "—"} revision={AssignmentRevision?.ToString() ?? "—"} relatedRevision={RelatedRevision?.ToString() ?? "—"}";
}

public sealed class ScenarioSecurityAdministrationApiClient : ISecurityAdministrationApiClient
{
    readonly SecurityScenarioData data;
    readonly ScenarioDesktopSession session;
    readonly long owner;
    readonly ScenarioJournal journal;
    readonly ScenarioTaskTracker tasks = new ScenarioTaskTracker();
    readonly List<ScenarioGate<ApiResponse<SecurityAdministrationResultDto>>> gates = new List<ScenarioGate<ApiResponse<SecurityAdministrationResultDto>>>();
    public ScenarioSecurityAdministrationApiClient(SecurityScenarioData data, ScenarioDesktopSession session, long owner, ScenarioJournal journal)
    {
        this.data = data;
        this.session = session;
        this.owner = owner;
        this.journal = journal;
    }

    bool Admitted => session.Version == owner && session.IsAuthenticated;
    public ApiError? UsersError { get; set; }
    public ApiError? RolesError { get; set; }

    readonly List<ScenarioGate<ApiResponse<IReadOnlyList<SecurityUserDto>>>> userGates = new List<ScenarioGate<ApiResponse<IReadOnlyList<SecurityUserDto>>>>();
    ScenarioGate<ApiResponse<IReadOnlyList<SecurityUserDto>>>? usersGate;
    public ScenarioGate<ApiResponse<IReadOnlyList<SecurityUserDto>>>? UsersGate
    {
        get => usersGate;
        set
        {
            usersGate = value;
            if (value != null)
                userGates.Add(value);
        }
    }

    readonly List<ScenarioGate<ApiResponse<IReadOnlyList<SecurityRoleDto>>>> roleGates = new List<ScenarioGate<ApiResponse<IReadOnlyList<SecurityRoleDto>>>>();
    ScenarioGate<ApiResponse<IReadOnlyList<SecurityRoleDto>>>? rolesGate;
    public ScenarioGate<ApiResponse<IReadOnlyList<SecurityRoleDto>>>? RolesGate
    {
        get => rolesGate;
        set
        {
            rolesGate = value;
            if (value != null)
                roleGates.Add(value);
        }
    }

    public Queue<ApiError?> MutationErrors { get; } = new Queue<ApiError?>();
    public IReadOnlyList<Task> OutstandingOperations => tasks.Outstanding;

    ScenarioGate<ApiResponse<SecurityAdministrationResultDto>>? nextMutation;
    public ScenarioGate<ApiResponse<SecurityAdministrationResultDto>> HoldNextMutation(bool ignoreCancellation = false)
    {
        var g = new ScenarioGate<ApiResponse<SecurityAdministrationResultDto>>
        {
            IgnoreCancellation = ignoreCancellation
        };
        nextMutation = g;
        gates.Add(g);
        return g;
    }

    public void CancelGates()
    {
        foreach (var g in userGates)
            g.Cancel();
        foreach (var g in roleGates)
            g.Cancel();
        foreach (var g in gates)
            g.Cancel();
    }

    public Task<ApiResponse<IReadOnlyList<SecurityUserDto>>> ListUsersAsync(CancellationToken token) => tasks.Track(ReadUsers(token));
    async Task<ApiResponse<IReadOnlyList<SecurityUserDto>>> ReadUsers(CancellationToken token)
    {
        if (!Admitted)
            return ScenarioErrors.SessionChanged<IReadOnlyList<SecurityUserDto>>();
        token.ThrowIfCancellationRequested();
        journal.Record("Security.Users");
        var gate = usersGate;
        usersGate = null;
        try
        {
            var r = gate != null ? await gate.WaitAsync(token) : UsersError != null ? ApiResponse<IReadOnlyList<SecurityUserDto>>.Failure(UsersError) : ApiResponse<IReadOnlyList<SecurityUserDto>>.Success(data.Users.ToArray());
            r = Admitted ? r : ScenarioErrors.SessionChanged<IReadOnlyList<SecurityUserDto>>();
            journal.Record("Security.Users.Result", new SecurityResponseJournalEntry(r.IsSuccess ? "Success" : r.Error?.Status.ToString() ?? "Failure", count: r.Data?.Count));
            return r;
        }
        catch (OperationCanceledException)
        {
            journal.Record("Security.Users.Result", new SecurityResponseJournalEntry("Canceled"));
            throw;
        }
    }

    public Task<ApiResponse<IReadOnlyList<SecurityRoleDto>>> ListRolesAsync(CancellationToken token) => tasks.Track(ReadRoles(token));
    async Task<ApiResponse<IReadOnlyList<SecurityRoleDto>>> ReadRoles(CancellationToken token)
    {
        if (!Admitted)
            return ScenarioErrors.SessionChanged<IReadOnlyList<SecurityRoleDto>>();
        token.ThrowIfCancellationRequested();
        journal.Record("Security.Roles");
        var gate = rolesGate;
        rolesGate = null;
        try
        {
            var r = gate != null ? await gate.WaitAsync(token) : RolesError != null ? ApiResponse<IReadOnlyList<SecurityRoleDto>>.Failure(RolesError) : ApiResponse<IReadOnlyList<SecurityRoleDto>>.Success(data.Roles.ToArray());
            r = Admitted ? r : ScenarioErrors.SessionChanged<IReadOnlyList<SecurityRoleDto>>();
            journal.Record("Security.Roles.Result", new SecurityResponseJournalEntry(r.IsSuccess ? "Success" : r.Error?.Status.ToString() ?? "Failure", count: r.Data?.Count));
            return r;
        }
        catch (OperationCanceledException)
        {
            journal.Record("Security.Roles.Result", new SecurityResponseJournalEntry("Canceled"));
            throw;
        }
    }

    public Task<ApiResponse<SecurityAdministrationResultDto>> MutateAsync(SecurityAdministrationMutationRequest request, CancellationToken token)
    {
        if (!Admitted)
            return Task.FromResult(ScenarioErrors.SessionChanged<SecurityAdministrationResultDto>());
        token.ThrowIfCancellationRequested();
        var safe = new SecurityMutationJournalEntry(request);
        journal.Record("Security.Mutate", safe);
        var gate = nextMutation;
        nextMutation = null;
        if (gate != null)
            return tasks.Track(CompleteHeld(gate, new SecurityAdministrationMutationRequest(request.Target, request.Operation, request.TargetId, request.RelatedId, request.Name, request.DisplayName, request.Email, request.IsActive, !string.IsNullOrEmpty(request.Password) && Encoding.UTF8.GetByteCount(request.Password!) <= 72 ? "redacted-valid-password" : null, request.PermissionKey, request.Decision, request.ExpectedRevision, request.ExpectedRelatedRevision, request.CorrelationId), token));
        var result = Mutate(request);
        RecordMutationResult(result);
        return Task.FromResult(result);
    }

    async Task<ApiResponse<SecurityAdministrationResultDto>> CompleteHeld(ScenarioGate<ApiResponse<SecurityAdministrationResultDto>> gate, SecurityAdministrationMutationRequest sanitizedRequest, CancellationToken token)
    {
        try
        {
            var r = await gate.WaitAsync(token);
            if (r.IsSuccess)
                r = Mutate(sanitizedRequest);
            r = Admitted ? r : ScenarioErrors.SessionChanged<SecurityAdministrationResultDto>();
            RecordMutationResult(r);
            return r;
        }
        catch (OperationCanceledException)
        {
            journal.Record("Security.Mutate.Result", new SecurityResponseJournalEntry("OutcomeUnknown"));
            throw;
        }
    }

    void RecordMutationResult(ApiResponse<SecurityAdministrationResultDto> result) =>
        journal.Record("Security.Mutate.Result", new SecurityResponseJournalEntry(
            result.IsSuccess ? "Success" : result.Error?.Status.ToString() ?? "Failure",
            result.Data?.AssignmentRevision, result.Data?.RelatedAssignmentRevision));

    static ApiResponse<SecurityAdministrationResultDto> Fail(ApiStatusCode status, string code) => ApiResponse<SecurityAdministrationResultDto>.Failure(new ApiError(status, code, "UI Lab controlled response.", status == ApiStatusCode.BadRequest ? new[] { new ApiErrorDetail("Security.InvalidInput", "The command is invalid.", "request") } : null));
    ApiResponse<SecurityAdministrationResultDto> Mutate(SecurityAdministrationMutationRequest r)
    {
        if (!Enum.IsDefined(typeof(SecurityAdministrationTargetDto), r.Target) || !Enum.IsDefined(typeof(SecurityAdministrationOperationDto), r.Operation) || (r.Target == SecurityAdministrationTargetDto.Role && (r.Operation == SecurityAdministrationOperationDto.SetPassword || r.Operation == SecurityAdministrationOperationDto.SetMembership)) || ((r.Operation == SecurityAdministrationOperationDto.Create || r.Operation == SecurityAdministrationOperationDto.Update) && (string.IsNullOrWhiteSpace(r.Name) || r.Name!.Length > 100 || (r.Target == SecurityAdministrationTargetDto.User && (string.IsNullOrWhiteSpace(r.DisplayName) || r.DisplayName!.Length > 100)))) || (r.Target == SecurityAdministrationTargetDto.User && (r.Operation == SecurityAdministrationOperationDto.Create || r.Operation == SecurityAdministrationOperationDto.SetPassword) && (string.IsNullOrEmpty(r.Password) || Encoding.UTF8.GetByteCount(r.Password!) > 72)) || ((r.Operation == SecurityAdministrationOperationDto.SetActive || r.Operation == SecurityAdministrationOperationDto.SetMembership) && !r.IsActive.HasValue) || (r.Target == SecurityAdministrationTargetDto.Role && r.Operation == SecurityAdministrationOperationDto.SetPermission && !r.Decision.HasValue))
            return Fail(ApiStatusCode.BadRequest, "ValidationError");
        if (MutationErrors.Count > 0)
        {
            var error = MutationErrors.Dequeue();
            if (error != null)
                return ApiResponse<SecurityAdministrationResultDto>.Failure(error);
        }

        if (r.Operation == SecurityAdministrationOperationDto.Create)
        {
            if (string.IsNullOrWhiteSpace(r.Name))
                return Fail(ApiStatusCode.BadRequest, "ValidationError");
            if (r.Target == SecurityAdministrationTargetDto.User)
                data.Users.Add(new SecurityUserDto(data.Users.Max(x => x.UserId) + 1, r.Name!, r.DisplayName ?? r.Name!, r.Email, true, 1, new int[0], new SecurityDecisionDto[0]));
            else
                data.Roles.Add(new SecurityRoleDto(data.Roles.Max(x => x.RoleId) + 1, r.Name!, true, 1, new int[0], new string[0]));
            return ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(1, true));
        }

        var u = data.Users.FirstOrDefault(x => x.UserId == r.TargetId);
        var role = data.Roles.FirstOrDefault(x => x.RoleId == r.TargetId);
        bool user = r.Target == SecurityAdministrationTargetDto.User;
        if (user ? u == null : role == null)
            return Fail(ApiStatusCode.NotFound, "Security.TargetNotFound");
        if (user && u!.IsProtectedAccount)
            return Fail(ApiStatusCode.Forbidden, "Security.ProtectedAccount");
        long revision = user ? u!.AssignmentRevision : role!.AssignmentRevision;
        if (r.ExpectedRevision != revision)
            return Fail(ApiStatusCode.Conflict, "Security.RevisionConflict");
        bool changed = true;
        long? related = null;
        var roles = user ? u!.RoleIds.ToList() : new List<int>();
        var decisions = user ? u!.DirectDecisions.ToList() : new List<SecurityDecisionDto>();
        var keys = !user ? role!.PermissionKeys.ToList() : new List<string>();
        if (r.Operation == SecurityAdministrationOperationDto.SetMembership)
        {
            var targetRole = data.Roles.FirstOrDefault(x => x.RoleId == r.RelatedId);
            if (!user || targetRole == null)
                return Fail(ApiStatusCode.NotFound, "Security.TargetNotFound");
            if (r.ExpectedRelatedRevision != targetRole.AssignmentRevision)
                return Fail(ApiStatusCode.Conflict, "Security.RevisionConflict");
            changed = roles.Contains(r.RelatedId) != (r.IsActive == true);
            if (changed)
            {
                if (r.IsActive == true)
                    roles.Add(r.RelatedId);
                else
                    roles.Remove(r.RelatedId);
                var members = targetRole.MemberUserIds.ToList();
                if (r.IsActive == true)
                    members.Add(r.TargetId);
                else
                    members.Remove(r.TargetId);
                data.Roles[data.Roles.IndexOf(targetRole)] = new SecurityRoleDto(targetRole.RoleId, targetRole.Name, targetRole.IsActive, targetRole.AssignmentRevision + 1, members, targetRole.PermissionKeys);
            }

            related = targetRole.AssignmentRevision + (changed ? 1 : 0);
        }

        if (r.Operation == SecurityAdministrationOperationDto.SetPermission)
        {
            if (!data.Catalog.Items.Any(x => x.Key == r.PermissionKey && x.Status == "Active"))
                return Fail(ApiStatusCode.BadRequest, "ValidationError");
            if (user)
            {
                var old = decisions.FirstOrDefault(x => x.PermissionKey == r.PermissionKey);
                changed = old?.Decision != r.Decision;
                decisions.RemoveAll(x => x.PermissionKey == r.PermissionKey);
                if (r.Decision != null)
                    decisions.Add(new SecurityDecisionDto(r.PermissionKey!, r.Decision));
            }
            else
            {
                changed = keys.Contains(r.PermissionKey!) != (r.Decision == true);
                if (r.Decision == true && !keys.Contains(r.PermissionKey!))
                    keys.Add(r.PermissionKey!);
                else if (r.Decision != true)
                    keys.Remove(r.PermissionKey!);
            }
        }

        if (r.Operation == SecurityAdministrationOperationDto.SetActive)
            changed = (user ? u!.IsActive : role!.IsActive) != r.IsActive;
        if (user)
            data.Users[data.Users.IndexOf(u!)] = new SecurityUserDto(u!.UserId, r.Name ?? u.Username, r.DisplayName ?? u.DisplayName, r.Email ?? u.Email, r.IsActive ?? u.IsActive, revision + (changed ? 1 : 0), roles, decisions, u.IsProtectedAccount);
        else
            data.Roles[data.Roles.IndexOf(role!)] = new SecurityRoleDto(role!.RoleId, r.Name ?? role.Name, r.IsActive ?? role.IsActive, revision + (changed ? 1 : 0), role.MemberUserIds, keys);
        return ApiResponse<SecurityAdministrationResultDto>.Success(new SecurityAdministrationResultDto(revision + (changed ? 1 : 0), changed, related));
    }

    public Task<ApiResponse<PermissionAssignmentImportPreviewDto>> PreviewPermissionImportAsync(PermissionImportTargetTypeDto type, int id, string json, CancellationToken token)
    {
        if (!Admitted)
            return Task.FromResult(ScenarioErrors.SessionChanged<PermissionAssignmentImportPreviewDto>());
        token.ThrowIfCancellationRequested();
        journal.Record("Security.ImportPreview", new { Target = type, TargetId = id });
        return Task.FromResult(Preview(type, id, json));
    }

    // Keep the deliberately narrow schema-v1 admission aligned with the application parser.
    // This fixture owns no Server.Application reference or SQL composition.
    ParsedPermissionAssignmentImportDto? ParseImport(string json)
    {
        try
        {
            if (json == null || new UTF8Encoding(false, true).GetByteCount(json) > PermissionAssignmentImportLimits.MaximumUtf8Bytes)
                return null;
            using var text = new StringReader(json);
            using var reader = new JsonTextReader(text)
            {
                DateParseHandling = DateParseHandling.None,
                MaxDepth = 16,
                SupportMultipleContent = false
            };
            var token = JToken.ReadFrom(reader, new JsonLoadSettings { CommentHandling = CommentHandling.Load, DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read() || token.Type != JTokenType.Object || ((JContainer)token).Descendants().Any(x => x.Type == JTokenType.Comment))
                return null;
            var root = (JObject)token;
            if (!HasExactProperties(root, "schemaVersion", "targetType", "targetId", "entries") || root["schemaVersion"]?.Type != JTokenType.Integer || (long)root["schemaVersion"]! != 1 || root["targetType"]?.Type != JTokenType.String)
                return null;
            var targetText = (string)root["targetType"]!;
            if (targetText != "User" && targetText != "Role")
                return null;
            var type = targetText == "User" ? PermissionImportTargetTypeDto.User : PermissionImportTargetTypeDto.Role;
            if (root["targetId"]?.Type != JTokenType.Integer || !int.TryParse(root["targetId"]!.ToString(Formatting.None), out var id) || id <= 0 || root["entries"]?.Type != JTokenType.Array)
                return null;
            var array = (JArray)root["entries"]!;
            if (array.Count > PermissionAssignmentImportLimits.MaximumEntries)
                return null;
            var parsed = new List<PermissionAssignmentImportEntryDto>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in array)
            {
                if (entry.Type != JTokenType.Object || !HasExactProperties((JObject)entry, "permissionKey", "decision") || entry["permissionKey"]?.Type != JTokenType.String || entry["decision"]?.Type != JTokenType.String)
                    return null;
                var key = (string)entry["permissionKey"]!;
                var decision = (string)entry["decision"]!;
                if (string.IsNullOrWhiteSpace(key) || key != key.Trim() || !data.Catalog.Items.Any(x => x.Key == key && x.Status == "Active") || !seen.Add(key) || (decision != "Allow" && !(decision == "Deny" && type == PermissionImportTargetTypeDto.User)))
                    return null;
                parsed.Add(new PermissionAssignmentImportEntryDto(key, decision == "Allow"));
            }

            return new ParsedPermissionAssignmentImportDto(1, type, id, parsed.ToArray());
        }
        catch (Exception error)when (error is JsonException || error is FormatException || error is OverflowException || error is ArgumentException)
        {
            return null;
        }
    }

    static bool HasExactProperties(JObject value, params string[] names)
    {
        var actual = value.Properties().Select(x => x.Name).ToArray();
        return actual.Length == names.Length && names.All(name => actual.Count(candidate => string.Equals(candidate, name, StringComparison.Ordinal)) == 1);
    }

    ApiResponse<PermissionAssignmentImportPreviewDto> Preview(PermissionImportTargetTypeDto type, int id, string json)
    {
        ApiResponse<PermissionAssignmentImportPreviewDto> Error(ApiStatusCode status, string code) => ApiResponse<PermissionAssignmentImportPreviewDto>.Failure(new ApiError(status, status == ApiStatusCode.BadRequest ? "ValidationError" : code, "UI Lab import rejected.", status == ApiStatusCode.BadRequest ? new[] { new ApiErrorDetail(code, "Check the import input.", "json") } : null));
        var parsed = ParseImport(json);
        if (parsed == null)
            return Error(ApiStatusCode.BadRequest, "Security.ImportInvalid");
        if (parsed.TargetType != type || parsed.TargetId != id)
            return Error(ApiStatusCode.BadRequest, "Security.ImportTargetMismatch");
        var u = data.Users.FirstOrDefault(x => x.UserId == id);
        var role = data.Roles.FirstOrDefault(x => x.RoleId == id);
        bool user = type == PermissionImportTargetTypeDto.User;
        if (user ? u == null : role == null)
            return Error(ApiStatusCode.NotFound, "Security.TargetNotFound");
        if (user ? !u!.IsActive || u.IsProtectedAccount : !role!.IsActive)
            return Error(ApiStatusCode.Conflict, "Security.ImportTargetUnavailable");
        if (user && parsed.Entries.Any(x => u!.DirectDecisions.Any(d => d.PermissionKey == x.PermissionKey && d.Decision != x.Granted)))
            return Error(ApiStatusCode.Conflict, "Security.ImportDecisionConflict");
        var additions = parsed.Entries.Where(x => user ? !u!.DirectDecisions.Any(d => d.PermissionKey == x.PermissionKey && d.Decision == x.Granted) : !role!.PermissionKeys.Contains(x.PermissionKey)).ToArray();
        return ApiResponse<PermissionAssignmentImportPreviewDto>.Success(new PermissionAssignmentImportPreviewDto(parsed, user ? u!.DisplayName : role!.Name, user ? u!.AssignmentRevision : role!.AssignmentRevision, data.Catalog.CatalogDigest, additions));
    }

    public Task<ApiResponse<PermissionAssignmentImportResultDto>> ApplyPermissionImportAsync(PermissionImportTargetTypeDto type, int id, PermissionAssignmentImportRequest request, CancellationToken token)
    {
        if (!Admitted)
            return Task.FromResult(ScenarioErrors.SessionChanged<PermissionAssignmentImportResultDto>());
        token.ThrowIfCancellationRequested();
        journal.Record("Security.ImportApply", new { Target = type, TargetId = id, request.ExpectedTargetRevision, request.ExpectedCatalogDigest });
        // Application admission precedes reading the current target state.
        var parsed = ParseImport(request.Json);
        ApiResponse<PermissionAssignmentImportResultDto> Validation(string code) => ApiResponse<PermissionAssignmentImportResultDto>.Failure(new ApiError(ApiStatusCode.BadRequest, "ValidationError", "UI Lab import rejected.", new[] { new ApiErrorDetail(code, "Check the import input.", "json") }));
        if (parsed == null)
            return Task.FromResult(Validation("Security.ImportInvalid"));
        if (parsed.TargetType != type || parsed.TargetId != id)
            return Task.FromResult(Validation("Security.ImportTargetMismatch"));
        if (!string.Equals(request.ExpectedCatalogDigest, data.Catalog.CatalogDigest, StringComparison.Ordinal))
            return Task.FromResult(ApiResponse<PermissionAssignmentImportResultDto>.Failure(new ApiError(ApiStatusCode.Conflict, "Security.ImportCatalogChanged", "The permission catalog changed after preview.")));
        if (request.ExpectedTargetRevision < 0)
            return Task.FromResult(Validation("Security.ImportRevisionInvalid"));
        var preview = Preview(type, id, request.Json);
        if (!preview.IsSuccess)
            return Task.FromResult(ApiResponse<PermissionAssignmentImportResultDto>.Failure(preview.Error!));
        var p = preview.Data!;
        if (p.TargetRevision != request.ExpectedTargetRevision)
            return Task.FromResult(ApiResponse<PermissionAssignmentImportResultDto>.Failure(new ApiError(ApiStatusCode.Conflict, "Security.RevisionConflict", "The target revision changed after preview.")));
        bool changed = p.Additions.Count > 0;
        var revision = p.TargetRevision + (changed ? 1 : 0);
        if (changed && type == PermissionImportTargetTypeDto.User)
        {
            var u = data.Users.Single(x => x.UserId == id);
            data.Users[data.Users.IndexOf(u)] = new SecurityUserDto(u.UserId, u.Username, u.DisplayName, u.Email, u.IsActive, revision, u.RoleIds, u.DirectDecisions.Concat(p.Additions.Select(x => new SecurityDecisionDto(x.PermissionKey, x.Granted))).ToArray(), u.IsProtectedAccount);
        }
        else if (changed)
        {
            var role = data.Roles.Single(x => x.RoleId == id);
            data.Roles[data.Roles.IndexOf(role)] = new SecurityRoleDto(role.RoleId, role.Name, role.IsActive, revision, role.MemberUserIds, role.PermissionKeys.Concat(p.Additions.Select(x => x.PermissionKey)).ToArray());
        }

        return Task.FromResult(ApiResponse<PermissionAssignmentImportResultDto>.Success(new PermissionAssignmentImportResultDto(revision, p.Additions.Count, changed)));
    }
}
