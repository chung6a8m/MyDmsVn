using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Focused;

public sealed class FocusedSqlScriptTests
{
    [Fact]
    public void Focused_script_preserves_caller_owned_harness_environment()
    {
        var root = FindRepositoryRoot();
        var temporary = Path.Combine(
            Path.GetTempPath(),
            "MyDmsVn.FocusedScript",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var fakeDotnet = Path.Combine(temporary, "dotnet.cmd");
            var project = Path.Combine(temporary, "tests.csproj");
            var approval = Path.Combine(temporary, "approval.json");
            var artifacts = Path.Combine(temporary, "artifacts");
            File.WriteAllText(fakeDotnet, "@exit /b 0" + Environment.NewLine);
            File.WriteAllText(project, string.Empty);
            File.WriteAllText(approval, "{}");

            var script = Path.Combine(root, "tests", "tools", "Invoke-FocusedSqlIntegrationTests.ps1");
            var command =
                "$env:MYDMSVN_SQL_TEST_APPROVAL_FILE='caller-approval';" +
                "$env:MYDMSVN_SQL_TEST_RUN_ID='caller-run';" +
                "$env:MYDMSVN_SQL_TEST_JOURNAL_DIRECTORY='caller-journal';" +
                "$env:MYDMSVN_SQL_TEST_TIMINGS='caller-timings';" +
                "$env:MYDMSVN_SQL_TEST_TIMING_OUTPUT='caller-output';" +
                "& '" + Quote(script) + "'" +
                " -RepositoryRoot '" + Quote(root) + "'" +
                " -TestProject '" + Quote(project) + "'" +
                " -ArtifactRoot '" + Quote(artifacts) + "'" +
                " -ApprovalFile '" + Quote(approval) + "'" +
                " -Category FastSQL -Frameworks net8.0;" +
                "Write-Output ($env:MYDMSVN_SQL_TEST_APPROVAL_FILE + '|' +" +
                "$env:MYDMSVN_SQL_TEST_RUN_ID + '|' +" +
                "$env:MYDMSVN_SQL_TEST_JOURNAL_DIRECTORY + '|' +" +
                "$env:MYDMSVN_SQL_TEST_TIMINGS + '|' +" +
                "$env:MYDMSVN_SQL_TEST_TIMING_OUTPUT)";
            var start = new ProcessStartInfo(
                "powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"" + command.Replace("\"", "`\"") + "\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            start.Environment["PATH"] = temporary + Path.PathSeparator +
                (Environment.GetEnvironmentVariable("PATH") ?? string.Empty);

            using var process = Process.Start(start)!;
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.True(process.ExitCode == 0, standardError);
            Assert.Contains(
                "caller-approval|caller-run|caller-journal|caller-timings|caller-output",
                standardOutput,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(temporary, true);
        }
    }

    private static string Quote(string value) => value.Replace("'", "''");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MyDmsVn.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
