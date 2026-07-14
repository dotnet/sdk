// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
using System.Text.Json;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    [TestClass]
    public class HostDataLoaderTests
    {
        // MSTest has no IClassFixture equivalent; a lazily-initialized static helper
        // mirrors the per-class lifetime that xUnit's IClassFixture provides.
        private static readonly Lazy<EnvironmentSettingsHelper> s_environmentSettingsHelper =
            new(() => new EnvironmentSettingsHelper());

        private EnvironmentSettingsHelper _environmentSettingsHelper = null!;

        [TestInitialize]
        public void TestInitialize()
        {
            _environmentSettingsHelper = s_environmentSettingsHelper.Value;
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
        public void CanLoadHostDataFile()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            HostSpecificDataLoader hostSpecificDataLoader = new(engineEnvironmentSettings);
            Assert.IsTrue(engineEnvironmentSettings.TryGetMountPoint(Directory.GetCurrentDirectory(), out IMountPoint? mountPoint));
            Assert.IsNotNull(mountPoint);
            IFile? dataFile = mountPoint!.FileInfo("/Resources/dotnetcli.host.json");

            ITemplateInfo template = A.Fake<ITemplateInfo>();
            A.CallTo(() => template.MountPointUri).Returns(Directory.GetCurrentDirectory());
            A.CallTo(() => template.HostConfigPlace).Returns("/Resources/dotnetcli.host.json");

            HostSpecificTemplateData data = hostSpecificDataLoader.ReadHostSpecificTemplateData(template);
            Assert.IsNotNull(data);

            Assert.IsFalse(data.IsHidden);
            Assert.HasCount(2, data.UsageExamples);
            Assert.IsNotNull(data.UsageExamples);
            Assert.Contains("--framework netcoreapp3.1 --langVersion '9.0'", data.UsageExamples);
            Assert.HasCount(4, data.SymbolInfo);
            Assert.Contains("TargetFrameworkOverride", data.HiddenParameterNames);
            Assert.Contains("Framework", data.ParametersToAlwaysShow);
            Assert.IsTrue(data.LongNameOverrides.ContainsKey("skipRestore"));
            Assert.AreEqual("no-restore", data.LongNameOverrides["skipRestore"]);
            Assert.IsTrue(data.ShortNameOverrides.ContainsKey("skipRestore"));
            Assert.AreEqual("", data.ShortNameOverrides["skipRestore"]);
            Assert.AreEqual("no-restore", data.DisplayNameForParameter("skipRestore"));
        }

        [TestMethod]
        public void CanReadHostDataFromITemplateInfo()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            HostSpecificDataLoader hostSpecificDataLoader = new(engineEnvironmentSettings);
            Assert.IsTrue(engineEnvironmentSettings.TryGetMountPoint(Directory.GetCurrentDirectory(), out IMountPoint? mountPoint));
            Assert.IsNotNull(mountPoint);
            IFile? dataFile = mountPoint!.FileInfo("/Resources/dotnetcli.host.json");
            Assert.IsNotNull(dataFile);
            using Stream s = dataFile.OpenRead();
            using TextReader tr = new StreamReader(s, Encoding.UTF8, true);

            string json = tr.ReadToEnd();
            ITemplateInfo template = A.Fake<ITemplateInfo>(builder => builder.Implements<ITemplateInfoHostJsonCache>().Implements<ITemplateInfo>());
            A.CallTo(() => ((ITemplateInfoHostJsonCache)template).HostData).Returns(json);

            HostSpecificTemplateData data = hostSpecificDataLoader.ReadHostSpecificTemplateData(template);
            Assert.IsNotNull(data);

            Assert.IsFalse(data.IsHidden);
            Assert.HasCount(2, data.UsageExamples);
            Assert.IsNotNull(data.UsageExamples);
            Assert.Contains("--framework netcoreapp3.1 --langVersion '9.0'", data.UsageExamples);
            Assert.HasCount(4, data.SymbolInfo);
            Assert.Contains("TargetFrameworkOverride", data.HiddenParameterNames);
            Assert.Contains("Framework", data.ParametersToAlwaysShow);
            Assert.IsTrue(data.LongNameOverrides.ContainsKey("skipRestore"));
            Assert.AreEqual("no-restore", data.LongNameOverrides["skipRestore"]);
            Assert.IsTrue(data.ShortNameOverrides.ContainsKey("skipRestore"));
            Assert.AreEqual("", data.ShortNameOverrides["skipRestore"]);
            Assert.AreEqual("no-restore", data.DisplayNameForParameter("skipRestore"));
        }

        [TestMethod]
        public void ReturnDefaultForInvalidEntry()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            HostSpecificDataLoader hostSpecificDataLoader = new(engineEnvironmentSettings);

            ITemplateInfo template = A.Fake<ITemplateInfo>(builder => builder.Implements<ITemplateInfoHostJsonCache>().Implements<ITemplateInfo>());
            A.CallTo(() => ((ITemplateInfoHostJsonCache)template).HostData).Returns(null);

            HostSpecificTemplateData data = hostSpecificDataLoader.ReadHostSpecificTemplateData(template);
            Assert.IsNotNull(data);
            Assert.AreEqual(HostSpecificTemplateData.Default, data);
        }

        [TestMethod]
        public void ReturnDefaultForInvalidFile()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true);
            HostSpecificDataLoader hostSpecificDataLoader = new(engineEnvironmentSettings);

            ITemplateInfo template = A.Fake<ITemplateInfo>();
            A.CallTo(() => template.MountPointUri).Returns(Directory.GetCurrentDirectory());
            A.CallTo(() => template.HostConfigPlace).Returns("unknown");

            HostSpecificTemplateData data = hostSpecificDataLoader.ReadHostSpecificTemplateData(template);
            Assert.IsNotNull(data);
            Assert.AreEqual(HostSpecificTemplateData.Default, data);
        }

        [TestMethod]
        public void CanSerializeData()
        {
            var usageExamples = new[] { "example1" };
            var symbolInfo = new Dictionary<string, IReadOnlyDictionary<string, string>>()
            {
                {
                    "param1",
                    new Dictionary<string, string>()
                    {
                        { "isHidden", "false" },
                        { "longName", "longParam1" },
                        { "shortName", "shortParam1" }
                    }
                },
                {
                    "param2",
                    new Dictionary<string, string>()
                    {
                        { "isHidden", "false" },
                        { "longName", "" },
                        { "shortName", "shortParam2" }
                    }
                },
                {
                    "param3",
                    new Dictionary<string, string>()
                    {
                        { "isHidden", "true" }
                    }
                }
            };
            var data = new HostSpecificTemplateData(symbolInfo, usageExamples, isHidden: true);
            var serialized = JsonSerializer.SerializeToNode(data)!.AsObject();

            Assert.IsNotNull(serialized);
            Assert.HasCount(3, serialized);

            Assert.Contains("UsageExamples", serialized.Select(p => p.Key));
            Assert.Contains("SymbolInfo", serialized.Select(p => p.Key));
            Assert.Contains("IsHidden", serialized.Select(p => p.Key));
        }

        [TestMethod]
        public void CanSerializeData_SkipsEmpty()
        {
            var usageExamples = Array.Empty<string>();
            var symbolInfo = new Dictionary<string, IReadOnlyDictionary<string, string>>()
            {
                {
                    "param1",
                    new Dictionary<string, string>()
                    {
                        { "isHidden", "false" },
                        { "longName", "longParam1" },
                        { "shortName", "shortParam1" }
                    }
                },
                {
                    "param2",
                    new Dictionary<string, string>()
                    {
                        { "isHidden", "false" },
                        { "longName", "" },
                        { "shortName", "shortParam2" }
                    }
                },
                {
                    "param3",
                    new Dictionary<string, string>()
                    {
                        { "isHidden", "true" }
                    }
                }
            };
            var data = new HostSpecificTemplateData(symbolInfo, usageExamples, isHidden: false);
            var serialized = JsonSerializer.SerializeToNode(data)!.AsObject();

            Assert.IsNotNull(serialized);
            Assert.HasCount(1, serialized);

            Assert.Contains("SymbolInfo", serialized.Select(p => p.Key));

            var symbolInfoObj = serialized["SymbolInfo"]!.AsObject();
            Assert.IsNotNull(symbolInfoObj);
            //empty values should stay when deserializing symbol info
            Assert.HasCount(3, symbolInfoObj["param1"]!.AsObject());
            Assert.AreEqual("", symbolInfoObj["param2"]!["longName"]!.GetValue<string>());
            Assert.HasCount(3, symbolInfoObj["param2"]!.AsObject());
            Assert.HasCount(1, symbolInfoObj["param3"]!.AsObject());

            Assert.DoesNotContain("IsHidden", serialized.Select(p => p.Key));
            Assert.DoesNotContain("UsageExamples", serialized.Select(p => p.Key));
        }
    }
}
