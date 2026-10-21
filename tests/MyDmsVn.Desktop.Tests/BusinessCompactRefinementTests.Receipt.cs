using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Theme;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.WinForms;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

public sealed partial class BusinessCompactRefinementTests
{
    [Theory]
    [InlineData(BootstrapThemeMode.Light, 1200)]
    [InlineData(BootstrapThemeMode.Dark, 1200)]
    [InlineData(BootstrapThemeMode.Light, 1680)]
    [InlineData(BootstrapThemeMode.Dark, 1680)]
    public void Receipt_composition_and_commands_match_reference(BootstrapThemeMode mode, int width) => Run((token, guard) =>
    {
        var previous = BootstrapThemeManager.CurrentTheme;
        try
        {
            BootstrapThemeManager.CurrentTheme = BusinessCompactAppearance.CreateTheme(mode);
            using var fixture = new ReceiptAcceptanceTests.Fixture();
            fixture.Api.Current = new GoodsReceiptDto(41, "GR0000000041", new DateTime(2026, 10, 21), 3, 5, "Draft",
                "Office supplies — October / ghi chú Unicode", new DateTime(2026, 10, 21), "AQIDBAUGBwg=",
                Enumerable.Range(1, 18).Select(index => new GoodsReceiptLineDto(90 + index, index, 7, index + .25m, 75m,
                    "P7 — Giấy A4 80gsm / office supplies " + index, true)).ToArray(), "W3 — Central warehouse", true, "E5 — Nguyễn An", true);
            using var receipt = fixture.CreateControl();
            using var shell = Shell();
            shell.ClientSize = new Size(width, width == 1680 ? 900 : 760);
            shell.SetCurrentUser("BCF Operator");
            shell.OpenWorkspace("receipts", "Goods Receipts", receipt, shell.WorkspaceSessionVersion);
            shell.Show();
            NativeLookupTypingDriver.Activate(shell);
            Assert.Equal(width, shell.ClientSize.Width); // Never call a fitted window a full-width capture.
            Assert.Equal(ReceiptWorkspaceMode.List, receipt.WorkspaceMode);
            WaitCatalog(receipt.ActivateAsync(token));
            var directory = Path.Combine(GuiSqlEnvironment.RepositoryRoot(), "TestResults/bcf-t05-captures/native" + (Environment.GetEnvironmentVariable("MYDMSVN_BCF_EXPECTED_SCALE") ?? "100"));
            Directory.CreateDirectory(directory);
            void Snapshot(string state) => File.Copy(Capture(shell, "t05-" + state + "-" + width, token), Path.Combine(directory, "receipt-" + width + "-" + mode + "-" + state + ".png"), true);
            Snapshot("list");
            WaitCatalog(receipt.SelectReceiptAsync(41, token));

            var split = Assert.Single(receipt.Controls.OfType<SplitContainer>());
            Assert.True(split.Panel1Collapsed);
            Assert.False(receipt.ReceiptGrid.Visible);
            Assert.False(receipt.VersionLabel.Visible);
            Assert.Equal("AQIDBAUGBwg=", fixture.Editor.Version);
            Assert.True(receipt.SaveButton.Outline);
            Assert.False(receipt.PostButton.Outline);
            Assert.True(receipt.RemoveLineButton.Visible);
            Assert.Equal(19, receipt.LineGrid.RowsCount);
            Assert.Equal(5, receipt.LineGrid.ColumnsCount);
            var metadata = Assert.Single(Descendants(receipt).OfType<TableLayoutPanel>(), control => control.Name == "ReceiptMetadata");
            Assert.Equal(metadata.ColumnCount, metadata.GetColumnSpan(receipt.NoteEditor.Parent!));
            Assert.True(receipt.WarehouseLookup.PointToScreen(Point.Empty).Y < receipt.LineGrid.PointToScreen(Point.Empty).Y);
            Assert.True(receipt.NoteEditor.Width > receipt.WarehouseLookup.Width);
            if (width == 1680)
            {
                Assert.Equal(3, metadata.ColumnCount);
                Assert.True(Enumerable.Range(0, receipt.LineGrid.ColumnsCount).Sum(index => receipt.LineGrid.Columns[index].Width) <= receipt.LineGrid.ClientSize.Width,
                    "All five columns, including safe numeric widths, must coexist at the wide reference viewport.");
            }
            Snapshot("saved");
            var line = fixture.Editor.Lines[10];
            receipt.LineGrid.Selection.Focus(new SourceGrid.Position(11, 2), true);
            receipt.LineGrid.ShowCell(new SourceGrid.Position(11, 2), false);
            shell.ClientSize = new Size(944, 600);
            shell.ToggleTheme();
            System.Windows.Forms.Application.DoEvents();
            Assert.Same(line, fixture.Editor.Lines[10]);
            Assert.Equal(new SourceGrid.Position(11, 2), receipt.LineGrid.Selection.ActivePosition);
            receipt.NoteEditor.Text = "Unsaved — preserve this text through reflow";
            Assert.False(receipt.TryShowReceiptList());
            Snapshot("dirty-constrained-opposite-theme");
            fixture.Editor.Load(fixture.Api.Current);
            receipt.LineGrid.Focus(); receipt.LineGrid.Selection.Focus(new SourceGrid.Position(1, 2), true);
            GoodsReceiptInlineEditorTests.Key(Keys.F2);
            receipt.QuantityEditor.Text = "8.00001";
            receipt.SaveButton.PerformClick();
            Assert.Contains("decimal places", receipt.OperationStatusLabel.Text);
            Assert.False(receipt.TryShowReceiptList());
            Assert.True(receipt.OperationStatusLabel.Visible);
            Snapshot("invalid-constrained-opposite-theme");
            GoodsReceiptInlineEditorTests.Key(Keys.Escape);
            fixture.Api.CommitThenLosePostResponse = true;
            receipt.PostButton.PerformClick(); WaitCatalog(receipt.LastOperation);
            Assert.True(fixture.Editor.RequiresPostOutcomeReload);
            Assert.False(receipt.TryShowReceiptList());
            Snapshot("recovery-constrained-opposite-theme");
            receipt.RefreshButton.PerformClick(); WaitCatalog(receipt.LastOperation);
            Assert.Equal("Posted", fixture.Editor.Status);
            Assert.False(receipt.SaveButton.Enabled);
            Snapshot("posted-constrained-opposite-theme");
            Assert.True(receipt.TryShowReceiptList());
            Assert.True(split.Panel2Collapsed);
            _output.WriteLine("T05: reference width=" + width + "; final=" + shell.ClientSize + "; metadata columns=" + metadata.ColumnCount + "; same receipt control; all owned probes cleaned.");
        }
        finally { BootstrapThemeManager.CurrentTheme = previous; }
    });
}
