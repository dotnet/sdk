// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.Fakes
{
    internal class FakeMacro : BaseGeneratedSymbolMacro<FakeMacroConfig>
    {
        public override string Type => "fake";

        public override Guid Id => new Guid("{3DBC6AAB-5D13-40E9-3EC8-0467A7AA7334}");

        public override FakeMacroConfig CreateConfig(IEngineEnvironmentSettings environmentSettings, IGeneratedSymbolConfig deferredConfig) => new(this, deferredConfig);

        public override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variableCollection, FakeMacroConfig config)
        {
            string fakeMessage = $"Hello {config.NameToGreet}!";
            variableCollection[config.VariableName] = fakeMessage;
        }

    }
}
