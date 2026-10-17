using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts.Security;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Application.Identity;
using Xunit;

namespace MyDmsVn.Server.Application.Tests;

public sealed class PermissionImportValidationTests
{
    private readonly PermissionAssignmentImportParser _parser =
        new(PermissionRegistryComposition.CreateDefault());

    [Fact]
    public void Rejects_unsupported_or_malformed_import_documents()
    {
        var documents = new[]
        {
            "{}",
            "{\"schemaVersion\":2,\"targetType\":\"Role\",\"targetId\":7,\"entries\":[]}",
            "{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":7,\"permissions\":[]}",
            "{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":7,\"actorUserId\":1,\"entries\":[]}",
            "{\"schemaVersion\":1,\"targetType\":\"Other\",\"targetId\":7,\"entries\":[]}",
            "{\"schemaVersion\":1,\"targetType\":\"Role\",\"targetId\":0,\"entries\":[]}",
        };
        foreach (var document in documents)
        {
            var result = _parser.Parse(document);
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }
    }

    [Fact]
    public void Rejects_unknown_deprecated_noncanonical_or_whitespace_keys()
    {
        var keys = new[]
        {
            "Unknown.Permission",
            "security.users.manage",
            " Security.Users.Manage",
            "Security.Users.Manage ",
            "Security.Superuser.Maintain",
        };
        foreach (var key in keys)
        {
            var result = _parser.Parse(Document("User", 7, $"{{\"permissionKey\":\"{key}\",\"decision\":\"Allow\"}}"));
            Assert.False(result.IsValid);
        }
    }

    [Fact]
    public void Accepts_canonical_user_allow_and_deny_and_role_allow()
    {
        var user = _parser.Parse(Document("User", 7,
            "{\"permissionKey\":\"Security.Users.Manage\",\"decision\":\"Deny\"}"));
        var role = _parser.Parse(Document("Role", 8,
            "{\"permissionKey\":\"Security.Users.Manage\",\"decision\":\"Allow\"}"));

        Assert.True(user.IsValid);
        Assert.Equal(7, user.Value!.TargetId);
        Assert.False(user.Value.Entries.Single().Granted);
        Assert.True(role.IsValid);
        Assert.True(role.Value!.Entries.Single().Granted);
    }

    [Fact]
    public void Accepts_the_ADR_0007_entries_and_permission_key_contract()
    {
        const string document = "{\"schemaVersion\":1,\"targetType\":\"User\",\"targetId\":7," +
                               "\"entries\":[{\"permissionKey\":\"Security.Users.Manage\",\"decision\":\"Allow\"}]}";

        var result = _parser.Parse(document);

        Assert.True(result.IsValid);
        Assert.Equal("Security.Users.Manage", result.Value!.Entries.Single().PermissionKey);
    }

    [Fact]
    public void Rejects_role_deny_duplicate_entries_and_conflicting_decisions()
    {
        var roleDeny = _parser.Parse(Document("Role", 8,
            "{\"permissionKey\":\"Security.Users.Manage\",\"decision\":\"Deny\"}"));
        var duplicate = _parser.Parse(Document("User", 7,
            "{\"permissionKey\":\"Security.Users.Manage\",\"decision\":\"Allow\"}," +
            "{\"permissionKey\":\"Security.Users.Manage\",\"decision\":\"Allow\"}"));
        var conflict = _parser.Parse(Document("User", 7,
            "{\"permissionKey\":\"Security.Users.Manage\",\"decision\":\"Allow\"}," +
            "{\"permissionKey\":\"Security.Users.Manage\",\"decision\":\"Deny\"}"));

        Assert.False(roleDeny.IsValid);
        Assert.False(duplicate.IsValid);
        Assert.False(conflict.IsValid);
    }

    [Fact]
    public void Enforces_utf8_size_and_entry_count_limits()
    {
        var tooLarge = _parser.Parse(new string(' ', PermissionAssignmentImportParser.MaximumUtf8Bytes + 1));
        var tooMany = _parser.Parse(Document("User", 7, string.Join(",", Enumerable.Repeat(
            "{\"permissionKey\":\"Security.Users.Manage\",\"decision\":\"Allow\"}",
            PermissionAssignmentImportParser.MaximumEntries + 1))));

        Assert.False(tooLarge.IsValid);
        Assert.False(tooMany.IsValid);
    }

