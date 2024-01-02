// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Core.Expressions.Shared
{
    public abstract class SharedEvaluatorDefinition<TSelf, TTokens>
        where TSelf : SharedEvaluatorDefinition<TSelf, TTokens>, new()
        where TTokens : struct
    {
        private static readonly TSelf Instance = new();
        private static readonly IOperatorMap<Operators, TTokens> Map = Instance.GenerateMap();
        private static readonly bool DereferenceInLiteralsSetting = Instance.DereferenceInLiterals;
        private static readonly string NullToken = Instance.NullTokenValue;
        private static readonly IOperationProvider[] NoOperationProviders = Array.Empty<IOperationProvider>();

        protected abstract string NullTokenValue { get; }

        protected abstract bool DereferenceInLiterals { get; }

        public static bool Evaluate(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, out bool faulted)
        {
            bool result = Evaluate(processor, ref bufferLength, ref currentBufferPosition, out string? faultedMessage, null, false);
            faulted = !string.IsNullOrEmpty(faultedMessage);
            return result;
        }

        /// <summary>
        /// Inspect the passed string, creates the expression, substitutes parameters within expression, evaluates substituted expression and returns result.
        /// If non-null bag for variable references is passed, it will be populated with references of variables used within the evaluable expression.
        /// </summary>
        /// <param name="logger">The logger to be used to log the messages during evaluation.</param>
        /// <param name="text">The string to be inspected and turned into expression.</param>
        /// <param name="variables">Variables to be substituted within the expression.</param>
        /// <returns>A boolean value indicating the result of the evaluation.</returns>
        public static bool EvaluateFromString(ILogger logger, string text, IVariableCollection variables)
        {
            return EvaluateFromString(logger, text, variables, out string? _, null);
        }

        /// <summary>
        /// Inspect the passed string, creates the expression, substitutes parameters within expression, evaluates substituted expression and returns result.
        /// If non-null bag for variable references is passed, it will be populated with references of variables used within the evaluable expression.
        /// </summary>
        /// <param name="logger">The logger to be used to log the messages during evaluation.</param>
        /// <param name="text">The string to be inspected and turned into expression.</param>
        /// <param name="variables">Variables to be substituted within the expression.</param>
        /// <param name="faultedMessage">Error message detailing failing evaluation, should it fail.</param>
        /// <param name="referencedVariablesKeys">If passed (if not null) it will be populated with references to variables used within the inspected expression.</param>
        /// <returns>A boolean value indicating the result of the evaluation.</returns>
        public static bool EvaluateFromString(ILogger logger, string text, IVariableCollection variables, out string? faultedMessage, HashSet<string>? referencedVariablesKeys = null)
        {
            using (MemoryStream ms = new(Encoding.UTF8.GetBytes(text)))
            using (MemoryStream res = new())
            {
                EngineConfig cfg = new(logger, variables);
                IProcessorState state = new ProcessorState(ms, res, (int)ms.Length, (int)ms.Length, cfg, NoOperationProviders);
                int len = (int)ms.Length;
                int pos = 0;
                return Evaluate(state, ref len, ref pos, out faultedMessage, referencedVariablesKeys, true);
            }
        }

        /// <summary>
        /// Creates the evaluable expression based on passed string,
        /// collects used symbols in the expression and reports if any errors occurs on expression creation.
        /// </summary>
        /// <param name="logger">The logger to be used to log the messages during building the evaluable expression.</param>
        /// <param name="text">The string to be inspected and turned into expression.</param>
        /// <param name="variables">Variables to be substituted within the expression.</param>
        /// <param name="evaluableExpressionError">Error message detailing failing building evaluable expression.</param>
        /// <param name="referencedVariablesKeys">If passed (if not null) it will be populated with references to variables used within the inspected expression.</param>
        /// <returns>Evaluable expression that represents decomposed <paramref name="text"></paramref>.</returns>
        public static IEvaluable? GetEvaluableExpression(
            ILogger logger,
            string text,
            IVariableCollection variables,
            out string? evaluableExpressionError,
            HashSet<string> referencedVariablesKeys)
        {
            using (MemoryStream ms = new(Encoding.UTF8.GetBytes(text)))
            using (MemoryStream res = new())
            {
                EngineConfig cfg = new(logger, variables);
                IProcessorState state = new ProcessorState(ms, res, (int)ms.Length, (int)ms.Length, cfg, NoOperationProviders);
                int len = (int)ms.Length;
                int pos = 0;

                return GetEvaluableExpression(state, ref len, ref pos, out evaluableExpressionError, referencedVariablesKeys);
            }
        }

        protected static int Compare(object? left, object? right)
        {
            if (Equals(right, NullToken))
            {
                right = null;
            }

            if (Equals(left, NullToken))
            {
                left = null;
            }

            return AttemptNumericComparison(left, right)
                   ?? AttemptBooleanComparison(left, right)
                   ?? AttemptVersionComparison(left, right)
                   ?? AttemptMultiValueComparison(left, right)
                   ?? AttemptLexicographicComparison(left, right)
                   ?? AttemptComparableComparison(left, right)
                   ?? 0;
        }

        protected abstract IOperatorMap<Operators, TTokens> GenerateMap();

        protected abstract ITokenTrie GetSymbols(IProcessorState processor);

        private static IEvaluable? GetEvaluableExpression(
            IProcessorState processor,
            ref int bufferLength,
            ref int currentBufferPosition,
            out string? faultedMessage,
            HashSet<string> referencedVariablesKeys)
        {
            faultedMessage = null;
            ITokenTrie tokens = Instance.GetSymbols(processor);
            ScopeBuilder<Operators, TTokens> builder = processor.ScopeBuilder(tokens, Map, DereferenceInLiteralsSetting);
            string? faultedSection = null;

            return builder.Build(
                ref bufferLength,
                ref currentBufferPosition,
                x => faultedSection = processor.Encoding.GetString(x.ToArray()),
                referencedVariablesKeys);
        }

        private static bool Evaluate(
            IProcessorState processor,
            ref int bufferLength,
            ref int currentBufferPosition,
            out string? faultedMessage,
            HashSet<string>? referencedVariablesKeys,
            // indicates whether passed buffer within processor contains only the analyzed expression,
            //  or it can possibly contain other content (e.g. the full template)
            bool shouldProcessWholeBuffer)
        {
            string? faultedSection = null;

            IEvaluable? expression = GetEvaluableExpression(
                processor,
                ref bufferLength,
                ref currentBufferPosition,
                out faultedMessage,
                referencedVariablesKeys ?? new HashSet<string>());

            bool result;
            if (faultedSection != null)
            {
                faultedMessage = faultedSection;
                result = false;
            }
            else
            {
                // Buffer continues after expression - let's populate error only if this is single expression evaluation
                //  as we want to avoid creation of message that would contain whole template content after some condition
                if (shouldProcessWholeBuffer && bufferLength != 0)
                {
                    faultedMessage = LocalizableStrings.Error_Evaluation_Expression_Substring +
                                     processor.Encoding.GetString(
                                         processor.CurrentBuffer,
                                         currentBufferPosition,
                                         bufferLength - currentBufferPosition);
                }

                try
                {
                    object? evalResult = expression?.Evaluate();
                    result = (bool)Convert.ChangeType(evalResult, typeof(bool));
                }
                catch (Exception e)
                {
                    faultedMessage = faultedMessage == null
                        ? e.Message
                        : (faultedMessage + Environment.NewLine + e.Message);
                    result = false;
                }
            }

            if (!string.IsNullOrEmpty(faultedMessage))
            {
                processor.Config.Logger.LogDebug(LocalizableStrings.Error_Evaluation_Expression + faultedMessage);
            }
            return result;
        }

        private static int? AttemptBooleanComparison(object? left, object? right)
        {
            bool leftIsBool = Map.TryConvert(left!, out bool lb);
            bool rightIsBool = Map.TryConvert(right!, out bool rb);

            if (!leftIsBool || !rightIsBool)
            {
                return null;
            }

            return lb.CompareTo(rb);
        }

        private static int? AttemptComparableComparison(object? left, object? right)
        {
            if (left is not IComparable ls || right is not IComparable rs)
            {
                return null;
            }

            return ls.CompareTo(rs);
        }

        private static int? AttemptMultiValueComparison(object? left, object? right)
        {
            if (MultiValueParameter.TryPerformMultiValueEqual(left!, right!, out bool result))
            {
                return result ? 0 : -1;
            }

            return null;
        }

        private static int? AttemptLexicographicComparison(object? left, object? right)
        {
            if (left is not string ls || right is not string rs)
            {
                return null;
            }

            return string.Compare(ls, rs, StringComparison.OrdinalIgnoreCase);
        }

        private static int? AttemptNumericComparison(object? left, object? right)
        {
            bool leftIsDouble = Map.TryConvert(left!, out double ld);
            bool rightIsDouble = Map.TryConvert(right!, out double rd);

            if (!leftIsDouble)
            {
                if (!Map.TryConvert(left!, out long ll))
                {
                    return null;
                }

                ld = ll;
            }

            if (!rightIsDouble)
            {
                if (!Map.TryConvert(right!, out long rl))
                {
                    return null;
                }

                rd = rl;
            }

            return ld.CompareTo(rd);
        }

        private static int? AttemptVersionComparison(object? left, object? right)
        {
            Version? lv = left as Version;

            if (lv == null)
            {
                if (left is not string ls || !Version.TryParse(ls, out lv))
                {
                    return null;
                }
            }

            Version? rv = right as Version;

            if (rv == null)
            {
                if (right is not string rs || !Version.TryParse(rs, out rv))
                {
                    return null;
                }
            }

            return lv.CompareTo(rv);
        }
    }
}
