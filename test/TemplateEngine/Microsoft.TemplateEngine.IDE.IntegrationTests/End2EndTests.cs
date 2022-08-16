// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    [UsesVerify]
    [Collection("Verify Tests")]
    public class End2EndTests : BootstrapperTestBase
    {
        [Fact]
        internal async Task SourceNameFormsTest()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("SourceNameForms");
            await InstallTemplateAsync(bootstrapper, templateLocation).ConfigureAwait(false);

            string output = TestUtils.CreateTemporaryFolder();

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.SourceNameForms") }).ConfigureAwait(false);
            var result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "MyApp.1", output, new Dictionary<string, string?>()).ConfigureAwait(false);

            Assert.Equal(Edge.Template.CreationResultStatus.Success, result.Status);

            string targetFile = Path.Combine(output, "myapp.12.cs");
            Assert.True(File.Exists(targetFile));
            string targetFile2 = Path.Combine(output, "MyApp.1.cs");
            Assert.True(File.Exists(targetFile2));

            await Verify(File.ReadAllText(targetFile2));
        }

        [Fact]
        internal async Task ValueForms_DerivedSymbolTest()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("ValueForms/DerivedSymbol");
            await InstallTemplateAsync(bootstrapper, templateLocation).ConfigureAwait(false);

            string output = TestUtils.CreateTemporaryFolder();

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.ValueForms.DerivedSymbol") }).ConfigureAwait(false);
            var result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "Real.Web.App", output, new Dictionary<string, string?>()).ConfigureAwait(false);

            Assert.Equal(Edge.Template.CreationResultStatus.Success, result.Status);

            string targetFile = Path.Combine(output, "Real.Web.App.txt");
            Assert.True(File.Exists(targetFile));

            await Verify(File.ReadAllText(targetFile));
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "https://github.com/dotnet/templating/issues/5115")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        internal async Task ValueForms_DerivedSymbolFromGeneratedSymbolTest()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("ValueForms/DerivedSymbolFromGeneratedSymbol");
            await InstallTemplateAsync(bootstrapper, templateLocation).ConfigureAwait(false);

            string output = TestUtils.CreateTemporaryFolder();

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.ValueForms.DerivedSymbolFromGeneratedSymbol") }).ConfigureAwait(false);
            var result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "Real.Web.App", output, new Dictionary<string, string?>()).ConfigureAwait(false);

            Assert.Equal(Edge.Template.CreationResultStatus.Success, result.Status);

            string targetFile = Path.Combine(output, "Real.Web.App.txt");
            Assert.True(File.Exists(targetFile));

            await Verify(File.ReadAllText(targetFile));
        }
    }
}
