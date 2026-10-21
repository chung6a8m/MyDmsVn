using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using MyDmsVn.Bootstrap5WinFormUI.Controls;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

// Uses the same WM_CHAR/key primitives as InlineReceiptTestDriver; never assigns query Text.
internal sealed class NativeLookupTypingDriver
{
    private readonly BootstrapLookupBox _lookup;
    private readonly IntPtr _edit;
    internal NativeLookupTypingDriver(BootstrapLookupBox lookup)
    {
        _lookup = lookup;
        lookup.Select();
        lookup.Focus();
        System.Windows.Forms.Application.DoEvents();
        AssertFocused();
        _edit = FocusedHandle;
    }

    internal IntPtr FocusedHandle => GetFocus();
    internal string Text
    {
        get
        {
            AssertFocused();
            var buffer = new StringBuilder(GetWindowTextLength(FocusedHandle) + 1);
            GetWindowText(FocusedHandle, buffer, buffer.Capacity);
            Assert.Equal(_lookup.Text, buffer.ToString());
            return buffer.ToString();
        }
    }
    internal int SelectionStart => Selection().Start;
    internal int SelectionLength { get { var range = Selection(); return range.End - range.Start; } }
    internal void TypeCharacter(char value)
    {
        AssertFocused();
        SendMessage(FocusedHandle, 0x0102, new IntPtr(value), IntPtr.Zero);
        System.Windows.Forms.Application.DoEvents();
        AssertFocused();
    }
    internal void SelectRange(int start, int length)
    {
        AssertFocused();
        SendMessage(FocusedHandle, 0x00B1, new IntPtr(start), new IntPtr(start + length));
        Assert.Equal(start, SelectionStart);
        Assert.Equal(length, SelectionLength);
    }
    internal void Key(Keys key)
    {
        AssertFocused();
        GoodsReceiptInlineEditorTests.Key(key);
    }
    internal void AssertFocused()
    {
        var handle = FocusedHandle;
        if (_edit != IntPtr.Zero) Assert.Equal(_edit, handle);
        var editor = Control.FromHandle(handle);
        var className = new StringBuilder(256);
        GetClassName(handle, className, className.Capacity);
        Assert.True(handle != IntPtr.Zero && editor is TextBox && _lookup.Contains(editor) &&
            className.ToString().IndexOf("EDIT", StringComparison.OrdinalIgnoreCase) >= 0,
            "Native focus precondition: focused EDIT must belong to " + _lookup.AccessibleName + "; class=" + className);
        Assert.Equal(GetCurrentThreadId(), GetWindowThreadProcessId(handle, out var process));
        Assert.Equal(GetCurrentProcessId(), process);
        Assert.True(_lookup.ContainsFocus);
    }
    private (int Start, int End) Selection()
    {
        AssertFocused();
        SendMessageSelection(FocusedHandle, 0x00B0, out var start, out var end);
        return (start, end);
    }
    [DllImport("user32.dll")] private static extern IntPtr GetFocus();
    [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "SendMessageW")] private static extern IntPtr SendMessageSelection(IntPtr window, uint message, out int start, out int end);
}
