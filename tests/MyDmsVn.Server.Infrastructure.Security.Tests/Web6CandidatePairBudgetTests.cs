using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using MyDmsVn.Desktop.Tests;
using System.Collections.Generic;
using MyDmsVn.Web6.CandidateBootstrap;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CandidatePairBudgetTests
{
    [Fact]
    public void Cutover_phase_and_response_budgets_match_the_deadline_contract()
    {
        Assert.Equal(TimeSpan.FromSeconds(15), Web6CutoverBudgets.LifecyclePhase);
        Assert.Equal(TimeSpan.FromSeconds(45), Web6CutoverBudgets.SqlMutationOrOraclePhase);
        Assert.Equal(TimeSpan.FromSeconds(150), Web6CutoverBudgets.HostSwitchPhase);
        Assert.Equal(TimeSpan.FromSeconds(5), Web6CutoverBudgets.ResponseReserve);
    }

    [Fact]
    public void Cutover_scenarios_and_resets_fit_inside_the_browser_pack_and_controller()
    {
        Assert.Equal(TimeSpan.FromSeconds(420), Web6CutoverBudgets.FirstScenario);
        Assert.Equal(TimeSpan.FromSeconds(420), Web6CutoverBudgets.SecondScenario);
        Assert.Equal(TimeSpan.FromSeconds(690), Web6CutoverBudgets.ThirdScenario);
        Assert.Equal(TimeSpan.FromSeconds(180), Web6CutoverBudgets.FirstResetReserve);
        Assert.Equal(TimeSpan.FromSeconds(180), Web6CutoverBudgets.SecondResetReserve);
        Assert.Equal(TimeSpan.FromSeconds(60), Web6CutoverBudgets.ThirdResetReserve);
        Assert.Equal(TimeSpan.FromSeconds(1560), Web6CutoverBudgets.BrowserPack);
        Assert.Equal(TimeSpan.FromSeconds(1620), Web6CutoverBudgets.Controller);

        var scenarios = Web6CutoverBudgets.FirstScenario + Web6CutoverBudgets.SecondScenario + Web6CutoverBudgets.ThirdScenario;
        Assert.Equal(TimeSpan.FromSeconds(1530), scenarios);
        Assert.True(scenarios < Web6CutoverBudgets.BrowserPack);
        Assert.True(Web6CutoverBudgets.BrowserPack < Web6CutoverBudgets.Controller);
    }

    [Fact]
    public void Cutover_controllers_fit_inside_the_aggregate_selector_and_process_ceiling()
    {
        Assert.Equal(TimeSpan.FromSeconds(3300), Web6CutoverBudgets.AggregateParent);
        Assert.Equal(TimeSpan.FromSeconds(3600), Web6CutoverBudgets.SelectorGate);
        Assert.Equal(TimeSpan.FromSeconds(3660), Web6CutoverBudgets.SelectorProcessMaximum);

        var controllers = Web6CutoverBudgets.Controller + Web6CutoverBudgets.Controller;
        Assert.Equal(TimeSpan.FromSeconds(3240), controllers);
        Assert.True(controllers < Web6CutoverBudgets.AggregateParent);
        Assert.True(Web6CutoverBudgets.AggregateParent < Web6CutoverBudgets.SelectorGate);
        Assert.True(Web6CutoverBudgets.SelectorGate < Web6CutoverBudgets.SelectorProcessMaximum);
    }

    [Theory]
    [InlineData("oldClientNewHostOn", 180)]
    [InlineData("oldClientNewHostOff", 180)]
    [InlineData("newClientOldHostOn", 180)]
    [InlineData("newClientOldHostOff", 180)]
    [InlineData("restoreEpoch", 180)]
    [InlineData("resetCutover", 180)]
    [InlineData("sameEpochRestart", 210)]
    [InlineData("committedUnavailable", 60)]
    [InlineData("revokeGrantOn", 45)]
    [InlineData("revokeGrantOff", 45)]
    [InlineData("verifyNoCutoverMutation", 45)]
    public void Cutover_actions_have_an_exact_cap_inside_the_shortest_scenario(string action, int seconds)
    {
        var cap = Web6CutoverBudgets.ActionCap(action);
        Assert.Equal(TimeSpan.FromSeconds(seconds), cap);
        Assert.True(cap - Web6CutoverBudgets.ResponseReserve < Web6CutoverBudgets.FirstScenario);
    }

    [Fact]
    public void Unknown_cutover_actions_have_no_cap()
    {
        Assert.Throws<InvalidDataException>(() => Web6CutoverBudgets.ActionCap("unknown"));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(40000, 40000)]
    [InlineData(205000, 150000)]
    public void Cutover_phase_allowance_respects_both_work_and_phase_caps(int remainingMs, int expectedMs)
    {
        var workAllowance = TimeSpan.FromMilliseconds(remainingMs);
        var effective = Web6CutoverBudgets.PhaseAllowance(Web6CutoverBudgets.HostSwitchPhase, workAllowance);
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), effective);
        Assert.True(effective <= workAllowance);
    }

    [Theory]
    [InlineData(false, 41)]
    [InlineData(true, 44)]
    public async Task Pair_process_ceiling_admits_only_its_finite_budget_before_executable_validation(bool selector, int minutes)
    {
        var invalid = new ProcessStartInfo("not-an-absolute-executable") { UseShellExecute = false };
        await Assert.ThrowsAsync<ArgumentException>(() => GuiOwnedProcess.RunWeb6CandidatePairAsync(invalid, TimeSpan.FromMinutes(minutes), selector));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GuiOwnedProcess.RunWeb6CandidatePairAsync(invalid, TimeSpan.FromMinutes(minutes).Add(TimeSpan.FromMilliseconds(1)), selector));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GuiOwnedProcess.RunAsync(invalid, TimeSpan.FromMinutes(6)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GuiOwnedProcess.RunAttendedAsync(invalid, TimeSpan.FromMinutes(31)));
    }

    [Fact]
    public void Sequential_children_fit_strictly_inside_test_controller_process_and_pair_gate()
    {
        var child = TimeSpan.FromMilliseconds(1827800);
        var test = Web6CandidatePairBudgets.ThirdTest;
        var controller = Web6CandidatePairBudgets.ControllerLifetime(true);
        var process = Web6CandidatePairBudgets.BrowserTimeout(true, false, "chromium");
        var gate = Web6CandidatePairBudgets.GateTimeout("pair-chromium");
        Assert.True(test > child + TimeSpan.FromSeconds(60));
        Assert.True(controller > test + TimeSpan.FromSeconds(240 + 90 + 60));
        Assert.True(process > controller + TimeSpan.FromSeconds(60));
        Assert.True(gate > process + TimeSpan.FromSeconds(45 + 45 + 30));
        // Reject the hierarchy that omitted both pre-login bootstrap refreshes.
        Assert.True(test > TimeSpan.FromMinutes(31));
        Assert.True(controller > TimeSpan.FromMinutes(38));
        Assert.True(process > TimeSpan.FromMinutes(40));
        Assert.True(gate > TimeSpan.FromMinutes(43));
        Assert.Equal(TimeSpan.FromMinutes(32), test);
        Assert.Equal(TimeSpan.FromMinutes(39), controller);
        Assert.Equal(TimeSpan.FromMinutes(41), process);
        Assert.Equal(TimeSpan.FromMinutes(44), gate);
    }

    [Theory]
    [InlineData(false, false, "chromium")]
    [InlineData(true, true, "chromium")]
    [InlineData(true, false, "unknown")]
    [InlineData(true, false, null)]
    public void Non_pair_browser_modes_keep_original_five_minutes(bool candidate, bool cutover, string? project)
    {
        Assert.False(Web6CandidatePairBudgets.IsPairMode(candidate, cutover, project));
        Assert.Equal(TimeSpan.FromMinutes(5), Web6CandidatePairBudgets.BrowserTimeout(candidate, cutover, project));
        Assert.Equal(TimeSpan.FromMinutes(6), Web6CandidatePairBudgets.ControllerLifetime(false));
    }

    [Theory]
    [InlineData("cutover")]
    [InlineData("workload")]
    [InlineData("recovery")]
    public void Other_selector_gates_keep_twenty_five_minutes(string gate) =>
        Assert.Equal(TimeSpan.FromMinutes(25), Web6CandidatePairBudgets.GateTimeout(gate));

    [Theory]
    [InlineData("chromium")]
    [InlineData("edge")]
    public void Extended_selector_budget_requires_exact_pair_name_filter_and_environment(string project)
    {
        var env = new Dictionary<string, string> { ["MYDMSVN_WEB6_CANDIDATE_ENABLE"] = "1",
            ["MYDMSVN_WEB_PILOT_PROJECT"] = project, ["MYDMSVN_WEB6_BOOTSTRAP_RUN_ID"] = "unit-run" };
        var name = "pair-" + project;
        Assert.True(Web6CandidatePairBudgets.IsPairSelector(name, Web6CandidatePairBudgets.PairFilter, env));
        Assert.Equal(TimeSpan.FromMinutes(44), Web6CandidatePairBudgets.SelectorMaximum(name, Web6CandidatePairBudgets.PairFilter, env));
        foreach (var key in new[] { "MYDMSVN_WEB6_CANDIDATE_ENABLE", "MYDMSVN_WEB_PILOT_PROJECT", "MYDMSVN_WEB6_BOOTSTRAP_RUN_ID" })
        {
            var missing = new Dictionary<string, string>(env); missing.Remove(key);
            Assert.False(Web6CandidatePairBudgets.IsPairSelector(name, Web6CandidatePairBudgets.PairFilter, missing));
            Assert.Equal(TimeSpan.FromMinutes(30), Web6CandidatePairBudgets.SelectorMaximum(name, Web6CandidatePairBudgets.PairFilter, missing));
        }
        Assert.Equal(TimeSpan.FromMinutes(30), Web6CandidatePairBudgets.SelectorMaximum("cutover", Web6CandidatePairBudgets.PairFilter, env));
        Assert.Equal(TimeSpan.FromMinutes(30), Web6CandidatePairBudgets.SelectorMaximum(name, "FullyQualifiedName=Other.Test", env));
    }
}
