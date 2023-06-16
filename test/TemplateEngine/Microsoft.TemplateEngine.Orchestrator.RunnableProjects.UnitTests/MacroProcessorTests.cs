// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.Fakes;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests
{
    public class MacroProcessorTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        public MacroProcessorTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        [Fact]
        public void CanThrow_WhenCannotProcessMacro()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true, additionalComponents: new[] { (typeof(IMacro), (IIdentifiedComponent)new FailMacro()) });

            var macros = new[] { new FailMacroConfig("test") };

            MacroProcessingException e = Assert.Throws<MacroProcessingException>(() => MacroProcessor.ProcessMacros(engineEnvironmentSettings, macros, new VariableCollection()));
            Assert.Equal("Failed to evaluate", e.InnerException?.Message);
        }

        [Fact]
        public void CanPrintWarningOnUnknownMacroConfig()
        {
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);

            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(
                virtualize: true, environment: A.Fake<IEnvironment>(), addLoggerProviders: new[] { loggerProvider });
            var fakeMacroConfig = new FakeMacroConfig(new FakeMacro(), "testVariable", "dummy");
            fakeMacroConfig.ResolveSymbolDependencies(new List<string>());

            var macroConfigs = new[] { fakeMacroConfig };

            MacroProcessor.ProcessMacros(engineEnvironmentSettings, macroConfigs, new VariableCollection());

            Assert.True(loggedMessages.Count == 1);
            Assert.Equal("Generated symbol 'testVariable': type 'fake' is unknown, processing is skipped.", loggedMessages.First().Message);
        }

        [Fact]
        public void CanProcessMacroWithCustomMacroAsDependency()
        {
            var fakeMacroVariableName = "testVariable";
            var coalesceVariableName = "coalesceTest";
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);
            var generatedConfig = A.Fake<IGeneratedSymbolConfig>();
            A.CallTo(() => generatedConfig.Parameters).Returns(new Dictionary<string, string>()
            {
                { "sourceVariableName",  JExtensions.ToJsonString("dummy") },
                { "fallbackVariableName",  JExtensions.ToJsonString(fakeMacroVariableName) }
            });
            A.CallTo(() => generatedConfig.VariableName).Returns(coalesceVariableName);

            var coalesceMacroConfig = new CoalesceMacroConfig(new CoalesceMacro(), generatedConfig);
            coalesceMacroConfig.ResolveSymbolDependencies(new List<string>() { fakeMacroVariableName });

            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true, environment: A.Fake<IEnvironment>(), additionalComponents: new[] { (typeof(IMacro), (IIdentifiedComponent)new FakeMacro()) }, addLoggerProviders: new[] { loggerProvider });

            var fakeMacro = new FakeMacro();
            var customGeneratedConfig = A.Fake<IGeneratedSymbolConfig>();
            A.CallTo(() => customGeneratedConfig.Parameters).Returns(new Dictionary<string, string>()
            {
                { "source",  JExtensions.ToJsonString("dummy") },
                { "name",  JExtensions.ToJsonString(fakeMacroVariableName) },
            });
            A.CallTo(() => customGeneratedConfig.VariableName).Returns(fakeMacroVariableName);
            var fakeMacroConfig = new FakeMacroConfig(new FakeMacro(), customGeneratedConfig);
            fakeMacroConfig.ResolveSymbolDependencies(new List<string>());
            var variableCollection = new Dictionary<string, object>() { { fakeMacroVariableName, fakeMacro } };

            MacroProcessor.ProcessMacros(engineEnvironmentSettings, new[] { (IMacroConfig)coalesceMacroConfig, fakeMacroConfig }, new VariableCollection(default, variableCollection));

            Assert.True(variableCollection.Count == 2);
            Assert.True(variableCollection.Values.Select(v => v.GetHashCode() == fakeMacro.GetHashCode()).Count() == 2);
            variableCollection.Select(v => v.Key).Should().Equal(new[] { fakeMacroVariableName, coalesceVariableName });
            Assert.True(coalesceMacroConfig.Dependencies.Count == 1);
            Assert.Equal(fakeMacroVariableName, coalesceMacroConfig.Dependencies.First());
        }

        [Fact]
        public void CanProcessCustomMacroWithDeps()
        {
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);

            string coalesceMacroName = "coalesceMacro";
            IGeneratedSymbolConfig coalesceGeneratedConfig = A.Fake<IGeneratedSymbolConfig>();
            A.CallTo(() => coalesceGeneratedConfig.Parameters).Returns(new Dictionary<string, string>()
            {
                { "sourceVariableName",  JExtensions.ToJsonString("dummy") },
                { "fallbackVariableName",  JExtensions.ToJsonString("dummy2") }
            });
            A.CallTo(() => coalesceGeneratedConfig.VariableName).Returns(coalesceMacroName);
            CoalesceMacroConfig coalesceMacroConfig = new(new CoalesceMacro(), coalesceGeneratedConfig);

            string switchMacroName = "switchMacro";
            SwitchMacroConfig switchMacroConfig = new(
                new SwitchMacro(),
                switchMacroName,
                "C++",
                "string",
                new List<(string?, string)>()
                {
                    ("(dummy == \"A\")", "val1"),
                    (null, "defVal")
                });
            switchMacroConfig.ResolveSymbolDependencies(new List<string>());

            string customMacroName = "customMacro";
            IGeneratedSymbolConfig customGeneratedConfig = A.Fake<IGeneratedSymbolConfig>();
            A.CallTo(() => customGeneratedConfig.Parameters).Returns(new Dictionary<string, string>()
            {
                { "source",  JExtensions.ToJsonString(switchMacroName) },
                { "name",  JExtensions.ToJsonString("dummy") }
            });
            A.CallTo(() => customGeneratedConfig.VariableName).Returns(customMacroName);
            FakeMacroConfig customMacroConfig = new(new FakeMacro(), customGeneratedConfig);
            customMacroConfig.ResolveSymbolDependencies(new List<string>());

            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(
                virtualize: true,
                environment: A.Fake<IEnvironment>(),
                addLoggerProviders: new[] { loggerProvider },
                additionalComponents: new[] { (typeof(IMacro), (IIdentifiedComponent)new FakeMacro()) });
            VariableCollection variableCollection = new()
            {
                ["dummy"] = "A",
                ["dummy2"] = "B",
                ["testVariable"] = "test"
            };

            IReadOnlyList<IMacroConfig> macroConfigs = new[] { (IMacroConfig)customMacroConfig, coalesceMacroConfig, switchMacroConfig };

            IReadOnlyList<IMacroConfig> sortedMacroConfigs = MacroProcessor.SortMacroConfigsByDependencies(new[] { "dummy", "dummy2", "coalesceMacro", "switchMacro", "customMacro", "testVariable" }, macroConfigs);
            MacroProcessor.ProcessMacros(engineEnvironmentSettings, sortedMacroConfigs, variableCollection);

            // Custom macro was processed without errors
            Assert.True(!loggedMessages.Any(lm => lm.Level == LogLevel.Error));
            Assert.Equal("A", variableCollection["coalesceMacro"]);
            Assert.Equal("val1", variableCollection["switchMacro"]);
            Assert.Equal("Hello dummy!", variableCollection["customMacro"]);
        }

        [Fact]
        public void CanSortCollectionWithCustomMacroWithDeps()
        {
            var coalesceMacroName = "coalesceMacro";
            var coalesceGeneratedConfig = A.Fake<IGeneratedSymbolConfig>();
            A.CallTo(() => coalesceGeneratedConfig.Parameters).Returns(new Dictionary<string, string>()
            {
                { "sourceVariableName",  JExtensions.ToJsonString("dummy") },
                { "fallbackVariableName",  JExtensions.ToJsonString("dummy") }
            });
            A.CallTo(() => coalesceGeneratedConfig.VariableName).Returns(coalesceMacroName);
            var coalesceMacroConfig = new CoalesceMacroConfig(new CoalesceMacro(), coalesceGeneratedConfig);

            var switchMacroName = "switchMacro";
            var switchMacroConfig = new SwitchMacroConfig(new SwitchMacro(), switchMacroName, string.Empty, string.Empty, new List<(string?, string)>());

            var customMacroName = "customMacro";
            var customGeneratedConfig = A.Fake<IGeneratedSymbolConfig>();
            A.CallTo(() => customGeneratedConfig.Parameters).Returns(new Dictionary<string, string>()
            {
                { "source",  JExtensions.ToJsonString(coalesceMacroName) },
                { "fallback",  JExtensions.ToJsonString(switchMacroName) },
                { "name",  JExtensions.ToJsonString("dummy") }
            });
            A.CallTo(() => customGeneratedConfig.VariableName).Returns(customMacroName);

            var customMacroConfig = new FakeMacroConfig(new FakeMacro(), customGeneratedConfig);
            var engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(
                virtualize: true,
                environment: A.Fake<IEnvironment>(),
                additionalComponents: new[] { (typeof(IMacro), (IIdentifiedComponent)new FakeMacro()) });
            var variableCollection = new VariableCollection();

            var sortedItems = MacroProcessor.SortMacroConfigsByDependencies(new[] { customMacroName, switchMacroName, coalesceMacroName }, new[] { (BaseMacroConfig)switchMacroConfig, customMacroConfig, coalesceMacroConfig });

            sortedItems.Select(si => si.VariableName).Should().Equal(new[] { switchMacroName, coalesceMacroName, customMacroName });
        }

        [Fact]
        public void CanSortMacrosWithDependencies()
        {
            var switchMacroName = "switchMacro";
            var coalesceMacroName = "coalesceMacro";
            var evaluateMacroName = "evaluateMacro";
            var joinMacroName = "joinMacro";

            var symbols = new string[] { switchMacroName, coalesceMacroName, evaluateMacroName, joinMacroName };

            var evaluateMacroConfig = new EvaluateMacroConfig(evaluateMacroName, string.Empty, "condition");
            var fakeCoalesceGeneratedSymbols = A.Fake<IGeneratedSymbolConfig>();
            A.CallTo(() => fakeCoalesceGeneratedSymbols.Parameters).Returns(new Dictionary<string, string>()
            {
                { "sourceVariableName",  JExtensions.ToJsonString(evaluateMacroName) },
                { "fallbackVariableName",  JExtensions.ToJsonString("dummy") }
            });
            A.CallTo(() => fakeCoalesceGeneratedSymbols.VariableName).Returns(coalesceMacroName);
            var coalesceMacroConfig = new CoalesceMacroConfig(new CoalesceMacro(), fakeCoalesceGeneratedSymbols);

            var switchMacroConfig = new SwitchMacroConfig(new SwitchMacro(), switchMacroName, string.Empty, string.Empty, new List<(string?, string)>()
            {
                ("evaluateMacro == 'blank'", "true"),
                ("coalesceMacro == 'blank'", "false")

            });

            var fakeJoinGeneratedSymbols = A.Fake<IGeneratedSymbolConfig>();
            A.CallTo(() => fakeJoinGeneratedSymbols.VariableName).Returns(joinMacroName);
            A.CallTo(() => fakeJoinGeneratedSymbols.Parameters).Returns(new Dictionary<string, string>()
            {
                { "symbols",  /*lang=json,strict*/ "[ {\"value\":\"switchMacro\"  } ]" },
                { "fallbackVariableName",  JExtensions.ToJsonString("dummy") }
            });
            var joinMacroConfig = new JoinMacroConfig(new JoinMacro(), fakeJoinGeneratedSymbols);
            var macroConfigs = new[] { (BaseMacroConfig)joinMacroConfig, switchMacroConfig, evaluateMacroConfig, coalesceMacroConfig };

            var sortedItems = MacroProcessor.SortMacroConfigsByDependencies(symbols, macroConfigs);

            sortedItems.Select(si => si.VariableName).Should().Equal(new[] { evaluateMacroName, coalesceMacroName, switchMacroName, joinMacroName });
        }

        [Fact]
        public void CanThrowErrorOnSortWhenMacrosHaveDepsCircle()
        {
            var switchMacroName = "switchMacro";
            var coalesceMacroName = "coalesceMacro";

            var symbols = new string[] { switchMacroName, coalesceMacroName };

            var fakeCoalesceGeneratedSymbols = A.Fake<IGeneratedSymbolConfig>();
            A.CallTo(() => fakeCoalesceGeneratedSymbols.Parameters).Returns(new Dictionary<string, string>()
            {
                { "sourceVariableName",  JExtensions.ToJsonString(switchMacroName) },
                { "fallbackVariableName",  JExtensions.ToJsonString("dummy") }
            });
            A.CallTo(() => fakeCoalesceGeneratedSymbols.VariableName).Returns(coalesceMacroName);
            var coalesceMacroConfig = new CoalesceMacroConfig(new CoalesceMacro(), fakeCoalesceGeneratedSymbols);

            var switchMacroConfig = new SwitchMacroConfig(new SwitchMacro(), switchMacroName, string.Empty, string.Empty, new List<(string?, string)>
            {
                ("coalesceMacroName == 'blank'", "true")
            });
            var macroConfigs = new[] { (BaseMacroConfig)switchMacroConfig, coalesceMacroConfig };

            Action sorting = () => { MacroProcessor.SortMacroConfigsByDependencies(symbols, macroConfigs); };
            sorting.Should().Throw<TemplateAuthoringException>()
                .Which.Message.Should().Contain("Parameter conditions contain cyclic dependency: [switchMacro, coalesceMacro, switchMacro] that is preventing deterministic evaluation.");
        }

        [Fact]
        public void CanRunDeterministically_ComputedMacros()
        {
            UndeterministicMacro macro = new UndeterministicMacro();

            IEnvironment environment = A.Fake<IEnvironment>();
            A.CallTo(() => environment.GetEnvironmentVariable("TEMPLATE_ENGINE_ENABLE_DETERMINISTIC_MODE")).Returns("true");

            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true, environment: environment, additionalComponents: new[] { (typeof(IMacro), (IIdentifiedComponent)macro) });

            IReadOnlyList<IMacroConfig> macros = new[] { (IMacroConfig)new UndeterministicMacroConfig("test"), new GuidMacroConfig("test-guid", "string", "Nn", "n") };

            IVariableCollection collection = new VariableCollection();

            MacroProcessor.ProcessMacros(engineEnvironmentSettings, macros, collection);
            Assert.Equal("deterministic", collection["test"]);
            Assert.Equal(new Guid("12345678-1234-1234-1234-1234567890AB").ToString("n"), collection["test-guid"]);

            A.CallTo(() => environment.GetEnvironmentVariable("TEMPLATE_ENGINE_ENABLE_DETERMINISTIC_MODE")).Returns("false");
            collection = new VariableCollection();

            MacroProcessor.ProcessMacros(engineEnvironmentSettings, macros, collection);
            Assert.Equal("undeterministic", collection["test"]);
            Assert.NotEqual(new Guid("12345678-1234-1234-1234-1234567890AB").ToString("n"), collection["test-guid"]);
        }

        [Fact]
        public void CanProcessMacroWithCustomMacroAsDependency_IndependentImplementation()
        {
            List<(LogLevel Level, string Message)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);

            string coalesceMacroName = "coalesceMacro";
            IGeneratedSymbolConfig coalesceGeneratedConfig = A.Fake<IGeneratedSymbolConfig>();
            A.CallTo(() => coalesceGeneratedConfig.Parameters).Returns(new Dictionary<string, string>()
            {
                { "sourceVariableName",  JExtensions.ToJsonString("dummy") },
                { "fallbackVariableName",  JExtensions.ToJsonString("dummy2") }
            });
            A.CallTo(() => coalesceGeneratedConfig.VariableName).Returns(coalesceMacroName);
            CoalesceMacroConfig coalesceMacroConfig = new(new CoalesceMacro(), coalesceGeneratedConfig);

            string switchMacroName = "switchMacro";
            SwitchMacroConfig switchMacroConfig = new(
                new SwitchMacro(),
                switchMacroName,
                "C++",
                "string",
                new List<(string?, string)>()
                {
                    ("(dummy == \"A\")", "val1"),
                    (null, "defVal")
                });

            string customMacroName = "customMacro";
            IGeneratedSymbolConfig customGeneratedConfig = A.Fake<IGeneratedSymbolConfig>();
            A.CallTo(() => customGeneratedConfig.Parameters).Returns(new Dictionary<string, string>()
            {
                { "source",  JExtensions.ToJsonString(switchMacroName) },
            });
            A.CallTo(() => customGeneratedConfig.VariableName).Returns(customMacroName);
            DependencyMacroConfig customMacroConfig = new(customMacroName, switchMacroName);

            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(
                virtualize: true,
                environment: A.Fake<IEnvironment>(),
                addLoggerProviders: new[] { loggerProvider },
                additionalComponents: new[] { (typeof(IMacro), (IIdentifiedComponent)new DependencyMacro()) });
            VariableCollection variableCollection = new()
            {
                ["dummy2"] = "B",
            };

            IReadOnlyList<IMacroConfig> macroConfigs = new[] { (IMacroConfig)switchMacroConfig, customMacroConfig, coalesceMacroConfig };

            IReadOnlyList<IMacroConfig> sortedMacroConfigs = MacroProcessor.SortMacroConfigsByDependencies(new[] { "dummy", "dummy2", coalesceMacroName, switchMacroName, customMacroName }, macroConfigs);
            MacroProcessor.ProcessMacros(engineEnvironmentSettings, sortedMacroConfigs, variableCollection);

            // Custom macro was processed without errors
            Assert.True(!loggedMessages.Any(lm => lm.Level == LogLevel.Error));
            Assert.Equal("B", variableCollection[coalesceMacroName]);
            Assert.Equal("defVal", variableCollection[switchMacroName]);
            Assert.Equal("defVal", variableCollection[customMacroName]);
        }

        private class FailMacro : IMacro<FailMacroConfig>, IGeneratedSymbolMacro
        {
            public string Type => "fail";

            public Guid Id { get; } = new Guid("{3DBC6AAB-5D13-40E9-9EC8-0467A7AA7335}");

            public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig generatedSymbolConfig) => new FailMacroConfig(generatedSymbolConfig.VariableName);

            public void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, FailMacroConfig config) => throw new Exception("Failed to evaluate");

            public void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, IGeneratedSymbolConfig generatedSymbolConfig) => throw new Exception("Failed to evaluate");

            public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig config) => throw new Exception("Failed to evaluate");
        }

        private class FailConfigMacro : IMacro<FailMacroConfig>, IGeneratedSymbolMacro
        {
            public string Type => "fail";

            public Guid Id { get; } = new Guid("{3DBC6AAB-5D13-40E9-9EC8-0467A7AA7335}");

            public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig generatedSymbolConfig) => new FailMacroConfig(generatedSymbolConfig.VariableName);

            public void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, FailMacroConfig config) => throw new Exception("Failed to evaluate");

            public void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, IGeneratedSymbolConfig generatedSymbolConfig) => throw new TemplateAuthoringException("bad config");

            public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig config) => throw new TemplateAuthoringException("bad config");
        }

        private class FailMacroConfig : BaseMacroConfig
        {
            public FailMacroConfig(string variableName) : base("fail", variableName, "string")
            {
            }
        }

        private class UndeterministicMacro : IDeterministicModeMacro, IDeterministicModeMacro<UndeterministicMacroConfig>, IGeneratedSymbolMacro, IGeneratedSymbolMacro<UndeterministicMacroConfig>
        {
            public string Type => "undeterministic";

            public Guid Id { get; } = new Guid("{3DBC6AAB-5D13-40E9-9EC8-0467A7AA7335}");

            public UndeterministicMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig generatedSymbolConfig)
            {
                return new UndeterministicMacroConfig(generatedSymbolConfig.VariableName);
            }

            public void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, UndeterministicMacroConfig config)
            {
                variables[config.VariableName] = "undeterministic";
            }

            public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, IMacroConfig config)
            {
                variables[config.VariableName] = "undeterministic";
            }

            public void EvaluateDeterministically(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, UndeterministicMacroConfig config)
            {
                variables[config.VariableName] = "deterministic";
            }

            public void EvaluateConfigDeterministically(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, IMacroConfig config)
            {
                variables[config.VariableName] = "deterministic";
            }

            IMacroConfig IGeneratedSymbolMacro.CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig generatedSymbolConfig)
            {
                return new UndeterministicMacroConfig(generatedSymbolConfig.VariableName);
            }
        }

        private class UndeterministicMacroConfig : IMacroConfig
        {
            public UndeterministicMacroConfig(string variableName)
            {
                VariableName = variableName;
            }

            public string VariableName { get; }

            public string Type => "undeterministic";
        }

        private class DependencyMacro : IGeneratedSymbolMacro, IGeneratedSymbolMacro<DependencyMacroConfig>
        {
            public string Type => "dependency";

            public Guid Id { get; } = new Guid("{545064DA-74B3-4A78-8B1A-A6B17B36E48D}");

            public IMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig generatedSymbolConfig)
            {
                return new DependencyMacroConfig(generatedSymbolConfig.VariableName, generatedSymbolConfig.Parameters["dependentSymbolName"]);
            }

            public void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, DependencyMacroConfig config)
            {
                variables[config.VariableName] = variables[config.DependentSymbolName];
            }

            public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig config)
            {
                if (config is DependencyMacroConfig dmc)
                {
                    vars[config.VariableName] = vars[dmc.DependentSymbolName];
                }
            }

            DependencyMacroConfig IGeneratedSymbolMacro<DependencyMacroConfig>.CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig generatedSymbolConfig)
            {
                return new DependencyMacroConfig(generatedSymbolConfig.VariableName, generatedSymbolConfig.Parameters["dependentSymbolName"]);
            }
        }

        private class DependencyMacroConfig : IMacroConfig, IMacroConfigDependency
        {
            public DependencyMacroConfig(string variableName, string dependentSymbolName)
            {
                VariableName = variableName;
                DependentSymbolName = dependentSymbolName;
            }

            public string VariableName { get; }

            public string DependentSymbolName { get; }

            public string Type => "dependency";

            public HashSet<string> Dependencies { get; private set; } = new HashSet<string>();

            public void ResolveSymbolDependencies(IReadOnlyList<string> symbols)
            {
                Dependencies = new HashSet<string>()
                {
                    DependentSymbolName
                };
            }
        }
    }
}
