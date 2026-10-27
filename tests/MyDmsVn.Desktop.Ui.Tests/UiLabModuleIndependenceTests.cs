using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Desktop.Tests;
using MyDmsVn.Desktop.UiLab;
using MyDmsVn.Desktop.UiLab.Scenarios;
using Xunit;

namespace MyDmsVn.Desktop.Ui.Tests;

[Trait("Module", "Common")]
public sealed class UiLabModuleIndependenceTests
{
    [Fact]
    [Trait("Module", "Security")]
    public void Security_start_reset_and_session_replacement_create_only_security_fixtures()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, __) =>
        {
            var created = new List<object>();
            Action<object> observer = fixture => created.Add(fixture);
            LabScenarioFactory.Created += observer;
            try
            {
                using var host = new SecurityLabForm();
                host.Show();
                Until(() => host.CurrentSecurityControl != null && !host.IsResetting);
                var original = host.CurrentSecurityControl!;
                var first = host.CurrentSecurityScenarioData!;
                Assert.Same(first, Assert.Single(created));
                Assert.Contains(ControlsUnder(host).OfType<Button>(), button => button.Text == "Security review tools");
                Assert.DoesNotContain(ControlsUnder(host).OfType<Button>(), button => button.Text == "Fail next Save");
                Assert.All(host.ScenarioSelector.Items.Cast<object>(), item => Assert.IsType<SecurityScenarioId>(item));

                var reset = host.ResetForTestAsync();
                Until(() => reset.IsCompleted);
                reset.GetAwaiter().GetResult();
                Assert.True(original.IsDisposed);
                Assert.NotSame(original, host.CurrentSecurityControl);
                Assert.NotSame(first, host.CurrentSecurityScenarioData);
                Assert.Equal(2, created.Count);
                Assert.All(created, item => Assert.IsType<SecurityScenario>(item));
                Assert.Same(host.CurrentSecurityScenarioData, created[1]);

                var replacement = host.CurrentSecurityControl!;
                var session = host.CurrentSecurityScenarioData!.Session;
                var version = session.Version;
                Button(host, "Replace session").PerformClick();
                Until(() => session.Version > version);
                Assert.Same(replacement, host.CurrentSecurityControl);
                Assert.Equal(2, created.Count);
                host.CloseWorkspaceButton.PerformClick();
                Until(() => host.CurrentSecurityControl == null);
                Assert.True(replacement.IsDisposed);
                Assert.Equal(2, created.Count);
            }
            finally { LabScenarioFactory.Created -= observer; }
        });

    [Fact]
    [Trait("Module", "Sales")]
    public void Sales_start_reset_and_session_replacement_create_only_sales_fixtures()
        => SecurityWorkspaceControlTests.Run(SecurityScenarioId.SEC_L01, (_, __) =>
        {
            var created = new List<object>();
            Action<object> observer = fixture => created.Add(fixture);
            LabScenarioFactory.Created += observer;
            try
            {
                using var host = new SalesLabForm();
                host.Show();
                Until(() => host.CurrentSalesControl != null && !host.IsResetting);
                var original = host.CurrentSalesControl!;
                var first = host.CurrentScenarioData!;
                Assert.Same(first, Assert.Single(created));
                Assert.Contains(ControlsUnder(host).OfType<Button>(), button => button.Text == "Fail next Save");
                Assert.DoesNotContain(ControlsUnder(host).OfType<Button>(), button => button.Text == "Security review tools");
                Assert.All(host.ScenarioSelector.Items.Cast<object>(), item => Assert.IsType<SalesScenarioId>(item));

                var reset = host.ResetForTestAsync();
                Until(() => reset.IsCompleted);
                reset.GetAwaiter().GetResult();
                Assert.True(original.IsDisposed);
                Assert.NotSame(original, host.CurrentSalesControl);
                Assert.NotSame(first, host.CurrentScenarioData);
                Assert.Equal(2, created.Count);
                Assert.All(created, item => Assert.IsType<SalesScenarioData>(item));
                Assert.Same(host.CurrentScenarioData, created[1]);

                var replacement = host.CurrentSalesControl!;
                var session = host.CurrentScenarioData!.Session;
                var version = session.Version;
                Button(host, "Replace session").PerformClick();
                Until(() => session.Version > version);
                Assert.Same(replacement, host.CurrentSalesControl);
                Assert.Equal(2, created.Count);
                host.CloseWorkspaceButton.PerformClick();
                Until(() => host.CurrentSalesControl == null);
                Assert.True(replacement.IsDisposed);
                Assert.Equal(2, created.Count);
            }
            finally { LabScenarioFactory.Created -= observer; }
        });

    private static Button Button(Control parent, string text) =>
        Assert.Single(ControlsUnder(parent).OfType<Button>(), button => button.Text == text);

    private static IEnumerable<Control> ControlsUnder(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in ControlsUnder(child)) yield return descendant;
        }
    }

    private static void Until(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(8)) throw new TimeoutException("UI Lab did not settle.");
            System.Windows.Forms.Application.DoEvents();
            System.Threading.Thread.Sleep(10);
        }
    }
}
