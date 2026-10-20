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
            var root = AutomationElement.FromHandle(new IntPtr(long.Parse(args[0])));
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
}
