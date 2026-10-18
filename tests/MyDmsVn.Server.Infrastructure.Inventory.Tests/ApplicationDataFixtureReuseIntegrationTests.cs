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

    [SqlServerFact]
    public async Task Inventory_scenarios_reset_receipts_ledger_balances_and_references()
    {
        foreach (var reverse in new[] { false, true })
        {
            var label = nameof(Inventory_scenarios_reset_receipts_ledger_balances_and_references) + (reverse ? "/reverse" : "/forward");
            await ApplicationSqlCollectionFixture.AssertExpectedScenarioFailureAsync(() => _fixture.RunAsync(label + "/failed-sentinel", _output, async scope =>
                {
                    await ApplicationSqlCollectionFixture.SeedContaminationAsync(scope);
                    Assert.Fail(ApplicationSqlCollectionFixture.ExpectedScenarioFailure);
                }));
            Assert.False(_fixture.Fixture.IsPoisoned);
            Func<Task>[] scenarios =
            {
                new GoodsReceiptMappingIntegrationTests(_fixture, _output).Goods_receipt_entities_round_trip_through_registered_RepoDb_mappings,
                new GoodsReceiptQueryIntegrationTests(_fixture, _output).Get_returns_complete_aggregate_version_and_inactive_historical_labels,
                new GoodsReceiptQueryIntegrationTests(_fixture, _output).List_is_deterministic_set_based_and_applies_each_filter,
                new InventoryQueryIntegrationTests(_fixture, _output).Stock_card_query_preserves_datetime2_boundaries,
                new InventoryQueryIntegrationTests(_fixture, _output).Balance_and_stock_card_queries_filter_page_sort_and_keep_inactive_history,
                new InventoryStorageIntegrationTests(_fixture, _output).Reconciliation_flags_missing_keys_on_either_side_and_quantity_mismatch,
                new InventoryStorageIntegrationTests(_fixture, _output).Reconciliation_executes_in_a_serializable_transaction,
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
