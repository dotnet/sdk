// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Core.Expressions.Shared
{
    public abstract class SharedEvaluatorDefinition<TSelf, TTokens>
        where TSelf : SharedEvaluatorDefinition<TSelf, TTokens>, new()
        where TTokens : struct
    {
        private static readonly TSelf s_instance = new TSelf();
        private static readonly IOperatorMap<Operators, TTokens> s_map = s_instance.GenerateMap();
        private static readonly bool s_dereferenceInLiteralsSetting = s_instance.DereferenceInLiterals;
        private static readonly string s_nullToken = s_instance.NullTokenValue;
        private static readonly IOperationProvider[] s_noOperationProviders = Array.Empty<IOperationProvider>();

        protected abstract string NullTokenValue { get; }

        protected abstract bool DereferenceInLiterals { get; }

        public static bool Evaluate(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, out bool faulted)
        {
            bool result = Evaluate(processor, ref bufferLength, ref currentBufferPosition, out string faultedMessage, null);
            faulted = !string.IsNullOrEmpty(faultedMessage);
            return result;
        }

        public static bool Evaluate(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, out string faultedMessage, HashSet<string> referencedVariablesKeys)
        {
            ITokenTrie tokens = s_instance.GetSymbols(processor);
            ScopeBuilder<Operators, TTokens> builder = processor.ScopeBuilder(tokens, s_map, s_dereferenceInLiteralsSetting);
            string faultedSection = null;
            IEvaluable result = builder.Build(
                ref bufferLength,
                ref currentBufferPosition,
                x => faultedSection = Encoding.UTF8.GetString(x.ToArray()),
                referencedVariablesKeys);

            if (faultedSection != null)
            {
                faultedMessage = faultedSection;
                return false;
            }

            try
            {
                object evalResult = result.Evaluate();
                bool r = (bool)Convert.ChangeType(evalResult, typeof(bool));
                faultedMessage = null;
                return r;
            }
            catch (Exception e)
            {
                faultedMessage = e.Message;
                return false;
            }
        }

        /// <summary>
        /// Inspect the passed string, creates the expression, substitutes parameters within expression, evaluates substituted expression and returns result.
        /// If non-null bag for variable references is passed, it will be populated with references of variables used within the evaluable expression.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="text">The string to be inspected and turned into expression.</param>
        /// <param name="variables">Variables to be substituted within the expression.</param>
        /// <param name="referencedVariablesKeys">If passed (if not null) it will be populated with references to variables used within the inspected expression.</param>
        /// <returns></returns>
        public static bool EvaluateFromString(ILogger logger, string text, IVariableCollection variables)
        {
            return EvaluateFromString(logger, text, variables, out string _, null);
        }

        /// <summary>
        /// Inspect the passed string, creates the expression, substitutes parameters within expression, evaluates substituted expression and returns result.
        /// If non-null bag for variable references is passed, it will be populated with references of variables used within the evaluable expression.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="text">The string to be inspected and turned into expression.</param>
        /// <param name="variables">Variables to be substituted within the expression.</param>
        /// <param name="faultedMessage">Error message detailing failing evaluation, should it fail.</param>
        /// <param name="referencedVariablesKeys">If passed (if not null) it will be populated with references to variables used within the inspected expression.</param>
        /// <returns></returns>
        public static bool EvaluateFromString(ILogger logger, string text, IVariableCollection variables, out string faultedMessage, HashSet<string> referencedVariablesKeys = null)
        {
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            using (MemoryStream res = new MemoryStream())
            {
                EngineConfig cfg = new EngineConfig(logger, variables);
                IProcessorState state = new ProcessorState(ms, res, (int)ms.Length, (int)ms.Length, cfg, s_noOperationProviders);
                int len = (int)ms.Length;
                int pos = 0;
                return Evaluate(state, ref len, ref pos, out faultedMessage, referencedVariablesKeys);
            }
        }

        protected static int Compare(object left, object right)
        {
            if (Equals(right, s_nullToken))
            {
                right = null;
            }

            if (Equals(left, s_nullToken))
            {
                left = null;
            }

            return AttemptNumericComparison(left, right)
                   ?? AttemptBooleanComparison(left, right)
                   ?? AttemptVersionComparison(left, right)
                   ?? AttemptMultiValueComparison(left, right)
                   ?? AttemptLexographicComparison(left, right)
                   ?? AttemptComparableComparison(left, right)
                   ?? 0;
        }

        protected abstract IOperatorMap<Operators, TTokens> GenerateMap();

        protected abstract ITokenTrie GetSymbols(IProcessorState processor);

        private static int? AttemptBooleanComparison(object left, object right)
        {
            bool leftIsBool = s_map.TryConvert(left, out bool lb);
            bool rightIsBool = s_map.TryConvert(right, out bool rb);

            if (!leftIsBool || !rightIsBool)
            {
                return null;
            }

            return lb.CompareTo(rb);
        }

        private static int? AttemptComparableComparison(object left, object right)
        {
            if (left is not IComparable ls || right is not IComparable rs)
            {
                return null;
            }

            return ls.CompareTo(rs);
        }

        private static int? AttemptMultiValueComparison(object left, object right)
        {
            if (MultiValueParameter.TryPerformMultiValueEqual(left, right, out bool result))
            {
                return result ? 0 : -1;
            }

            return null;
        }

        private static int? AttemptLexographicComparison(object left, object right)
        {
            if (left is not string ls || right is not string rs)
            {
                return null;
            }

            return string.Compare(ls, rs, StringComparison.OrdinalIgnoreCase);
        }

        private static int? AttemptNumericComparison(object left, object right)
        {
            bool leftIsDouble = s_map.TryConvert(left, out double ld);
            bool rightIsDouble = s_map.TryConvert(right, out double rd);

            if (!leftIsDouble)
            {
                if (!s_map.TryConvert(left, out long ll))
                {
                    return null;
                }

                ld = ll;
            }

            if (!rightIsDouble)
            {
                if (!s_map.TryConvert(right, out long rl))
                {
                    return null;
                }

                rd = rl;
            }

            return ld.CompareTo(rd);
        }

        private static int? AttemptVersionComparison(object left, object right)
        {
            Version lv = left as Version;

            if (lv == null)
            {
                if (left is not string ls || !Version.TryParse(ls, out lv))
                {
                    return null;
                }
            }

            Version rv = right as Version;

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
