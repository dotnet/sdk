// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    internal static class MacroTestHelpers
    {
        internal static ParameterSetter TestParameterSetter(IEngineEnvironmentSettings environmentSettings, IParameterSet parameters)
        {
            ParameterSetter setter = (p, value) =>
            {
                ((RunnableProjectGenerator.ParameterSet)parameters).AddParameter(p);
                parameters.ResolvedValues[p] = RunnableProjectGenerator.InternalConvertParameterValueToType(environmentSettings, p, value, out bool valueResolutionError);
            };

            return setter;
        }
    }
}
