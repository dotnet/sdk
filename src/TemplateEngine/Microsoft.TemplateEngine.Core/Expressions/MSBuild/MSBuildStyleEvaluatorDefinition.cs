// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Shared;
using Microsoft.TemplateEngine.Core.Util;

namespace Microsoft.TemplateEngine.Core.Expressions.MSBuild
{
    public class MSBuildStyleEvaluatorDefinition : SharedEvaluatorDefinition<MSBuildStyleEvaluatorDefinition, MSBuildStyleEvaluatorDefinition.Tokens>
    {
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
            Literal = 18
        }

        protected override bool DereferenceInLiterals => true;

        protected override string NullTokenValue => "null";

        protected override IOperatorMap<Operators, Tokens> GenerateMap() => new OperatorSetBuilder<Tokens>(XmlStyleConverters.XmlEncode, XmlStyleConverters.XmlDecode)
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
            .TypeConverter<MSBuildStyleEvaluatorDefinition>(CppStyleConverters.ConfigureConverters);

        protected override ITokenTrie GetSymbols(IProcessorState processor)
        {
            if (!TokenCache.TryGetValue(processor.Encoding, out ITokenTrie tokens))
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
    }
}
