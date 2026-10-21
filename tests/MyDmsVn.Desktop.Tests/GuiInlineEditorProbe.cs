// Bounded cross-process observation client; compiled only for the Desktop test host.
using System;
using System.Windows.Automation;
using System.Runtime.InteropServices;

internal static class GuiInlineEditorProbe
{
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Console.WriteLine("UIA_CLIENT_BITNESS=" + (IntPtr.Size * 8));
            if (args[1] == "measure-window") return Measure(new IntPtr(long.Parse(args[0])), int.Parse(args[2]));
            if (args[1] == "measure-lookup") return Measure(new IntPtr(long.Parse(args[0])), int.Parse(args[2]), true);
            var root = AutomationElement.FromHandle(new IntPtr(long.Parse(args[0])));
            if (args[1] == "validate") return Validate(root);
            if (args[1] == "readonly") return ReadOnly(root);
            if (args[1].StartsWith("cell:", StringComparison.Ordinal)) return EditCell(root, args[1].Substring(5));
            var named = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, args[1]));
            if (named == null) throw new InvalidOperationException("Named hosted editor absent: " + args[1]);
            var edit = named.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            if (edit == null && named.Current.ControlType == ControlType.Edit) edit = named;
            if (edit == null) throw new InvalidOperationException("Native Edit descendant absent.");
            Console.WriteLine("WRAPPER=" + named.Current.ControlType.ProgrammaticName + "; NAME=" + named.Current.Name);
            Console.WriteLine("EDIT=" + edit.Current.ClassName + "; NAME=" + edit.Current.Name + "; HELP=" + edit.Current.HelpText);
            edit.SetFocus();
            if (!edit.Current.HasKeyboardFocus) throw new InvalidOperationException("Native Edit did not acquire focus.");
            var value = (ValuePattern)edit.GetCurrentPattern(ValuePattern.Pattern);
            if (value.Current.IsReadOnly) throw new InvalidOperationException("Native input unexpectedly readonly.");
            value.SetValue("6.25");
            if (value.Current.Value != "6.25") throw new InvalidOperationException("Native Value did not roundtrip.");
            if (!args[1].StartsWith("Product", StringComparison.Ordinal))
            {
                var handle = new IntPtr(edit.Current.NativeWindowHandle);
                if (!PostMessage(handle, 0x0100, new IntPtr(13), IntPtr.Zero) ||
                    !PostMessage(handle, 0x0101, new IntPtr(13), IntPtr.Zero))
                    throw new InvalidOperationException("Native Enter dispatch failed.");
            }
            Console.WriteLine("NATIVE_EDITOR_REACHABLE=PASS");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out NativeRectangle rectangle);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRectangle { public int Left, Top, Right, Bottom; }
    private static int Measure(IntPtr window, int expectedProcess, bool lookup = false)
    {
        if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) throw new InvalidOperationException("PerMonitorV2 observer unavailable.");
        uint process;
        GetWindowThreadProcessId(window, out process);
        if (process != expectedProcess) throw new InvalidOperationException("Observation target ownership mismatch.");
        uint x, y;
        if (GetDpiForMonitor(MonitorFromWindow(window, 2), 0, out x, out y) != 0 || x != y)
            throw new InvalidOperationException("Monitor DPI unavailable.");
        NativeRectangle client;
        if (!GetClientRect(window, out client)) throw new InvalidOperationException("Client measurement unavailable.");
        var root = AutomationElement.FromHandle(window);
        Console.WriteLine("OBSERVER_CONTEXT=PerMonitorV2;MONITOR_SCALE=" + (x * 100 / 96) + ";MONITOR_DPI=" + x +
            ";WINDOW_DPI=" + GetDpiForWindow(window) + ";PHYSICAL_WINDOW=" + root.Current.BoundingRectangle +
            ";NATIVE_CLIENT=" + (client.Right - client.Left) + "x" + (client.Bottom - client.Top));
        if (lookup)
        {
            var expected = Environment.GetEnvironmentVariable("MYDMSVN_BCF_EXPECTED_SCALE");
            if ((expected != "100" && expected != "125") || x * 100m / 96m != int.Parse(expected))
                throw new InvalidOperationException("Measured native monitor scale differs from declared BCF context.");
            Console.WriteLine("NATIVE_LOOKUP_ENVIRONMENT=PASS");
            return 0;
        }
        var quantity = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Quantity row 1"));
        Console.WriteLine("OBSERVED_QUANTITY_CELL=" + (quantity == null ? "absent" : quantity.Current.BoundingRectangle.ToString()));
        if (quantity == null || quantity.Current.BoundingRectangle.Height < 36 * x / 96d)
            throw new InvalidOperationException("Native receipt row is smaller than the scaled target.");
        Console.WriteLine("PHYSICAL_QUANTITY_CELL=" + quantity.Current.BoundingRectangle);
        Console.WriteLine("NATIVE_WINDOW_MEASUREMENT=PASS");
        return 0;
    }

    private static AutomationElement Named(AutomationElement root, string name)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(8))
        {
            var found = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
            if (found != null) return found;
            System.Threading.Thread.Sleep(25);
        }
        throw new InvalidOperationException("Semantic input absent: " + name);
    }
    private static void Key(AutomationElement edit, int key)
    {
        var handle = new IntPtr(edit.Current.NativeWindowHandle);
        if (handle == IntPtr.Zero || !PostMessage(handle, 0x0100, new IntPtr(key), IntPtr.Zero) ||
            !PostMessage(handle, 0x0101, new IntPtr(key), IntPtr.Zero)) throw new InvalidOperationException("Native key dispatch failed.");
        System.Threading.Thread.Sleep(100);
    }
    private static int EditCell(AutomationElement root, string name)
    {
        var cell = Named(root, name);
        foreach (var pattern in cell.GetSupportedPatterns()) Console.WriteLine("CELL_PATTERN=" + pattern.ProgrammaticName);
        var logical = (ValuePattern)cell.GetCurrentPattern(ValuePattern.Pattern);
        if (logical.Current.IsReadOnly) throw new InvalidOperationException("Editable cell reported read-only.");
        ((InvokePattern)cell.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        var wrapper = Named(root, name + " editor");
        var input = wrapper.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
        if (input == null) throw new InvalidOperationException("Actual native textbox is absent.");
        input.SetFocus();
        if (!input.Current.HasKeyboardFocus) throw new InvalidOperationException("Native editor has no focus.");
        var value = (ValuePattern)input.GetCurrentPattern(ValuePattern.Pattern);
        value.SetValue(name.StartsWith("Product", StringComparison.Ordinal) ? "P8" : "6.25");
        if (name.StartsWith("Product", StringComparison.Ordinal))
        {
            Key(input, 0x73);
            Key(input, 0x28);
        }
        Console.WriteLine("CELL=" + name + "; EDITOR=" + wrapper.Current.Name + "; NATIVE_CLASS=" + input.Current.ClassName);
        Key(input, 0x0d);
        Console.WriteLine("NATIVE_CELL_PIPELINE=PASS");
        return 0;
    }

    private static int Validate(AutomationElement root)
    {
        var cell = Named(root, "Quantity row 1");
        var logical = (ValuePattern)cell.GetCurrentPattern(ValuePattern.Pattern);
        logical.SetValue("7.25000");
        if (decimal.Parse(logical.Current.Value, System.Globalization.CultureInfo.CurrentCulture) != 7.25m) throw new InvalidOperationException("Accessible typed commit did not preserve exact padding.");
        var rejected = false;
        try { logical.SetValue("8.00001"); }
        catch (Exception) { rejected = true; }
        if (!rejected || decimal.Parse(logical.Current.Value, System.Globalization.CultureInfo.CurrentCulture) != 7.25m) throw new InvalidOperationException("Meaningful precision was accepted.");
        if (!cell.Current.HelpText.Contains("decimal places")) throw new InvalidOperationException("Accessible validation help absent.");
        var wrapper = Named(root, "Quantity row 1 editor");
        var input = wrapper.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
        if (((ValuePattern)input.GetCurrentPattern(ValuePattern.Pattern)).Current.Value != "8.00001")
            throw new InvalidOperationException("Rejected raw input was lost.");
        input.SetFocus();
        Key(input, 27);
        var product = Named(root, "Product row 1");
        ((InvokePattern)product.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        var lookup = Named(root, "Product row 1 editor");
        var native = lookup.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
        native.SetFocus();
        ((ValuePattern)native.GetCurrentPattern(ValuePattern.Pattern)).SetValue("pending query");
        if (!product.Current.HelpText.Contains("Pending selection") || !product.Current.HelpText.Contains("Committed key: 7"))
            throw new InvalidOperationException("Pending versus committed lookup help absent.");
        Key(native, 27);
        Key(native, 27);
        Console.WriteLine("NATIVE_VALUE_GUARDS=PASS");
        return 0;
    }
    private static int ReadOnly(AutomationElement root)
    {
        var cell = Named(root, "Quantity row 1");
        var value = (ValuePattern)cell.GetCurrentPattern(ValuePattern.Pattern);
        if (!value.Current.IsReadOnly) throw new InvalidOperationException("Posted cell was writable.");
        var rejected = false;
        try { value.SetValue("99"); }
        catch (Exception) { rejected = true; }
        if (!rejected || value.Current.Value != "2.5") throw new InvalidOperationException("Readonly accessible write was accepted.");
        Console.WriteLine("NATIVE_READONLY_GUARD=PASS");
        return 0;
    }

}
