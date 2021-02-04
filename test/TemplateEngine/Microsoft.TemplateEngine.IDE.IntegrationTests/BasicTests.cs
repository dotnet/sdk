using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.IDE.IntegrationTests.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    public class BasicTests : IClassFixture<PackageManager>
    {
        private PackageManager _packageManager;
        public BasicTests(PackageManager packageManager)
        {
            _packageManager = packageManager;
        }

        [Fact]
        internal async Task GetCreationEffects_BasicTest_Folder()
        {
            var bootstrapper = BootstrapperFactory.GetBootstrapper();
            bootstrapper.InstallTestTemplate("TemplateWithSourceName");

            string output = TestHelper.CreateTemporaryFolder();
            var template = bootstrapper.ListTemplates(true, WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithSourceName"));
            var result = await bootstrapper.GetCreationEffectsAsync(template.First().Info, "test", output, new Dictionary<string, string>(), "").ConfigureAwait(false);
            Assert.Equal(2, result.CreationResult.PrimaryOutputs.Count);
            Assert.Equal(0, result.CreationResult.PostActions.Count);
            Assert.Equal(2, result.FileChanges.Count);

            var expectedFileChanges = new FileChange[]
            {
                new FileChange ("bar.cs", "test.cs", ChangeKind.Create),
                new FileChange ("bar/bar.cs", "test/test.cs", ChangeKind.Create),

            };
            IFileChangeComparer comparer = new IFileChangeComparer();
            Assert.Equal(
                expectedFileChanges.OrderBy(s => s, comparer),
                result.FileChanges.OrderBy(s => s, comparer),
                comparer);

        }

        [Fact]
        internal async Task Create_BasicTest_Folder()
        {
            var bootstrapper = BootstrapperFactory.GetBootstrapper(additionalVirtualLocations: new string[] { "test" });
            bootstrapper.InstallTestTemplate("TemplateWithSourceName");

            string output = TestHelper.CreateTemporaryFolder();
            var template = bootstrapper.ListTemplates(true, WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithSourceName"));

            var result = await bootstrapper.CreateAsync(template.First().Info, "test", output, new Dictionary<string, string>(), false, "").ConfigureAwait(false);

            Assert.Equal(2, result.PrimaryOutputs.Count);
            Assert.Equal(0, result.PostActions.Count);
            Assert.True(File.Exists(Path.Combine(output, "test.cs")));
            Assert.True(File.Exists(Path.Combine(output, "test/test.cs")));
        }

        [Fact]
        internal async Task GetCreationEffects_BasicTest_Package()
        {
            var bootstrapper = BootstrapperFactory.GetBootstrapper();
            string packageLocation = _packageManager.PackProjectTemplatesNuGetPackage("microsoft.dotnet.common.projecttemplates.5.0");
            bootstrapper.InstallTemplate(packageLocation);

            string output = TestHelper.CreateTemporaryFolder();
            var template = bootstrapper.ListTemplates(true, WellKnownSearchFilters.NameFilter("console"));
            var result = await bootstrapper.GetCreationEffectsAsync(template.First().Info, "test", output, new Dictionary<string, string>(), "").ConfigureAwait(false);
            Assert.Equal(2, result.CreationResult.PrimaryOutputs.Count);
            Assert.Equal(2, result.CreationResult.PostActions.Count);
            Assert.Equal(2, result.FileChanges.Count);

            var expectedFileChanges = new FileChange[]
            {
                new FileChange ("Company.ConsoleApplication1.csproj", "test.csproj", ChangeKind.Create),
                new FileChange ("Program.cs", "Program.cs", ChangeKind.Create),
            };
            IFileChangeComparer comparer = new IFileChangeComparer();
            Assert.Equal(
                expectedFileChanges.OrderBy(s => s, comparer),
                result.FileChanges.OrderBy(s => s, comparer),
                comparer);
        }

        [Fact]
        internal async Task Create_BasicTest_Package()
        {
            var bootstrapper = BootstrapperFactory.GetBootstrapper();
            string packageLocation = _packageManager.PackProjectTemplatesNuGetPackage("microsoft.dotnet.common.projecttemplates.5.0");
            bootstrapper.InstallTemplate(packageLocation);

            string output = TestHelper.CreateTemporaryFolder();
            var template = bootstrapper.ListTemplates(true, WellKnownSearchFilters.NameFilter("console"));
            var result = await bootstrapper.CreateAsync(template.First().Info, "test", output, new Dictionary<string, string>(), false, "").ConfigureAwait(false);
            Assert.Equal(2, result.PrimaryOutputs.Count);
            Assert.Equal(2, result.PostActions.Count);

            Assert.True(File.Exists(Path.Combine(output, "Program.cs")));
            Assert.True(File.Exists(Path.Combine(output, "test.csproj")));
        }
    }
}
