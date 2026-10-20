using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using MyDmsVn.Server.Infrastructure.IntegrationTests;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

// Invoked only inside an owned child: synthetic failure must not poison the testhost.
internal static class GuiSqlQuarantineRegression
{
    public static async Task RunAsync(string mode)
    {
        var root = Path.Combine(Path.GetTempPath(), "MyDmsVn-quarantine-" + Guid.NewGuid().ToString("N"));
        var records = Path.Combine(root, "TestResults", "gui-p5-t03");
        Directory.CreateDirectory(root);
        if (mode != "record-error") Directory.CreateDirectory(records);
        try
        {
            // Constructors only: no database is created, opened or dropped. The invalid
            // administration keyword makes the real cleanup fail before any SQL I/O.
            var database = (SqlTestDatabase)typeof(SqlTestDatabase).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0]
                .Invoke(new object?[] { "Data Source=unused;Initial Catalog=master", null,
                    "SyntheticNoSqlAllocation", "unused", "synthetic", (0, DateTime.MinValue), true });
            var fixture = (GuiSqlEnvironment)typeof(GuiSqlEnvironment).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0]
                .Invoke(new object[] { database, "UnsupportedSyntheticKeyword=1", root });
            if (mode == "uncertain")
                typeof(GuiSqlEnvironment).GetField("_uncertainLogin", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture, true);

            var failure = await Record.ExceptionAsync(() => fixture.DisposeAsync());
            Assert.NotNull(failure);
            Assert.True(GuiTestSafety.IsPoisoned, "SQL quarantine must stop the entire GUI session.");
            var admission = Assert.Throws<InvalidOperationException>(() => { _ = GuiSqlEnvironment.CreateAsync("unused-package"); });
            var original = admission.InnerException;
            Assert.NotNull(original);
            if (mode == "record-error")
            {
                var combined = Assert.IsType<AggregateException>(failure);
                Assert.Same(original, combined.InnerExceptions[0]);
                Assert.IsType<DirectoryNotFoundException>(combined.InnerExceptions[1]);
            }
            else Assert.Same(failure, original);

            var processAdmission = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                GuiOwnedProcess.RunAsync(new ProcessStartInfo("must-not-start.exe"), TimeSpan.FromSeconds(1)));
            Assert.Same(original, processAdmission.InnerException);
            if (mode != "record-error")
            {
                var record = Assert.Single(Directory.GetFiles(records));
                Assert.Contains(mode == "uncertain" ? "QuarantinedOwnershipOrProcessCleanupUncertain" : "QuarantinedCleanupFailed",
                    File.ReadAllText(record));
            }
        }
        finally
        {
            // Delete only this child's exact synthetic files and empty owned directories.
            if (Directory.Exists(records))
            {
                foreach (var file in Directory.GetFiles(records)) File.Delete(file);
                Directory.Delete(records);
                Directory.Delete(Path.GetDirectoryName(records)!);
            }
            Directory.Delete(root);
        }
    }
}
