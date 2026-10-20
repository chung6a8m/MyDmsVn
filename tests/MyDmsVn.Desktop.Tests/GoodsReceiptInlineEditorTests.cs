using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Formatting;
using MyDmsVn.BootstrapSourceGrid.Editors;
using Xunit;
using Xunit.Abstractions;
using Grid = MyDmsVn.Bootstrap5WinFormUI.Controls.BootstrapSourceGrid;

namespace MyDmsVn.Desktop.Tests;

public sealed class GoodsReceiptInlineEditorTests
{
    private readonly ITestOutputHelper _output;
    public GoodsReceiptInlineEditorTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Native_cell_entry_uses_registered_bootstrap_editor()
    {
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var host = new EditorHost();
            Assert.Equal(4, IntPtr.Size);
            host.Begin(0, 1); // Native F2, dispatched through the grid window.
            AssertHosted(host.Grid, host.Number.Control);
            Type("3.125");
            Assert.Equal("3.125", host.Number.BootstrapControl.RawValue);
            Key(Keys.Enter);
            Assert.False(host.Number.IsEditing);
            Assert.Equal(3.125m, host.Grid[0, 1].Value);

            host.FocusCell(0, 2);
            Key(Keys.D7);
            PostMessage(host.Grid.Handle, 0x0102, new IntPtr('7'), IntPtr.Zero); // type-to-edit
            Pump();
            AssertHosted(host.Grid, host.Cost.Control);
            Assert.Equal("7", host.Cost.BootstrapControl.RawValue);
            Key(Keys.Escape);
            Assert.Equal(4m, host.Grid[0, 2].Value);
            host.FocusCell(0, 2);
            var bounds = host.Grid.PositionToRectangle(new SourceGrid.Position(0, 2));
            var previousPointer = Cursor.Position;
            var doubleClicks = 0;
            host.Grid.MouseDoubleClick += (_, _) => doubleClicks++;
            try
            {
                host.Form.Activate();
                var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
                var currentThread = GetCurrentThreadId();
                var attached = foregroundThread != currentThread && AttachThreadInput(currentThread, foregroundThread, true);
                try { SetForegroundWindow(host.Form.Handle); }
                finally { if (attached) Assert.True(AttachThreadInput(currentThread, foregroundThread, false)); }
                Assert.Equal(host.Form.Handle, GetForegroundWindow());
                Cursor.Position = host.Grid.PointToScreen(new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2));
                var hitClock = Stopwatch.StartNew();
                while (WindowFromPoint(Cursor.Position) != host.Grid.Handle && hitClock.Elapsed < TimeSpan.FromSeconds(2))
                {
                    token.ThrowIfCancellationRequested();
                    Pump();
                    Thread.Yield();
                }
                var underPointer = WindowFromPoint(Cursor.Position);
                Assert.True(underPointer == host.Grid.Handle,
                    "Pointer target=" + Control.FromHandle(underPointer)?.GetType().FullName + "; pointer=" + Cursor.Position +
                    "; grid=" + host.Grid.RectangleToScreen(host.Grid.ClientRectangle));
                // Real mouse input keeps native button/double-click state consistent.
                mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
                mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
                mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
                mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
                var mouseClock = Stopwatch.StartNew();
                while (!host.Cost.IsEditing && mouseClock.Elapsed < TimeSpan.FromSeconds(2))
                {
                    token.ThrowIfCancellationRequested();
                    Pump();
                    Thread.Yield();
                }
                AssertHosted(host.Grid, host.Cost.Control);
                Assert.Equal(1, doubleClicks);
            }
            finally { Cursor.Position = previousPointer; }
            Type("9.75");
            Key(Keys.Enter);
            Assert.Equal(9.75m, Assert.IsType<decimal>(host.Grid[0, 2].Value));

