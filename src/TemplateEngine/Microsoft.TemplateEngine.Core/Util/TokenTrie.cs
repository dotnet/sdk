// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Matching;

namespace Microsoft.TemplateEngine.Core.Util
{
    public class TokenTrie : Trie<Token>, ITokenTrie
    {
        private List<IToken> _tokens = new List<IToken>();
        private List<int> _lengths = new List<int>();

        public int Count => _tokens.Count;

        public int MaxLength { get; private set; }

        public int MinLength { get; private set; } = int.MaxValue;

        public IReadOnlyList<int> TokenLength => _lengths;

        public IReadOnlyList<IToken> Tokens => _tokens;

        public int AddToken(byte[] literalToken)
        {
            return AddToken(TokenConfig.LiteralToken(literalToken));
        }

        public void AddToken(byte[] literalToken, int index)
        {
            AddToken(TokenConfig.LiteralToken(literalToken), index);
        }

        public int AddToken(IToken token)
        {
            int count = _tokens.Count;
            AddToken(token, count);
            return count;
        }

        public void AddToken(IToken token, int index)
        {
            _tokens.Add(token);
            _lengths.Add(token.Length);
            Token t = new Token(token.Value, index, token.Start, token.End);
            AddPath(token.Value, t);

            if (token.Value.Length > MaxLength)
            {
                MaxLength = token.Value.Length;
            }

            if (token.Value.Length < MinLength)
            {
                MinLength = token.Value.Length;
            }
        }

        public void Append(ITokenTrie trie)
        {
            foreach (IToken token in trie.Tokens)
            {
                AddToken(token);
            }
        }

        public ITokenTrieEvaluator CreateEvaluator()
        {
            return new TokenTrieEvaluator(this); 
        }

        public bool GetOperation(byte[] buffer, int bufferLength, ref int currentBufferPosition, out int token)
        {
            return GetOperation(buffer, bufferLength, ref currentBufferPosition, true, out token);
        }

        public bool GetOperation(byte[] buffer, int bufferLength, ref int currentBufferPosition, bool mustMatchPosition, out int token)
        {
            int originalPosition = currentBufferPosition;
            TrieEvaluator<Token> evaluator = new TrieEvaluator<Token>(this);
            TrieEvaluationDriver<Token> driver = new TrieEvaluationDriver<Token>(evaluator);

            if (mustMatchPosition)
            {
                bufferLength = Math.Min(bufferLength, currentBufferPosition + MaxLength);
            }

            TerminalLocation<Token> location = driver.Evaluate(buffer, bufferLength, true, 0, ref currentBufferPosition);

            if (location != null && (!mustMatchPosition || (currentBufferPosition - location.Terminal.Length == originalPosition)))
            {
                token = location.Terminal.Index;
                currentBufferPosition = location.Location + location.Terminal.End - location.Terminal.Start + 1;
                return true;
            }

            if (mustMatchPosition)
            {
                currentBufferPosition = originalPosition;
            }
            else
            {
                currentBufferPosition = bufferLength - MaxLength + 1;
            }

            token = -1;
            return false;
        }
    }
}
