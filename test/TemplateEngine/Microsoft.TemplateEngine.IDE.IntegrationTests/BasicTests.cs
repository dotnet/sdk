// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.IDE.IntegrationTests.Utils;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests
{
    [UsesVerify]
    [Collection("Verify Tests")]
    public class BasicTests : BootstrapperTestBase, IClassFixture<PackageManager>
    {
        private readonly PackageManager _packageManager;

        public BasicTests(PackageManager packageManager)
        {
            _packageManager = packageManager;
        }

        [Fact]
        internal async Task GetCreationEffects_BasicTest_Folder()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            await InstallTestTemplateAsync(bootstrapper, "TemplateWithSourceName");

            string output = TestUtils.CreateTemporaryFolder();
            var foundTemplates = await bootstrapper.GetTemplatesAsync(
                new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithSourceName") });
            var result = await bootstrapper.GetCreationEffectsAsync(foundTemplates[0].Info, "test", output, new Dictionary<string, string?>());
            Assert.Equal(2, result.CreationEffects?.CreationResult.PrimaryOutputs.Count);
            Assert.Equal(0, result.CreationEffects?.CreationResult.PostActions.Count);
            Assert.Equal(2, result.CreationEffects?.FileChanges.Count);

            var expectedFileChanges = new FileChange[]
            {
                new FileChange("bar.cs", "test.cs", ChangeKind.Create),
                new FileChange("bar/bar.cs", "test/test.cs", ChangeKind.Create),
            };
            IFileChangeComparer comparer = new IFileChangeComparer();
            Assert.NotNull(result.CreationEffects?.FileChanges);

            Assert.Equal(
                expectedFileChanges.OrderBy(s => s, comparer),
                result.CreationEffects.FileChanges.OrderBy(s => s, comparer),
                comparer);
        }

        [Fact]
        internal async Task Create_BasicTest_Folder()
        {
            using Bootstrapper bootstrapper = GetBootstrapper(additionalVirtualLocations: new string[] { "test" });
            await InstallTestTemplateAsync(bootstrapper, "TemplateWithSourceName");

            string output = TestUtils.CreateTemporaryFolder();
            var foundTemplates = await bootstrapper.GetTemplatesAsync(
                new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithSourceName") });

            var result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "test", output, new Dictionary<string, string?>());

            Assert.Equal(2, result.CreationResult?.PrimaryOutputs.Count);
            Assert.Equal(0, result.CreationResult?.PostActions.Count);
            Assert.True(File.Exists(Path.Combine(output, "test.cs")));
            Assert.True(File.Exists(Path.Combine(output, "test/test.cs")));
        }

        [Fact]
        internal async Task GetCreationEffects_BasicTest_Package()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string packageLocation = await _packageManager.GetNuGetPackage("Microsoft.DotNet.Common.ProjectTemplates.5.0");
            await InstallTemplateAsync(bootstrapper, packageLocation);

            string output = TestUtils.CreateTemporaryFolder();

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("console") });
            var result = await bootstrapper.GetCreationEffectsAsync(foundTemplates[0].Info, "test", output, new Dictionary<string, string?>());
            Assert.Equal(2, result.CreationEffects?.CreationResult.PrimaryOutputs.Count);
            Assert.Equal(2, result.CreationEffects?.CreationResult.PostActions.Count);
            Assert.Equal(2, result.CreationEffects?.FileChanges.Count);

            var expectedFileChanges = new FileChange[]
            {
                new FileChange("Company.ConsoleApplication1.csproj", "test.csproj", ChangeKind.Create),
                new FileChange("Program.cs", "Program.cs", ChangeKind.Create),
            };
            IFileChangeComparer comparer = new IFileChangeComparer();
            Assert.NotNull(result.CreationEffects?.FileChanges);
            Assert.Equal(
                expectedFileChanges.OrderBy(s => s, comparer),
                result.CreationEffects.FileChanges.OrderBy(s => s, comparer),
                comparer);
        }

        [Fact]
        internal async Task Create_BasicTest_Package()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string packageLocation = await _packageManager.GetNuGetPackage("Microsoft.DotNet.Common.ProjectTemplates.5.0");
            await InstallTemplateAsync(bootstrapper, packageLocation);

            string output = TestUtils.CreateTemporaryFolder();
            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("console") });
            var result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "test", output, new Dictionary<string, string?>());
            Assert.Equal(2, result.CreationResult?.PrimaryOutputs.Count);
            Assert.Equal(2, result.CreationResult?.PostActions.Count);

            Assert.True(File.Exists(Path.Combine(output, "Program.cs")));
            Assert.True(File.Exists(Path.Combine(output, "test.csproj")));
        }

        [Fact]
        internal async Task Create_TemplateWithBinaryFile_Folder()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("TemplateWithBinaryFile");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithBinaryFile") });
            var result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "my-test-folder", output, new Dictionary<string, string?>());

            string sourceImage = Path.Combine(templateLocation, "image.png");
            string targetImage = Path.Combine(output, "image.png");

            Assert.True(File.Exists(targetImage));

            Assert.Equal(
                new FileInfo(sourceImage).Length,
                new FileInfo(targetImage).Length);
            Assert.True(TestUtils.CompareFiles(sourceImage, targetImage), $"The content of {sourceImage} and {targetImage} is not same.");
        }

        [Fact]
        internal async Task Create_TemplateWithBinaryFile_Package()
        {
            Console.WriteLine(System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
            using Bootstrapper bootstrapper = GetBootstrapper();
            string packageLocation = PackTestTemplatesNuGetPackage(_packageManager);
            await InstallTemplateAsync(bootstrapper, packageLocation);
            string templateLocation = GetTestTemplateLocation("TemplateWithBinaryFile");

            string output = TestUtils.CreateTemporaryFolder();

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithBinaryFile") });
            var result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "my-test-folder", output, new Dictionary<string, string?>());

            string sourceImage = Path.Combine(templateLocation, "image.png");
            string targetImage = Path.Combine(output, "image.png");

            Assert.True(File.Exists(targetImage));

            Assert.Equal(
                new FileInfo(sourceImage).Length,
                new FileInfo(targetImage).Length);
            Assert.True(TestUtils.CompareFiles(sourceImage, targetImage), $"The content of {sourceImage} and {targetImage} is not same.");
        }

        [Fact]
        internal async Task GetTemplates_BasicTest()
        {
            using Bootstrapper bootstrapper = GetBootstrapper(loadTestTemplates: true);

            var result1 = await bootstrapper.GetTemplatesAsync(default);
            var result2 = await bootstrapper.GetTemplatesAsync(Array.Empty<Func<ITemplateInfo, MatchInfo>>(), cancellationToken: default);

            Assert.NotEmpty(result1);
            Assert.Equal(result1.Count, result2.Count);
        }

        [Fact]
        internal async Task SourceNameForms_BasicTest()
        {
            using Bootstrapper bootstrapper = GetBootstrapper();
            string templateLocation = GetTestTemplateLocation("SourceNameForms");
            await InstallTemplateAsync(bootstrapper, templateLocation);

            string output = TestUtils.CreateTemporaryFolder();

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.SourceNameForms") });
            var result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "MyApp.1", output, new Dictionary<string, string?>());

            Assert.Equal(Edge.Template.CreationResultStatus.Success, result.Status);

            string targetFile = Path.Combine(output, "myapp.12.cs");
            Assert.True(File.Exists(targetFile));
            string targetFile2 = Path.Combine(output, "MyApp.1.cs");
            Assert.True(File.Exists(targetFile2));

            await Verify(File.ReadAllText(targetFile2));
        }
    }
}
