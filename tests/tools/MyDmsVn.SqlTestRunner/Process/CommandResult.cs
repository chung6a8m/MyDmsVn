using System.Collections.Generic;

namespace MyDmsVn.SqlTestRunner.Processes;

public sealed record CommandResult(
    int ExitCode,
    bool TimedOut,
    int RootProcessId,
    string StandardOutput,
    string StandardError,
    IReadOnlyList<int> TerminatedProcessIds);
