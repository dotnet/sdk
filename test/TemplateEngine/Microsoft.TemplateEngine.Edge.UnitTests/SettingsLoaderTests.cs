using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using AutoFixture;
using AutoFixture.Kernel;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge.Mount.FileSystem;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class SettingsLoaderTests : TestBase
    {
        private readonly IEngineEnvironmentSettings _environmentSettings;
        private readonly MockFileSystem _fileSystem;
        private readonly IFixture _fixture;

        public SettingsLoaderTests()
        {
            _fixture = new Fixture();
            _fixture.Customizations.Add(new TemplateInfoBuilder());

            _fileSystem = new MockFileSystem
            {
                CurrentDirectory = Environment.CurrentDirectory
            };
            _environmentSettings = A.Fake<IEngineEnvironmentSettings>();

            A.CallTo(() => _environmentSettings.Host.FileSystem)
                .Returns(_fileSystem);
            A.CallTo(() => _environmentSettings.Paths.BaseDir)
                .Returns(BaseDir);
        }

        public string BaseDir
        {
            get
            {
                bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                string profileDir = Environment.GetEnvironmentVariable(isWindows
                    ? "USERPROFILE"
                    : "HOME");

                return Path.Combine(profileDir, ".tetestrunner");
            }
        }

        [Fact(DisplayName = nameof(RebuildCacheIfNotCurrentScansAll))]
        public void RebuildCacheIfNotCurrentScansAll()
        {
            _fixture.Customizations.Add(new MountPointInfoBuilder());
            List<MountPointInfo> mountPoints = _fixture.CreateMany<MountPointInfo>().ToList();
            List<TemplateInfo> templates = TemplatesFromMountPoints(mountPoints);

            SetupUserSettings(isCurrentVersion: false, mountPoints: mountPoints);
            SetupTemplates(templates);

            MockMountPointManager mockMountPointManager = new MockMountPointManager(_environmentSettings);
            SettingsLoader subject = new SettingsLoader(_environmentSettings, mockMountPointManager);

            subject.RebuildCacheFromSettingsIfNotCurrent(false);

            // All mount points should have been scanned
            AssertMountPointsWereScanned(mountPoints);
        }

        [Fact(DisplayName = nameof(RebuildCacheSkipsNonAccessibleMounts))]
        public void RebuildCacheSkipsNonAccessibleMounts()
        {
            _fixture.Customizations.Add(new MountPointInfoBuilder());
            List<MountPointInfo> availableMountPoints = _fixture.CreateMany<MountPointInfo>().ToList();
            List<MountPointInfo> unavailableMountPoints = _fixture.CreateMany<MountPointInfo>().ToList();
            List<MountPointInfo> allMountPoints = availableMountPoints.Concat(unavailableMountPoints).ToList();

            List<TemplateInfo> templates = TemplatesFromMountPoints(allMountPoints);

            SetupUserSettings(isCurrentVersion: false, mountPoints: allMountPoints);
            SetupTemplates(templates);

            MockMountPointManager mockMountPointManager = new MockMountPointManager(_environmentSettings);
            mockMountPointManager.UnavailableMountPoints.AddRange(unavailableMountPoints);
            SettingsLoader subject = new SettingsLoader(_environmentSettings, mockMountPointManager);

            subject.RebuildCacheFromSettingsIfNotCurrent(false);

            // All mount points should have been scanned
            AssertMountPointsWereScanned(availableMountPoints);
            AssertMountPointsWereNotScanned(unavailableMountPoints);
        }


        [Fact(DisplayName = nameof(RebuildCacheIfForceRebuildScansAll))]
        public void RebuildCacheIfForceRebuildScansAll()
        {
            _fixture.Customizations.Add(new MountPointInfoBuilder());
            List<MountPointInfo> mountPoints = _fixture.CreateMany<MountPointInfo>().ToList();
            List<TemplateInfo> templates = TemplatesFromMountPoints(mountPoints);

            SetupUserSettings(isCurrentVersion: true, mountPoints: mountPoints);
            SetupTemplates(templates);

            MockMountPointManager mockMountPointManager = new MockMountPointManager(_environmentSettings);
            SettingsLoader subject = new SettingsLoader(_environmentSettings, mockMountPointManager);

            subject.RebuildCacheFromSettingsIfNotCurrent(true);

            // All mount points should have been scanned
            AssertMountPointsWereScanned(mountPoints);
        }

        [Fact(DisplayName = nameof(RebuildCacheFromSettingsOnlyScansOutOfDateFileSystemMountPoints))]
        public void RebuildCacheFromSettingsOnlyScansOutOfDateFileSystemMountPoints()
        {
            _fixture.Customizations.Add(new MountPointInfoBuilder(FileSystemMountPointFactory.FactoryId));
            List<MountPointInfo> mountPoints = _fixture.Build<MountPointInfo>()
                .CreateMany()
                .ToList();
            List<TemplateInfo> templates = TemplatesFromMountPoints(mountPoints);
            
            DateTime oldTimestamp = new DateTime(2018,1,1);
            DateTime recentTimestamp = new DateTime(2018, 9, 28);
            DateTime moreRecentTimestamp = new DateTime(2018, 9, 29);
            foreach (TemplateInfo templateInfo in templates)
            {
                MountPointInfo mountPoint =
                    mountPoints.Single(mp => mp.MountPointId == templateInfo.ConfigMountPointId);

                // The first template has a recent timestamp in the cache, but a more
                // recent one on disk
                templateInfo.ConfigTimestampUtc = templateInfo == templates.First()
                    ? recentTimestamp
                    : oldTimestamp;
                DateTime fileTimestamp = templateInfo == templates.First()
                    ? moreRecentTimestamp
                    : oldTimestamp;

                string pathToTemplateFile = Path.Combine(mountPoint.Place, templateInfo.ConfigPlace.TrimStart('/'));
                _fileSystem.Add(pathToTemplateFile, "{}", lastWriteTime: fileTimestamp);
            }

            SetupUserSettings(isCurrentVersion: true, mountPoints: mountPoints);
            SetupTemplates(templates);

            MockMountPointManager mockMountPointManager = new MockMountPointManager(_environmentSettings);
            SettingsLoader subject = new SettingsLoader(_environmentSettings, mockMountPointManager);

            subject.RebuildCacheFromSettingsIfNotCurrent(false);

            // Only the first mount point should have been scanned
            AssertMountPointsWereScanned(mountPoints.Take(1));
        }

        private void SetupUserSettings(bool isCurrentVersion = true, IEnumerable<MountPointInfo> mountPoints = null)
        {
            SettingsStore userSettings = new SettingsStore();

            if (isCurrentVersion)
            {
                userSettings.SetVersionToCurrent();
            }

            userSettings.MountPoints.AddRange(mountPoints ?? new MountPointInfo[0]);

            JObject serialized = JObject.FromObject(userSettings);
            _fileSystem.Add(Path.Combine(BaseDir, "settings.json"), serialized.ToString());
        }

        private void SetupTemplates(List<TemplateInfo> templates)
        {
            TemplateCache cache = new TemplateCache(_environmentSettings, templates);

            JObject serialized = JObject.FromObject(cache);
            _fileSystem.Add(Path.Combine(BaseDir, "templatecache.json"), serialized.ToString());
        }

        private List<TemplateInfo> TemplatesFromMountPoints(IEnumerable<MountPointInfo> mountPoints)
        {
            return mountPoints.Select(mp => _fixture
                    .Build<TemplateInfo>()
                    .With(x => x.ConfigMountPointId, mp.MountPointId)
                    .Create())
                .ToList();
        }

        private void AssertMountPointsWereScanned(IEnumerable<MountPointInfo> mountPoints)
        {
            string[] expectedScannedDirectories = mountPoints
                .Select(x => x.Place)
                .OrderBy(x => x)
                .ToArray();
            string[] actualScannedDirectories = _fileSystem.DirectoriesScanned
                .Select(dir => Path.Combine(dir.DirectoryName, dir.Pattern))
                .OrderBy(x => x)
                .ToArray();

            Assert.Equal(expectedScannedDirectories, actualScannedDirectories);
        }
        private void AssertMountPointsWereNotScanned(IEnumerable<MountPointInfo> mountPoints)
        {
            IEnumerable <string> expectedScannedDirectories = mountPoints.Select(x => x.Place);
            IEnumerable<string> actualScannedDirectories = _fileSystem.DirectoriesScanned.Select(dir => Path.Combine(dir.DirectoryName, dir.Pattern));
            Assert.Empty(actualScannedDirectories.Intersect(expectedScannedDirectories));
        }

        public class MountPointInfoBuilder : ISpecimenBuilder
        {
            private readonly Guid? _mountPointFactoryId;

            public MountPointInfoBuilder(Guid? mountPointFactoryId = null)
            {
                _mountPointFactoryId = mountPointFactoryId;
            }

            public object Create(object request, ISpecimenContext context)
            {
                if (!(request is ParameterInfo pi))
                {
                    return new NoSpecimen();
                }

                if (pi.Member.DeclaringType == typeof(MountPointInfo) &&
                    pi.ParameterType == typeof(string) &&
                    pi.Name == "place")
                {
                    bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                    if (isWindows)
                    {
                        return Path.Combine(@"C:\", context.Create<string>(), context.Create<string>());
                    }
                    else
                    {
                        return Path.Combine(@"/", context.Create<string>(), context.Create<string>());
                    }    
                }

                if (pi.Member.DeclaringType == typeof(MountPointInfo) &&
                    pi.ParameterType == typeof(Guid) &&
                    pi.Name == "mountPointFactoryId" &&
                    _mountPointFactoryId.HasValue)
                {
                    return _mountPointFactoryId;
                }

                return new NoSpecimen();
            }
        }

        public class TemplateInfoBuilder : ISpecimenBuilder
        {
            public object Create(object request, ISpecimenContext context)
            {
                if (!(request is PropertyInfo pi))
                {
                    return new NoSpecimen();
                }

                if (pi.PropertyType == typeof(IReadOnlyDictionary<string, IBaselineInfo>))
                {
                    return new Dictionary<string, IBaselineInfo>();
                }

                if (pi.PropertyType == typeof(IReadOnlyDictionary<string, ICacheParameter>))
                {
                    return new Dictionary<string, ICacheParameter>();
                }

                if (pi.PropertyType == typeof(IReadOnlyDictionary<string, ICacheTag>))
                {
                    return new Dictionary<string, ICacheTag>();
                }

                return new NoSpecimen();
            }
        }
    }
}
