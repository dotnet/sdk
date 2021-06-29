// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.IDE.IntegrationTests.Utils;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
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
            using Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper();
            await bootstrapper.InstallTestTemplateAsync("TemplateWithSourceName").ConfigureAwait(false);

            string output = TestUtils.CreateTemporaryFolder();
            var foundTemplates = await bootstrapper.GetTemplatesAsync(
                new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithSourceName") }).ConfigureAwait(false);
            var result = await bootstrapper.GetCreationEffectsAsync(foundTemplates[0].Info, "test", output, new Dictionary<string, string>()).ConfigureAwait(false);
            Assert.Equal(2, result.CreationEffects.CreationResult.PrimaryOutputs.Count);
            Assert.Equal(0, result.CreationEffects.CreationResult.PostActions.Count);
            Assert.Equal(2, result.CreationEffects.FileChanges.Count);

            var expectedFileChanges = new FileChange[]
            {
                new FileChange ("bar.cs", "test.cs", ChangeKind.Create),
                new FileChange ("bar/bar.cs", "test/test.cs", ChangeKind.Create),
            };
            IFileChangeComparer comparer = new IFileChangeComparer();
            Assert.Equal(
                expectedFileChanges.OrderBy(s => s, comparer),
                result.CreationEffects.FileChanges.OrderBy(s => s, comparer),
                comparer);
        }

        [Fact]
        internal async Task Create_BasicTest_Folder()
        {
            using Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper(additionalVirtualLocations: new string[] { "test" });
            await bootstrapper.InstallTestTemplateAsync("TemplateWithSourceName").ConfigureAwait(false);

            string output = TestUtils.CreateTemporaryFolder();
            var foundTemplates = await bootstrapper.GetTemplatesAsync(
                new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithSourceName") }).ConfigureAwait(false);

            var result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "test", output, new Dictionary<string, string>()).ConfigureAwait(false);

            Assert.Equal(2, result.CreationResult.PrimaryOutputs.Count);
            Assert.Equal(0, result.CreationResult.PostActions.Count);
            Assert.True(File.Exists(Path.Combine(output, "test.cs")));
            Assert.True(File.Exists(Path.Combine(output, "test/test.cs")));
        }

        [Fact]
        internal async Task GetCreationEffects_BasicTest_Package()
        {
            using Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper();
            string packageLocation = await _packageManager.GetNuGetPackage("Microsoft.DotNet.Common.ProjectTemplates.5.0").ConfigureAwait(false);
            await bootstrapper.InstallTemplateAsync(packageLocation).ConfigureAwait(false);

            string output = TestUtils.CreateTemporaryFolder();

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("console") }).ConfigureAwait(false);
            var result = await bootstrapper.GetCreationEffectsAsync(foundTemplates[0].Info, "test", output, new Dictionary<string, string>()).ConfigureAwait(false);
            Assert.Equal(2, result.CreationEffects.CreationResult.PrimaryOutputs.Count);
            Assert.Equal(2, result.CreationEffects.CreationResult.PostActions.Count);
            Assert.Equal(2, result.CreationEffects.FileChanges.Count);

            var expectedFileChanges = new FileChange[]
            {
                new FileChange ("Company.ConsoleApplication1.csproj", "test.csproj", ChangeKind.Create),
                new FileChange ("Program.cs", "Program.cs", ChangeKind.Create),
            };
            IFileChangeComparer comparer = new IFileChangeComparer();
            Assert.Equal(
                expectedFileChanges.OrderBy(s => s, comparer),
                result.CreationEffects.FileChanges.OrderBy(s => s, comparer),
                comparer);
        }

        [Fact]
        internal async Task Create_BasicTest_Package()
        {
            using Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper();
            string packageLocation = await _packageManager.GetNuGetPackage("Microsoft.DotNet.Common.ProjectTemplates.5.0").ConfigureAwait(false);
            await bootstrapper.InstallTemplateAsync(packageLocation).ConfigureAwait(false);

            string output = TestUtils.CreateTemporaryFolder();
            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("console") }).ConfigureAwait(false);
            var result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "test", output, new Dictionary<string, string>()).ConfigureAwait(false);
            Assert.Equal(2, result.CreationResult.PrimaryOutputs.Count);
            Assert.Equal(2, result.CreationResult.PostActions.Count);

            Assert.True(File.Exists(Path.Combine(output, "Program.cs")));
            Assert.True(File.Exists(Path.Combine(output, "test.csproj")));
        }

        [Fact]
        internal async Task Create_TemplateWithBinaryFile_Folder()
        {
            using Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper();
            string templateLocation = TestUtils.GetTestTemplateLocation("TemplateWithBinaryFile");
            await bootstrapper.InstallTemplateAsync(templateLocation).ConfigureAwait(false);
   
            string output = TestUtils.CreateTemporaryFolder();

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithBinaryFile") }).ConfigureAwait(false);
            var result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "my-test-folder", output, new Dictionary<string, string>()).ConfigureAwait(false);

            string sourceImage = Path.Combine(templateLocation, "image.png");
            string targetImage = Path.Combine(output, "image.png");

            Assert.True(File.Exists(targetImage));

            Assert.Equal(
                new FileInfo(sourceImage).Length,
                new FileInfo(targetImage).Length);
            Assert.True(TestUtils.CompareFiles(sourceImage, targetImage), $"The content of {sourceImage} and {targetImage} is not same.");
        }

#pragma warning disable xUnit1004 // Test methods should not be skipped
        [Fact(Skip = "re-enable after https://github.com/dotnet/templating/issues/3325 is fixed")]
#pragma warning restore xUnit1004 // Test methods should not be skipped
        internal async Task Create_TemplateWithBinaryFile_Package()
        {
            Console.WriteLine(System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
            using Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper();
            string packageLocation = _packageManager.PackTestTemplatesNuGetPackage();
            await bootstrapper.InstallTemplateAsync(packageLocation).ConfigureAwait(false);
            string templateLocation = TestUtils.GetTestTemplateLocation("TemplateWithBinaryFile");

            string output = TestUtils.CreateTemporaryFolder();

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithBinaryFile") }).ConfigureAwait(false);
            var result = await bootstrapper.CreateAsync(foundTemplates[0].Info, "my-test-folder", output, new Dictionary<string, string>()).ConfigureAwait(false);

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
            using Bootstrapper bootstrapper = BootstrapperFactory.GetBootstrapper(loadBuiltInTemplates: true);

            var result1 = await bootstrapper.GetTemplatesAsync(default).ConfigureAwait(false);
            var result2 = await bootstrapper.GetTemplatesAsync(Array.Empty<Func<ITemplateInfo, MatchInfo>>(), cancellationToken: default).ConfigureAwait(false);

            Assert.NotEmpty(result1);
            Assert.Equal(result1.Count, result2.Count);
        }
    }
}
