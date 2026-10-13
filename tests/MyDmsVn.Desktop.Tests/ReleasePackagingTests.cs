using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class ReleasePackagingTests
    {
        [Fact]
        public async Task Manifest_only_mode_hashes_both_hosts_and_separate_admin_package()
        {
            var fixture = CreateFixture();
            try
            {
                var result = await RunPackagingScriptAsync(fixture.Root, "1.2.3");

                Assert.True(
                    result.ExitCode == 0,
                    "stdout: " + result.StandardOutput + Environment.NewLine +
                    "stderr: " + result.StandardError);
                var manifest = JObject.Parse(
                    File.ReadAllText(Path.Combine(fixture.Root, "release-manifest.json")));
                Assert.Equal("1.2.3", manifest.Value<string>("productVersion"));
                Assert.Equal(8, manifest["schemaSupport"]!.Value<int>("minimum"));
                Assert.Equal(8, manifest["schemaSupport"]!.Value<int>("maximum"));
                Assert.IsType<bool>(manifest["sourceTreeDirty"]!.Value<bool>());
                Assert.Equal(3, manifest["packages"]!.Count());
                Assert.All(
                    manifest["packages"]!,
                    package => Assert.All(
                        package["files"]!,
                        file => Assert.Matches("^[A-F0-9]{64}$", file.Value<string>("sha256")!)));
            }
            finally
            {
                Directory.Delete(fixture.Root, recursive: true);
            }
        }

        [Theory]
        [InlineData("MyDmsVn.Server.DbMigrator.exe", "binary")]
        [InlineData("appsettings.json", "Server=db;Database=prod;User Id=sa;Password=secret")]
        public async Task Desktop_packages_reject_admin_tools_and_embedded_secrets(
            string fileName,
            string content)
        {
            var fixture = CreateFixture();
            try
            {
                File.WriteAllText(Path.Combine(fixture.Net8Directory, fileName), content);

                var result = await RunPackagingScriptAsync(fixture.Root, "1.2.3");

                Assert.NotEqual(0, result.ExitCode);
                Assert.Contains("Release.ArtifactRejected", result.StandardError);
                Assert.False(File.Exists(Path.Combine(fixture.Root, "release-manifest.json")));
            }
            finally
            {
                Directory.Delete(fixture.Root, recursive: true);
            }
        }

        private static PackagingFixture CreateFixture()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "MyDmsVn.ReleaseTests",
                Guid.NewGuid().ToString("N"));
            var net48 = Path.Combine(root, "desktop-net48-win-x86");
            var net8 = Path.Combine(root, "desktop-net8-win-x64");
            var admin = Path.Combine(root, "admin-migrator-net8-win-x64");
            Directory.CreateDirectory(net48);
            Directory.CreateDirectory(net8);
            Directory.CreateDirectory(admin);
            File.WriteAllText(Path.Combine(net48, "MyDmsVn.Desktop.App.exe"), "net48-host");
            File.WriteAllText(Path.Combine(net8, "MyDmsVn.Desktop.AppCore.dll"), "net8-host");
            File.WriteAllText(Path.Combine(admin, "MyDmsVn.Server.DbMigrator.dll"), "admin-only");
            return new PackagingFixture(root, net8);
        }

        private static async Task<ProcessResult> RunPackagingScriptAsync(
            string outputRoot,
            string version)
        {
            var repositoryRoot = FindRepositoryRoot();
            var script = Path.Combine(repositoryRoot, "deployment", "release", "Publish-LocalRelease.ps1");
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" +
                    script + "\" -Version \"" + version + "\" -OutputRoot \"" +
                    outputRoot + "\" -SkipPublish",
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = repositoryRoot,
            };
            using (var process = Process.Start(startInfo)!)
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());
                return new ProcessResult(process.ExitCode, await output, await error);
            }
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "MyDmsVn.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ??
                throw new InvalidOperationException("Repository root was not found.");
        }

        private sealed class PackagingFixture
        {
            public PackagingFixture(string root, string net8Directory)
            {
                Root = root;
                Net8Directory = net8Directory;
            }

            public string Root { get; }
            public string Net8Directory { get; }
        }

        private sealed class ProcessResult
        {
            public ProcessResult(int exitCode, string standardOutput, string standardError)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput;
                StandardError = standardError;
            }

            public int ExitCode { get; }
            public string StandardOutput { get; }
            public string StandardError { get; }
        }
    }
}
