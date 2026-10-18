using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

[Collection(ApplicationSqlCollectionFixture.CollectionName)]
public sealed class ApplicationDataFixtureReuseIntegrationTests
{
    private readonly ApplicationSqlCollectionFixture _fixture;
    private readonly ITestOutputHelper _output;
    public ApplicationDataFixtureReuseIntegrationTests(ApplicationSqlCollectionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Security_scenarios_reset_assignments_revisions_audit_and_binding()
    {
        foreach (var reverse in new[] { false, true })
        {
            var label = nameof(Security_scenarios_reset_assignments_revisions_audit_and_binding) + (reverse ? "/reverse" : "/forward");
            await ApplicationSqlCollectionFixture.AssertExpectedScenarioFailureAsync(() => _fixture.RunAsync(label + "/failed-sentinel", _output, async scope =>
                {
                    await ApplicationSqlCollectionFixture.SeedContaminationAsync(scope);
                    Assert.Fail(ApplicationSqlCollectionFixture.ExpectedScenarioFailure);
                }));
            Assert.False(_fixture.Fixture.IsPoisoned);
            Func<Task>[] scenarios =
            {
                new IdentityStoreIntegrationTests(_fixture, _output).User_and_permission_stores_read_current_sql_state_and_replace_hash_atomically,
                new IdentityStoreIntegrationTests(_fixture, _output).Identity_unique_relationship_constraints_reject_duplicates,
                new IdentityStoreIntegrationTests(_fixture, _output).Login_with_an_outdated_bcrypt_hash_rehashes_through_the_sql_store,
                new SecurityAdministrationSqlTests(_fixture, _output).Permission_import_previews_without_writes_then_adds_user_and_role_decisions_atomically,
                new SecurityAdministrationSqlTests(_fixture, _output).Permission_import_rechecks_actor_permission_after_preview,
                new SecurityAdministrationSqlTests(_fixture, _output).Permission_import_rejects_inactive_targets_and_stale_preview_revisions,
                new SecurityAdministrationSqlTests(_fixture, _output).Administration_is_revisioned_audited_and_preserves_direct_decision_precedence,
                new SecurityAdministrationSqlTests(_fixture, _output).Missing_targets_and_bound_superuser_lifecycle_changes_are_rejected,
            };
            if (reverse) { Array.Reverse(scenarios); }
            foreach (var scenario in scenarios)
            {
                var before = _fixture.Fixture.Observations.Count(x => x.Operation == "reset");
                await scenario();
                Assert.Equal(before + 1, _fixture.Fixture.Observations.Count(x => x.Operation == "reset"));
            }
            await _fixture.RunAsync(label + "/next-baseline", _output,
                scope => ApplicationSqlCollectionFixture.AssertEmptyAsync(scope, default));
        }
    }
}
