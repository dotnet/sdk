using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Shared;
using Microsoft.TemplateEngine.Core.Util;

namespace Microsoft.TemplateEngine.Core.Expressions.MSBuild
{
    public class MSBuildStyleEvaluatorDefinition : SharedEvaluatorDefinition<MSBuildStyleEvaluatorDefinition, MSBuildStyleEvaluatorDefinition.Tokens>
    {
        protected override IOperatorMap<Operators, Tokens> GenerateMap() => new OperatorSetBuilder<Tokens>(XmlEncode, XmlDecode)
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
            .Literal(Tokens.Literal)
            .TypeConverter<MSBuildStyleEvaluatorDefinition>(ConfigureConverters);

        private static readonly IOperationProvider[] NoOperationProviders = new IOperationProvider[0];

        private static readonly Dictionary<Encoding, ITokenTrie> TokenCache = new Dictionary<Encoding, ITokenTrie>();

        public enum Tokens
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

        protected override bool DereferenceInLiterals => true;

        protected override string NullTokenValue => "null";

        protected override ITokenTrie GetSymbols(IProcessorState processor)
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
