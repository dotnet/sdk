using System;
using System.Collections.Generic;

namespace Mutant.Chicken.Core
{
    internal class Trie
    {
        private readonly Dictionary<byte, Trie> _map = new Dictionary<byte, Trie>();

        private Trie()
        {
            HandlerTokenIndex = -1;
        }

        public int MaxLength { get; private set; }

        public int MinLength { get; private set; }

        public IOperation End { get; private set; }

        public static Trie Create(IOperation[] modifiers)
        {
            Trie root = new Trie();
            Trie current = root;
            int length = 0;
            int minLength = 0;

            for (int i = 0; i < modifiers.Length; ++i)
            {
                for (int k = 0; k < modifiers[i].Tokens.Count; ++k)
                {
                    length = Math.Max(length, modifiers[i].Tokens[k].Length);

                    minLength = minLength == 0
                        ? modifiers[i].Tokens[k].Length
                        : Math.Min(minLength, modifiers[i].Tokens[k].Length);

                    for (int j = 0; j < modifiers[i].Tokens[k].Length; ++j)
                    {
                        Trie child;
                        if (!current._map.TryGetValue(modifiers[i].Tokens[k][j], out child))
                        {
                            child = new Trie();
                            current._map[modifiers[i].Tokens[k][j]] = child;
                        }

                        current = child;
                    }

                    current.HandlerTokenIndex = k;
                    current.End = modifiers[i];
                    current = root;
                }
            }

            root.MaxLength = length;
            root.MinLength = minLength;
            return root;
        }

        public int HandlerTokenIndex { get; private set; }

        public IOperation GetOperation(byte[] buffer, int bufferLength, ref int currentBufferPosition, out int token)
        {
            //If a match couldn't fit in what's left of the buffer
            if (MinLength > bufferLength - currentBufferPosition)
            {
                token = -1;
                return null;
            }

            int i = currentBufferPosition;
            Trie current = this;
            IOperation operation = null;
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
                        return operation;
                    }

                    return null;
                }

                if (current.HandlerTokenIndex != -1)
                {
                    index = current.HandlerTokenIndex;
                    operation = current.End;
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
            return operation;
        }
    }
}
