// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.TestFramework;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    [TestClass]
    public class DefaultTemplatePackageProviderTests
    {
        // MSTest has no IClassFixture equivalent; a lazily-initialized static helper
        // mirrors the per-class lifetime that xUnit's IClassFixture provides.
        private static readonly Lazy<EnvironmentSettingsHelper> s_environmentSettingsHelper =
            new(() => new EnvironmentSettingsHelper(NullMessageSink.Instance));

        private IEngineEnvironmentSettings _engineEnvironmentSettings = null!;

        public TestContext TestContext { get; set; } = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _engineEnvironmentSettings = s_environmentSettingsHelper.Value.CreateEnvironment(
                hostIdentifier: GetType().Name,
                virtualize: true);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (s_environmentSettingsHelper.IsValueCreated)
            {
                s_environmentSettingsHelper.Value.Dispose();
            }
        }

        [TestMethod]
        public async Task ReturnsFoldersAndNuPkgs()
        {
            string testAssetsDir = SdkTestContext.Current.TestAssetsDirectory;
            string templateEngineTestAssets = Path.Combine(testAssetsDir, "TestPackages", "TemplateEngine");

            //Pass in 5 folders
            var folders = Directory.GetDirectories(Path.Combine(templateEngineTestAssets, "test_templates")).Take(5);
            //And one *.nupkg, but that folder contains 2 .nupkg files
            var nupkgs = new[] { Path.Combine(templateEngineTestAssets, "nupkg_templates", "*.nupkg") };

            var provider = new DefaultTemplatePackageProvider(null!, _engineEnvironmentSettings, nupkgs, folders);
            var sources = await provider.GetAllTemplatePackagesAsync(TestContext.CancellationTokenSource.Token);

            //Total should be 7
            Assert.AreEqual(7, sources.Count);

            Assert.IsTrue(sources[0].LastChangeTime > new DateTime(2000, 1, 1));
            Assert.IsFalse(string.IsNullOrWhiteSpace(sources[0].MountPointUri));
            Assert.AreEqual(provider, sources[0].Provider);
        }
    }
}
