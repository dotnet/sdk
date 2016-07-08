using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions.Engine
{
    public interface ITokenTrie
    {
        int Count { get; }

        int Index { get; }

        int MaxLength { get; }

        int MinLength { get; }

        IReadOnlyList<int> TokenLength { get; }

        IReadOnlyList<byte[]> Tokens { get; }

        int AddToken(byte[] token);

        void AddToken(byte[] token, int index);

        bool GetOperation(byte[] buffer, int bufferLength, ref int currentBufferPosition, out int token);

        void Append(ITokenTrie trie);
    }
}