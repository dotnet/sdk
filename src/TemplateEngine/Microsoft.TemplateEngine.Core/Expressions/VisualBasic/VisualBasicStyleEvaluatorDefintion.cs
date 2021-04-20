// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Shared;
using Microsoft.TemplateEngine.Core.Util;

namespace Microsoft.TemplateEngine.Core.Expressions.VisualBasic
{
    public class VisualBasicStyleEvaluatorDefintion : SharedEvaluatorDefinition<VisualBasicStyleEvaluatorDefintion, VisualBasicStyleEvaluatorDefintion.Tokens>
    {
        protected override IOperatorMap<Operators, Tokens> GenerateMap() => new OperatorSetBuilder<Tokens>(CppStyleConverters.Encode, CppStyleConverters.Decode)
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
            .TypeConverter<VisualBasicStyleEvaluatorDefintion>(VisualBasicStyleConverters.ConfigureConverters);

        private static readonly Dictionary<Encoding, ITokenTrie> TokenCache = new Dictionary<Encoding, ITokenTrie>();

        protected override bool DereferenceInLiterals => false;

        protected override string NullTokenValue => "Nothing";

        public enum Tokens
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

        protected override ITokenTrie GetSymbols(IProcessorState processor)
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
    }
}
