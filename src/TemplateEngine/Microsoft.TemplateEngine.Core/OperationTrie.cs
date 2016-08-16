using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.Engine;

namespace Microsoft.TemplateEngine.Core
{
    internal class OperationTrie
    {
        private readonly Dictionary<byte, OperationTrie> _map = new Dictionary<byte, OperationTrie>();

        private OperationTrie()
        {
            HandlerTokenIndex = -1;
        }

        public int MaxLength { get; private set; }

        public int MinLength { get; private set; }

        public IOperation End { get; private set; }

        public static OperationTrie Create(IReadOnlyList<IOperation> modifiers)
        {
            OperationTrie root = new OperationTrie();
            OperationTrie current = root;
            int length = 0;
            int minLength = 0;

            for (int i = 0; i < modifiers.Count; ++i)
            {
                for (int k = 0; k < modifiers[i].Tokens.Count; ++k)
                {
                    if (modifiers[i].Tokens[k] == null)
                    {
                        continue;
                    }

                    length = Math.Max(length, modifiers[i].Tokens[k].Length);

                    if (modifiers[i].Tokens[k].Length > 0)
                    {
                        minLength = minLength == 0
                            ? modifiers[i].Tokens[k].Length
                            : Math.Min(minLength, modifiers[i].Tokens[k].Length);
                    }

                    for (int j = 0; j < modifiers[i].Tokens[k].Length; ++j)
                    {
                        OperationTrie child;
                        if (!current._map.TryGetValue(modifiers[i].Tokens[k][j], out child))
                        {
                            child = new OperationTrie();
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
            OperationTrie current = this;
            IOperation operation = null;
            int index = -1;
            int offsetToMatch = 0;

            while (i < bufferLength)
            {
                if (!current._map.TryGetValue(buffer[i], out current))
                {   // the current byte doesn't match in the context of the current trie state
                    token = index;

                    if (index != -1)
                    {   // a full token match was encountered along the way - return its operation
                        currentBufferPosition = i - offsetToMatch;
                        return operation;
                    }

                    // no token match
                    return null;
                }

                if (current.HandlerTokenIndex != -1)
                {   // a full token was matched, note its operation
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
            {   // matched to the end of the buffer
                currentBufferPosition = i;
            }

            // end of buffer, but a full token matched along the way - return its operation
            token = index;
            return operation;
        }
    }
}
