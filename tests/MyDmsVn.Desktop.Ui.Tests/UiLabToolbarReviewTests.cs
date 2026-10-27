using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.UiLab;
using MyDmsVn.Desktop.UiLab.Scenarios;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

[Trait("Module", "Sales")]
public sealed class UiLabToolbarReviewTests
{
    [Fact]
    public void Lab_startup_and_theme_toggle_use_product_palette_and_preserve_reduced_motion()
    {
        var previous = MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeManager.CurrentTheme;
        try
        {
            MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeManager.CurrentTheme =
                MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapTheme.CreateDefault(
                    MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeMode.Light, true);
            Run(SalesScenarioId.S01Draft, null, (form, guard, token) =>
            {
                var current = MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeManager.CurrentTheme;
                Assert.Equal(BusinessCompactAppearance.CreateTheme(current.Mode).Colors.Primary, current.Colors.Primary);
                Assert.True(current.ReducedMotion);
                var button = form.Controls.OfType<Panel>().SelectMany(p => p.Controls.OfType<Panel>()).SelectMany(p => p.Controls.OfType<FlowLayoutPanel>()).SelectMany(p => p.Controls.OfType<Button>()).First(b => b.Text == "Light / Dark");
                button.PerformClick();
                current = MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeManager.CurrentTheme;
                Assert.Equal(MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeMode.Dark, current.Mode);
                Assert.Equal(BusinessCompactAppearance.CreateTheme(current.Mode).Colors.Primary, current.Colors.Primary);
                Assert.True(current.ReducedMotion);
                Assert.Empty(guard.Exceptions);
            });
        }
        finally { MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeManager.CurrentTheme = previous; }
    }

