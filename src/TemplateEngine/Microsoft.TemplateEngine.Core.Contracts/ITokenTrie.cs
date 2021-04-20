// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface ITokenTrie
    {
        int Count { get; }

        int MaxLength { get; }

        int MinLength { get; }

        IReadOnlyList<int> TokenLength { get; }

        IReadOnlyList<IToken> Tokens { get; }

        int AddToken(IToken token);

        void AddToken(IToken token, int index);

        bool GetOperation(byte[] buffer, int bufferLength, ref int currentBufferPosition, out int token);

        bool GetOperation(byte[] buffer, int bufferLength, ref int currentBufferPosition, bool mustMatchPosition, out int token);

        void Append(ITokenTrie trie);

        ITokenTrieEvaluator CreateEvaluator();
    }
}
