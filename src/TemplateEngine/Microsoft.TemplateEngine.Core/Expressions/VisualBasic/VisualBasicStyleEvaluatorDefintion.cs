using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;

namespace Microsoft.TemplateEngine.Core.Expressions.VisualBasic
{
    public class VisualBasicStyleEvaluatorDefintion
    {
        private static readonly IOperatorMap<Operators, Tokens> Map = new OperatorSetBuilder<Tokens>(Encode, Decode)
            .And(Tokens.And)
            .And(Tokens.AndAlso)
            .Or(Tokens.Or)
            .Or(Tokens.OrElse)
            .Not(Tokens.Not)
            .Xor(Tokens.Xor)
            .GreaterThan(Tokens.GreaterThan, evaluate: (x, y) => Compare(x, y) > 0)
            .GreaterThanOrEqualTo(Tokens.GreaterThanOrEqualTo, evaluate: (x, y) => Compare(x, y) >= 0)
            .LessThan(Tokens.LessThan, evaluate: (x, y) => Compare(x, y) < 0)
            .LessThanOrEqualTo(Tokens.LessThanOrEqualTo, evaluate: (x, y) => Compare(x, y) <= 0)
            .EqualTo(Tokens.EqualTo, evaluate: (x, y) => Compare(x, y) == 0)
            .NotEqualTo(Tokens.NotEqualTo, evaluate: (x, y) => Compare(x, y) != 0)
            .Ignore(Tokens.Space, Tokens.Tab)
            .LiteralBoundsMarkers(Tokens.Quote)
            .OpenGroup(Tokens.OpenBrace)
            .CloseGroup(Tokens.CloseBrace)
            .TerminateWith(Tokens.WindowsEOL, Tokens.UnixEOL, Tokens.LegacyMacEOL)
            .LeftShift(Tokens.LeftShift)
            .RightShift(Tokens.RightShift)
            .Add(Tokens.Add)
            .Subtract(Tokens.Subtract)
            .Multiply(Tokens.Multiply)
            .Divide(Tokens.Divide)
            .Exponentiate(Tokens.Exponentiate)
            .Literal(Tokens.Literal)
            .LiteralBoundsMarkers(Tokens.DoubleQuote)
            .TypeConverter<VisualBasicStyleEvaluatorDefintion>(ConfigureConverters);

        private static readonly IOperationProvider[] NoOperationProviders = new IOperationProvider[0];

        private static readonly Dictionary<Encoding, ITokenTrie> TokenCache = new Dictionary<Encoding, ITokenTrie>();

        private enum Tokens
        {
            And = 0,
            AndAlso = 1,
            Or = 2,
            OrElse = 3,
            Not = 4,
            GreaterThan = 5,
            GreaterThanOrEqualTo = 6,
            LessThan = 7,
            LessThanOrEqualTo = 8,
            EqualTo = 9,
            NotEqualTo = 10,
            Xor = 11,
            OpenBrace = 12,
            CloseBrace = 13,
            Space = 14,
            Tab = 15,
            WindowsEOL = 16,
            UnixEOL = 17,
            LegacyMacEOL = 18,
            Quote = 19,
            LeftShift = 20,
            RightShift = 21,
            Add = 22,
            Subtract = 23,
            Multiply = 24,
            Divide = 25,
            Exponentiate = 26,
            DoubleQuote = 27,
            Literal = 28,
        }

        public static bool Evaluate(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, out bool faulted)
        {
            ITokenTrie tokens = GetSymbols(processor);
            ScopeBuilder<Operators, Tokens> builder = processor.ScopeBuilder(tokens, Map, true);
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

        private static int Compare(object left, object right)
        {
            //TODO: Make "null" token configurable
            if (Equals(right, "Nothing"))
            {
                right = null;
            }

            //TODO: Make "null" token configurable
            if (Equals(left, "Nothing"))
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

        private static void ConfigureConverters(ITypeConverter obj)
        {
            obj.Register((object o, out long r) =>
            {
                if (TryHexConvert(obj, o, out r))
                {
                    return true;
                }

                return obj.TryCoreConvert(o, out r);
            }).Register((object o, out int r) =>
            {
                if (TryHexConvert(obj, o, out r))
                {
                    return true;
                }

                return obj.TryCoreConvert(o, out r);
            });
        }

        private static string Decode(string arg)
        {
            return arg.Replace("\\\"", "\"").Replace("\\'", "'");
        }

        private static string Encode(string arg)
        {
            return arg.Replace("\"", "\\\"").Replace("'", "\\'");
        }

        private static ITokenTrie GetSymbols(IProcessorState processor)
        {
            if (!TokenCache.TryGetValue(processor.Encoding, out ITokenTrie tokens))
            {
                TokenTrie trie = new TokenTrie();

                //Logic
                trie.AddToken(processor.Encoding.GetBytes("And"));
                trie.AddToken(processor.Encoding.GetBytes("AndAlso"));
                trie.AddToken(processor.Encoding.GetBytes("Or"));
                trie.AddToken(processor.Encoding.GetBytes("OrElse"));
                trie.AddToken(processor.Encoding.GetBytes("Not"));
                trie.AddToken(processor.Encoding.GetBytes(">"));
                trie.AddToken(processor.Encoding.GetBytes(">="));
                trie.AddToken(processor.Encoding.GetBytes("<"));
                trie.AddToken(processor.Encoding.GetBytes("<="));
                trie.AddToken(processor.Encoding.GetBytes("="));
                trie.AddToken(processor.Encoding.GetBytes("<>"));
                trie.AddToken(processor.Encoding.GetBytes("Xor"));

                //Braces
                trie.AddToken(processor.Encoding.GetBytes("("));
                trie.AddToken(processor.Encoding.GetBytes(")"));

                //Whitespace
                trie.AddToken(processor.Encoding.GetBytes(" "));
                trie.AddToken(processor.Encoding.GetBytes("\t"));

                //EOLs
                trie.AddToken(processor.Encoding.GetBytes("\r\n"));
                trie.AddToken(processor.Encoding.GetBytes("\n"));
                trie.AddToken(processor.Encoding.GetBytes("\r"));

                // quotes
                trie.AddToken(processor.Encoding.GetBytes("'"));

                //Shifts
                trie.AddToken(processor.Encoding.GetBytes("<<"));
                trie.AddToken(processor.Encoding.GetBytes(">>"));

                //Maths
                trie.AddToken(processor.Encoding.GetBytes("+"));
                trie.AddToken(processor.Encoding.GetBytes("-"));
                trie.AddToken(processor.Encoding.GetBytes("*"));
                trie.AddToken(processor.Encoding.GetBytes("/"));
                trie.AddToken(processor.Encoding.GetBytes("^"));

                // quotes
                trie.AddToken(processor.Encoding.GetBytes("\""));

                TokenCache[processor.Encoding] = tokens = trie;
            }

            return tokens;
        }

        private static bool TryHexConvert(ITypeConverter obj, object source, out long result)
        {
            if (!obj.TryConvert(source, out string ls))
            {
                result = 0;
                return false;
            }

            if (ls.StartsWith("&H", StringComparison.OrdinalIgnoreCase) && long.TryParse(ls.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }

            result = 0;
            return false;
        }

        private static bool TryHexConvert(ITypeConverter obj, object source, out int result)
        {
            if (!obj.TryConvert(source, out string ls))
            {
                result = 0;
                return false;
            }

            if (ls.StartsWith("&H", StringComparison.OrdinalIgnoreCase) && int.TryParse(ls.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }

            result = 0;
            return false;
        }
    }
}
