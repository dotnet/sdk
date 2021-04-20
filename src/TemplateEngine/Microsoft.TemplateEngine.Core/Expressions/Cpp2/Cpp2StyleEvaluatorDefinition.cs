// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.Core.Expressions.Shared;

namespace Microsoft.TemplateEngine.Core.Expressions.Cpp2
{
    public class Cpp2StyleEvaluatorDefinition : SharedEvaluatorDefinition<Cpp2StyleEvaluatorDefinition, Cpp2StyleEvaluatorDefinition.Tokens>
    {
        protected override IOperatorMap<Operators, Tokens> GenerateMap() => new OperatorSetBuilder<Tokens>(CppStyleConverters.Encode, CppStyleConverters.Decode)
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
            .TypeConverter<Cpp2StyleEvaluatorDefinition>(CppStyleConverters.ConfigureConverters);

        private static readonly Dictionary<Encoding, ITokenTrie> TokenCache = new Dictionary<Encoding, ITokenTrie>();

        protected override bool DereferenceInLiterals => false;

        protected override string NullTokenValue => "null";

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
            Literal = 27
        }

        protected override ITokenTrie GetSymbols(IProcessorState processor)
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
    }
}
