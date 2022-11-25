// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    // Symbol.type = "computed" is the only thing that becomes an evaluate macro.
    internal class EvaluateMacro : BaseMacro<EvaluateMacroConfig>
    {
        internal const string MacroType = "evaluate";

        public override Guid Id { get; } = new Guid("BB625F71-6404-4550-98AF-B2E546F46C5F");

        public override string Type => MacroType;

        public override void Evaluate(IEngineEnvironmentSettings environmentSettings, IVariableCollection variableCollection, EvaluateMacroConfig config)
        {
            bool result = config.Evaluator(environmentSettings.Host.Logger, config.Condition, variableCollection);
            variableCollection[config.VariableName] = result;
            environmentSettings.Host.Logger.LogDebug("[{macro}]: Variable '{var}' was assigned to value '{value}'.", nameof(EvaluateMacro), config.VariableName, result);
        }
    }
}
