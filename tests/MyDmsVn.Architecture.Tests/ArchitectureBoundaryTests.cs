using Xunit;

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
    }
}
