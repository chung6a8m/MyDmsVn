using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace MyDmsVn.Desktop.Tests
{
    internal static class GuiOwnedProcess
    {
        public const int OutputLimit = 32768;
        private static readonly TimeSpan CleanupBudget = TimeSpan.FromSeconds(5);

        public static async Task<GuiProcessResult> RunAsync(ProcessStartInfo start, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            GuiTestSafety.ThrowIfPoisoned();
            cancellationToken.ThrowIfCancellationRequested();
            if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(5))
                throw new ArgumentOutOfRangeException(nameof(timeout));
            if (!Path.IsPathRooted(start.FileName) || !File.Exists(start.FileName) || start.UseShellExecute)
                throw new ArgumentException("GUI child requires an existing absolute executable and UseShellExecute=false.", nameof(start));

            var clock = Stopwatch.StartNew();
            using (var child = NativeChild.Start(start))
            {
                var outputCapture = new GuiOutputCapture();
                var errorCapture = new GuiOutputCapture();
                var output = outputCapture.DrainAsync(child.Output);
                var error = errorCapture.DrainAsync(child.Error);
                Exception? initial = null;
                string? reason = null;
                int exitCode = -1;
                try
                {
                    while (!child.RootExited)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            reason = "Canceled";
                            initial = new OperationCanceledException(cancellationToken);
                            break;
                        }
                        if (clock.Elapsed >= timeout)
                        {
                            reason = "TimedOut";
                            initial = new TimeoutException("Owned GUI child exceeded its startup/execution budget.");
                            break;
                        }
                        await Task.Delay(20).ConfigureAwait(false);
                    }
                    if (reason == null)
                    {
                        exitCode = child.ExitCode;
                        if (child.ActiveProcesses != 0)
                        {
                            reason = "DescendantsOutlivedParent";
                            initial = new InvalidOperationException("Owned descendants remained after the root exited.");
                        }
                    }
                }
                catch (Exception exception)
                {
                    reason = "ObservationFailed";
                    initial = exception;
                }

                Exception? cleanup = null;
                var cleanupClock = Stopwatch.StartNew();
                try
                {
                    child.Terminate();
                    while (child.ActiveProcesses != 0 || !child.RootExited)
                    {
                        if (cleanupClock.Elapsed >= CleanupBudget)
                            throw new TimeoutException("Owned GUI process tree did not drain within the cleanup budget.");
                        await Task.Delay(20).ConfigureAwait(false);
                    }
                    var drains = Task.WhenAll(output, error);
                    var remaining = CleanupBudget - cleanupClock.Elapsed;
                    if (remaining <= TimeSpan.Zero || await Task.WhenAny(drains, Task.Delay(remaining)).ConfigureAwait(false) != drains)
                        throw new TimeoutException("Owned stdout/stderr did not reach EOF after process-tree termination.");
                    await drains.ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    cleanup = exception;
                    GuiTestSafety.Poison(exception);
                }

                var stdout = outputCapture.Snapshot();
                var stderr = errorCapture.Snapshot();
                if (initial != null || cleanup != null)
                {
                    var cause = initial == null ? cleanup! : cleanup == null ? initial : new AggregateException(initial, cleanup);
                    throw new GuiProcessException(reason ?? "CleanupFailed", cleanup == null, stdout, stderr, exitCode, cause);
                }
                return new GuiProcessResult(exitCode, stdout, stderr);
            }
        }

        // The root starts suspended: no descendant can run before job assignment.
        // A handle allowlist prevents inheriting unrelated testhost handles/credentials.
        private sealed class NativeChild : IDisposable
        {
            private readonly KernelHandle _job;
            private readonly KernelHandle _process;
            public StreamReader Output { get; }
            public StreamReader Error { get; }

            private NativeChild(KernelHandle job, KernelHandle process, StreamReader output, StreamReader error)
            {
                _job = job;
                _process = process;
                Output = output;
                Error = error;
            }

            public bool RootExited
            {
                get
                {
                    var result = WaitForSingleObject(_process, 0);
                    if (result == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
                    return result == 0;
                }
            }

            public int ExitCode
            {
                get { Check(GetExitCodeProcess(_process, out var code)); return unchecked((int)code); }
            }

            public uint ActiveProcesses
            {
                get
                {
                    Check(QueryInformationJobObject(_job, 1, out var accounting, Marshal.SizeOf<JobAccounting>(), IntPtr.Zero));
                    return accounting.ActiveProcesses;
                }
            }

            public void Terminate() => Check(TerminateJobObject(_job, 0xe001));

            public static NativeChild Start(ProcessStartInfo start)
            {
                var job = CreateJobObject(IntPtr.Zero, null);
                KernelHandle? process = null;
                StreamReader? output = null;
                StreamReader? error = null;
                bool assigned = false;
                try
                {
                    Check(!job.IsInvalid);
                    var limits = new JobExtendedLimits();
                    limits.BasicLimitInformation.LimitFlags = 0x2000; // KILL_ON_JOB_CLOSE; no breakaway.
                    Check(SetInformationJobObject(job, 9, ref limits, Marshal.SizeOf<JobExtendedLimits>()));
                    var security = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), InheritHandle = true };
                    Check(CreatePipe(out var outputRead, out var outputWrite, ref security, 0));
                    using (outputWrite)
                    {
                        output = Reader(outputRead);
                        Check(CreatePipe(out var errorRead, out var errorWrite, ref security, 0));
                        using (errorWrite)
                        using (var input = CreateFile("NUL", 0x80000000, 3, ref security, 3, 0, IntPtr.Zero))
                        {
                            Check(!input.IsInvalid);
                            error = Reader(errorRead);
                            using (var attributes = new HandleAttributes(input, outputWrite, errorWrite))
                            {
                                var startup = new StartupInfoEx();
                                startup.StartupInfo.Size = Marshal.SizeOf<StartupInfoEx>();
                                startup.StartupInfo.Flags = 0x100; // STARTF_USESTDHANDLES
                                startup.StartupInfo.Input = input.DangerousGetHandle();
                                startup.StartupInfo.Output = outputWrite.DangerousGetHandle();
                                startup.StartupInfo.Error = errorWrite.DangerousGetHandle();
                                startup.AttributeList = attributes.Pointer;
                                var environment = string.Join("\0", start.EnvironmentVariables.Cast<DictionaryEntry>()
                                    .Select(item => item.Key + "=" + item.Value).OrderBy(item => item, StringComparer.OrdinalIgnoreCase)) + "\0\0";
                                var environmentPointer = Marshal.StringToHGlobalUni(environment);
                                try
                                {
                                    Check(CreateProcess(start.FileName, new StringBuilder("\"" + start.FileName + "\" " + start.Arguments),
                                        IntPtr.Zero, IntPtr.Zero, true, 0x00080000 | 0x08000000 | 0x00000400 | 0x00000004,
                                        environmentPointer, start.WorkingDirectory, ref startup, out var created));
                                    process = new KernelHandle(created.Process);
                                    using (var thread = new KernelHandle(created.Thread))
                                    {
                                        Check(AssignProcessToJobObject(job, process));
                                        assigned = true;
                                        if (ResumeThread(thread) == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
                                    }
                                }
                                finally { Marshal.FreeHGlobal(environmentPointer); }
                            }
                        }
                    }
                    return new NativeChild(job, process!, output, error!);
                }
                catch (Exception initial)
                {
                    try
                    {
                        if (process != null)
                        {
                            Check(assigned ? TerminateJobObject(job, 0xe001) : TerminateProcess(process, 0xe001));
                            if (WaitForSingleObject(process, 5000) != 0) throw new TimeoutException("Failed launch did not drain.");
                        }
                    }
                    catch (Exception cleanup)
                    {
                        GuiTestSafety.Poison(cleanup);
                        throw new AggregateException("GUI launch and cleanup both failed.", initial, cleanup);
                    }
                    finally
                    {
                        process?.Dispose();
                        job.Dispose();
                        output?.Dispose();
                        error?.Dispose();
                    }
                    throw;
                }
            }

            private static StreamReader Reader(SafeFileHandle handle)
            {
                try
                {
                    Check(SetHandleInformation(handle, 1, 0));
                    return new StreamReader(new FileStream(handle, FileAccess.Read, 4096, false), Encoding.UTF8);
                }
                catch { handle.Dispose(); throw; }
            }

            public void Dispose()
            {
                _job.Dispose();
                _process.Dispose();
                Output.Dispose();
                Error.Dispose();
            }
        }

        private sealed class HandleAttributes : IDisposable
        {
            public IntPtr Pointer { get; }
            private readonly IntPtr _handles;

            public HandleAttributes(params SafeFileHandle[] handles)
            {
                IntPtr size = IntPtr.Zero;
                InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
                Pointer = Marshal.AllocHGlobal(size);
                _handles = Marshal.AllocHGlobal(IntPtr.Size * handles.Length);
                bool initialized = false;
                try
                {
                    Check(InitializeProcThreadAttributeList(Pointer, 1, 0, ref size));
                    initialized = true;
                    for (int index = 0; index < handles.Length; index++)
                        Marshal.WriteIntPtr(_handles, index * IntPtr.Size, handles[index].DangerousGetHandle());
                    Check(UpdateProcThreadAttribute(Pointer, 0, new IntPtr(0x20002), _handles,
                        new IntPtr(IntPtr.Size * handles.Length), IntPtr.Zero, IntPtr.Zero));
                }
                catch
                {
                    if (initialized) DeleteProcThreadAttributeList(Pointer);
                    Marshal.FreeHGlobal(Pointer);
                    Marshal.FreeHGlobal(_handles);
                    throw;
                }
            }

            public void Dispose()
            {
                DeleteProcThreadAttributeList(Pointer);
                Marshal.FreeHGlobal(Pointer);
                Marshal.FreeHGlobal(_handles);
            }
        }

        private sealed class KernelHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public KernelHandle() : base(true) { }
            public KernelHandle(IntPtr handle) : base(true) => SetHandle(handle);
            protected override bool ReleaseHandle() => CloseHandle(handle);
        }

        private static void Check(bool success)
        {
            if (!success) throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int Length; public IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] public bool InheritHandle; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfo
        {
            public int Size; public IntPtr Reserved; public IntPtr Desktop; public IntPtr Title;
            public int X; public int Y; public int XSize; public int YSize; public int XCountChars; public int YCountChars;
            public int FillAttribute; public int Flags; public short ShowWindow; public short ReservedSize;
            public IntPtr ReservedBytes; public IntPtr Input; public IntPtr Output; public IntPtr Error;
        }
        [StructLayout(LayoutKind.Sequential)] private struct StartupInfoEx { public StartupInfo StartupInfo; public IntPtr AttributeList; }
        [StructLayout(LayoutKind.Sequential)] private struct ProcessInformation { public IntPtr Process; public IntPtr Thread; public int ProcessId; public int ThreadId; }
        [StructLayout(LayoutKind.Sequential)] private struct JobBasicLimits
        {
            public long ProcessTimeLimit; public long JobTimeLimit; public uint LimitFlags;
            public UIntPtr MinimumWorkingSet; public UIntPtr MaximumWorkingSet; public uint ActiveProcessLimit;
            public UIntPtr Affinity; public uint PriorityClass; public uint SchedulingClass;
        }
        [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong ReadOperations; public ulong WriteOperations; public ulong OtherOperations; public ulong ReadBytes; public ulong WriteBytes; public ulong OtherBytes; }
        [StructLayout(LayoutKind.Sequential)] private struct JobExtendedLimits { public JobBasicLimits BasicLimitInformation; public IoCounters Io; public UIntPtr ProcessMemory; public UIntPtr JobMemory; public UIntPtr PeakProcessMemory; public UIntPtr PeakJobMemory; }
        [StructLayout(LayoutKind.Sequential)] private struct JobAccounting { public long UserTime; public long KernelTime; public long PeriodUserTime; public long PeriodKernelTime; public uint PageFaults; public uint TotalProcesses; public uint ActiveProcesses; public uint TerminatedProcesses; }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern KernelHandle CreateJobObject(IntPtr security, string? name);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(KernelHandle job, int type, ref JobExtendedLimits information, int length);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool QueryInformationJobObject(KernelHandle job, int type, out JobAccounting information, int length, IntPtr returned);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(KernelHandle job, KernelHandle process);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateJobObject(KernelHandle job, uint code);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(KernelHandle process, uint code);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(KernelHandle process, out uint code);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(KernelHandle handle, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(KernelHandle thread);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CreatePipe(out SafeFileHandle read, out SafeFileHandle write, ref SecurityAttributes security, int size);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern SafeFileHandle CreateFile(string path, uint access, uint sharing, ref SecurityAttributes security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool CreateProcess(string application, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string directory, ref StartupInfoEx startup, out ProcessInformation information);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returned);
        [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr list);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    }

    internal sealed class GuiOutputCapture
    {
        private readonly StringBuilder _output = new StringBuilder();
        private readonly object _sync = new object();
        public async Task DrainAsync(TextReader reader)
        {
            var buffer = new char[4096];
            int count;
            while ((count = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
            {
                lock (_sync)
                {
                    var keep = Math.Min(count, GuiOwnedProcess.OutputLimit - _output.Length);
                    if (keep > 0) _output.Append(buffer, 0, keep);
                }
            }
        }
        public string Snapshot() { lock (_sync) return _output.ToString(); }
    }

    internal sealed class GuiProcessResult
    {
        public GuiProcessResult(int exitCode, string output, string error) { ExitCode = exitCode; StandardOutput = output; StandardError = error; }
        public int ExitCode { get; }
        public string StandardOutput { get; }
        public string StandardError { get; }
        public bool CleanupVerified => true;
    }

    internal sealed class GuiProcessException : Exception
    {
        public GuiProcessException(string reason, bool cleanupVerified, string output, string error, int exitCode, Exception cause)
            : base("Owned GUI child failed: " + reason + "; exit code=" + exitCode + "; cleanup verified=" + cleanupVerified + Environment.NewLine +
                "stdout: " + output + Environment.NewLine + "stderr: " + error, cause)
        { Reason = reason; CleanupVerified = cleanupVerified; StandardOutput = output; StandardError = error; }
        public string Reason { get; }
        public string StandardOutput { get; }
        public string StandardError { get; }
        public bool CleanupVerified { get; }
    }

    internal static class GuiTestSafety
    {
        private static Exception? _failure;
        public static bool IsPoisoned => Volatile.Read(ref _failure) != null;
        public static void Poison(Exception failure) => Interlocked.CompareExchange(ref _failure, failure, null);
        public static void ThrowIfPoisoned()
        {
            var failure = Volatile.Read(ref _failure);
            if (failure != null) throw new InvalidOperationException("GUI test session has uncertain cleanup. Stop this test run and review owned resources.", failure);
        }
    }
}
