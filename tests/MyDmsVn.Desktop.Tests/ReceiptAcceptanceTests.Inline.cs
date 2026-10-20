using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.WinForms;
using SourceGrid.Cells.Editors;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed partial class ReceiptAcceptanceTests
{
    [Fact]
    public void Submitted_line_errors_focus_the_actual_cell_in_submitted_order()
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var fixture = new Fixture();
            using var control = fixture.CreateControl();
            using var host = new Form { Size = new Size(1500, 850) };
            host.Controls.Add(control);
            host.Show();
            Load(control, token);
            var line = fixture.Editor.AddLine();
            line.SetProduct(8, "P8", true);
            line.UnitCost = 2m;
            fixture.Api.UpdateError = new ApiError(ApiStatusCode.BadRequest, "Validation.Failed", "Rejected",
                new[] { new ApiErrorDetail("Invalid", "Second quantity rejected", "lines[1].quantity") });
            BeginReceiptCell(control, 3);
            control.SaveButton.PerformClick();
            Drain(control.LastOperation, token);
            Assert.Equal("Second quantity rejected", control.GetFieldError("lines[1].quantity"));
            Assert.Equal(new SourceGrid.Position(2, 2), control.LineGrid.Selection.ActivePosition);
            Assert.True(control.QuantityEditor.ContainsFocus);
            Assert.Equal(8, fixture.Api.Updates[0].Lines[1].ProductId);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Replacing_a_document_during_save_rejects_the_old_completion()
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var fixture = new Fixture();
            using var control = fixture.CreateControl();
            using var host = new Form { Size = new Size(1500, 850) };
            host.Controls.Add(control);
            host.Show();
            Load(control, token);
            BeginReceiptCell(control, 2);
            control.QuantityEditor.Text = "6.125";
            fixture.Api.PendingUpdate = new TaskCompletionSource<ApiResponse<GoodsReceiptDto>>();
            control.SaveButton.PerformClick();
            var save = control.LastOperation;
            fixture.Editor.Load(Receipt(note: "replacement"));
            fixture.Editor.Note = "replacement input";
            var notification = fixture.Notifications.LastNotification;
            fixture.Api.PendingUpdate.SetResult(ApiResponse<GoodsReceiptDto>.Success(Receipt(note: "old save")));
            Drain(save, token);
            Assert.Equal("replacement input", fixture.Editor.Note);
            Assert.True(fixture.Editor.IsDirty);
            Assert.Same(notification, fixture.Notifications.LastNotification);
            Assert.Empty(guard.Exceptions);
        }, TimeSpan.FromSeconds(10));
    }

    private static SourceGrid.CellContext BeginReceiptCell(GoodsReceiptControl control, int column)
    {
        control.LineGrid.Focus();
        var position = new SourceGrid.Position(1, column);
        Assert.True(control.LineGrid.Selection.Focus(position, true));
        var context = new SourceGrid.CellContext(control.LineGrid, position);
        context.StartEdit();
        Assert.True(context.IsEditing());
        return context;
    }
}
