// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros.Config;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    // Symbol.type = "computed" is the only thing that becomes an evaluate macro.
    internal class EvaluateMacro : IMacro
    {
        internal const string DefaultEvaluator = "C++";
        public Guid Id => new Guid("BB625F71-6404-4550-98AF-B2E546F46C5F");

        public string Type => "evaluate";

        public void EvaluateConfig(IEngineEnvironmentSettings environmentSettings, IVariableCollection vars, IMacroConfig rawConfig, IParameterSet parameters, ParameterSetter setter)
        {
            EvaluateMacroConfig config = rawConfig as EvaluateMacroConfig;

            if (config == null)
            {
                throw new InvalidCastException("Couldn't cast the rawConfig as EvaluateMacroConfig");
            }

            ConditionEvaluator evaluator = EvaluatorSelector.Select(config.Evaluator, Cpp2StyleEvaluatorDefinition.Evaluate);

            byte[] data = Encoding.UTF8.GetBytes(config.Value);
            int len = data.Length;
            int pos = 0;
            IProcessorState state = new GlobalRunSpec.ProcessorState(environmentSettings, vars, data, Encoding.UTF8);
            bool result = evaluator(state, ref len, ref pos, out bool faulted);

            Parameter p;

            if (parameters.TryGetParameterDefinition(config.VariableName, out ITemplateParameter existingParam))
            {
                // If there is an existing parameter with this name, it must be reused so it can be referenced by name
                // for other processing, for example: if the parameter had value forms defined for creating variants.
                // When the param already exists, use its definition, but set IsVariable = true for consistency.
                p = (Parameter)existingParam;
                p.IsVariable = true;

                if (string.IsNullOrEmpty(p.DataType))
                {
                    p.DataType = config.DataType;
                }
            }
            else
            {
                p = new Parameter
                {
                    IsVariable = true,
                    Name = config.VariableName,
                    DataType = config.DataType
                };
            }

            vars[config.VariableName] = result.ToString();
            setter(p, result.ToString());
        }
    }
}
