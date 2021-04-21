// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.Matching
{
    internal class TriePath<T>
        where T : TerminalBase
    {
        public TriePath(int startSequenceNumber)
        {
            StartSequenceNumber = startSequenceNumber;
            EncounteredTerminals = new List<T>();
        }

        public List<T> EncounteredTerminals { get; }

        public int StartSequenceNumber { get; }

        public TrieNode<T> CurrentNode { get; set; }
    }
}
