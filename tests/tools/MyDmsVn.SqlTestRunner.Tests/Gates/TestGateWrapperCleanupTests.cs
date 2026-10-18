using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Gates;

public sealed class TestGateWrapperCleanupTests
{
    [Fact]
    public async Task Post_setup_wrapper_failure_restores_environment_and_removes_inventory_output()
    {
        var repositoryRoot = RepositoryRoot();
        var testRoot = Path.Combine(Path.GetTempPath(), "t35-wrapper-cleanup-" + Guid.NewGuid().ToString("N"));
        var scriptDirectory = Path.Combine(testRoot, "tools");
        var runnerOutput = Path.Combine(scriptDirectory, "MyDmsVn.SqlTestRunner", "bin", "Release", "net8.0");
        Directory.CreateDirectory(runnerOutput);

        var wrapperSource = File.ReadAllText(Path.Combine(
            repositoryRoot, "tests", "tools", "Invoke-TestGate.ps1"));
        var physicalShadowAssignment = "$shadowRoot = Join-Path ([IO.Path]::GetTempPath()) (Join-Path 'MyDmsVn\\TestGate' ('runner-' + $PID.ToString('D10')))";
        Assert.Contains(physicalShadowAssignment, wrapperSource, StringComparison.Ordinal);
        var wrapperPath = Path.Combine(scriptDirectory, "Invoke-TestGate.ps1");
        File.WriteAllText(wrapperPath,
            wrapperSource.Replace(
                physicalShadowAssignment,
                "$shadowRoot = $env:MYDMSVN_TESTGATE_TEST_SHADOW_ROOT",
                StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(runnerOutput, new string('x', 100) + ".dll"), "fixture");

        var artifactsDirectory = Path.Combine(repositoryRoot, "artifacts");
        var harnessPath = Path.Combine(testRoot, "run-cleanup-regression.ps1");
        var artifactRoot = "artifacts/t35-wrapper-cleanup-" + Guid.NewGuid().ToString("N");
        var harness = """
$ErrorActionPreference = 'Stop'
$env:MYDMSVN_TESTGATE_INVENTORY_ROOT = 'prior-inventory'
$env:MYDMSVN_TESTGATE_ISOLATED_BUILD_PROPS = 'prior-props'
$env:MYDMSVN_TESTGATE_TEST_SHADOW_ROOT = 'C:\' + ('s' * 145)
function git { $global:LASTEXITCODE = 0; if ($args -contains 'rev-parse') { return ('0' * 40) }; return '' }
function dotnet { $global:LASTEXITCODE = 0 }
$beforeInventory = @(Get-ChildItem -LiteralPath __ARTIFACTS__ -Directory -Filter 'testgate-inventory-*' | ForEach-Object FullName)
$caughtExpectedFailure = $false
try {
    . __WRAPPER__ -RepositoryRoot __REPOSITORY__ -Mode Final -PlanId 'review-test' -TaskId 'cleanup' -ArtifactRoot '__ARTIFACT_ROOT__' -ApprovalFile 'fake-approval.json'
}
catch {
    if ($_.Exception.Message -like '*shadow dependency path exceeds*') { $caughtExpectedFailure = $true } else { throw }
}
if (-not $caughtExpectedFailure) { throw 'The deliberate post-setup failure did not occur.' }
if ($env:MYDMSVN_TESTGATE_INVENTORY_ROOT -ne 'prior-inventory') { throw 'Inventory environment was not restored.' }
if ($env:MYDMSVN_TESTGATE_ISOLATED_BUILD_PROPS -ne 'prior-props') { throw 'Build props environment was not restored.' }
$remainingInventory = @(Get-ChildItem -LiteralPath __ARTIFACTS__ -Directory -Filter 'testgate-inventory-*' | Where-Object { $_.FullName -notin $beforeInventory })
if ($remainingInventory.Count -ne 0) { throw 'Isolated inventory output was not removed.' }
Write-Output 'cleanup regression passed'
""".Replace("__WRAPPER__", QuotePs(wrapperPath), StringComparison.Ordinal)
            .Replace("__REPOSITORY__", QuotePs(repositoryRoot), StringComparison.Ordinal)
            .Replace("__ARTIFACT_ROOT__", artifactRoot, StringComparison.Ordinal)
            .Replace("__ARTIFACTS__", QuotePs(artifactsDirectory), StringComparison.Ordinal);
        File.WriteAllText(harnessPath, harness);

        var start = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(harnessPath);

        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start PowerShell.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            Assert.True(process.ExitCode == 0, output);
            Assert.Contains("cleanup regression passed", output, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private static string QuotePs(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MyDmsVn.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
