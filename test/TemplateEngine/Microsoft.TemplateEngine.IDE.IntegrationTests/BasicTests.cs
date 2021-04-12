// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.IDE.IntegrationTests.Utils;
using Microsoft.TemplateEngine.TestHelper;
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
            await bootstrapper.InstallTestTemplateAsync("TemplateWithSourceName").ConfigureAwait(false);

            string output = TestUtils.CreateTemporaryFolder();
            var foundTemplates = await bootstrapper.GetTemplatesAsync(
                new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithSourceName") }).ConfigureAwait(false);
            var result = await bootstrapper.GetCreationEffectsAsync(foundTemplates.First().Info, "test", output, new Dictionary<string, string>(), "").ConfigureAwait(false);
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
            await bootstrapper.InstallTestTemplateAsync("TemplateWithSourceName").ConfigureAwait(false);

            string output = TestUtils.CreateTemporaryFolder();
            var foundTemplates = await bootstrapper.GetTemplatesAsync(
                new[] { WellKnownSearchFilters.NameFilter("TestAssets.TemplateWithSourceName") }).ConfigureAwait(false);

            var result = await bootstrapper.CreateAsync(foundTemplates.First().Info, "test", output, new Dictionary<string, string>(), false, "").ConfigureAwait(false);

            Assert.Equal(2, result.PrimaryOutputs.Count);
            Assert.Equal(0, result.PostActions.Count);
            Assert.True(File.Exists(Path.Combine(output, "test.cs")));
            Assert.True(File.Exists(Path.Combine(output, "test/test.cs")));
        }

        [Fact]
        internal async Task GetCreationEffects_BasicTest_Package()
        {
            var bootstrapper = BootstrapperFactory.GetBootstrapper();
            string packageLocation = _packageManager.PackProjectTemplatesNuGetPackage("Microsoft.DotNet.Common.ProjectTemplates.5.0");
            await bootstrapper.InstallTemplateAsync(packageLocation).ConfigureAwait(false);

            string output = TestUtils.CreateTemporaryFolder();

            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("console") }).ConfigureAwait(false);
            var result = await bootstrapper.GetCreationEffectsAsync(foundTemplates.First().Info, "test", output, new Dictionary<string, string>(), "").ConfigureAwait(false);
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
            string packageLocation = _packageManager.PackProjectTemplatesNuGetPackage("Microsoft.DotNet.Common.ProjectTemplates.5.0");
            await bootstrapper.InstallTemplateAsync(packageLocation).ConfigureAwait(false);

            string output = TestUtils.CreateTemporaryFolder();
            var foundTemplates = await bootstrapper.GetTemplatesAsync(new[] { WellKnownSearchFilters.NameFilter("console") }).ConfigureAwait(false);
            var result = await bootstrapper.CreateAsync(foundTemplates.First().Info, "test", output, new Dictionary<string, string>(), false, "").ConfigureAwait(false);
            Assert.Equal(2, result.PrimaryOutputs.Count);
            Assert.Equal(2, result.PostActions.Count);

            Assert.True(File.Exists(Path.Combine(output, "Program.cs")));
            Assert.True(File.Exists(Path.Combine(output, "test.csproj")));
        }
    }
}