    [Fact]
    public async Task Preview_and_apply_reauthorize_and_bind_actor_from_current_session()
    {
        var permissionStore = new MutablePermissionStore { Allowed = true };
        var importStore = new StubImportStore();
        var services = new ServiceCollection();
        services.AddServerApplication();
        services.AddSingleton<ICurrentUserAccessor>(new CurrentUserAccessor());
        services.AddSingleton<IPermissionStore>(permissionStore);
        services.AddSingleton<IPermissionAssignmentImportStore>(importStore);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        const string json = "{\"schemaVersion\":1,\"targetType\":\"User\",\"targetId\":17,\"entries\":[{\"permissionKey\":\"Catalog.Products.Read\",\"decision\":\"Allow\"}]}";

        var preview = await sender.Send(new PreviewPermissionAssignmentImportQuery(
            PermissionImportTargetTypeDto.User, 17, json));
        Assert.False(preview.IsError);
        Assert.Equal(42, importStore.LastReadActorId);

        permissionStore.Allowed = false;
        var apply = await sender.Send(new ApplyPermissionAssignmentImportCommand(
            PermissionImportTargetTypeDto.User, 17, json, preview.Value.TargetRevision,
            preview.Value.CatalogDigest));

        Assert.True(apply.IsError);
        Assert.Equal(ErrorType.Forbidden, apply.FirstError.Type);
        Assert.Equal(0, importStore.ApplyCount);
    }

    [Fact]
    public async Task Catalog_change_and_target_mismatch_never_reach_the_import_store()
    {
        var importStore = new StubImportStore();
        var services = new ServiceCollection();
        services.AddServerApplication();
        services.AddSingleton<ICurrentUserAccessor>(new CurrentUserAccessor());
        services.AddSingleton<IPermissionStore>(new MutablePermissionStore { Allowed = true });
        services.AddSingleton<IPermissionAssignmentImportStore>(importStore);
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        const string json = "{\"schemaVersion\":1,\"targetType\":\"User\",\"targetId\":17,\"entries\":[]}";

        var mismatch = await sender.Send(new PreviewPermissionAssignmentImportQuery(
            PermissionImportTargetTypeDto.User, 18, json));
        var changedCatalog = await sender.Send(new ApplyPermissionAssignmentImportCommand(
            PermissionImportTargetTypeDto.User, 17, json, 0, new string('0', 64)));

        Assert.True(mismatch.IsError);
        Assert.True(changedCatalog.IsError);
        Assert.Equal("Security.ImportTargetMismatch", mismatch.FirstError.Code);
        Assert.Equal("Security.ImportCatalogChanged", changedCatalog.FirstError.Code);
        Assert.Equal(0, importStore.ReadCount);
        Assert.Equal(0, importStore.ApplyCount);
    }

    private static string Document(string targetType, int targetId, string entries) =>
        "{\"schemaVersion\":1,\"targetType\":\"" + targetType + "\",\"targetId\":" +
        targetId + ",\"entries\":[" + entries + "]}";

    private sealed class CurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUser Current => CurrentUser.Authenticated(42, "operator", "Operator", true);
    }

    private sealed class MutablePermissionStore : IPermissionStore
    {
        public bool Allowed { get; set; }
        public Task<PermissionSnapshot> GetSnapshotAsync(int userId, string permissionKey, CancellationToken cancellationToken) =>
            Task.FromResult(new PermissionSnapshot(Allowed ? true : false, false));
    }

    private sealed class StubImportStore : IPermissionAssignmentImportStore
    {
        public int ReadCount { get; private set; }
        public int ApplyCount { get; private set; }
        public int LastReadActorId { get; private set; }

        public Task<ErrorOr<PermissionAssignmentImportTargetState>> ReadTargetAsync(
            PermissionImportTargetTypeDto targetType, int targetId, int actorUserId, CancellationToken cancellationToken)
        {
            ReadCount++;
            LastReadActorId = actorUserId;
            return Task.FromResult<ErrorOr<PermissionAssignmentImportTargetState>>(
                new PermissionAssignmentImportTargetState("operator", true, false, 9,
                    new Dictionary<string, bool>(StringComparer.Ordinal)));
        }

        public Task<ErrorOr<PermissionAssignmentImportResultDto>> ApplyAsync(
            ParsedPermissionAssignmentImportDto import, int actorUserId, long expectedTargetRevision,
            Guid correlationId, CancellationToken cancellationToken)
        {
            ApplyCount++;
            return Task.FromResult<ErrorOr<PermissionAssignmentImportResultDto>>(
                new PermissionAssignmentImportResultDto(expectedTargetRevision, 0, false));
        }
    }
}
