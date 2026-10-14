using System;
using System.Collections.Generic;

namespace MyDmsVn.SqlTestRunner.Processes;

public sealed class CommandSpec
{
    public CommandSpec(
        string fileName,
        string arguments,
        string workingDirectory,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        FileName = fileName;
        Arguments = arguments;
        WorkingDirectory = workingDirectory;
        Timeout = timeout;
        Environment = environment ?? new Dictionary<string, string>();
    }

    public string FileName { get; }

    public string Arguments { get; }

    public string WorkingDirectory { get; }

    public TimeSpan Timeout { get; }

    public IReadOnlyDictionary<string, string> Environment { get; }
}
