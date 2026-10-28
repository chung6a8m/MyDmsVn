using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using MyDmsVn.Desktop.Tests;

namespace MyDmsVn.Desktop.Ui.Tests;

internal sealed class NativeMessageBoxObserver : IDisposable
{
    private const uint GwOwner = 4;
    private const uint WmClose = 0x0010;
    private static readonly TimeSpan AppearanceTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(2);
    private readonly uint _threadId = GetCurrentThreadId();
    private readonly uint _processId = GetCurrentProcessId();
    private readonly HashSet<IntPtr> _existingWindows;
    private readonly IntPtr _expectedOwner;
    private readonly string _expectedTitle;
    private readonly Action<IntPtr>? _onDiscovered;
    private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer { Interval = 30 };
    private readonly Stopwatch _watch = new Stopwatch();
    private ExceptionDispatchInfo? _failure;
    private IntPtr _dialog;
    private bool _completed;
    private bool _handling;
    private bool _invoked;
    private bool _disposed;

    internal NativeMessageBoxObserver(IntPtr expectedOwner, string expectedTitle, Action<IntPtr>? onDiscovered = null)
    {
        if (expectedOwner == IntPtr.Zero) throw new ArgumentException("Expected owner must have a native handle.", nameof(expectedOwner));
        _expectedOwner = expectedOwner;
        _expectedTitle = expectedTitle ?? throw new ArgumentNullException(nameof(expectedTitle));
        _onDiscovered = onDiscovered;
        _existingWindows = EnumerateWindows().ToHashSet();
        _timer.Tick += OnTick;
    }

    internal bool Observed => _dialog != IntPtr.Zero;
    internal bool DialogStillOpen => _dialog != IntPtr.Zero && IsWindow(_dialog);
    internal IntPtr DialogHandle => _dialog;
    internal IntPtr OwnerHandle { get; private set; }
    internal IntPtr FocusedHandle { get; private set; }
    internal string Title { get; private set; } = string.Empty;
    internal string FocusedText { get; private set; } = string.Empty;

