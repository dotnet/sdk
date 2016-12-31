using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.MacroTests
{
    internal static class MacroTestHelpers
    {
        internal static ParameterSetter TestParameterSetter(IParameterSet parameters)
        {
            ParameterSetter setter = (p, value) =>
            {
                ((RunnableProjectGenerator.ParameterSet)parameters).AddParameter(p);
                parameters.ResolvedValues[p] = RunnableProjectGenerator.InternalConvertParameterValueToType(p, value, out bool valueResolutionError);
            };

            return setter;
        }
    }
}
