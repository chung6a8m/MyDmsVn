using Xunit;

using System.Linq;
using System.Reflection;
using System.Threading;
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
    }
}
