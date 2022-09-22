// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp;
using Microsoft.TemplateEngine.Core.Expressions.Cpp2;
using Microsoft.TemplateEngine.Core.Expressions.MSBuild;
using Microsoft.TemplateEngine.Core.Expressions.VisualBasic;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects
{
    internal delegate bool ConditionStringEvaluator(ILogger logger, string text, IVariableCollection variables);

    internal static class EvaluatorSelector
    {
        internal static ConditionStringEvaluator SelectStringEvaluator(string? name, ConditionStringEvaluator? @default = null)
        {
            @default ??= CppStyleEvaluatorDefinition.EvaluateFromString;
            string evaluatorName = name ?? string.Empty;
            ConditionStringEvaluator evaluator = evaluatorName switch
            {
                "" => @default,
                "C++2" => Cpp2StyleEvaluatorDefinition.EvaluateFromString,
                "C++" => CppStyleEvaluatorDefinition.EvaluateFromString,
                "MSBUILD" => MSBuildStyleEvaluatorDefinition.EvaluateFromString,
                "VB" => VisualBasicStyleEvaluatorDefintion.EvaluateFromString,
                _ => throw new TemplateAuthoringException($"Unrecognized evaluator: '{evaluatorName}'.", evaluatorName),
            };
            return evaluator;
        }

        internal static ConditionEvaluator Select(string? name, ConditionEvaluator? @default = null)
        {
            @default ??= CppStyleEvaluatorDefinition.Evaluate;
            string evaluatorName = name ?? string.Empty;
            ConditionEvaluator evaluator = evaluatorName switch
            {
                "" => @default,
                "C++2" => Cpp2StyleEvaluatorDefinition.Evaluate,
                "C++" => CppStyleEvaluatorDefinition.Evaluate,
                "MSBUILD" => MSBuildStyleEvaluatorDefinition.Evaluate,
                "VB" => VisualBasicStyleEvaluatorDefintion.Evaluate,
                _ => throw new TemplateAuthoringException($"Unrecognized evaluator: '{evaluatorName}'.", evaluatorName),
            };
            return evaluator;
        }
    }
}
