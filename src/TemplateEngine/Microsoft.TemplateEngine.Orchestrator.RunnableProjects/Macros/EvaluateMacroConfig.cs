// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros
{
    internal class EvaluateMacroConfig : BaseMacroConfig<EvaluateMacro, EvaluateMacroConfig>, IMacroConfigDependency
    {
        private const EvaluatorType DefaultEvaluator = EvaluatorType.CPP2;
        private static readonly EvaluateMacro DefaultMacro = new();

        internal EvaluateMacroConfig(string variableName, string? dataType, string condition, string? evaluator = null)
             : base(DefaultMacro, variableName, dataType)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                throw new ArgumentException($"'{nameof(condition)}' cannot be null or whitespace.", nameof(condition));
            }

            Condition = condition;
            if (!string.IsNullOrEmpty(evaluator))
            {
                Evaluator = EvaluatorSelector.SelectStringEvaluator(EvaluatorSelector.ParseEvaluatorName(evaluator, DefaultEvaluator));
            }
        }

        internal string Condition { get; private set; }

        internal ConditionStringEvaluator Evaluator { get; private set; } = EvaluatorSelector.SelectStringEvaluator(DefaultEvaluator);

        public void ResolveSymbolDependencies(IReadOnlyList<string> symbols)
        {
            MacroDependenciesResolved = true;
            PopulateMacroConfigDependencies(Condition, symbols);
        }
    }
}
