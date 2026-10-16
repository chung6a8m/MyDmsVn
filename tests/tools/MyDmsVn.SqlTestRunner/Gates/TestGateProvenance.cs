using System;
using System.Diagnostics;
using System.IO;

namespace MyDmsVn.SqlTestRunner.Gates;

internal static class TestGateProvenance
{
    private const string ReviewedManifest = "tests/test-gates.v1.json";

    public static bool Verify(TestGateRunOptions options)
    {
        if (options.Mode != TestGateMode.Final || string.IsNullOrWhiteSpace(options.Revision))
        {
            return options.Mode != TestGateMode.Final;
        }

        try
        {
            var root = Path.GetFullPath(options.RepositoryRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var manifest = Path.GetFullPath(options.ManifestPath);
            var reviewedManifest = Path.GetFullPath(Path.Combine(
                root,
                ReviewedManifest.Replace('/', Path.DirectorySeparatorChar)));
            if (!string.Equals(manifest, reviewedManifest, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(reviewedManifest))
            {
                return false;
            }

            var trackedManifest = Git(root, "ls-files", "--error-unmatch", "--", ReviewedManifest);
            return VerifyCleanRevision(root, options.Revision) && trackedManifest.ExitCode == 0;
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is InvalidOperationException ||
            exception is System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    public static bool VerifyCleanRevision(string repositoryRoot, string revision)
    {
        if (string.IsNullOrWhiteSpace(revision)) return false;
        try
        {
            var root = Path.GetFullPath(repositoryRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var topLevel = Git(root, "rev-parse", "--show-toplevel");
            var head = Git(root, "rev-parse", "HEAD");
            var status = Git(root, "status", "--porcelain", "--untracked-files=all");
            return topLevel.ExitCode == 0 &&
                   string.Equals(Path.GetFullPath(topLevel.Output.Trim()), root, StringComparison.OrdinalIgnoreCase) &&
                   head.ExitCode == 0 &&
                   string.Equals(head.Output.Trim(), revision, StringComparison.OrdinalIgnoreCase) &&
                   status.ExitCode == 0 &&
                   string.IsNullOrWhiteSpace(status.Output);
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is InvalidOperationException ||
            exception is System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static GitResult Git(string repositoryRoot, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.ArgumentList.Add("-C");
        process.StartInfo.ArgumentList.Add(repositoryRoot);
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new GitResult(process.ExitCode, output);
    }

    private sealed record GitResult(int ExitCode, string Output);
}
