using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;

namespace Microsoft.TemplateEngine.Core.Expressions.Cpp2
{
    public class Cpp2StyleEvaluatorDefinition
    {
        private static readonly IOperatorMap<Operators, Tokens> Map = new OperatorSetBuilder<Tokens>(Encode, Decode)
            .And(Tokens.And)
            .Or(Tokens.Or)
            .Not(Tokens.Not)
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
            .BitwiseAnd(Tokens.BitwiseAnd)
            .BitwiseOr(Tokens.BitwiseOr)
            .Literal(Tokens.Literal)
            .LiteralBoundsMarkers(Tokens.SingleQuote, Tokens.DoubleQuote)
            .TypeConverter<Cpp2StyleEvaluatorDefinition>(ConfigureConverters);

        private static readonly IOperationProvider[] NoOperationProviders = new IOperationProvider[0];

        private static readonly Dictionary<Encoding, ITokenTrie> TokenCache = new Dictionary<Encoding, ITokenTrie>();

        private enum Tokens
        {
            And = 0,
            Or = 1,
            Not = 2,
            GreaterThan = 3,
            GreaterThanOrEqualTo = 4,
            LessThan = 5,
            LessThanOrEqualTo = 6,
            EqualTo = 7,
            NotEqualTo = 8,
            OpenBrace = 9,
            CloseBrace = 10,
            Space = 11,
            Tab = 12,
            WindowsEOL = 13,
            UnixEOL = 14,
            LegacyMacEOL = 15,
            Quote = 16,
            LeftShift = 17,
            RightShift = 18,
            Add = 19,
            Subtract = 20,
            Multiply = 21,
            Divide = 22,
            BitwiseAnd = 23,
            BitwiseOr = 24,
            SingleQuote = 25,
            DoubleQuote = 26,
            Literal = 27,
        }

        public static bool Evaluate(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, out bool faulted)
        {
            ITokenTrie tokens = GetSymbols(processor);
            ScopeBuilder<Operators, Tokens> builder = processor.ScopeBuilder(tokens, Map, false);
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
            if (Equals(right, "null"))
            {
                right = null;
            }

            //TODO: Make "null" token configurable
            if (Equals(left, "null"))
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
                trie.AddToken(processor.Encoding.GetBytes("&&"));
                trie.AddToken(processor.Encoding.GetBytes("||"));
                trie.AddToken(processor.Encoding.GetBytes("!"));
                trie.AddToken(processor.Encoding.GetBytes(">"));
                trie.AddToken(processor.Encoding.GetBytes(">="));
                trie.AddToken(processor.Encoding.GetBytes("<"));
                trie.AddToken(processor.Encoding.GetBytes("<="));
                trie.AddToken(processor.Encoding.GetBytes("=="));
                trie.AddToken(processor.Encoding.GetBytes("!="));

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

                //Bitwise operators
                trie.AddToken(processor.Encoding.GetBytes("&"));
                trie.AddToken(processor.Encoding.GetBytes("|"));

                // quotes
                trie.AddToken(processor.Encoding.GetBytes("'"));
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

            if (ls.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && long.TryParse(ls.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result))
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

            if (ls.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(ls.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }

            result = 0;
            return false;
        }
    }
}
