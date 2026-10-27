using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using MyDmsVn.Server.Infrastructure.Operations;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class ReleasePackagingTests
    {
        [Fact]
        public async Task Manifest_only_mode_hashes_active_desktop_and_separate_admin_package()
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
                Assert.Equal(SchemaCompatibilityPolicy.CurrentScripts.Count, manifest["schemaSupport"]!.Value<int>("minimum"));
                Assert.Equal(SchemaCompatibilityPolicy.CurrentScripts.Count, manifest["schemaSupport"]!.Value<int>("maximum"));
                Assert.Equal(SchemaCompatibilityPolicy.CurrentScripts,
                    manifest["schemaSupport"]!["requiredScripts"]!.Values<string>());
                Assert.IsType<bool>(manifest["sourceTreeDirty"]!.Value<bool>());
                Assert.Equal(2, manifest["packages"]!.Count());
                Assert.All(manifest["packages"]!, package =>
                    Assert.Equal("net48", package.Value<string>("targetFramework")));
                Assert.All(
                    manifest["packages"]!,
                    package => Assert.All(
                        package["files"]!,
                        file => Assert.Matches("^[A-F0-9]{64}$", file.Value<string>("sha256")!)));
            }
            finally
            {
                if (!GuiTestSafety.IsPoisoned) Directory.Delete(fixture.Root, recursive: true);
            }
        }

        [Fact]
        public async Task Packages_without_runtime_schema_policy_are_rejected()
        {
            var fixture = CreateFixture();
            try
            {
                File.Delete(Path.Combine(fixture.Root, "admin-migrator-net48-win-x86", "MyDmsVn.Server.Infrastructure.dll"));
                var result = await RunPackagingScriptAsync(fixture.Root, "1.2.3");
                Assert.NotEqual(0, result.ExitCode);
                Assert.Contains("Release.SchemaPolicyMissing", result.StandardError);
                Assert.False(File.Exists(Path.Combine(fixture.Root, "release-manifest.json")));
            }
            finally
            {
                if (!GuiTestSafety.IsPoisoned) Directory.Delete(fixture.Root, recursive: true);
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
                File.WriteAllText(Path.Combine(fixture.DesktopDirectory, fileName), content);

                var result = await RunPackagingScriptAsync(fixture.Root, "1.2.3");

                Assert.NotEqual(0, result.ExitCode);
                Assert.Contains("Release.ArtifactRejected", result.StandardError);
                Assert.False(File.Exists(Path.Combine(fixture.Root, "release-manifest.json")));
            }
            finally
            {
                if (!GuiTestSafety.IsPoisoned) Directory.Delete(fixture.Root, recursive: true);
            }
        }

        private static PackagingFixture CreateFixture()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "MyDmsVn.ReleaseTests",
                Guid.NewGuid().ToString("N"));
            var net48 = Path.Combine(root, "desktop-net48-win-x86");
            var admin = Path.Combine(root, "admin-migrator-net48-win-x86");
            Directory.CreateDirectory(net48);
            Directory.CreateDirectory(admin);
            File.WriteAllText(Path.Combine(net48, "MyDmsVn.Desktop.App.exe"), "net48-host");
            File.WriteAllText(Path.Combine(admin, "MyDmsVn.Server.DbMigrator.exe"), "admin-only");
            File.Copy(typeof(SchemaCompatibilityPolicy).Assembly.Location,
                Path.Combine(admin, "MyDmsVn.Server.Infrastructure.dll"));
            Require32BitPolicy(Path.Combine(admin, "MyDmsVn.Server.Infrastructure.dll"));
            return new PackagingFixture(root, net48);
        }

        // Match publish -p:PlatformTarget=x86 even when test dependencies were built AnyCPU.
        private static void Require32BitPolicy(string path)
        {
            var bytes = File.ReadAllBytes(path);
            using (var reader = new BinaryReader(new MemoryStream(bytes)))
            {
                reader.BaseStream.Position = 0x3c;
                var pe = reader.ReadInt32();
                reader.BaseStream.Position = pe + 4;
                Assert.Equal((ushort)0x14c, reader.ReadUInt16());
                var sections = reader.ReadUInt16();
                reader.BaseStream.Position = pe + 20;
                var optionalSize = reader.ReadUInt16();
                reader.BaseStream.Position = pe + 24;
                Assert.Equal((ushort)0x10b, reader.ReadUInt16());
                reader.BaseStream.Position = pe + 24 + 96 + 14 * 8;
                var cliRva = reader.ReadUInt32();
                for (var index = 0; index < sections; index++)
                {
                    reader.BaseStream.Position = pe + 24 + optionalSize + index * 40 + 8;
                    var virtualSize = reader.ReadUInt32();
                    var virtualAddress = reader.ReadUInt32();
                    var rawSize = reader.ReadUInt32();
                    var rawAddress = reader.ReadUInt32();
                    if (cliRva < virtualAddress || cliRva >= virtualAddress + Math.Max(virtualSize, rawSize))
                        continue;
                    var flagsOffset = checked((int)(rawAddress + cliRva - virtualAddress + 16));
                    var flags = BitConverter.GetBytes(BitConverter.ToUInt32(bytes, flagsOffset) | 2u);
                    Array.Copy(flags, 0, bytes, flagsOffset, flags.Length);
                    File.WriteAllBytes(path, bytes);
                    return;
                }
            }
            throw new InvalidOperationException("Fixture policy has no CLR header.");
        }

        private static async Task<GuiProcessResult> RunPackagingScriptAsync(
            string outputRoot,
            string version)
        {
            var repositoryRoot = FindRepositoryRoot();
            var script = Path.Combine(repositoryRoot, "deployment", "release", "Publish-LocalRelease.ps1");
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" +
                    script + "\" -Version \"" + version + "\" -OutputRoot \"" +
                    outputRoot + "\" -SkipPublish",
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = repositoryRoot,
            };
            return await GuiOwnedProcess.RunAsync(startInfo, TimeSpan.FromSeconds(60));
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
            public PackagingFixture(string root, string desktopDirectory)
            {
                Root = root;
                DesktopDirectory = desktopDirectory;
            }

            public string Root { get; }
            public string DesktopDirectory { get; }
        }

    }
}
