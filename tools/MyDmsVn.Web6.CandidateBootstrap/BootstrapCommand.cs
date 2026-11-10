using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal enum BootstrapExitCode
{
    Success = 0,
    Usage = 2,
    RefusedAuthority = 3,
    BlockedPreflight = 4,
    FailedRetainedRun = 5,
}

internal static class BootstrapCommand
{
    private const string Acknowledgement = "I AUTHORIZE WEB6 NONPRODUCTION DESTRUCTIVE DRILLS";

    internal static Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken token) =>
        Task.FromResult((int)Run(args, stdout, stderr, token));

    private static BootstrapExitCode Run(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken token)
    {
        if (args.Length == 1 && args[0] == "--help")
        {
            stdout.WriteLine("MyDmsVn.Web6.CandidateBootstrap");
            stdout.WriteLine("  authorize --input <strict-json> --output <new-file> --acknowledge <exact-phrase>");
            stdout.WriteLine("  prepare --request <strict-json> --authority <path> --authority-sha256 <64-lowercase-hex>");
            stdout.WriteLine("  run --prepared <path> --prepared-sha256 <64-lowercase-hex>");
            stdout.WriteLine("  Exact acknowledgement: " + Acknowledgement);
            return BootstrapExitCode.Success;
        }

        if (!TryReadOptions(args, out var options))
        {
            stderr.WriteLine("status=USAGE");
            return BootstrapExitCode.Usage;
        }

        try
        {
            token.ThrowIfCancellationRequested();
            switch (args[0])
            {
                case "authorize":
                    if (!string.Equals(options["--acknowledge"], Acknowledgement, StringComparison.Ordinal))
                        throw new IOException("Owner acknowledgement refused.");
                    var authorityDigest = BootstrapAuthority.Authorize(options["--input"], options["--output"], token);
                    stdout.WriteLine("status=AUTHORIZED sha256=" + authorityDigest);
                    return BootstrapExitCode.Success;
                case "prepare":
                    using (var authority = BootstrapAuthority.ReadProtected(
                        options["--authority"], options["--authority-sha256"], DateTimeOffset.UtcNow))
                    {
                        BootstrapRequest.ReadStrict(options["--request"], authority);
                        authority.Verify(DateTimeOffset.UtcNow);
                        token.ThrowIfCancellationRequested();
                    }
                    break;
                case "run":
                    BootstrapJson.AbsolutePath(options["--prepared"]);
                    break;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or SecurityException)
        {
            stderr.WriteLine("status=REFUSED_AUTHORITY");
            return BootstrapExitCode.RefusedAuthority;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            stderr.WriteLine("status=BLOCKED_PREFLIGHT");
            return BootstrapExitCode.BlockedPreflight;
        }

        // Preparation and selector execution are added by the later bootstrap tasks.
        stderr.WriteLine("status=BLOCKED_PREFLIGHT");
        return BootstrapExitCode.BlockedPreflight;
    }

    private static bool TryReadOptions(string[] args, out Dictionary<string, string> options)
    {
        options = new Dictionary<string, string>(StringComparer.Ordinal);
        var expected = (args.Length == 0 ? "" : args[0]) switch
        {
            "authorize" => new[] { "--input", "--output", "--acknowledge" },
            "prepare" => new[] { "--request", "--authority", "--authority-sha256" },
            "run" => new[] { "--prepared", "--prepared-sha256" },
            _ => Array.Empty<string>(),
        };
        if (expected.Length == 0 || args.Length != 1 + expected.Length * 2)
            return false;

        for (var i = 1; i < args.Length; i += 2)
        {
            var option = args[i];
            var value = args[i + 1];
            if (!expected.Contains(option) || options.ContainsKey(option) || string.IsNullOrWhiteSpace(value) ||
                value.StartsWith("--", StringComparison.Ordinal) ||
                ((option == "--authority-sha256" || option == "--prepared-sha256") && !BootstrapJson.IsLowerHex(value, 64)))
                return false;
            options.Add(option, value);
        }
        return true;
    }
}
