using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MyDmsVn.Architecture.Tests
{
    internal static class ProjectReferencePolicy
    {
        private static readonly IReadOnlyDictionary<string, ISet<string>> AllowedProjectReferences =
            new Dictionary<string, ISet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["MyDmsVn.Contracts"] = Set(),
                ["MyDmsVn.SharedKernel"] = Set(),
                ["MyDmsVn.Server.Domain"] = Set("MyDmsVn.SharedKernel"),
                ["MyDmsVn.Server.Application"] = Set("MyDmsVn.Contracts", "MyDmsVn.Server.Domain"),
                ["MyDmsVn.Server.Infrastructure"] = Set("MyDmsVn.Server.Application", "MyDmsVn.Server.Domain"),
                ["MyDmsVn.Server.DbMigrator"] = Set("MyDmsVn.Server.Infrastructure"),
                ["MyDmsVn.Desktop.Application"] = Set("MyDmsVn.Contracts"),
                ["MyDmsVn.Desktop.Infrastructure.Local"] = Set(
                    "MyDmsVn.Desktop.Application",
                    "MyDmsVn.Server.Application"),
                ["MyDmsVn.Desktop.WinForms"] = Set("MyDmsVn.Desktop.Application"),
                ["MyDmsVn.Desktop.App"] = Set(
                    "MyDmsVn.Desktop.Infrastructure.Local",
                    "MyDmsVn.Desktop.WinForms",
                    "MyDmsVn.Server.Infrastructure"),
                ["MyDmsVn.Desktop.AppCore"] = Set(
                    "MyDmsVn.Desktop.Infrastructure.Local",
                    "MyDmsVn.Desktop.WinForms",
                    "MyDmsVn.Server.Infrastructure"),
            };

        private static readonly IReadOnlyDictionary<string, string[]> ForbiddenPackageTokens =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["MyDmsVn.Contracts"] = new[] { "ErrorOr", "MediatR", "RepoDb", "Dapper", "SqlClient", "AspNetCore", "Windows.Forms" },
                ["MyDmsVn.SharedKernel"] = new[] { "RepoDb", "Dapper", "SqlClient", "AspNetCore", "Windows.Forms" },
                ["MyDmsVn.Server.Domain"] = new[] { "RepoDb", "Dapper", "SqlClient", "AspNetCore", "Windows.Forms" },
                ["MyDmsVn.Server.Application"] = new[] { "RepoDb", "Dapper", "SqlClient", "AspNetCore", "Windows.Forms" },
                ["MyDmsVn.Desktop.Application"] = new[] { "ErrorOr", "MediatR", "RepoDb", "Dapper", "SqlClient", "AspNetCore", "Windows.Forms" },
                ["MyDmsVn.Desktop.WinForms"] = new[] { "RepoDb", "Dapper", "SqlClient", "AspNetCore" },
            };

        public static IReadOnlyCollection<string> FindViolations()
        {
            var violations = new List<string>();
            foreach (var projectPath in GetSourceProjects())
            {
                var projectName = Path.GetFileNameWithoutExtension(projectPath);
                if (!AllowedProjectReferences.TryGetValue(projectName, out var allowed))
                {
                    violations.Add($"No reference policy is defined for {projectName}.");
                    continue;
                }

                var document = XDocument.Load(projectPath);
                var references = document.Descendants("ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => Path.GetFileNameWithoutExtension(value!));

                violations.AddRange(
                    references
                        .Where(reference => !allowed.Contains(reference))
                        .Select(reference => $"{projectName} must not reference {reference}."));
            }

            return violations;
        }

        public static IReadOnlyCollection<string> FindForbiddenCorePackages()
        {
            var violations = new List<string>();
            foreach (var projectPath in GetSourceProjects())
            {
                var projectName = Path.GetFileNameWithoutExtension(projectPath);
                if (!ForbiddenPackageTokens.TryGetValue(projectName, out var forbiddenTokens))
                {
                    continue;
                }

                var document = XDocument.Load(projectPath);
                var packages = document.Descendants("PackageReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value));

                foreach (var package in packages)
                {
                    if (forbiddenTokens.Any(token => package!.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        violations.Add($"{projectName} must not reference package {package}.");
                    }
                }
            }

            return violations;
        }

        private static IEnumerable<string> GetSourceProjects()
        {
            return Directory.GetFiles(Path.Combine(FindRepositoryRoot(), "src"), "*.csproj", SearchOption.AllDirectories);
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "MyDmsVn.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate MyDmsVn.sln.");
        }

        private static ISet<string> Set(params string[] values)
        {
            return new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
        }
    }
}
