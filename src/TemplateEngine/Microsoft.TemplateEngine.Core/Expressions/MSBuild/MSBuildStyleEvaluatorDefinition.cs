using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;

namespace Microsoft.TemplateEngine.Core.Expressions.MSBuild
{
    public class MSBuildStyleEvaluatorDefinition
    {
        private static readonly IOperatorMap<Operators, Tokens> Map = new OperatorSetBuilder<Tokens>(XmlEncode, XmlDecode)
            .And(Tokens.And)
            .Or(Tokens.Or)
            .Not(Tokens.Not)
            .GreaterThan(Tokens.GreaterThan, evaluate: (x, y) => Compare(x, y) > 0)
            .GreaterThanOrEqualTo(Tokens.GreaterThanOrEqualTo, evaluate: (x, y) => Compare(x, y) >= 0)
            .LessThan(Tokens.LessThan, evaluate: (x, y) => Compare(x, y) < 0)
            .LessThanOrEqualTo(Tokens.LessThanOrEqualTo, evaluate: (x, y) => Compare(x, y) <= 0)
            .EqualTo(Tokens.EqualTo, evaluate: (x, y) => Compare(x, y) == 0)
            .NotEqualTo(Tokens.NotEqualTo, evaluate: (x, y) => Compare(x, y) != 0)
            .BadSyntax(Tokens.VariableStart)
            .Ignore(Tokens.Space, Tokens.Tab)
            .LiteralBoundsMarkers(Tokens.Quote)
            .OpenGroup(Tokens.OpenBrace)
            .CloseGroup(Tokens.CloseBrace)
            .TerminateWith(Tokens.WindowsEOL, Tokens.UnixEOL, Tokens.LegacyMacEOL)
            .Literal(Tokens.Literal);

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
            VariableStart = 17,
            Literal = 18,
        }

        public static bool EvaluateFromString(IEngineEnvironmentSettings environmentSettings, string text, IVariableCollection variables)
        {
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            using (MemoryStream res = new MemoryStream())
            {
                EngineConfig cfg = new EngineConfig(environmentSettings, variables);
                IProcessorState state = new ProcessorState(ms, res, (int) ms.Length, (int) ms.Length, cfg, NoOperationProviders);
                int len = (int) ms.Length;
                int pos = 0;
                bool faulted;
                return Evaluate(state, ref len, ref pos, out faulted);
            }
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
            bool leftIsDouble = left is double;
            bool rightIsDouble = right is double;
            double ld = leftIsDouble ? (double)left : 0;
            double rd = rightIsDouble ? (double)right : 0;

            if (!leftIsDouble)
            {
                string ls = left as string;

                if (ls != null)
                {
                    int lh;
                    if (double.TryParse(ls, out ld))
                    {
                    }
                    else if (ls.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(ls.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out lh))
                    {
                        ld = lh;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            if (!rightIsDouble)
            {
                string rs = right as string;

                if (rs != null)
                {
                    int rh;
                    if (double.TryParse(rs, out rd))
                    {
                    }
                    else if (rs.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(rs.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rh))
                    {
                        rd = rh;
                    }
                    else
                    {
                        return null;
                    }
                }
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
            return AttemptNumericComparison(left, right)
                   ?? AttemptVersionComparison(left, right)
                   ?? AttemptLexographicComparison(left, right)
                   ?? AttemptComparableComparison(left, right)
                   ?? 0;
        }

        private static ITokenTrie GetSymbols(IProcessorState processor)
        {
            ITokenTrie tokens;
            if (!TokenCache.TryGetValue(processor.Encoding, out tokens))
            {
                TokenTrie trie = new TokenTrie();

                //Logic
                trie.AddToken(processor.Encoding.GetBytes("AND"));
                trie.AddToken(processor.Encoding.GetBytes("OR"));
                trie.AddToken(processor.Encoding.GetBytes("!"));
                trie.AddToken(processor.Encoding.GetBytes("&gt;"));
                trie.AddToken(processor.Encoding.GetBytes("&gt;="));
                trie.AddToken(processor.Encoding.GetBytes("&lt;"));
                trie.AddToken(processor.Encoding.GetBytes("&lt;="));
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

                // variable start
                trie.AddToken(processor.Encoding.GetBytes("$("));

                TokenCache[processor.Encoding] = tokens = trie;
            }

            return tokens;
        }

        private static string XmlDecode(string arg)
        {
            List<char> output = new List<char>();

            for (int i = 0; i < arg.Length; ++i)
            {
                //Not entity mode
                if (arg[i] != '&')
                {
                    output.Add(arg[i]);
                    continue;
                }

                ++i;
                //Entity mode, decimal or hex
                if (arg[i] == '#')
                {
                    ++i;

                    //Hex entity mode
                    if (arg[i] == 'x')
                    {
                        string hex = arg.Substring(i + 1, 4);
                        char c = (char)short.Parse(hex.TrimStart('0'), NumberStyles.HexNumber);
                        output.Add(c);
                        i += 5; //x, 4 digits, semicolon (consumed by the loop bound)
                    }
                    else
                    {
                        string dec = arg.Substring(i, 4);
                        char c = (char)short.Parse(dec.TrimStart('0'), NumberStyles.Integer);
                        output.Add(c);
                        i += 4; //4 digits, semicolon (consumed by the loop bound)
                    }
                }
                else
                {
                    switch (arg[i])
                    {
                        case 'q':
                            switch (arg[i + 1])
                            {
                                case 'u':
                                    switch (arg[i + 2])
                                    {
                                        case 'o':
                                            switch (arg[i + 3])
                                            {
                                                case 't':
                                                    switch (arg[i + 4])
                                                    {
                                                        case ';':
                                                            output.Add('"');
                                                            i += 4;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'a':
                            switch (arg[i + 1])
                            {
                                case 'm':
                                    switch (arg[i + 2])
                                    {
                                        case 'p':
                                            switch (arg[i + 3])
                                            {
                                                case ';':
                                                    output.Add('&');
                                                    i += 3;
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                                case 'p':
                                    switch (arg[i + 2])
                                    {
                                        case 'o':
                                            switch (arg[i + 3])
                                            {
                                                case 's':
                                                    switch (arg[i + 4])
                                                    {
                                                        case ';':
                                                            output.Add('\'');
                                                            i += 4;
                                                            break;
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'l':
                            switch (arg[i + 1])
                            {
                                case 't':
                                    switch (arg[i + 2])
                                    {
                                        case ';':
                                            output.Add('<');
                                            i += 2;
                                            break;
                                    }
                                    break;
                            }
                            break;
                        case 'g':
                            switch (arg[i + 1])
                            {
                                case 't':
                                    switch (arg[i + 2])
                                    {
                                        case ';':
                                            output.Add('>');
                                            i += 2;
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                }
            }

            string s = new string(output.ToArray());
            return s;
        }

        private static string XmlEncode(string arg)
        {
            return arg.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }
    }
}
