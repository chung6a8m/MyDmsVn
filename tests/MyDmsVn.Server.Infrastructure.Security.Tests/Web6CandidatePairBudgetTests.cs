using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MyDmsVn.Desktop.Tests;
using System.Collections.Generic;
using MyDmsVn.Web6.CandidateBootstrap;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CandidatePairBudgetTests
{
    [Theory]
    [InlineData(false, 40)]
    [InlineData(true, 43)]
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
        var child = TimeSpan.FromMilliseconds(1797800);
        var test = Web6CandidatePairBudgets.ThirdTest;
        var controller = Web6CandidatePairBudgets.ControllerLifetime(true);
        var process = Web6CandidatePairBudgets.BrowserTimeout(true, false, "chromium");
        var gate = Web6CandidatePairBudgets.GateTimeout("pair-chromium");
        Assert.True(test > child + TimeSpan.FromSeconds(60));
        Assert.True(controller > test + TimeSpan.FromSeconds(240 + 90 + 60));
        Assert.True(process > controller + TimeSpan.FromSeconds(60));
        Assert.True(gate > process + TimeSpan.FromSeconds(45 + 45 + 30));
        Assert.Equal(TimeSpan.FromMinutes(31), test);
        Assert.Equal(TimeSpan.FromMinutes(38), controller);
        Assert.Equal(TimeSpan.FromMinutes(40), process);
        Assert.Equal(TimeSpan.FromMinutes(43), gate);
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
        Assert.Equal(TimeSpan.FromMinutes(43), Web6CandidatePairBudgets.SelectorMaximum(name, Web6CandidatePairBudgets.PairFilter, env));
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
