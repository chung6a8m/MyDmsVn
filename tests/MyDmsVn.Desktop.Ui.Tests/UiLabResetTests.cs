using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.UiLab;
using MyDmsVn.Desktop.UiLab.Scenarios;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

[Trait("Module", "Sales")]
public sealed class UiLabResetTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reset_drains_held_native_lookup_and_replaces_workspace(bool ignoresCancellation)
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            ExceptionDispatchInfo? error = null;
            using var loop = new ApplicationContext();
            using var startup = new Control();
            startup.CreateControl();
            startup.BeginInvoke(new Action(() =>
            {
                try
                {
                    using var form = new SalesLabForm();
                    form.Show();
                    Until(() => form.CurrentScenarioData != null && form.CurrentSalesControl != null && !form.IsResetting,
                        token, "Initial UI Lab workspace did not activate.");
                    NativeLookupTypingDriver.Activate(form);
                    var oldData = form.CurrentScenarioData!;
                    var oldControl = form.CurrentSalesControl!;
                    guard.Attach(oldControl.ProductLookup.ResultsGrid);
                    var productSource = oldData.CreatedLookupSources.First(source => source.Kind == CatalogKind.Product);
                    var gate = new ScenarioGate<ApiResponse<System.Collections.Generic.IReadOnlyList<CatalogLookupDto>>>
                    {
                        IgnoreCancellation = ignoresCancellation,
                    };
                    productSource.LookupGate = gate;
                    var before = productSource.LookupCalls.Count;
                    var grid = oldControl.LineGrid;
                    grid.Focus();
                    var position = new SourceGrid.Position(1, 1);
                    Assert.True(grid.Selection.Focus(position, true));
                    new SourceGrid.CellContext(grid, position).StartEdit();
                    var native = new NativeLookupTypingDriver(oldControl.ProductLookup);
                    native.SelectRange(0, native.Text.Length);
                    native.TypeCharacter('P');
                    Until(() => productSource.LookupCalls.Count > before &&
                        productSource.OutstandingOperations.Any(task => !task.IsCompleted),
                        token, "Held Product lookup was not dispatched before Reset.");

                    var reset = form.ResetForTestAsync();
                    Until(() => reset.IsCompleted, token, "UI Lab Reset did not drain held lookup.");
                    reset.GetAwaiter().GetResult();
                    Assert.False(form.IsPoisoned);
                    Assert.NotSame(oldData, form.CurrentScenarioData);
                    Assert.NotSame(oldControl, form.CurrentSalesControl);
                    Assert.True(oldControl.IsDisposed);
                    Assert.True(gate.IsCompleted);
                    Assert.False(gate.Complete(ApiResponse<System.Collections.Generic.IReadOnlyList<CatalogLookupDto>>.Success(
                        new[] { new CatalogLookupDto(12, "P-012", "Late result", true) })));
                    Assert.Equal(SalesScenarioData.SeedSaleId, form.CurrentSalesControl!.Editor.SaleId);
                    Assert.Equal(11, form.CurrentSalesControl.Editor.Lines[0].ProductId);
                    Assert.DoesNotContain(oldData.Journal.Entries, entry => entry.Operation == "Update.request" ||
                        entry.Operation == "Post.request");
                    Assert.Empty(guard.Exceptions);
                }
                catch (Exception exception) { error = ExceptionDispatchInfo.Capture(exception); }
                finally { loop.ExitThread(); }
            }));
            System.Windows.Forms.Application.Run(loop);
            error?.Throw();
        }, TimeSpan.FromSeconds(35));
    }

    private static void Until(Func<bool> condition, CancellationToken token, string message)
    {
        var clock = Stopwatch.StartNew();
        while (!condition() && clock.Elapsed < TimeSpan.FromSeconds(8))
        {
            token.ThrowIfCancellationRequested();
            System.Windows.Forms.Application.DoEvents();
            Thread.Yield();
        }
        Assert.True(condition(), message);
        System.Windows.Forms.Application.DoEvents();
    }
}
