using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Engine;

namespace Microsoft.TemplateEngine.Core
{
    public class TokenTrie : ITokenTrie
    {
        private readonly Dictionary<byte, TokenTrie> _map = new Dictionary<byte, TokenTrie>();
        private readonly List<int> _tokenLength;
        private readonly List<byte[]> _tokens;

        public TokenTrie()
        {
            Index = -1;
            _tokenLength = new List<int>();
            _tokens = new List<byte[]>();
        }

        public int Count { get; private set; }

        public int Index { get; private set; }

        public int MaxLength { get; private set; }

        public int MinLength { get; private set; }

        public IReadOnlyList<byte[]> Tokens => _tokens;

        public IReadOnlyList<int> TokenLength => _tokenLength;

        public int AddToken(byte[] token)
        {
            int result = Count;
            AddToken(token, result);
            return result;
        }

        public void AddToken(byte[] token, int index)
        {
            ++Count;
            TokenTrie current = this;
            _tokenLength.Add(token.Length);
            _tokens.Add(token);

            MaxLength = Math.Max(MaxLength, token.Length);

            MinLength = MinLength == 0
                ? token.Length
                : Math.Min(MinLength, token.Length);

            for (int i = 0; i < token.Length; ++i)
            {
                TokenTrie child;
                if (!current._map.TryGetValue(token[i], out child))
                {
                    child = new TokenTrie();
                    current._map[token[i]] = child;
                }

                if (i == token.Length - 1)
                {
                    child.Index = index;
                }

                current = child;
            }
        }

        public bool GetOperation(byte[] buffer, int bufferLength, ref int currentBufferPosition, out int token)
        {
            if (bufferLength < MinLength)
            {
                token = -1;
                return false;
            }

            int i = currentBufferPosition;
            TokenTrie current = this;
            int index = -1;
            int offsetToMatch = 0;

            while (i < bufferLength)
            {
                if (!current._map.TryGetValue(buffer[i], out current))
                {
                    token = index;

                    if (index != -1)
                    {
                        currentBufferPosition = i - offsetToMatch;
                        token = index;
                        return true;
                    }

                    return false;
                }

                if (current.Index != -1)
                {
                    index = current.Index;
                    offsetToMatch = 0;
                }
                else
                {
                    ++offsetToMatch;
                }

                ++i;
            }

            if (index != -1)
            {
                currentBufferPosition = i;
            }

            token = index;
            return index != -1;
        }

        public void Append(ITokenTrie trie)
        {
            foreach (byte[] token in trie.Tokens)
            {
                AddToken(token);
            }
        }
    }
}