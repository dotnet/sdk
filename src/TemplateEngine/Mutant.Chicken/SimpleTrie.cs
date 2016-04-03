using System;
using System.Collections.Generic;

namespace Mutant.Chicken
{
    public class SimpleTrie
    {
        private readonly Dictionary<byte, SimpleTrie> _map = new Dictionary<byte, SimpleTrie>();

        public SimpleTrie()
        {
            Index = -1;
        }

        public int Index { get; private set; }

        public int Length { get; private set; }

        public void AddToken(byte[] token, int index)
        {
            SimpleTrie current = this;

            Length = Math.Max(Length, token.Length);
            for (int i = 0; i < token.Length; ++i)
            {
                SimpleTrie child;
                if (!current._map.TryGetValue(token[i], out child))
                {
                    child = new SimpleTrie();
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
            int i = currentBufferPosition;
            SimpleTrie current = this;
            int index = -1;

            while (i < bufferLength)
            {
                if (!current._map.TryGetValue(buffer[i], out current))
                {
                    token = index;

                    if (index != -1)
                    {
                        currentBufferPosition = i;
                        token = index;
                        return true;
                    }

                    return false;
                }

                index = current.Index;
                ++i;
            }

            if (index != -1)
            {
                currentBufferPosition = i;
                token = index;
                return true;
            }

            token = -1;
            return false;
        }
    }
}