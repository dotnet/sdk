// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
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
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
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

                IScanTemplateInfo template = A.Fake<IScanTemplateInfo>();
                A.CallTo(() => template.Identity).Returns("testIdentity");
                List<ILocalizationLocator> locators = new List<ILocalizationLocator>();
                foreach (string locale in availableLocales)
                {
                    ILocalizationLocator locator = A.Fake<ILocalizationLocator>();
#pragma warning disable CS0618 // Type or member is obsolete
                    A.CallTo(() => locator.Identity).Returns("testIdentity");
#pragma warning restore CS0618 // Type or member is obsolete
                    A.CallTo(() => locator.Locale).Returns(locale);
                    A.CallTo(() => locator.Name).Returns(locale + " name");
                    locators.Add(locator);
                }
                A.CallTo(() => template.Localizations).Returns(locators.ToDictionary(l => l.Locale, l => l));
                IMountPoint mountPoint = A.Fake<IMountPoint>();
                A.CallTo(() => mountPoint.MountPointUri).Returns("testMount");

                ScanResult result = new ScanResult(mountPoint, new[] { template }, locators, Array.Empty<(string AssemblyPath, Type InterfaceType, IIdentifiedComponent Instance)>());

                TemplateCache templateCache = new TemplateCache(Array.Empty<ITemplatePackage>(), new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

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

            var template = GetFakedTemplate("testIdentity", "testMount", "testName");
            A.CallTo(() => template.PostActions).Returns(new[] { postAction1, postAction2 });
            IMountPoint mountPoint = A.Fake<IMountPoint>();
            A.CallTo(() => mountPoint.MountPointUri).Returns("testMount");

            ScanResult result = new ScanResult(mountPoint, new[] { template }, Array.Empty<ILocalizationLocator>(), Array.Empty<(string AssemblyPath, Type InterfaceType, IIdentifiedComponent Instance)>());
            TemplateCache templateCache = new TemplateCache(Array.Empty<ITemplatePackage>(), new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

            WriteObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile, templateCache);
            var readCache = new TemplateCache(ReadObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile));

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

            var template = GetFakedTemplate("testIdentity", "testMount", "testName");
            A.CallTo(() => template.Constraints).Returns(new[] { constraintInfo1, constraintInfo2 });
            IMountPoint mountPoint = A.Fake<IMountPoint>();
            A.CallTo(() => mountPoint.MountPointUri).Returns("testMount");

            ScanResult result = new ScanResult(mountPoint, new[] { template }, Array.Empty<ILocalizationLocator>(), Array.Empty<(string AssemblyPath, Type InterfaceType, IIdentifiedComponent Instance)>());
            TemplateCache templateCache = new TemplateCache(Array.Empty<ITemplatePackage>(), new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

            WriteObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile, templateCache);
            var readCache = new TemplateCache(ReadObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile));

            Assert.Single(readCache.TemplateInfo);
            var readTemplate = readCache.TemplateInfo[0];
            Assert.Equal(2, readTemplate.Constraints.Count);
            Assert.Equal("t1", readTemplate.Constraints[0].Type);
            Assert.Equal("t1", readTemplate.Constraints[1].Type);
            Assert.Null(readTemplate.Constraints[0].Args);
            Assert.Equal(constraintInfo2.Args, readTemplate.Constraints[1].Args);
        }

        [Fact]
        public void CanHandleParameters()
        {
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            SettingsFilePaths paths = new SettingsFilePaths(environmentSettings);

            ITemplateParameter param1 = new TemplateParameter("param1", "parameter", "string");
            ITemplateParameter param2 = new TemplateParameter("param2", "parameter", "string", new TemplateParameterPrecedence(PrecedenceDefinition.ConditionalyRequired, isRequiredCondition: "param1 == \"foo\""));
            ITemplateParameter param3 = new TemplateParameter(
                "param3",
                "parameter",
                "choice",
                new TemplateParameterPrecedence(PrecedenceDefinition.Required),
                defaultValue: "def",
                defaultIfOptionWithoutValue: "def-no-value",
                description: "desc",
                displayName: "displ",
                allowMultipleValues: true,
                choices: new Dictionary<string, ParameterChoice>()
                {
                    { "ch1", new ParameterChoice("ch1-displ", "ch1-desc") },
                    { "ch2", new ParameterChoice("ch2-displ", "ch2-desc") },
                });

            var template = GetFakedTemplate("testIdentity", "testMount", "testName");
            A.CallTo(() => template.ParameterDefinitions).Returns(new ParameterDefinitionSet(new[] { param1, param2, param3 }));
            IMountPoint mountPoint = A.Fake<IMountPoint>();
            A.CallTo(() => mountPoint.MountPointUri).Returns("testMount");

            ScanResult result = new ScanResult(mountPoint, new[] { template }, Array.Empty<ILocalizationLocator>(), Array.Empty<(string AssemblyPath, Type InterfaceType, IIdentifiedComponent Instance)>());
            TemplateCache templateCache = new TemplateCache(Array.Empty<ITemplatePackage>(), new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

            WriteObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile, templateCache);
            var readCache = new TemplateCache(ReadObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile));

            Assert.Single(readCache.TemplateInfo);
            var readTemplate = readCache.TemplateInfo[0];
            Assert.Equal(3, readTemplate.ParameterDefinitions.Count);
            Assert.True(readTemplate.ParameterDefinitions.ContainsKey("param1"));
            Assert.Equal(PrecedenceDefinition.Optional, readTemplate.ParameterDefinitions["param1"].Precedence.PrecedenceDefinition);
            Assert.True(readTemplate.ParameterDefinitions.ContainsKey("param2"));
            Assert.Equal("string", readTemplate.ParameterDefinitions["param2"].DataType);
            Assert.Equal("param1 == \"foo\"", readTemplate.ParameterDefinitions["param2"].Precedence.IsRequiredCondition);
            Assert.True(readTemplate.ParameterDefinitions.ContainsKey("param3"));
            Assert.Equal("choice", readTemplate.ParameterDefinitions["param3"].DataType);
            Assert.Equal(PrecedenceDefinition.Required, readTemplate.ParameterDefinitions["param3"].Precedence.PrecedenceDefinition);
            Assert.Equal("def", readTemplate.ParameterDefinitions["param3"].DefaultValue);
            Assert.Equal("def-no-value", readTemplate.ParameterDefinitions["param3"].DefaultIfOptionWithoutValue);
            Assert.Equal("desc", readTemplate.ParameterDefinitions["param3"].Description);
            Assert.Equal("displ", readTemplate.ParameterDefinitions["param3"].DisplayName);
            Assert.True(readTemplate.ParameterDefinitions["param3"].AllowMultipleValues);
            Assert.Equal(2, readTemplate.ParameterDefinitions["param3"].Choices!.Count);
        }

        [Theory]
        [InlineData(true, "defaultName")]
        [InlineData(true, null)]
        [InlineData(false, "anotherDefault")]
        public void CanHandleDefaultName(bool preferDefaultName, string? defaultName)
        {
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            SettingsFilePaths paths = new SettingsFilePaths(environmentSettings);

            var template = GetFakedTemplate("testIdentity", "testMount", "testName");
            A.CallTo(() => template.PreferDefaultName).Returns(preferDefaultName);
            A.CallTo(() => template.DefaultName).Returns(defaultName);
            IMountPoint mountPoint = A.Fake<IMountPoint>();
            A.CallTo(() => mountPoint.MountPointUri).Returns("testMount");

            ScanResult result = new ScanResult(mountPoint, new[] { template }, Array.Empty<ILocalizationLocator>(), Array.Empty<(string AssemblyPath, Type InterfaceType, IIdentifiedComponent Instance)>());
            TemplateCache templateCache = new TemplateCache(Array.Empty<ITemplatePackage>(), new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

            WriteObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile, templateCache);
            var readCache = new TemplateCache(ReadObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile));

            Assert.Single(readCache.TemplateInfo);
            var readTemplate = readCache.TemplateInfo[0];
            Assert.Equal(preferDefaultName, readTemplate.PreferDefaultName);
            Assert.Equal(defaultName, readTemplate.DefaultName);
        }

        [Fact]
        public void ProducesCorrectWarningOnOverlappingIdentity_ManagedCandidatesOnly()
        {
            List<(LogLevel, string)> loggedMessages = new List<(LogLevel, string)>();
            InMemoryLoggerProvider loggerProvider = new InMemoryLoggerProvider(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });
            var overlappingIdentity = "testIdentity";

            var templateA = GetFakedTemplate(overlappingIdentity, "testMountA", "TemplateA");
            var managedTPA = GetFakedManagedTemplatePackage("testMountA", "PackageA");

            var templateB = GetFakedTemplate(overlappingIdentity, "testMountB", "TemplateB");
            var managedTPB = GetFakedManagedTemplatePackage("testMountB", "PackageB");

            var templateC = GetFakedTemplate(overlappingIdentity, "testMountC", "TemplateC");
            var managedTPC = GetFakedManagedTemplatePackage("testMountC", "PackageC");

            var expectedOutput = "The following templates use the same identity 'testIdentity':" +
                $"{Environment.NewLine}  • 'TemplateA' from 'PackageA'" +
                $"{Environment.NewLine}  • 'TemplateB' from 'PackageB'" +
                $"{Environment.NewLine}  • 'TemplateC' from 'PackageC'" +
                $"{Environment.NewLine}The template from 'TemplateC' will be used. To resolve this conflict, uninstall the conflicting template packages.";

            ScanResult result = new ScanResult(A.Fake<IMountPoint>(), new[] { templateA, templateB, templateC }, Array.Empty<ILocalizationLocator>(), Array.Empty<(string AssemblyPath, Type InterfaceType, IIdentifiedComponent Instance)>());
            _ = new TemplateCache(new[] { managedTPA, managedTPB, managedTPC }, new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

            var warningMessages = loggedMessages.Where(log => log.Item1 == LogLevel.Warning);
            Assert.Single(warningMessages);
            Assert.Contains(expectedOutput, warningMessages.Single().Item2);
        }

        [Fact]
        public void ProducesCorrectOutputOnOverlappingIdentity_ManagedAndUnmanagedCandidates()
        {
            List<(LogLevel, string)> loggedMessages = new List<(LogLevel, string)>();
            InMemoryLoggerProvider loggerProvider = new InMemoryLoggerProvider(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });
            var overlappingIdentity = "testIdentity";

            var templateA = GetFakedTemplate(overlappingIdentity, "testMountA", "TemplateA");
            var managedTPA = GetFakedManagedTemplatePackage("testMountA", "PackageA");

            var templateB = GetFakedTemplate(overlappingIdentity, "testMountB", "TemplateB");
            var managedTPB = GetFakedTemplatePackage("testMountB");

            var templateC = GetFakedTemplate(overlappingIdentity, "testMountC", "TemplateC");
            var managedTPC = GetFakedManagedTemplatePackage("testMountC", "PackageC");

            var expectedOutput = "The following templates use the same identity 'testIdentity':" +
                $"{Environment.NewLine}  • 'TemplateA' from 'PackageA'" +
                $"{Environment.NewLine}  • 'TemplateC' from 'PackageC'" +
                $"{Environment.NewLine}The template from 'TemplateC' will be used. To resolve this conflict, uninstall the conflicting template packages.";

            ScanResult result = new ScanResult(A.Fake<IMountPoint>(), new[] { templateA, templateB, templateC }, Array.Empty<ILocalizationLocator>(), Array.Empty<(string AssemblyPath, Type InterfaceType, IIdentifiedComponent Instance)>());
            _ = new TemplateCache(new[] { managedTPA, managedTPB, managedTPC }, new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

            var warningMessages = loggedMessages.Where(log => log.Item1 == LogLevel.Warning);
            Assert.Single(warningMessages);
            Assert.Contains(expectedOutput, warningMessages.Single().Item2);
        }

        [Fact]
        public void NoOutputOnOverlappingIdentity_UnmanagedCandidateWins()
        {
            List<(LogLevel, string)> loggedMessages = new List<(LogLevel, string)>();
            InMemoryLoggerProvider loggerProvider = new InMemoryLoggerProvider(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });
            var overlappingIdentity = "testIdentity";

            var templateA = GetFakedTemplate(overlappingIdentity, "testMountA", "TemplateA");
            var managedTPA = GetFakedManagedTemplatePackage("testMountA", "PackageA");

            var templateB = GetFakedTemplate(overlappingIdentity, "testMountB", "TemplateB");
            var managedTPB = GetFakedManagedTemplatePackage("testMountB", "PackageB");

            var templateC = GetFakedTemplate(overlappingIdentity, "testMountC", "TemplateC");
            var managedTPC = GetFakedTemplatePackage("testMountC");

            ScanResult result = new ScanResult(A.Fake<IMountPoint>(), new[] { templateA, templateB, templateC }, Array.Empty<ILocalizationLocator>(), Array.Empty<(string AssemblyPath, Type InterfaceType, IIdentifiedComponent Instance)>());
            _ = new TemplateCache(new[] { managedTPA, managedTPB, managedTPC }, new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

            var warningMessages = loggedMessages.Where(log => log.Item1 == LogLevel.Warning);
            Assert.Empty(warningMessages);
        }

        [Fact]
        public void CanHandleHostData()
        {
            IEngineEnvironmentSettings environmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            SettingsFilePaths paths = new SettingsFilePaths(environmentSettings);

            string hostfile =
                /*lang=json,strict*/
                """
                   {
                      "$schema": "http://json.schemastore.org/dotnetcli.host",
                      "symbolInfo": {
                        "useArtifacts": {
                          "longName": "use-artifacts",
                          "shortName": ""
                        },
                        "inherit": {
                          "shortName": ""
                        }
                      }
                    }
                """;

            string hostFileFormatted = JObject.Parse(hostfile).ToString(Formatting.None);
            const string hostFileLocation = ".template.config/dotnetcli.host.json";

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { hostFileLocation, hostfile }
            };
            string sourceBasePath = environmentSettings.GetTempVirtualizedPath();
            TestFileSystemUtils.WriteTemplateSource(environmentSettings, sourceBasePath, templateSourceFiles);

            var template = GetFakedTemplate("testIdentity", sourceBasePath, "testName");
            A.CallTo(() => template.HostConfigFiles).Returns(new Dictionary<string, string>() { { "dotnetcli", hostFileLocation } });
            using IMountPoint sourceMountPoint = environmentSettings.MountPath(sourceBasePath);
            A.CallTo(() => template.GeneratorId).Returns(new("0C434DF7-E2CB-4DEE-B216-D7C58C8EB4B3")); // runnable projects generator ID

            ScanResult result = new ScanResult(sourceMountPoint, new[] { template }, Array.Empty<ILocalizationLocator>(), Array.Empty<(string AssemblyPath, Type InterfaceType, IIdentifiedComponent Instance)>());
            TemplateCache templateCache = new TemplateCache(Array.Empty<ITemplatePackage>(), new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

            Assert.Equal(hostFileLocation, templateCache.TemplateInfo[0].HostConfigPlace);
            Assert.Equal(hostFileFormatted, templateCache.TemplateInfo[0].HostData);

            WriteObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile, templateCache);
            var readCache = new TemplateCache(ReadObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile));

            Assert.Single(readCache.TemplateInfo);
            var readTemplate = readCache.TemplateInfo[0];
            Assert.Equal(hostFileLocation, readTemplate.HostConfigPlace);
            Assert.Equal(hostFileFormatted, readTemplate.HostData);
        }

        private IScanTemplateInfo GetFakedTemplate(string identity, string mountPointUri, string name)
        {
            IScanTemplateInfo template = A.Fake<IScanTemplateInfo>();
            A.CallTo(() => template.Identity).Returns(identity);
            A.CallTo(() => template.MountPointUri).Returns(mountPointUri);
            A.CallTo(() => template.Name).Returns(name);
            A.CallTo(() => template.ConfigPlace).Returns(".template.config/template.json");
            A.CallTo(() => template.ShortNameList).Returns(new[] { "testShort" });

            return template;
        }

        private IManagedTemplatePackage GetFakedManagedTemplatePackage(string mountPointUri, string displayName)
        {
            var managedTemplatePackage = A.Fake<IManagedTemplatePackage>();
            A.CallTo(() => managedTemplatePackage.MountPointUri).Returns(mountPointUri);
            A.CallTo(() => managedTemplatePackage.DisplayName).Returns(displayName);

            return managedTemplatePackage;
        }

        private ITemplatePackage GetFakedTemplatePackage(string mountPointUri)
        {
            var managedTemplatePackage = A.Fake<ITemplatePackage>();
            A.CallTo(() => managedTemplatePackage.MountPointUri).Returns(mountPointUri);

            return managedTemplatePackage;
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
