using Xunit;

using System.Linq;
using System.Reflection;
using System.Threading;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Architecture.Tests
{
    public sealed class ArchitectureBoundaryTests
    {
        [Fact]
        public void Source_projects_obey_allowed_reference_directions()
        {
            var violations = ProjectReferencePolicy.FindViolations();

            Assert.Empty(violations);
        }

        [Fact]
        public void Core_projects_do_not_reference_transport_ui_or_database_packages()
        {
            var violations = ProjectReferencePolicy.FindForbiddenCorePackages();

            Assert.Empty(violations);
        }

        [Fact]
        public void Persistence_infrastructure_has_no_static_ambient_transaction_storage()
        {
            var ambientFields = typeof(SqlUnitOfWorkFactory).Assembly
                .GetTypes()
                .SelectMany(type => type.GetFields(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Static))
                .Where(field =>
                    field.FieldType.IsGenericType &&
                    field.FieldType.GetGenericTypeDefinition() == typeof(AsyncLocal<>))
                .Select(field => $"{field.DeclaringType?.FullName}.{field.Name}");

            Assert.Empty(ambientFields);
        }

        [Fact]
        public void Server_application_has_no_http_ui_or_database_assembly_references()
        {
            var forbiddenTokens = new[]
            {
                "AspNetCore",
                "Windows.Forms",
                "Dapper",
                "RepoDb",
                "SqlClient",
            };
            var references = typeof(ApiResponseMapper).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name ?? string.Empty);

            Assert.DoesNotContain(
                references,
                reference => forbiddenTokens.Any(token =>
                    reference.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0));
        }

        [Fact]
        public void Desktop_application_public_api_does_not_expose_mediatr_or_erroror()
        {
            var exposedTypes = typeof(IFoundationApiClient).Assembly
                .GetExportedTypes()
                .SelectMany(type =>
                    type.GetMethods().Select(method => method.ReturnType)
                        .Concat(type.GetMethods().SelectMany(method =>
                            method.GetParameters().Select(parameter => parameter.ParameterType)))
                        .Concat(type.GetProperties().Select(property => property.PropertyType)))
                .SelectMany(FlattenType)
                .Where(type => type.Namespace != null)
                .Select(type => type.Namespace!);

            Assert.DoesNotContain(
                exposedTypes,
                typeNamespace =>
                    typeNamespace.StartsWith("ErrorOr", System.StringComparison.Ordinal) ||
                    typeNamespace.StartsWith("MediatR", System.StringComparison.Ordinal));
        }

        private static System.Collections.Generic.IEnumerable<System.Type> FlattenType(System.Type type)
        {
            yield return type;
            foreach (var argument in type.GetGenericArguments())
            {
                foreach (var nested in FlattenType(argument))
                {
                    yield return nested;
                }
            }
        }
    }
}
