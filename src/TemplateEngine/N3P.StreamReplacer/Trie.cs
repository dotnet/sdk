using System;
using System.Collections.Generic;

namespace N3P.StreamReplacer
{
    internal class Trie
    {
        private readonly Dictionary<byte, Trie> _map = new Dictionary<byte, Trie>();

        private Trie()
        {
        }

        public int Length { get; private set; }

        public IOperation End { get; private set; }

        public static Trie Create(IOperation[] modifiers)
        {
            Trie root = new Trie();
            Trie current = root;
            int length = 0;

            for (int i = 0; i < modifiers.Length; ++i)
            {
                for (int k = 0; k < modifiers[i].Tokens.Count; ++k)
                {
                    length = Math.Max(length, modifiers[i].Tokens[k].Length);
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

            root.Length = length;
            return root;
        }

        public int HandlerTokenIndex { get; private set; }

        public IOperation GetOperation(byte[] buffer, int bufferLength, ref int currentBufferPosition, out int token)
        {
            int i = currentBufferPosition;
            Trie current = this;

            while (current.End == null && i < bufferLength)
            {
                if (!current._map.TryGetValue(buffer[i], out current))
                {
                    token = 0;
                    return null;
                }

                ++i;
            }

            token = current.HandlerTokenIndex;
            currentBufferPosition = i;
            return current.End;
        }
    }
}
