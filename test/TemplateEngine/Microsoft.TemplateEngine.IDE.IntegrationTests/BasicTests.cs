using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.IDE.IntegrationTests.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    public class BasicTests
    {

        [Fact]
        internal async Task GetCreationEffectsBasicTest()
        {
            var bootstrapper = BootstrapperFactory.GetBootstrapper();
            bootstrapper.InstallTestTemplate("TemplateWithSourceName");

            var template = bootstrapper.ListTemplates(true, WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithSourceName"));
            var result = await bootstrapper.GetCreationEffectsAsync(template.First().Info, "test", "test", new Dictionary<string, string>(), "").ConfigureAwait(false);
            Assert.Equal(2, result.CreationResult.PrimaryOutputs.Count);
            Assert.Equal(0, result.CreationResult.PostActions.Count);
            Assert.Equal(2, result.FileChanges.Count);

            Assert.Single(result.FileChanges.Where(change => change.ChangeKind == ChangeKind.Create && ((IFileChange2)change).SourceRelativePath == "bar.cs" && change.TargetRelativePath == "test.cs"));
            Assert.Single(result.FileChanges.Where(change => change.ChangeKind == ChangeKind.Create && ((IFileChange2)change).SourceRelativePath == "bar/bar.cs" && change.TargetRelativePath == "test/test.cs"));
        }

        [Fact]
        internal async Task CreateBasicTest()
        {
            var bootstrapper = BootstrapperFactory.GetBootstrapper(additionalVirtualLocations: new string[] { "test" });
            bootstrapper.InstallTestTemplate("TemplateWithSourceName");
            var template = bootstrapper.ListTemplates(true, WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithSourceName"));

            var result = await bootstrapper.CreateAsync(template.First().Info, "test", "test", new Dictionary<string, string>(), false, "").ConfigureAwait(false);

            Assert.Equal(2, result.PrimaryOutputs.Count);
            Assert.Equal(0, result.PostActions.Count);
        }
    }
}
