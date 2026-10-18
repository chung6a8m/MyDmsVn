using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class DesktopSessionProjectContractTests
    {
        private static readonly string[] SessionTestFiles =
        {
            "CatalogLookupViewModelTests.cs",
            "CatalogViewModelTests.cs",
            "DebouncedAsyncActionTests.cs",
            "DesktopSessionExpiryTests.cs",
            "DesktopViewModelBaseTests.cs",
            "FeatureApiContractTests.cs",
            "FoundationViewModelTests.cs",
            "GoodsReceiptListViewModelTests.cs",
            "GoodsReceiptProductOptionsViewModelTests.cs",
            "InventoryViewModelTests.cs",
            "LocalGoodsReceiptApiClientTests.cs",
            "LocalInventoryApiClientTests.cs",
            "LocalPermissionApiClientTests.cs",
            "LoginViewModelTests.cs",
            "PermissionImportViewModelTests.cs",
            "SecurityAdministrationViewModelTests.cs",
            "UiDispatcherTests.cs",
        };

        [Fact]
        public void Session_project_owns_only_audited_pure_tests_and_is_in_the_active_build_matrix()
        {
            var root = FindRepositoryRoot();
            var sessionDirectory = Path.Combine(root, "tests", "MyDmsVn.Desktop.Session.Tests");
            var projectPath = Path.Combine(sessionDirectory, "MyDmsVn.Desktop.Session.Tests.csproj");
            var originalDirectory = Path.Combine(root, "tests", "MyDmsVn.Desktop.Tests");
            var project = XDocument.Load(projectPath);

            Assert.Equal("net48", project.Descendants("TargetFramework").Single().Value);
            Assert.Empty(project.Descendants("UseWindowsForms"));

            var references = project.Descendants("ProjectReference")
                .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
                .ToArray();
            Assert.DoesNotContain(references, reference => reference.IndexOf("Desktop.WinForms", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.DoesNotContain(references, reference => reference.IndexOf("Server.Infrastructure", StringComparison.OrdinalIgnoreCase) >= 0);

            foreach (var file in SessionTestFiles)
            {
                Assert.True(File.Exists(Path.Combine(sessionDirectory, file)), $"Missing session test file {file}.");
                Assert.False(File.Exists(Path.Combine(originalDirectory, file)), $"Duplicate physical owner for {file}.");
            }

            Assert.Contains("MyDmsVn.Desktop.Session.Tests.csproj", File.ReadAllText(Path.Combine(root, "MyDmsVn.sln")));
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MyDmsVn.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate MyDmsVn.sln.");
        }
    }
}
