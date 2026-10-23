using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed class SalesReceiptAccessibilityTests
{
    [Fact]
    public void Seven_semantic_columns_expose_readonly_unit_amount_and_gift_checked_state()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            var grid = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid>("LineGrid");
            var table = grid.AccessibilityObject; Assert.Equal(AccessibleRole.Table, table.Role);
            var row = table.GetChild(1)!; Assert.Equal(7, row.GetChildCount());
            Assert.Equal("Unit row 1", row.GetChild(2)!.Name); Assert.Equal("box", row.GetChild(2)!.Value);
            foreach (var index in new[] { 0, 2, 6 }) Assert.True((row.GetChild(index)!.State & AccessibleStates.ReadOnly) != 0);
            var gift = row.GetChild(5)!; Assert.Equal(AccessibleRole.CheckButton, gift.Role);
            gift.DoDefaultAction(); Assert.True(host.Editor.Lines.Single().IsGift); Assert.True((gift.State & AccessibleStates.Checked) != 0);
            Assert.Equal(4m, host.Editor.Lines.Single().UnitPrice); Assert.Equal(0m, host.Editor.Lines.Single().LineAmount);
            gift.Value = "false"; Assert.False(host.Editor.Lines.Single().IsGift); Assert.Equal("4", row.GetChild(4)!.Value);
        }, TimeSpan.FromSeconds(20));
    }

    [Theory]
    [InlineData("Posted")]
    [InlineData("Busy")]
    [InlineData("Denied")]
    [InlineData("Recovery")]
    public void Locked_states_reject_checkbox_pointer_keyboard_and_accessibility_mutation(string state)
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load(); Task? pending = null;
            try
            {
                if (state == "Posted") { Assert.False(host.Editor.IsDirty); host.Api.Posted = true; host.Load(); Assert.True(host.Editor.Status == "Posted", "Status=" + host.Editor.Status + ";dirty=" + host.Editor.IsDirty + ";busy=" + host.Editor.IsBusy + ";error=" + host.Editor.Error?.Message + ";ui=" + host.Value<Label>("OperationStatusLabel").Text); }
                else if (state == "Busy") { host.Editor.Note = "changed"; host.Api.SaveBarrier = new TaskCompletionSource<ApiResponse<SalesReceiptDto>>(); pending = (Task)host.Call("SaveAsync")!; Assert.True(host.Editor.IsBusy); }
                else if (state == "Denied") host.Editor.SetAccess(true, false, true);
                else host.CallTask("PostAsync");
                var line = host.Editor.Lines.Single(); var price = line.UnitPrice; var giftBefore = line.IsGift;
                var grid = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid>("LineGrid"); var gift = grid.AccessibilityObject.GetChild(1)!.GetChild(5)!;
                Assert.True((gift.State & AccessibleStates.ReadOnly) != 0); Assert.Throws<InvalidOperationException>(() => gift.Value = "true");
                Assert.Throws<InvalidOperationException>(() => gift.DoDefaultAction());
                grid.Focus(); grid.Selection.Focus(new SourceGrid.Position(1, 5), true); NativeLookupTypingDriver.KeyCharacter(grid, Keys.Space, ' ');
                var bounds = grid.PositionToRectangle(new SourceGrid.Position(1, 5)); NativeLookupTypingDriver.Click(grid, new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2));
                Assert.Equal(giftBefore, line.IsGift); Assert.Equal(price, line.UnitPrice);
                Assert.False(host.Editor.CanEdit); Assert.Equal(state == "Recovery", host.Editor.RequiresPostOutcomeReload);
            }
            finally
            {
                if (pending != null) { host.Api.SaveBarrier!.TrySetResult(ApiResponse<SalesReceiptDto>.Success(SalesGuiApi.Receipt(41))); var until = DateTime.UtcNow.AddSeconds(5); while (!pending.IsCompleted && DateTime.UtcNow < until) System.Windows.Forms.Application.DoEvents(); Assert.True(pending.IsCompleted); pending.GetAwaiter().GetResult(); }
            }
        }, TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Removed_duplicate_accessibility_identity_cannot_target_retained_row()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard(); using var host = new SalesControlHost(); host.Load();
            var a = host.Editor.Lines.Single(); var b = host.Editor.AddLine()!; b.SetProduct(7, "Same", true, "box");
            var grid = host.Value<MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid>("LineGrid"); var stale = grid.AccessibilityObject.GetChild(1)!.GetChild(5)!;
            Assert.True(host.Editor.RemoveLine(a)); Assert.True((stale.State & AccessibleStates.Unavailable) != 0);
            Assert.Throws<InvalidOperationException>(() => stale.Value = "true"); Assert.False(b.IsGift); Assert.Single(host.Editor.Lines);
        }, TimeSpan.FromSeconds(20));
    }
}
