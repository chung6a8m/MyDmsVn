using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MyDmsVn.SqlTestRunner.Processes;

internal sealed class SuspendedOwnedProcess : IDisposable
{
    private const uint CreateSuspended = 0x00000004;
    private const uint CreateNoWindow = 0x08000000;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint StartfUseStdHandles = 0x00000100;
    private const uint WaitTimeoutMilliseconds = 5000;
    private const int StdInputHandle = -10;

    private SuspendedOwnedProcess(
        System.Diagnostics.Process process,
        AnonymousPipeServerStream stdoutPipe,
        AnonymousPipeServerStream stderrPipe,
        StreamReader standardOutput,
        StreamReader standardError,
        IntPtr processHandle)
    {
        Process = process;
        _stdoutPipe = stdoutPipe;
        _stderrPipe = stderrPipe;
        StandardOutput = standardOutput;
        StandardError = standardError;
        _processHandle = processHandle;
    }

    private readonly AnonymousPipeServerStream _stdoutPipe;
    private readonly AnonymousPipeServerStream _stderrPipe;
    private IntPtr _processHandle;

    public System.Diagnostics.Process Process { get; }

    public StreamReader StandardOutput { get; }

    public StreamReader StandardError { get; }

    public int ExitCode
    {
        get
        {
            if (_processHandle == IntPtr.Zero || !GetExitCodeProcess(_processHandle, out var exitCode))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            return unchecked((int)exitCode);
        }
    }

    public static SuspendedOwnedProcess Start(CommandSpec command, WindowsJobObject job) =>
        Start(command, (_, processHandle) => job.Assign(processHandle));

    internal static SuspendedOwnedProcess Start(
        CommandSpec command,
        Action<int, IntPtr> assignToOwner)
    {
        var stdout = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        var stderr = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        var startup = new StartupInfo
        {
            Size = Marshal.SizeOf<StartupInfo>(),
            Flags = StartfUseStdHandles,
            StandardInput = GetStdHandle(StdInputHandle),
            StandardOutput = new IntPtr(long.Parse(stdout.GetClientHandleAsString())),
            StandardError = new IntPtr(long.Parse(stderr.GetClientHandleAsString())),
        };
        var commandLine = new StringBuilder("\"" + command.FileName + "\" " + command.Arguments);
        var environment = BuildEnvironment(command.Environment);
        var environmentPointer = Marshal.StringToHGlobalUni(environment);
        ProcessInformation information = default;
        try
        {
            var created = CreateProcess(
                null,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                inheritHandles: true,
                CreateSuspended | CreateNoWindow | CreateUnicodeEnvironment,
                environmentPointer,
                command.WorkingDirectory,
                ref startup,
                out information);
            if (!created)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            stdout.DisposeLocalCopyOfClientHandle();
            stderr.DisposeLocalCopyOfClientHandle();
            assignToOwner((int)information.ProcessId, information.ProcessHandle);
            if (ResumeThread(information.ThreadHandle) == uint.MaxValue)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }

            var process = System.Diagnostics.Process.GetProcessById((int)information.ProcessId);
            var owned = new SuspendedOwnedProcess(
                process,
                stdout,
                stderr,
                new StreamReader(stdout, Encoding.UTF8, true, 4096, leaveOpen: true),
                new StreamReader(stderr, Encoding.UTF8, true, 4096, leaveOpen: true),
                information.ProcessHandle);
            information.ProcessHandle = IntPtr.Zero;
            return owned;
        }
        catch
        {
            if (information.ProcessHandle != IntPtr.Zero)
            {
                TerminateProcess(information.ProcessHandle, 1);
                WaitForSingleObject(information.ProcessHandle, WaitTimeoutMilliseconds);
            }

            stdout.Dispose();
            stderr.Dispose();
            throw;
        }
        finally
        {
            Marshal.FreeHGlobal(environmentPointer);
            if (information.ThreadHandle != IntPtr.Zero)
            {
                CloseHandle(information.ThreadHandle);
            }

            if (information.ProcessHandle != IntPtr.Zero)
            {
                CloseHandle(information.ProcessHandle);
            }
        }
    }

    public void Dispose()
    {
        StandardOutput.Dispose();
        StandardError.Dispose();
        _stdoutPipe.Dispose();
        _stderrPipe.Dispose();
        Process.Dispose();
        if (_processHandle != IntPtr.Zero)
        {
            CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
        }
    }

    private static string BuildEnvironment(IReadOnlyDictionary<string, string> overrides)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry pair in Environment.GetEnvironmentVariables())
        {
            values[(string)pair.Key] = (string?)pair.Value ?? string.Empty;
        }

        foreach (var pair in overrides)
        {
            values[pair.Key] = pair.Value;
        }

        return string.Join("\0", values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key + "=" + pair.Value)) + "\0\0";
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(
        string? applicationName,
        StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(IntPtr threadHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr processHandle, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr processHandle, out uint exitCode);

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Size;
        public string? Reserved;
        public string? Desktop;
        public string? Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public uint Flags;
        public short ShowWindow;
        public short Reserved2Count;
        public IntPtr Reserved2;
        public IntPtr StandardInput;
        public IntPtr StandardOutput;
        public IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr ProcessHandle;
        public IntPtr ThreadHandle;
        public uint ProcessId;
        public uint ThreadId;
    }
}
