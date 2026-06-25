// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
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

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    [TestClass]
    public class TemplateCacheTests
    {
        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper(NullMessageSink.Instance);

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        [TestMethod]
        [DataRow("en-US", "en")]
        [DataRow("zh-CN", "zh-Hans")]
        [DataRow("zh-SG", "zh-Hans")]
        [DataRow("zh-TW", "zh-Hant")]
        [DataRow("zh-HK", "zh-Hant")]
        [DataRow("zh-MO", "zh-Hant")]
        [DataRow("pt-BR", "pt-BR")]
        [DataRow("pt", null)]
        [DataRow("uk-UA", null)]
        [DataRow("invariant", null)]
        public void PicksCorrectLocator(string currentCulture, string? expectedLocator)
        {
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
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

                ScanResult result = new ScanResult(mountPoint, new[] { template }, locators, []);

                TemplateCache templateCache = new TemplateCache([], new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

                Assert.AreEqual(currentCulture, templateCache.Locale);
                Assert.AreEqual("testIdentity", templateCache.TemplateInfo.Single().Identity);
                Assert.AreEqual(string.IsNullOrEmpty(expectedLocator) ? string.Empty : expectedLocator + " name", templateCache.TemplateInfo.Single().Name);
            }
            finally
            {
                CultureInfo.CurrentUICulture = persistedCulture;
            }
        }

        [TestMethod]
        public void CanHandlePostActions()
        {
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            SettingsFilePaths paths = new SettingsFilePaths(environmentSettings);

            Guid postAction1 = Guid.NewGuid();
            Guid postAction2 = Guid.NewGuid();

            var template = GetFakedTemplate("testIdentity", "testMount", "testName");
            A.CallTo(() => template.PostActions).Returns(new[] { postAction1, postAction2 });
            IMountPoint mountPoint = A.Fake<IMountPoint>();
            A.CallTo(() => mountPoint.MountPointUri).Returns("testMount");

            ScanResult result = new ScanResult(mountPoint, new[] { template }, [], []);
            TemplateCache templateCache = new TemplateCache([], new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

            WriteObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile, templateCache);
            var readCache = new TemplateCache(ReadObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile));

            Assert.ContainsSingle(readCache.TemplateInfo);
            var readTemplate = readCache.TemplateInfo[0];
            Assert.AreSequenceEqual(new[] { postAction1, postAction2 }, readTemplate.PostActions);
        }

        [TestMethod]
        public void CanHandleConstraints()
        {
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            SettingsFilePaths paths = new SettingsFilePaths(environmentSettings);

            TemplateConstraintInfo constraintInfo1 = new TemplateConstraintInfo("t1", null);
            TemplateConstraintInfo constraintInfo2 = new TemplateConstraintInfo("t1", "{[ \"one\", \"two\"]}");

            var template = GetFakedTemplate("testIdentity", "testMount", "testName");
            A.CallTo(() => template.Constraints).Returns(new[] { constraintInfo1, constraintInfo2 });
            IMountPoint mountPoint = A.Fake<IMountPoint>();
            A.CallTo(() => mountPoint.MountPointUri).Returns("testMount");

            ScanResult result = new ScanResult(mountPoint, new[] { template }, [], []);
            TemplateCache templateCache = new TemplateCache([], new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

            WriteObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile, templateCache);
            var readCache = new TemplateCache(ReadObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile));

            Assert.ContainsSingle(readCache.TemplateInfo);
            var readTemplate = readCache.TemplateInfo[0];
            Assert.HasCount(2, readTemplate.Constraints);
            Assert.AreEqual("t1", readTemplate.Constraints[0].Type);
            Assert.AreEqual("t1", readTemplate.Constraints[1].Type);
            Assert.IsNull(readTemplate.Constraints[0].Args);
            Assert.AreEqual(constraintInfo2.Args, readTemplate.Constraints[1].Args);
        }

        [TestMethod]
        public void CanHandleParameters()
        {
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
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

            ScanResult result = new ScanResult(mountPoint, new[] { template }, [], []);
            TemplateCache templateCache = new TemplateCache([], new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

            WriteObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile, templateCache);
            var readCache = new TemplateCache(ReadObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile));

            Assert.ContainsSingle(readCache.TemplateInfo);
            var readTemplate = readCache.TemplateInfo[0];
            Assert.HasCount(3, readTemplate.ParameterDefinitions);
            Assert.IsTrue(readTemplate.ParameterDefinitions.ContainsKey("param1"));
            Assert.AreEqual(PrecedenceDefinition.Optional, readTemplate.ParameterDefinitions["param1"].Precedence.PrecedenceDefinition);
            Assert.IsTrue(readTemplate.ParameterDefinitions.ContainsKey("param2"));
            Assert.AreEqual("string", readTemplate.ParameterDefinitions["param2"].DataType);
            Assert.AreEqual("param1 == \"foo\"", readTemplate.ParameterDefinitions["param2"].Precedence.IsRequiredCondition);
            Assert.IsTrue(readTemplate.ParameterDefinitions.ContainsKey("param3"));
            Assert.AreEqual("choice", readTemplate.ParameterDefinitions["param3"].DataType);
            Assert.AreEqual(PrecedenceDefinition.Required, readTemplate.ParameterDefinitions["param3"].Precedence.PrecedenceDefinition);
            Assert.AreEqual("def", readTemplate.ParameterDefinitions["param3"].DefaultValue);
            Assert.AreEqual("def-no-value", readTemplate.ParameterDefinitions["param3"].DefaultIfOptionWithoutValue);
            Assert.AreEqual("desc", readTemplate.ParameterDefinitions["param3"].Description);
            Assert.AreEqual("displ", readTemplate.ParameterDefinitions["param3"].DisplayName);
            Assert.IsTrue(readTemplate.ParameterDefinitions["param3"].AllowMultipleValues);
            Assert.HasCount(2, readTemplate.ParameterDefinitions["param3"].Choices!);
        }

        [TestMethod]
        [DataRow(true, "defaultName")]
        [DataRow(true, null)]
        [DataRow(false, "anotherDefault")]
        public void CanHandleDefaultName(bool preferDefaultName, string? defaultName)
        {
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            SettingsFilePaths paths = new SettingsFilePaths(environmentSettings);

            var template = GetFakedTemplate("testIdentity", "testMount", "testName");
            A.CallTo(() => template.PreferDefaultName).Returns(preferDefaultName);
            A.CallTo(() => template.DefaultName).Returns(defaultName);
            IMountPoint mountPoint = A.Fake<IMountPoint>();
            A.CallTo(() => mountPoint.MountPointUri).Returns("testMount");

            ScanResult result = new ScanResult(mountPoint, new[] { template }, [], []);
            TemplateCache templateCache = new TemplateCache([], new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

            WriteObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile, templateCache);
            var readCache = new TemplateCache(ReadObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile));

            Assert.ContainsSingle(readCache.TemplateInfo);
            var readTemplate = readCache.TemplateInfo[0];
            Assert.AreEqual(preferDefaultName, readTemplate.PreferDefaultName);
            Assert.AreEqual(defaultName, readTemplate.DefaultName);
        }

        [TestMethod]
        public void ProducesCorrectWarningOnOverlappingIdentity_ManagedCandidatesOnly()
        {
            List<(LogLevel, string)> loggedMessages = new List<(LogLevel, string)>();
            InMemoryLoggerProvider loggerProvider = new InMemoryLoggerProvider(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });
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

            ScanResult result = new ScanResult(A.Fake<IMountPoint>(), new[] { templateA, templateB, templateC }, [], []);
            _ = new TemplateCache(new[] { managedTPA, managedTPB, managedTPC }, new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

            var warningMessages = loggedMessages.Where(log => log.Item1 == LogLevel.Warning);
            Assert.ContainsSingle(warningMessages);
            Assert.Contains(expectedOutput, warningMessages.Single().Item2);
        }

        [TestMethod]
        public void ProducesCorrectOutputOnOverlappingIdentity_ManagedAndUnmanagedCandidates()
        {
            List<(LogLevel, string)> loggedMessages = new List<(LogLevel, string)>();
            InMemoryLoggerProvider loggerProvider = new InMemoryLoggerProvider(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });
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

            ScanResult result = new ScanResult(A.Fake<IMountPoint>(), new[] { templateA, templateB, templateC }, [], []);
            _ = new TemplateCache(new[] { managedTPA, managedTPB, managedTPC }, new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

            var warningMessages = loggedMessages.Where(log => log.Item1 == LogLevel.Warning);
            Assert.ContainsSingle(warningMessages);
            Assert.Contains(expectedOutput, warningMessages.Single().Item2);
        }

        [TestMethod]
        public void NoOutputOnOverlappingIdentity_UnmanagedCandidateWins()
        {
            List<(LogLevel, string)> loggedMessages = new List<(LogLevel, string)>();
            InMemoryLoggerProvider loggerProvider = new InMemoryLoggerProvider(loggedMessages);
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });
            var overlappingIdentity = "testIdentity";

            var templateA = GetFakedTemplate(overlappingIdentity, "testMountA", "TemplateA");
            var managedTPA = GetFakedManagedTemplatePackage("testMountA", "PackageA");

            var templateB = GetFakedTemplate(overlappingIdentity, "testMountB", "TemplateB");
            var managedTPB = GetFakedManagedTemplatePackage("testMountB", "PackageB");

            var templateC = GetFakedTemplate(overlappingIdentity, "testMountC", "TemplateC");
            var managedTPC = GetFakedTemplatePackage("testMountC");

            ScanResult result = new ScanResult(A.Fake<IMountPoint>(), new[] { templateA, templateB, templateC }, [], []);
            _ = new TemplateCache(new[] { managedTPA, managedTPB, managedTPC }, new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

            var warningMessages = loggedMessages.Where(log => log.Item1 == LogLevel.Warning);
            Assert.IsEmpty(warningMessages);
        }

        [TestMethod]
        public void CanHandleHostData()
        {
            IEngineEnvironmentSettings environmentSettings = s_environmentSettingsHelper.CreateEnvironment(virtualize: true);
            SettingsFilePaths paths = new SettingsFilePaths(environmentSettings);

            string hostfile = /*lang=json,strict*/ """
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

            string hostFileFormatted = JsonNode.Parse(hostfile)!.ToJsonString();
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

            ScanResult result = new ScanResult(sourceMountPoint, new[] { template }, [], []);
            TemplateCache templateCache = new TemplateCache([], new[] { result }, new Dictionary<string, DateTime>(), environmentSettings);

            Assert.AreEqual(hostFileLocation, templateCache.TemplateInfo[0].HostConfigPlace);
            Assert.AreEqual(hostFileFormatted, templateCache.TemplateInfo[0].HostData);

            WriteObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile, templateCache);
            var readCache = new TemplateCache(ReadObject(environmentSettings.Host.FileSystem, paths.TemplateCacheFile));

            Assert.ContainsSingle(readCache.TemplateInfo);
            var readTemplate = readCache.TemplateInfo[0];
            Assert.AreEqual(hostFileLocation, readTemplate.HostConfigPlace);
            Assert.AreEqual(hostFileFormatted, readTemplate.HostData);
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

        private static JsonObject ReadObject(IPhysicalFileSystem fileSystem, string path)
        {
            using var fileStream = fileSystem.OpenRead(path);
            using var textReader = new StreamReader(fileStream, System.Text.Encoding.UTF8, true);
            string content = textReader.ReadToEnd();
            return JsonNode.Parse(content)!.AsObject();
        }

        private static void WriteObject(IPhysicalFileSystem fileSystem, string path, object obj)
        {
            using var fileStream = fileSystem.CreateFile(path);
            JsonSerializer.Serialize(fileStream, obj);
        }
    }
}