            host.Begin(0, 0);
            AssertHosted(host.Grid, host.Lookup.Control);
            SendMessage(GetFocus(), 0x00B1, IntPtr.Zero, new IntPtr(-1));
            Type("P8");
            Key(Keys.F4);
            Assert.True(host.Lookup.BootstrapControl.IsDropDownOpen);
            Key(Keys.Down);
            Key(Keys.Enter);
            Assert.False(host.Lookup.IsEditing);
            Assert.Equal(8, host.Grid[0, 0].Value);
            host.Begin(0, 0);
            Key(Keys.F4);
            Key(Keys.Escape);
            Assert.False(host.Lookup.BootstrapControl.IsDropDownOpen);
            Key(Keys.Escape);
            Assert.False(host.Lookup.IsEditing);
            Assert.Equal(8, host.Grid[0, 0].Value);
            host.Begin(0, 0);
            Key(Keys.Tab);
            Assert.Equal(new SourceGrid.Position(0, 1), host.Grid.Selection.ActivePosition);
            Assert.False(host.Lookup.IsEditing);
            _output.WriteLine("Native F2/type/mouse double-click, numeric Enter/Escape, lookup F4/Down/Enter/Escape/Tab; DPI=" + host.Form.DeviceDpi);
            _output.WriteLine("SCREEN=" + Screen.FromControl(host.Form).Bounds + "; THEME=" + MyDmsVn.Bootstrap5WinFormUI.Theme.BootstrapThemeManager.CurrentTheme.Mode);
        }, TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Logical_values_remain_typed_when_display_changes()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard();
            using var host = new EditorHost();
            host.Begin(0, 0);
            Type("pending");
            Assert.True(host.Lookup.BootstrapControl.HasPendingText);
            Assert.Equal(7, host.Lookup.BootstrapControl.SelectedValue);
            Assert.Equal(7, host.Grid[0, 0].Value);
            Key(Keys.Escape);
            if (host.Lookup.IsEditing) Key(Keys.Escape);
            host.Begin(0, 0);
            Assert.True(host.Lookup.BootstrapControl.SelectValue(8));
            Assert.True(host.Lookup.EditCellContext.EndEdit(false));
            Assert.Equal(8, Assert.IsType<int>(host.Grid[0, 0].Value));
            Assert.Equal("P8 - Second product", host.Grid[0, 0].DisplayText);

            host.Number.BootstrapControl.FormatMode = BootstrapInputFormatMode.Numeral;
            host.Begin(0, 1);
            Type("1234.5");
            Assert.Equal("1,234.5", host.Number.BootstrapControl.Text);
            Key(Keys.Enter);
            Assert.Equal(1234.5m, Assert.IsType<decimal>(host.Grid[0, 1].Value));
            Assert.Equal("1,234.5000", host.Grid[0, 1].DisplayText);
            Assert.False(host.Number.Control.Visible);
            host.Begin(1, 1);
            Assert.Equal("2.5", host.Number.BootstrapControl.RawValue);
            Key(Keys.Escape);
            Assert.Equal(2.5m, host.Grid[1, 1].Value);
        }, TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void Shared_editors_dispose_with_their_grid()
    {
        StaTest.Run(_ =>
        {
            using var guard = new WinFormsTestGuard();
            using var host = new EditorHost();
            using var other = new Grid();
            Assert.NotSame(host.Lookup, other.BootstrapEditors.CreateLookupBox(typeof(int)));
            var unused = host.Grid.BootstrapEditors.CreateFormattedTextBox(typeof(decimal));
            var controls = new[] { host.Lookup.Control, host.Number.Control, host.Cost.Control, unused.Control };
            var counts = new int[controls.Length];
            for (var i = 0; i < controls.Length; i++)
            {
                var index = i;
                controls[i].Disposed += (_, _) => counts[index]++;
            }
            host.Begin(0, 1);
            var numberHandle = host.Number.Control.Handle;
            Key(Keys.Escape);
            host.Begin(1, 1);
            Assert.Equal(numberHandle, host.Number.Control.Handle);
            Assert.Same(host.Grid[0, 1].Editor, host.Grid[1, 1].Editor);
            Key(Keys.Escape);
            host.Begin(0, 0);
            var lookupHandle = host.Lookup.Control.Handle;
            Key(Keys.Escape);
            host.Begin(1, 0);
            Assert.Equal(lookupHandle, host.Lookup.Control.Handle);
            Key(Keys.F4);
            Assert.True(host.Lookup.BootstrapControl.IsDropDownOpen);
            // The registry/grid is the sole editor owner, including active popup and never-started controls.
            var popupGrid = host.Lookup.BootstrapControl.ResultsGrid;
            host.Grid.Dispose();
            Assert.True(popupGrid.IsDisposed);
            Assert.All(controls, control => Assert.True(control.IsDisposed));
            Assert.False(host.Lookup.BootstrapControl.IsDropDownOpen);
            Assert.Equal(new[] { 1, 1, 1, 1 }, counts);
        }, TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void Native_uia_observes_the_stock_hosted_editor()
    {
        using var probe = new GuiProcessProbeFixture(package: false, inline: true);
        StaTest.Run(token =>
        {
            using var guard = new WinFormsTestGuard();
            using var host = new EditorHost();
            foreach (var position in new[] { new SourceGrid.Position(0, 1), new SourceGrid.Position(1, 1),
                new SourceGrid.Position(0, 2), new SourceGrid.Position(0, 0), new SourceGrid.Position(1, 0) })
            {
                var column = position.Column;
                host.Begin(position.Row, column);
                var name = (column == 0 ? "Product" : column == 1 ? "Quantity" : "Unit cost") + " row " + (position.Row + 1);
                var run = GuiOwnedProcess.RunAsync(probe.Start(host.Form.Handle.ToInt64() + " \"" + name + "\""), TimeSpan.FromSeconds(15), token);
                // GuiOwnedProcess owns the finite execution/cleanup budgets. Keep the host
                // alive and pumping until that task returns, including when token is cancelled.
                while (!run.IsCompleted)
                {
                    Pump();
                    Thread.Yield();
                }
                var result = run.GetAwaiter().GetResult();
                _output.WriteLine(result.StandardOutput);
                Assert.True(result.CleanupVerified);
                Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
                Assert.Contains("NATIVE_EDITOR_REACHABLE=PASS", result.StandardOutput);
                if (column != 0)
                {
                    var commitClock = Stopwatch.StartNew();
                    while ((column == 1 ? host.Number : host.Cost).IsEditing && commitClock.Elapsed < TimeSpan.FromSeconds(2))
                    {
                        Pump();
                        Thread.Yield();
                    }
                    Assert.False((column == 1 ? host.Number : host.Cost).IsEditing);
                    Assert.Equal(6.25m, Assert.IsType<decimal>(host.Grid[position.Row, column].Value));
                }
                else
                {
                    Assert.Equal("6.25", host.Lookup.BootstrapControl.Text);
                    Assert.True(host.Lookup.BootstrapControl.HasPendingText);
                    Assert.Equal(7, host.Lookup.BootstrapControl.SelectedValue);
                }
                if (host.Lookup.BootstrapControl.IsDropDownOpen) Key(Keys.Escape);
                if (host.Lookup.IsEditing) Key(Keys.Escape);
            }
        }, TimeSpan.FromSeconds(130));
    }

    private static void AssertHosted(Grid grid, Control editor)
    {
        Assert.True(editor.Visible, "Editor invisible; active=" + grid.Selection.ActivePosition);
        Assert.True(editor.ContainsFocus, "Focus=" + Control.FromHandle(GetFocus())?.GetType().FullName + "; editor=" + editor.GetType().FullName + "; active=" + grid.Selection.ActivePosition);
        Assert.True(grid.Contains(editor));
        Assert.True(grid.RectangleToScreen(grid.PositionToRectangle(grid.Selection.ActivePosition))
            .IntersectsWith(editor.RectangleToScreen(editor.ClientRectangle)));
    }

    private static void Pump() => System.Windows.Forms.Application.DoEvents();
    private static void Type(string text)
    {
        foreach (var ch in text) SendMessage(GetFocus(), 0x0102, new IntPtr(ch), IntPtr.Zero);
        Pump();
    }
    private static void Key(Keys key)
    {
        // Posted window messages traverse WinForms preprocessing (popup/Enter/Escape routing).
        Assert.True(PostMessage(GetFocus(), 0x0100, new IntPtr((int)key), IntPtr.Zero));
        Assert.True(PostMessage(GetFocus(), 0x0101, new IntPtr((int)key), IntPtr.Zero));
        Pump();
    }
    [DllImport("user32.dll")] private static extern IntPtr GetFocus();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr processId);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint first, uint second, bool attach);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extraInfo);

    private sealed class EditorHost : IDisposable
    {
        public readonly Form Form = new Form { ClientSize = new Size(700, 220), ShowInTaskbar = false, Text = "BCI-T00 proof" };
        public readonly Grid Grid = new Grid { Dock = DockStyle.Fill, AccessibleName = "Goods receipt lines proof" };
        public readonly BootstrapLookupBoxEditor Lookup;
        public readonly BootstrapFormattedTextBoxEditor Number;
        public readonly BootstrapFormattedTextBoxEditor Cost;
        public EditorHost()
        {
            Lookup = Grid.BootstrapEditors.CreateLookupBox(typeof(int));
            Lookup.BootstrapControl.DisplayMember = nameof(Product.Label);
            Lookup.BootstrapControl.ValueMember = nameof(Product.Id);
            Lookup.BootstrapControl.DataSource = new BindingList<Product> { new Product(7, "P7 - First product"), new Product(8, "P8 - Second product") };
            Lookup.BootstrapControl.SearchDebounceMilliseconds = 0;
            Lookup.ConvertingValueToDisplayString += (_, e) => { e.Value = (int)e.Value == 7 ? "P7 - First product" : "P8 - Second product"; e.ConvertingStatus = DevAge.ComponentModel.ConvertingStatus.Completed; };
            Number = Grid.BootstrapEditors.CreateFormattedTextBox(typeof(decimal));
            Cost = Grid.BootstrapEditors.CreateFormattedTextBox(typeof(decimal));
            foreach (var editor in new[] { Number, Cost })
            {
                editor.CultureInfo = CultureInfo.GetCultureInfo("en-US");
                editor.ConvertingValueToDisplayString += (_, e) => { e.Value = ((decimal)e.Value).ToString("N4", CultureInfo.GetCultureInfo("en-US")); e.ConvertingStatus = DevAge.ComponentModel.ConvertingStatus.Completed; };
                editor.BootstrapControl.FormatMode = BootstrapInputFormatMode.None;
            }
            Grid.Redim(2, 3);
            for (var row = 0; row < 2; row++)
            {
                Grid.Rows[row].Height = 42;
                Grid[row, 0] = new SourceGrid.Cells.Cell(7, typeof(int)) { Editor = Lookup };
                Grid[row, 1] = new SourceGrid.Cells.Cell(2.5m, typeof(decimal)) { Editor = Number };
                Grid[row, 2] = new SourceGrid.Cells.Cell(4m, typeof(decimal)) { Editor = Cost };
            }
            foreach (var editor in new SourceGrid.Cells.Editors.EditorBase[] { Lookup, Number, Cost })
                editor.EditableMode = SourceGrid.EditableMode.F2Key | SourceGrid.EditableMode.AnyKey | SourceGrid.EditableMode.DoubleClick;
            Grid.Columns[0].Width = 280;
            Grid.Columns[1].Width = Grid.Columns[2].Width = 180;
            Form.Controls.Add(Grid);
            Form.Show();
            Pump();
        }
        public void FocusCell(int row, int column)
        {
            Assert.True(Grid.Focus());
            Assert.True(Grid.Selection.Focus(new SourceGrid.Position(row, column), true));
            Pump();
            Assert.Equal(new SourceGrid.Position(row, column), Grid.Selection.ActivePosition);
            var context = new SourceGrid.CellContext(Grid, new SourceGrid.Position(row, column), Grid[row, column]);
            Grid[row, column].View.Measure(context, Size.Empty);
        }
        public void Begin(int row, int column)
        {
            FocusCell(row, column);
            var editor = (SourceGrid.Cells.Editors.EditorControlBase)Grid[row, column].Editor;
            editor.Control.AccessibleName = (column == 0 ? "Product" : column == 1 ? "Quantity" : "Unit cost") + " row " + (row + 1);
            Key(Keys.F2);
            Assert.True(editor.IsEditing);
        }
        public void Dispose()
        {
            foreach (var editor in new SourceGrid.Cells.Editors.EditorControlBase[] { Lookup, Number, Cost })
                if (!Grid.IsDisposed && editor.IsEditing) editor.EditCellContext.EndEdit(true);
            Form.Dispose();
        }
    }
    public sealed class Product
    {
        public Product(int id, string label) { Id = id; Label = label; }
        public int Id { get; }
        public string Label { get; }
    }
}
