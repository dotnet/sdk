// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
using Xunit;

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

            GlobalRunConfig config = new()
            {
                ComputedMacros = new[] { new FailMacroConfig("test") }
            };

            MacroProcessingException e = Assert.Throws<MacroProcessingException>(() => MacroProcessor.ProcessMacros(engineEnvironmentSettings, config, new VariableCollection()));
            Assert.Equal("Failed to evaluate", e.InnerException?.Message);
        }

        [Fact]
        public void CanThrow_WhenCannotProcessGeneratedMacro()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true, additionalComponents: new[] { (typeof(IGeneratedSymbolMacro), (IIdentifiedComponent)new FailMacro()) });

            GlobalRunConfig config = new()
            {
                GeneratedSymbolMacros = new[] { new GeneratedSymbol("s", "fail") }
            };

            MacroProcessingException e = Assert.Throws<MacroProcessingException>(() => MacroProcessor.ProcessMacros(engineEnvironmentSettings, config, new VariableCollection()));
            Assert.Equal("Failed to evaluate", e.InnerException?.Message);
        }

        [Fact]
        public void CanThrow_WhenAuthoringError()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true, additionalComponents: new[] { (typeof(IGeneratedSymbolMacro), (IIdentifiedComponent)new FailConfigMacro()) });

            GlobalRunConfig config = new()
            {
                GeneratedSymbolMacros = new[] { new GeneratedSymbol("s", "fail") }
            };

            TemplateAuthoringException e = Assert.Throws<TemplateAuthoringException>(() => MacroProcessor.ProcessMacros(engineEnvironmentSettings, config, new VariableCollection()));
            Assert.Equal("bad config", e.Message);
        }

        [Fact]
        public void CanLogWarning_OnUnknownMacro()
        {
            List<(LogLevel, string)> loggedMessages = new();
            InMemoryLoggerProvider loggerProvider = new(loggedMessages);
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(virtualize: true, addLoggerProviders: new[] { loggerProvider });

            GlobalRunConfig config = new()
            {
                GeneratedSymbolMacros = new[] { new GeneratedSymbol("s", "fail") }
            };

            MacroProcessor.ProcessMacros(engineEnvironmentSettings, config, new VariableCollection());
            Assert.Equal("Generated symbol 's': type 'fail' is unknown, processing is skipped.", loggedMessages.Single(m => m.Item1 == LogLevel.Warning).Item2);
        }

        private class FailMacro : IMacro<FailMacroConfig>, IGeneratedSymbolMacro<FailMacroConfig>
        {
            public string Type => "fail";

            public Guid Id { get; } = new Guid("{3DBC6AAB-5D13-40E9-9EC8-0467A7AA7335}");

            public FailMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig generatedSymbolConfig) => throw new TemplateAuthoringException("bad config");

            public void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, FailMacroConfig config) => throw new Exception("Failed to evaluate");

            public void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, IGeneratedSymbolConfig generatedSymbolConfig) => throw new Exception("Failed to evaluate");

            public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig config) => throw new Exception("Failed to evaluate");
        }

        private class FailConfigMacro : IMacro<FailMacroConfig>, IGeneratedSymbolMacro<FailMacroConfig>
        {
            public string Type => "fail";

            public Guid Id { get; } = new Guid("{3DBC6AAB-5D13-40E9-9EC8-0467A7AA7335}");

            public FailMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig generatedSymbolConfig) => throw new TemplateAuthoringException("bad config");

            public void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, FailMacroConfig config) => throw new Exception("Failed to evaluate");

            public void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variables, IGeneratedSymbolConfig generatedSymbolConfig) => throw new TemplateAuthoringException("bad config");

            public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig config) => throw new TemplateAuthoringException("bad config");
        }

        private class FailMacroConfig : BaseMacroConfig
        {
            public FailMacroConfig(string variableName) : base("fail", variableName, "string")
            {
            }

            internal override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars) => throw new Exception("Failed to evaluate");
        }
    }
}
