// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    // Symbol.type = "computed" is the only thing that becomes an evaluate macro.
    internal class EvaluateMacro : IMacro
    {
        internal const EvaluatorType DefaultEvaluator = EvaluatorType.CPP2;

        public Guid Id => new Guid("BB625F71-6404-4550-98AF-B2E546F46C5F");

        public string Type => "evaluate";

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection variableCollection, IMacroConfig rawConfig)
        {
            if (rawConfig is not EvaluateMacroConfig config)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as EvaluateMacroConfig");
            }

            ConditionStringEvaluator evaluator = EvaluatorSelector.SelectStringEvaluator(EvaluatorSelector.ParseEvaluatorName(config.Evaluator, DefaultEvaluator));
            bool result = evaluator(environmentSettings.Host.Logger, config.Value, variableCollection);

            variableCollection[config.VariableName] = result;
        }
    }
}
