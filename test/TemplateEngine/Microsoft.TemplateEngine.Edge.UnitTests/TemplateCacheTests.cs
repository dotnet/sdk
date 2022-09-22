// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
#endif
using System.Globalization;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class TemplateCacheTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        public TemplateCacheTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        [Theory]
        [InlineData("en-US", "en")]
        [InlineData("zh-CN", "zh-Hans")]
        [InlineData("zh-SG", "zh-Hans")]
        [InlineData("zh-TW", "zh-Hant")]
        [InlineData("zh-HK", "zh-Hant")]
        [InlineData("zh-MO", "zh-Hant")]
        [InlineData("pt-BR", "pt-BR")]
        [InlineData("pt", null)]
        [InlineData("uk-UA", null)]
        [InlineData("invariant", null)]
        public void PicksCorrectLocator(string currentCulture, string? expectedLocator)
        {
            CultureInfo persistedCulture = CultureInfo.CurrentUICulture;
            try
            {
                if (currentCulture != "invariant")
                {
                    CultureInfo.CurrentUICulture = new CultureInfo(currentCulture);
                }
                else
                {
                    currentCulture = string.Empty;
                    CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                }
                string[] availableLocales = new[] { "cs", "de", "en", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant" };

                ITemplate template = A.Fake<ITemplate>();
                A.CallTo(() => template.Identity).Returns("testIdentity");
                List<ILocalizationLocator> locators = new List<ILocalizationLocator>();
                foreach (string locale in availableLocales)
                {
                    ILocalizationLocator locator = A.Fake<ILocalizationLocator>();
                    A.CallTo(() => locator.Identity).Returns("testIdentity");
                    A.CallTo(() => locator.Locale).Returns(locale);
                    A.CallTo(() => locator.Name).Returns(locale + " name");
                    locators.Add(locator);
                }
                IMountPoint mountPoint = A.Fake<IMountPoint>();
                A.CallTo(() => mountPoint.MountPointUri).Returns("testMount");

                ScanResult result = new ScanResult(mountPoint, new[] { template }, locators, Array.Empty<(string AssemblyPath, Type InterfaceType, IIdentifiedComponent Instance)>());

                TemplateCache templateCache = new TemplateCache(new[] { result }, new Dictionary<string, DateTime>(), NullLogger.Instance);

                Assert.Equal(currentCulture, templateCache.Locale);
                Assert.Equal("testIdentity", templateCache.TemplateInfo.Single().Identity);
                Assert.Equal(string.IsNullOrEmpty(expectedLocator) ? string.Empty : expectedLocator + " name", templateCache.TemplateInfo.Single().Name);
            }
            finally
            {
                CultureInfo.CurrentUICulture = persistedCulture;
            }
        }

        [Fact]
        public void CanHandlePostActions()
        {
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            SettingsFilePaths paths = new SettingsFilePaths(environmentSettings);

            Guid postAction1 = Guid.NewGuid();
            Guid postAction2 = Guid.NewGuid();

            ITemplate template = A.Fake<ITemplate>();
            A.CallTo(() => template.Identity).Returns("testIdentity");
            A.CallTo(() => template.Name).Returns("testName");
            A.CallTo(() => template.ShortNameList).Returns(new[] { "testShort" });
            A.CallTo(() => template.MountPointUri).Returns("testMount");
            A.CallTo(() => template.ConfigPlace).Returns(".template.config/template.json");
            A.CallTo(() => template.PostActions).Returns(new[] { postAction1, postAction2 });
            IMountPoint mountPoint = A.Fake<IMountPoint>();
            A.CallTo(() => mountPoint.MountPointUri).Returns("testMount");

            ScanResult result = new ScanResult(mountPoint, new[] { template }, Array.Empty<ILocalizationLocator>(), Array.Empty<(string AssemblyPath, Type InterfaceType, IIdentifiedComponent Instance)>());
            TemplateCache templateCache = new TemplateCache(new[] { result }, new Dictionary<string, DateTime>(), NullLogger.Instance);

            WriteObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile, templateCache);
            var readCache = new TemplateCache(ReadObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile), NullLogger.Instance);

            Assert.Single(readCache.TemplateInfo);
            var readTemplate = readCache.TemplateInfo[0];
            Assert.Equal(new[] { postAction1, postAction2 }, readTemplate.PostActions);
        }

        [Fact]
        public void CanHandleConstraints()
        {
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            SettingsFilePaths paths = new SettingsFilePaths(environmentSettings);

            TemplateConstraintInfo constraintInfo1 = new TemplateConstraintInfo("t1", null);
            TemplateConstraintInfo constraintInfo2 = new TemplateConstraintInfo("t1", "{[ \"one\", \"two\"]}");

            ITemplate template = A.Fake<ITemplate>();
            A.CallTo(() => template.Identity).Returns("testIdentity");
            A.CallTo(() => template.Name).Returns("testName");
            A.CallTo(() => template.ShortNameList).Returns(new[] { "testShort" });
            A.CallTo(() => template.MountPointUri).Returns("testMount");
            A.CallTo(() => template.ConfigPlace).Returns(".template.config/template.json");
            A.CallTo(() => template.Constraints).Returns(new[] { constraintInfo1, constraintInfo2 });
            IMountPoint mountPoint = A.Fake<IMountPoint>();
            A.CallTo(() => mountPoint.MountPointUri).Returns("testMount");

            ScanResult result = new ScanResult(mountPoint, new[] { template }, Array.Empty<ILocalizationLocator>(), Array.Empty<(string AssemblyPath, Type InterfaceType, IIdentifiedComponent Instance)>());
            TemplateCache templateCache = new TemplateCache(new[] { result }, new Dictionary<string, DateTime>(), NullLogger.Instance);

            WriteObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile, templateCache);
            var readCache = new TemplateCache(ReadObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile), NullLogger.Instance);

            Assert.Single(readCache.TemplateInfo);
            var readTemplate = readCache.TemplateInfo[0];
            Assert.Equal(2, readTemplate.Constraints.Count);
            Assert.Equal("t1", readTemplate.Constraints[0].Type);
            Assert.Equal("t1", readTemplate.Constraints[1].Type);
            Assert.Null(readTemplate.Constraints[0].Args);
            Assert.Equal(constraintInfo2.Args, readTemplate.Constraints[1].Args);
        }

        private static JObject ReadObject(IPhysicalFileSystem fileSystem, string path)
        {
            using (var fileStream = fileSystem.OpenRead(path))
            using (var textReader = new StreamReader(fileStream, System.Text.Encoding.UTF8, true))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                return JObject.Load(jsonReader);
            }
        }

        private static void WriteObject(IPhysicalFileSystem fileSystem, string path, object obj)
        {
            using (var fileStream = fileSystem.CreateFile(path))
            using (var textWriter = new StreamWriter(fileStream, System.Text.Encoding.UTF8))
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(jsonWriter, obj);
            }
        }
    }
}
