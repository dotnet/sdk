// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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

    internal enum EvaluatorType
    {
        CPP2,
        CPP,
        MSBuild,
        VB
    }

    internal static class EvaluatorSelector
    {
        internal static ConditionStringEvaluator SelectStringEvaluator(string? name, EvaluatorType? @default = null)
        {
            EvaluatorType evaluatorType = ParseEvaluatorName(name, @default);
            ConditionStringEvaluator evaluator = evaluatorType switch
            {
                EvaluatorType.CPP2 => Cpp2StyleEvaluatorDefinition.EvaluateFromString,
                EvaluatorType.CPP => CppStyleEvaluatorDefinition.EvaluateFromString,
                EvaluatorType.MSBuild => MSBuildStyleEvaluatorDefinition.EvaluateFromString,
                EvaluatorType.VB => VisualBasicStyleEvaluatorDefintion.EvaluateFromString,
                _ => throw new NotSupportedException($"{evaluatorType} is not supported.")
            };
            return evaluator;
        }

        internal static EvaluatorType ParseEvaluatorName(string? name, EvaluatorType? @default = null)
        {
            EvaluatorType defaultType = @default ?? EvaluatorType.CPP;
            string evaluatorName = name ?? string.Empty;
            EvaluatorType evaluator = evaluatorName switch
            {
                "" => defaultType,
                "C++2" => EvaluatorType.CPP2,
                "C++" => EvaluatorType.CPP,
                "MSBUILD" => EvaluatorType.MSBuild,
                "VB" => EvaluatorType.VB,
                _ => throw new TemplateAuthoringException($"Unrecognized evaluator: '{evaluatorName}'.", evaluatorName),
            };
            return evaluator;
        }

        internal static ConditionEvaluator Select(EvaluatorType evaluatorType)
        {
            ConditionEvaluator evaluator = evaluatorType switch
            {
                EvaluatorType.CPP2 => Cpp2StyleEvaluatorDefinition.Evaluate,
                EvaluatorType.CPP => CppStyleEvaluatorDefinition.Evaluate,
                EvaluatorType.MSBuild => MSBuildStyleEvaluatorDefinition.Evaluate,
                EvaluatorType.VB => VisualBasicStyleEvaluatorDefintion.Evaluate,
                _ => throw new NotSupportedException($"{evaluatorType} is not supported.")
            };
            return evaluator;
        }
    }
}
