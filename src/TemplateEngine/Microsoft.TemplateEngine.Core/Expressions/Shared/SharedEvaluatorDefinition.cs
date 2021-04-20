// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;

namespace Microsoft.TemplateEngine.Core.Expressions.Shared
{
    public abstract class SharedEvaluatorDefinition<TSelf, TTokens>
        where TSelf : SharedEvaluatorDefinition<TSelf, TTokens>, new()
        where TTokens : struct
    {
        private static readonly TSelf Instance = new TSelf();
        private static readonly IOperatorMap<Operators, TTokens> Map = Instance.GenerateMap();
        private static readonly bool DereferenceInLiteralsSetting = Instance.DereferenceInLiterals;
        private static readonly string NullToken = Instance.NullTokenValue;
        private static readonly IOperationProvider[] NoOperationProviders = Array.Empty<IOperationProvider>();

        protected abstract string NullTokenValue { get; }

        public static bool Evaluate(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, out bool faulted)
        {
            ITokenTrie tokens = Instance.GetSymbols(processor);
            ScopeBuilder<Operators, TTokens> builder = processor.ScopeBuilder(tokens, Map, DereferenceInLiteralsSetting);
            bool isFaulted = false;
            IEvaluable result = builder.Build(ref bufferLength, ref currentBufferPosition, x => isFaulted = true);

            if (isFaulted)
            {
                faulted = true;
                return false;
            }

            try
            {
                object evalResult = result.Evaluate();
                bool r = (bool)Convert.ChangeType(evalResult, typeof(bool));
                faulted = false;
                return r;
            }
            catch
            {
                faulted = true;
                return false;
            }
        }

        public static bool EvaluateFromString(IEngineEnvironmentSettings environmentSettings, string text, IVariableCollection variables)
        {
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            using (MemoryStream res = new MemoryStream())
            {
                EngineConfig cfg = new EngineConfig(environmentSettings, variables);
                IProcessorState state = new ProcessorState(ms, res, (int)ms.Length, (int)ms.Length, cfg, NoOperationProviders);
                int len = (int)ms.Length;
                int pos = 0;
                return Evaluate(state, ref len, ref pos, out bool faulted);
            }
        }

        protected abstract bool DereferenceInLiterals { get; }

        protected abstract IOperatorMap<Operators, TTokens> GenerateMap();

        protected abstract ITokenTrie GetSymbols(IProcessorState processor);

        private static int? AttemptBooleanComparison(object left, object right)
        {
            bool leftIsBool = Map.TryConvert(left, out bool lb);
            bool rightIsBool = Map.TryConvert(right, out bool rb);

            if (!leftIsBool || !rightIsBool)
            {
                return null;
            }

            return lb.CompareTo(rb);
        }

        private static int? AttemptComparableComparison(object left, object right)
        {
            IComparable ls = left as IComparable;
            IComparable rs = right as IComparable;

            if (ls == null || rs == null)
            {
                return null;
            }

            return ls.CompareTo(rs);
        }

        private static int? AttemptLexographicComparison(object left, object right)
        {
            string ls = left as string;
            string rs = right as string;

            if (ls == null || rs == null)
            {
                return null;
            }

            return string.Compare(ls, rs, StringComparison.OrdinalIgnoreCase);
        }

        private static int? AttemptNumericComparison(object left, object right)
        {
            bool leftIsDouble = Map.TryConvert(left, out double ld);
            bool rightIsDouble = Map.TryConvert(right, out double rd);

            if (!leftIsDouble)
            {
                if (!Map.TryConvert(left, out long ll))
                {
                    return null;
                }

                ld = ll;
            }

            if (!rightIsDouble)
            {
                if (!Map.TryConvert(right, out long rl))
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
                string ls = left as string;
                if (ls == null || !Version.TryParse(ls, out lv))
                {
                    return null;
                }
            }

            Version rv = right as Version;

            if (rv == null)
            {
                string rs = right as string;
                if (rs == null || !Version.TryParse(rs, out rv))
                {
                    return null;
                }
            }

            return lv.CompareTo(rv);
        }

        protected static int Compare(object left, object right)
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
                   ?? AttemptLexographicComparison(left, right)
                   ?? AttemptComparableComparison(left, right)
                   ?? 0;
        }
    }
}
