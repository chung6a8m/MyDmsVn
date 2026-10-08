using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MyDmsVn.Desktop.Tests
{
    internal sealed class WinFormsTestGuard : IDisposable
    {
        private const uint FailCriticalErrors = 0x0001;
        private const uint NoGeneralProtectionFaultErrorBox = 0x0002;

        private readonly List<Exception> _exceptions = new List<Exception>();
        private readonly List<DataGridView> _grids = new List<DataGridView>();
        private readonly uint _previousThreadErrorMode;
        private bool _disposed;

        public WinFormsTestGuard()
        {
            var requestedErrorMode =
                GetThreadErrorMode() | FailCriticalErrors | NoGeneralProtectionFaultErrorBox;
            if (!SetThreadErrorMode(requestedErrorMode, out _previousThreadErrorMode))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            System.Windows.Forms.Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        public IReadOnlyList<Exception> Exceptions => _exceptions;

        public bool FatalErrorDialogsSuppressed =>
            (GetThreadErrorMode() & (FailCriticalErrors | NoGeneralProtectionFaultErrorBox)) ==
            (FailCriticalErrors | NoGeneralProtectionFaultErrorBox);

        public IReadOnlyList<Exception> DrainExceptions()
        {
            var exceptions = _exceptions.ToArray();
            _exceptions.Clear();
            return exceptions;
        }

        public void Attach(DataGridView grid)
        {
            grid.DataError += OnDataError;
            _grids.Add(grid);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (var grid in _grids)
            {
                grid.DataError -= OnDataError;
            }

            System.Windows.Forms.Application.ThreadException -= OnThreadException;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            if (!SetThreadErrorMode(_previousThreadErrorMode, out _))
            {
                _exceptions.Add(new Win32Exception(Marshal.GetLastWin32Error()));
            }

            _disposed = true;

            if (_exceptions.Count > 0)
            {
                throw new AggregateException(
                    "Unexpected WinForms test exceptions were captured.",
                    _exceptions);
            }
        }

        private void OnThreadException(object? sender, ThreadExceptionEventArgs eventArgs)
        {
            _exceptions.Add(eventArgs.Exception);
        }

        private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs eventArgs)
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                _exceptions.Add(exception);
            }
        }

        private void OnDataError(object? sender, DataGridViewDataErrorEventArgs eventArgs)
        {
            if (eventArgs.Exception != null)
            {
                _exceptions.Add(eventArgs.Exception);
            }

            eventArgs.Cancel = true;
            eventArgs.ThrowException = false;
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetThreadErrorMode();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetThreadErrorMode(uint newMode, out uint oldMode);
    }
}
