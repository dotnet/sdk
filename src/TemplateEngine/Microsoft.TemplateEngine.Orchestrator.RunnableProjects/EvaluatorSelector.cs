// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
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
        /// <summary>
        /// C++ V2 style evaluator, see <see cref="Cpp2StyleEvaluatorDefinition"/> for more details.
        /// </summary>
        CPP2,

        /// <summary>
        /// C++ style evaluator, see <see cref="CppStyleEvaluatorDefinition"/> for more details.
        /// </summary>
        CPP,

        /// <summary>
        /// MSBuild style evaluator, see <see cref="MSBuildStyleEvaluatorDefinition"/> for more details.
        /// </summary>
        MSBuild,

        /// <summary>
        /// VB style evaluator, see <see cref="VisualBasicStyleEvaluatorDefintion"/> for more details.
        /// </summary>
        VB
    }

    internal static class EvaluatorSelector
    {
        /// <summary>
        /// Gets <see cref="ConditionStringEvaluator"/> based on selected <paramref name="evaluatorType"/>.
        /// </summary>
        /// <param name="evaluatorType">The evaluator type to use. See <see cref=" EvaluatorType"/> for more info.</param>
        /// <returns><see cref="ConditionStringEvaluator"/>.</returns>
        /// <exception cref="NotSupportedException">when <paramref name="evaluatorType"/> is not supported (unknown).</exception>
        internal static ConditionStringEvaluator SelectStringEvaluator(EvaluatorType evaluatorType)
        {
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

        /// <summary>
        /// Parses <paramref name="name"/> to <see cref="EvaluatorType"/>.
        /// </summary>
        /// <param name="name">the string value to parse.</param>
        /// <param name="default">default <see cref="EvaluatorType"/> to use, when the <paramref name="name"/> is null or empty.
        /// The parameter is optional. In case it is not passed or <see langword="null"/> is passed, <see cref="EvaluatorType.CPP"/> is used.</param>
        /// <returns>Parsed <see cref="EvaluatorType"/>.</returns>
        /// <exception cref="TemplateAuthoringException">when <paramref name="name"/> cannot is unknown and cannot be parsed.</exception>
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
                _ => throw new TemplateAuthoringException(string.Format(LocalizableStrings.EvaluatorSelector_Exception_UnknownEvaluator, evaluatorName)),
            };
            return evaluator;
        }

        /// <summary>
        /// Gets <see cref="ConditionEvaluator"/> based on selected <paramref name="evaluatorType"/>.
        /// </summary>
        /// <param name="evaluatorType">The evaluator type to use. See <see cref=" EvaluatorType"/> for more info.</param>
        /// <returns><see cref="ConditionEvaluator"/>.</returns>
        /// <exception cref="NotSupportedException">when <paramref name="evaluatorType"/> is not supported (unknown).</exception>
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