    internal void Invoke(Action command)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));
        if (_disposed) throw new ObjectDisposedException(nameof(NativeMessageBoxObserver));
        if (_invoked) throw new InvalidOperationException("A native MessageBox observer can be invoked only once.");
        _invoked = true;

        ExceptionDispatchInfo? commandFailure = null;
        _watch.Start();
        _timer.Start();
        try { command(); }
        catch (Exception error) { commandFailure = ExceptionDispatchInfo.Capture(error); }
        finally { _timer.Stop(); }

        if (!_completed && _failure == null)
            _failure = ExceptionDispatchInfo.Capture(new InvalidOperationException(
                "Close command returned before the native MessageBox observation completed."));

        var cleanupFailure = EnsureCapturedDialogClosed();
        if (cleanupFailure != null)
        {
            var causes = new List<Exception>();
            if (_failure != null) causes.Add(_failure.SourceException);
            if (commandFailure != null) causes.Add(commandFailure.SourceException);
            causes.Add(cleanupFailure);
            throw new AggregateException("Native MessageBox observation cleanup failed.", causes);
        }
        _failure?.Throw();
        commandFailure?.Throw();
    }

    private void OnTick(object? sender, EventArgs args)
    {
        if (_handling || _completed) return;
        _handling = true;
        try
        {
            if (_dialog == IntPtr.Zero)
            {
                var candidates = EnumerateWindows()
                    .Where(window => !_existingWindows.Contains(window) && IsWindowVisible(window))
                    .ToArray();
                _dialog = candidates.FirstOrDefault(window => WindowText(window, className: true) == "#32770");
                if (_dialog == IntPtr.Zero && _watch.Elapsed >= AppearanceTimeout)
                    _dialog = candidates.FirstOrDefault();
                if (_dialog == IntPtr.Zero)
                {
                    if (_watch.Elapsed < AppearanceTimeout) return;
                    CompleteFailure(new TimeoutException(
                        "No new native dialog appeared on the testcase UI thread within 5 seconds."));
                    return;
                }
            }

            try
            {
                _onDiscovered?.Invoke(_dialog);
                ValidateCapturedDialog();
                NativeLookupTypingDriver.KeyOnFocusedOwnedWindow(Keys.Escape);
                _completed = true;
                _timer.Stop();
            }
            catch (Exception error)
            {
                CompleteFailure(error);
                RequestCapturedDialogClose();
            }
        }
        finally { _handling = false; }
    }

    private void ValidateCapturedDialog()
    {
        var className = WindowText(_dialog, className: true);
        if (className != "#32770")
            throw new InvalidOperationException(
                "Native Close prompt class mismatch. Expected=#32770; actual=" + className + ".");

        Title = WindowText(_dialog, className: false);
        if (!string.Equals(Title, _expectedTitle, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Native Close prompt title mismatch. Expected='" + _expectedTitle + "'; actual='" + Title + "'.");

        OwnerHandle = GetWindow(_dialog, GwOwner);
        if (OwnerHandle != _expectedOwner)
            throw new InvalidOperationException(
                "Native Close prompt owner mismatch. Expected=" + _expectedOwner + "; actual=" + OwnerHandle + ".");

        var foreground = GetForegroundWindow();
        if (foreground != _dialog)
            throw new InvalidOperationException(
                "Native Close prompt foreground mismatch. Expected=" + _dialog + "; actual=" + foreground + ".");

        FocusedHandle = GetFocus();
        if (FocusedHandle == IntPtr.Zero || !IsChild(_dialog, FocusedHandle))
            throw new InvalidOperationException(
                "Native Close prompt focus mismatch. Dialog=" + _dialog + "; actual=" + FocusedHandle + ".");
        FocusedText = WindowText(FocusedHandle, className: false);
    }

    private void CompleteFailure(Exception error)
    {
        _failure = ExceptionDispatchInfo.Capture(error);
        _completed = true;
        _timer.Stop();
    }

    private void RequestCapturedDialogClose()
    {
        if (_dialog != IntPtr.Zero && IsWindow(_dialog) && !PostMessage(_dialog, WmClose, IntPtr.Zero, IntPtr.Zero))
            _failure = ExceptionDispatchInfo.Capture(new InvalidOperationException(
                "Native Close prompt cleanup could not post WM_CLOSE.", _failure?.SourceException));
    }

    private Exception? EnsureCapturedDialogClosed()
    {
        if (_dialog == IntPtr.Zero || !IsWindow(_dialog)) return null;
        RequestCapturedDialogClose();
        var cleanup = Stopwatch.StartNew();
        while (IsWindow(_dialog) && cleanup.Elapsed < CleanupTimeout)
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }
        return IsWindow(_dialog)
            ? new TimeoutException("Captured native Close prompt did not close within 2 seconds.")
            : null;
    }

    private IEnumerable<IntPtr> EnumerateWindows()
    {
        var windows = new List<IntPtr>();
        EnumThreadWindows(_threadId, (window, _) =>
        {
            GetWindowThreadProcessId(window, out var processId);
            if (processId == _processId) windows.Add(window);
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer.Dispose();
        EnsureCapturedDialogClosed();
    }

    private delegate bool EnumThreadWindowCallback(IntPtr window, IntPtr parameter);

    [DllImport("kernel32.dll")] private static extern uint GetCurrentProcessId();
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumThreadWindows(uint threadId, EnumThreadWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern IntPtr GetFocus();
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsChild(IntPtr parent, IntPtr child);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);

    private static string WindowText(IntPtr window, bool className)
    {
        var text = new StringBuilder(256);
        if (className) GetClassName(window, text, text.Capacity);
        else GetWindowText(window, text, text.Capacity);
        return text.ToString();
    }
}
