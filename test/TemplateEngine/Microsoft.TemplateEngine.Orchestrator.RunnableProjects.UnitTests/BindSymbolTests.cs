// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Components;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.Parameters;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.TestHelper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests
{
    public class BindSymbolTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        public BindSymbolTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        [Fact]
        public async void CreateAsyncTest_UseBindValuesWithReplace()
        {
            //
            // Template content preparation
            //

            var templateConfig = new
            {
                identity = "test.template",
                name = "test",
                shortName = "test",
                symbols = new
                {
                    hostPrefixed = new
                    {
                        type = "bind",
                        binding = "host:HostIdentifier",
                        replaces = "%R1%"
                    },
                    envPrefixed = new
                    {
                        type = "bind",
                        binding = "env:MYENVVAR",
                        replaces = "%R2%"
                    },
                    hostUnprefixed = new
                    {
                        type = "bind",
                        binding = "host:HostIdentifier",
                        replaces = "%R3%"
                    },
                    envUnprefixed = new
                    {
                        type = "bind",
                        binding = "env:MYENVVAR",
                        replaces = "%R4%"
                    },
                }
            };

            string sourceSnippet = """
            %R1%
            %R2%
            %R3%
            %R4%
            """;

            string expectedSnippet = """
            TestHost
            MyValue
            TestHost
            MyValue
            """;

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, JsonConvert.SerializeObject(templateConfig, Formatting.Indented) },
                { "sourceFile", sourceSnippet }
            };

            //
            // Dependencies preparation and mounting
            //

            Environment.SetEnvironmentVariable("MYENVVAR", "MyValue");
            IEnvironment environment = new DefaultEnvironment();

            IEngineEnvironmentSettings settings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: "TestHost", virtualize: true, environment: environment);
            ((TestHost)settings.Host).HostParamDefaults["HostIdentifier"] = "TestHost";
            string sourceBasePath = settings.GetTempVirtualizedPath();
            string targetDir = settings.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(settings, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = settings.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.FromObject(templateConfig));
            using RunnableProjectConfig runnableConfig = new RunnableProjectConfig(settings, rpg, configModel, sourceMountPoint.Root);

            ParameterSetData parametersData = new ParameterSetData(runnableConfig);
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await RunnableProjectGenerator.CreateAsync(settings, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Verifying the outputs
            //

            string resultContent = settings.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourceFile"));
            Assert.Equal(expectedSnippet, resultContent);
        }

        [Fact]
        public async void CreateAsyncTest_UseBindValuesWithFileRename()
        {
            //
            // Template content preparation
            //

            var templateConfig = new
            {
                identity = "test.template",
                name = "test",
                shortName = "test",
                symbols = new
                {
                    hostPrefixed = new
                    {
                        type = "bind",
                        binding = "host:HostIdentifier",
                        fileRename = "_R1_"
                    },
                    envPrefixed = new
                    {
                        type = "bind",
                        binding = "env:MYENVVAR",
                        fileRename = "_R2_"
                    },
                }
            };

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, JsonConvert.SerializeObject(templateConfig, Formatting.Indented) },
                //content
                { "_R1_.cs", string.Empty },
                { "_R2_.cs", string.Empty }
            };

            //
            // Dependencies preparation and mounting
            //

            Environment.SetEnvironmentVariable("MYENVVAR", "MyValue");
            IEnvironment environment = new DefaultEnvironment();

            IEngineEnvironmentSettings settings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: "TestHost", virtualize: true, environment: environment);
            ((TestHost)settings.Host).HostParamDefaults["HostIdentifier"] = "TestHost";
            string sourceBasePath = settings.GetTempVirtualizedPath();
            string targetDir = settings.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(settings, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = settings.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.FromObject(templateConfig));
            using RunnableProjectConfig runnableConfig = new RunnableProjectConfig(settings, rpg, configModel, sourceMountPoint.Root);
            ParameterSetData parametersData = new ParameterSetData(runnableConfig);
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            _ = await RunnableProjectGenerator.CreateAsync(settings, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Verifying the outputs
            //

            Assert.True(settings.Host.FileSystem.FileExists(Path.Combine(targetDir, "TestHost.cs")));
            Assert.True(settings.Host.FileSystem.FileExists(Path.Combine(targetDir, "MyValue.cs")));
        }

        [Fact]
        public async void CreateAsyncTest_UseBindValuesInMacros()
        {
            //
            // Template content preparation
            //

            var templateConfig = new
            {
                identity = "test.template",
                name = "test",
                shortName = "test",
                symbols = new
                {
                    hostPrefixed = new
                    {
                        type = "bind",
                        binding = "host:HostIdentifier",
                    },
                    switchSymbol = new
                    {
                        type = "generated",
                        generator = "switch",
                        replaces = "%VAL%",
                        parameters = new
                        {
                            cases = new[]
                            {
                                new
                                {
                                    condition = "(hostPrefixed == \"TestHost\")",
                                    value = "Correct"
                                },
                                new
                                {
                                    condition = "(hostPrefixed != \"TestHost\")",
                                    value = "Incorrect"
                                },
                            }
                        }
                    }
                }
            };

            string sourceSnippet = @"%VAL%";

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, JsonConvert.SerializeObject(templateConfig, Formatting.Indented) },
                //content
                { "sourceFile", sourceSnippet }
            };

            //
            // Dependencies preparation and mounting
            //
            IEngineEnvironmentSettings settings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: "TestHost", virtualize: true);
            ((TestHost)settings.Host).HostParamDefaults["HostIdentifier"] = "TestHost";
            string sourceBasePath = settings.GetTempVirtualizedPath();
            string targetDir = settings.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(settings, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = settings.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.FromObject(templateConfig));
            using RunnableProjectConfig runnableConfig = new RunnableProjectConfig(settings, rpg, configModel, sourceMountPoint.Root);
            ParameterSetData parametersData = new ParameterSetData(runnableConfig);
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await RunnableProjectGenerator.CreateAsync(settings, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Verifying the outputs
            //

            string resultContent = settings.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourceFile"));
            Assert.Equal("Correct", resultContent);

            ((TestHost)settings.Host).HostParamDefaults["HostIdentifier"] = "NoTestHost";
            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await RunnableProjectGenerator.CreateAsync(settings, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Verifying the outputs
            //

            resultContent = settings.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourceFile"));
            Assert.Equal("Incorrect", resultContent);
        }

        [Fact]
        public async void CreateAsyncTest_BindingConflict()
        {
            //
            // Template content preparation
            //

            var templateConfig = new
            {
                identity = "test.template",
                name = "test",
                shortName = "test",
                symbols = new
                {
                    testBindConflict = new
                    {
                        type = "bind",
                        binding = "Test",
                        replaces = "%VAL%"
                    },
                }
            };

            string sourceSnippet = @"%VAL%";

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, JsonConvert.SerializeObject(templateConfig, Formatting.Indented) },
                //content
                { "sourceFile", sourceSnippet }
            };

            //
            // Dependencies preparation and mounting
            //

            (Type, IIdentifiedComponent)[]? additionalComponents = new[]
            {
                (typeof(IBindSymbolSource), new TestBindSymbolSource(Guid.NewGuid())),
                (typeof(IBindSymbolSource), new TestBindSymbolSource(Guid.NewGuid()) as IIdentifiedComponent)
            };

            List<(LogLevel, string)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);

            IEngineEnvironmentSettings settings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: "TestHost", virtualize: true, additionalComponents: additionalComponents, addLoggerProviders: new[] { loggerProvider });
            string sourceBasePath = settings.GetTempVirtualizedPath();
            string targetDir = settings.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(settings, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = settings.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.FromObject(templateConfig));
            using RunnableProjectConfig runnableConfig = new RunnableProjectConfig(settings, rpg, configModel, sourceMountPoint.Root);
            ParameterSetData parametersData = new ParameterSetData(runnableConfig);
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            await RunnableProjectGenerator.CreateAsync(settings, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Verifying the outputs
            //

            string resultContent = settings.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourceFile"));
            Assert.Equal("%VAL%", resultContent);

            IEnumerable<string> warningMessages = loggedMessages.Where(log => log.Item1 == LogLevel.Warning).Select(log => log.Item2);
            Assert.Equal(2, warningMessages.Count());
            Assert.Contains(string.Format(LocalizableStrings.BindSymbolEvaluator_Warning_ValueAvailableFromMultipleSources, "Test", "'Test', 'Test'", "'test:', 'test:'"), warningMessages);
            Assert.Contains(string.Format(LocalizableStrings.BindSymbolEvaluator_Warning_EvaluationError, "testBindConflict"), warningMessages);
        }

        [Fact]
        public async void CreateAsyncTest_ForcedPrefixBinding()
        {
            //
            // Template content preparation
            //

            var templateConfig = new
            {
                identity = "test.template",
                name = "test",
                shortName = "test",
                symbols = new
                {
                    notPrefixed = new
                    {
                        type = "bind",
                        binding = "Test",
                        replaces = "%VAL%"
                    },
                }
            };

            string sourceSnippet = @"%VAL%";

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, JsonConvert.SerializeObject(templateConfig, Formatting.Indented) },
                //content
                { "sourceFile", sourceSnippet }
            };

            //
            // Dependencies preparation and mounting
            //

            var symbolSource = new TestBindSymbolSource(Guid.NewGuid(), prefix: "test", requiresPrefixMatch: true);
            (Type, IIdentifiedComponent)[]? additionalComponents = new[]
            {
                (typeof(IBindSymbolSource), (IIdentifiedComponent)symbolSource),
            };

            List<(LogLevel, string)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);

            IEngineEnvironmentSettings settings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: "TestHost", virtualize: true, additionalComponents: additionalComponents, addLoggerProviders: new[] { loggerProvider });
            string sourceBasePath = settings.GetTempVirtualizedPath();
            string targetDir = settings.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(settings, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = settings.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.FromObject(templateConfig));
            using RunnableProjectConfig runnableConfig = new RunnableProjectConfig(settings, rpg, configModel, sourceMountPoint.Root);
            ParameterSetData parametersData = new ParameterSetData(runnableConfig);
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            await RunnableProjectGenerator.CreateAsync(settings, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Verifying the outputs
            //

            string resultContent = settings.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourceFile"));
            Assert.Equal("%VAL%", resultContent);

            IEnumerable<string> warningMessages = loggedMessages.Where(log => log.Item1 == LogLevel.Warning).Select(log => log.Item2);
            Assert.Single(warningMessages);
            Assert.Contains(string.Format(LocalizableStrings.BindSymbolEvaluator_Warning_EvaluationError, "notPrefixed"), warningMessages);
            Assert.False(symbolSource.GetBoundValueAsync_WasCalled);
        }

        [Fact]
        public async void CreateAsyncTest_CanUseDefaultValue()
        {
            //
            // Template content preparation
            //

            var templateConfig = new
            {
                identity = "test.template",
                name = "test",
                shortName = "test",
                symbols = new
                {
                    hostPrefixed = new
                    {
                        type = "bind",
                        binding = "host:HostIdentifier",
                        replaces = "%R1%",
                        defaultValue = "hostDefault"
                    },
                    envPrefixed = new
                    {
                        type = "bind",
                        binding = "env:MYENVVAR",
                        replaces = "%R2%",
                        defaultValue = "envDefault"
                    },
                    hostUnprefixed = new
                    {
                        type = "bind",
                        binding = "unknown",
                        replaces = "%R3%",
                        defaultValue = "expectedDefValue"
                    },
                }
            };

            string sourceSnippet = """
            %R1%
            %R2%
            %R3%
            """;

            string expectedSnippet = """
            TestHost
            MyValue
            expectedDefValue
            """;

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, JsonConvert.SerializeObject(templateConfig, Formatting.Indented) },
                //content
                { "sourceFile", sourceSnippet }
            };

            //
            // Dependencies preparation and mounting
            //

            Environment.SetEnvironmentVariable("MYENVVAR", "MyValue");
            IEnvironment environment = new DefaultEnvironment();

            IEngineEnvironmentSettings settings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: "TestHost", virtualize: true, environment: environment);
            ((TestHost)settings.Host).HostParamDefaults["HostIdentifier"] = "TestHost";
            string sourceBasePath = settings.GetTempVirtualizedPath();
            string targetDir = settings.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(settings, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = settings.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.FromObject(templateConfig));
            using RunnableProjectConfig runnableConfig = new RunnableProjectConfig(settings, rpg, configModel, sourceMountPoint.Root);
            ParameterSetData parametersData = new ParameterSetData(runnableConfig);
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await RunnableProjectGenerator.CreateAsync(settings, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Verifying the outputs
            //

            string resultContent = settings.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourceFile"));
            Assert.Equal(expectedSnippet, resultContent);
        }

        [Fact]
        public async void CreateAsyncTest_CanConvertValueToDataType()
        {
            //
            // Template content preparation
            //

            var templateConfig = new
            {
                identity = "test.template",
                symbols = new
                {
                    envPrefixed = new
                    {
                        type = "bind",
                        binding = "env:MYENVVAR",
                        dataType = "string"
                    },
                    computed = new
                    {
                        type = "computed",
                        value = "envPrefixed != \"\""
                    }
                }
            };

            string sourceSnippet = """
            //#if (computed)
            success
            //#endif
            """;

            string expectedSnippet = """
            success

            """;

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, JsonConvert.SerializeObject(templateConfig, Formatting.Indented) },
                //content
                { "sourceFile.cs", sourceSnippet }
            };

            //
            // Dependencies preparation and mounting
            //

            //int
            Environment.SetEnvironmentVariable("MYENVVAR", "100");
            IEnvironment environment = new DefaultEnvironment();

            IEngineEnvironmentSettings settings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: "TestHost", virtualize: true, environment: environment);
            string sourceBasePath = settings.GetTempVirtualizedPath();
            string targetDir = settings.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(settings, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = settings.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.FromObject(templateConfig));
            RunnableProjectConfig runnableConfig = new RunnableProjectConfig(settings, rpg, configModel, sourceMountPoint.Root);
            ParameterSetData parametersData = new ParameterSetData(runnableConfig);
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await RunnableProjectGenerator.CreateAsync(settings, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Verifying the outputs
            //

            string resultContent = settings.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourceFile.cs"));
            Assert.Equal(expectedSnippet, resultContent);
        }

        [Fact]
        public async void CreateAsyncTest_NoWarningOnUnknownBindingWithDefaultValue()
        {
            //
            // Template content preparation
            //

            var templateConfig = new
            {
                identity = "test.template",
                symbols = new
                {
                    env1 = new
                    {
                        type = "bind",
                        binding = "env:UNKNOWN1",
                        replaces = "%R1%",
                        defaultValue = "envDefault"
                    },
                    env2 = new
                    {
                        type = "bind",
                        binding = "env:UNKNOWN2",
                        replaces = "%R2%"
                    },
                }
            };

            string sourceSnippet = """
            %R1%
            %R2%
            """;

            string expectedSnippet = """
            envDefault
            %R2%
            """;

            IDictionary<string, string?> templateSourceFiles = new Dictionary<string, string?>
            {
                // template.json
                { TestFileSystemUtils.DefaultConfigRelativePath, JsonConvert.SerializeObject(templateConfig, Formatting.Indented) },
                //content
                { "sourceFile", sourceSnippet }
            };

            //
            // Dependencies preparation and mounting
            //

            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);

            IEngineEnvironmentSettings settings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: "TestHost", virtualize: true, addLoggerProviders: new[] { loggerProvider });

            string sourceBasePath = settings.GetTempVirtualizedPath();
            string targetDir = settings.GetTempVirtualizedPath();

            TestFileSystemUtils.WriteTemplateSource(settings, sourceBasePath, templateSourceFiles);
            using IMountPoint sourceMountPoint = settings.MountPath(sourceBasePath);
            RunnableProjectGenerator rpg = new RunnableProjectGenerator();
            TemplateConfigModel configModel = TemplateConfigModel.FromJObject(JObject.FromObject(templateConfig));
            RunnableProjectConfig runnableConfig = new RunnableProjectConfig(settings, rpg, configModel, sourceMountPoint.Root);
            ParameterSetData parametersData = new ParameterSetData(runnableConfig);
            IDirectory sourceDir = sourceMountPoint!.DirectoryInfo("/")!;

            //
            // Running the actual scenario: template files processing and generating output (including macros processing)
            //

            await RunnableProjectGenerator.CreateAsync(settings, runnableConfig, sourceDir, parametersData, targetDir, CancellationToken.None);

            //
            // Verifying the outputs
            //

            string resultContent = settings.Host.FileSystem.ReadAllText(Path.Combine(targetDir, "sourceFile"));
            Assert.Equal(expectedSnippet, resultContent);

            (LogLevel, string Message) warningMessage = Assert.Single(loggedMessages.Where(lm => lm.Level == LogLevel.Warning));
            Assert.Equal("Failed to evaluate bind symbol 'env2', it will be skipped.", warningMessage.Message);
            Assert.Contains(loggedMessages, lm => lm.Message == "Failed to evaluate bind symbol 'env1', the returned value is null. The default value 'envDefault' is used instead.");
        }

        private class TestBindSymbolSource : IBindSymbolSource
        {
            public TestBindSymbolSource(Guid guid, string prefix = "test", bool requiresPrefixMatch = false)
            {
                Id = guid;
                SourcePrefix = prefix;
                RequiresPrefixMatch = requiresPrefixMatch;
            }

            public string DisplayName => "Test";

            public string? SourcePrefix { get; }

            public int Priority => 0;

            public Guid Id { get; private set; }

            public bool RequiresPrefixMatch { get; }

            public bool GetBoundValueAsync_WasCalled { get; private set; }

            public Task<string?> GetBoundValueAsync(IEngineEnvironmentSettings settings, string bindname, CancellationToken cancellationToken)
            {
                GetBoundValueAsync_WasCalled = true;
                return Task.FromResult((string?)("TestVal" + Id.ToString()));
            }
        }
    }
}