    [Fact]
    public void Narrow_sales_toolbar_keeps_access_and_reload_release_reachable_without_resizing_fixed_viewport()
    {
        Run(SalesScenarioId.S01Draft, null, (form, guard, token) =>
        {
            form.ClientSize = new Size(900, 700);
            var width = Assert.IsType<NumericUpDown>(Assert.Single(form.Controls.Find("ReviewViewportWidth", true)));
            var height = Assert.IsType<NumericUpDown>(Assert.Single(form.Controls.Find("ReviewViewportHeight", true)));
            var viewportApply = Assert.IsType<Button>(Assert.Single(form.Controls.Find("ReviewViewportApply", true)));
            width.Value = 652;
            height.Value = 497;
            viewportApply.PerformClick();
            var control = form.CurrentSalesControl!;
            var viewport = control.ClientSize;
            var toolbar = Assert.IsType<FlowLayoutPanel>(form.ApplyAccessButton.Parent);
            var status = toolbar.Controls.OfType<Label>().Single();
            status.Text = "A long review status describes an error and the recovery action for the current workspace.";
            System.Windows.Forms.Application.DoEvents();

            void ClickVisible(Button button)
            {
                var scrollArea = Assert.IsType<Panel>(toolbar.Parent);
                Assert.True(scrollArea.VerticalScroll.Visible, "Toolbar scroll bar is unavailable.");
                Assert.True(PostMessage(scrollArea.Handle, 0x0115, new IntPtr(7), IntPtr.Zero)); // WM_VSCROLL / SB_BOTTOM
                System.Windows.Forms.Application.DoEvents();
                var bounds = scrollArea.RectangleToClient(button.RectangleToScreen(button.ClientRectangle));
                Assert.True(scrollArea.ClientRectangle.Contains(bounds),
                    $"{button.Text} is clipped: bounds={bounds}, toolbar viewport={scrollArea.ClientRectangle}.");
                Assert.True(button.Enabled, button.Text + " is disabled.");
                NativeLookupTypingDriver.Click(button, Center(button));
            }

            form.PermissionVariantSelector.SelectedIndex = 2;
            ClickVisible(form.ApplyAccessButton);
            Until(() => !WriteAllowed(form.CurrentScenarioData!), token, "Deny Write did not apply.");
            ClickVisible(form.HoldReloadButton);
            control.ReloadButton.PerformClick();
            Until(() => control.Editor.IsBusy && form.ReleaseReloadButton.Enabled, token, "Reload did not reach its gate.");
            ClickVisible(form.ReleaseReloadButton);
            Until(() => !control.Editor.IsBusy && control.LastOperation.IsCompleted, token, "Reload did not complete.");
            Assert.Equal(viewport, control.ClientSize);
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void Native_close_click_preserves_pending_customer_query_when_prompt_cancels()
    {
        var prompts = 0;
        Run(SalesScenarioId.S01Draft, (_, __) =>
        {
            prompts++;
            return Task.FromResult(WorkspaceCloseChoice.Cancel);
        }, (form, guard, token) =>
        {
            var data = form.CurrentScenarioData!;
            var control = form.CurrentSalesControl!;
            var source = data.CreatedLookupSources.First(item => item.Kind == CatalogKind.Customer);
            var gate = new ScenarioGate<ApiResponse<System.Collections.Generic.IReadOnlyList<CatalogLookupDto>>>();
            source.LookupGate = gate;
            var before = source.LookupCalls.Count;
            var query = new NativeLookupTypingDriver(control.CustomerLookup);
            query.SelectRange(0, query.Text.Length);
            query.TypeCharacter('Z');
            Until(() => source.LookupCalls.Count > before &&
                source.OutstandingOperations.Any(task => !task.IsCompleted), token,
                "Pending Customer query did not reach the fake lookup source.");
            var raw = query.Text;
            Assert.True(control.CustomerLookup.HasPendingText);

            var button = form.CloseWorkspaceButton;
            var center = Center(button);
            var location = new IntPtr((center.Y << 16) | (center.X & 0xffff));
            Assert.True(PostMessage(button.Handle, 0x0201, new IntPtr(1), location));
            System.Windows.Forms.Application.DoEvents();
            query.AssertFocused();
            Assert.Equal(raw, control.CustomerLookup.Text);
            Assert.True(PostMessage(button.Handle, 0x0202, IntPtr.Zero, location));
            System.Windows.Forms.Application.DoEvents();
            Until(() => prompts == 1, token, "Native Close click did not reach the close coordinator prompt.");
            Assert.Same(control, form.CurrentSalesControl);
            Assert.False(control.IsDisposed);
            Assert.False(form.IsDisposed);
            Assert.Equal(raw, control.CustomerLookup.Text);
            Assert.True(control.CustomerLookup.HasPendingText);
            Assert.DoesNotContain(data.Journal.Entries, entry => entry.Operation == "Update.request");
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void Access_change_is_not_applied_during_held_reload_but_applies_after_release()
    {
        Run(SalesScenarioId.S01Draft, null, (form, guard, token) =>
        {
            var data = form.CurrentScenarioData!;
            var control = form.CurrentSalesControl!;
            form.HoldReloadButton.PerformClick();
            Assert.False(form.HoldReloadButton.Enabled);
            var before = data.Journal.Entries.Count(entry => entry.Operation == "Get.request");
            control.ReloadButton.PerformClick();
            Until(() => data.Journal.Entries.Count(entry => entry.Operation == "Get.request") > before &&
                control.Editor.IsBusy, token, "Reload did not wait at the held Get response.");

            form.PermissionVariantSelector.SelectedIndex = 2; // Deny Write.
            Until(() => !form.ApplyAccessButton.Enabled, token, "Access command stayed enabled during Reload.");
            NativeLookupTypingDriver.Click(form.ApplyAccessButton, Center(form.ApplyAccessButton));
            Assert.True(WriteAllowed(data));

            form.ReleaseReloadButton.PerformClick();
            Until(() => !control.Editor.IsBusy && control.LastOperation.IsCompleted,
                token, "Held Reload did not finish after release.");
            control.LastOperation.GetAwaiter().GetResult();
            Assert.True(control.SaveButton.Enabled);
            Until(() => form.ApplyAccessButton.Enabled, token, "Access command did not reenable after Reload.");
            form.ApplyAccessButton.PerformClick();
            Until(() => !WriteAllowed(data) && !control.SaveButton.Enabled,
                token, "Deny Write did not apply after Reload completed.");
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void Repeated_hold_click_cannot_strand_first_reload_gate()
    {
        Run(SalesScenarioId.S01Draft, null, (form, guard, token) =>
        {
            var data = form.CurrentScenarioData!;
            var control = form.CurrentSalesControl!;
            form.HoldReloadButton.PerformClick();
            Assert.False(form.HoldReloadButton.Enabled);
            NativeLookupTypingDriver.Click(form.HoldReloadButton, Center(form.HoldReloadButton));
            var before = data.Journal.Entries.Count(entry => entry.Operation == "Get.request");
            control.ReloadButton.PerformClick();
            Until(() => data.Journal.Entries.Count(entry => entry.Operation == "Get.request") > before &&
                control.Editor.IsBusy, token, "Reload did not wait at the first held gate.");
            form.ReleaseReloadButton.PerformClick();
            Until(() => !control.Editor.IsBusy && control.LastOperation.IsCompleted &&
                data.SalesApi.OutstandingOperations.All(task => task.IsCompleted), token,
                "The first held Reload remained stranded after Release.");
            control.LastOperation.GetAwaiter().GetResult();
            Assert.Equal(SalesScenarioData.SeedSaleId, control.Editor.SaleId);
            Assert.Empty(guard.Exceptions);
        });
    }

    [Fact]
    public void Repeated_arm_click_cannot_strand_first_lookup_pair()
    {
        Run(SalesScenarioId.S03LookupRace, null, (form, guard, token) =>
        {
            var data = form.CurrentScenarioData!;
            var control = form.CurrentSalesControl!;
            var source = data.CreatedLookupSources.First(item => item.Kind == CatalogKind.Product);
            form.ArmRaceButton.PerformClick();
            Assert.False(form.ArmRaceButton.Enabled);
            NativeLookupTypingDriver.Click(form.ArmRaceButton, Center(form.ArmRaceButton));
            var before = source.LookupCalls.Count;
            var grid = control.LineGrid;
            grid.Focus();
            var position = new SourceGrid.Position(1, 1);
            Assert.True(grid.Selection.Focus(position, true));
            new SourceGrid.CellContext(grid, position).StartEdit();
            var query = new NativeLookupTypingDriver(control.ProductLookup);
            query.SelectRange(0, query.Text.Length);
            query.TypeCharacter('P');
            Until(() => source.LookupCalls.Count > before, token, "Lookup A did not dispatch.");
            query.SelectRange(0, query.Text.Length);
            query.TypeCharacter('B');
            Until(() => source.LookupCalls.Count > before + 1, token, "Lookup B did not dispatch.");

            form.CompleteBButton.PerformClick();
            form.CompleteAButton.PerformClick();
            Until(() => source.OutstandingOperations.All(task => task.IsCompleted) &&
                control.LastOperation.IsCompleted, token,
                "The first lookup pair remained stranded after A and B were completed.");
            control.LastOperation.GetAwaiter().GetResult();
            Assert.True(source.LookupCalls.Count >= before + 2);
            Assert.Empty(guard.Exceptions);
        });
    }

    private static bool WriteAllowed(SalesScenarioData data) =>
        data.Permissions.CheckAsync("Sales.Receipts.Write", CancellationToken.None)
            .GetAwaiter().GetResult().Data!.IsAllowed;

    private static Point Center(Control control) =>
        new Point(control.ClientSize.Width / 2, control.ClientSize.Height / 2);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private static void Run(SalesScenarioId id,
        Func<WorkspaceCloseState, CancellationToken, Task<WorkspaceCloseChoice>>? prompt,
        Action<SalesLabForm, WinFormsTestGuard, CancellationToken> action)
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
                SalesLabForm? form = null;
                try
                {
                    form = prompt == null ? new SalesLabForm() : new SalesLabForm(prompt);
                    form.Show();
                    Until(() => form.CurrentScenarioData != null && form.CurrentSalesControl != null &&
                        !form.IsResetting, token, "Initial UI Lab workspace did not activate.");
                    if (id != SalesScenarioId.S01Draft)
                    {
                        form.ScenarioSelector.SelectedItem = id;
                        Until(() => form.CurrentScenarioData?.Id == id && !form.IsResetting,
                            token, "Selected UI Lab scenario did not activate.");
                    }
                    var control = form.CurrentSalesControl!;
                    foreach (var lookup in new[] { control.CustomerLookup, control.WarehouseLookup,
                        control.EmployeeLookup, control.ProductLookup, control.CustomerFilterLookup,
                        control.WarehouseFilterLookup, control.EmployeeFilterLookup, control.ProductFilterLookup })
                        guard.Attach(lookup.ResultsGrid);
                    NativeLookupTypingDriver.Activate(form);
                    action(form, guard, token);
                }
                catch (Exception exception) { error = ExceptionDispatchInfo.Capture(exception); }
                finally
                {
                    try
                    {
                        if (form != null)
                        {
                            var data = form.CurrentScenarioData;
                            data?.CancelPending();
                            var pending = form.CurrentSalesControl?.LastOperation ?? Task.CompletedTask;
                            Until(() => pending.IsCompleted, token, "Toolbar operation did not drain during test cleanup.");
                            if (data != null)
                            {
                                var drain = data.DrainAsync(TimeSpan.FromSeconds(5));
                                Until(() => drain.IsCompleted, token, "Fixture operations did not drain during test cleanup.");
                                drain.GetAwaiter().GetResult();
                            }
                            form.CurrentSalesControl?.Dispose();
                            data?.Dispose();
                            form.Dispose();
                        }
                    }
                    catch (Exception cleanup) { error ??= ExceptionDispatchInfo.Capture(cleanup); }
                    finally { loop.ExitThread(); }
                }
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
