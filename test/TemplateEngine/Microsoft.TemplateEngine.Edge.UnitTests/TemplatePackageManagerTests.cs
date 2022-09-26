// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class TemplatePackageManagerTests : TestBase, IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        public TemplatePackageManagerTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        [Fact]
        public async Task OrderOfScanningIsCorrect()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment();
            var workingDir = TestUtils.CreateTemporaryFolder("workingDir");
            var folders = new List<string>();

            for (int i = 0; i < 100; i++)
            {
                var folderPath = Path.Combine(workingDir, $"Folder{i}");
                folders.Add(folderPath);
                engineEnvironmentSettings.Host.FileSystem.CreateDirectory(Path.Combine(folderPath, ".template.config"));
                engineEnvironmentSettings.Host.FileSystem.WriteAllText(
                    Path.Combine(folderPath, ".template.config", "template.json"),
                    $"{{ \"identity\": \"AllHaveSameIdentity\", \"shortName\": \"sample{i}\", \"name\": \"sample name {i}\"}}");
            }

            FakeFactory.SetNuPkgsAndFolders(folders: folders);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplatePackageProviderFactory), new FakeFactory());
            var templates = await new TemplatePackageManager(engineEnvironmentSettings).GetTemplatesAsync(default)
                .ConfigureAwait(false);

            Assert.Equal(1, templates.Count);
            Assert.Equal("sample99", templates.Single().ShortNameList[0]);
        }

        [Fact]
        public async Task OrderOfScanningIsCorrectWithPriority()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment();
            var workingDir = TestUtils.CreateTemporaryFolder("workingDir");
            var folders = new List<string>();

            for (int i = 0; i < 100; i++)
            {
                var folderPath = Path.Combine(workingDir, $"Folder{i}");
                folders.Add(folderPath);
                engineEnvironmentSettings.Host.FileSystem.CreateDirectory(Path.Combine(folderPath, ".template.config"));
                engineEnvironmentSettings.Host.FileSystem.WriteAllText(
                    Path.Combine(folderPath, ".template.config", "template.json"),
                    $"{{ \"identity\": \"AllHaveSameIdentity\", \"shortName\": \"sample{i}\", \"name\": \"sample name {i}\"}}");
            }

            FakeFactoryWithPriority.StaticPriority = 100;
            FakeFactoryWithPriority.SetNuPkgsAndFolders(folders: folders.Take(50));
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplatePackageProviderFactory), new FakeFactoryWithPriority());

            FakeFactory.SetNuPkgsAndFolders(folders: folders.Skip(50).Take(50));
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplatePackageProviderFactory), new FakeFactory());

            var templates = await new TemplatePackageManager(engineEnvironmentSettings).GetTemplatesAsync(default)
                .ConfigureAwait(false);

            Assert.Equal(1, templates.Count);
            Assert.Equal("sample49", templates.Single().ShortNameList[0]);
        }

        [Fact]
        public async Task RebuildCacheIfNotCurrentScansAll()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment();

            var nupkgFolder = TestTemplatePackagesLocation;
            var nupkgsWildcard = new[] { Path.Combine(nupkgFolder, "*.nupkg") };

            FakeFactory.SetNuPkgsAndFolders(nupkgsWildcard);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplatePackageProviderFactory), new FakeFactory());
            await new TemplatePackageManager(engineEnvironmentSettings).GetTemplatesAsync(default).ConfigureAwait(false);

            var allNupkgs = Directory.GetFiles(nupkgFolder);
            // All mount points should have been scanned
            AssertMountPointsWereOpened(allNupkgs, engineEnvironmentSettings);
        }

        [Fact]
        public async Task EnsureCacheIsLoadedOnlyOnce()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment();
            var monitoredFileSystem = (MonitoredFileSystem)engineEnvironmentSettings.Host.FileSystem;

            var nupkgFolder = TestTemplatePackagesLocation;
            var nupkgsWildcard = new[] { Path.Combine(nupkgFolder, "*.nupkg") };

            FakeFactory.SetNuPkgsAndFolders(nupkgsWildcard);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplatePackageProviderFactory), new FakeFactory());

            // Execute 1st time to create file on disk
            monitoredFileSystem.Reset();
            var templatePackageManager1 = new TemplatePackageManager(engineEnvironmentSettings);
            await templatePackageManager1.GetTemplatesAsync(default).ConfigureAwait(false);
            Assert.Contains(new SettingsFilePaths(engineEnvironmentSettings).TemplateCacheFile, monitoredFileSystem.FilesOpened);
            // All mount points should have been scanned
            AssertMountPointsWereOpened(Directory.GetFiles(nupkgFolder), engineEnvironmentSettings);

            // Execute 2st time with different templatePackageManager to load existing cached created in 1st step
            monitoredFileSystem.Reset();
            var templatePackageManager2 = new TemplatePackageManager(engineEnvironmentSettings);
            await templatePackageManager2.GetTemplatesAsync(default).ConfigureAwait(false);
            Assert.Contains(new SettingsFilePaths(engineEnvironmentSettings).TemplateCacheFile, monitoredFileSystem.FilesOpened);
            // No mount points should have been scanned
            AssertMountPointsWereOpened(Array.Empty<string>(), engineEnvironmentSettings);

            // Execute 3rd time with same templatePackageManager to test that TemplateCacheFile is not parsed.
            monitoredFileSystem.Reset();
            await templatePackageManager2.GetTemplatesAsync(default).ConfigureAwait(false);
            Assert.DoesNotContain(new SettingsFilePaths(engineEnvironmentSettings).TemplateCacheFile, monitoredFileSystem.FilesOpened);
            // No mount points should have been scanned
            AssertMountPointsWereOpened(Array.Empty<string>(), engineEnvironmentSettings);
        }

        [Fact]
        public async Task RebuildCacheSkipsNonAccessibleMounts()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment();
            var nupkgFolder = TestTemplatePackagesLocation;
            var validAndInvalidNuPkg = new[] { Directory.GetFiles(nupkgFolder, "*.nupkg")[0], Path.Combine(nupkgFolder, $"{default(Guid)}.nupkg") };

            FakeFactory.SetNuPkgsAndFolders(validAndInvalidNuPkg);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplatePackageProviderFactory), new FakeFactory());
            await new TemplatePackageManager(engineEnvironmentSettings).GetTemplatesAsync(default).ConfigureAwait(false);

            var allNupkgs = validAndInvalidNuPkg.Take(1);
            // All mount points should have been scanned
            AssertMountPointsWereOpened(allNupkgs, engineEnvironmentSettings);
        }

        [Fact]
        public async Task RebuildCacheIfForceRebuildScansAll()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment();

            var nupkgFolder = TestTemplatePackagesLocation;
            var nupkgsWildcard = new[] { Path.Combine(nupkgFolder, "*.nupkg") };

            FakeFactory.SetNuPkgsAndFolders(nupkgsWildcard);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplatePackageProviderFactory), new FakeFactory());
            TemplatePackageManager templatePackageManager = new TemplatePackageManager(engineEnvironmentSettings);
            await templatePackageManager.GetTemplatesAsync(default).ConfigureAwait(false);

            var allNupkgs = Directory.GetFiles(nupkgFolder);
            // All mount points should have been scanned
            AssertMountPointsWereOpened(allNupkgs, engineEnvironmentSettings);

            var monitoredFileSystem = (MonitoredFileSystem)engineEnvironmentSettings.Host.FileSystem;

            monitoredFileSystem.Reset();
            await templatePackageManager.GetTemplatesAsync(default).ConfigureAwait(false);
            // Make sure that we don't rescan with force=false
            AssertMountPointsWereOpened(Array.Empty<string>(), engineEnvironmentSettings);

            monitoredFileSystem.Reset();
            await templatePackageManager.RebuildTemplateCacheAsync(default).ConfigureAwait(false);
            // Make sure that we rescan with force=false
            AssertMountPointsWereOpened(allNupkgs, engineEnvironmentSettings);

            monitoredFileSystem.Reset();
            await templatePackageManager.GetTemplatesAsync(default).ConfigureAwait(false);
            // Make sure that we don't rescan with force=false
            AssertMountPointsWereOpened(Array.Empty<string>(), engineEnvironmentSettings);
        }

        [Fact]
        public async Task EnsureCacheRoundtripPreservesTemplateWithLocaleTimestamp()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment("en-GB");

            var nupkgFolder = TestTemplatePackagesLocation;
            var nupkgsWildcard = new[] { Path.Combine(nupkgFolder, "*.nupkg") };

            FakeFactory.SetNuPkgsAndFolders(nupkgsWildcard);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplatePackageProviderFactory), new FakeFactory());
            TemplatePackageManager templatePackageManager = new TemplatePackageManager(engineEnvironmentSettings);
            await templatePackageManager.GetTemplatesAsync(default).ConfigureAwait(false);

            var allNupkgs = Directory.GetFiles(nupkgFolder);
            // All mount points should have been scanned
            AssertMountPointsWereOpened(allNupkgs, engineEnvironmentSettings);

            var monitoredFileSystem = (MonitoredFileSystem)engineEnvironmentSettings.Host.FileSystem;

            FakeFactory.TriggerChanged();

            monitoredFileSystem.Reset();
            await templatePackageManager.GetTemplatesAsync(default).ConfigureAwait(false);
            // Make sure that we don't rescan with force=false
            AssertMountPointsWereOpened(Array.Empty<string>(), engineEnvironmentSettings);
        }

        [Fact]
        public async Task RemoveMountpointRemovesTemplates()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment();

            var nupkgFolder = TestTemplatePackagesLocation;
            var allNupkgs = Directory.GetFiles(nupkgFolder).Select(Path.GetFullPath).ToList();

            FakeFactory.SetNuPkgsAndFolders(allNupkgs);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplatePackageProviderFactory), new FakeFactory());
            TemplatePackageManager templatePackageManager = new TemplatePackageManager(engineEnvironmentSettings);
            var templatesAll = await templatePackageManager.GetTemplatesAsync(default).ConfigureAwait(false);

            // All mount points should have been scanned
            AssertMountPointsWereOpened(allNupkgs, engineEnvironmentSettings);

            // All mountpoints/sources should have at least 1 template
            Assert.Equal(allNupkgs.OrderBy(m => m), templatesAll.Select(t => t.MountPointUri).Distinct().OrderBy(m => m));

            var monitoredFileSystem = (MonitoredFileSystem)engineEnvironmentSettings.Host.FileSystem;

            monitoredFileSystem.Reset();

            //Remove all but 1
            allNupkgs.RemoveRange(1, allNupkgs.Count - 1);
            FakeFactory.TriggerChanged();

            var templatesOnly1 = await templatePackageManager.GetTemplatesAsync(default).ConfigureAwait(false);

            // Make sure that templates return only have MountPointUri of our remaining nupkg
            Assert.Equal(allNupkgs, templatesOnly1.Select(t => t.MountPointUri).Distinct().OrderBy(m => m));
        }

        [Fact]
        public async Task CanSkipFaultedProvider()
        {
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment();
            FakeFactory.SetNuPkgsAndFolders(folders: new[] { TestTemplatesLocation });
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplatePackageProviderFactory), new FakeFactory());
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplatePackageProviderFactory), new FaultFakeFactory());

            TemplatePackageManager templatePackageManager = new TemplatePackageManager(engineEnvironmentSettings);
            var templates = await templatePackageManager.GetTemplatesAsync(default).ConfigureAwait(false);
            Assert.NotEmpty(templates);
        }

        private void AssertMountPointsWereScanned(IEnumerable<string> mountPoints, IEngineEnvironmentSettings environmentSettings)
        {
            string[] expectedScannedDirectories = mountPoints
                .Select(x => x)
                .OrderBy(x => x)
                .ToArray();
            string[] actualScannedDirectories = ((MonitoredFileSystem)environmentSettings.Host.FileSystem).DirectoriesScanned
                .Select(dir => Path.Combine(dir.DirectoryName, dir.Pattern))
                .OrderBy(x => x)
                .ToArray();

            Assert.Equal(expectedScannedDirectories, actualScannedDirectories);
        }

        private void AssertMountPointsWereOpened(IEnumerable<string> mountPoints, IEngineEnvironmentSettings environmentSettings)
        {
            string[] expectedScannedDirectories = mountPoints
                .Select(Path.GetFullPath)
                .OrderBy(x => x)
                .ToArray();
            string[] actualScannedDirectories = ((MonitoredFileSystem)environmentSettings.Host.FileSystem).FilesOpened
                .Where((f) => Path.GetExtension(f) == ".nupkg") // Filter to only check for .nupkgs, and ignore templatecache.json and others...
                .Select(Path.GetFullPath)
                .OrderBy(x => x)
                .ToArray();

            Assert.Equal(expectedScannedDirectories, actualScannedDirectories);
        }

        private void AssertMountPointsWereNotScanned(IEnumerable<string> mountPoints, IEngineEnvironmentSettings environmentSettings)
        {
            IEnumerable<string> expectedScannedDirectories = mountPoints;
            IEnumerable<string> actualScannedDirectories = ((MonitoredFileSystem)environmentSettings.Host.FileSystem).DirectoriesScanned.Select(dir => Path.Combine(dir.DirectoryName, dir.Pattern));
            Assert.Empty(actualScannedDirectories.Intersect(expectedScannedDirectories));
        }

        private class FakeFactory : ITemplatePackageProviderFactory
        {
            private static readonly List<WeakReference<DefaultTemplatePackageProvider>> AllCreatedProviders = new List<WeakReference<DefaultTemplatePackageProvider>>();

            public string DisplayName => nameof(FakeFactory);

            public Guid Id { get; } = new Guid("{61CFA828-97B6-44EB-A44D-0AE673D6DF52}");

            private static IEnumerable<string>? Folders { get; set; }

            private static IEnumerable<string>? NuPkgs { get; set; }

            public static void SetNuPkgsAndFolders(IEnumerable<string>? nupkgs = null, IEnumerable<string>? folders = null)
            {
                NuPkgs = nupkgs;
                Folders = folders;
            }

            public static void TriggerChanged()
            {
                foreach (var provider in AllCreatedProviders)
                {
                    if (provider.TryGetTarget(out var actualProvider))
                    {
                        actualProvider.UpdatePackages(NuPkgs, Folders);
                    }
                }
            }

            public ITemplatePackageProvider CreateProvider(IEngineEnvironmentSettings settings)
            {
                var defaultTemplatePackageProvider = new DefaultTemplatePackageProvider(this, settings, NuPkgs, Folders);
                AllCreatedProviders.Add(new WeakReference<DefaultTemplatePackageProvider>(defaultTemplatePackageProvider));
                return defaultTemplatePackageProvider;
            }
        }

        private class FaultFakeFactory : ITemplatePackageProviderFactory
        {
            public string DisplayName => nameof(FaultFakeFactory);

            public Guid Id { get; } = new Guid("{61CFA828-97B6-44EB-A44D-0AE673D6DF53}");

            public ITemplatePackageProvider CreateProvider(IEngineEnvironmentSettings settings)
            {
                return new FaultProvider(this);
            }

            private class FaultProvider : ITemplatePackageProvider
            {
                public FaultProvider(ITemplatePackageProviderFactory factory)
                {
                    Factory = factory;
                }

                public ITemplatePackageProviderFactory Factory { get; }

                public event Action? TemplatePackagesChanged
                {
                    add { }
                    remove { }
                }

                public Task<IReadOnlyList<ITemplatePackage>> GetAllTemplatePackagesAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
            }
        }

        private class FakeFactoryWithPriority : ITemplatePackageProviderFactory, IPrioritizedComponent
        {
            private static readonly List<WeakReference<DefaultTemplatePackageProvider>> AllCreatedProviders = new();

            public string DisplayName => nameof(FakeFactory);

            public Guid Id { get; } = new Guid("{D98CAC97-2474-48B2-AE8D-B665D9E79C66}");

            private static IEnumerable<string>? Folders { get; set; }

            private static IEnumerable<string>? NuPkgs { get; set; }

            public static void SetNuPkgsAndFolders(IEnumerable<string>? nupkgs = null, IEnumerable<string>? folders = null)
            {
                NuPkgs = nupkgs;
                Folders = folders;
            }

            public static void TriggerChanged()
            {
                foreach (var provider in AllCreatedProviders)
                {
                    if (provider.TryGetTarget(out var actualProvider))
                    {
                        actualProvider.UpdatePackages(NuPkgs, Folders);
                    }
                }
            }

            public static int StaticPriority { get; set; }

            public ITemplatePackageProvider CreateProvider(IEngineEnvironmentSettings settings)
            {
                var defaultTemplatePackageProvider = new DefaultTemplatePackageProvider(this, settings, NuPkgs, Folders);
                AllCreatedProviders.Add(new WeakReference<DefaultTemplatePackageProvider>(defaultTemplatePackageProvider));
                return defaultTemplatePackageProvider;
            }

            public int Priority => StaticPriority;
        }
    }
}
